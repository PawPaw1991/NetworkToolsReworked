using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Mathematics;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkToolsReworked.Edits
{
    /// <summary>
    /// Emits a new road alongside a chain of edges, offset sideways and vertically. Each edge becomes
    /// one new course of the same type and upgrades; new nodes are shared between consecutive courses
    /// so the copy is one continuous road.
    /// </summary>
    public static class ParallelEdit
    {
        /// <param name="offset">Sideways distance in metres; positive is to the right of start → end.</param>
        /// <param name="height">Vertical offset in metres.</param>
        /// <param name="reverse">Build the copy running end → start (for one-way roads).</param>
        public static void Emit(EntityManager em, EntityCommandBuffer ecb, List<Entity> nodes, List<Entity> edges, float offset, float height, bool reverse, int randomSeed)
        {
            if (math.abs(offset) < 0.5f && math.abs(height) < 0.5f)
                return;

            var curves = new Bezier4x3[edges.Count];
            for (var i = 0; i < edges.Count; i++)
            {
                var bezier = em.GetComponentData<Curve>(edges[i]).m_Bezier;
                if (em.GetComponentData<Edge>(edges[i]).m_Start != nodes[i])
                    bezier = MathUtils.Invert(bezier);
                curves[i] = bezier;
            }

            // Offset each node once, along the average direction of the edges meeting there, so
            // consecutive courses share exactly the same new node position.
            var positions = new float3[nodes.Count];
            var elevations = new float[nodes.Count];
            for (var i = 0; i < nodes.Count; i++)
            {
                var direction = float3.zero;
                if (i > 0) direction += Flat(MathUtils.EndTangent(curves[i - 1]));
                if (i < edges.Count) direction += Flat(MathUtils.StartTangent(curves[i]));
                positions[i] = em.GetComponentData<Node>(nodes[i]).m_Position + Right(direction) * offset + new float3(0f, height, 0f);
                elevations[i] = (em.TryGetComponent(nodes[i], out Elevation elevation) ? elevation.m_Elevation.x : 0f) + height;
            }

            for (var i = 0; i < edges.Count; i++)
            {
                var source = curves[i];
                var a = positions[i];
                var d = positions[i + 1];

                // Keep the source curve's handle directions, scaled to the new end-to-end distance.
                var scale = math.distance(a, d) / math.max(math.distance(source.a, source.d), 0.01f);
                var curve = new Bezier4x3(a, a + (source.b - source.a) * scale, d + (source.c - source.d) * scale, d);
                var e0 = elevations[i];
                var e1 = elevations[i + 1];
                if (reverse)
                {
                    curve = MathUtils.Invert(curve);
                    (e0, e1) = (e1, e0);
                }

                var definition = NetDefinitions.Emit(ecb, new CreationDefinition
                {
                    m_Prefab = em.GetComponentData<PrefabRef>(edges[i]).m_Prefab,
                    m_RandomSeed = randomSeed,
                    m_Flags = CreationFlags.SubElevation,
                }, new NetCourse
                {
                    m_Curve = curve,
                    m_StartPosition = NewEnd(curve, start: true, e0),
                    m_EndPosition = NewEnd(curve, start: false, e1),
                    m_Elevation = new float2(e0, e1),
                    m_Length = MathUtils.Length(curve),
                    m_FixedIndex = -1,
                });

                if (em.TryGetComponent(edges[i], out Upgraded upgraded))
                    ecb.AddComponent(definition, upgraded);
            }
        }

        private static CoursePos NewEnd(Bezier4x3 curve, bool start, float elevation)
        {
            return new CoursePos
            {
                m_Entity = Entity.Null,
                m_Position = start ? curve.a : curve.d,
                m_Rotation = NetUtils.GetNodeRotation(start ? MathUtils.StartTangent(curve) : MathUtils.EndTangent(curve)),
                m_Elevation = new float2(elevation),
                m_CourseDelta = start ? 0f : 1f,
                m_Flags = start ? CoursePosFlags.IsFirst : CoursePosFlags.IsLast,
                m_ParentMesh = -1,
            };
        }

        private static float3 Flat(float3 v)
        {
            v.y = 0f;
            return math.lengthsq(v) > 0.000001f ? math.normalize(v) : float3.zero;
        }

        /// <summary>Unit vector to the right of a horizontal travel direction.</summary>
        private static float3 Right(float3 direction)
        {
            var right = new float3(direction.z, 0f, -direction.x);
            return math.lengthsq(right) > 0.000001f ? math.normalize(right) : float3.zero;
        }
    }
}
