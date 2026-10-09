using System.Collections.Generic;
using System.Globalization;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkToolsReworked.Tools
{
    /// <summary>Formatting of lengths and grades for the tool panel summary.</summary>
    internal static class PathInfo
    {
        public static float Length(EntityManager em, List<Entity> edges)
        {
            var length = 0f;
            foreach (var edge in edges)
                length += em.GetComponentData<Curve>(edge).m_Length;
            return length;
        }

        public static string Describe(EntityManager em, List<Entity> nodes, List<Entity> edges)
        {
            var count = edges.Count == 1 ? "1 segment" : $"{edges.Count} segments";
            return $"{count}, {Distance(Length(em, edges))}";
        }

        /// <summary>Height change from the first to the last node, with the average grade over <paramref name="length"/>.</summary>
        public static string Grade(EntityManager em, Entity from, Entity to, float length)
        {
            var rise = em.GetComponentData<Node>(to).m_Position.y - em.GetComponentData<Node>(from).m_Position.y;
            var grade = length > 0.01f ? rise / length * 100f : 0f;
            return $"height {Signed(rise)} m, grade {Signed(grade)}%";
        }

        public static string Distance(float metres)
        {
            if (Mod.Settings.Unit == Setting.DistanceUnit.Cells)
                return (metres / 8f).ToString("0.#", CultureInfo.InvariantCulture) + " cells";
            return math.round(metres).ToString(CultureInfo.InvariantCulture) + " m";
        }

        public static string Signed(float value) => (value >= 0f ? "+" : "") + value.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
