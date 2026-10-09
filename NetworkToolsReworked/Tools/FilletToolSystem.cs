using Colossal.Entities;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using NetworkToolsReworked.Edits;
using NetworkToolsReworked.Undo;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace NetworkToolsReworked.Tools
{
    /// <summary>
    /// Hover a corner (a node joining exactly two roads) to preview it rounded off with a curve of the set
    /// radius, click to lock it, adjust the radius, then click or Apply. See <see cref="FilletEdit"/>.
    /// </summary>
    public partial class FilletToolSystem : ToolBaseSystem, IPreviewTool
    {
        private ToolOutputBarrier m_ToolOutputBarrier;
        private TerrainSystem m_TerrainSystem;
        private ToolOverlay m_Overlay;
        private Unity.Mathematics.Random m_Random;
        private Entity m_Node;
        private bool m_ApplyRequested;
        private bool m_CancelRequested;

        public override string toolID => "NetworkToolsReworked.FilletTool";

        public ToolPhase Phase => m_Node == Entity.Null ? ToolPhase.PickStart : ToolPhase.Review;

        public string Summary { get; private set; } = string.Empty;

        public void RequestApply() => m_ApplyRequested = true;

        public void RequestCancel() => m_CancelRequested = true;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_Overlay = new ToolOverlay(World);
            m_Random = new Unity.Mathematics.Random(0xF111u);
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            m_Node = Entity.Null;
            applyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            m_Node = Entity.Null;
            applyAction.shouldBeEnabled = false;
            cancelAction.shouldBeEnabled = false;
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            m_ToolRaycastSystem.typeMask = TypeMask.Net;
            m_ToolRaycastSystem.netLayerMask = Layer.Road | Layer.Pathway | Layer.TrainTrack | Layer.TramTrack | Layer.SubwayTrack | Layer.Waterway | Layer.PublicTransportRoad;
            m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround | CollisionMask.Overground | CollisionMask.Underground;
        }

        public override PrefabBase GetPrefab() => null;

        public override bool TrySetPrefab(PrefabBase prefab) => false;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var applyRequested = m_ApplyRequested;
            var cancelRequested = m_CancelRequested;
            m_ApplyRequested = m_CancelRequested = false;

            if (cancelAction.WasPressedThisFrame() || cancelRequested)
            {
                if (m_Node != Entity.Null)
                    m_Node = Entity.Null;
                else
                    m_ToolSystem.activeTool = m_DefaultToolSystem;
                Summary = string.Empty;
                return inputDeps;
            }

            applyMode = ApplyMode.Clear;
            UndoRecorder.Begin(EntityManager, toolID);
            Summary = string.Empty;
            m_Overlay.BeginFrame();

            if (m_Node != Entity.Null && !ToolPicks.IsAlive(EntityManager, m_Node))
                m_Node = Entity.Null;

            var click = applyAction.WasPressedThisFrame();
            var target = m_Node;
            if (target == Entity.Null && GetRaycastResult(out Entity hitEntity, out RaycastHit hit))
                target = HoveredNode(hitEntity, hit);
            if (target == Entity.Null)
                return inputDeps;

            var problem = FilletEdit.Check(EntityManager, target);
            if (problem != FilletEdit.Problem.None)
            {
                m_Overlay.Node(target, ToolOverlay.Invalid);
                Summary = problem == FilletEdit.Problem.Straight
                    ? "The road runs straight on here: there is no corner to round."
                    : "Pick a corner: a node where exactly two roads meet.";
                return inputDeps;
            }

            var terrain = m_TerrainSystem.GetHeightData();
            if (!FilletEdit.Emit(EntityManager, m_ToolOutputBarrier.CreateCommandBuffer(), ref terrain, target, Mod.Settings.FilletRadius, m_Random.NextInt(), out var result))
            {
                m_Overlay.Node(target, ToolOverlay.Invalid);
                Summary = "The roads at this corner are too short to round it.";
                return inputDeps;
            }

            var locked = m_Node != Entity.Null;
            m_Overlay.Node(target, locked ? ToolOverlay.End : ToolOverlay.Hover);
            m_Overlay.Bezier(result.Arc, m_Overlay.Width(target) * 0.5f, locked ? ToolOverlay.Locked : ToolOverlay.Path);
            var clamped = result.Clamped ? $" (the roads leave room for {PathInfo.Distance(result.MaxRadius)} at most)" : $", up to {PathInfo.Distance(result.MaxRadius)} possible";
            Summary = $"Corner of {result.Turn:0}°, radius {PathInfo.Distance(result.Radius)}{clamped}";

            if (!locked)
            {
                if (click)
                    m_Node = target;
            }
            else if (click || applyRequested)
            {
                applyMode = ApplyMode.Apply;
                UndoRecorder.Commit(Summary);
                if (Mod.Settings.DebugLogging)
                    Mod.Log.Info($"Fillet applied at {m_Node}, radius {result.Radius}");
                m_Node = Entity.Null;
            }
            return inputDeps;
        }

        private Entity HoveredNode(Entity entity, RaycastHit hit)
        {
            if (EntityManager.HasComponent<Node>(entity))
                return ToolPicks.IsAlive(EntityManager, entity) ? entity : Entity.Null;
            if (EntityManager.HasComponent<Owner>(entity) || EntityManager.HasComponent<Deleted>(entity) || !EntityManager.TryGetComponent(entity, out Edge edge))
                return Entity.Null;
            return hit.m_CurvePosition < 0.5f ? edge.m_Start : edge.m_End;
        }
    }
}
