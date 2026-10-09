using Colossal.Entities;
using Colossal.Mathematics;
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
    /// Hover a road to preview a ramp leaving (or joining) it at the cursor, click to lock it, adjust
    /// side, angle, turn, length and height in the panel, then click or Apply. The ramp uses the road's
    /// own type unless another type is copied from a road. See <see cref="RampEdit"/>.
    /// </summary>
    public partial class RampToolSystem : ToolBaseSystem, IPreviewTool
    {
        private ToolOutputBarrier m_ToolOutputBarrier;
        private TerrainSystem m_TerrainSystem;
        private ToolOverlay m_Overlay;
        private Unity.Mathematics.Random m_Random;
        private Entity m_LockedEdge;
        private float m_LockedT;
        private Entity m_RampPrefab;
        private bool m_PickingType;
        private bool m_ApplyRequested;
        private bool m_CancelRequested;

        public override string toolID => "NetworkToolsReworked.RampTool";

        public ToolPhase Phase => m_PickingType ? ToolPhase.PickSource : m_LockedEdge == Entity.Null ? ToolPhase.PickStart : ToolPhase.Review;

        public string Summary { get; private set; } = string.Empty;

        /// <summary>Name of the copied ramp road type; empty when the ramp uses the road's own type.</summary>
        public string RampTypeName { get; private set; } = string.Empty;

        public void RequestApply() => m_ApplyRequested = true;

        public void RequestCancel() => m_CancelRequested = true;

        /// <summary>Next click on a road copies its type for the ramp.</summary>
        public void RequestPickType() => m_PickingType = true;

        /// <summary>Go back to building ramps of the same type as the road they leave.</summary>
        public void UseRoadType()
        {
            m_RampPrefab = Entity.Null;
            RampTypeName = string.Empty;
            m_PickingType = false;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_Overlay = new ToolOverlay(World);
            m_Random = new Unity.Mathematics.Random(0x4A3Bu);
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            m_LockedEdge = Entity.Null;
            m_PickingType = false;
            applyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            m_LockedEdge = Entity.Null;
            m_PickingType = false;
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
                if (m_PickingType)
                    m_PickingType = false;
                else if (m_LockedEdge != Entity.Null)
                    m_LockedEdge = Entity.Null;
                else
                    m_ToolSystem.activeTool = m_DefaultToolSystem;
                Summary = string.Empty;
                return inputDeps;
            }

            applyMode = ApplyMode.Clear;
            UndoRecorder.Begin(EntityManager, toolID);
            Summary = string.Empty;
            m_Overlay.BeginFrame();

            if (m_RampPrefab != Entity.Null && (!EntityManager.Exists(m_RampPrefab) || EntityManager.HasComponent<Deleted>(m_RampPrefab)))
                UseRoadType();
            if (m_LockedEdge != Entity.Null && !IsLiveEdge(m_LockedEdge))
                m_LockedEdge = Entity.Null;

            var click = applyAction.WasPressedThisFrame();
            var hasHit = GetRaycastResult(out Entity hitEntity, out RaycastHit hit) && IsLiveEdge(hitEntity);

            if (m_PickingType)
            {
                if (!hasHit)
                    return inputDeps;
                var prefab = EntityManager.GetComponentData<PrefabRef>(hitEntity).m_Prefab;
                m_Overlay.Edge(hitEntity, ToolOverlay.Hover);
                Summary = $"Click to build ramps as {PrefabName(prefab)}.";
                if (click)
                {
                    m_RampPrefab = prefab;
                    RampTypeName = PrefabName(prefab);
                    m_PickingType = false;
                }
                return inputDeps;
            }

            Entity edge;
            float t;
            if (m_LockedEdge != Entity.Null)
            {
                edge = m_LockedEdge;
                t = m_LockedT;
            }
            else
            {
                if (!hasHit)
                    return inputDeps;
                edge = hitEntity;
                t = math.saturate(hit.m_CurvePosition);
            }

            m_Overlay.Edge(edge, ToolOverlay.Path);
            var junction = MathUtils.Position(EntityManager.GetComponentData<Curve>(edge).m_Bezier, t);
            if (!RampEdit.CanAttach(EntityManager, edge, t))
            {
                m_Overlay.Point(junction, 4f, ToolOverlay.Invalid);
                Summary = $"Too close to a node: the ramp needs {RampEdit.kEndMargin:0} m of road on both sides.";
                return inputDeps;
            }

            var rampPrefab = m_RampPrefab != Entity.Null ? m_RampPrefab : EntityManager.GetComponentData<PrefabRef>(edge).m_Prefab;
            var limit = PrefabGradeLimit(rampPrefab);
            var terrain = m_TerrainSystem.GetHeightData();
            var settings = Mod.Settings;
            var ramp = new RampParams
            {
                Right = settings.RampRight,
                Entry = settings.RampEntry,
                Flip = settings.RampFlip,
                Angle = settings.RampAngle,
                Turn = settings.RampTurn,
                Length = settings.RampLength,
                Height = settings.RampHeight,
            };
            if (!RampEdit.Emit(EntityManager, m_ToolOutputBarrier.CreateCommandBuffer(), ref terrain, edge, t, rampPrefab, ramp, limit, m_Random.NextInt(), out var result))
                return inputDeps;

            var locked = m_LockedEdge != Entity.Null;
            m_Overlay.Point(junction, 4f, locked ? ToolOverlay.End : ToolOverlay.Start);
            m_Overlay.Bezier(result.Curve, m_Overlay.Width(edge) * 0.5f, Grades.ColorFor(result.MaxGrade, limit));
            Summary = Describe(result, limit, rampPrefab);

            if (!locked)
            {
                if (click)
                {
                    m_LockedEdge = edge;
                    m_LockedT = t;
                }
            }
            else if (click || applyRequested)
            {
                applyMode = ApplyMode.Apply;
                UndoRecorder.Commit(Summary);
                if (settings.DebugLogging)
                    Mod.Log.Info($"Ramp applied on {edge} at {t:0.000}");
                m_LockedEdge = Entity.Null;
            }

            return inputDeps;
        }

        private string Describe(RampResult r, float limit, Entity prefab)
        {
            var s = Mod.Settings;
            var kind = s.RampEntry ? "Entry" : "Exit";
            var side = s.RampRight ? "right" : "left";
            var lengthened = r.Lengthened ? $" (lengthened from {PathInfo.Distance(s.RampLength)} to keep within the {limit * 100f:0}% grade limit)" : "";
            var end = math.abs(r.EndAboveGround) < 0.5f ? "ends at ground level" : r.EndAboveGround > 0f ? $"ends {r.EndAboveGround:0.0} m above ground" : $"ends {-r.EndAboveGround:0.0} m below ground";
            return $"{kind} on the {side}, {PrefabName(prefab)}, {PathInfo.Distance(r.Length)}{lengthened}, height {PathInfo.Signed(s.RampHeight)} m, steepest {r.MaxGrade * 100f:0.0}%, {end}";
        }

        private float PrefabGradeLimit(Entity prefab)
        {
            return EntityManager.TryGetComponent(prefab, out NetGeometryData geometry) && geometry.m_MaxSlopeSteepness > 0f ? geometry.m_MaxSlopeSteepness : 0.12f;
        }

        private string PrefabName(Entity prefab)
        {
            var asset = m_PrefabSystem.GetPrefab<PrefabBase>(prefab);
            return asset != null ? asset.name : "road";
        }

        private bool IsLiveEdge(Entity entity)
        {
            return EntityManager.Exists(entity) && EntityManager.HasComponent<Edge>(entity) && !EntityManager.HasComponent<Deleted>(entity) && !EntityManager.HasComponent<Owner>(entity);
        }
    }
}
