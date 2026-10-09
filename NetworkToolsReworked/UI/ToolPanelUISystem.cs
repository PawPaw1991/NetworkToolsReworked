using System;
using Colossal.UI.Binding;
using Game.UI;
using NetworkToolsReworked.Edits;
using NetworkToolsReworked.Presets;
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
        private ValueBinding<bool> m_RampRight;
        private ValueBinding<bool> m_RampEntry;
        private ValueBinding<bool> m_RampFlip;
        private ValueBinding<float> m_RampAngle;
        private ValueBinding<float> m_RampTurn;
        private ValueBinding<float> m_RampLength;
        private ValueBinding<float> m_RampHeight;
        private ValueBinding<string> m_RampType;
        private ValueBinding<float> m_MatchHeight;
        private ValueBinding<bool> m_PresetsAvailable;
        private ValueBinding<string> m_PresetNames;
        private ValueBinding<bool> m_SelectionAvailable;
        private ValueBinding<float> m_FilletRadius;
        private ValueBinding<int> m_ParallelTaper;
        private ValueBinding<string> m_UndoHistory;
        private int m_UndoVersion = -1;
        private ValueBinding<float> m_HelixRadius;
        private ValueBinding<float> m_HelixTurns;
        private ValueBinding<float> m_HelixClimb;
        private ValueBinding<bool> m_HelixClockwise;
        private ValueBinding<float> m_HelixStartAngle;
        private ValueBinding<float> m_HelixStartHeight;
        private ValueBinding<string> m_HelixType;
        private ValueBinding<int> m_SplitMode;
        private ValueBinding<float> m_SplitParts;
        private ValueBinding<float> m_SplitSpacing;
        private ValueBinding<float> m_SimplifyTolerance;
        private ValueBinding<int> m_BridgeMode;
        private ValueBinding<float> m_BridgeHeight;
        private ValueBinding<float> m_BridgeClearance;
        private ValueBinding<string> m_HealthIssues;
        private ValueBinding<int> m_HealthSelected;
        private ValueBinding<float> m_HealthMinLength;
        private int m_HealthVersion = -1;
        private ValueBinding<bool> m_UsingSelection;
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
            AddBinding(m_RampRight = new ValueBinding<bool>(kGroup, "RampRight", true));
            AddBinding(m_RampEntry = new ValueBinding<bool>(kGroup, "RampEntry", false));
            AddBinding(m_RampFlip = new ValueBinding<bool>(kGroup, "RampFlip", false));
            AddBinding(m_RampAngle = new ValueBinding<float>(kGroup, "RampAngle", 15f));
            AddBinding(m_RampTurn = new ValueBinding<float>(kGroup, "RampTurn", 0f));
            AddBinding(m_RampLength = new ValueBinding<float>(kGroup, "RampLength", 120f));
            AddBinding(m_RampHeight = new ValueBinding<float>(kGroup, "RampHeight", 6f));
            AddBinding(m_RampType = new ValueBinding<string>(kGroup, "RampType", string.Empty));
            AddBinding(m_MatchHeight = new ValueBinding<float>(kGroup, "MatchHeight", 0f));
            AddBinding(m_PresetsAvailable = new ValueBinding<bool>(kGroup, "PresetsAvailable", false));
            AddBinding(m_PresetNames = new ValueBinding<string>(kGroup, "PresetNames", string.Empty));
            AddBinding(m_SelectionAvailable = new ValueBinding<bool>(kGroup, "SelectionAvailable", false));
            AddBinding(m_UsingSelection = new ValueBinding<bool>(kGroup, "UsingSelection", false));
            AddBinding(m_HealthIssues = new ValueBinding<string>(kGroup, "HealthIssues", string.Empty));
            AddBinding(m_HealthSelected = new ValueBinding<int>(kGroup, "HealthSelected", -1));
            AddBinding(m_HealthMinLength = new ValueBinding<float>(kGroup, "HealthMinLength", 3f));
            AddBinding(new TriggerBinding<int>(kGroup, "SelectIssue", i => m_ToolActivationSystem.HealthTool.RequestSelect(i)));
            AddBinding(new TriggerBinding(kGroup, "RescanHealth", () => m_ToolActivationSystem.HealthTool.RequestRescan()));
            AddBinding(new TriggerBinding<float>(kGroup, "SetHealthMinLength", v =>
            {
                Save(s => s.HealthMinLength = v);
                m_ToolActivationSystem.HealthTool.RequestRescan();
            }));
            AddBinding(m_HelixRadius = new ValueBinding<float>(kGroup, "HelixRadius", 30f));
            AddBinding(m_HelixTurns = new ValueBinding<float>(kGroup, "HelixTurns", 1f));
            AddBinding(m_HelixClimb = new ValueBinding<float>(kGroup, "HelixClimb", 8f));
            AddBinding(m_HelixClockwise = new ValueBinding<bool>(kGroup, "HelixClockwise", false));
            AddBinding(m_HelixStartAngle = new ValueBinding<float>(kGroup, "HelixStartAngle", 0f));
            AddBinding(m_HelixStartHeight = new ValueBinding<float>(kGroup, "HelixStartHeight", 0f));
            AddBinding(m_HelixType = new ValueBinding<string>(kGroup, "HelixType", string.Empty));
            AddBinding(new TriggerBinding<float>(kGroup, "SetHelixRadius", v => Save(s => s.HelixRadius = v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetHelixTurns", v => Save(s => s.HelixTurns = v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetHelixClimb", v => Save(s => s.HelixClimb = v)));
            AddBinding(new TriggerBinding<bool>(kGroup, "SetHelixClockwise", v => Save(s => s.HelixClockwise = v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetHelixStartAngle", v => Save(s => s.HelixStartAngle = v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetHelixStartHeight", v => Save(s => s.HelixStartHeight = v)));
            AddBinding(new TriggerBinding(kGroup, "PickHelixType", () => m_ToolActivationSystem.HelixTool.RequestPickType()));
            AddBinding(new TriggerBinding(kGroup, "HelixUseRoadType", () => m_ToolActivationSystem.HelixTool.UseRoadType()));
            AddBinding(m_UndoHistory = new ValueBinding<string>(kGroup, "UndoHistory", string.Empty));
            AddBinding(new TriggerBinding<int>(kGroup, "RollBack", steps =>
            {
                m_ToolActivationSystem.UndoTool.RollBack(steps);
                if (m_ToolActivationSystem.Current != ToolId.Undo)
                    m_ToolActivationSystem.Activate(ToolId.Undo);
            }));
            AddBinding(m_ParallelTaper = new ValueBinding<int>(kGroup, "ParallelTaper", 0));
            AddBinding(new TriggerBinding<int>(kGroup, "SetParallelTaper", v => Save(s => s.ParallelTaper = (ParallelTaper)v)));
            AddBinding(m_SplitMode = new ValueBinding<int>(kGroup, "SplitMode", 0));
            AddBinding(m_SplitParts = new ValueBinding<float>(kGroup, "SplitParts", 2f));
            AddBinding(m_SplitSpacing = new ValueBinding<float>(kGroup, "SplitSpacing", 48f));
            AddBinding(m_SimplifyTolerance = new ValueBinding<float>(kGroup, "SimplifyTolerance", 0.5f));
            AddBinding(new TriggerBinding<int>(kGroup, "SetSplitMode", v => Save(s => s.SplitMode = (SplitMode)v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetSplitParts", v => Save(s => s.SplitParts = v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetSplitSpacing", v => Save(s => s.SplitSpacing = v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetSimplifyTolerance", v => Save(s => s.SimplifyTolerance = v)));
            AddBinding(m_FilletRadius = new ValueBinding<float>(kGroup, "FilletRadius", 40f));
            AddBinding(new TriggerBinding<float>(kGroup, "SetFilletRadius", v => Save(s => s.FilletRadius = v)));
            AddBinding(m_BridgeMode = new ValueBinding<int>(kGroup, "BridgeMode", 0));
            AddBinding(m_BridgeHeight = new ValueBinding<float>(kGroup, "BridgeHeight", 10f));
            AddBinding(m_BridgeClearance = new ValueBinding<float>(kGroup, "BridgeClearance", 8f));
            AddBinding(new TriggerBinding<int>(kGroup, "SetBridgeMode", v => Save(s => s.BridgeMode = (LiftMode)v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetBridgeHeight", v => Save(s => s.BridgeHeight = v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetBridgeClearance", v => Save(s => s.BridgeClearance = v)));
            AddBinding(new TriggerBinding(kGroup, "UseSelection", () => m_ToolActivationSystem.SourceTool?.RequestUseSelection()));

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
            AddBinding(new TriggerBinding<bool>(kGroup, "SetRampRight", v => Save(s => s.RampRight = v)));
            AddBinding(new TriggerBinding<bool>(kGroup, "SetRampEntry", v => Save(s => s.RampEntry = v)));
            AddBinding(new TriggerBinding<bool>(kGroup, "SetRampFlip", v => Save(s => s.RampFlip = v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetRampAngle", v => Save(s => s.RampAngle = v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetRampTurn", v => Save(s => s.RampTurn = v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetRampLength", v => Save(s => s.RampLength = v)));
            AddBinding(new TriggerBinding<float>(kGroup, "SetRampHeight", v => Save(s => s.RampHeight = v)));
            AddBinding(new TriggerBinding(kGroup, "PickRampType", () => m_ToolActivationSystem.RampTool.RequestPickType()));
            AddBinding(new TriggerBinding(kGroup, "UseRoadType", () => m_ToolActivationSystem.RampTool.UseRoadType()));
            AddBinding(new TriggerBinding<float>(kGroup, "SetMatchHeight", v => m_ToolActivationSystem.MatchHeightTool.SetTargetHeight(v)));
            AddBinding(new TriggerBinding(kGroup, "SavePreset", () => PresetStore.SaveCurrent(m_ToolActivationSystem.Current)));
            AddBinding(new TriggerBinding<int>(kGroup, "LoadPreset", i => PresetStore.Load(m_ToolActivationSystem.Current, i)));
            AddBinding(new TriggerBinding<int>(kGroup, "DeletePreset", i => PresetStore.Delete(m_ToolActivationSystem.Current, i)));
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
            m_RampRight.Update(settings.RampRight);
            m_RampEntry.Update(settings.RampEntry);
            m_RampFlip.Update(settings.RampFlip);
            m_RampAngle.Update(settings.RampAngle);
            m_RampTurn.Update(settings.RampTurn);
            m_RampLength.Update(settings.RampLength);
            m_RampHeight.Update(settings.RampHeight);
            m_RampType.Update(m_ToolActivationSystem.RampTool.RampTypeName);
            m_MatchHeight.Update(m_ToolActivationSystem.MatchHeightTool.TargetHeight);
            var pathTool = m_ToolActivationSystem.SourceTool;
            m_SelectionAvailable.Update(pathTool != null && m_PanelOpen.value && MoveItSelection.Available);
            m_UsingSelection.Update(pathTool != null && pathTool.UsingSelection);
            m_FilletRadius.Update(settings.FilletRadius);
            m_ParallelTaper.Update((int)settings.ParallelTaper);
            m_HelixRadius.Update(settings.HelixRadius);
            m_HelixTurns.Update(settings.HelixTurns);
            m_HelixClimb.Update(settings.HelixClimb);
            m_HelixClockwise.Update(settings.HelixClockwise);
            m_HelixStartAngle.Update(settings.HelixStartAngle);
            m_HelixStartHeight.Update(settings.HelixStartHeight);
            m_HelixType.Update(m_ToolActivationSystem.HelixTool.TypeName);
            m_SplitMode.Update((int)settings.SplitMode);
            m_SplitParts.Update(settings.SplitParts);
            m_SplitSpacing.Update(settings.SplitSpacing);
            m_SimplifyTolerance.Update(settings.SimplifyTolerance);
            m_BridgeMode.Update((int)settings.BridgeMode);
            m_BridgeHeight.Update(settings.BridgeHeight);
            m_BridgeClearance.Update(settings.BridgeClearance);
            UpdateHealth();
            UpdateUndoHistory();
            UpdatePresets();
        }

        /// <summary>The edits that can be undone, newest first, one per line.</summary>
        private void UpdateUndoHistory()
        {
            if (Undo.UndoHistory.Version == m_UndoVersion)
                return;
            m_UndoVersion = Undo.UndoHistory.Version;
            var steps = Undo.UndoHistory.Steps;
            var lines = new System.Collections.Generic.List<string>();
            for (var i = steps.Count - 1; i >= 0; i--)
                lines.Add(steps[i].Label ?? Undo.UndoRecorder.ToolName(steps[i].Tool));
            m_UndoHistory.Update(string.Join("\n", lines));
        }

        /// <summary>One line per issue: "fix" or "info", the kind, then the description, tab separated.</summary>
        private void UpdateHealth()
        {
            var health = m_ToolActivationSystem.HealthTool;
            m_HealthSelected.Update(health.Selected);
            m_HealthMinLength.Update(Mod.Settings.HealthMinLength);
            if (health.Version == m_HealthVersion)
                return;
            m_HealthVersion = health.Version;
            var lines = new System.Collections.Generic.List<string>();
            foreach (var issue in health.Issues)
                lines.Add($"{(issue.CanFix ? "fix" : "info")}\t{HealthToolSystem.Name(issue.Kind)}\t{issue.Text}");
            m_HealthIssues.Update(string.Join("\n", lines));
        }

        private void UpdatePresets()
        {
            var tool = m_ToolActivationSystem.Current;
            var available = m_PanelOpen.value && PresetStore.Supports(tool);
            m_PresetsAvailable.Update(available);
            if (!available)
            {
                m_PresetNames.Update(string.Empty);
                return;
            }
            var names = new System.Collections.Generic.List<string>();
            foreach (var preset in PresetStore.For(tool))
                names.Add(preset.Name);
            m_PresetNames.Update(string.Join("\n", names));
        }

        private static void Save(Action<Setting> change)
        {
            change(Mod.Settings);
            Mod.Settings.ApplyAndSave();
        }
    }
}
