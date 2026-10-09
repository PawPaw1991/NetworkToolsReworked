using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NetworkToolsReworked.Edits;
using NetworkToolsReworked.Tools;
using UnityEngine;

namespace NetworkToolsReworked.Presets
{
    /// <summary>A saved set of one tool's options.</summary>
    public sealed class Preset
    {
        public ToolId Tool;
        public string Name;
        public readonly Dictionary<string, float> Values = new Dictionary<string, float>();
    }

    /// <summary>
    /// Named option sets per tool, saved in their own small text file next to the mod's settings so they
    /// survive settings resets. Each preset stores the tool's panel options by setting name.
    /// </summary>
    public static class PresetStore
    {
        public const int kMaxPerTool = 12;

        /// <summary>The settings each tool's presets capture.</summary>
        private static readonly Dictionary<ToolId, string[]> s_Keys = new Dictionary<ToolId, string[]>
        {
            [ToolId.Slope] = new[] { nameof(Setting.SlopeProfile), nameof(Setting.SlopeEase), nameof(Setting.SlopeArch), nameof(Setting.CurveMode), nameof(Setting.CurveStrength), nameof(Setting.CurveKeepEnds) },
            [ToolId.Smooth] = new[] { nameof(Setting.SmoothStrength), nameof(Setting.SmoothKeepEnds), nameof(Setting.SmoothGrades), nameof(Setting.SmoothRelax) },
            [ToolId.Arrange] = new[] { nameof(Setting.ArrangeMode), nameof(Setting.ArrangeBulge) },
            [ToolId.Roundabout] = new[] { nameof(Setting.RoundaboutCustomRing), nameof(Setting.RoundaboutRadius), nameof(Setting.RoundaboutClockwise) },
            [ToolId.Parallel] = new[] { nameof(Setting.ParallelSpacing), nameof(Setting.ParallelOffset), nameof(Setting.ParallelGap), nameof(Setting.ParallelWidths), nameof(Setting.ParallelBothSides), nameof(Setting.ParallelHeight), nameof(Setting.ParallelReverse), nameof(Setting.ParallelTaper) },
            [ToolId.Ramp] = new[] { nameof(Setting.RampEntry), nameof(Setting.RampRight), nameof(Setting.RampFlip), nameof(Setting.RampAngle), nameof(Setting.RampTurn), nameof(Setting.RampLength), nameof(Setting.RampHeight) },
            [ToolId.MoveNode] = new[] { nameof(Setting.MoveSnap), nameof(Setting.MoveGridSize) },
            [ToolId.Connect] = new[] { nameof(Setting.ConnectMode) },
            [ToolId.Helix] = new[] { nameof(Setting.HelixRadius), nameof(Setting.HelixTurns), nameof(Setting.HelixClimb), nameof(Setting.HelixClockwise), nameof(Setting.HelixStartAngle), nameof(Setting.HelixStartHeight) },
            [ToolId.Split] = new[] { nameof(Setting.SplitMode), nameof(Setting.SplitParts), nameof(Setting.SplitSpacing), nameof(Setting.SimplifyTolerance) },
            [ToolId.Fillet] = new[] { nameof(Setting.FilletRadius) },
            [ToolId.Bridge] = new[] { nameof(Setting.BridgeMode), nameof(Setting.BridgeHeight), nameof(Setting.BridgeClearance) },
        };

        private static List<Preset> s_Presets;

        private static string FilePath => Path.Combine(Application.persistentDataPath, "ModsSettings", nameof(NetworkToolsReworked), "presets.txt");

        public static bool Supports(ToolId tool) => s_Keys.ContainsKey(tool);

        public static List<Preset> For(ToolId tool)
        {
            var list = new List<Preset>();
            foreach (var preset in All())
                if (preset.Tool == tool)
                    list.Add(preset);
            return list;
        }

        /// <summary>Saves the tool's current options as a preset named after them; an identical one is replaced.</summary>
        public static void SaveCurrent(ToolId tool)
        {
            if (!s_Keys.TryGetValue(tool, out var keys))
                return;
            var preset = new Preset { Tool = tool };
            foreach (var key in keys)
                preset.Values[key] = Read(key);
            preset.Name = Describe(tool);

            var all = All();
            all.RemoveAll(p => p.Tool == tool && p.Name == preset.Name);
            all.Add(preset);
            var mine = For(tool);
            if (mine.Count > kMaxPerTool)
                all.Remove(mine[0]);
            Write();
        }

        public static void Load(ToolId tool, int index)
        {
            var mine = For(tool);
            if (index < 0 || index >= mine.Count)
                return;
            foreach (var pair in mine[index].Values)
                Apply(pair.Key, pair.Value);
            Mod.Settings.ApplyAndSave();
        }

        public static void Delete(ToolId tool, int index)
        {
            var mine = For(tool);
            if (index < 0 || index >= mine.Count)
                return;
            All().Remove(mine[index]);
            Write();
        }

        /// <summary>Readable name from the current options, e.g. "Exit right, 15°, 120 m, +6 m".</summary>
        private static string Describe(ToolId tool)
        {
            var s = Mod.Settings;
            var c = CultureInfo.InvariantCulture;
            switch (tool)
            {
                case ToolId.Slope:
                    var slope = s.SlopeProfile == SlopeProfile.EaseInOut ? $"Ease {s.SlopeEase:0}%" : s.SlopeProfile.ToString();
                    var arch = s.SlopeArch != 0f ? $", arch {s.SlopeArch.ToString("+0.#;-0.#", c)} m" : "";
                    var curve = s.CurveMode != CurveMode.Keep ? $", {s.CurveMode} {s.CurveStrength:0}%" : "";
                    return slope + arch + curve;
                case ToolId.Smooth:
                    return $"Smooth {s.SmoothStrength:0}%{(s.SmoothGrades ? " + grades" : "")}{(s.SmoothRelax > 0f ? $", relax {s.SmoothRelax:0}%" : "")}{(s.SmoothKeepEnds ? "" : ", free ends")}";
                case ToolId.Arrange:
                    return s.ArrangeMode == ArrangeMode.Arc ? $"Arc, bulge {s.ArrangeBulge:0}%" : s.ArrangeMode.ToString();
                case ToolId.Roundabout:
                    return s.RoundaboutCustomRing ? $"Custom ring, radius {s.RoundaboutRadius:0} m, {(s.RoundaboutClockwise ? "clockwise" : "anticlockwise")}" : "Game roundabout";
                case ToolId.Parallel:
                    var spacing = s.ParallelSpacing == ParallelSpacing.Touching ? $"touching + {s.ParallelGap.ToString("0.#", c)} m"
                        : s.ParallelSpacing == ParallelSpacing.Widths ? $"{s.ParallelWidths:0} widths" : $"{Math.Abs(s.ParallelOffset).ToString("0.#", c)} m";
                    var side = s.ParallelBothSides ? "both sides" : s.ParallelOffset >= 0f ? "right" : "left";
                    var height = s.ParallelHeight != 0f ? $", {s.ParallelHeight.ToString("+0.#;-0.#", c)} m high" : "";
                    var taper = s.ParallelTaper == ParallelTaper.Split ? ", split" : s.ParallelTaper == ParallelTaper.Merge ? ", merge" : "";
                    return $"{spacing} {side}{height}{(s.ParallelReverse ? ", opposite" : "")}{taper}";
                case ToolId.Ramp:
                    var turn = s.RampTurn != 0f ? $", turn {s.RampTurn:0}°" : "";
                    return $"{(s.RampEntry ? "Entry" : "Exit")} {(s.RampRight ? "right" : "left")}, {s.RampAngle:0}°, {s.RampLength:0} m, {s.RampHeight.ToString("+0.#;-0.#;0", c)} m{turn}";
                case ToolId.MoveNode:
                    return s.MoveSnap == MoveSnap.Grid ? $"Grid {s.MoveGridSize.ToString("0.#", c)} m" : s.MoveSnap == MoveSnap.Angle ? "15° steps" : "No snap";
                case ToolId.Connect:
                    return s.ConnectMode == ConnectMode.SmoothBothEnds ? "Smooth both ends" : "Simple curve";
                case ToolId.Helix:
                    return $"Radius {s.HelixRadius:0} m, {s.HelixTurns.ToString("0.##", c)} turns, {s.HelixClimb.ToString("+0.#;-0.#;0", c)} m per turn, {(s.HelixClockwise ? "clockwise" : "anticlockwise")}";
                case ToolId.Split:
                    return s.SplitMode == SplitMode.EqualParts ? $"{s.SplitParts:0} equal parts"
                        : s.SplitMode == SplitMode.EveryDistance ? $"Every {s.SplitSpacing:0} m" : $"Simplify, {s.SimplifyTolerance.ToString("0.0#", c)} m";
                case ToolId.Fillet:
                    return $"Radius {s.FilletRadius:0} m";
                case ToolId.Bridge:
                    return s.BridgeMode == LiftMode.OverCrossings ? $"Over crossings, {s.BridgeClearance.ToString("0.#", c)} m clear"
                        : s.BridgeMode == LiftMode.Raise ? $"Raise {s.BridgeHeight.ToString("0.#", c)} m" : $"Lower {s.BridgeHeight.ToString("0.#", c)} m";
                default:
                    return tool.ToString();
            }
        }

        private static float Read(string key)
        {
            var property = typeof(Setting).GetProperty(key);
            var value = property?.GetValue(Mod.Settings);
            switch (value)
            {
                case bool b:
                    return b ? 1f : 0f;
                case float f:
                    return f;
                case Enum e:
                    return Convert.ToInt32(e, CultureInfo.InvariantCulture);
                default:
                    return 0f;
            }
        }

        private static void Apply(string key, float value)
        {
            var property = typeof(Setting).GetProperty(key);
            if (property == null || !property.CanWrite)
                return;
            var type = property.PropertyType;
            if (type == typeof(bool))
                property.SetValue(Mod.Settings, value > 0.5f);
            else if (type == typeof(float))
                property.SetValue(Mod.Settings, value);
            else if (type.IsEnum)
                property.SetValue(Mod.Settings, Enum.ToObject(type, (int)Math.Round(value)));
        }

        private static List<Preset> All()
        {
            if (s_Presets != null)
                return s_Presets;
            s_Presets = new List<Preset>();
            try
            {
                if (!File.Exists(FilePath))
                    return s_Presets;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    // tool <tab> name <tab> key=value;key=value
                    var parts = line.Split('\t');
                    if (parts.Length != 3 || !Enum.TryParse(parts[0], out ToolId tool))
                        continue;
                    var preset = new Preset { Tool = tool, Name = parts[1] };
                    foreach (var pair in parts[2].Split(';'))
                    {
                        var kv = pair.Split('=');
                        if (kv.Length == 2 && float.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                            preset.Values[kv[0]] = v;
                    }
                    s_Presets.Add(preset);
                }
            }
            catch (Exception e)
            {
                Mod.Log.Warn($"Couldn't read presets from {FilePath}: {e.Message}");
            }
            return s_Presets;
        }

        private static void Write()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var text = new StringBuilder();
                foreach (var preset in All())
                {
                    var values = new List<string>();
                    foreach (var pair in preset.Values)
                        values.Add(pair.Key + "=" + pair.Value.ToString("R", CultureInfo.InvariantCulture));
                    text.Append(preset.Tool).Append('\t').Append(preset.Name.Replace('\t', ' ')).Append('\t').Append(string.Join(";", values)).Append('\n');
                }
                File.WriteAllText(FilePath, text.ToString());
            }
            catch (Exception e)
            {
                Mod.Log.Warn($"Couldn't save presets to {FilePath}: {e.Message}");
            }
        }
    }
}
