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
        public enum GameProblem
        {
            None,
            NotANode,
            NotSupported,
        }

        /// <summary>
        /// Whether the game's own roundabout can go on this node: every road there must be a type the game
        /// allows roundabouts on (roads and tram track).
        /// </summary>
        public static GameProblem CheckGame(EntityManager em, Entity node)
        {
            if (!SlopeEdit.IsEditableNode(em, node))
                return GameProblem.NotANode;
            var roads = 0;
            foreach (var c in em.GetBuffer<ConnectedEdge>(node, isReadOnly: true))
            {
                if (em.HasComponent<Owner>(c.m_Edge) || !em.TryGetComponent(c.m_Edge, out Edge e) || (e.m_Start != node && e.m_End != node))
                    continue;
                var prefab = em.GetComponentData<PrefabRef>(c.m_Edge).m_Prefab;
                if (!em.TryGetComponent(prefab, out NetGeometryData geometry) || (geometry.m_Flags & GeometryFlags.SupportRoundabout) == 0)
                    return GameProblem.NotSupported;
                roads++;
            }
            return roads > 0 ? GameProblem.None : GameProblem.NotANode;
        }

        /// <summary>True if the prefab is one of the game's roundabout central islands (or a modded one).</summary>
        public static bool IsIslandPrefab(EntityManager em, Entity prefab)
        {
            return em.TryGetComponent(prefab, out NetObjectData data) && (data.m_CompositionFlags.m_General & CompositionFlags.General.Roundabout) != 0;
        }

        /// <summary>The roundabout island already on this junction, if any.</summary>
        public static Entity FindIsland(EntityManager em, Entity node)
        {
            if (!em.TryGetBuffer(node, true, out DynamicBuffer<Game.Objects.SubObject> subObjects))
                return Entity.Null;
            foreach (var sub in subObjects)
            {
                if (em.HasComponent<Deleted>(sub.m_SubObject) || !em.TryGetComponent(sub.m_SubObject, out PrefabRef prefabRef))
                    continue;
                if (IsIslandPrefab(em, prefabRef.m_Prefab))
                    return sub.m_SubObject;
            }
            return Entity.Null;
        }

        /// <summary>
        /// Left over from an earlier version of this tool, which set the roundabout flag on the junction
        /// itself. The game doesn't use that, so it's cleared whenever the junction is edited.
        /// </summary>
        private static bool HasStrayFlag(EntityManager em, Entity node, out Upgraded upgraded)
        {
            return em.TryGetComponent(node, out upgraded) && (upgraded.m_Flags.m_General & CompositionFlags.General.Roundabout) != 0;
        }

        /// <summary>
        /// The game's own roundabout: a central island object placed on the junction, as the game's
        /// roundabout tool does. The island turns the junction into a roundabout sized by the island and the
        /// roads. <paramref name="island"/> Entity.Null removes the one that is there.
        /// </summary>
        public static void EmitIsland(EntityManager em, EntityCommandBuffer ecb, Entity node, Entity island, int randomSeed)
        {
            var n = em.GetComponentData<Node>(node);
            var existing = FindIsland(em, node);
            var elevated = em.TryGetComponent(node, out Elevation nodeElevation);

            if (existing != Entity.Null)
            {
                var transform = em.GetComponentData<Game.Objects.Transform>(existing);
                // Delete it, or swap it for the chosen island in place.
                var definition = ecb.CreateEntity();
                ecb.AddComponent(definition, new CreationDefinition
                {
                    m_Original = existing,
                    m_Prefab = island != Entity.Null ? island : em.GetComponentData<PrefabRef>(existing).m_Prefab,
                    m_RandomSeed = randomSeed,
                    m_Flags = island != Entity.Null ? CreationFlags.Upgrade | CreationFlags.Parent : CreationFlags.Delete,
                });
                ecb.AddComponent(definition, Placement(transform.m_Position, transform.m_Rotation, elevated, nodeElevation));
                ecb.AddComponent(definition, default(Updated));
            }
            else if (island != Entity.Null)
            {
                var definition = ecb.CreateEntity();
                ecb.AddComponent(definition, new CreationDefinition
                {
                    m_Prefab = island,
                    m_RandomSeed = randomSeed,
                    m_Flags = CreationFlags.Attach,
                });
                ecb.AddComponent(definition, Placement(n.m_Position, n.m_Rotation, elevated, nodeElevation));
                ecb.AddComponent(definition, default(Updated));
            }

            // Refresh the junction so it picks up (or drops) the roundabout.
            var pos = new CoursePos
            {
                m_Entity = node,
                m_Position = n.m_Position,
                m_Rotation = n.m_Rotation,
                m_Elevation = elevated ? nodeElevation.m_Elevation : float2.zero,
                m_CourseDelta = 0f,
                m_ParentMesh = -1,
            };
            var endPos = pos;
            endPos.m_CourseDelta = 1f;
            var refresh = NetDefinitions.Emit(ecb, new CreationDefinition
            {
                m_Original = node,
                m_Prefab = em.GetComponentData<PrefabRef>(node).m_Prefab,
                m_RandomSeed = randomSeed,
            }, new NetCourse
            {
                m_Curve = new Bezier4x3(n.m_Position, n.m_Position, n.m_Position, n.m_Position),
                m_StartPosition = pos,
                m_EndPosition = endPos,
                m_Length = 0f,
                m_FixedIndex = -1,
            });
            if (HasStrayFlag(em, node, out var upgraded))
            {
                upgraded.m_Flags.m_General &= ~CompositionFlags.General.Roundabout;
                ecb.AddComponent(refresh, upgraded);
            }
        }

        private static ObjectDefinition Placement(float3 position, quaternion rotation, bool elevated, Elevation nodeElevation)
        {
            return new ObjectDefinition
            {
                m_Position = position,
                m_Rotation = rotation,
                m_LocalPosition = position,
                m_LocalRotation = rotation,
                m_Scale = new float3(1f),
                m_Probability = 100,
                m_PrefabSubIndex = -1,
                m_ParentMesh = elevated ? 0 : -1,
                m_Elevation = elevated ? (nodeElevation.m_Elevation.x + nodeElevation.m_Elevation.y) * 0.5f : 0f,
            };
        }

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

            // The junction node itself goes; every road that met there now ends on the ring.
            NetDefinitions.DeleteNode(em, ecb, node, randomSeed);

            ringPrefab = BuildType.For(em, ringPrefab);

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
                    EmitRingPiece(ecb, ref terrain, ringPrefab, arc, randomSeed);
                    ringOut?.Add(MathUtils.Position(arc, 0.5f));
                }
            }

            return true;
        }

        private static void EmitRingPiece(EntityCommandBuffer ecb, ref TerrainHeightData terrain, Entity prefab, Bezier4x3 arc, int randomSeed)
        {
            var startPos = RingEnd(ref terrain, arc, start: true);
            var endPos = RingEnd(ref terrain, arc, start: false);
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
        private static CoursePos RingEnd(ref TerrainHeightData terrain, Bezier4x3 arc, bool start)
        {
            var p = start ? arc.a : arc.d;
            var tangent = start ? MathUtils.StartTangent(arc) : MathUtils.EndTangent(arc);
            return new CoursePos
            {
                m_Entity = Entity.Null,
                m_Position = p,
                m_Rotation = NetUtils.GetNodeRotation(tangent),
                // Same rule as SlopeEdit.ChainEnd for new nodes, so ring pieces and roads join up.
                m_Elevation = new float2(p.y - TerrainUtils.SampleHeight(ref terrain, p)),
                m_CourseDelta = start ? 0f : 1f,
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
