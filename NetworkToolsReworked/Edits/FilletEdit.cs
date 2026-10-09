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
    public struct FilletResult
    {
        public Bezier4x3 Arc;

        /// <summary>Radius used (smaller than asked if the roads are too short for it), metres.</summary>
        public float Radius;

        /// <summary>Largest radius the two roads leave room for, metres.</summary>
        public float MaxRadius;

        /// <summary>How far the road turns at the corner, degrees.</summary>
        public float Turn;

        public bool Clamped;
    }

    /// <summary>
    /// Rounds the corner where exactly two roads meet: both roads are cut back from the corner node and
    /// a curve of the set radius joins them, tangent to both. The two roads keep their identity (they
    /// are re-pointed to the new ends through m_Original); the corner node is left for the game to
    /// remove once nothing uses it.
    /// </summary>
    public static class FilletEdit
    {
        public enum Problem
        {
            None,
            NotACorner,
            Straight,
        }

        /// <summary>Length left on each road after cutting it back, metres.</summary>
        private const float kKeep = 2f;

        private const float kMinTurn = 3f;

        public static Problem Check(EntityManager em, Entity node)
        {
            if (!CornerEdges(em, node, out var edge1, out var edge2))
                return Problem.NotACorner;
            var b1 = Outward(em, edge1, node);
            var b2 = Outward(em, edge2, node);
            return TurnDegrees(b1, b2) < kMinTurn ? Problem.Straight : Problem.None;
        }

        public static bool Emit(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, Entity node, float radius, int randomSeed, out FilletResult result)
        {
            result = default;
            if (!CornerEdges(em, node, out var edge1, out var edge2))
                return false;

            var b1 = Outward(em, edge1, node);
            var b2 = Outward(em, edge2, node);
            var turn = TurnDegrees(b1, b2);
            if (turn < kMinTurn)
                return false;

            // Each road is cut back by the tangent length of the curve: radius x tan(turn / 2).
            var half = math.tan(math.radians(turn) * 0.5f);
            var room = math.min(MathUtils.Length(b1.xz), MathUtils.Length(b2.xz)) - kKeep;
            result.Turn = turn;
            result.MaxRadius = math.max(room, 0f) / half;
            if (result.MaxRadius < 1f)
                return false;
            result.Radius = math.clamp(radius, 1f, result.MaxRadius);
            result.Clamped = radius > result.MaxRadius;
            var cut = result.Radius * half;

            var t1 = AtLength(b1, cut);
            var t2 = AtLength(b2, cut);
            var p1 = MathUtils.Position(b1, t1);
            var p2 = MathUtils.Position(b2, t2);

            // Directions along the new curve: into the corner on road 1, away from it on road 2.
            var dirIn = -Flat(MathUtils.Tangent(b1, t1));
            var dirOut = Flat(MathUtils.Tangent(b2, t2));
            var arc = NetUtils.FitCurve(new float3(p1.x, 0f, p1.z), dirIn, dirOut, new float3(p2.x, 0f, p2.z));
            var g1 = -CurveStats.Grade(b1, t1);
            var g2 = CurveStats.Grade(b2, t2);
            arc.a.y = p1.y;
            arc.d.y = p2.y;
            arc.b.y = arc.a.y + g1 * math.distance(arc.a.xz, arc.b.xz);
            arc.c.y = arc.d.y - g2 * math.distance(arc.c.xz, arc.d.xz);

            var elevated1 = em.HasComponent<Elevation>(edge1);
            var elevated2 = em.HasComponent<Elevation>(edge2);
            var end1 = NewEnd(ref terrain, p1, elevated1);
            var end2 = NewEnd(ref terrain, p2, elevated2);

            EmitTrimmed(em, ecb, ref terrain, edge1, node, Trim(b1, t1), end1, randomSeed);
            EmitTrimmed(em, ecb, ref terrain, edge2, node, Trim(b2, t2), end2, randomSeed);

            // Follow road 1's direction of travel, which matters for one-way roads.
            var e1 = em.GetComponentData<Edge>(edge1);
            if (e1.m_Start == node)
            {
                arc = MathUtils.Invert(arc);
                (end1, end2) = (end2, end1);
            }

            var startPos = end1;
            startPos.m_Rotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(arc));
            startPos.m_CourseDelta = 0f;
            startPos.m_Flags = CoursePosFlags.IsFirst;
            var endPos = end2;
            endPos.m_Rotation = NetUtils.GetNodeRotation(MathUtils.EndTangent(arc));
            endPos.m_CourseDelta = 1f;
            endPos.m_Flags = CoursePosFlags.IsLast;

            var definition = NetDefinitions.Emit(ecb, new CreationDefinition
            {
                m_Prefab = em.GetComponentData<PrefabRef>(edge1).m_Prefab,
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
            if (em.TryGetComponent(edge1, out Upgraded upgraded))
                ecb.AddComponent(definition, upgraded);

            result.Arc = arc;
            return true;
        }

        /// <summary>The two plain roads ending at a node joined by nothing else.</summary>
        private static bool CornerEdges(EntityManager em, Entity node, out Entity edge1, out Entity edge2)
        {
            edge1 = edge2 = Entity.Null;
            if (!SlopeEdit.IsEditableNode(em, node))
                return false;
            var connected = em.GetBuffer<ConnectedEdge>(node, isReadOnly: true);
            if (connected.Length != 2)
                return false;
            edge1 = connected[0].m_Edge;
            edge2 = connected[1].m_Edge;
            if (edge1 == edge2 || em.HasComponent<Owner>(edge1) || em.HasComponent<Owner>(edge2))
                return false;
            var e1 = em.GetComponentData<Edge>(edge1);
            var e2 = em.GetComponentData<Edge>(edge2);
            return (e1.m_Start == node || e1.m_End == node) && (e2.m_Start == node || e2.m_End == node);
        }

        /// <summary>The road's curve running from the node outwards.</summary>
        private static Bezier4x3 Outward(EntityManager em, Entity edge, Entity node)
        {
            var b = em.GetComponentData<Curve>(edge).m_Bezier;
            return em.GetComponentData<Edge>(edge).m_Start == node ? b : MathUtils.Invert(b);
        }

        /// <summary>How far the road turns at the node: 0 when the two roads carry straight on.</summary>
        private static float TurnDegrees(Bezier4x3 out1, Bezier4x3 out2)
        {
            var d1 = Flat(MathUtils.StartTangent(out1));
            var d2 = Flat(MathUtils.StartTangent(out2));
            var between = math.degrees(math.acos(math.clamp(math.dot(d1, d2), -1f, 1f)));
            return 180f - between;
        }

        /// <summary>The outward curve from t to its far end.</summary>
        private static Bezier4x3 Trim(Bezier4x3 outward, float t) => MathUtils.Cut(outward, new float2(t, 1f));

        /// <summary>Re-points a road from the corner node to its new end, keeping the road (m_Original).</summary>
        private static void EmitTrimmed(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, Entity edge, Entity node, Bezier4x3 outward, CoursePos newEnd, int randomSeed)
        {
            var e = em.GetComponentData<Edge>(edge);
            var startsAtCorner = e.m_Start == node;
            var curve = startsAtCorner ? outward : MathUtils.Invert(outward);
            var far = startsAtCorner ? e.m_End : e.m_Start;
            var hasElevation = em.HasComponent<Elevation>(edge);
            var none = new System.Collections.Generic.Dictionary<Entity, float3>();

            CoursePos startPos, endPos;
            if (startsAtCorner)
            {
                startPos = newEnd;
                startPos.m_Rotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(curve));
                startPos.m_CourseDelta = 0f;
                startPos.m_Flags = CoursePosFlags.IsFirst;
                endPos = SlopeEdit.ChainEnd(em, ref terrain, far, none, curve, start: false, hasElevation);
            }
            else
            {
                startPos = SlopeEdit.ChainEnd(em, ref terrain, far, none, curve, start: true, hasElevation);
                endPos = newEnd;
                endPos.m_Rotation = NetUtils.GetNodeRotation(MathUtils.EndTangent(curve));
                endPos.m_CourseDelta = 1f;
                endPos.m_Flags = CoursePosFlags.IsLast;
            }

            var definition = NetDefinitions.Emit(ecb, new CreationDefinition
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
            if (em.TryGetComponent(edge, out Upgraded upgraded))
                ecb.AddComponent(definition, upgraded);
        }

        private static CoursePos NewEnd(ref TerrainHeightData terrain, float3 p, bool elevated)
        {
            var elevation = elevated ? p.y - TerrainUtils.SampleHeight(ref terrain, p) : 0f;
            return new CoursePos
            {
                m_Entity = Entity.Null,
                m_Position = p,
                m_Elevation = new float2(elevation),
                m_ParentMesh = -1,
            };
        }

        /// <summary>Curve position at a horizontal distance from the start.</summary>
        private static float AtLength(Bezier4x3 curve, float length)
        {
            const int samples = 64;
            var previous = curve.a.xz;
            var total = 0f;
            for (var k = 1; k <= samples; k++)
            {
                var p = MathUtils.Position(curve, (float)k / samples).xz;
                var step = math.distance(previous, p);
                if (total + step >= length)
                    return (k - 1 + (length - total) / math.max(step, 1e-5f)) / samples;
                total += step;
                previous = p;
            }
            return 1f;
        }

        private static float3 Flat(float3 v)
        {
            v.y = 0f;
            return math.normalizesafe(v);
        }
    }
}
