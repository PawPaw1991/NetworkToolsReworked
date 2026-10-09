using Unity.Mathematics;

namespace NetworkToolsReworked.Edits
{
    public enum SlopeProfile
    {
        /// <summary>Constant grade from start to end.</summary>
        Linear,

        /// <summary>Flat at both ends, constant grade in the middle, with smooth transitions between.</summary>
        EaseInOut,

        /// <summary>Keep the current height profile; only end height changes and the arch are applied.</summary>
        Keep,
    }

    public enum CurveMode
    {
        /// <summary>Leave the road's horizontal shape as it is.</summary>
        Keep,

        /// <summary>Line up the direction at every joint so the road flows without kinks. Nodes stay put.</summary>
        Smooth,

        /// <summary>Pull the road onto the straight line between its two ends.</summary>
        Straighten,

        /// <summary>
        /// Highway-style curve between the two ends' directions: the bend tightens gradually from
        /// straight and eases out again (like a transition spiral). Inner nodes move along it.
        /// </summary>
        Transition,
    }

    public static class SlopeProfiles
    {
        /// <summary>
        /// Height fraction f(u) and its derivative for u in [0, 1]. <paramref name="ease"/> is the share
        /// of the length (0 to 0.5) at each end used to bend from flat into the constant middle grade.
        /// </summary>
        public static void Evaluate(SlopeProfile profile, float ease, float u, out float value, out float derivative)
        {
            if (profile != SlopeProfile.EaseInOut || ease <= 0.001f)
            {
                value = u;
                derivative = 1f;
                return;
            }

            var t = math.min(ease, 0.5f);
            var g = 1f / (1f - t);
            if (u < t)
            {
                value = g * u * u / (2f * t);
                derivative = g * u / t;
            }
            else if (u > 1f - t)
            {
                var v = 1f - u;
                value = 1f - g * v * v / (2f * t);
                derivative = g * v / t;
            }
            else
            {
                value = g * t / 2f + g * (u - t);
                derivative = g;
            }
        }
    }
}
