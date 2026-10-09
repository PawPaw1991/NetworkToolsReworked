using Colossal.Entities;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using NetworkToolsReworked.Undo;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkToolsReworked.Edits
{
    /// <summary>
    /// Writes network edits as definition entities (CreationDefinition + NetCourse) so the game's own
    /// systems build the Temp preview, validate it and apply it. Nothing here touches live Node, Edge,
    /// Curve, Elevation or Composition components.
    /// </summary>
    public static class NetDefinitions
    {
        /// <summary>Split an edge at curve position t, leaving a new node there.</summary>
        public static void SplitEdge(EntityManager em, EntityCommandBuffer ecb, Entity edge, float t, int randomSeed)
        {
            var prefab = em.GetComponentData<PrefabRef>(edge).m_Prefab;
            var curve = em.GetComponentData<Curve>(edge).m_Bezier;
            var position = MathUtils.Position(curve, t);
            var rotation = NetUtils.GetNodeRotation(MathUtils.Tangent(curve, t));
            var elevation = EdgeElevation(em, edge);

            var pos = new CoursePos
            {
                m_Entity = edge,
                m_SplitPosition = t,
                m_Position = position,
                m_Rotation = rotation,
                m_Elevation = new float2(math.lerp(elevation.x, elevation.y, t)),
                m_ParentMesh = -1,
            };

            var startPos = pos;
            startPos.m_CourseDelta = 0f;
            startPos.m_Flags = CoursePosFlags.IsFirst;
            var endPos = pos;
            endPos.m_CourseDelta = 1f;
            endPos.m_Flags = CoursePosFlags.IsLast;

            UndoRecorder.RecordOriginal(edge);
            UndoRecorder.RecordResult(MathUtils.Cut(curve, new float2(0f, t)));
            UndoRecorder.RecordResult(MathUtils.Cut(curve, new float2(t, 1f)));

            var course = new NetCourse
            {
                m_Curve = new Bezier4x3(position, position, position, position),
                m_StartPosition = startPos,
                m_EndPosition = endPos,
                m_Elevation = startPos.m_Elevation,
                m_Length = 0f,
                m_FixedIndex = -1,
            };

            Emit(ecb, new CreationDefinition
            {
                m_Prefab = prefab,
                m_RandomSeed = randomSeed,
                m_Flags = CreationFlags.SubElevation,
            }, course);
        }

        /// <summary>
        /// Remove a node that joins exactly two edges of the same network type, replacing both edges
        /// with one curve between their far nodes. Keeps elevation (bridges, tunnels) and the upgrades
        /// both edges share.
        /// </summary>
        public static bool MergeAtNode(EntityManager em, EntityCommandBuffer ecb, Entity node, int randomSeed)
        {
            if (!CanMergeAtNode(em, node, out var edge1, out var edge2))
                return false;

            var e1 = em.GetComponentData<Edge>(edge1);
            var e2 = em.GetComponentData<Edge>(edge2);

            // Orient edge1 as far1 -> node and edge2 as node -> far2.
            var reverse1 = e1.m_Start == node;
            var reverse2 = e2.m_End == node;
            var far1 = reverse1 ? e1.m_End : e1.m_Start;
            var far2 = reverse2 ? e2.m_Start : e2.m_End;

            var curve1 = em.GetComponentData<Curve>(edge1);
            var curve2 = em.GetComponentData<Curve>(edge2);
            var b1 = reverse1 ? MathUtils.Invert(curve1.m_Bezier) : curve1.m_Bezier;
            var b2 = reverse2 ? MathUtils.Invert(curve2.m_Bezier) : curve2.m_Bezier;

            var merged = NetUtils.FitCurve(b1.a, MathUtils.StartTangent(b1), MathUtils.EndTangent(b2), b2.d);

            var elevation1 = EdgeElevation(em, edge1);
            var elevation2 = EdgeElevation(em, edge2);
            if (reverse1) elevation1 = elevation1.yx;
            if (reverse2) elevation2 = elevation2.yx;
            var elevation = new float2(elevation1.x, elevation2.y);

            DeleteEdge(em, ecb, edge1, randomSeed);
            DeleteEdge(em, ecb, edge2, randomSeed);
            DeleteNode(em, ecb, node, randomSeed);

            var course = new NetCourse
            {
                m_Curve = merged,
                m_StartPosition = EndPoint(em, far1, merged, start: true, elevation.x),
                m_EndPosition = EndPoint(em, far2, merged, start: false, elevation.y),
                m_Elevation = elevation,
                m_Length = MathUtils.Length(merged),
                m_FixedIndex = -1,
            };

            var definition = Emit(ecb, new CreationDefinition
            {
                m_Prefab = em.GetComponentData<PrefabRef>(edge1).m_Prefab,
                m_RandomSeed = randomSeed,
                m_Flags = CreationFlags.SubElevation,
            }, course);

            // Keep only the upgrades (sidewalks, trees, walls...) both edges had.
            if (em.TryGetComponent(edge1, out Upgraded upgraded1) && em.TryGetComponent(edge2, out Upgraded upgraded2))
            {
                var shared = upgraded1.m_Flags & upgraded2.m_Flags;
                if (shared != default)
                    ecb.AddComponent(definition, new Upgraded { m_Flags = shared });
            }

            return true;
        }

        public static bool CanMergeAtNode(EntityManager em, Entity node, out Entity edge1, out Entity edge2)
        {
            edge1 = edge2 = Entity.Null;
            if (!em.HasComponent<Node>(node) || em.HasComponent<Owner>(node) || !em.HasBuffer<ConnectedEdge>(node))
                return false;

            var connected = em.GetBuffer<ConnectedEdge>(node, isReadOnly: true);
            if (connected.Length != 2)
                return false;

            edge1 = connected[0].m_Edge;
            edge2 = connected[1].m_Edge;
            if (em.HasComponent<Owner>(edge1) || em.HasComponent<Owner>(edge2))
                return false;

            // Both edges must actually end at this node (not just pass by it as a lane connection).
            var e1 = em.GetComponentData<Edge>(edge1);
            var e2 = em.GetComponentData<Edge>(edge2);
            if ((e1.m_Start != node && e1.m_End != node) || (e2.m_Start != node && e2.m_End != node))
                return false;

            return em.GetComponentData<PrefabRef>(edge1).m_Prefab == em.GetComponentData<PrefabRef>(edge2).m_Prefab;
        }

        public static void DeleteEdge(EntityManager em, EntityCommandBuffer ecb, Entity edge, int randomSeed)
        {
            var e = em.GetComponentData<Edge>(edge);
            var bezier = em.GetComponentData<Curve>(edge).m_Bezier;

            var course = new NetCourse
            {
                m_Curve = bezier,
                m_StartPosition = new CoursePos
                {
                    m_Entity = e.m_Start,
                    m_Position = bezier.a,
                    m_Rotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(bezier)),
                    m_CourseDelta = 0f,
                    m_ParentMesh = -1,
                },
                m_EndPosition = new CoursePos
                {
                    m_Entity = e.m_End,
                    m_Position = bezier.d,
                    m_Rotation = NetUtils.GetNodeRotation(MathUtils.EndTangent(bezier)),
                    m_CourseDelta = 1f,
                    m_ParentMesh = -1,
                },
                m_Length = MathUtils.Length(bezier),
                m_FixedIndex = -1,
            };

            Emit(ecb, new CreationDefinition
            {
                m_Original = edge,
                m_Prefab = em.GetComponentData<PrefabRef>(edge).m_Prefab,
                m_RandomSeed = randomSeed,
                m_Flags = CreationFlags.Delete,
            }, course);
        }

        public static void DeleteNode(EntityManager em, EntityCommandBuffer ecb, Entity node, int randomSeed)
        {
            var n = em.GetComponentData<Node>(node);
            var pos = new CoursePos
            {
                m_Entity = node,
                m_Position = n.m_Position,
                m_Rotation = n.m_Rotation,
                m_ParentMesh = -1,
            };
            var endPos = pos;
            endPos.m_CourseDelta = 1f;

            Emit(ecb, new CreationDefinition
            {
                m_Original = node,
                m_Prefab = em.GetComponentData<PrefabRef>(node).m_Prefab,
                m_RandomSeed = randomSeed,
                m_Flags = CreationFlags.Delete,
            }, new NetCourse
            {
                m_Curve = new Bezier4x3(n.m_Position, n.m_Position, n.m_Position, n.m_Position),
                m_StartPosition = pos,
                m_EndPosition = endPos,
                m_FixedIndex = -1,
            });
        }

        private static CoursePos EndPoint(EntityManager em, Entity node, Bezier4x3 curve, bool start, float elevation)
        {
            var n = em.GetComponentData<Node>(node);
            var tangent = start ? MathUtils.StartTangent(curve) : MathUtils.EndTangent(curve);
            return new CoursePos
            {
                m_Entity = node,
                m_Position = n.m_Position,
                m_Rotation = NetUtils.GetNodeRotation(tangent),
                m_Elevation = em.TryGetComponent(node, out Elevation nodeElevation) ? nodeElevation.m_Elevation : new float2(elevation),
                m_CourseDelta = start ? 0f : 1f,
                m_Flags = start ? CoursePosFlags.IsFirst : CoursePosFlags.IsLast,
                m_ParentMesh = -1,
            };
        }

        private static float2 EdgeElevation(EntityManager em, Entity edge)
        {
            return em.TryGetComponent(edge, out Elevation elevation) ? elevation.m_Elevation : float2.zero;
        }

        internal static Entity Emit(EntityCommandBuffer ecb, CreationDefinition definition, NetCourse course)
        {
            UndoRecorder.Record(definition, course);
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, definition);
            ecb.AddComponent(entity, course);
            ecb.AddComponent(entity, default(Updated));
            return entity;
        }
    }
}
