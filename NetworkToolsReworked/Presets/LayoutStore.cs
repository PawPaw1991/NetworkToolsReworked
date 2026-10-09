using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Colossal.Mathematics;
using Game.Net;
using Game.Prefabs;
using NetworkToolsReworked.Edits;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace NetworkToolsReworked.Presets
{
    /// <summary>
    /// Saved road layouts, shared between cities. Each layout is stored with road types by name, so it
    /// can be placed in any city that has those road types (asset roads included, if installed).
    /// File format, one line each: "L name dirX,dirZ" starts a layout, then one "R type name upgrades
    /// elevated points" line per road, tab separated, with the 12 control point numbers comma separated.
    /// </summary>
    public static class LayoutStore
    {
        public const int kMaxLayouts = 40;

        private static List<Layout> s_Layouts;

        private static string FilePath => Path.Combine(Application.persistentDataPath, "ModsSettings", nameof(NetworkToolsReworked), "layouts.txt");

        /// <summary>Bumped when the list changes, so the panel knows when to refresh.</summary>
        public static int Version { get; private set; }

        public static IReadOnlyList<Layout> All => Load();

        public static void Save(Layout layout)
        {
            var all = Load();
            all.RemoveAll(l => l.Name == layout.Name);
            all.Add(layout);
            if (all.Count > kMaxLayouts)
                all.RemoveAt(0);
            Write();
        }

        public static void Delete(int index)
        {
            var all = Load();
            if (index < 0 || index >= all.Count)
                return;
            all.RemoveAt(index);
            Write();
        }

        /// <summary>Finds the road types of a saved layout in the loaded game. Returns how many are missing.</summary>
        public static int Resolve(Layout layout, PrefabSystem prefabs)
        {
            var missing = 0;
            for (var i = 0; i < layout.Roads.Count; i++)
            {
                var road = layout.Roads[i];
                road.Prefab = Entity.Null;
                if (prefabs.TryGetPrefab(new PrefabID(road.PrefabType, road.PrefabName), out PrefabBase asset))
                    road.Prefab = prefabs.GetEntity(asset);
                if (road.Prefab == Entity.Null)
                    missing++;
                layout.Roads[i] = road;
            }
            return missing;
        }

        private static List<Layout> Load()
        {
            if (s_Layouts != null)
                return s_Layouts;
            s_Layouts = new List<Layout>();
            try
            {
                if (!File.Exists(FilePath))
                    return s_Layouts;
                Layout current = null;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var parts = line.Split('\t');
                    if (parts.Length == 3 && parts[0] == "L")
                    {
                        current = new Layout { Name = parts[1] };
                        var d = Floats(parts[2]);
                        if (d.Length == 2)
                            current.Direction = math.normalizesafe(new float3(d[0], 0f, d[1]), new float3(1f, 0f, 0f));
                        s_Layouts.Add(current);
                    }
                    else if (parts.Length == 7 && parts[0] == "R" && current != null)
                    {
                        var flags = Flags(parts[3]);
                        var p = Floats(parts[6]);
                        if (flags.Length != 3 || p.Length != 12)
                            continue;
                        var road = new LayoutRoad
                        {
                            PrefabType = parts[1],
                            PrefabName = parts[2],
                            Elevated = parts[4] == "1",
                            HasUpgrades = parts[5] == "1",
                            Curve = new Bezier4x3(new float3(p[0], p[1], p[2]), new float3(p[3], p[4], p[5]), new float3(p[6], p[7], p[8]), new float3(p[9], p[10], p[11])),
                        };
                        SetFlags(out road.Upgrades.m_Flags.m_General, flags[0]);
                        SetFlags(out road.Upgrades.m_Flags.m_Left, flags[1]);
                        SetFlags(out road.Upgrades.m_Flags.m_Right, flags[2]);
                        current.Roads.Add(road);
                    }
                }
            }
            catch (Exception e)
            {
                Mod.Log.Warn($"Couldn't read layouts from {FilePath}: {e.Message}");
            }
            return s_Layouts;
        }

        private static void Write()
        {
            Version++;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var c = CultureInfo.InvariantCulture;
                var text = new StringBuilder();
                foreach (var layout in Load())
                {
                    text.Append("L\t").Append(Clean(layout.Name)).Append('\t')
                        .Append(layout.Direction.x.ToString("R", c)).Append(',').Append(layout.Direction.z.ToString("R", c)).Append('\n');
                    foreach (var road in layout.Roads)
                    {
                        var f = road.Upgrades.m_Flags;
                        var b = road.Curve;
                        text.Append("R\t").Append(Clean(road.PrefabType)).Append('\t').Append(Clean(road.PrefabName)).Append('\t')
                            .Append(Convert.ToUInt32(f.m_General, c).ToString(c)).Append(',').Append(Convert.ToUInt32(f.m_Left, c).ToString(c)).Append(',').Append(Convert.ToUInt32(f.m_Right, c).ToString(c)).Append('\t')
                            .Append(road.Elevated ? '1' : '0').Append('\t')
                            .Append(road.HasUpgrades ? '1' : '0').Append('\t');
                        var points = new[] { b.a, b.b, b.c, b.d };
                        for (var k = 0; k < 4; k++)
                        {
                            if (k > 0) text.Append(',');
                            text.Append(points[k].x.ToString("R", c)).Append(',').Append(points[k].y.ToString("R", c)).Append(',').Append(points[k].z.ToString("R", c));
                        }
                        text.Append('\n');
                    }
                }
                File.WriteAllText(FilePath, text.ToString());
            }
            catch (Exception e)
            {
                Mod.Log.Warn($"Couldn't save layouts to {FilePath}: {e.Message}");
            }
        }

        private static float[] Floats(string text)
        {
            var parts = text.Split(',');
            var values = new float[parts.Length];
            for (var i = 0; i < parts.Length; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                    return Array.Empty<float>();
            return values;
        }

        private static void SetFlags<T>(out T field, uint value) where T : struct, Enum
        {
            field = (T)Enum.ToObject(typeof(T), value);
        }

        /// <summary>Upgrade flags are whole numbers that can use all 32 bits, so they aren't read as floats.</summary>
        private static uint[] Flags(string text)
        {
            var parts = text.Split(',');
            var values = new uint[parts.Length];
            for (var i = 0; i < parts.Length; i++)
                if (!uint.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i]))
                    return Array.Empty<uint>();
            return values;
        }

        private static string Clean(string text) => (text ?? string.Empty).Replace('\t', ' ').Replace('\n', ' ');
    }
}
