using System.Collections.Generic;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using NetworkToolsReworked.Edits;
using NetworkToolsReworked.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace NetworkToolsReworked.Undo
{
    /// <summary>
    /// Previews undoing the last edit made with these tools, applied on click or Apply. Undo goes through
    /// definitions like every other edit: the roads the edit left are removed and the roads it replaced
    /// are built again from their snapshots. Roads changed since by something else are left alone.
    /// Rolling back several edits undoes them one after another, waiting a few frames between them so
    /// each undo has landed before the next one looks for its roads.
    /// </summary>
    public partial class UndoToolSystem : NetEditToolSystem, IPreviewTool
    {
        private const float kMatchDistance = 0.1f;
        private const int kFramesBetweenSteps = 5;
        private ToolOverlay m_Overlay;
        private EntityQuery m_EdgeQuery;
        private EntityQuery m_NodeQuery;
        private Unity.Mathematics.Random m_Random;
        private UndoStep m_PlannedStep;
        private readonly List<Entity> m_ToRemove = new List<Entity>();
        private readonly List<(Entity start, Entity end)> m_Ends = new List<(Entity, Entity)>();
        private int m_Missing;
        private bool m_ApplyRequested;
        private bool m_CancelRequested;
        private int m_Remaining;
        private int m_Wait;

        // The step whose definitions were written last frame. Applying uses last frame's preview, so a
        // step is only applied once it has been previewed for a frame.
        private UndoStep m_PreviewedStep;

        public override string toolID => "NetworkToolsReworked.UndoTool";

        public ToolPhase Phase => ToolPhase.Review;

        public string Summary { get; private set; } = string.Empty;

        public void RequestApply() => m_ApplyRequested = true;

        public void RequestCancel() => m_CancelRequested = true;

        /// <summary>Undoes the last <paramref name="steps"/> edits in a row, without a click for each.</summary>
        public void RollBack(int steps)
        {
            m_Remaining = math.clamp(steps, 0, UndoHistory.Count);
            m_Wait = 0;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Overlay = new ToolOverlay(World);
            m_Random = new Unity.Mathematics.Random(0x0DD0u);
            m_EdgeQuery = GetEntityQuery(ComponentType.ReadOnly<Edge>(), ComponentType.ReadOnly<Curve>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
            m_NodeQuery = GetEntityQuery(ComponentType.ReadOnly<Node>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
        }

        /// <summary>History belongs to the loaded city; a new load starts it again.</summary>
        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            UndoHistory.Clear();
            m_PlannedStep = null;
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            m_PlannedStep = null;
            applyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            m_PlannedStep = null;
            m_Remaining = 0;
            applyAction.shouldBeEnabled = false;
            cancelAction.shouldBeEnabled = false;
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            m_ToolRaycastSystem.typeMask = TypeMask.Terrain;
        }

        public override PrefabBase GetPrefab() => null;

        public override bool TrySetPrefab(PrefabBase prefab) => false;

        protected override JobHandle OnToolUpdate(JobHandle inputDeps)
        {
            var applyRequested = m_ApplyRequested;
            var cancelRequested = m_CancelRequested;
            m_ApplyRequested = m_CancelRequested = false;

            if (cancelAction.WasPressedThisFrame() || cancelRequested)
            {
                m_Remaining = 0;
                m_ToolSystem.activeTool = m_DefaultToolSystem;
                return inputDeps;
            }

            applyMode = ApplyMode.Clear;
            UndoRecorder.Suspend();
            m_Overlay.BeginFrame();

            var previewed = m_PreviewedStep;
            m_PreviewedStep = null;
            if (m_Wait > 0)
            {
                m_Wait--;
                Summary = $"Rolling back: {m_Remaining} more to undo.";
                return inputDeps;
            }
            var auto = m_Remaining > 0;

            var step = UndoHistory.Peek();
            if (step == null)
            {
                Summary = "Nothing to undo.";
                m_Remaining = 0;
                return inputDeps;
            }

            if (step != m_PlannedStep)
                Plan(step);

            // Nothing the edit left is still there: the roads were changed since, so restoring would
            // build on top of something else.
            if (step.Results.Count > 0 && m_ToRemove.Count == 0)
            {
                Summary = "The last edit's roads were changed since, so it can't be undone. Press Apply to drop it from the history.";
                if (auto)
                {
                    m_Remaining = 0;
                    Summary = "Rolling back stopped: " + Summary;
                }
                if (applyAction.WasPressedThisFrame() || applyRequested)
                {
                    UndoHistory.Pop();
                    m_PlannedStep = null;
                }
                return inputDeps;
            }

            var ecb = DefinitionBuffer();
            var seed = m_Random.NextInt();
            foreach (var edge in m_ToRemove)
            {
                if (!EntityManager.Exists(edge) || EntityManager.HasComponent<Deleted>(edge))
                    continue;
                m_Overlay.Edge(edge, ToolOverlay.Invalid);
                NetDefinitions.DeleteEdge(EntityManager, ecb, edge, seed);
            }
            for (var i = 0; i < step.Originals.Count; i++)
            {
                var road = step.Originals[i];
                m_Overlay.Bezier(road.Curve, 2f, ToolOverlay.Start);
                Rebuild(ecb, road, m_Ends[i].start, m_Ends[i].end, seed);
            }

            var name = string.IsNullOrEmpty(step.Label) ? UndoRecorder.ToolName(step.Tool) : step.Label;
            var more = UndoHistory.Count > 1 ? $", {UndoHistory.Count - 1} more after this" : "";
            var missing = m_Missing > 0 ? $". {m_Missing} road(s) changed since and are left as they are" : "";
            Summary = $"Undo \"{name}\": remove {m_ToRemove.Count}, restore {step.Originals.Count} road(s){more}{missing}";
            if (auto)
                Summary = $"Rolling back ({m_Remaining} left). " + Summary;

            m_PreviewedStep = step;
            if ((applyAction.WasPressedThisFrame() || applyRequested || auto) && previewed == step)
            {
                m_PreviewedStep = null;
                applyMode = ApplyMode.Apply;
                if (Mod.Settings.DebugLogging)
                    Mod.Log.Info($"Undo applied for {step.Tool}: removed {m_ToRemove.Count}, restored {step.Originals.Count}");
                UndoHistory.Pop();
                if (m_Remaining > 0)
                    m_Remaining--;
                if (m_Remaining > 0)
                    m_Wait = kFramesBetweenSteps;
                else
                    m_ToolSystem.activeTool = m_DefaultToolSystem;
            }

            return inputDeps;
        }

        /// <summary>Finds the roads the step left behind and the existing nodes the restored roads attach to.</summary>
        private void Plan(UndoStep step)
        {
            m_PlannedStep = step;
            m_ToRemove.Clear();
            m_Ends.Clear();
            m_Missing = 0;

            using var edges = m_EdgeQuery.ToEntityArray(Allocator.Temp);
            using var curves = m_EdgeQuery.ToComponentDataArray<Curve>(Allocator.Temp);
            foreach (var result in step.Results)
            {
                var found = Entity.Null;
                for (var i = 0; i < curves.Length && found == Entity.Null; i++)
                    if (SameCurve(curves[i].m_Bezier, result) && !EntityManager.HasComponent<Owner>(edges[i]))
                        found = edges[i];
                if (found == Entity.Null)
                    m_Missing++;
                else if (!m_ToRemove.Contains(found))
                    m_ToRemove.Add(found);
            }

            using var nodes = m_NodeQuery.ToEntityArray(Allocator.Temp);
            using var nodeData = m_NodeQuery.ToComponentDataArray<Node>(Allocator.Temp);
            Entity NodeAt(float3 p)
            {
                for (var i = 0; i < nodeData.Length; i++)
                    if (math.distancesq(nodeData[i].m_Position, p) < kMatchDistance * kMatchDistance && !EntityManager.HasComponent<Owner>(nodes[i]))
                        return nodes[i];
                return Entity.Null;
            }

            foreach (var road in step.Originals)
                m_Ends.Add((NodeAt(road.Curve.a), NodeAt(road.Curve.d)));
        }

        private static bool SameCurve(Bezier4x3 a, Bezier4x3 b)
        {
            const float d2 = kMatchDistance * kMatchDistance;
            var forward = math.distancesq(a.a, b.a) < d2 && math.distancesq(a.d, b.d) < d2;
            var backward = math.distancesq(a.a, b.d) < d2 && math.distancesq(a.d, b.a) < d2;
            if (!forward && !backward)
                return false;
            return math.distancesq(MathUtils.Position(a, 0.5f), MathUtils.Position(b, 0.5f)) < 0.25f;
        }

        private static void Rebuild(EntityCommandBuffer ecb, RoadSnapshot road, Entity startNode, Entity endNode, int seed)
        {
            CoursePos End(Entity node, bool start)
            {
                var p = start ? road.Curve.a : road.Curve.d;
                var tangent = start ? MathUtils.StartTangent(road.Curve) : MathUtils.EndTangent(road.Curve);
                return new CoursePos
                {
                    m_Entity = node,
                    m_Position = p,
                    m_Rotation = NetUtils.GetNodeRotation(tangent),
                    m_Elevation = start ? road.StartElevation : road.EndElevation,
                    m_CourseDelta = start ? 0f : 1f,
                    m_Flags = start ? CoursePosFlags.IsFirst : CoursePosFlags.IsLast,
                    m_ParentMesh = -1,
                };
            }

            var startPos = End(startNode, true);
            var endPos = End(endNode, false);
            var definition = NetDefinitions.Emit(ecb, new CreationDefinition
            {
                m_Prefab = road.Prefab,
                m_RandomSeed = seed,
                m_Flags = CreationFlags.SubElevation,
            }, new NetCourse
            {
                m_Curve = road.Curve,
                m_StartPosition = startPos,
                m_EndPosition = endPos,
                m_Elevation = new float2(startPos.m_Elevation.x, endPos.m_Elevation.x),
                m_Length = MathUtils.Length(road.Curve),
                m_FixedIndex = -1,
            });

            if (road.HasUpgrades)
                ecb.AddComponent(definition, road.Upgrades);
        }
    }
}
