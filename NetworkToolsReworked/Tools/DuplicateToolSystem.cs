using System;
using System.Collections.Generic;
using Colossal.Entities;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using NetworkToolsReworked.Edits;
using NetworkToolsReworked.Presets;
using NetworkToolsReworked.Undo;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace NetworkToolsReworked.Tools
{
    /// <summary>
    /// Copies a group of roads (the road between two picked nodes, Move It's selection, or a saved layout)
    /// and places it again under the cursor, turned or mirrored. Click to lock the spot, adjust, then
    /// click or Apply; the same group can then be placed again. Groups can be saved as layouts and
    /// placed in other cities. See <see cref="LayoutEdit"/> and <see cref="LayoutStore"/>.
    /// </summary>
    public partial class DuplicateToolSystem : ToolBaseSystem, IPreviewTool
    {
        private ToolOutputBarrier m_ToolOutputBarrier;
        private TerrainSystem m_TerrainSystem;
        private ToolOverlay m_Overlay;
        private Unity.Mathematics.Random m_Random;
        private readonly List<Entity> m_PathNodes = new List<Entity>();
        private readonly List<Entity> m_PathEdges = new List<Entity>();
        private readonly HashSet<Entity> m_Selected = new HashSet<Entity>();
        private Entity m_StartNode;
        private Layout m_Layout;
        private int m_Missing;
        private bool m_Locked;
        private float3 m_Target;
        private string m_Notice = string.Empty;
        private bool m_ApplyRequested;
        private bool m_CancelRequested;
        private bool m_UseSelectionRequested;
        private int m_LoadRequested = -1;
        private bool m_SaveRequested;

        public override string toolID => "NetworkToolsReworked.DuplicateTool";

        public ToolPhase Phase => m_Layout != null ? (m_Locked ? ToolPhase.Review : ToolPhase.Place) : m_StartNode == Entity.Null ? ToolPhase.PickStart : ToolPhase.PickEnd;

        public string Summary { get; private set; } = string.Empty;

        /// <summary>True while a group is picked, so it can be saved.</summary>
        public bool HasGroup => m_Layout != null;

        public void RequestApply() => m_ApplyRequested = true;

        public void RequestCancel() => m_CancelRequested = true;

        public void RequestUseSelection() => m_UseSelectionRequested = true;

        public void RequestLoad(int index) => m_LoadRequested = index;

        public void RequestSave() => m_SaveRequested = true;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_Overlay = new ToolOverlay(World);
            m_Random = new Unity.Mathematics.Random(0xD0B1u);
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            Clear();
            applyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            Clear();
            applyAction.shouldBeEnabled = false;
            cancelAction.shouldBeEnabled = false;
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            m_ToolRaycastSystem.typeMask = TypeMask.Net | TypeMask.Terrain;
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
                if (m_Locked)
                    m_Locked = false;
                else if (m_Layout != null || m_StartNode != Entity.Null)
                    Clear();
                else
                    m_ToolSystem.activeTool = m_DefaultToolSystem;
                Summary = string.Empty;
                return inputDeps;
            }

            applyMode = ApplyMode.Clear;
            UndoRecorder.Begin(EntityManager, toolID);
            Summary = string.Empty;
            m_Overlay.BeginFrame();
            var terrain = m_TerrainSystem.GetHeightData();

            HandleRequests(ref terrain);

            var click = applyAction.WasPressedThisFrame();
            var hasHit = GetRaycastResult(out Entity hitEntity, out RaycastHit hit);

            if (m_Layout == null)
                return PickGroup(click, hasHit, hitEntity, hit, ref terrain, inputDeps);

            var target = m_Locked ? m_Target : hasHit ? hit.m_HitPosition : float3.zero;
            if (!m_Locked && !hasHit)
            {
                Summary = Describe("Move the cursor to where the copy should go.");
                return inputDeps;
            }

            var s = Mod.Settings;
            var placed = LayoutEdit.Place(ref terrain, m_Layout, target, s.DuplicateMode, s.DuplicateAngle);
            LayoutEdit.Emit(m_ToolOutputBarrier.CreateCommandBuffer(), ref terrain, m_Layout, placed, s.DuplicateMode, m_Random.NextInt());
            foreach (var curve in placed)
                m_Overlay.Bezier(curve, 2f, m_Locked ? ToolOverlay.Locked : ToolOverlay.Path);
            m_Overlay.Point(target, 4f, m_Locked ? ToolOverlay.End : ToolOverlay.Start);
            Summary = Describe(m_Locked ? "Click or press Apply to build it." : "Click to lock it here.");

            if (!m_Locked)
            {
                if (click)
                {
                    m_Locked = true;
                    m_Target = target;
                }
            }
            else if (click || applyRequested)
            {
                applyMode = ApplyMode.Apply;
                UndoRecorder.Commit(Summary);
                if (s.DebugLogging)
                    Mod.Log.Info($"Duplicate placed {m_Layout.Roads.Count} roads at {target}");
                // Keep the group, so it can be placed again.
                m_Locked = false;
            }
            return inputDeps;
        }

        /// <summary>Picking the group: a start node, then an end node (or Move It's selection, or a saved layout).</summary>
        private JobHandle PickGroup(bool click, bool hasHit, Entity hitEntity, RaycastHit hit, ref TerrainHeightData terrain, JobHandle inputDeps)
        {
            if (m_StartNode != Entity.Null && !ToolPicks.IsAlive(EntityManager, m_StartNode))
                m_StartNode = Entity.Null;

            var hovered = hasHit ? HoveredNode(hitEntity, hit) : Entity.Null;
            if (m_StartNode == Entity.Null)
            {
                Summary = m_Notice;
                if (hovered != Entity.Null)
                {
                    m_Overlay.Node(hovered, ToolOverlay.Hover);
                    if (click)
                    {
                        m_StartNode = hovered;
                        m_Notice = string.Empty;
                    }
                }
                return inputDeps;
            }

            m_Overlay.Node(m_StartNode, ToolOverlay.Start);
            if (hovered == Entity.Null || hovered == m_StartNode)
                return inputDeps;
            if (!SlopeEdit.FindPath(EntityManager, m_StartNode, hovered, m_PathNodes, m_PathEdges))
            {
                m_Overlay.Node(hovered, ToolOverlay.Invalid);
                Summary = "No connected road between these nodes.";
                return inputDeps;
            }
            foreach (var edge in m_PathEdges)
                m_Overlay.Edge(edge, ToolOverlay.Path);
            m_Overlay.Node(hovered, ToolOverlay.Hover);
            Summary = $"{PathInfo.Describe(EntityManager, m_PathNodes, m_PathEdges)}. Click to copy it.";
            if (click)
                Take(LayoutEdit.Capture(EntityManager, m_PrefabSystem, ref terrain, m_PathEdges), 0);
            return inputDeps;
        }

        private void HandleRequests(ref TerrainHeightData terrain)
        {
            if (m_UseSelectionRequested)
            {
                m_UseSelectionRequested = false;
                if (!MoveItSelection.Collect(EntityManager, World, m_Selected))
                    m_Notice = "Move It isn't installed or its selection can't be read.";
                else if (m_Selected.Count == 0)
                    m_Notice = "Nothing usable is selected in Move It: select roads (or the nodes at both ends of them) first.";
                else
                    Take(LayoutEdit.Capture(EntityManager, m_PrefabSystem, ref terrain, m_Selected), 0);
            }

            if (m_LoadRequested >= 0)
            {
                var index = m_LoadRequested;
                m_LoadRequested = -1;
                var all = LayoutStore.All;
                if (index < all.Count)
                {
                    var layout = all[index];
                    var missing = LayoutStore.Resolve(layout, m_PrefabSystem);
                    if (missing == layout.Roads.Count)
                        m_Notice = $"None of the road types in \"{layout.Name}\" are in this game.";
                    else
                        Take(layout, missing);
                }
            }

            if (m_SaveRequested)
            {
                m_SaveRequested = false;
                if (m_Layout != null)
                {
                    var name = $"{m_Layout.Roads.Count} roads, {PathInfo.Distance(m_Layout.Length)} ({DateTime.Now.ToString("MMM d HH:mm", System.Globalization.CultureInfo.InvariantCulture)})";
                    if (string.IsNullOrEmpty(m_Layout.Name))
                        m_Layout.Name = name;
                    LayoutStore.Save(m_Layout);
                }
            }
        }

        private void Take(Layout layout, int missing)
        {
            m_Layout = layout.Roads.Count > 0 ? layout : null;
            m_Missing = missing;
            m_StartNode = Entity.Null;
            m_Locked = false;
        }

        private string Describe(string next)
        {
            var s = Mod.Settings;
            var how = s.DuplicateMode == DuplicateMode.Mirror
                ? $"mirrored across an axis at {s.DuplicateAngle:0}° to its main direction"
                : math.abs(s.DuplicateAngle) > 0.01f ? $"turned {s.DuplicateAngle:0}°" : "as it is";
            var name = string.IsNullOrEmpty(m_Layout.Name) ? "" : $"\"{m_Layout.Name}\": ";
            var missing = m_Missing > 0 ? $" {m_Missing} road(s) of types not in this game are left out." : "";
            return $"{name}{m_Layout.Roads.Count} roads, {PathInfo.Distance(m_Layout.Length)}, {how}. {next}{missing}";
        }

        private void Clear()
        {
            m_Layout = null;
            m_StartNode = Entity.Null;
            m_Locked = false;
            m_Missing = 0;
            m_Notice = string.Empty;
            Summary = string.Empty;
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
