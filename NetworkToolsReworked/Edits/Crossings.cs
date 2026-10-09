using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;

namespace NetworkToolsReworked.Edits
{
    /// <summary>Where two road curves cross when seen from above.</summary>
    public static class Crossings
    {
        private const int kSegments = 32;

        public struct Hit
        {
            public float TA;
            public float TB;
            public float3 PointA;
            public float3 PointB;
        }

        /// <summary>True if the two curves' boxes seen from above overlap (cheap pre-check).</summary>
        public static bool BoundsOverlap(Bezier4x3 a, Bezier4x3 b, float margin = 0f)
        {
            var ba = MathUtils.Bounds(a.xz);
            var bb = MathUtils.Bounds(b.xz);
            return math.all(ba.min - margin <= bb.max) && math.all(bb.min - margin <= ba.max);
        }

        /// <summary>
        /// Adds every crossing of a and b seen from above, ignoring crossings within
        /// <paramref name="endMargin"/> metres of either curve's ends (where connected roads meet).
        /// </summary>
        public static void Find(Bezier4x3 a, Bezier4x3 b, List<Hit> hits, float endMargin = 1f)
        {
            if (!BoundsOverlap(a, b))
                return;

            for (var i = 0; i < kSegments; i++)
            {
                var a0 = MathUtils.Position(a, (float)i / kSegments).xz;
                var a1 = MathUtils.Position(a, (float)(i + 1) / kSegments).xz;
                for (var j = 0; j < kSegments; j++)
                {
                    var b0 = MathUtils.Position(b, (float)j / kSegments).xz;
                    var b1 = MathUtils.Position(b, (float)(j + 1) / kSegments).xz;
                    if (!SegmentIntersect(a0, a1, b0, b1, out var sa, out var sb))
                        continue;

                    var ta = (i + sa) / kSegments;
                    var tb = (j + sb) / kSegments;
                    var pa = MathUtils.Position(a, ta);
                    var pb = MathUtils.Position(b, tb);
                    if (NearEnd(a, pa, endMargin) || NearEnd(b, pb, endMargin))
                        continue;
                    hits.Add(new Hit { TA = ta, TB = tb, PointA = pa, PointB = pb });
                }
            }
        }

        private static bool NearEnd(Bezier4x3 curve, float3 p, float margin)
        {
            return math.distance(curve.a.xz, p.xz) < margin || math.distance(curve.d.xz, p.xz) < margin;
        }

        private static bool SegmentIntersect(float2 p, float2 p2, float2 q, float2 q2, out float s, out float u)
        {
            var r = p2 - p;
            var d = q2 - q;
            var denom = r.x * d.y - r.y * d.x;
            s = u = 0f;
            if (math.abs(denom) < 1e-6f)
                return false;
            var qp = q - p;
            s = (qp.x * d.y - qp.y * d.x) / denom;
            u = (qp.x * r.y - qp.y * r.x) / denom;
            return s >= 0f && s < 1f && u >= 0f && u < 1f;
        }
    }
}
