using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Game.Simulation;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkToolsReworked.Edits
{
    public enum ArrangeMode
    {
        /// <summary>Keep the road's shape and space its inner nodes evenly along it.</summary>
        EvenSpacing,

        /// <summary>Put the road on the straight line between its ends, nodes evenly spaced.</summary>
        Line,

        /// <summary>Put the road on a circular arc between its ends, nodes evenly spaced.</summary>
        Arc,
    }

    /// <summary>
    /// Re-lays the road between two picked nodes so its nodes sit evenly spaced along its current shape,
    /// a straight line, or a circular arc. The two end nodes stay; inner nodes move, so the chain is
    /// rebuilt like Slope. Heights follow the road's current height profile.
    /// </summary>
    public static class ArrangeEdit
    {
        /// <param name="bulge">
        /// Arc only: how far the arc's middle sits from the straight line, as a fraction of the distance
        /// the road's current midpoint sits from it (1 = through the current midpoint). Negative flips sides.
        /// </param>
        public static bool Emit(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, List<Entity> nodes, List<Entity> edges, ArrangeMode mode, float bulge, int randomSeed, out float arcRadius)
        {
            arcRadius = 0f;
            var count = edges.Count;
            if (count == 0 || (mode == ArrangeMode.EvenSpacing && count == 1))
                return false;

            var path = new ChainPath(em, nodes, edges);
            var a = path.Original[0].a;
            var b = path.Original[count - 1].d;
            var chord = new float2(b.x - a.x, b.z - a.z);
            var chordLength = math.length(chord);
            if (chordLength < 0.5f)
                return false;

            // Arc through both ends with its middle offset sideways (sagitta) from the chord's midpoint.
            var side = new float2(-chord.y, chord.x) / chordLength;
            var sagitta = 0f;
            if (mode == ArrangeMode.Arc)
            {
                var mid = path.Sample(0.5f, out _);
                var chordMid = (a + b) * 0.5f;
                sagitta = math.dot(new float2(mid.x - chordMid.x, mid.z - chordMid.z), side) * bulge;
                if (math.abs(sagitta) < 0.05f)
                    mode = ArrangeMode.Line;
            }

            var points = new float3[count + 1];
            var directions = new float3[count + 1];
            for (var i = 0; i <= count; i++)
            {
                var u = (float)i / count;
                var heightSource = path.Sample(u, out var originalTangent);
                float2 xz;
                float2 dir;
                switch (mode)
                {
                    case ArrangeMode.Line:
                        xz = math.lerp(a.xz, b.xz, u);
                        dir = chord / chordLength;
                        break;
                    case ArrangeMode.Arc:
                        ArcPoint(a.xz, b.xz, side, sagitta, u, out xz, out dir, out arcRadius);
                        break;
                    default:
                        xz = heightSource.xz;
                        dir = math.normalizesafe(originalTangent.xz);
                        break;
                }

                // Ends keep their exact position.
                if (i == 0) { xz = a.xz; }
                if (i == count) { xz = b.xz; }

                var grade = originalTangent.y / math.max(math.length(originalTangent.xz), 0.001f);
                points[i] = new float3(xz.x, i == 0 ? a.y : i == count ? b.y : heightSource.y, xz.y);
                directions[i] = math.normalize(new float3(dir.x, grade, dir.y));
            }

            var curves = new Bezier4x3[count];
            for (var i = 0; i < count; i++)
                curves[i] = NetUtils.FitCurve(points[i], directions[i], directions[i + 1], points[i + 1]);

            return SlopeEdit.EmitChain(em, ecb, ref terrain, nodes, edges, curves, path.Original, randomSeed, out _);
        }

        /// <summary>Point and direction at fraction u along the arc from a to b (by arc length).</summary>
        private static void ArcPoint(float2 a, float2 b, float2 side, float sagitta, float u, out float2 point, out float2 direction, out float radius)
        {
            var half = math.distance(a, b) * 0.5f;
            var s = math.abs(sagitta);
            radius = (half * half + s * s) / (2f * s);
            var sign = math.sign(sagitta);

            // Centre lies on the perpendicular through the chord midpoint, on the far side from the bulge.
            var centre = (a + b) * 0.5f + side * sign * (s - radius);
            var startAngle = math.atan2(a.y - centre.y, a.x - centre.x);
            var endAngle = math.atan2(b.y - centre.y, b.x - centre.x);
            var sweep = endAngle - startAngle;

            // Pick the sweep that passes through the bulge side.
            var bulgePoint = (a + b) * 0.5f + side * sagitta;
            var bulgeAngle = math.atan2(bulgePoint.y - centre.y, bulgePoint.x - centre.x);
            sweep = Wrap(sweep);
            var midAngle = startAngle + sweep * 0.5f;
            if (math.abs(Wrap(midAngle - bulgeAngle)) > 0.01f)
                sweep = sweep > 0f ? sweep - 2f * math.PI : sweep + 2f * math.PI;

            var angle = startAngle + sweep * u;
            point = centre + radius * new float2(math.cos(angle), math.sin(angle));
            var tangent = new float2(-math.sin(angle), math.cos(angle)) * math.sign(sweep);
            direction = math.normalizesafe(tangent);
        }

        private static float Wrap(float angle)
        {
            while (angle > math.PI) angle -= 2f * math.PI;
            while (angle < -math.PI) angle += 2f * math.PI;
            return angle;
        }
    }

    /// <summary>The chain's current curves, oriented from the first node, sampled by horizontal length.</summary>
    internal readonly struct ChainPath
    {
        public readonly Bezier4x3[] Original;
        private readonly float[] m_Along;

        public ChainPath(EntityManager em, List<Entity> nodes, List<Entity> edges)
        {
            Original = new Bezier4x3[edges.Count];
            m_Along = new float[edges.Count + 1];
            for (var i = 0; i < edges.Count; i++)
            {
                var bezier = em.GetComponentData<Curve>(edges[i]).m_Bezier;
                if (em.GetComponentData<Edge>(edges[i]).m_Start != nodes[i])
                    bezier = MathUtils.Invert(bezier);
                Original[i] = bezier;
                m_Along[i + 1] = m_Along[i] + math.max(MathUtils.Length(bezier.xz), 0.01f);
            }
        }

        public float Length => m_Along[m_Along.Length - 1];

        /// <summary>Position and tangent at fraction u of the horizontal length.</summary>
        public float3 Sample(float u, out float3 tangent)
        {
            var s = math.saturate(u) * Length;
            var i = 0;
            while (i < Original.Length - 1 && s > m_Along[i + 1])
                i++;
            var segment = Original[i];
            var target = s - m_Along[i];

            // Bezier parameter for that distance, by bisection on the horizontal length.
            float lo = 0f, hi = 1f;
            for (var k = 0; k < 20; k++)
            {
                var mid = (lo + hi) * 0.5f;
                if (MathUtils.Length(MathUtils.Cut(segment.xz, new float2(0f, mid))) < target)
                    lo = mid;
                else
                    hi = mid;
            }
            var t = (lo + hi) * 0.5f;
            tangent = MathUtils.Tangent(segment, t);
            return MathUtils.Position(segment, t);
        }
    }
}
