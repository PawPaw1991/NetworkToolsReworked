using Colossal.Entities;
using Colossal.Mathematics;
using Game.Common;
using Game.Input;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using NetworkToolsReworked.Edits;
using Unity.Entities;
using Unity.Jobs;

namespace NetworkToolsReworked.Tools
{
    public enum NodeToolMode
    {
        AddNode,
        RemoveNode,
    }

    /// <summary>
    /// Add a node to an edge, or remove a node joining two edges. Every frame the hovered target is
    /// written as definitions (preview); a click applies them. Vanilla validation stays enabled.
    /// </summary>
    public partial class NodeToolSystem : ToolBaseSystem
    {
        // Don't split closer than this to an existing node, in metres along the edge.
        private const float kMinSplitDistance = 4f;

        private ToolOutputBarrier m_ToolOutputBarrier;
        private ToolOverlay m_Overlay;
        private Unity.Mathematics.Random m_Random;

        public override string toolID => "NetworkToolsReworked.NodeTool";

        public NodeToolMode Mode { get; set; }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_Overlay = new ToolOverlay(World);
            m_Random = new Unity.Mathematics.Random(0x6E74u);
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
            if (cancelAction.WasPressedThisFrame())
            {
                m_ToolSystem.activeTool = m_DefaultToolSystem;
                return inputDeps;
            }

            // Clear last frame's preview; definitions are re-emitted for whatever is hovered now.
            applyMode = ApplyMode.Clear;
            m_Overlay.BeginFrame();

            if (!GetRaycastResult(out Entity hitEntity, out RaycastHit hit))
                return inputDeps;

            var ecb = m_ToolOutputBarrier.CreateCommandBuffer();
            var seed = m_Random.NextInt();
            bool emitted = Mode switch
            {
                NodeToolMode.AddNode => TryAddNode(ecb, hitEntity, hit, seed),
                NodeToolMode.RemoveNode => TryRemoveNode(ecb, hitEntity, hit, seed),
                _ => false,
            };

            if (emitted && applyAction.WasPressedThisFrame())
            {
                applyMode = ApplyMode.Apply;
                if (Mod.Settings.DebugLogging)
                    Mod.Log.Info($"{Mode} applied on {hitEntity}");
            }

            return inputDeps;
        }

        private bool TryAddNode(EntityCommandBuffer ecb, Entity entity, RaycastHit hit, int seed)
        {
            if (!EntityManager.HasComponent<Edge>(entity) || EntityManager.HasComponent<Owner>(entity))
                return false;

            var curve = EntityManager.GetComponentData<Curve>(entity);
            var t = hit.m_CurvePosition;
            var distanceAlong = t * curve.m_Length;
            var tooClose = distanceAlong < kMinSplitDistance || curve.m_Length - distanceAlong < kMinSplitDistance;

            m_Overlay.Edge(entity, ToolOverlay.Path);
            m_Overlay.Point(MathUtils.Position(curve.m_Bezier, t), 4f, tooClose ? ToolOverlay.Invalid : ToolOverlay.Start);
            if (tooClose)
                return false;

            NetDefinitions.SplitEdge(EntityManager, ecb, entity, t, seed);
            return true;
        }

        private bool TryRemoveNode(EntityCommandBuffer ecb, Entity entity, RaycastHit hit, int seed)
        {
            // Hovering an edge picks whichever of its nodes is nearer the cursor.
            if (EntityManager.TryGetComponent(entity, out Edge edge))
                entity = hit.m_CurvePosition < 0.5f ? edge.m_Start : edge.m_End;

            if (!EntityManager.HasComponent<Node>(entity) || EntityManager.HasComponent<Owner>(entity))
                return false;

            var merged = NetDefinitions.MergeAtNode(EntityManager, ecb, entity, seed);
            m_Overlay.Node(entity, merged ? ToolOverlay.Start : ToolOverlay.Invalid);
            return merged;
        }
    }
}
