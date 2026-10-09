using Colossal.Entities;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkToolsReworked.Edits
{
    public enum ConnectMode
    {
        /// <summary>Leave the start node in the chosen direction; the end follows a symmetric arc.</summary>
        SimpleCurve,

        /// <summary>Match the direction of the road at both ends (S-curves where needed).</summary>
        SmoothBothEnds,
    }

    /// <summary>Builds a new road between two existing nodes as a single definition course.</summary>
    public static class ConnectEdit
    {
        /// <summary>
        /// Horizontal direction a road would naturally continue in when leaving this node: away from
        /// its only edge for a dead end, otherwise straight towards the target. Tracks can't branch at
        /// an angle, so on a track the direction is whichever way along an existing track at the node
        /// points most towards the target.
        /// </summary>
        public static float3 DefaultDirection(EntityManager em, Entity node, float3 target)
        {
            var position = em.GetComponentData<Node>(node).m_Position;
            var toTarget = Flat(target - position);
            var buffer = em.GetBuffer<ConnectedEdge>(node, isReadOnly: true);
            var isTrack = IsTrack(em, node);

            var best = float3.zero;
            var bestScore = float.MinValue;
            foreach (var connected in buffer)
            {
                if (em.HasComponent<Owner>(connected.m_Edge) || !em.TryGetComponent(connected.m_Edge, out Edge edge))
                    continue;
                if (edge.m_Start != node && edge.m_End != node)
                    continue;
                var bezier = em.GetComponentData<Curve>(connected.m_Edge).m_Bezier;
                var away = Flat(edge.m_Start == node ? -MathUtils.StartTangent(bezier) : MathUtils.EndTangent(bezier));
                if (math.lengthsq(away) < 0.5f)
                    continue;

                if (buffer.Length == 1)
                    return away;
                if (!isTrack)
                    continue;

                // Along the track either way: continuing past the node, or back the way the edge came.
                foreach (var candidate in new[] { away, -away })
                {
                    var score = math.dot(candidate, toTarget);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                    }
                }
            }

            if (math.lengthsq(best) > 0.5f)
                return best;
            return math.lengthsq(toTarget) > 0.5f ? toTarget : new float3(0f, 0f, 1f);
        }

        /// <summary>True if the node is on a train, tram or subway track.</summary>
        public static bool IsTrack(EntityManager em, Entity node)
        {
            var prefab = PrefabFor(em, node);
            return prefab != Entity.Null && RestyleEdit.KindOf(em, prefab) == RestyleEdit.NetKind.Track;
        }

        private static float3 Flat(float3 v) => math.normalizesafe(new float3(v.x, 0f, v.z));

        public static Entity PrefabFor(EntityManager em, Entity node)
        {
            foreach (var connected in em.GetBuffer<ConnectedEdge>(node, isReadOnly: true))
                if (!em.HasComponent<Owner>(connected.m_Edge))
                    return em.GetComponentData<PrefabRef>(connected.m_Edge).m_Prefab;
            return Entity.Null;
        }

        public static bool AreConnected(EntityManager em, Entity a, Entity b)
        {
            foreach (var connected in em.GetBuffer<ConnectedEdge>(a, isReadOnly: true))
                if (em.TryGetComponent(connected.m_Edge, out Edge edge) && (edge.m_Start == b || edge.m_End == b))
                    return true;
            return false;
        }

        /// <param name="startDirection">Horizontal direction of travel leaving the start node.</param>
        public static bool Emit(EntityManager em, EntityCommandBuffer ecb, Entity start, Entity end, float3 startDirection, ConnectMode mode, int randomSeed)
        {
            if (start == end || AreConnected(em, start, end))
                return false;

            var prefab = PrefabFor(em, start);
            if (prefab == Entity.Null)
                return false;

            var p0 = em.GetComponentData<Node>(start).m_Position;
            var p1 = em.GetComponentData<Node>(end).m_Position;
            var chord = p1 - p0;
            var flatChord = new float3(chord.x, 0f, chord.z);
            if (math.lengthsq(flatChord) < 1f)
                return false;

            // Tracks have to meet the track at the end node in line too.
            if (IsTrack(em, start))
                mode = ConnectMode.SmoothBothEnds;

            float3 endDirection;
            if (mode == ConnectMode.SmoothBothEnds)
            {
                // Arrive moving opposite to how a road would leave the end node.
                endDirection = -DefaultDirection(em, end, p0);
            }
            else
            {
                // Mirror the start direction across the chord for a circular-looking arc.
                var c = math.normalize(flatChord);
                endDirection = 2f * math.dot(startDirection, c) * c - startDirection;
            }

            // Fit the shape on the flat first, then spread the height evenly along it. Tilting the
            // tangents instead squashes the curve sideways on steep links and can kink it.
            var flat0 = new float3(p0.x, 0f, p0.z);
            var flat1 = new float3(p1.x, 0f, p1.z);
            var curve = NetUtils.FitCurve(flat0, Flat(startDirection), Flat(endDirection), flat1);
            curve.a.y = p0.y;
            curve.b.y = math.lerp(p0.y, p1.y, 1f / 3f);
            curve.c.y = math.lerp(p0.y, p1.y, 2f / 3f);
            curve.d.y = p1.y;

            var startElevation = em.TryGetComponent(start, out Elevation e0) ? e0.m_Elevation : float2.zero;
            var endElevation = em.TryGetComponent(end, out Elevation e1) ? e1.m_Elevation : float2.zero;

            var course = new NetCourse
            {
                m_Curve = curve,
                m_StartPosition = new CoursePos
                {
                    m_Entity = start,
                    m_Position = p0,
                    m_Rotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(curve)),
                    m_Elevation = startElevation,
                    m_CourseDelta = 0f,
                    m_Flags = CoursePosFlags.IsFirst,
                    m_ParentMesh = -1,
                },
                m_EndPosition = new CoursePos
                {
                    m_Entity = end,
                    m_Position = p1,
                    m_Rotation = NetUtils.GetNodeRotation(MathUtils.EndTangent(curve)),
                    m_Elevation = endElevation,
                    m_CourseDelta = 1f,
                    m_Flags = CoursePosFlags.IsLast,
                    m_ParentMesh = -1,
                },
                m_Elevation = new float2(startElevation.x, endElevation.x),
                m_Length = MathUtils.Length(curve),
                m_FixedIndex = -1,
            };

            NetDefinitions.Emit(ecb, new CreationDefinition
            {
                m_Prefab = prefab,
                m_RandomSeed = randomSeed,
                m_Flags = CreationFlags.SubElevation,
            }, course);
            return true;
        }
    }
}
