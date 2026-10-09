using Colossal.Mathematics;
using Unity.Mathematics;

namespace NetworkToolsReworked.Edits
{
    /// <summary>Grade and curvature measurements of a road curve, sampled along its length.</summary>
    public static class CurveStats
    {
        public const int kSamples = 32;

        /// <summary>Grade (rise per horizontal metre, signed) at curve position t.</summary>
        public static float Grade(Bezier4x3 curve, float t)
        {
            var tangent = MathUtils.Tangent(curve, t);
            return tangent.y / math.max(math.length(tangent.xz), 0.001f);
        }

        /// <summary>Steepest grade along the curve, as a positive number.</summary>
        public static float MaxGrade(Bezier4x3 curve)
        {
            var max = 0f;
            for (var k = 0; k <= kSamples; k++)
                max = math.max(max, math.abs(Grade(curve, (float)k / kSamples)));
            return max;
        }

        /// <summary>Horizontal radius of curvature at t; float.PositiveInfinity where the road is straight.</summary>
        public static float Radius(Bezier4x3 curve, float t)
        {
            var d1 = MathUtils.Tangent(curve, t).xz;
            var d2 = SecondDerivative(curve, t).xz;
            var cross = math.abs(d1.x * d2.y - d1.y * d2.x);
            var speed = math.length(d1);
            if (cross < 1e-5f || speed < 1e-4f)
                return float.PositiveInfinity;
            return speed * speed * speed / cross;
        }

        /// <summary>Tightest horizontal radius along the curve.</summary>
        public static float MinRadius(Bezier4x3 curve)
        {
            var min = float.PositiveInfinity;
            for (var k = 0; k <= kSamples; k++)
                min = math.min(min, Radius(curve, (float)k / kSamples));
            return min;
        }

        /// <summary>Horizontal direction change from start to end in degrees, signed (positive turns left).</summary>
        public static float Turn(float3 fromDirection, float3 toDirection)
        {
            var a = math.normalizesafe(fromDirection.xz);
            var b = math.normalizesafe(toDirection.xz);
            return math.degrees(math.atan2(a.x * b.y - a.y * b.x, math.dot(a, b)));
        }

        private static float3 SecondDerivative(Bezier4x3 c, float t)
        {
            return 6f * (1f - t) * (c.a - 2f * c.b + c.c) + 6f * t * (c.b - 2f * c.c + c.d);
        }
    }
}
