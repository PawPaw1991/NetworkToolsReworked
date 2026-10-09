using System.Collections.Generic;
using NetworkToolsReworked.Edits;
using Unity.Entities;

namespace NetworkToolsReworked.Tools
{
    /// <summary>Draws a copy of the road between two picked nodes; see <see cref="ParallelEdit"/>.</summary>
    public partial class ParallelToolSystem : PathToolSystem
    {
        public override string toolID => "NetworkToolsReworked.ParallelTool";

        protected override void EmitPath(EntityCommandBuffer ecb, List<Entity> nodes, List<Entity> edges, int randomSeed)
        {
            ParallelEdit.Emit(EntityManager, ecb, nodes, edges, Mod.Settings.ParallelOffset, Mod.Settings.ParallelHeight, Mod.Settings.ParallelReverse, randomSeed);
        }
    }
}
