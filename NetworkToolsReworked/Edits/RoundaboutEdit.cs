using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkToolsReworked.Edits
{
    /// <summary>
    /// Replaces a junction node with a roundabout: each road at the junction is shortened to end on a
    /// circle around it (keeping the road itself via m_Original), and ring roads are built between those
    /// ends. The junction node is left without roads and removed by the game.
    /// </summary>
    public static class RoundaboutEdit
    {
        private const float kMaxArcStep = math.PI / 2f;
        private const float kMinStubLength = 4f;

        public enum Problem
        {
            None,
            NotAJunction,
            RadiusTooLarge,
        }

        public static Problem Check(EntityManager em, Entity node, float radius)
        {
            if (!SlopeEdit.IsEditableNode(em, node))
                return Problem.NotAJunction;
            var connected = em.GetBuffer<ConnectedEdge>(node, isReadOnly: true);
            var roads = 0;
            foreach (var c in connected)
            {
                if (em.HasComponent<Owner>(c.m_Edge) || !em.TryGetComponent(c.m_Edge, out Edge e) || (e.m_Start != node && e.m_End != node))
                    return Problem.NotAJunction;
                if (em.GetComponentData<Curve>(c.m_Edge).m_Length < radius + kMinStubLength)
                    return Problem.RadiusTooLarge;
                roads++;
            }
            return roads >= 3 ? Problem.None : Problem.NotAJunction;
        }

        /// <param name="clockwise">Ring direction seen from above; matters for one-way ring roads.</param>
        public static bool Emit(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, Entity node, float radius, bool clockwise, int randomSeed, List<float3> ringOut)
        {
            ringOut?.Clear();
            if (Check(em, node, radius) != Problem.None)
                return false;

            var centre = em.GetComponentData<Node>(node).m_Position;
            var hasElevation = em.HasComponent<Elevation>(node);
            var ring = new List<(float angle, float3 point)>();
            Entity ringPrefab = Entity.Null;

            foreach (var c in em.GetBuffer<ConnectedEdge>(node, isReadOnly: true))
            {
                var edge = c.m_Edge;
                var e = em.GetComponentData<Edge>(edge);
                var original = em.GetComponentData<Curve>(edge).m_Bezier;
                var fromCentre = e.m_Start == node ? original : MathUtils.Invert(original);

                var t = CrossingParameter(fromCentre, centre, radius);
                var point = MathUtils.Position(fromCentre, t);
                point.y = centre.y;

                // The outer part of the road, from the ring to its far node, in the edge's own direction.
                var outer = MathUtils.Cut(fromCentre, new float2(t, 1f));
                outer.a = point;
                var curve = e.m_Start == node ? outer : MathUtils.Invert(outer);

                var moved = new Dictionary<Entity, float3> { [node] = point };
                var edgeElevation = em.HasComponent<Elevation>(edge);
                var startPos = SlopeEdit.ChainEnd(em, ref terrain, e.m_Start, moved, curve, start: true, edgeElevation);
                var endPos = SlopeEdit.ChainEnd(em, ref terrain, e.m_End, moved, curve, start: false, edgeElevation);
                NetDefinitions.Emit(ecb, new CreationDefinition
                {
                    m_Original = edge,
                    m_Prefab = em.GetComponentData<PrefabRef>(edge).m_Prefab,
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

                if (ringPrefab == Entity.Null)
                    ringPrefab = em.GetComponentData<PrefabRef>(edge).m_Prefab;
                ring.Add((math.atan2(point.z - centre.z, point.x - centre.x), point));
            }

            // Ring roads between neighbouring road ends, split so no piece turns more than 90 degrees.
            ring.Sort((x, y) => x.angle.CompareTo(y.angle));
            for (var i = 0; i < ring.Count; i++)
            {
                var from = ring[i];
                var to = ring[(i + 1) % ring.Count];
                var sweep = to.angle - from.angle;
                if (sweep <= 0f)
                    sweep += 2f * math.PI;
                var pieces = (int)math.ceil(sweep / kMaxArcStep);
                for (var k = 0; k < pieces; k++)
                {
                    var a0 = from.angle + sweep * k / pieces;
                    var a1 = from.angle + sweep * (k + 1) / pieces;
                    var arc = Arc(centre, radius, a0, a1);
                    // Snap the ends that meet the roads to the exact cut points so the nodes merge.
                    if (k == 0) arc.a = from.point;
                    if (k == pieces - 1) arc.d = to.point;
                    if (clockwise)
                        arc = MathUtils.Invert(arc);
                    EmitRingPiece(em, ecb, ref terrain, ringPrefab, arc, hasElevation, centre, randomSeed);
                    ringOut?.Add(MathUtils.Position(arc, 0.5f));
                }
            }

            return true;
        }

        private static void EmitRingPiece(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, Entity prefab, Bezier4x3 arc, bool hasElevation, float3 centre, int randomSeed)
        {
            var startPos = RingEnd(ref terrain, arc, start: true, hasElevation);
            var endPos = RingEnd(ref terrain, arc, start: false, hasElevation);
            NetDefinitions.Emit(ecb, new CreationDefinition
            {
                m_Prefab = prefab,
                m_RandomSeed = randomSeed,
                m_Flags = CreationFlags.SubElevation,
            }, new NetCourse
            {
                m_Curve = arc,
                m_StartPosition = startPos,
                m_EndPosition = endPos,
                m_Elevation = new float2(startPos.m_Elevation.x, endPos.m_Elevation.x),
                m_Length = MathUtils.Length(arc),
                m_FixedIndex = -1,
            });
        }

        // A static method rather than a local function: C# does not let local functions capture ref parameters.
        private static CoursePos RingEnd(ref TerrainHeightData terrain, Bezier4x3 arc, bool start, bool hasElevation)
        {
            var p = start ? arc.a : arc.d;
            var tangent = start ? MathUtils.StartTangent(arc) : MathUtils.EndTangent(arc);
            return new CoursePos
            {
                m_Entity = Entity.Null,
                m_Position = p,
                m_Rotation = NetUtils.GetNodeRotation(tangent),
                m_Elevation = hasElevation ? new float2(p.y - TerrainUtils.SampleHeight(ref terrain, p)) : float2.zero,
                m_CourseDelta = start ? 0f : 1f,
                m_Flags = start ? CoursePosFlags.IsFirst : CoursePosFlags.IsLast,
                m_ParentMesh = -1,
            };
        }

        /// <summary>Counter-clockwise (seen from above) circular arc as a cubic bezier, flat at the centre's height.</summary>
        public static Bezier4x3 Arc(float3 centre, float radius, float from, float to)
        {
            var handle = 4f / 3f * math.tan((to - from) / 4f) * radius;
            var p0 = centre + radius * new float3(math.cos(from), 0f, math.sin(from));
            var p1 = centre + radius * new float3(math.cos(to), 0f, math.sin(to));
            var t0 = new float3(-math.sin(from), 0f, math.cos(from));
            var t1 = new float3(-math.sin(to), 0f, math.cos(to));
            return new Bezier4x3(p0, p0 + t0 * handle, p1 - t1 * handle, p1);
        }

        /// <summary>First curve parameter at which the curve leaves the circle around the centre.</summary>
        private static float CrossingParameter(Bezier4x3 curve, float3 centre, float radius)
        {
            float Distance(float t) => math.distance(MathUtils.Position(curve, t).xz, centre.xz);

            const int samples = 64;
            var lo = 0f;
            var hi = 1f;
            for (var k = 1; k <= samples; k++)
            {
                var t = (float)k / samples;
                if (Distance(t) >= radius)
                {
                    hi = t;
                    break;
                }
                lo = t;
            }
            for (var k = 0; k < 24; k++)
            {
                var mid = (lo + hi) * 0.5f;
                if (Distance(mid) < radius)
                    lo = mid;
                else
                    hi = mid;
            }
            return (lo + hi) * 0.5f;
        }
    }
}
