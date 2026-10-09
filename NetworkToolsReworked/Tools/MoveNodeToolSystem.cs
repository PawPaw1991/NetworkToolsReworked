using Colossal.Entities;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using NetworkToolsReworked.Edits;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace NetworkToolsReworked.Tools
{
    /// <summary>
    /// Click a node to pick it up, move the cursor to drag it (the roads follow in the preview), click
    /// to drop and review, then nudge its position and height in the panel and click or press Apply.
    /// Right-click steps back one phase, or exits if nothing is picked.
    /// </summary>
    public partial class MoveNodeToolSystem : ToolBaseSystem, IPreviewTool
    {
        private ToolOutputBarrier m_ToolOutputBarrier;
        private TerrainSystem m_TerrainSystem;
        private ToolOverlay m_Overlay;
        private Unity.Mathematics.Random m_Random;
        private Entity m_Node;
        private bool m_Dropped;
        private float3 m_Target;
        private bool m_ApplyRequested;
        private bool m_CancelRequested;

        public override string toolID => "NetworkToolsReworked.MoveNodeTool";

        public ToolPhase Phase => m_Node == Entity.Null ? ToolPhase.PickStart : m_Dropped ? ToolPhase.Review : ToolPhase.PickEnd;

        public string Summary { get; private set; } = string.Empty;

        /// <summary>Sideways nudge from the dropped position, in metres (x east, y north).</summary>
        public float2 Nudge { get; set; }

        /// <summary>Height change from where the node would sit, in metres.</summary>
        public float HeightOffset { get; set; }

        public void RequestApply() => m_ApplyRequested = true;

        public void RequestCancel() => m_CancelRequested = true;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_Overlay = new ToolOverlay(World);
            m_Random = new Unity.Mathematics.Random(0x30BEu);
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            Reset();
            applyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            Reset();
            applyAction.shouldBeEnabled = false;
            cancelAction.shouldBeEnabled = false;
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            // While dragging, the cursor position comes from the terrain as well as from roads.
            m_ToolRaycastSystem.typeMask = m_Node == Entity.Null ? TypeMask.Net : TypeMask.Net | TypeMask.Terrain;
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
                if (m_Dropped)
                {
                    m_Dropped = false;
                    Nudge = float2.zero;
                }
                else if (m_Node != Entity.Null)
                    Reset();
                else
                    m_ToolSystem.activeTool = m_DefaultToolSystem;
                Summary = string.Empty;
                return inputDeps;
            }

            applyMode = ApplyMode.Clear;
            Summary = string.Empty;
            m_Overlay.BeginFrame();

            if (m_Node != Entity.Null && !ToolPicks.IsAlive(EntityManager, m_Node))
                Reset();

            var click = applyAction.WasPressedThisFrame();
            var hasHit = GetRaycastResult(out Entity hitEntity, out RaycastHit hit);

            if (m_Node == Entity.Null)
            {
                var hovered = hasHit ? HoveredNode(hitEntity, hit) : Entity.Null;
                if (hovered != Entity.Null)
                {
                    var movable = MoveEdit.CanMove(EntityManager, hovered);
                    m_Overlay.Node(hovered, movable ? ToolOverlay.Hover : ToolOverlay.Invalid);
                    if (click && movable)
                        m_Node = hovered;
                }
                return inputDeps;
            }

            if (!m_Dropped && hasHit)
                m_Target = hit.m_HitPosition;

            var original = EntityManager.GetComponentData<Node>(m_Node).m_Position;
            var terrain = m_TerrainSystem.GetHeightData();
            var position = TargetPosition(ref terrain, original);

            m_Overlay.Node(m_Node, ToolOverlay.Start);
            m_Overlay.Line(original, position, 1f, ToolOverlay.Start);
            m_Overlay.Point(position, m_Overlay.Width(m_Node) + 2f, m_Dropped ? ToolOverlay.End : ToolOverlay.Hover);

            var emitted = MoveEdit.Emit(EntityManager, m_ToolOutputBarrier.CreateCommandBuffer(), ref terrain, m_Node, position, m_Random.NextInt());
            var moved = position - original;
            Summary = emitted
                ? $"Moved {PathInfo.Distance(math.length(moved.xz))}, height {PathInfo.Signed(moved.y)} m"
                : "Nothing to change yet: move the cursor or adjust below.";

            if (!m_Dropped)
            {
                if (click)
                    m_Dropped = true;
            }
            else if (emitted && (click || applyRequested))
            {
                applyMode = ApplyMode.Apply;
                if (Mod.Settings.DebugLogging)
                    Mod.Log.Info($"Move node applied: {m_Node} by {moved}");
                Reset();
            }

            return inputDeps;
        }

        /// <summary>
        /// Where the node goes: the cursor (plus nudge), at ground level for ground roads or at the
        /// node's current height for bridges and tunnels, plus the height offset.
        /// </summary>
        private float3 TargetPosition(ref TerrainHeightData terrain, float3 original)
        {
            var p = m_Target;
            p.x += Nudge.x;
            p.z += Nudge.y;
            p.y = EntityManager.HasComponent<Elevation>(m_Node) ? original.y : TerrainUtils.SampleHeight(ref terrain, p);
            p.y += HeightOffset;
            return p;
        }

        private void Reset()
        {
            m_Node = Entity.Null;
            m_Dropped = false;
            Nudge = float2.zero;
            HeightOffset = 0f;
            Summary = string.Empty;
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
