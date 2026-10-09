using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;
using NetworkToolsReworked.Edits;

namespace NetworkToolsReworked
{
    [FileLocation(nameof(NetworkToolsReworked))]
    [SettingsUIGroupOrder(kKeybindingGroup, kGeneralGroup)]
    [SettingsUIShowGroupName(kKeybindingGroup, kGeneralGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";
        public const string kGeneralGroup = "General";
        public const string kKeybindingGroup = "Keybinding";

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
            ConnectMode = ConnectMode.SimpleCurve;
            ParallelOffset = 16f;
            ParallelHeight = 0f;
            ParallelReverse = false;
            Unit = DistanceUnit.Meters;
            DebugLogging = false;
        }
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

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ParallelTool)), "Parallel tool" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ParallelTool)), "Toggle the Parallel tool. Click a start node, hover an end node to preview a copy of the road between them, click to build it." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.ParallelTool)), "Parallel tool" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ParallelOffset)), "Parallel: side offset (m)" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ParallelOffset)), "How far to the side the copy is built, centre to centre. Positive is to the right when looking from the start node to the end node." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ParallelHeight)), "Parallel: height offset (m)" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ParallelHeight)), "Raise or lower the copy, e.g. for a stacked or sunken road." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ParallelReverse)), "Parallel: opposite direction" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ParallelReverse)), "Build the copy running the other way, for the second carriageway of a one-way pair." },

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
