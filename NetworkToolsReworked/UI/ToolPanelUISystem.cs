using System;
using Colossal.UI.Binding;
using Game.UI;
using NetworkToolsReworked.Edits;
using NetworkToolsReworked.Tools;
using Unity.Mathematics;

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
        private ValueBinding<float> m_SmoothStrength;
        private ValueBinding<bool> m_SmoothKeepEnds;
        private ValueBinding<bool> m_SmoothGrades;
        private ValueBinding<float> m_SmoothRelax;
        private ValueBinding<float> m_MoveNudgeX;
        private ValueBinding<float> m_MoveNudgeZ;
        private ValueBinding<float> m_MoveHeight;
        private ValueBinding<int> m_ArrangeMode;
        private ValueBinding<float> m_ArrangeBulge;
        private ValueBinding<float> m_RoundaboutRadius;
        private ValueBinding<bool> m_RoundaboutClockwise;
        private ValueBinding<int> m_MoveSnap;
        private ValueBinding<float> m_MoveGridSize;
        private ValueBinding<string> m_SourceName;
        private ValueBinding<bool> m_ReplaceKeepUpgrades;
        private ValueBinding<bool> m_UpgradesSwapSides;
        private ValueBinding<int> m_ParallelSpacing;
        private ValueBinding<float> m_ParallelGap;
        private ValueBinding<float> m_ParallelWidths;
        private ValueBinding<bool> m_ParallelBothSides;
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
            AddBinding(m_SmoothStrength = new ValueBinding<float>(kGroup, "SmoothStrength", 100f));
            AddBinding(m_SmoothKeepEnds = new ValueBinding<bool>(kGroup, "SmoothKeepEnds", true));
            AddBinding(m_SmoothGrades = new ValueBinding<bool>(kGroup, "SmoothGrades", true));
            AddBinding(m_SmoothRelax = new ValueBinding<float>(kGroup, "SmoothRelax", 0f));
            AddBinding(m_MoveNudgeX = new ValueBinding<float>(kGroup, "MoveNudgeX", 0f));
            AddBinding(m_MoveNudgeZ = new ValueBinding<float>(kGroup, "MoveNudgeZ", 0f));
            AddBinding(m_MoveHeight = new ValueBinding<float>(kGroup, "MoveHeight", 0f));
            AddBinding(m_ArrangeMode = new ValueBinding<int>(kGroup, "ArrangeMode", 0));
            AddBinding(m_ArrangeBulge = new ValueBinding<float>(kGroup, "ArrangeBulge", 100f));
            AddBinding(m_RoundaboutRadius = new ValueBinding<float>(kGroup, "RoundaboutRadius", 24f));
            AddBinding(m_RoundaboutClockwise = new ValueBinding<bool>(kGroup, "RoundaboutClockwise", false));
            AddBinding(m_MoveSnap = new ValueBinding<int>(kGroup, "MoveSnap", 0));
            AddBinding(m_MoveGridSize = new ValueBinding<float>(kGroup, "MoveGridSize", 8f));
            AddBinding(m_SourceName = new ValueBinding<string>(kGroup, "SourceName", string.Empty));
            AddBinding(m_ReplaceKeepUpgrades = new ValueBinding<bool>(kGroup, "ReplaceKeepUpgrades", true));
            AddBinding(m_UpgradesSwapSides = new ValueBinding<bool>(kGroup, "UpgradesSwapSides", false));
            AddBinding(m_ParallelSpacing = new ValueBinding<int>(kGroup, "ParallelSpacing", 0));
            AddBinding(m_ParallelGap = new ValueBinding<float>(kGroup, "ParallelGap", 0f));
            AddBinding(m_ParallelWidths = new ValueBinding<float>(kGroup, "ParallelWidths", 1f));
            AddBinding(m_ParallelBothSides = new ValueBinding<bool>(kGroup, "ParallelBothSides", false));

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
            AddBinding(new TriggerBinding<float>(kGroup, "SetSmoothStrength", v => Save(s => s.SmoothStrength = v)));
            AddBinding(new TriggerBinding<bool>(kGroup, "SetSmoothKeepEnds", v => Save(s => s.SmoothKeepEnds = v)));
            AddBinding(new TriggerBinding<bool>(kGroup, "SetSmoothGrades", v => Save(s => s.SmoothGrades = v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetSmoothRelax", v => Save(s => s.SmoothRelax = v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetMoveNudgeX", v => m_ToolActivationSystem.MoveNodeTool.Nudge = new float2(v, m_ToolActivationSystem.MoveNodeTool.Nudge.y)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetMoveNudgeZ", v => m_ToolActivationSystem.MoveNodeTool.Nudge = new float2(m_ToolActivationSystem.MoveNodeTool.Nudge.x, v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetMoveHeight", v => m_ToolActivationSystem.MoveNodeTool.HeightOffset = v));
            AddBinding(new TriggerBinding<int>(kGroup, "SetArrangeMode", v => Save(s => s.ArrangeMode = (ArrangeMode)v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetArrangeBulge", v => Save(s => s.ArrangeBulge = v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetRoundaboutRadius", v => Save(s => s.RoundaboutRadius = v)));
            AddBinding(new TriggerBinding<bool>(kGroup, "SetRoundaboutClockwise", v => Save(s => s.RoundaboutClockwise = v)));
            AddBinding(new TriggerBinding<int>(kGroup, "SetMoveSnap", v => Save(s => s.MoveSnap = (MoveSnap)v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetMoveGridSize", v => Save(s => s.MoveGridSize = v)));
            AddBinding(new TriggerBinding<bool>(kGroup, "SetReplaceKeepUpgrades", v => Save(s => s.ReplaceKeepUpgrades = v)));
            AddBinding(new TriggerBinding<bool>(kGroup, "SetUpgradesSwapSides", v => Save(s => s.UpgradesSwapSides = v)));
            AddBinding(new TriggerBinding<int>(kGroup, "SetParallelSpacing", v => Save(s => s.ParallelSpacing = (ParallelSpacing)v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetParallelGap", v => Save(s => s.ParallelGap = v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetParallelWidths", v => Save(s => s.ParallelWidths = v)));
            AddBinding(new TriggerBinding<bool>(kGroup, "SetParallelBothSides", v => Save(s => s.ParallelBothSides = v)));
            AddBinding(new TriggerBinding(kGroup, "PickSource", () => m_ToolActivationSystem.SourceTool?.RequestPickSource()));
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
            m_SmoothStrength.Update(settings.SmoothStrength);
            m_SmoothKeepEnds.Update(settings.SmoothKeepEnds);
            m_SmoothGrades.Update(settings.SmoothGrades);
            m_SmoothRelax.Update(settings.SmoothRelax);
            var move = m_ToolActivationSystem.MoveNodeTool;
            m_MoveNudgeX.Update(move.Nudge.x);
            m_MoveNudgeZ.Update(move.Nudge.y);
            m_MoveHeight.Update(move.HeightOffset);
            m_ArrangeMode.Update((int)settings.ArrangeMode);
            m_ArrangeBulge.Update(settings.ArrangeBulge);
            m_RoundaboutRadius.Update(settings.RoundaboutRadius);
            m_RoundaboutClockwise.Update(settings.RoundaboutClockwise);
            m_MoveSnap.Update((int)settings.MoveSnap);
            m_MoveGridSize.Update(settings.MoveGridSize);
            m_SourceName.Update(m_ToolActivationSystem.SourceTool?.SourceName ?? string.Empty);
            m_ReplaceKeepUpgrades.Update(settings.ReplaceKeepUpgrades);
            m_UpgradesSwapSides.Update(settings.UpgradesSwapSides);
            m_ParallelSpacing.Update((int)settings.ParallelSpacing);
            m_ParallelGap.Update(settings.ParallelGap);
            m_ParallelWidths.Update(settings.ParallelWidths);
            m_ParallelBothSides.Update(settings.ParallelBothSides);
        }

        private static void Save(Action<Setting> change)
        {
            change(Mod.Settings);
            Mod.Settings.ApplyAndSave();
        }
    }
}
