using System.Collections.Generic;
using Colossal.Entities;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Rendering;
using Game.Tools;
using NetworkToolsReworked.Edits;
using NetworkToolsReworked.Undo;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace NetworkToolsReworked.Tools
{
    /// <summary>
    /// Scans the whole city for network problems (see <see cref="NetworkScan"/>) and lists them in the
    /// panel. Every issue is marked on the map; picking one in the list moves the camera there and, if it
    /// has a safe fix, previews the fix, which is applied with a click or Apply. The list is scanned again
    /// after every fix and on request.
    /// </summary>
    public partial class HealthToolSystem : ToolBaseSystem, IPreviewTool
    {
        private static readonly Color kFixable = new Color(1f, 0.65f, 0.15f, 0.8f);
        private static readonly Color kReport = new Color(1f, 0.25f, 0.2f, 0.8f);

        private ToolOutputBarrier m_ToolOutputBarrier;
        private CameraUpdateSystem m_CameraUpdateSystem;
        private ToolOverlay m_Overlay;
        private EntityQuery m_EdgeQuery;
        private EntityQuery m_NodeQuery;
        private Unity.Mathematics.Random m_Random;
        private readonly List<Issue> m_Issues = new List<Issue>();
        private int m_Selected = -1;
        private int m_RescanIn;
        private bool m_ApplyRequested;
        private bool m_CancelRequested;
        private int m_SelectRequested = -1;

        public override string toolID => "NetworkToolsReworked.HealthTool";

        public ToolPhase Phase => m_Selected >= 0 ? ToolPhase.Review : ToolPhase.PickStart;

        public string Summary { get; private set; } = string.Empty;

        /// <summary>The issues found by the last scan, in list order.</summary>
        public IReadOnlyList<Issue> Issues => m_Issues;

        /// <summary>Index of the issue being looked at, or -1.</summary>
        public int Selected => m_Selected;

        /// <summary>Bumped on every scan, so the panel knows when to refresh the list.</summary>
        public int Version { get; private set; }

        public void RequestApply() => m_ApplyRequested = true;

        public void RequestCancel() => m_CancelRequested = true;

        /// <summary>Looks at an issue from the list (next update).</summary>
        public void RequestSelect(int index) => m_SelectRequested = index;

        public void RequestRescan() => m_RescanIn = 1;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_CameraUpdateSystem = World.GetOrCreateSystemManaged<CameraUpdateSystem>();
            m_Overlay = new ToolOverlay(World);
            m_Random = new Unity.Mathematics.Random(0x4EA1u);
            m_EdgeQuery = GetEntityQuery(ComponentType.ReadOnly<Edge>(), ComponentType.ReadOnly<Curve>(), ComponentType.Exclude<Owner>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
            m_NodeQuery = GetEntityQuery(ComponentType.ReadOnly<Node>(), ComponentType.Exclude<Owner>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            m_Selected = -1;
            m_RescanIn = 1;
            applyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            m_Selected = -1;
            m_Issues.Clear();
            Version++;
            applyAction.shouldBeEnabled = false;
            cancelAction.shouldBeEnabled = false;
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            m_ToolRaycastSystem.typeMask = TypeMask.Terrain;
        }

        public override PrefabBase GetPrefab() => null;

        public override bool TrySetPrefab(PrefabBase prefab) => false;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var applyRequested = m_ApplyRequested;
            var cancelRequested = m_CancelRequested;
            var select = m_SelectRequested;
            m_ApplyRequested = m_CancelRequested = false;
            m_SelectRequested = -1;

            if (cancelAction.WasPressedThisFrame() || cancelRequested)
            {
                if (m_Selected >= 0)
                    m_Selected = -1;
                else
                    m_ToolSystem.activeTool = m_DefaultToolSystem;
                return inputDeps;
            }

            applyMode = ApplyMode.Clear;
            UndoRecorder.Begin(EntityManager, toolID);
            m_Overlay.BeginFrame();

            // Fixes take a frame or two to land, so the scan after a fix waits a little.
            if (m_RescanIn > 0 && --m_RescanIn == 0)
                Scan();

            if (select >= 0 && select < m_Issues.Count)
            {
                m_Selected = select;
                JumpTo(m_Issues[select].Position);
            }
            if (m_Selected >= m_Issues.Count || (m_Selected >= 0 && !NetworkScan.IsLive(EntityManager, m_Issues[m_Selected])))
            {
                m_Selected = -1;
                m_RescanIn = 1;
            }

            for (var i = 0; i < m_Issues.Count; i++)
                if (i != m_Selected)
                    m_Overlay.Point(m_Issues[i].Position, 6f, m_Issues[i].CanFix ? kFixable : kReport);

            if (m_Selected < 0)
            {
                Summary = Counts();
                return inputDeps;
            }

            var issue = m_Issues[m_Selected];
            m_Overlay.Point(issue.Position, 14f, ToolOverlay.End);
            Highlight(issue.A);
            Highlight(issue.B);

            var fixable = NetworkScan.EmitFix(EntityManager, m_ToolOutputBarrier.CreateCommandBuffer(), issue, m_Random.NextInt());
            Summary = $"{m_Selected + 1} of {m_Issues.Count}: {issue.Text}" + (fixable ? " Click or press Apply to fix it." : "");

            if (fixable && (applyAction.WasPressedThisFrame() || applyRequested))
            {
                applyMode = ApplyMode.Apply;
                UndoRecorder.Commit();
                if (Mod.Settings.DebugLogging)
                    Mod.Log.Info($"Health check fixed {issue.Kind} at {issue.Position}");
                m_Selected = -1;
                m_RescanIn = 3;
            }
            return inputDeps;
        }

        private void Scan()
        {
            NetworkScan.Run(EntityManager, m_EdgeQuery, m_NodeQuery, Mod.Settings.HealthMinLength, m_Issues);
            m_Selected = -1;
            Version++;
            if (Mod.Settings.DebugLogging)
                Mod.Log.Info($"Health check found {m_Issues.Count} issues");
        }

        private string Counts()
        {
            if (m_Issues.Count == 0)
                return "No problems found.";
            var counts = new Dictionary<IssueKind, int>();
            var fixable = 0;
            foreach (var issue in m_Issues)
            {
                counts.TryGetValue(issue.Kind, out var n);
                counts[issue.Kind] = n + 1;
                if (issue.CanFix)
                    fixable++;
            }
            var parts = new List<string>();
            foreach (var pair in counts)
                parts.Add($"{pair.Value} {Name(pair.Key)}");
            var capped = m_Issues.Count >= NetworkScan.kMaxIssues ? $" (showing the first {NetworkScan.kMaxIssues})" : "";
            return $"{m_Issues.Count} found{capped}: {string.Join(", ", parts)}. {fixable} can be fixed here (orange on the map).";
        }

        public static string Name(IssueKind kind)
        {
            switch (kind)
            {
                case IssueKind.Overlapping: return "overlapping";
                case IssueKind.LooseNode: return "loose node";
                case IssueKind.TinySegment: return "tiny segment";
                case IssueKind.NearMiss: return "unjoined ends";
                case IssueKind.TooSteep: return "too steep";
                default: return "not connected";
            }
        }

        private void Highlight(Entity entity)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity))
                return;
            if (EntityManager.HasComponent<Edge>(entity))
                m_Overlay.Edge(entity, ToolOverlay.Locked);
            else if (EntityManager.HasComponent<Node>(entity))
                m_Overlay.Node(entity, ToolOverlay.End);
        }

        /// <summary>Centres the camera on a point, keeping its angle and zoom.</summary>
        private void JumpTo(float3 position)
        {
            var controller = m_CameraUpdateSystem.activeCameraController;
            if (controller != null)
                controller.pivot = position;
        }
    }
}
