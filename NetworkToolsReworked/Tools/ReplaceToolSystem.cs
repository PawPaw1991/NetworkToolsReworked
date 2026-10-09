using System.Collections.Generic;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using NetworkToolsReworked.Edits;
using Unity.Entities;

namespace NetworkToolsReworked.Tools
{
    /// <summary>
    /// Click a road to copy its type, then pick two nodes: the road between them becomes that type,
    /// keeping its shape, height and (optionally) its upgrades. See <see cref="RestyleEdit"/>.
    /// </summary>
    public partial class ReplaceToolSystem : PathToolSystem
    {
        private TerrainSystem m_TerrainSystem;
        private Entity m_Prefab;
        private int m_Changed;
        private bool m_Mismatch;
        private bool m_AnyMismatch;

        public override string toolID => "NetworkToolsReworked.ReplaceTool";

        protected override bool UsesSource => true;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
        }

        protected override bool TakeSource(Entity edge, out string problem)
        {
            problem = string.Empty;
            m_Prefab = EntityManager.GetComponentData<PrefabRef>(edge).m_Prefab;
            SourceLabel = PrefabName(m_Prefab);
            return true;
        }

        protected override bool SourceIsValid() => EntityManager.Exists(m_Prefab) && !EntityManager.HasComponent<Deleted>(m_Prefab);

        protected override string DescribeSource(Entity edge) => $"Click to use {SourceLabel} as the new road type.";

        protected override bool EmitPath(EntityCommandBuffer ecb, List<Entity> nodes, List<Entity> edges, int randomSeed)
        {
            var kind = RestyleEdit.KindOf(EntityManager, m_Prefab);
            m_Mismatch = false;
            foreach (var edge in edges)
                if (RestyleEdit.KindOf(EntityManager, EntityManager.GetComponentData<PrefabRef>(edge).m_Prefab) != kind)
                {
                    m_Mismatch = m_AnyMismatch = true;
                    return false;
                }

            var terrain = m_TerrainSystem.GetHeightData();
            Upgraded? upgrades = Mod.Settings.ReplaceKeepUpgrades ? (Upgraded?)null : default(Upgraded);
            m_Changed += RestyleEdit.Emit(EntityManager, ecb, ref terrain, edges, m_Prefab, upgrades, randomSeed);
            return m_Changed > 0;
        }

        protected override void OnBeforeEmit()
        {
            m_Changed = 0;
            m_AnyMismatch = false;
        }

        protected override string Describe(List<Entity> nodes, List<Entity> edges)
        {
            var upgrades = Mod.Settings.ReplaceKeepUpgrades ? "keeping upgrades" : "without upgrades";
            var skipped = m_AnyMismatch ? ". Stretches of a different kind of network are left out" : "";
            return $"{PathInfo.Describe(EntityManager, nodes, edges)}: {m_Changed} becoming {SourceLabel}, {upgrades}{skipped}";
        }

        protected override void DrawPath(ToolOverlay overlay, List<Entity> nodes, List<Entity> edges, bool locked)
        {
            if (m_Mismatch)
            {
                foreach (var edge in edges)
                    overlay.Edge(edge, ToolOverlay.Invalid);
                return;
            }
            base.DrawPath(overlay, nodes, edges, locked);
        }

        protected override string NothingToChange => m_AnyMismatch
            ? $"Part of this stretch is a different kind of network (road, track or path) from {SourceLabel}."
            : $"This stretch is already {SourceLabel}.";

        private string PrefabName(Entity prefab)
        {
            var asset = m_PrefabSystem.GetPrefab<PrefabBase>(prefab);
            return asset != null ? asset.name : "the copied type";
        }
    }
}
