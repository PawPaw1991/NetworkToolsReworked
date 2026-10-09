using Game.Common;
using Game.Net;
using Unity.Entities;

namespace NetworkToolsReworked.Tools
{
    internal static class ToolPicks
    {
        /// <summary>
        /// True if a node picked on an earlier frame is still a live road node. Other mods (Move It, for
        /// one) and our own rebuilds can delete or replace it between frames.
        /// </summary>
        public static bool IsAlive(EntityManager em, Entity node)
        {
            return em.Exists(node) && em.HasComponent<Node>(node) && !em.HasComponent<Deleted>(node);
        }
    }
}
