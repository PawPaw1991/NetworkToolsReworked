using Colossal.Entities;
using Game.Common;
using Game.Input;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using NetworkToolsReworked.Edits;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace NetworkToolsReworked.Tools
{
    /// <summary>
    /// Click a start node, hover an end node to preview a new road between them, click to build it.
    /// The rotate keys turn the start direction in 15° steps. Right-click clears the start node, or
    /// exits if none is picked. The new road uses the start node's road type.
    /// </summary>
    public partial class ConnectToolSystem : ToolBaseSystem
    {
        private const float kRotateStep = math.PI / 12f;

        private ToolOutputBarrier m_ToolOutputBarrier;
        private Unity.Mathematics.Random m_Random;
        private ProxyAction m_RotateLeftAction;
        private ProxyAction m_RotateRightAction;
        private Entity m_StartNode;
        private float m_RotationOffset;

        public override string toolID => "NetworkToolsReworked.ConnectTool";

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_Random = new Unity.Mathematics.Random(0xC0EEu);
            m_RotateLeftAction = Mod.Settings.GetAction(nameof(Setting.ConnectRotateLeft));
            m_RotateRightAction = Mod.Settings.GetAction(nameof(Setting.ConnectRotateRight));
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            ClearStart();
            applyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;
            m_RotateLeftAction.shouldBeEnabled = true;
            m_RotateRightAction.shouldBeEnabled = true;
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            ClearStart();
            applyAction.shouldBeEnabled = false;
            cancelAction.shouldBeEnabled = false;
            m_RotateLeftAction.shouldBeEnabled = false;
            m_RotateRightAction.shouldBeEnabled = false;
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
                if (m_StartNode != Entity.Null)
                    ClearStart();
                else
                    m_ToolSystem.activeTool = m_DefaultToolSystem;
                return inputDeps;
            }

            if (m_RotateLeftAction.WasPerformedThisFrame())
                m_RotationOffset += kRotateStep;
            if (m_RotateRightAction.WasPerformedThisFrame())
                m_RotationOffset -= kRotateStep;

            applyMode = ApplyMode.Clear;

            if (m_StartNode != Entity.Null && !EntityManager.Exists(m_StartNode))
                ClearStart();

            if (!GetRaycastResult(out Entity hitEntity, out RaycastHit hit))
                return inputDeps;

            var hovered = HoveredNode(hitEntity, hit);
            if (hovered == Entity.Null)
                return inputDeps;

            if (m_StartNode == Entity.Null)
            {
                if (applyAction.WasPressedThisFrame() && ConnectEdit.PrefabFor(EntityManager, hovered) != Entity.Null)
                    m_StartNode = hovered;
                return inputDeps;
            }

            var target = EntityManager.GetComponentData<Node>(hovered).m_Position;
            var direction = math.rotate(quaternion.RotateY(m_RotationOffset), ConnectEdit.DefaultDirection(EntityManager, m_StartNode, target));

            var ecb = m_ToolOutputBarrier.CreateCommandBuffer();
            if (!ConnectEdit.Emit(EntityManager, ecb, m_StartNode, hovered, direction, Mod.Settings.ConnectMode, m_Random.NextInt()))
                return inputDeps;

            if (applyAction.WasPressedThisFrame())
            {
                applyMode = ApplyMode.Apply;
                if (Mod.Settings.DebugLogging)
                    Mod.Log.Info($"Connect applied from {m_StartNode} to {hovered}");
                ClearStart();
            }

            return inputDeps;
        }

        private void ClearStart()
        {
            m_StartNode = Entity.Null;
            m_RotationOffset = 0f;
        }

        private Entity HoveredNode(Entity entity, RaycastHit hit)
        {
            if (EntityManager.HasComponent<Node>(entity))
                return EntityManager.HasComponent<Owner>(entity) ? Entity.Null : entity;
            if (EntityManager.HasComponent<Owner>(entity) || !EntityManager.TryGetComponent(entity, out Edge edge))
                return Entity.Null;
            return hit.m_CurvePosition < 0.5f ? edge.m_Start : edge.m_End;
        }
    }
}
