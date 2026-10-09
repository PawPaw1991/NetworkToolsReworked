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
    /// Re-grades a chain of edges between two anchor nodes. The game can't move an existing road
    /// node through definitions, so the chain is rebuilt: old chain edges are deleted, new courses
    /// are emitted through new interior nodes at the new heights, and side roads at those nodes are
    /// re-pointed to the new nodes (keeping their identity via m_Original). The old interior nodes
    /// are left orphaned and removed by the game. The two anchors keep their height.
    /// </summary>
    public static class SlopeEdit
    {
        private const int kMaxSearchNodes = 512;

        /// <summary>Shortest chain of unowned edges from start to end, as ordered node and edge lists.</summary>
        public static bool FindPath(EntityManager em, Entity start, Entity end, List<Entity> nodes, List<Entity> edges)
        {
            nodes.Clear();
            edges.Clear();
            if (start == end || !IsEditableNode(em, start) || !IsEditableNode(em, end))
                return false;

            var distance = new Dictionary<Entity, float> { [start] = 0f };
            var previous = new Dictionary<Entity, (Entity node, Entity edge)>();
            var open = new List<Entity> { start };
            var closed = new HashSet<Entity>();

            while (open.Count > 0 && closed.Count < kMaxSearchNodes)
            {
                var best = 0;
                for (var i = 1; i < open.Count; i++)
                    if (distance[open[i]] < distance[open[best]])
                        best = i;
                var node = open[best];
                open.RemoveAt(best);
                if (!closed.Add(node))
                    continue;
                if (node == end)
                    break;

                foreach (var connected in em.GetBuffer<ConnectedEdge>(node, isReadOnly: true))
                {
                    var edge = connected.m_Edge;
                    if (em.HasComponent<Owner>(edge) || !em.TryGetComponent(edge, out Edge e))
                        continue;
                    var other = e.m_Start == node ? e.m_End : e.m_Start;
                    if ((e.m_Start != node && e.m_End != node) || closed.Contains(other) || !IsEditableNode(em, other))
                        continue;

                    var d = distance[node] + em.GetComponentData<Curve>(edge).m_Length;
                    if (!distance.TryGetValue(other, out var known) || d < known)
                    {
                        distance[other] = d;
                        previous[other] = (node, edge);
                        open.Add(other);
                    }
                }
            }

            if (!previous.ContainsKey(end))
                return false;

            for (var node = end; node != start; node = previous[node].node)
            {
                nodes.Add(node);
                edges.Add(previous[node].edge);
            }
            nodes.Add(start);
            nodes.Reverse();
            edges.Reverse();
            return true;
        }

        public static void Emit(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, List<Entity> nodes, List<Entity> edges, SlopeProfile profile, int randomSeed)
        {
            // Oriented curves along the chain and horizontal distance of every node from the start.
            var curves = new Bezier4x3[edges.Count];
            var lengths = new float[edges.Count];
            var along = new float[nodes.Count];
            for (var i = 0; i < edges.Count; i++)
            {
                var bezier = em.GetComponentData<Curve>(edges[i]).m_Bezier;
                if (em.GetComponentData<Edge>(edges[i]).m_Start != nodes[i])
                    bezier = MathUtils.Invert(bezier);
                curves[i] = bezier;
                lengths[i] = math.max(MathUtils.Length(bezier.xz), 0.01f);
                along[i + 1] = along[i] + lengths[i];
            }

            var total = along[nodes.Count - 1];
            var startHeight = em.GetComponentData<Node>(nodes[0]).m_Position.y;
            var endHeight = em.GetComponentData<Node>(nodes[nodes.Count - 1]).m_Position.y;

            float Height(float s, out float grade)
            {
                SlopeProfiles.Evaluate(profile, s / total, out var f, out var df);
                grade = (endHeight - startHeight) * df / total;
                return startHeight + (endHeight - startHeight) * f;
            }

            // New positions for interior nodes, computed once so courses that share a node match exactly.
            var newPositions = new Dictionary<Entity, float3>();
            for (var i = 1; i < nodes.Count - 1; i++)
            {
                var p = em.GetComponentData<Node>(nodes[i]).m_Position;
                p.y = Height(along[i], out _);
                newPositions[nodes[i]] = p;
            }

            for (var i = 0; i < edges.Count; i++)
            {
                var b = curves[i];
                var h0 = Height(along[i], out var g0);
                var h1 = Height(along[i + 1], out var g1);
                b.a.y = h0;
                b.d.y = h1;
                b.b.y = h0 + g0 * lengths[i] / 3f;
                b.c.y = h1 - g1 * lengths[i] / 3f;

                NetDefinitions.DeleteEdge(em, ecb, edges[i], randomSeed);

                var hasElevation = em.HasComponent<Elevation>(edges[i]);
                var startPos = ChainEnd(em, ref terrain, nodes[i], newPositions, b, start: true, hasElevation);
                var endPos = ChainEnd(em, ref terrain, nodes[i + 1], newPositions, b, start: false, hasElevation);

                var course = new NetCourse
                {
                    m_Curve = b,
                    m_StartPosition = startPos,
                    m_EndPosition = endPos,
                    m_Elevation = new float2(startPos.m_Elevation.x, endPos.m_Elevation.x),
                    m_Length = MathUtils.Length(b),
                    m_FixedIndex = -1,
                };

                var definition = NetDefinitions.Emit(ecb, new CreationDefinition
                {
                    m_Prefab = em.GetComponentData<PrefabRef>(edges[i]).m_Prefab,
                    m_RandomSeed = randomSeed,
                    m_Flags = CreationFlags.SubElevation,
                }, course);

                if (em.TryGetComponent(edges[i], out Upgraded upgraded))
                    ecb.AddComponent(definition, upgraded);
            }

            // Re-point side roads at each moved node, keeping the side road itself (m_Original).
            var chainEdges = new HashSet<Entity>(edges);
            var handled = new HashSet<Entity>();
            for (var i = 1; i < nodes.Count - 1; i++)
            {
                foreach (var connected in em.GetBuffer<ConnectedEdge>(nodes[i], isReadOnly: true))
                {
                    var side = connected.m_Edge;
                    if (chainEdges.Contains(side) || !handled.Add(side) || !em.TryGetComponent(side, out Edge e))
                        continue;
                    if (e.m_Start != nodes[i] && e.m_End != nodes[i])
                        continue;
                    EmitSideEdge(em, ecb, ref terrain, side, e, newPositions, randomSeed);
                }
            }
        }

        private static void EmitSideEdge(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, Entity side, Edge e, Dictionary<Entity, float3> newPositions, int randomSeed)
        {
            var b = em.GetComponentData<Curve>(side).m_Bezier;
            var hasElevation = em.HasComponent<Elevation>(side);

            // Shift each moved end, and its neighbouring control point, by the node's height change.
            if (newPositions.TryGetValue(e.m_Start, out var newStart))
            {
                var dy = newStart.y - b.a.y;
                b.a.y += dy;
                b.b.y += dy;
            }
            if (newPositions.TryGetValue(e.m_End, out var newEnd))
            {
                var dy = newEnd.y - b.d.y;
                b.d.y += dy;
                b.c.y += dy;
            }

            var startPos = ChainEnd(em, ref terrain, e.m_Start, newPositions, b, start: true, hasElevation);
            var endPos = ChainEnd(em, ref terrain, e.m_End, newPositions, b, start: false, hasElevation);

            NetDefinitions.Emit(ecb, new CreationDefinition
            {
                m_Original = side,
                m_Prefab = em.GetComponentData<PrefabRef>(side).m_Prefab,
                m_RandomSeed = randomSeed,
            }, new NetCourse
            {
                m_Curve = b,
                m_StartPosition = startPos,
                m_EndPosition = endPos,
                m_Length = MathUtils.Length(b),
                m_FixedIndex = -1,
            });
        }

        /// <summary>
        /// One end of a course: an existing node if it isn't moving, otherwise a new node (m_Entity null)
        /// at its new position with its elevation above terrain.
        /// </summary>
        private static CoursePos ChainEnd(EntityManager em, ref TerrainHeightData terrain, Entity node, Dictionary<Entity, float3> newPositions, Bezier4x3 curve, bool start, bool hasElevation)
        {
            var tangent = start ? MathUtils.StartTangent(curve) : MathUtils.EndTangent(curve);
            var pos = new CoursePos
            {
                m_Rotation = NetUtils.GetNodeRotation(tangent),
                m_CourseDelta = start ? 0f : 1f,
                m_Flags = start ? CoursePosFlags.IsFirst : CoursePosFlags.IsLast,
                m_ParentMesh = -1,
            };

            if (newPositions.TryGetValue(node, out var moved))
            {
                pos.m_Entity = Entity.Null;
                pos.m_Position = moved;
                pos.m_Elevation = hasElevation ? new float2(moved.y - TerrainUtils.SampleHeight(ref terrain, moved)) : float2.zero;
            }
            else
            {
                pos.m_Entity = node;
                pos.m_Position = em.GetComponentData<Node>(node).m_Position;
                pos.m_Elevation = em.TryGetComponent(node, out Elevation elevation) ? elevation.m_Elevation : float2.zero;
            }

            return pos;
        }

        private static bool IsEditableNode(EntityManager em, Entity node)
        {
            return em.HasComponent<Node>(node) && !em.HasComponent<Owner>(node) && em.HasBuffer<ConnectedEdge>(node);
        }
    }
}
