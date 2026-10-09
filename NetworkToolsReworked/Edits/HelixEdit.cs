using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Game.Simulation;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkToolsReworked.Edits
{
    public struct HelixParams
    {
        public float Radius;

        /// <summary>Number of full turns; fractions allowed.</summary>
        public float Turns;

        /// <summary>Height gained per full turn, metres; negative goes down.</summary>
        public float Climb;

        public bool Clockwise;
    }

    public struct HelixResult
    {
        public List<Bezier4x3> Curves;
        public float Grade;
        public float Length;
        public float TopHeight;
        public float LowestAboveGround;
    }

    /// <summary>
    /// A spiral road (helix): constant radius, climbing steadily, built as one node every quarter turn so
    /// the circle holds its shape. It either starts at a free point on the ground or carries straight on
    /// from the open end of a road. Every node gets its height above the ground as its elevation, so the
    /// game builds the raised turns as bridges.
    /// </summary>
    public static class HelixEdit
    {
        public const int kMaxQuarters = 48;

        /// <summary>Centre and start angle when the helix carries on from a road end in direction <paramref name="direction"/>.</summary>
        public static void Attach(float3 start, float3 direction, float radius, bool clockwise, out float3 centre, out float startAngle)
        {
            var s = clockwise ? -1f : 1f;
            var d = math.normalizesafe(new float3(direction.x, 0f, direction.z));
            var r = radius * new float3(s * d.z, 0f, -s * d.x);
            centre = start - r;
            startAngle = math.atan2(r.z, r.x);
        }

        /// <param name="startNode">Road end the helix carries on from, or Entity.Null for a free start.</param>
        public static void Emit(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, Entity prefab, float3 centre, float startAngle, float startHeight, Entity startNode, in HelixParams helix, int randomSeed, out HelixResult result)
        {
            result = new HelixResult { Curves = new List<Bezier4x3>(), LowestAboveGround = float.MaxValue };
            var s = helix.Clockwise ? -1f : 1f;
            var radius = math.max(helix.Radius, 4f);
            var total = math.PI * 2f * math.max(helix.Turns, 0.25f);
            var quarters = math.clamp((int)math.ceil(total / (math.PI * 0.5f) - 0.001f), 1, kMaxQuarters);
            var step = total / quarters;
            var rise = helix.Climb * step / (math.PI * 2f);
            var handle = 4f / 3f * math.tan(step / 4f) * radius;

            result.Grade = helix.Climb / (math.PI * 2f * radius);
            var previous = End(em, ref terrain, Point(centre, radius, startAngle, startHeight), startNode, ref result);
            for (var k = 0; k < quarters; k++)
            {
                var a0 = startAngle + s * step * k;
                var a1 = a0 + s * step;
                var p0 = Point(centre, radius, a0, startHeight + rise * k);
                var p1 = Point(centre, radius, a1, startHeight + rise * (k + 1));
                var curve = new Bezier4x3(
                    p0,
                    p0 + Tangent(a0, s) * handle + new float3(0f, rise / 3f, 0f),
                    p1 - Tangent(a1, s) * handle - new float3(0f, rise / 3f, 0f),
                    p1);

                var startPos = previous;
                startPos.m_Rotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(curve));
                startPos.m_CourseDelta = 0f;
                // Only the helix's own two ends are loose ends; the joints between quarters are shared.
                startPos.m_Flags = k == 0 ? CoursePosFlags.IsFirst : 0;
                var endPos = End(em, ref terrain, p1, Entity.Null, ref result);
                endPos.m_Rotation = NetUtils.GetNodeRotation(MathUtils.EndTangent(curve));
                endPos.m_CourseDelta = 1f;
                endPos.m_Flags = k == quarters - 1 ? CoursePosFlags.IsLast : 0;

                NetDefinitions.Emit(ecb, new CreationDefinition
                {
                    m_Prefab = prefab,
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

                result.Curves.Add(curve);
                result.Length += MathUtils.Length(curve);
                previous = endPos;
            }
            result.TopHeight = rise * quarters;
        }

        private static float3 Point(float3 centre, float radius, float angle, float y) =>
            new float3(centre.x + radius * math.cos(angle), y, centre.z + radius * math.sin(angle));

        /// <summary>Unit direction of travel at an angle; <paramref name="s"/> is +1 anticlockwise, -1 clockwise.</summary>
        private static float3 Tangent(float angle, float s) => s * new float3(-math.sin(angle), 0f, math.cos(angle));

        private static CoursePos End(EntityManager em, ref TerrainHeightData terrain, float3 p, Entity node, ref HelixResult result)
        {
            if (node != Entity.Null)
            {
                return new CoursePos
                {
                    m_Entity = node,
                    m_Position = em.GetComponentData<Node>(node).m_Position,
                    m_Elevation = em.HasComponent<Elevation>(node) ? em.GetComponentData<Elevation>(node).m_Elevation : float2.zero,
                    m_ParentMesh = -1,
                };
            }

            var aboveGround = p.y - TerrainUtils.SampleHeight(ref terrain, p);
            result.LowestAboveGround = math.min(result.LowestAboveGround, aboveGround);
            return new CoursePos
            {
                m_Entity = Entity.Null,
                m_Position = p,
                m_Elevation = new float2(math.abs(aboveGround) < 0.5f ? 0f : aboveGround),
                m_ParentMesh = -1,
            };
        }
    }
}
