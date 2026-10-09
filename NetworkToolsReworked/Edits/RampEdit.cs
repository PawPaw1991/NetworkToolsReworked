using Colossal.Entities;
using Colossal.Mathematics;
using Game.Net;
using Game.Simulation;
using Game.Tools;
using NetworkToolsReworked.Undo;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkToolsReworked.Edits
{
    public struct RampParams
    {
        /// <summary>Ramp on the right of the road's direction (false: left).</summary>
        public bool Right;

        /// <summary>Entry (merging into the road) instead of exit (leaving it).</summary>
        public bool Entry;

        /// <summary>Use the road's direction the other way round (the opposite carriageway of a two-way road).</summary>
        public bool Flip;

        /// <summary>Angle between the road and the ramp where they meet, in degrees.</summary>
        public float Angle;

        /// <summary>Further turn along the ramp, in degrees; positive turns away from the road.</summary>
        public float Turn;

        /// <summary>Wanted ramp length in metres; lengthened if the climb would be too steep.</summary>
        public float Length;

        /// <summary>Height of the far end relative to the junction, in metres.</summary>
        public float Height;
    }

    public struct RampResult
    {
        public Bezier4x3 Curve;
        public float Length;
        public float MaxGrade;
        public bool Lengthened;
        public float EndAboveGround;
    }

    /// <summary>
    /// Builds a ramp branching off (or merging into) an existing road at a point along it. The ramp is one
    /// new course whose road-side end is attached to the edge at that point (CoursePos on the edge with a
    /// split position), so the game splits the road and makes the junction itself.
    /// </summary>
    public static class RampEdit
    {
        /// <summary>Keeps the junction this far from the road's own nodes, in metres.</summary>
        public const float kEndMargin = 10f;

        public static bool CanAttach(EntityManager em, Entity edge, float t)
        {
            var b = em.GetComponentData<Curve>(edge).m_Bezier;
            return MathUtils.Length(MathUtils.Cut(b, new float2(0f, t))) >= kEndMargin && MathUtils.Length(MathUtils.Cut(b, new float2(t, 1f))) >= kEndMargin;
        }

        public static bool Emit(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, Entity edge, float t, Entity prefab, in RampParams ramp, float gradeLimit, int randomSeed, out RampResult result)
        {
            result = default;
            var road = em.GetComponentData<Curve>(edge).m_Bezier;
            var p = MathUtils.Position(road, t);
            var roadTangent = MathUtils.Tangent(road, t);
            var dir = math.normalizesafe(new float3(roadTangent.x, 0f, roadTangent.z));
            if (math.lengthsq(dir) < 0.5f)
                return false;
            var roadGrade = roadTangent.y / math.max(math.length(roadTangent.xz), 0.001f);
            if (ramp.Flip)
            {
                dir = -dir;
                roadGrade = -roadGrade;
            }

            // An exit leaves along the road's direction; an entry is built the same way backwards and
            // then reversed, so traffic flows into the road. The side stays the physical side.
            var side = new float3(dir.z, 0f, -dir.x) * (ramp.Right ? 1f : -1f);
            var baseDir = ramp.Entry ? -dir : dir;
            var startGrade = ramp.Entry ? -roadGrade : roadGrade;

            var limit = math.max(gradeLimit, 0.01f) * 0.95f;
            var length = math.max(ramp.Length, 20f);
            var curve = default(Bezier4x3);
            var maxGrade = 0f;
            for (var attempt = 0; attempt < 12; attempt++)
            {
                curve = Shape(p, baseDir, side, startGrade, ramp, length);
                maxGrade = CurveStats.MaxGrade(curve);
                if (maxGrade <= limit)
                    break;
                result.Lengthened = true;
                length *= 1.15f;
            }

            var farEnd = curve.d;
            var aboveGround = farEnd.y - TerrainUtils.SampleHeight(ref terrain, farEnd);
            var farElevation = math.abs(aboveGround) < 0.5f ? 0f : aboveGround;
            if (ramp.Entry)
                curve = MathUtils.Invert(curve);

            var elevation = em.TryGetComponent(edge, out Elevation edgeElevation) ? edgeElevation.m_Elevation : float2.zero;
            var roadPos = new CoursePos
            {
                m_Entity = edge,
                m_SplitPosition = t,
                m_Position = p,
                m_Elevation = new float2(math.lerp(elevation.x, elevation.y, t)),
                m_ParentMesh = -1,
            };
            var freePos = new CoursePos
            {
                m_Entity = Entity.Null,
                m_Position = farEnd,
                m_Elevation = new float2(farElevation),
                m_ParentMesh = -1,
            };

            var startPos = ramp.Entry ? freePos : roadPos;
            var endPos = ramp.Entry ? roadPos : freePos;
            startPos.m_Rotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(curve));
            startPos.m_CourseDelta = 0f;
            startPos.m_Flags = CoursePosFlags.IsFirst;
            endPos.m_Rotation = NetUtils.GetNodeRotation(MathUtils.EndTangent(curve));
            endPos.m_CourseDelta = 1f;
            endPos.m_Flags = CoursePosFlags.IsLast;

            // The game splits the road at the junction; record that so Undo can join it again.
            UndoRecorder.RecordOriginal(edge);
            UndoRecorder.RecordResult(MathUtils.Cut(road, new float2(0f, t)));
            UndoRecorder.RecordResult(MathUtils.Cut(road, new float2(t, 1f)));

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

            result.Curve = curve;
            result.Length = MathUtils.Length(curve.xz);
            result.MaxGrade = maxGrade;
            result.EndAboveGround = aboveGround;
            return true;
        }

        /// <summary>
        /// Ramp from the junction outwards: leaves at the set angle, turns steadily by the set turn, and
        /// climbs from the road's grade to level at the set height.
        /// </summary>
        private static Bezier4x3 Shape(float3 p, float3 dir, float3 side, float startGrade, in RampParams ramp, float length)
        {
            var theta = math.radians(math.clamp(ramp.Angle, 1f, 89f));
            var phi = math.radians(ramp.Turn);
            var d0 = math.cos(theta) * dir + math.sin(theta) * side;
            var d1 = math.cos(theta + phi) * dir + math.sin(theta + phi) * side;

            // End of a constant-turn arc of this length (a straight line when there is no turn).
            float3 end;
            if (math.abs(phi) < 0.001f)
                end = p + d0 * length;
            else
                end = p + length / phi * ((math.sin(theta + phi) - math.sin(theta)) * dir + (math.cos(theta) - math.cos(theta + phi)) * side);

            var flatStart = new float3(p.x, 0f, p.z);
            var flatEnd = new float3(end.x, 0f, end.z);
            var curve = NetUtils.FitCurve(flatStart, d0, d1, flatEnd);

            var horizontal = math.max(MathUtils.Length(curve.xz), 1f);
            curve.a.y = p.y;
            curve.b.y = p.y + startGrade * horizontal / 3f;
            curve.d.y = p.y + ramp.Height;
            curve.c.y = curve.d.y;
            return curve;
        }
    }
}
