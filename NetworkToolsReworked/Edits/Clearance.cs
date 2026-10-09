using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkToolsReworked.Edits
{
    /// <summary>
    /// Finds where new road curves pass over or under other roads with too little height between them.
    /// The game's own validation still decides what can be built; this is an early warning.
    /// </summary>
    public static class Clearance
    {
        public struct Problem
        {
            public float3 Position;
            public float Gap;
        }

        /// <param name="ignore">Roads being replaced by the new curves, and roads joined to them.</param>
        public static void Check(EntityManager em, EntityQuery edgeQuery, IReadOnlyList<Bezier4x3> curves, HashSet<Entity> ignore, float minimum, List<Problem> problems)
        {
            problems.Clear();
            if (curves == null || curves.Count == 0)
                return;

            var min = new float2(float.MaxValue);
            var max = new float2(float.MinValue);
            foreach (var c in curves)
            {
                var b = MathUtils.Bounds(c.xz);
                min = math.min(min, b.min);
                max = math.max(max, b.max);
            }

            using var edges = edgeQuery.ToEntityArray(Allocator.Temp);
            using var others = edgeQuery.ToComponentDataArray<Curve>(Allocator.Temp);
            var hits = new List<Crossings.Hit>();
            for (var i = 0; i < edges.Length; i++)
            {
                if (ignore.Contains(edges[i]))
                    continue;
                var other = others[i].m_Bezier;
                var ob = MathUtils.Bounds(other.xz);
                if (!math.all(ob.min <= max) || !math.all(min <= ob.max))
                    continue;

                foreach (var curve in curves)
                {
                    hits.Clear();
                    Crossings.Find(curve, other, hits, endMargin: 2f);
                    foreach (var hit in hits)
                    {
                        var gap = math.abs(hit.PointA.y - hit.PointB.y);
                        if (gap < minimum)
                            problems.Add(new Problem { Position = hit.PointA, Gap = gap });
                    }
                }
            }
        }

        /// <summary>The chain edges plus every road attached to the chain's nodes.</summary>
        public static HashSet<Entity> Neighbourhood(EntityManager em, List<Entity> nodes, List<Entity> edges)
        {
            var set = new HashSet<Entity>(edges);
            foreach (var node in nodes)
                if (em.HasBuffer<ConnectedEdge>(node))
                    foreach (var c in em.GetBuffer<ConnectedEdge>(node, isReadOnly: true))
                        set.Add(c.m_Edge);
            return set;
        }
    }
}
