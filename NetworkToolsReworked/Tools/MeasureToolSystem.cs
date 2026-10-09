using System.Collections.Generic;
using System.Globalization;
using Colossal.Entities;
using Colossal.Mathematics;
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
    /// Read-only measurements. Hover a road to see its length, grade, tightest curve and height above the
    /// ground at the cursor; parts steeper than the road type allows are marked red. Click a node and
    /// hover (or click) another to measure the road between them: length, straight-line distance and
    /// bearing, height difference, grades and how far the road turns. Nothing is ever changed.
    /// </summary>
    public partial class MeasureToolSystem : ToolBaseSystem, IPreviewTool
    {
        private TerrainSystem m_TerrainSystem;
        private ToolOverlay m_Overlay;
        private readonly List<Entity> m_PathNodes = new List<Entity>();
        private readonly List<Entity> m_PathEdges = new List<Entity>();
        private Entity m_StartNode;
        private Entity m_EndNode;
        private bool m_CancelRequested;

        public override string toolID => "NetworkToolsReworked.MeasureTool";

        public ToolPhase Phase => m_StartNode == Entity.Null ? ToolPhase.PickStart : m_EndNode == Entity.Null ? ToolPhase.PickEnd : ToolPhase.Review;

        public string Summary { get; private set; } = string.Empty;

        /// <summary>Nothing to apply: measuring changes nothing.</summary>
        public void RequestApply()
        {
        }

        public void RequestCancel() => m_CancelRequested = true;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_Overlay = new ToolOverlay(World);
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
            var cancelRequested = m_CancelRequested;
            m_CancelRequested = false;

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

            // Measuring never creates definitions; Clear keeps any stale preview away.
            applyMode = ApplyMode.Clear;
            Summary = string.Empty;
            m_Overlay.BeginFrame();

            if (m_StartNode != Entity.Null && !ToolPicks.IsAlive(EntityManager, m_StartNode))
                Reset();
            if (m_EndNode != Entity.Null && !ToolPicks.IsAlive(EntityManager, m_EndNode))
                m_EndNode = Entity.Null;

            var click = applyAction.WasPressedThisFrame();
            var hasHit = GetRaycastResult(out Entity hitEntity, out RaycastHit hit);
            var hovered = hasHit ? HoveredNode(hitEntity, hit) : Entity.Null;

            if (m_StartNode == Entity.Null)
            {
                if (hasHit && IsLiveEdge(hitEntity))
                {
                    Summary = DescribeEdge(hitEntity, hit);
                    DrawGrades(hitEntity);
                }
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
            {
                Summary = "Hover another node to measure the road between them.";
                return inputDeps;
            }

            m_Overlay.Node(end, locked ? ToolOverlay.End : ToolOverlay.Hover);
            if (!SlopeEdit.FindPath(EntityManager, m_StartNode, end, m_PathNodes, m_PathEdges))
            {
                Summary = $"No connected road between these nodes. {DescribeStraight(m_StartNode, end)}";
                m_Overlay.Line(Position(m_StartNode), Position(end), 0.6f, ToolOverlay.Before);
                return inputDeps;
            }

            foreach (var edge in m_PathEdges)
                DrawGrades(edge);
            Summary = DescribePath();

            if (!locked && click)
                m_EndNode = end;
            return inputDeps;
        }

        private string DescribeEdge(Entity edge, RaycastHit hit)
        {
            var curve = EntityManager.GetComponentData<Curve>(edge);
            var b = curve.m_Bezier;
            var limit = Grades.Limit(EntityManager, edge);
            var steepest = CurveStats.MaxGrade(b);
            var radius = CurveStats.MinRadius(b);

            var t = math.saturate(hit.m_CurvePosition);
            var here = MathUtils.Position(b, t);
            var terrain = m_TerrainSystem.GetHeightData();
            var above = here.y - TerrainUtils.SampleHeight(ref terrain, here);
            var ground = math.abs(above) < 0.5f ? "at ground level" : above > 0f ? $"{F(above)} m above ground" : $"{F(-above)} m below ground";

            var over = steepest > limit + 0.0005f ? $", over the {Pct(limit)} limit (red)" : $" (limit {Pct(limit)})";
            return $"{Name(edge)}: {PathInfo.Distance(curve.m_Length)}, grade here {Signed(CurveStats.Grade(b, t))}, steepest {Pct(steepest)}{over}, " +
                   $"tightest curve {RadiusText(radius)}, {ground} at the cursor";
        }

        private string DescribePath()
        {
            var length = 0f;
            var steepest = 0f;
            var over = 0;
            var radius = float.PositiveInfinity;
            foreach (var edge in m_PathEdges)
            {
                var curve = EntityManager.GetComponentData<Curve>(edge);
                length += curve.m_Length;
                var grade = CurveStats.MaxGrade(curve.m_Bezier);
                steepest = math.max(steepest, grade);
                if (grade > Grades.Limit(EntityManager, edge) + 0.0005f)
                    over++;
                radius = math.min(radius, CurveStats.MinRadius(curve.m_Bezier));
            }

            var start = m_PathNodes[0];
            var end = m_PathNodes[m_PathNodes.Count - 1];
            var rise = Position(end).y - Position(start).y;
            var average = length > 0.01f ? rise / length : 0f;
            var turn = CurveStats.Turn(Direction(m_PathEdges[0], start, leaving: true), Direction(m_PathEdges[m_PathEdges.Count - 1], end, leaving: false));
            var turnText = math.abs(turn) < 1f ? "runs straight on" : $"turns {math.abs(turn):0}° {(turn > 0f ? "left" : "right")}";
            var steep = over > 0 ? $", {over} segment(s) over their grade limit (red)" : "";
            var segments = m_PathEdges.Count == 1 ? "1 segment" : $"{m_PathEdges.Count} segments";

            return $"{segments}, {PathInfo.Distance(length)} along the road. {DescribeStraight(start, end)} " +
                   $"Height {PathInfo.Signed(rise)} m, average {Signed(average)}, steepest {Pct(steepest)}{steep}. " +
                   $"Tightest curve {RadiusText(radius)}; the road {turnText}.";
        }

        /// <summary>Straight-line distance and compass bearing (0° = +z) between two nodes.</summary>
        private string DescribeStraight(Entity from, Entity to)
        {
            var delta = Position(to) - Position(from);
            var bearing = math.degrees(math.atan2(delta.x, delta.z));
            if (bearing < 0f)
                bearing += 360f;
            return $"Straight line {PathInfo.Distance(math.length(delta.xz))} at {bearing:0}°.";
        }

        /// <summary>Marks the parts of an edge steeper than its road type allows.</summary>
        private void DrawGrades(Entity edge)
        {
            var b = EntityManager.GetComponentData<Curve>(edge).m_Bezier;
            var limit = Grades.Limit(EntityManager, edge);
            var width = m_Overlay.Width(edge);
            const int pieces = 16;
            for (var k = 0; k < pieces; k++)
            {
                var t0 = (float)k / pieces;
                var t1 = (float)(k + 1) / pieces;
                var grade = math.max(math.abs(CurveStats.Grade(b, t0)), math.abs(CurveStats.Grade(b, t1)));
                var color = grade > limit + 0.0005f ? ToolOverlay.Invalid : Grades.ColorFor(grade, limit);
                m_Overlay.Bezier(MathUtils.Cut(b, new float2(t0, t1)), width, color);
            }
        }

        private float3 Direction(Entity edge, Entity node, bool leaving)
        {
            var b = EntityManager.GetComponentData<Curve>(edge).m_Bezier;
            var forward = EntityManager.GetComponentData<Edge>(edge).m_Start == node == leaving;
            if (leaving)
                return forward ? MathUtils.StartTangent(b) : -MathUtils.EndTangent(b);
            return forward ? MathUtils.EndTangent(b) : -MathUtils.StartTangent(b);
        }

        private string Name(Entity edge)
        {
            var prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            var prefab = prefabSystem.GetPrefab<PrefabBase>(EntityManager.GetComponentData<PrefabRef>(edge).m_Prefab);
            return prefab != null ? prefab.name : "Road";
        }

        private float3 Position(Entity node) => EntityManager.GetComponentData<Node>(node).m_Position;

        private bool IsLiveEdge(Entity entity)
        {
            return EntityManager.Exists(entity) && EntityManager.HasComponent<Edge>(entity) && !EntityManager.HasComponent<Deleted>(entity) && !EntityManager.HasComponent<Owner>(entity);
        }

        private Entity HoveredNode(Entity entity, RaycastHit hit)
        {
            if (EntityManager.HasComponent<Node>(entity))
                return entity;
            if (EntityManager.HasComponent<Owner>(entity) || !EntityManager.TryGetComponent(entity, out Edge edge))
                return Entity.Null;
            return hit.m_CurvePosition < 0.5f ? edge.m_Start : edge.m_End;
        }

        private void Reset()
        {
            m_StartNode = Entity.Null;
            m_EndNode = Entity.Null;
            Summary = string.Empty;
        }

        private static string RadiusText(float radius) => float.IsInfinity(radius) || radius > 5000f ? "none (straight)" : $"{PathInfo.Distance(radius)} radius";

        private static string Pct(float grade) => (grade * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%";

        private static string Signed(float grade) => PathInfo.Signed(grade * 100f) + "%";

        private static string F(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
