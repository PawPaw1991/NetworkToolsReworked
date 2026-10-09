using System.Collections.Generic;
using Game.Simulation;
using NetworkToolsReworked.Edits;
using Unity.Entities;

namespace NetworkToolsReworked.Tools
{
    /// <summary>Evens out the nodes of the road between two picked nodes; see <see cref="ArrangeEdit"/>.</summary>
    public partial class ArrangeToolSystem : PathToolSystem
    {
        private TerrainSystem m_TerrainSystem;
        private float m_ArcRadius;

        public override string toolID => "NetworkToolsReworked.ArrangeTool";

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
        }

        protected override bool EmitPath(EntityCommandBuffer ecb, List<Entity> nodes, List<Entity> edges, int randomSeed)
        {
            var terrain = m_TerrainSystem.GetHeightData();
            var settings = Mod.Settings;
            return ArrangeEdit.Emit(EntityManager, ecb, ref terrain, nodes, edges, settings.ArrangeMode, settings.ArrangeBulge / 100f, randomSeed, out m_ArcRadius);
        }

        protected override string Describe(List<Entity> nodes, List<Entity> edges)
        {
            var spacing = PathInfo.Length(EntityManager, edges) / edges.Count;
            var radius = Mod.Settings.ArrangeMode == ArrangeMode.Arc && m_ArcRadius > 0f ? $", radius {PathInfo.Distance(m_ArcRadius)}" : "";
            return $"{PathInfo.Describe(EntityManager, nodes, edges)}, about {PathInfo.Distance(spacing)} between nodes{radius}";
        }
    }
}
