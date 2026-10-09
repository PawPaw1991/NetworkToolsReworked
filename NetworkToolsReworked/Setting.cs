using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;
using NetworkToolsReworked.Edits;
using NetworkToolsReworked.Tools;

namespace NetworkToolsReworked
{
    [FileLocation(nameof(NetworkToolsReworked))]
    [SettingsUIGroupOrder(kKeybindingGroup, kQuickGroup, kGeneralGroup)]
    [SettingsUIShowGroupName(kKeybindingGroup, kQuickGroup, kGeneralGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";
        public const string kGeneralGroup = "General";
        public const string kKeybindingGroup = "Keybinding";
        public const string kQuickGroup = "QuickSwitch";

        public Setting(IMod mod) : base(mod)
        {
        }

        public enum DistanceUnit
        {
            Meters,
            Cells,
        }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.N, nameof(AddNodeTool), ctrl: true)]
        public ProxyBinding AddNodeTool { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.N, nameof(RemoveNodeTool), ctrl: true, shift: true)]
        public ProxyBinding RemoveNodeTool { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.G, nameof(SlopeTool), ctrl: true)]
        public ProxyBinding SlopeTool { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.J, nameof(ConnectTool), ctrl: true)]
        public ProxyBinding ConnectTool { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Comma, nameof(ConnectRotateLeft))]
        public ProxyBinding ConnectRotateLeft { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Period, nameof(ConnectRotateRight))]
        public ProxyBinding ConnectRotateRight { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.P, nameof(ParallelTool), ctrl: true, shift: true)]
        public ProxyBinding ParallelTool { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.G, nameof(SmoothTool), ctrl: true, shift: true)]
        public ProxyBinding SmoothTool { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.D, nameof(MoveNodeTool), ctrl: true, shift: true)]
        public ProxyBinding MoveNodeTool { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.A, nameof(ArrangeTool), ctrl: true, shift: true)]
        public ProxyBinding ArrangeTool { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.R, nameof(ReverseTool), ctrl: true, shift: true)]
        public ProxyBinding ReverseTool { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.O, nameof(RoundaboutTool), ctrl: true, shift: true)]
        public ProxyBinding RoundaboutTool { get; set; }

        /// <summary>Ctrl+Alt+Z so it doesn't take Ctrl+Z from other mods' undo (Move It uses it).</summary>
        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Z, nameof(UndoTool), ctrl: true, alt: true)]
        public ProxyBinding UndoTool { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.X, nameof(IntersectTool), ctrl: true, shift: true)]
        public ProxyBinding IntersectTool { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.T, nameof(ReplaceTool), ctrl: true, shift: true)]
        public ProxyBinding ReplaceTool { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.U, nameof(UpgradesTool), ctrl: true, shift: true)]
        public ProxyBinding UpgradesTool { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.M, nameof(MeasureTool), ctrl: true, shift: true)]
        public ProxyBinding MeasureTool { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.E, nameof(RampTool), ctrl: true, shift: true)]
        public ProxyBinding RampTool { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.H, nameof(MatchHeightTool), ctrl: true, shift: true)]
        public ProxyBinding MatchHeightTool { get; set; }

        /// <summary>Steps back one pick in the active tool, like a right-click. Esc is left to the game.</summary>
        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Backspace, nameof(StepBack))]
        public ProxyBinding StepBack { get; set; }

        [SettingsUISection(kSection, kQuickGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit1, nameof(QuickTool1), alt: true)]
        public ProxyBinding QuickTool1 { get; set; }

        [SettingsUISection(kSection, kQuickGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit2, nameof(QuickTool2), alt: true)]
        public ProxyBinding QuickTool2 { get; set; }

        [SettingsUISection(kSection, kQuickGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit3, nameof(QuickTool3), alt: true)]
        public ProxyBinding QuickTool3 { get; set; }

        [SettingsUISection(kSection, kQuickGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit4, nameof(QuickTool4), alt: true)]
        public ProxyBinding QuickTool4 { get; set; }

        [SettingsUISection(kSection, kQuickGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit5, nameof(QuickTool5), alt: true)]
        public ProxyBinding QuickTool5 { get; set; }

        [SettingsUISection(kSection, kQuickGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit6, nameof(QuickTool6), alt: true)]
        public ProxyBinding QuickTool6 { get; set; }

        [SettingsUISection(kSection, kQuickGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit7, nameof(QuickTool7), alt: true)]
        public ProxyBinding QuickTool7 { get; set; }

        [SettingsUISection(kSection, kQuickGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit8, nameof(QuickTool8), alt: true)]
        public ProxyBinding QuickTool8 { get; set; }

        [SettingsUISection(kSection, kQuickGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit9, nameof(QuickTool9), alt: true)]
        public ProxyBinding QuickTool9 { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.K, nameof(HealthTool), ctrl: true, shift: true)]
        public ProxyBinding HealthTool { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.B, nameof(BridgeTool), ctrl: true, shift: true)]
        public ProxyBinding BridgeTool { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.F, nameof(FilletTool), ctrl: true, shift: true)]
        public ProxyBinding FilletTool { get; set; }

        [SettingsUISection(kSection, kGeneralGroup)]
        public SlopeProfile SlopeProfile { get; set; }

        // Shape options below are set from the tool panel only.

        /// <summary>Percent of the length at each end that eases into the grade (Ease in/out).</summary>
        [SettingsUIHidden]
        public float SlopeEase { get; set; }

        /// <summary>Extra height at the middle of the shaped road, in metres.</summary>
        [SettingsUIHidden]
        public float SlopeArch { get; set; }

        [SettingsUIHidden]
        public CurveMode CurveMode { get; set; }

        /// <summary>Percent of the way from the current curve to the smoothed or straight one.</summary>
        [SettingsUIHidden]
        public float CurveStrength { get; set; }

        [SettingsUIHidden]
        public bool CurveKeepEnds { get; set; }

        /// <summary>Smooth tool: percent of the way from the current curve to the fully smoothed one.</summary>
        [SettingsUIHidden]
        public float SmoothStrength { get; set; }

        [SettingsUIHidden]
        public bool SmoothKeepEnds { get; set; }

        /// <summary>Smooth tool: also even out bumps and dips in the grade at the nodes.</summary>
        [SettingsUIHidden]
        public bool SmoothGrades { get; set; }

        /// <summary>Smooth tool: percent to pull inner nodes towards an even line (moves nodes).</summary>
        [SettingsUIHidden]
        public float SmoothRelax { get; set; }

        [SettingsUIHidden]
        public ArrangeMode ArrangeMode { get; set; }

        /// <summary>Arrange arc: percent of the road's current bulge to keep; negative flips the side.</summary>
        [SettingsUIHidden]
        public float ArrangeBulge { get; set; }

        /// <summary>Roundabout ring radius in metres, centre line.</summary>
        [SettingsUIHidden]
        public float RoundaboutRadius { get; set; }

        /// <summary>Ring direction seen from above; clockwise suits left-hand traffic.</summary>
        [SettingsUIHidden]
        public bool RoundaboutClockwise { get; set; }

        /// <summary>Change road type: keep each road's upgrades (trees, sidewalks...) on the new type.</summary>
        [SettingsUIHidden]
        public bool ReplaceKeepUpgrades { get; set; }

        /// <summary>Copy upgrades: put left-side upgrades on the right and vice versa (roads drawn the other way).</summary>
        [SettingsUIHidden]
        public bool UpgradesSwapSides { get; set; }

        [SettingsUIHidden]
        public ParallelSpacing ParallelSpacing { get; set; }

        /// <summary>Parallel, Touching spacing: gap between the road edges in metres.</summary>
        [SettingsUIHidden]
        public float ParallelGap { get; set; }

        /// <summary>Parallel, Widths spacing: centre-to-centre distance in road widths.</summary>
        [SettingsUIHidden]
        public float ParallelWidths { get; set; }

        /// <summary>Parallel: build a copy on each side of the road.</summary>
        [SettingsUIHidden]
        public bool ParallelBothSides { get; set; }

        /// <summary>Ramp: on the right of the road's direction (false: left).</summary>
        [SettingsUIHidden]
        public bool RampRight { get; set; }

        /// <summary>Ramp: an entry merging into the road instead of an exit.</summary>
        [SettingsUIHidden]
        public bool RampEntry { get; set; }

        /// <summary>Ramp: take the road's direction the other way round.</summary>
        [SettingsUIHidden]
        public bool RampFlip { get; set; }

        /// <summary>Ramp: angle off the road at the junction, degrees.</summary>
        [SettingsUIHidden]
        public float RampAngle { get; set; }

        /// <summary>Ramp: extra turn along the ramp, degrees; positive turns away from the road.</summary>
        [SettingsUIHidden]
        public float RampTurn { get; set; }

        [SettingsUIHidden]
        public float RampLength { get; set; }

        /// <summary>Ramp: height of the far end above the junction, metres.</summary>
        [SettingsUIHidden]
        public float RampHeight { get; set; }

        /// <summary>Round corner: radius of the curve, metres.</summary>
        [SettingsUIHidden]
        public float FilletRadius { get; set; }

        /// <summary>Bridge and tunnel: what sets the height.</summary>
        [SettingsUIHidden]
        public LiftMode BridgeMode { get; set; }

        /// <summary>Bridge and tunnel: height to raise or depth to lower, metres.</summary>
        [SettingsUIHidden]
        public float BridgeHeight { get; set; }

        /// <summary>Bridge and tunnel, over crossings: height kept above crossing roads, metres.</summary>
        [SettingsUIHidden]
        public float BridgeClearance { get; set; }

        /// <summary>Network check: segments shorter than this (metres) are reported; 0 turns it off.</summary>
        [SettingsUIHidden]
        public float HealthMinLength { get; set; }

        [SettingsUIHidden]
        public MoveSnap MoveSnap { get; set; }

        /// <summary>Move Node grid snapping size in metres.</summary>
        [SettingsUIHidden]
        public float MoveGridSize { get; set; }

        /// <summary>Warn when a reshaped road passes closer than this (metres) above or below another road; 0 turns it off.</summary>
        [SettingsUISection(kSection, kGeneralGroup)]
        [SettingsUISlider(min = 0, max = 15, step = 0.5f)]
        public float ClearanceWarning { get; set; }

        /// <summary>Sideways distance of the Parallel copy in metres; positive is to the right.</summary>
        [SettingsUISection(kSection, kGeneralGroup)]
        [SettingsUISlider(min = -64, max = 64, step = 1)]
        public float ParallelOffset { get; set; }

        [SettingsUISection(kSection, kGeneralGroup)]
        [SettingsUISlider(min = -40, max = 40, step = 1)]
        public float ParallelHeight { get; set; }

        [SettingsUISection(kSection, kGeneralGroup)]
        public bool ParallelReverse { get; set; }

        [SettingsUISection(kSection, kGeneralGroup)]
        public ConnectMode ConnectMode { get; set; }

        /// <summary>Unit for lengths and offsets in tool panels. A cell is the 8 m zone grid.</summary>
        [SettingsUISection(kSection, kGeneralGroup)]
        public DistanceUnit Unit { get; set; }

        [SettingsUISection(kSection, kGeneralGroup)]
        public bool DebugLogging { get; set; }

        public override void SetDefaults()
        {
            SlopeProfile = SlopeProfile.Linear;
            SlopeEase = 50f;
            SlopeArch = 0f;
            CurveMode = CurveMode.Keep;
            CurveStrength = 100f;
            CurveKeepEnds = true;
            SmoothStrength = 100f;
            SmoothKeepEnds = true;
            SmoothGrades = true;
            SmoothRelax = 0f;
            ArrangeMode = ArrangeMode.EvenSpacing;
            ArrangeBulge = 100f;
            RoundaboutRadius = 24f;
            RoundaboutClockwise = false;
            ReplaceKeepUpgrades = true;
            UpgradesSwapSides = false;
            ParallelSpacing = ParallelSpacing.Metres;
            ParallelGap = 0f;
            ParallelWidths = 1f;
            ParallelBothSides = false;
            RampRight = true;
            RampEntry = false;
            RampFlip = false;
            RampAngle = 15f;
            RampTurn = 0f;
            RampLength = 120f;
            RampHeight = 6f;
            HealthMinLength = 3f;
            FilletRadius = 40f;
            BridgeMode = LiftMode.OverCrossings;
            BridgeHeight = 10f;
            BridgeClearance = 8f;
            MoveSnap = MoveSnap.Off;
            MoveGridSize = 8f;
            ClearanceWarning = 6f;
            ConnectMode = ConnectMode.SimpleCurve;
            ParallelOffset = 16f;
            ParallelHeight = 0f;
            ParallelReverse = false;
            Unit = DistanceUnit.Meters;
            DebugLogging = false;
        }
    }

    public enum MoveSnap
    {
        Off,
        Grid,
        Angle,
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;

        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Network Tools Reworked" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kGeneralGroup), "General" },

                { m_Setting.GetOptionGroupLocaleID(Setting.kKeybindingGroup), "Key bindings" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kQuickGroup), "Quick switch" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.StepBack)), "Step back" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.StepBack)), "Step back one pick in the active tool (like a right-click), or leave the tool if nothing is picked." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.StepBack)), "Step back" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.QuickTool1)), "Quick switch 1: Slope & Curve" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.QuickTool1)), "Switch straight to the Slope & Curve tool (or leave it if it is active)." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.QuickTool1)), "Slope & Curve" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.QuickTool2)), "Quick switch 2: Smooth" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.QuickTool2)), "Switch straight to the Smooth tool (or leave it if it is active)." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.QuickTool2)), "Smooth" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.QuickTool3)), "Quick switch 3: Move Node" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.QuickTool3)), "Switch straight to the Move Node tool (or leave it if it is active)." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.QuickTool3)), "Move Node" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.QuickTool4)), "Quick switch 4: Arrange" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.QuickTool4)), "Switch straight to the Arrange tool (or leave it if it is active)." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.QuickTool4)), "Arrange" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.QuickTool5)), "Quick switch 5: Connect" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.QuickTool5)), "Switch straight to the Connect tool (or leave it if it is active)." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.QuickTool5)), "Connect" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.QuickTool6)), "Quick switch 6: Parallel" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.QuickTool6)), "Switch straight to the Parallel tool (or leave it if it is active)." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.QuickTool6)), "Parallel" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.QuickTool7)), "Quick switch 7: Ramp" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.QuickTool7)), "Switch straight to the Ramp tool (or leave it if it is active)." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.QuickTool7)), "Ramp" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.QuickTool8)), "Quick switch 8: Change road type" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.QuickTool8)), "Switch straight to the Change road type tool (or leave it if it is active)." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.QuickTool8)), "Change road type" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.QuickTool9)), "Quick switch 9: Measure" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.QuickTool9)), "Switch straight to the Measure tool (or leave it if it is active)." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.QuickTool9)), "Measure" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AddNodeTool)), "Add Node tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.AddNodeTool)), "Toggle the Add Node tool. Click a road to split it with a new node." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.AddNodeTool)), "Add Node tool" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RemoveNodeTool)), "Remove Node tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.RemoveNodeTool)), "Toggle the Remove Node tool. Click a node between two segments of the same road to merge them." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.RemoveNodeTool)), "Remove Node tool" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SlopeTool)), "Slope tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SlopeTool)), "Toggle the Slope tool. Pick two nodes to re-grade, smooth or straighten the road between them; adjust the shape in the tool panel before applying." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.SlopeTool)), "Slope tool" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SlopeProfile)), "Slope shape" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SlopeProfile)), "Keep leaves the heights as they are. Linear keeps one constant grade. Ease in/out starts and ends flat. More options are in the tool panel." },
                { m_Setting.GetEnumValueLocaleID(SlopeProfile.Linear), "Linear" },
                { m_Setting.GetEnumValueLocaleID(SlopeProfile.EaseInOut), "Ease in/out" },
                { m_Setting.GetEnumValueLocaleID(SlopeProfile.Keep), "Keep" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ConnectTool)), "Connect tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ConnectTool)), "Toggle the Connect tool. Click a start node, hover an end node to preview a new road between them, click to build it." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.ConnectTool)), "Connect tool" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ConnectRotateLeft)), "Connect: rotate start left" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ConnectRotateLeft)), "Turn the start direction of the Connect tool 15 degrees left." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.ConnectRotateLeft)), "Rotate start left" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ConnectRotateRight)), "Connect: rotate start right" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ConnectRotateRight)), "Turn the start direction of the Connect tool 15 degrees right." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.ConnectRotateRight)), "Rotate start right" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ConnectMode)), "Connect curve" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ConnectMode)), "Simple curve leaves the start node in the chosen direction. Smooth both ends also lines up with the road at the end node." },
                { m_Setting.GetEnumValueLocaleID(ConnectMode.SimpleCurve), "Simple curve" },
                { m_Setting.GetEnumValueLocaleID(ConnectMode.SmoothBothEnds), "Smooth both ends" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.MoveNodeTool)), "Move Node tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.MoveNodeTool)), "Toggle the Move Node tool. Click a node, drag it with the cursor, click to drop, then fine-tune its position and height in the tool panel." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.MoveNodeTool)), "Move Node tool" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ArrangeTool)), "Arrange tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ArrangeTool)), "Toggle the Arrange tool. Pick two nodes to space the nodes between them evenly, along the current shape, a straight line or an arc." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.ArrangeTool)), "Arrange tool" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ReverseTool)), "Reverse tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ReverseTool)), "Toggle the Reverse tool. Pick two nodes to reverse the direction of the road between them, e.g. to flip a one-way road." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.ReverseTool)), "Reverse tool" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RoundaboutTool)), "Roundabout tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.RoundaboutTool)), "Toggle the Roundabout tool. Click a junction to turn it into a roundabout; set the radius and direction in the tool panel." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.RoundaboutTool)), "Roundabout tool" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.UndoTool)), "Undo" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.UndoTool)), "Preview undoing the last edit made with Network Tools Reworked, then click or press Apply. Keeps the last 30 edits for this session." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.UndoTool)), "Undo" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.IntersectTool)), "Intersect tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.IntersectTool)), "Toggle the Intersect tool. Hover a road where another crosses it without a junction, click to preview a junction there, then apply." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.IntersectTool)), "Intersect tool" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ReplaceTool)), "Change road type tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ReplaceTool)), "Toggle the Change road type tool. Click a road to copy its type, then pick two nodes: the road between them becomes that type, keeping its shape and height." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.ReplaceTool)), "Change road type tool" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.UpgradesTool)), "Copy upgrades tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.UpgradesTool)), "Toggle the Copy upgrades tool. Click a road to copy its upgrades (trees, sidewalks, sound walls...), then pick two nodes to give the road between them the same upgrades. Copying a road without upgrades clears them." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.UpgradesTool)), "Copy upgrades tool" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.MeasureTool)), "Measure tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.MeasureTool)), "Toggle the Measure tool. Hover a road to see its length, grade, tightest curve and height above ground; click two nodes to measure the road between them. Changes nothing." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.MeasureTool)), "Measure tool" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RampTool)), "Ramp tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.RampTool)), "Toggle the Ramp tool. Hover a road to preview a ramp leaving or joining it at the cursor, click to lock it, set its side, angle, length and height in the tool panel, then apply." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.RampTool)), "Ramp tool" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.MatchHeightTool)), "Match height tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.MatchHeightTool)), "Toggle the Match height tool. Click a node to take its height (or set one in the tool panel), then click other nodes to move them to that height." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.MatchHeightTool)), "Match height tool" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.HealthTool)), "Network check tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.HealthTool)), "Toggle the Network check tool. Scans the city for overlapping roads, loose nodes, tiny segments, ends that nearly meet, grades over the limit and roads cut off from the rest, lists them, jumps to each and fixes the safe ones." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.HealthTool)), "Network check tool" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.BridgeTool)), "Bridge and tunnel tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.BridgeTool)), "Toggle the Bridge and tunnel tool. Pick two nodes to lift the road between them over the roads crossing it, raise it into a bridge or lower it into a tunnel, with approach slopes within its grade limit." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.BridgeTool)), "Bridge and tunnel tool" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.FilletTool)), "Round corner tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.FilletTool)), "Toggle the Round corner tool. Hover a node where two roads meet at an angle to preview the corner rounded off with a curve of the set radius, click to lock it, adjust the radius, then apply." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.FilletTool)), "Round corner tool" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SmoothTool)), "Smooth tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SmoothTool)), "Toggle the Smooth tool. Pick two nodes to smooth the curves, and optionally the grade, of the road between them. Nodes stay put unless Relax is used." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.SmoothTool)), "Smooth tool" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ParallelTool)), "Parallel tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ParallelTool)), "Toggle the Parallel tool. Click a start node, hover an end node to preview a copy of the road between them, click to build it." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.ParallelTool)), "Parallel tool" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ParallelOffset)), "Parallel: side offset (m)" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ParallelOffset)), "How far to the side the copy is built, centre to centre. Positive is to the right when looking from the start node to the end node." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ParallelHeight)), "Parallel: height offset (m)" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ParallelHeight)), "Raise or lower the copy, e.g. for a stacked or sunken road." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ParallelReverse)), "Parallel: opposite direction" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ParallelReverse)), "Build the copy running the other way, for the second carriageway of a one-way pair." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ClearanceWarning)), "Clearance warning (m)" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ClearanceWarning)), "Slope & Curve and Smooth mark crossings where the reshaped road passes closer than this above or below another road. 0 turns the warning off. The game's own checks still decide what can be built." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Unit)), "Distance unit" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Unit)), "Unit used for lengths and offsets in the tool panels. A cell is one 8 m zone grid square." },
                { m_Setting.GetEnumValueLocaleID(Setting.DistanceUnit.Meters), "Meters" },
                { m_Setting.GetEnumValueLocaleID(Setting.DistanceUnit.Cells), "Cells (8 m)" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DebugLogging)), "Debug logging" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.DebugLogging)), "Write detailed tool logs. Leave off unless reporting a bug." },
            };
        }

        public void Unload()
        {
        }
    }
}
