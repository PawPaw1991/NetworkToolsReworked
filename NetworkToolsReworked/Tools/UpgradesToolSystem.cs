using System.Collections.Generic;
using Colossal.Entities;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using NetworkToolsReworked.Edits;
using Unity.Entities;

namespace NetworkToolsReworked.Tools
{
    /// <summary>
    /// Click a road to copy its upgrades (trees, sidewalks, sound walls, lighting...), then pick two
    /// nodes: the road between them gets the same upgrades. Copying a road without upgrades clears
    /// them. See <see cref="RestyleEdit"/>.
    /// </summary>
    public partial class UpgradesToolSystem : PathToolSystem
    {
        private TerrainSystem m_TerrainSystem;
        private Upgraded m_Upgrades;
        private int m_Changed;

        public override string toolID => "NetworkToolsReworked.UpgradesTool";

        protected override bool UsesSource => true;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
        }

        protected override bool TakeSource(Entity edge, out string problem)
        {
            problem = string.Empty;
            m_Upgrades = EntityManager.TryGetComponent(edge, out Upgraded upgraded) ? upgraded : default;
            SourceLabel = Label(m_Upgrades.m_Flags);
            return true;
        }

        protected override string DescribeSource(Entity edge) =>
            m_Upgrades.m_Flags == default ? "This road has no upgrades: click to clear upgrades on other roads." : $"Click to copy: {SourceLabel}.";

        protected override bool EmitPath(EntityCommandBuffer ecb, List<Entity> nodes, List<Entity> edges, int randomSeed)
        {
            var upgrades = m_Upgrades;
            if (Mod.Settings.UpgradesSwapSides)
                (upgrades.m_Flags.m_Left, upgrades.m_Flags.m_Right) = (upgrades.m_Flags.m_Right, upgrades.m_Flags.m_Left);

            var terrain = m_TerrainSystem.GetHeightData();
            m_Changed += RestyleEdit.Emit(EntityManager, ecb, ref terrain, edges, Entity.Null, upgrades, randomSeed);
            return m_Changed > 0;
        }

        protected override void OnBeforeEmit() => m_Changed = 0;

        protected override string Describe(List<Entity> nodes, List<Entity> edges)
        {
            var swapped = Mod.Settings.UpgradesSwapSides ? ", sides swapped" : "";
            return $"{PathInfo.Describe(EntityManager, nodes, edges)}: {m_Changed} getting {SourceLabel}{swapped}";
        }

        protected override string NothingToChange => "This stretch already has these upgrades.";

        private static string Label(CompositionFlags flags)
        {
            if (flags == default)
                return "no upgrades";
            var parts = new List<string>();
            if (flags.m_General != 0) parts.Add(flags.m_General.ToString());
            if (flags.m_Left != 0) parts.Add($"left: {flags.m_Left}");
            if (flags.m_Right != 0) parts.Add($"right: {flags.m_Right}");
            return string.Join("; ", parts);
        }
    }
}
