using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;

namespace NetworkToolsReworked
{
    [FileLocation(nameof(NetworkToolsReworked))]
    [SettingsUIGroupOrder(kGeneralGroup)]
    [SettingsUIShowGroupName(kGeneralGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";
        public const string kGeneralGroup = "General";

        public Setting(IMod mod) : base(mod)
        {
        }

        public enum DistanceUnit
        {
            Meters,
            Cells,
        }

        /// <summary>Unit for lengths and offsets in tool panels. A cell is the 8 m zone grid.</summary>
        [SettingsUISection(kSection, kGeneralGroup)]
        public DistanceUnit Unit { get; set; }

        [SettingsUISection(kSection, kGeneralGroup)]
        public bool DebugLogging { get; set; }

        public override void SetDefaults()
        {
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
