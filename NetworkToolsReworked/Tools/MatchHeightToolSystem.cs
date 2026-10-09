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
    /// Click a node to take its height (or set a height in the panel), then hover other nodes to preview
    /// them moved to that height and click to apply, one node after another. Nodes keep their position
    /// on the map; the roads at them follow, as with Move Node. Right-click forgets the height.
    /// </summary>
    public partial class MatchHeightToolSystem : ToolBaseSystem, IPreviewTool
    {
        private ToolOutputBarrier m_ToolOutputBarrier;
        private TerrainSystem m_TerrainSystem;
        private ToolOverlay m_Overlay;
        private Unity.Mathematics.Random m_Random;
        private bool m_HasTarget;
        private bool m_CancelRequested;

        public override string toolID => "NetworkToolsReworked.MatchHeightTool";

        public ToolPhase Phase => m_HasTarget ? ToolPhase.PickEnd : ToolPhase.PickStart;

        public string Summary { get; private set; } = string.Empty;

        /// <summary>Height (world y, metres) that nodes are moved to.</summary>
        public float TargetHeight { get; private set; }

        public void SetTargetHeight(float height)
        {
            TargetHeight = height;
            m_HasTarget = true;
        }

        /// <summary>Applying happens by clicking each node.</summary>
        public void RequestApply()
        {
        }

        public void RequestCancel() => m_CancelRequested = true;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_Overlay = new ToolOverlay(World);
            m_Random = new Unity.Mathematics.Random(0x4E1Au);
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            applyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            m_HasTarget = false;
            Summary = string.Empty;
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
            var cancelRequested = m_CancelRequested;
            m_CancelRequested = false;

            if (cancelAction.WasPressedThisFrame() || cancelRequested)
            {
                if (m_HasTarget)
                    m_HasTarget = false;
                else
                    m_ToolSystem.activeTool = m_DefaultToolSystem;
                Summary = string.Empty;
                return inputDeps;
            }

            applyMode = ApplyMode.Clear;
            UndoRecorder.Begin(EntityManager, toolID);
            Summary = string.Empty;
            m_Overlay.BeginFrame();

            var click = applyAction.WasPressedThisFrame();
            var hovered = GetRaycastResult(out Entity hitEntity, out RaycastHit hit) ? HoveredNode(hitEntity, hit) : Entity.Null;
            if (hovered == Entity.Null)
                return inputDeps;

            var position = EntityManager.GetComponentData<Node>(hovered).m_Position;
            if (!m_HasTarget)
            {
                m_Overlay.Node(hovered, ToolOverlay.Hover);
                Summary = $"This node is at {position.y:0.0} m. Click to use its height.";
                if (click)
                    SetTargetHeight(position.y);
                return inputDeps;
            }

            var movable = MoveEdit.CanMove(EntityManager, hovered);
            var change = TargetHeight - position.y;
            if (!movable)
            {
                m_Overlay.Node(hovered, ToolOverlay.Invalid);
                Summary = "This node belongs to a building or has no roads, so it can't be moved.";
                return inputDeps;
            }
            if (math.abs(change) < 0.01f)
            {
                m_Overlay.Node(hovered, ToolOverlay.Start);
                Summary = $"Already at {TargetHeight:0.0} m.";
                return inputDeps;
            }

            var target = new float3(position.x, TargetHeight, position.z);
            var terrain = m_TerrainSystem.GetHeightData();
            if (!MoveEdit.Emit(EntityManager, m_ToolOutputBarrier.CreateCommandBuffer(), ref terrain, hovered, target, m_Random.NextInt()))
                return inputDeps;

            m_Overlay.Node(hovered, ToolOverlay.End);
            m_Overlay.Line(position, target, 1f, ToolOverlay.End);
            var aboveGround = TargetHeight - TerrainUtils.SampleHeight(ref terrain, target);
            Summary = $"{PathInfo.Signed(change)} m to {TargetHeight:0.0} m ({aboveGround:0.0} m above ground here). Click to apply.";

            if (click)
            {
                applyMode = ApplyMode.Apply;
                UndoRecorder.Commit();
                if (Mod.Settings.DebugLogging)
                    Mod.Log.Info($"Match height applied: {hovered} to {TargetHeight}");
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
