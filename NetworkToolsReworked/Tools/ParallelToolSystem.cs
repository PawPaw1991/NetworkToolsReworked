using System.Collections.Generic;
using NetworkToolsReworked.Edits;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkToolsReworked.Tools
{
    /// <summary>Draws a copy of the road between two picked nodes; see <see cref="ParallelEdit"/>.</summary>
    public partial class ParallelToolSystem : PathToolSystem
    {
        public override string toolID => "NetworkToolsReworked.ParallelTool";

        protected override bool EmitPath(EntityCommandBuffer ecb, List<Entity> nodes, List<Entity> edges, int randomSeed)
        {
            ParallelEdit.Emit(EntityManager, ecb, nodes, edges, Mod.Settings.ParallelOffset, Mod.Settings.ParallelHeight, Mod.Settings.ParallelReverse, randomSeed);
            return true;
        }

        protected override string Describe(List<Entity> nodes, List<Entity> edges)
        {
            var s = Mod.Settings;
            var side = s.ParallelOffset >= 0f ? "right" : "left";
            var direction = s.ParallelReverse ? ", opposite direction" : "";
            return $"{PathInfo.Describe(EntityManager, nodes, edges)}, {PathInfo.Distance(math.abs(s.ParallelOffset))} to the {side}, {PathInfo.Signed(s.ParallelHeight)} m height{direction}";
        }
    }
}
