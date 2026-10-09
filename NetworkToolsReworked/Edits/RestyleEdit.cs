using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Mathematics;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkToolsReworked.Edits
{
    /// <summary>
    /// Changes what a chain of roads is (its road type) or what it carries (its upgrades) without
    /// touching its shape: each edge gets an m_Original definition with the same curve and end nodes,
    /// so the game keeps the edge and only swaps the prefab or the upgrades.
    /// </summary>
    public static class RestyleEdit
    {
        public enum NetKind
        {
            Other,
            Road,
            Track,
            Pathway,
        }

        /// <summary>Broad kind of network, so a road isn't swapped for a track or a path.</summary>
        public static NetKind KindOf(EntityManager em, Entity prefab)
        {
            if (em.HasComponent<RoadData>(prefab)) return NetKind.Road;
            if (em.HasComponent<TrackData>(prefab)) return NetKind.Track;
            if (em.HasComponent<PathwayData>(prefab)) return NetKind.Pathway;
            return NetKind.Other;
        }

        /// <param name="prefab">New road type, or Entity.Null to keep each edge's type.</param>
        /// <param name="upgrades">New upgrades, or null to keep each edge's upgrades.</param>
        /// <returns>Number of edges that change.</returns>
        public static int Emit(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, List<Entity> edges, Entity prefab, Upgraded? upgrades, int randomSeed)
        {
            var none = new Dictionary<Entity, float3>();
            var changed = 0;
            foreach (var edge in edges)
            {
                var oldPrefab = em.GetComponentData<PrefabRef>(edge).m_Prefab;
                var hasUpgrades = em.TryGetComponent(edge, out Upgraded oldUpgrades);
                var newPrefab = prefab != Entity.Null ? prefab : oldPrefab;
                var newUpgrades = upgrades ?? oldUpgrades;
                var sameUpgrades = upgrades == null || (hasUpgrades ? oldUpgrades.m_Flags == newUpgrades.m_Flags : newUpgrades.m_Flags == default);
                if (newPrefab == oldPrefab && sameUpgrades)
                    continue;

                var e = em.GetComponentData<Edge>(edge);
                var curve = em.GetComponentData<Curve>(edge).m_Bezier;
                var hasElevation = em.HasComponent<Elevation>(edge);
                var startPos = SlopeEdit.ChainEnd(em, ref terrain, e.m_Start, none, curve, start: true, hasElevation);
                var endPos = SlopeEdit.ChainEnd(em, ref terrain, e.m_End, none, curve, start: false, hasElevation);

                var definition = NetDefinitions.Emit(ecb, new CreationDefinition
                {
                    m_Original = edge,
                    m_Prefab = newPrefab,
                    m_RandomSeed = randomSeed,
                }, new NetCourse
                {
                    m_Curve = curve,
                    m_StartPosition = startPos,
                    m_EndPosition = endPos,
                    m_Elevation = new float2(startPos.m_Elevation.x, endPos.m_Elevation.x),
                    m_Length = MathUtils.Length(curve),
                    m_FixedIndex = -1,
                });

                // Always state the upgrades, so a new type keeps them and a cleared set really clears.
                if (upgrades != null || hasUpgrades)
                    ecb.AddComponent(definition, newUpgrades);
                changed++;
            }
            return changed;
        }
    }
}
