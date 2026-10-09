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
    /// node to preview, click it to lock the preview, then adjust the options and click (or press Apply
    /// in the panel) to apply. Right-click steps back one phase, or exits if nothing is picked.
    /// Subclasses only emit the definitions for the found path.
    /// </summary>
    public abstract partial class PathToolSystem : ToolBaseSystem, IPreviewTool
    {
        private ToolOutputBarrier m_ToolOutputBarrier;
        private ToolOverlay m_Overlay;
        private Unity.Mathematics.Random m_Random;
        private readonly List<Entity> m_PathNodes = new List<Entity>();
        private readonly List<Entity> m_PathEdges = new List<Entity>();
        private Entity m_StartNode;
        private Entity m_EndNode;
        private bool m_ApplyRequested;
        private bool m_CancelRequested;

        public ToolPhase Phase => m_StartNode == Entity.Null ? ToolPhase.PickStart : m_EndNode == Entity.Null ? ToolPhase.PickEnd : ToolPhase.Review;

        public string Summary { get; private set; } = string.Empty;

        public void RequestApply() => m_ApplyRequested = true;

        public void RequestCancel() => m_CancelRequested = true;

        /// <summary>Writes the definitions for the path. Returns false if there is nothing to change.</summary>
        protected abstract bool EmitPath(EntityCommandBuffer ecb, List<Entity> nodes, List<Entity> edges, int randomSeed);

        /// <summary>Highlights the path; called after <see cref="EmitPath"/>.</summary>
        protected virtual void DrawPath(ToolOverlay overlay, List<Entity> nodes, List<Entity> edges, bool locked)
        {
            foreach (var edge in edges)
                overlay.Edge(edge, locked ? ToolOverlay.Locked : ToolOverlay.Path);
        }

        /// <summary>Called when the start node is cleared, to reset per-selection options.</summary>
        protected virtual void OnSelectionCleared()
        {
        }

        /// <summary>One line describing the previewed change, shown in the tool panel.</summary>
        protected virtual string Describe(List<Entity> nodes, List<Entity> edges) => PathInfo.Describe(EntityManager, nodes, edges);

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_Overlay = new ToolOverlay(World);
            m_Random = new Unity.Mathematics.Random((uint)toolID.GetHashCode() | 1u);
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
                if (m_EndNode != Entity.Null)
                    m_EndNode = Entity.Null;
                else if (m_StartNode != Entity.Null)
                    Reset();
                else
                    m_ToolSystem.activeTool = m_DefaultToolSystem;
                Summary = string.Empty;
                return inputDeps;
            }

            applyMode = ApplyMode.Clear;
            Summary = string.Empty;
            m_Overlay.BeginFrame();

            if (m_StartNode != Entity.Null && !EntityManager.Exists(m_StartNode))
                Reset();
            if (m_EndNode != Entity.Null && !EntityManager.Exists(m_EndNode))
                m_EndNode = Entity.Null;

            var click = applyAction.WasPressedThisFrame();
            var hovered = GetRaycastResult(out Entity hitEntity, out RaycastHit hit) ? HoveredNode(hitEntity, hit) : Entity.Null;

            if (m_StartNode == Entity.Null)
            {
                if (hovered != Entity.Null)
                {
                    m_Overlay.Node(hovered, ToolOverlay.Hover);
                    if (click)
                        m_StartNode = hovered;
                }
                return inputDeps;
            }

            m_Overlay.Node(m_StartNode, ToolOverlay.Start);

            var locked = m_EndNode != Entity.Null;
            var end = locked ? m_EndNode : hovered;
            if (end == Entity.Null || end == m_StartNode)
                return inputDeps;

            if (!SlopeEdit.FindPath(EntityManager, m_StartNode, end, m_PathNodes, m_PathEdges))
            {
                m_Overlay.Node(end, ToolOverlay.Invalid);
                Summary = "No connected road between these nodes.";
                return inputDeps;
            }

            var emitted = EmitPath(m_ToolOutputBarrier.CreateCommandBuffer(), m_PathNodes, m_PathEdges, m_Random.NextInt());
            DrawPath(m_Overlay, m_PathNodes, m_PathEdges, locked);
            m_Overlay.Node(end, locked ? ToolOverlay.End : ToolOverlay.Hover);
            Summary = emitted ? Describe(m_PathNodes, m_PathEdges) : "Nothing to change yet: adjust the options.";

            if (!locked)
            {
                if (click)
                    m_EndNode = end;
            }
            else if (emitted && (click || applyRequested))
            {
                applyMode = ApplyMode.Apply;
                if (Mod.Settings.DebugLogging)
                    Mod.Log.Info($"{toolID} applied over {m_PathEdges.Count} edges from {m_StartNode} to {end}");
                Reset();
            }

            return inputDeps;
        }

        private void Reset()
        {
            m_StartNode = Entity.Null;
            m_EndNode = Entity.Null;
            Summary = string.Empty;
            OnSelectionCleared();
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
