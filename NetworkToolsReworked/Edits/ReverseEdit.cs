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
    /// Reverses the direction of every road in a chain (for one-way roads and tracks): each edge is
    /// replaced by the same road running the other way between the same nodes. Upgrades that belong to
    /// one side (left/right) are swapped so they stay on the same physical side.
    /// </summary>
    public static class ReverseEdit
    {
        public static bool Emit(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, List<Entity> edges, int randomSeed)
        {
            if (edges.Count == 0)
                return false;

            var unchanged = new Dictionary<Entity, float3>();
            foreach (var edge in edges)
            {
                var e = em.GetComponentData<Edge>(edge);
                var curve = MathUtils.Invert(em.GetComponentData<Curve>(edge).m_Bezier);
                var hasElevation = em.HasComponent<Elevation>(edge);

                NetDefinitions.DeleteEdge(em, ecb, edge, randomSeed);

                var startPos = SlopeEdit.ChainEnd(em, ref terrain, e.m_End, unchanged, curve, start: true, hasElevation);
                var endPos = SlopeEdit.ChainEnd(em, ref terrain, e.m_Start, unchanged, curve, start: false, hasElevation);
                var definition = NetDefinitions.Emit(ecb, new CreationDefinition
                {
                    m_Prefab = em.GetComponentData<PrefabRef>(edge).m_Prefab,
                    m_RandomSeed = randomSeed,
                    m_Flags = CreationFlags.SubElevation,
                }, new NetCourse
                {
                    m_Curve = curve,
                    m_StartPosition = startPos,
                    m_EndPosition = endPos,
                    m_Elevation = new float2(startPos.m_Elevation.x, endPos.m_Elevation.x),
                    m_Length = MathUtils.Length(curve),
                    m_FixedIndex = -1,
                });

                if (em.TryGetComponent(edge, out Upgraded upgraded))
                {
                    var flags = upgraded.m_Flags;
                    (flags.m_Left, flags.m_Right) = (flags.m_Right, flags.m_Left);
                    ecb.AddComponent(definition, new Upgraded { m_Flags = flags });
                }
            }
            return true;
        }
    }
}
