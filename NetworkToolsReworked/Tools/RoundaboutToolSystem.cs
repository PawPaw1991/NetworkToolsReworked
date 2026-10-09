using System.Collections.Generic;
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
    /// Click a junction to preview a roundabout around it, adjust the radius and direction in the panel,
    /// then click or press Apply. Right-click picks another junction, or exits.
    /// </summary>
    public partial class RoundaboutToolSystem : ToolBaseSystem, IPreviewTool
    {
        private ToolOutputBarrier m_ToolOutputBarrier;
        private TerrainSystem m_TerrainSystem;
        private ToolOverlay m_Overlay;
        private Unity.Mathematics.Random m_Random;
        private readonly List<float3> m_Ring = new List<float3>();
        private Entity m_Node;
        private bool m_ApplyRequested;
        private bool m_CancelRequested;

        public override string toolID => "NetworkToolsReworked.RoundaboutTool";

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
            m_Random = new Unity.Mathematics.Random(0x60DAu);
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
            var radius = math.clamp(Mod.Settings.RoundaboutRadius, 8f, 200f);
            var target = m_Node;
            if (target == Entity.Null && GetRaycastResult(out Entity hitEntity, out RaycastHit hit))
                target = HoveredNode(hitEntity, hit);
            if (target == Entity.Null)
                return inputDeps;

            var problem = RoundaboutEdit.Check(EntityManager, target, radius);
            m_Overlay.Node(target, problem == RoundaboutEdit.Problem.None ? (m_Node != Entity.Null ? ToolOverlay.End : ToolOverlay.Hover) : ToolOverlay.Invalid);
            if (problem != RoundaboutEdit.Problem.None)
            {
                Summary = problem == RoundaboutEdit.Problem.RadiusTooLarge
                    ? "Radius too large: a road at this junction is shorter than the radius."
                    : "Pick a junction where three or more roads meet.";
                return inputDeps;
            }

            var terrain = m_TerrainSystem.GetHeightData();
            var ecb = m_ToolOutputBarrier.CreateCommandBuffer();
            if (!RoundaboutEdit.Emit(EntityManager, ecb, ref terrain, target, radius, Mod.Settings.RoundaboutClockwise, m_Random.NextInt(), m_Ring))
                return inputDeps;

            Summary = $"Radius {PathInfo.Distance(radius)}, {EntityManager.GetBuffer<ConnectedEdge>(target, isReadOnly: true).Length} roads, {(Mod.Settings.RoundaboutClockwise ? "clockwise" : "anticlockwise")}";

            if (m_Node == Entity.Null)
            {
                if (click)
                    m_Node = target;
            }
            else if (click || applyRequested)
            {
                applyMode = ApplyMode.Apply;
                UndoRecorder.Commit(Summary);
                if (Mod.Settings.DebugLogging)
                    Mod.Log.Info($"Roundabout applied at {m_Node}, radius {radius}");
                m_Node = Entity.Null;
            }

            return inputDeps;
        }

        private Entity HoveredNode(Entity entity, RaycastHit hit)
        {
            if (EntityManager.HasComponent<Node>(entity))
                return entity;
            if (EntityManager.HasComponent<Owner>(entity) || !EntityManager.TryGetComponent(entity, out Edge edge))
                return Entity.Null;
            return hit.m_CurvePosition < 0.5f ? edge.m_Start : edge.m_End;
        }
    }
}
