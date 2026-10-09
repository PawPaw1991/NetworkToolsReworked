using System.Collections.Generic;
using Game.Simulation;
using NetworkToolsReworked.Edits;
using Unity.Entities;

namespace NetworkToolsReworked.Tools
{
    /// <summary>Reverses the direction of the road between two picked nodes; see <see cref="ReverseEdit"/>.</summary>
    public partial class ReverseToolSystem : PathToolSystem
    {
        private TerrainSystem m_TerrainSystem;

        public override string toolID => "NetworkToolsReworked.ReverseTool";

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
        }

        protected override bool EmitPath(EntityCommandBuffer ecb, List<Entity> nodes, List<Entity> edges, int randomSeed)
        {
            var terrain = m_TerrainSystem.GetHeightData();
            return ReverseEdit.Emit(EntityManager, ecb, ref terrain, edges, randomSeed);
        }
    }
}
