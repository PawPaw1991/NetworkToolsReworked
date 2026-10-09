using System.Collections.Generic;
using Colossal.Entities;
using Game.Common;
using Game.Net;
using Game.Simulation;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkToolsReworked.Edits
{
    /// <summary>
    /// Moves a road node. The game can't move an existing node through definitions, so every road at
    /// the node is re-pointed (keeping the road itself via m_Original) to a new node at the new
    /// position, and the old node is left orphaned for the game to remove. Same approach as Slope.
    /// </summary>
    public static class MoveEdit
    {
        /// <summary>True if the node is a plain road node with at least one road and no road owned by a building.</summary>
        public static bool CanMove(EntityManager em, Entity node)
        {
            if (!SlopeEdit.IsEditableNode(em, node))
                return false;
            var connected = em.GetBuffer<ConnectedEdge>(node, isReadOnly: true);
            if (connected.Length == 0)
                return false;
            foreach (var c in connected)
                if (em.HasComponent<Owner>(c.m_Edge))
                    return false;
            return true;
        }

        public static bool Emit(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, Entity node, float3 position, int randomSeed)
        {
            if (!CanMove(em, node) || math.distancesq(position, em.GetComponentData<Node>(node).m_Position) < 0.0001f)
                return false;

            var newPositions = new Dictionary<Entity, float3> { [node] = position };
            foreach (var connected in em.GetBuffer<ConnectedEdge>(node, isReadOnly: true))
            {
                var edge = connected.m_Edge;
                if (!em.TryGetComponent(edge, out Edge e) || (e.m_Start != node && e.m_End != node))
                    continue;
                SlopeEdit.EmitSideEdge(em, ecb, ref terrain, edge, e, newPositions, randomSeed);
            }
            return true;
        }
    }
}
