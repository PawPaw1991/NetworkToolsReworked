using NetworkToolsReworked.Edits;
using Unity.Mathematics;

namespace NetworkToolsReworked.Tools
{
    /// <summary>
    /// Smooths the road between two picked nodes: lines up the direction, and optionally the grade, at
    /// every joint. By default no node moves, so the roads are edited in place; Relax also evens out
    /// the inner node positions, which rebuilds the stretch like the Slope tool.
    /// </summary>
    public partial class SmoothToolSystem : SlopeToolSystem
    {
        public override string toolID => "NetworkToolsReworked.SmoothTool";

        protected override ShapeParams BuildShape()
        {
            var settings = Mod.Settings;
            return new ShapeParams
            {
                Profile = SlopeProfile.Keep,
                Curve = CurveMode.Smooth,
                CurveStrength = math.clamp(settings.SmoothStrength, 0f, 100f) / 100f,
                KeepEndDirections = settings.SmoothKeepEnds,
                SmoothGrades = settings.SmoothGrades,
                Relax = math.clamp(settings.SmoothRelax, 0f, 100f) / 100f,
            };
        }
    }
}
