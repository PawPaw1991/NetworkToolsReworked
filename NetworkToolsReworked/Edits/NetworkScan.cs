using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Mathematics;
using Game.Net;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkToolsReworked.Edits
{
    public enum IssueKind
    {
        Overlapping,
        LooseNode,
        TinySegment,
        NearMiss,
        TooSteep,
        Disconnected,
    }

    public struct Issue
    {
        public IssueKind Kind;

        /// <summary>The road or node the issue is about.</summary>
        public Entity A;

        /// <summary>The other road or node, for issues between two of them.</summary>
        public Entity B;

        public float3 Position;
        public string Text;

        /// <summary>True if <see cref="NetworkScan.EmitFix"/> can repair it.</summary>
        public bool CanFix;
    }

    /// <summary>
    /// Read-only checks over every road, track and path in the city: overlapping roads, loose nodes,
    /// tiny segments, nodes that nearly meet but aren't joined, grades over the road type's limit, and
    /// small pieces of road not joined to the rest. Some issues have a safe fix through definitions.
    /// </summary>
    public static class NetworkScan
    {
        public const int kMaxIssues = 300;

        private const float kOverlapDistance = 1f;
        private const float kNearMissDistance = 1.5f;
        private const float kNearMissHeight = 2f;
        private const int kSmallFragment = 25;

        public static void Run(EntityManager em, EntityQuery edgeQuery, EntityQuery nodeQuery, float minLength, List<Issue> issues)
        {
            issues.Clear();
            using var edges = edgeQuery.ToEntityArray(Allocator.Temp);
            using var nodes = nodeQuery.ToEntityArray(Allocator.Temp);

            var roadEdges = new List<Entity>(edges.Length);
            foreach (var edge in edges)
                if (IsNetwork(em, edge))
                    roadEdges.Add(edge);

            FindOverlaps(em, roadEdges, issues);
            FindLooseNodes(em, nodes, issues);
            FindTinySegments(em, roadEdges, minLength, issues);
            FindNearMisses(em, nodes, issues);
            FindSteep(em, roadEdges, issues);
            FindFragments(em, roadEdges, issues);

            if (issues.Count > kMaxIssues)
                issues.RemoveRange(kMaxIssues, issues.Count - kMaxIssues);
        }

        /// <summary>True if the issue's roads and nodes are all still there.</summary>
        public static bool IsLive(EntityManager em, in Issue issue)
        {
            return Live(em, issue.A) && (issue.B == Entity.Null || Live(em, issue.B));
        }

        /// <summary>Writes the fix for a fixable issue. Returns false if it can't be fixed (any more).</summary>
        public static bool EmitFix(EntityManager em, EntityCommandBuffer ecb, in Issue issue, int randomSeed)
        {
            if (!issue.CanFix || !IsLive(em, issue))
                return false;
            switch (issue.Kind)
            {
                case IssueKind.Overlapping:
                    NetDefinitions.DeleteEdge(em, ecb, issue.B, randomSeed);
                    return true;
                case IssueKind.LooseNode:
                    NetDefinitions.DeleteNode(em, ecb, issue.A, randomSeed);
                    return true;
                case IssueKind.TinySegment:
                    return NetDefinitions.MergeAtNode(em, ecb, issue.B, randomSeed);
                default:
                    return false;
            }
        }

        private static void FindOverlaps(EntityManager em, List<Entity> edges, List<Issue> issues)
        {
            var grid = new Dictionary<int2, List<int>>();
            var curves = new Bezier4x3[edges.Count];
            for (var i = 0; i < edges.Count; i++)
            {
                curves[i] = em.GetComponentData<Curve>(edges[i]).m_Bezier;
                var cell = Cell(MathUtils.Position(curves[i], 0.5f), 4f);
                if (!grid.TryGetValue(cell, out var list))
                    grid[cell] = list = new List<int>();
                list.Add(i);
            }

            for (var i = 0; i < edges.Count; i++)
            {
                var mid = MathUtils.Position(curves[i], 0.5f);
                var cell = Cell(mid, 4f);
                for (var dx = -1; dx <= 1; dx++)
                for (var dz = -1; dz <= 1; dz++)
                {
                    if (!grid.TryGetValue(cell + new int2(dx, dz), out var list))
                        continue;
                    foreach (var j in list)
                    {
                        if (j <= i || !SameCourse(curves[i], curves[j]))
                            continue;
                        issues.Add(new Issue
                        {
                            Kind = IssueKind.Overlapping,
                            A = edges[i],
                            B = edges[j],
                            Position = mid,
                            Text = "Two roads lie on top of each other. Fix removes one of them.",
                            CanFix = true,
                        });
                    }
                }
            }
        }

        private static void FindLooseNodes(EntityManager em, NativeArray<Entity> nodes, List<Issue> issues)
        {
            foreach (var node in nodes)
            {
                if (!IsNetwork(em, node) || !em.HasBuffer<ConnectedEdge>(node) || em.GetBuffer<ConnectedEdge>(node, isReadOnly: true).Length > 0)
                    continue;
                issues.Add(new Issue
                {
                    Kind = IssueKind.LooseNode,
                    A = node,
                    Position = em.GetComponentData<Node>(node).m_Position,
                    Text = "A node with no roads attached. Fix removes it.",
                    CanFix = true,
                });
            }
        }

        private static void FindTinySegments(EntityManager em, List<Entity> edges, float minLength, List<Issue> issues)
        {
            if (minLength <= 0f)
                return;
            foreach (var edge in edges)
            {
                var length = em.GetComponentData<Curve>(edge).m_Length;
                if (length >= minLength)
                    continue;

                // It can be fixed by merging it into the road beyond one of its ends.
                var e = em.GetComponentData<Edge>(edge);
                var mergeAt = NetDefinitions.CanMergeAtNode(em, e.m_End, out _, out _) ? e.m_End
                    : NetDefinitions.CanMergeAtNode(em, e.m_Start, out _, out _) ? e.m_Start : Entity.Null;
                issues.Add(new Issue
                {
                    Kind = IssueKind.TinySegment,
                    A = edge,
                    B = mergeAt,
                    Position = MathUtils.Position(em.GetComponentData<Curve>(edge).m_Bezier, 0.5f),
                    Text = mergeAt != Entity.Null
                        ? $"A segment only {length:0.0} m long. Fix merges it into the road next to it."
                        : $"A segment only {length:0.0} m long, between junctions, so it can't be merged away.",
                    CanFix = mergeAt != Entity.Null,
                });
            }
        }

        private static void FindNearMisses(EntityManager em, NativeArray<Entity> nodes, List<Issue> issues)
        {
            var grid = new Dictionary<int2, List<Entity>>();
            var live = new List<Entity>();
            foreach (var node in nodes)
            {
                if (!IsNetwork(em, node))
                    continue;
                live.Add(node);
                var cell = Cell(em.GetComponentData<Node>(node).m_Position, 4f);
                if (!grid.TryGetValue(cell, out var list))
                    grid[cell] = list = new List<Entity>();
                list.Add(node);
            }

            foreach (var node in live)
            {
                var p = em.GetComponentData<Node>(node).m_Position;
                var cell = Cell(p, 4f);
                for (var dx = -1; dx <= 1; dx++)
                for (var dz = -1; dz <= 1; dz++)
                {
                    if (!grid.TryGetValue(cell + new int2(dx, dz), out var list))
                        continue;
                    foreach (var other in list)
                    {
                        if (other.Index <= node.Index)
                            continue;
                        var q = em.GetComponentData<Node>(other).m_Position;
                        if (math.distance(p.xz, q.xz) > kNearMissDistance || math.abs(p.y - q.y) > kNearMissHeight || Joined(em, node, other))
                            continue;
                        issues.Add(new Issue
                        {
                            Kind = IssueKind.NearMiss,
                            A = node,
                            B = other,
                            Position = p,
                            Text = $"Two nodes {math.distance(p, q):0.0} m apart that aren't joined. Roads ending here probably don't connect: rebuild the end onto the other node.",
                        });
                    }
                }
            }
        }

        private static void FindSteep(EntityManager em, List<Entity> edges, List<Issue> issues)
        {
            foreach (var edge in edges)
            {
                var curve = em.GetComponentData<Curve>(edge).m_Bezier;
                var limit = Limit(em, edge);
                var grade = CurveStats.MaxGrade(curve);
                if (grade <= limit + 0.005f)
                    continue;
                issues.Add(new Issue
                {
                    Kind = IssueKind.TooSteep,
                    A = edge,
                    Position = MathUtils.Position(curve, 0.5f),
                    Text = $"Steepest grade {grade * 100f:0.0}%, over this road type's {limit * 100f:0}% limit. Re-grade it with Slope & Curve.",
                });
            }
        }

        /// <summary>Small groups of roads (not tracks or paths) that aren't joined to the rest of the roads.</summary>
        private static void FindFragments(EntityManager em, List<Entity> edges, List<Issue> issues)
        {
            var parent = new Dictionary<Entity, Entity>();
            var roads = new List<Entity>();
            foreach (var edge in edges)
            {
                if (RestyleEdit.KindOf(em, em.GetComponentData<PrefabRef>(edge).m_Prefab) != RestyleEdit.NetKind.Road)
                    continue;
                roads.Add(edge);
                var e = em.GetComponentData<Edge>(edge);
                Union(parent, e.m_Start, e.m_End);
            }

            var groups = new Dictionary<Entity, List<Entity>>();
            foreach (var edge in roads)
            {
                var root = Find(parent, em.GetComponentData<Edge>(edge).m_Start);
                if (!groups.TryGetValue(root, out var list))
                    groups[root] = list = new List<Entity>();
                list.Add(edge);
            }

            var largest = 0;
            foreach (var group in groups.Values)
                largest = math.max(largest, group.Count);

            foreach (var group in groups.Values)
            {
                if (group.Count == largest || group.Count > kSmallFragment)
                    continue;
                var first = group[0];
                var count = group.Count == 1 ? "1 road" : $"{group.Count} roads";
                issues.Add(new Issue
                {
                    Kind = IssueKind.Disconnected,
                    A = first,
                    Position = MathUtils.Position(em.GetComponentData<Curve>(first).m_Bezier, 0.5f),
                    Text = $"{count} not joined to the rest of the road network.",
                });
            }
        }

        private static bool SameCourse(Bezier4x3 a, Bezier4x3 b)
        {
            var d2 = kOverlapDistance * kOverlapDistance;
            var forward = math.distancesq(a.a, b.a) < d2 && math.distancesq(a.d, b.d) < d2;
            var backward = math.distancesq(a.a, b.d) < d2 && math.distancesq(a.d, b.a) < d2;
            return (forward || backward) && math.distancesq(MathUtils.Position(a, 0.5f), MathUtils.Position(b, 0.5f)) < d2;
        }

        private static bool Joined(EntityManager em, Entity a, Entity b)
        {
            if (!em.HasBuffer<ConnectedEdge>(a))
                return false;
            foreach (var c in em.GetBuffer<ConnectedEdge>(a, isReadOnly: true))
                if (em.TryGetComponent(c.m_Edge, out Edge e) && (e.m_Start == b || e.m_End == b))
                    return true;
            return false;
        }

        private static float Limit(EntityManager em, Entity edge)
        {
            if (em.TryGetComponent(edge, out PrefabRef prefab) && em.TryGetComponent(prefab.m_Prefab, out NetGeometryData geometry) && geometry.m_MaxSlopeSteepness > 0f)
                return geometry.m_MaxSlopeSteepness;
            return 0.12f;
        }

        /// <summary>Roads, tracks and paths only, so power lines, pipes and the like are left out.</summary>
        private static bool IsNetwork(EntityManager em, Entity entity)
        {
            return em.TryGetComponent(entity, out PrefabRef prefab) && RestyleEdit.KindOf(em, prefab.m_Prefab) != RestyleEdit.NetKind.Other;
        }

        private static bool Live(EntityManager em, Entity entity)
        {
            return entity != Entity.Null && em.Exists(entity) && !em.HasComponent<Game.Common.Deleted>(entity);
        }

        private static int2 Cell(float3 p, float size) => new int2(math.floor(p.xz / size));

        private static Entity Find(Dictionary<Entity, Entity> parent, Entity x)
        {
            while (parent.TryGetValue(x, out var p) && p != x)
            {
                if (parent.TryGetValue(p, out var gp))
                    parent[x] = gp;
                x = p;
            }
            return x;
        }

        private static void Union(Dictionary<Entity, Entity> parent, Entity a, Entity b)
        {
            if (!parent.ContainsKey(a)) parent[a] = a;
            if (!parent.ContainsKey(b)) parent[b] = b;
            var ra = Find(parent, a);
            var rb = Find(parent, b);
            if (ra != rb)
                parent[ra] = rb;
        }
    }
}
