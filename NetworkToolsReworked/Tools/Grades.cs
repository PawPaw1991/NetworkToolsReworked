using Colossal.Entities;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace NetworkToolsReworked.Tools
{
    /// <summary>Grade limits of road types and the colours used to show grades against them.</summary>
    internal static class Grades
    {
        /// <summary>Steepest grade the road type allows (rise per metre); 12% if the prefab doesn't say.</summary>
        public static float Limit(EntityManager em, Entity edge)
        {
            if (em.TryGetComponent(edge, out PrefabRef prefab) &&
                em.TryGetComponent(prefab.m_Prefab, out NetGeometryData geometry) &&
                geometry.m_MaxSlopeSteepness > 0f)
                return geometry.m_MaxSlopeSteepness;
            return 0.12f;
        }

        /// <summary>Green when gentle, yellow at two thirds of the limit, red at the limit.</summary>
        public static Color ColorFor(float grade, float limit)
        {
            var ratio = math.saturate(math.abs(grade) / limit);
            var color = ratio < 0.67f
                ? Color.Lerp(new Color(0.25f, 0.9f, 0.4f), new Color(1f, 0.85f, 0.2f), ratio / 0.67f)
                : Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.25f, 0.2f), (ratio - 0.67f) / 0.33f);
            color.a = 0.45f;
            return color;
        }
    }
}
