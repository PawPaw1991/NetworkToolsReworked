using System.Collections.Generic;
using Colossal.Entities;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using NetworkToolsReworked.Edits;
using Unity.Entities;
using Unity.Jobs;

namespace NetworkToolsReworked.Tools
{
    /// <summary>
    /// Base for tools that act on the road between two picked nodes. Click a start node, hover an end
    /// node to preview, click again to apply. Right-click clears the start node, or exits if none is
    /// picked. Subclasses only emit the definitions for the found path.
    /// </summary>
    public abstract partial class PathToolSystem : ToolBaseSystem
    {
        private ToolOutputBarrier m_ToolOutputBarrier;
        private Unity.Mathematics.Random m_Random;
        private readonly List<Entity> m_PathNodes = new List<Entity>();
        private readonly List<Entity> m_PathEdges = new List<Entity>();
        private Entity m_StartNode;

        protected abstract void EmitPath(EntityCommandBuffer ecb, List<Entity> nodes, List<Entity> edges, int randomSeed);

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_Random = new Unity.Mathematics.Random((uint)toolID.GetHashCode() | 1u);
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            m_StartNode = Entity.Null;
            applyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            m_StartNode = Entity.Null;
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
                if (m_StartNode != Entity.Null)
                    m_StartNode = Entity.Null;
                else
                    m_ToolSystem.activeTool = m_DefaultToolSystem;
                return inputDeps;
            }

            applyMode = ApplyMode.Clear;

            if (m_StartNode != Entity.Null && !EntityManager.Exists(m_StartNode))
                m_StartNode = Entity.Null;

            if (!GetRaycastResult(out Entity hitEntity, out RaycastHit hit))
                return inputDeps;

            var hovered = HoveredNode(hitEntity, hit);
            if (hovered == Entity.Null)
                return inputDeps;

            if (m_StartNode == Entity.Null)
            {
                if (applyAction.WasPressedThisFrame())
                    m_StartNode = hovered;
                return inputDeps;
            }

            if (!SlopeEdit.FindPath(EntityManager, m_StartNode, hovered, m_PathNodes, m_PathEdges))
                return inputDeps;

            EmitPath(m_ToolOutputBarrier.CreateCommandBuffer(), m_PathNodes, m_PathEdges, m_Random.NextInt());

            if (applyAction.WasPressedThisFrame())
            {
                applyMode = ApplyMode.Apply;
                if (Mod.Settings.DebugLogging)
                    Mod.Log.Info($"{toolID} applied over {m_PathEdges.Count} edges from {m_StartNode} to {hovered}");
                m_StartNode = Entity.Null;
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
