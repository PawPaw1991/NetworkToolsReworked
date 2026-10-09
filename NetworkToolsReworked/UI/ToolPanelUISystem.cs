using System;
using Colossal.UI.Binding;
using Game.UI;
using NetworkToolsReworked.Edits;
using NetworkToolsReworked.Tools;

namespace NetworkToolsReworked.UI
{
    /// <summary>Bindings for the in-game tool panel (UI/src). Group name must match UI/mod.json "id".</summary>
    public partial class ToolPanelUISystem : UISystemBase
    {
        private const string kGroup = "NetworkToolsReworked";

        private ToolActivationSystem m_ToolActivationSystem;
        private ValueBinding<bool> m_PanelOpen;
        private ValueBinding<string> m_ActiveTool;
        private ValueBinding<int> m_SlopeProfile;
        private ValueBinding<int> m_ConnectMode;
        private ValueBinding<float> m_ParallelOffset;
        private ValueBinding<float> m_ParallelHeight;
        private ValueBinding<bool> m_ParallelReverse;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolActivationSystem = World.GetOrCreateSystemManaged<ToolActivationSystem>();

            AddBinding(m_PanelOpen = new ValueBinding<bool>(kGroup, "PanelOpen", false));
            AddBinding(m_ActiveTool = new ValueBinding<string>(kGroup, "ActiveTool", nameof(ToolId.None)));
            AddBinding(m_SlopeProfile = new ValueBinding<int>(kGroup, "SlopeProfile", 0));
            AddBinding(m_ConnectMode = new ValueBinding<int>(kGroup, "ConnectMode", 0));
            AddBinding(m_ParallelOffset = new ValueBinding<float>(kGroup, "ParallelOffset", 16f));
            AddBinding(m_ParallelHeight = new ValueBinding<float>(kGroup, "ParallelHeight", 0f));
            AddBinding(m_ParallelReverse = new ValueBinding<bool>(kGroup, "ParallelReverse", false));

            AddBinding(new TriggerBinding(kGroup, "TogglePanel", () => m_PanelOpen.Update(!m_PanelOpen.value)));
            AddBinding(new TriggerBinding<string>(kGroup, "SelectTool", name =>
            {
                if (Enum.TryParse(name, out ToolId tool))
                    m_ToolActivationSystem.Activate(tool);
            }));
            AddBinding(new TriggerBinding<int>(kGroup, "SetSlopeProfile", v => Save(s => s.SlopeProfile = (SlopeProfile)v)));
            AddBinding(new TriggerBinding<int>(kGroup, "SetConnectMode", v => Save(s => s.ConnectMode = (ConnectMode)v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetParallelOffset", v => Save(s => s.ParallelOffset = v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetParallelHeight", v => Save(s => s.ParallelHeight = v)));
            AddBinding(new TriggerBinding<bool>(kGroup, "SetParallelReverse", v => Save(s => s.ParallelReverse = v)));
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            var settings = Mod.Settings;
            m_ActiveTool.Update(m_ToolActivationSystem.Current.ToString());
            m_SlopeProfile.Update((int)settings.SlopeProfile);
            m_ConnectMode.Update((int)settings.ConnectMode);
            m_ParallelOffset.Update(settings.ParallelOffset);
            m_ParallelHeight.Update(settings.ParallelHeight);
            m_ParallelReverse.Update(settings.ParallelReverse);
        }

        private static void Save(Action<Setting> change)
        {
            change(Mod.Settings);
            Mod.Settings.ApplyAndSave();
        }
    }
}
