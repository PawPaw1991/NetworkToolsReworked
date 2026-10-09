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

        [SettingsUISection(kSection, kGeneralGroup)]
        public SlopeProfile SlopeProfile { get; set; }

        /// <summary>Unit for lengths and offsets in tool panels. A cell is the 8 m zone grid.</summary>
        [SettingsUISection(kSection, kGeneralGroup)]
        public DistanceUnit Unit { get; set; }

        [SettingsUISection(kSection, kGeneralGroup)]
        public bool DebugLogging { get; set; }

        public override void SetDefaults()
        {
            SlopeProfile = SlopeProfile.Linear;
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
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SlopeTool)), "Toggle the Slope tool. Click a start node, hover an end node to preview, click to re-grade the road between them." },
                { m_Setting.GetBindingKeyLocaleID(nameof(Setting.SlopeTool)), "Slope tool" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SlopeProfile)), "Slope shape" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SlopeProfile)), "Linear keeps one constant grade. Ease in/out starts and ends flat and is steepest in the middle." },
                { m_Setting.GetEnumValueLocaleID(SlopeProfile.Linear), "Linear" },
                { m_Setting.GetEnumValueLocaleID(SlopeProfile.EaseInOut), "Ease in/out" },

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
