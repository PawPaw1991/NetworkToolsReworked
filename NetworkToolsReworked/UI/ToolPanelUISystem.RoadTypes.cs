using System.Collections.Generic;
using System.Text;
using Colossal.UI.Binding;
using Game.Prefabs;
using NetworkToolsReworked.Edits;
using Unity.Collections;
using Unity.Entities;

namespace NetworkToolsReworked.UI
{
    /// <summary>
    /// The road type list in the panel: every road, track and path that appears in the game's build
    /// menus (asset roads included), for the shared <see cref="BuildType"/> choice.
    /// </summary>
    public partial class ToolPanelUISystem
    {
        private ValueBinding<string> m_RoadTypes;
        private ValueBinding<string> m_BuildType;
        private EntityQuery m_NetPrefabQuery;
        private PrefabSystem m_PrefabSystemForTypes;
        private readonly Dictionary<string, Entity> m_TypesByName = new Dictionary<string, Entity>();
        private int m_TypeCount = -1;
        private ValueBinding<string> m_RoundaboutIslands;
        private ValueBinding<string> m_RoundaboutIsland;
        private EntityQuery m_IslandQuery;
        private List<Islands.Island> m_Islands = new List<Islands.Island>();
        private int m_IslandCount = -1;

        private void CreateRoadTypeBindings()
        {
            m_PrefabSystemForTypes = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_NetPrefabQuery = GetEntityQuery(ComponentType.ReadOnly<PrefabData>(), ComponentType.ReadOnly<NetGeometryData>(), ComponentType.ReadOnly<PlaceableNetData>());

            // One line per type: kind, prefab name, icon path (tab separated).
            AddBinding(m_RoadTypes = new ValueBinding<string>(kGroup, "RoadTypes", string.Empty));
            AddBinding(m_BuildType = new ValueBinding<string>(kGroup, "BuildType", string.Empty));
            AddBinding(new TriggerBinding<string>(kGroup, "SetBuildType", SetBuildType));

            // Roundabout islands, one line each: prefab name, icon path (tab separated).
            m_IslandQuery = GetEntityQuery(Islands.QueryTypes);
            AddBinding(m_RoundaboutIslands = new ValueBinding<string>(kGroup, "RoundaboutIslands", string.Empty));
            AddBinding(m_RoundaboutIsland = new ValueBinding<string>(kGroup, "RoundaboutIsland", string.Empty));
            AddBinding(new TriggerBinding<string>(kGroup, "SetRoundaboutIsland", v => Save(s => s.RoundaboutIsland = v ?? string.Empty)));
        }

        private void UpdateRoadTypes()
        {
            m_BuildType.Update(BuildType.IsSet(EntityManager) ? BuildType.Name : string.Empty);

            // Rebuilt only while the panel is open and the set of network types has changed (asset loading).
            if (!m_PanelOpen.value)
                return;
            UpdateIslands();
            var count = m_NetPrefabQuery.CalculateEntityCount();
            if (count == m_TypeCount)
                return;
            m_TypeCount = count;

            m_TypesByName.Clear();
            var text = new StringBuilder();
            using (var entities = m_NetPrefabQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (var entity in entities)
                {
                    var kind = RestyleEdit.KindOf(EntityManager, entity);
                    if (kind == RestyleEdit.NetKind.Other)
                        continue;
                    var prefab = m_PrefabSystemForTypes.GetPrefab<PrefabBase>(entity);
                    // Only types offered in the build menus; the rest are internal pieces.
                    if (prefab == null || !prefab.TryGet(out UIObject ui) || m_TypesByName.ContainsKey(prefab.name))
                        continue;
                    m_TypesByName[prefab.name] = entity;
                    text.Append(kind).Append('\t').Append(Clean(prefab.name)).Append('\t').Append(Clean(ui.m_Icon)).Append('\n');
                }
            }
            m_RoadTypes.Update(text.ToString());
        }

        private void UpdateIslands()
        {
            var count = m_IslandQuery.CalculateEntityCount();
            if (count != m_IslandCount)
            {
                m_IslandCount = count;
                m_Islands = Islands.List(EntityManager, m_PrefabSystemForTypes, m_IslandQuery);
                var text = new StringBuilder();
                foreach (var island in m_Islands)
                    text.Append(Clean(island.Name)).Append('\t').Append(Clean(island.Icon)).Append('\n');
                m_RoundaboutIslands.Update(text.ToString());
            }
            // The island the tool will place (the saved choice, or the default small one).
            var chosen = Islands.Pick(m_Islands, Mod.Settings.RoundaboutIsland);
            var name = string.Empty;
            foreach (var island in m_Islands)
                if (island.Prefab == chosen)
                    name = island.Name;
            m_RoundaboutIsland.Update(name);
        }

        /// <summary>Empty name: build new roads as the road they start from.</summary>
        private void SetBuildType(string name)
        {
            if (!string.IsNullOrEmpty(name) && m_TypesByName.TryGetValue(name, out var prefab))
                BuildType.Set(prefab, name);
            else
                BuildType.Clear();
        }

        private static string Clean(string text) => (text ?? string.Empty).Replace('\t', ' ').Replace('\n', ' ');
    }
}
