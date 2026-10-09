using System.Collections.Generic;
using Game.Simulation;
using NetworkToolsReworked.Edits;
using Unity.Entities;

namespace NetworkToolsReworked.Tools
{
    /// <summary>
    /// Pick two nodes: the segments between them are cut into equal parts or parts of a set length, or
    /// nodes they don't need are taken out. See <see cref="SplitEdit"/>.
    /// </summary>
    public partial class SplitToolSystem : PathToolSystem
    {
        private TerrainSystem m_TerrainSystem;
        private SplitResult m_Result;
        private bool m_HasResult;
        private int m_Nodes;
        private int m_Kept;

        public override string toolID => "NetworkToolsReworked.SplitTool";

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
        }

        protected override void OnBeforeEmit()
        {
            m_Nodes = 0;
            m_Kept = 0;
        }

        protected override bool EmitPath(EntityCommandBuffer ecb, List<Entity> nodes, List<Entity> edges, int randomSeed)
        {
            var s = Mod.Settings;
            var terrain = m_TerrainSystem.GetHeightData();
            m_HasResult = SplitEdit.Emit(EntityManager, ecb, ref terrain, nodes, edges, s.SplitMode, (int)s.SplitParts, s.SplitSpacing, s.SimplifyTolerance, randomSeed, out m_Result);
            m_Nodes += m_Result.Nodes;
            m_Kept += m_Result.Kept;
            return m_HasResult;
        }

        protected override void DrawPath(ToolOverlay overlay, List<Entity> nodes, List<Entity> edges, bool locked)
        {
            base.DrawPath(overlay, nodes, edges, locked);
            if (!m_HasResult)
                return;
            foreach (var curve in m_Result.Curves)
            {
                overlay.Point(curve.a, 5f, ToolOverlay.End);
                overlay.Point(curve.d, 5f, ToolOverlay.End);
            }
        }

        protected override string Describe(List<Entity> nodes, List<Entity> edges)
        {
            var info = PathInfo.Describe(EntityManager, nodes, edges);
            if (Mod.Settings.SplitMode != SplitMode.Simplify)
                return $"{info}: {m_Nodes} node(s) added";
            var kept = m_Kept > 0 ? $", {m_Kept} kept where the road bends more than the tolerance" : "";
            return $"{info}: {m_Nodes} node(s) removed{kept}";
        }

        protected override string NothingToChange => Mod.Settings.SplitMode == SplitMode.Simplify
            ? "No node here can go: each has a side road, a change of type, upgrades or direction, or a bend beyond the tolerance."
            : "The segments are already shorter than that.";
    }
}
