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
        private ValueBinding<string> m_Phase;
        private ValueBinding<string> m_Summary;
        private ValueBinding<int> m_ConnectRotation;
        private ValueBinding<float> m_SlopeEase;
        private ValueBinding<float> m_SlopeArch;
        private ValueBinding<float> m_SlopeStartOffset;
        private ValueBinding<float> m_SlopeEndOffset;
        private ValueBinding<int> m_CurveMode;
        private ValueBinding<float> m_CurveStrength;
        private ValueBinding<bool> m_CurveKeepEnds;
        private SlopeToolSystem m_SlopeToolSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolActivationSystem = World.GetOrCreateSystemManaged<ToolActivationSystem>();
            m_SlopeToolSystem = World.GetOrCreateSystemManaged<SlopeToolSystem>();

            AddBinding(m_PanelOpen = new ValueBinding<bool>(kGroup, "PanelOpen", false));
            AddBinding(m_ActiveTool = new ValueBinding<string>(kGroup, "ActiveTool", nameof(ToolId.None)));
            AddBinding(m_SlopeProfile = new ValueBinding<int>(kGroup, "SlopeProfile", 0));
            AddBinding(m_ConnectMode = new ValueBinding<int>(kGroup, "ConnectMode", 0));
            AddBinding(m_ParallelOffset = new ValueBinding<float>(kGroup, "ParallelOffset", 16f));
            AddBinding(m_ParallelHeight = new ValueBinding<float>(kGroup, "ParallelHeight", 0f));
            AddBinding(m_ParallelReverse = new ValueBinding<bool>(kGroup, "ParallelReverse", false));
            AddBinding(m_Phase = new ValueBinding<string>(kGroup, "Phase", string.Empty));
            AddBinding(m_Summary = new ValueBinding<string>(kGroup, "Summary", string.Empty));
            AddBinding(m_ConnectRotation = new ValueBinding<int>(kGroup, "ConnectRotation", 0));
            AddBinding(m_SlopeEase = new ValueBinding<float>(kGroup, "SlopeEase", 50f));
            AddBinding(m_SlopeArch = new ValueBinding<float>(kGroup, "SlopeArch", 0f));
            AddBinding(m_SlopeStartOffset = new ValueBinding<float>(kGroup, "SlopeStartOffset", 0f));
            AddBinding(m_SlopeEndOffset = new ValueBinding<float>(kGroup, "SlopeEndOffset", 0f));
            AddBinding(m_CurveMode = new ValueBinding<int>(kGroup, "CurveMode", 0));
            AddBinding(m_CurveStrength = new ValueBinding<float>(kGroup, "CurveStrength", 100f));
            AddBinding(m_CurveKeepEnds = new ValueBinding<bool>(kGroup, "CurveKeepEnds", true));

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
            AddBinding(new TriggerBinding<float>(kGroup, "SetSlopeEase", v => Save(s => s.SlopeEase = v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetSlopeArch", v => Save(s => s.SlopeArch = v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetSlopeStartOffset", v => m_SlopeToolSystem.StartOffset = v));
            AddBinding(new TriggerBinding<float>(kGroup, "SetSlopeEndOffset", v => m_SlopeToolSystem.EndOffset = v));
            AddBinding(new TriggerBinding<int>(kGroup, "SetCurveMode", v => Save(s => s.CurveMode = (CurveMode)v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetCurveStrength", v => Save(s => s.CurveStrength = v)));
            AddBinding(new TriggerBinding<bool>(kGroup, "SetCurveKeepEnds", v => Save(s => s.CurveKeepEnds = v)));
            AddBinding(new TriggerBinding(kGroup, "ApplyPreview", () => m_ToolActivationSystem.PreviewTool?.RequestApply()));
            AddBinding(new TriggerBinding(kGroup, "CancelPreview", () => m_ToolActivationSystem.PreviewTool?.RequestCancel()));
            AddBinding(new TriggerBinding<int>(kGroup, "RotateConnect", steps => m_ToolActivationSystem.ConnectTool.RequestRotate(steps)));
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

            var preview = m_ToolActivationSystem.PreviewTool;
            m_Phase.Update(preview?.Phase.ToString() ?? string.Empty);
            m_Summary.Update(preview?.Summary ?? string.Empty);
            m_ConnectRotation.Update(m_ToolActivationSystem.ConnectTool.RotationDegrees);
            m_SlopeEase.Update(settings.SlopeEase);
            m_SlopeArch.Update(settings.SlopeArch);
            m_SlopeStartOffset.Update(m_SlopeToolSystem.StartOffset);
            m_SlopeEndOffset.Update(m_SlopeToolSystem.EndOffset);
            m_CurveMode.Update((int)settings.CurveMode);
            m_CurveStrength.Update(settings.CurveStrength);
            m_CurveKeepEnds.Update(settings.CurveKeepEnds);
        }

        private static void Save(Action<Setting> change)
        {
            change(Mod.Settings);
            Mod.Settings.ApplyAndSave();
        }
    }
}
