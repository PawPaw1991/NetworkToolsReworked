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
    /// Joins two roads that cross without meeting: both are cut at the crossing and rebuilt as four
    /// roads sharing one new junction node there.
    /// </summary>
    public static class IntersectEdit
    {
        /// <summary>Roads further apart in height than this at the crossing pass over each other; they aren't joined.</summary>
        public const float kMaxHeightDifference = 2f;

        public static bool Emit(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, Entity a, Entity b, Crossings.Hit hit, int randomSeed)
        {
            if (math.abs(hit.PointA.y - hit.PointB.y) > kMaxHeightDifference)
                return false;

            // One shared position so the four new roads meet in a single node.
            var junction = new float3(hit.PointA.x, (hit.PointA.y + hit.PointB.y) * 0.5f, hit.PointA.z);
            Split(em, ecb, ref terrain, a, hit.TA, junction, randomSeed);
            Split(em, ecb, ref terrain, b, hit.TB, junction, randomSeed);
            return true;
        }

        private static void Split(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, Entity edge, float t, float3 junction, int randomSeed)
        {
            var e = em.GetComponentData<Edge>(edge);
            var curve = em.GetComponentData<Curve>(edge).m_Bezier;
            var prefab = em.GetComponentData<PrefabRef>(edge).m_Prefab;
            var hasElevation = em.HasComponent<Elevation>(edge);
            var hasUpgrades = em.TryGetComponent(edge, out Upgraded upgraded);

            NetDefinitions.DeleteEdge(em, ecb, edge, randomSeed);

            var first = MathUtils.Cut(curve, new float2(0f, t));
            var second = MathUtils.Cut(curve, new float2(t, 1f));
            first.d = junction;
            second.a = junction;
            var none = new System.Collections.Generic.Dictionary<Entity, float3>();

            foreach (var (piece, startNode, endNode) in new[] { (first, e.m_Start, Entity.Null), (second, Entity.Null, e.m_End) })
            {
                var startPos = startNode != Entity.Null
                    ? SlopeEdit.ChainEnd(em, ref terrain, startNode, none, piece, start: true, hasElevation)
                    : JunctionEnd(ref terrain, piece, junction, start: true, hasElevation);
                var endPos = endNode != Entity.Null
                    ? SlopeEdit.ChainEnd(em, ref terrain, endNode, none, piece, start: false, hasElevation)
                    : JunctionEnd(ref terrain, piece, junction, start: false, hasElevation);

                var definition = NetDefinitions.Emit(ecb, new CreationDefinition
                {
                    m_Prefab = prefab,
                    m_RandomSeed = randomSeed,
                    m_Flags = CreationFlags.SubElevation,
                }, new NetCourse
                {
                    m_Curve = piece,
                    m_StartPosition = startPos,
                    m_EndPosition = endPos,
                    m_Elevation = new float2(startPos.m_Elevation.x, endPos.m_Elevation.x),
                    m_Length = MathUtils.Length(piece),
                    m_FixedIndex = -1,
                });
                if (hasUpgrades)
                    ecb.AddComponent(definition, upgraded);
            }
        }

        private static CoursePos JunctionEnd(ref TerrainHeightData terrain, Bezier4x3 curve, float3 junction, bool start, bool hasElevation)
        {
            var tangent = start ? MathUtils.StartTangent(curve) : MathUtils.EndTangent(curve);
            return new CoursePos
            {
                m_Entity = Entity.Null,
                m_Position = junction,
                m_Rotation = NetUtils.GetNodeRotation(tangent),
                m_Elevation = hasElevation ? new float2(junction.y - TerrainUtils.SampleHeight(ref terrain, junction)) : float2.zero,
                m_CourseDelta = start ? 0f : 1f,
                m_Flags = start ? CoursePosFlags.IsFirst : CoursePosFlags.IsLast,
                m_ParentMesh = -1,
            };
        }
    }
}
