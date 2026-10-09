using System.Collections.Generic;
using Game.Simulation;
using NetworkToolsReworked.Edits;
using Unity.Entities;

namespace NetworkToolsReworked.Tools
{
    /// <summary>Re-grades the road between two picked nodes; see <see cref="SlopeEdit"/>.</summary>
    public partial class SlopeToolSystem : PathToolSystem
    {
        private TerrainSystem m_TerrainSystem;

        public override string toolID => "NetworkToolsReworked.SlopeTool";

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
        }

        protected override void EmitPath(EntityCommandBuffer ecb, List<Entity> nodes, List<Entity> edges, int randomSeed)
        {
            var terrain = m_TerrainSystem.GetHeightData();
            SlopeEdit.Emit(EntityManager, ecb, ref terrain, nodes, edges, Mod.Settings.SlopeProfile, randomSeed);
        }
    }
}
