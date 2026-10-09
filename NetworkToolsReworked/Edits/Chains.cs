using System.Collections.Generic;
using Game.Net;
using Unity.Entities;

namespace NetworkToolsReworked.Edits
{
    /// <summary>A run of connected roads, as ordered node and edge lists (nodes.Count == edges.Count + 1).</summary>
    public sealed class Chain
    {
        public readonly List<Entity> Nodes = new List<Entity>();
        public readonly List<Entity> Edges = new List<Entity>();
    }

    /// <summary>Splits a loose set of roads into chains that break at junctions and dead ends.</summary>
    public static class Chains
    {
        public static List<Chain> Build(EntityManager em, HashSet<Entity> edges)
        {
            var chains = new List<Chain>();
            var byNode = new Dictionary<Entity, List<Entity>>();
            foreach (var edge in edges)
            {
                var e = em.GetComponentData<Edge>(edge);
                Add(byNode, e.m_Start, edge);
                Add(byNode, e.m_End, edge);
            }

            var used = new HashSet<Entity>();

            // Start at every node where the selection doesn't simply pass through.
            foreach (var pair in byNode)
            {
                if (pair.Value.Count == 2)
                    continue;
                foreach (var edge in pair.Value)
                    if (!used.Contains(edge))
                        chains.Add(Walk(em, byNode, used, pair.Key, edge));
            }

            // Whatever is left forms closed loops; open each one at an arbitrary node.
            foreach (var edge in edges)
            {
                if (used.Contains(edge))
                    continue;
                chains.Add(Walk(em, byNode, used, em.GetComponentData<Edge>(edge).m_Start, edge));
            }

            chains.RemoveAll(c => !Editable(em, c));
            return chains;
        }

        private static Chain Walk(EntityManager em, Dictionary<Entity, List<Entity>> byNode, HashSet<Entity> used, Entity from, Entity edge)
        {
            var chain = new Chain();
            chain.Nodes.Add(from);
            var node = from;
            while (edge != Entity.Null && used.Add(edge))
            {
                var e = em.GetComponentData<Edge>(edge);
                node = e.m_Start == node ? e.m_End : e.m_Start;
                chain.Edges.Add(edge);
                chain.Nodes.Add(node);

                var next = Entity.Null;
                var at = byNode[node];
                if (at.Count == 2)
                    next = at[0] == edge ? at[1] : at[0];
                edge = next;
            }
            return chain;
        }

        private static bool Editable(EntityManager em, Chain chain)
        {
            if (chain.Edges.Count == 0)
                return false;
            foreach (var node in chain.Nodes)
                if (!SlopeEdit.IsEditableNode(em, node))
                    return false;
            return true;
        }

        private static void Add(Dictionary<Entity, List<Entity>> byNode, Entity node, Entity edge)
        {
            if (!byNode.TryGetValue(node, out var list))
                byNode[node] = list = new List<Entity>();
            list.Add(edge);
        }
    }
}
