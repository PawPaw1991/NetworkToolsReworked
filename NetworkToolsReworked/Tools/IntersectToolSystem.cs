using System.Collections.Generic;
using Colossal.Entities;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using NetworkToolsReworked.Edits;
using NetworkToolsReworked.Undo;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace NetworkToolsReworked.Tools
{
    /// <summary>
    /// Hover a road where another road crosses it without a junction; the crossing nearest the cursor is
    /// previewed as a new junction. Click to lock, then click or press Apply. Right-click steps back or exits.
    /// </summary>
    public partial class IntersectToolSystem : NetEditToolSystem, IPreviewTool
    {
        private TerrainSystem m_TerrainSystem;
        private ToolOverlay m_Overlay;
        private EntityQuery m_EdgeQuery;
        private Unity.Mathematics.Random m_Random;
        private readonly List<(Entity other, Crossings.Hit hit)> m_Crossings = new List<(Entity, Crossings.Hit)>();
        private readonly List<Crossings.Hit> m_Scratch = new List<Crossings.Hit>();
        private Entity m_ScannedEdge;
        private Entity m_LockedEdge;
        private Entity m_LockedOther;
        private Crossings.Hit m_LockedHit;
        private bool m_ApplyRequested;
        private bool m_CancelRequested;

        public override string toolID => "NetworkToolsReworked.IntersectTool";

        public ToolPhase Phase => m_LockedEdge == Entity.Null ? ToolPhase.PickStart : ToolPhase.Review;

        public string Summary { get; private set; } = string.Empty;

        public void RequestApply() => m_ApplyRequested = true;

        public void RequestCancel() => m_CancelRequested = true;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_Overlay = new ToolOverlay(World);
            m_Random = new Unity.Mathematics.Random(0x1A7Eu);
            m_EdgeQuery = GetEntityQuery(ComponentType.ReadOnly<Edge>(), ComponentType.ReadOnly<Curve>(), ComponentType.Exclude<Owner>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
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

        protected override JobHandle OnToolUpdate(JobHandle inputDeps)
        {
            var applyRequested = m_ApplyRequested;
            var cancelRequested = m_CancelRequested;
            m_ApplyRequested = m_CancelRequested = false;

            if (cancelAction.WasPressedThisFrame() || cancelRequested)
            {
                if (m_LockedEdge != Entity.Null)
                    Reset();
                else
                    m_ToolSystem.activeTool = m_DefaultToolSystem;
                Summary = string.Empty;
                return inputDeps;
            }

            applyMode = ApplyMode.Clear;
            UndoRecorder.Begin(EntityManager, toolID);
            Summary = string.Empty;
            m_Overlay.BeginFrame();

            if (m_LockedEdge != Entity.Null && (!IsLiveEdge(m_LockedEdge) || !IsLiveEdge(m_LockedOther)))
                Reset();

            var click = applyAction.WasPressedThisFrame();
            Entity edge, other;
            Crossings.Hit hit;
            if (m_LockedEdge != Entity.Null)
            {
                edge = m_LockedEdge;
                other = m_LockedOther;
                hit = m_LockedHit;
            }
            else
            {
                if (!GetRaycastResult(out Entity hitEntity, out RaycastHit cursor) || !IsLiveEdge(hitEntity))
                    return inputDeps;
                edge = hitEntity;
                if (edge != m_ScannedEdge)
                    Scan(edge);
                m_Overlay.Edge(edge, ToolOverlay.Path);
                if (!Nearest(cursor.m_HitPosition, out other, out hit))
                {
                    Summary = "No road crosses this one without a junction.";
                    return inputDeps;
                }
            }

            m_Overlay.Edge(other, ToolOverlay.Path);
            var heightGap = math.abs(hit.PointA.y - hit.PointB.y);
            if (heightGap > IntersectEdit.kMaxHeightDifference)
            {
                m_Overlay.Point(hit.PointA, 6f, ToolOverlay.Invalid);
                Summary = $"These roads pass {heightGap:0.0} m apart in height here, so they aren't joined.";
                return inputDeps;
            }

            m_Overlay.Point(hit.PointA, 6f, m_LockedEdge != Entity.Null ? ToolOverlay.End : ToolOverlay.Start);
            var terrain = m_TerrainSystem.GetHeightData();
            if (!IntersectEdit.Emit(EntityManager, DefinitionBuffer(), ref terrain, edge, other, hit, m_Random.NextInt()))
                return inputDeps;
            Summary = $"New junction where the two roads cross ({heightGap:0.0} m height difference evened out)";

            if (m_LockedEdge == Entity.Null)
            {
                if (click)
                {
                    m_LockedEdge = edge;
                    m_LockedOther = other;
                    m_LockedHit = hit;
                }
            }
            else if (click || applyRequested)
            {
                applyMode = ApplyMode.Apply;
                UndoRecorder.Commit(Summary);
                if (Mod.Settings.DebugLogging)
                    Mod.Log.Info($"Intersect applied: {edge} x {other}");
                Reset();
            }

            return inputDeps;
        }

        /// <summary>Finds every road crossing the given one, once per hovered road.</summary>
        private void Scan(Entity edge)
        {
            m_ScannedEdge = edge;
            m_Crossings.Clear();
            var e = EntityManager.GetComponentData<Edge>(edge);
            var curve = EntityManager.GetComponentData<Curve>(edge).m_Bezier;

            using var edges = m_EdgeQuery.ToEntityArray(Allocator.Temp);
            using var curves = m_EdgeQuery.ToComponentDataArray<Curve>(Allocator.Temp);
            using var ends = m_EdgeQuery.ToComponentDataArray<Edge>(Allocator.Temp);
            for (var i = 0; i < edges.Length; i++)
            {
                if (edges[i] == edge || !Crossings.BoundsOverlap(curve, curves[i].m_Bezier))
                    continue;
                // Roads that already share a node meet there.
                var o = ends[i];
                if (o.m_Start == e.m_Start || o.m_Start == e.m_End || o.m_End == e.m_Start || o.m_End == e.m_End)
                    continue;
                m_Scratch.Clear();
                Crossings.Find(curve, curves[i].m_Bezier, m_Scratch, endMargin: 2f);
                foreach (var hit in m_Scratch)
                    m_Crossings.Add((edges[i], hit));
            }
        }

        private bool Nearest(float3 cursor, out Entity other, out Crossings.Hit hit)
        {
            other = Entity.Null;
            hit = default;
            var best = float.MaxValue;
            foreach (var (candidate, h) in m_Crossings)
            {
                if (!IsLiveEdge(candidate))
                    continue;
                var d = math.distancesq(cursor.xz, h.PointA.xz);
                if (d < best)
                {
                    best = d;
                    other = candidate;
                    hit = h;
                }
            }
            return other != Entity.Null;
        }

        private bool IsLiveEdge(Entity entity)
        {
            return EntityManager.Exists(entity) && EntityManager.HasComponent<Edge>(entity) && !EntityManager.HasComponent<Deleted>(entity) && !EntityManager.HasComponent<Owner>(entity);
        }

        private void Reset()
        {
            m_LockedEdge = Entity.Null;
            m_LockedOther = Entity.Null;
            m_ScannedEdge = Entity.Null;
            Summary = string.Empty;
        }
    }
}
