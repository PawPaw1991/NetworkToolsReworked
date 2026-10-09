namespace NetworkToolsReworked.Edits
{
    public enum SlopeProfile
    {
        /// <summary>Constant grade from start to end.</summary>
        Linear,

        /// <summary>Flat at both ends, steepest in the middle.</summary>
        EaseInOut,
    }

    public static class SlopeProfiles
    {
        /// <summary>Height fraction f(u) and its derivative for u in [0, 1].</summary>
        public static void Evaluate(SlopeProfile profile, float u, out float value, out float derivative)
        {
            switch (profile)
            {
                case SlopeProfile.EaseInOut:
                    value = u * u * (3f - 2f * u);
                    derivative = 6f * u * (1f - u);
                    break;
                default:
                    value = u;
                    derivative = 1f;
                    break;
            }
        }
    }
}
