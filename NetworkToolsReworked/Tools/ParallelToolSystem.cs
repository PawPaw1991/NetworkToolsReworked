using System.Collections.Generic;
using Colossal.Entities;
using Game.Prefabs;
using NetworkToolsReworked.Edits;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkToolsReworked.Tools
{
    /// <summary>How the Parallel tool's sideways distance is given.</summary>
    public enum ParallelSpacing
    {
        /// <summary>Centre to centre, in metres (the Side offset setting).</summary>
        Metres,

        /// <summary>Edge to edge: the copy sits right next to the road, plus a gap.</summary>
        Touching,

        /// <summary>A whole number of road widths, centre to centre.</summary>
        Widths,
    }

    /// <summary>Draws a copy of the road between two picked nodes; see <see cref="ParallelEdit"/>.</summary>
    public partial class ParallelToolSystem : PathToolSystem
    {
        public override string toolID => "NetworkToolsReworked.ParallelTool";

        protected override bool EmitPath(EntityCommandBuffer ecb, List<Entity> nodes, List<Entity> edges, int randomSeed)
        {
            var s = Mod.Settings;
            var offset = Offset(edges);
            ParallelEdit.Emit(EntityManager, ecb, nodes, edges, offset, s.ParallelHeight, s.ParallelReverse, s.ParallelTaper, randomSeed);
            if (s.ParallelBothSides)
                ParallelEdit.Emit(EntityManager, ecb, nodes, edges, -offset, s.ParallelHeight, s.ParallelReverse, s.ParallelTaper, randomSeed);
            return true;
        }

        protected override string Describe(List<Entity> nodes, List<Entity> edges)
        {
            var s = Mod.Settings;
            var offset = Offset(edges);
            var side = s.ParallelBothSides ? "on both sides" : offset >= 0f ? "to the right" : "to the left";
            var direction = s.ParallelReverse ? ", opposite direction" : "";
            if (s.ParallelTaper == ParallelTaper.Split)
                direction += ", splitting off at the start node";
            else if (s.ParallelTaper == ParallelTaper.Merge)
                direction += ", merging in at the end node";
            var spacing = s.ParallelSpacing == ParallelSpacing.Touching ? $" (road width {PathInfo.Distance(RoadWidth(edges))} + {s.ParallelGap:0.#} m gap)"
                : s.ParallelSpacing == ParallelSpacing.Widths ? $" ({s.ParallelWidths:0} road widths)" : "";
            return $"{PathInfo.Describe(EntityManager, nodes, edges)}, {PathInfo.Distance(math.abs(offset))}{spacing} {side}, {PathInfo.Signed(s.ParallelHeight)} m height{direction}";
        }

        /// <summary>Signed centre-to-centre offset for the current spacing mode; the sign of Side offset picks the side.</summary>
        private float Offset(List<Entity> edges)
        {
            var s = Mod.Settings;
            var sign = s.ParallelOffset >= 0f ? 1f : -1f;
            switch (s.ParallelSpacing)
            {
                case ParallelSpacing.Touching:
                    return sign * (RoadWidth(edges) + math.max(s.ParallelGap, 0f));
                case ParallelSpacing.Widths:
                    return sign * RoadWidth(edges) * math.max(s.ParallelWidths, 1f);
                default:
                    return s.ParallelOffset;
            }
        }

        /// <summary>Widest road on the path; the copy uses the same road types, so this is also its width.</summary>
        private float RoadWidth(List<Entity> edges)
        {
            var width = 0f;
            foreach (var edge in edges)
                if (EntityManager.TryGetComponent(edge, out PrefabRef prefab) && EntityManager.TryGetComponent(prefab.m_Prefab, out NetGeometryData geometry))
                    width = math.max(width, geometry.m_DefaultWidth);
            return width > 0f ? width : 8f;
        }
    }
}
