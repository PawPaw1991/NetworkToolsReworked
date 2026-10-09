using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Simulation;
using Game.Tools;
using NetworkToolsReworked.Edits;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkToolsReworked.Tools
{
    /// <summary>
    /// Pick two nodes: the road between them is lifted over the roads crossing it (grade separation),
    /// raised into a bridge, or lowered into a tunnel, with approach slopes within the road type's grade
    /// limit at both ends. See <see cref="LiftEdit"/>.
    /// </summary>
    public partial class BridgeToolSystem : PathToolSystem
    {
        private TerrainSystem m_TerrainSystem;
        private EntityQuery m_EdgeQuery;
        private readonly Dictionary<(Entity, Entity, int), List<LiftEdit.Crossing>> m_Crossings = new Dictionary<(Entity, Entity, int), List<LiftEdit.Crossing>>();
        private List<LiftEdit.Crossing> m_LastCrossings;
        private LiftResult m_Result;
        private bool m_HasResult;

        public override string toolID => "NetworkToolsReworked.BridgeTool";

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_EdgeQuery = GetEntityQuery(ComponentType.ReadOnly<Edge>(), ComponentType.ReadOnly<Curve>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
        }

        protected override void OnSelectionCleared()
        {
            m_Crossings.Clear();
            m_HasResult = false;
        }

        protected override bool EmitPath(EntityCommandBuffer ecb, List<Entity> nodes, List<Entity> edges, int randomSeed)
        {
            var s = Mod.Settings;
            m_LastCrossings = CrossingsFor(nodes, edges);
            var terrain = m_TerrainSystem.GetHeightData();
            m_HasResult = LiftEdit.Emit(EntityManager, ecb, ref terrain, nodes, edges, s.BridgeMode, s.BridgeHeight, s.BridgeClearance, m_LastCrossings, randomSeed, out m_Result);
            return m_HasResult;
        }

        protected override void DrawPath(ToolOverlay overlay, List<Entity> nodes, List<Entity> edges, bool locked)
        {
            if (!m_HasResult)
            {
                base.DrawPath(overlay, nodes, edges, locked);
                return;
            }
            foreach (var edge in edges)
                overlay.DashedEdge(edge, ToolOverlay.Before);
            var width = overlay.Width(edges[0]);
            foreach (var curve in m_Result.Curves)
                overlay.Bezier(curve, width, Grades.ColorFor(m_Result.TooShort ? m_Result.MaxGrade : CurveStats.MaxGrade(curve), m_Result.GradeLimit));
            if (m_LastCrossings != null)
                foreach (var c in m_LastCrossings)
                    overlay.Point(c.Position, 6f, ToolOverlay.End);
        }

        protected override string NothingToChange
        {
            get
            {
                var s = Mod.Settings;
                if (s.BridgeMode != LiftMode.OverCrossings)
                    return "Set a height above zero.";
                return m_LastCrossings == null || m_LastCrossings.Count == 0
                    ? "No other road crosses this stretch. Pick nodes on both sides of a crossing, or use Raise."
                    : "This stretch already clears the roads crossing it.";
            }
        }

        protected override string Describe(List<Entity> nodes, List<Entity> edges)
        {
            var r = m_Result;
            var what = r.Height >= 0f ? $"lifted {r.Height:0.0} m" : $"lowered {-r.Height:0.0} m";
            var crossings = Mod.Settings.BridgeMode == LiftMode.OverCrossings
                ? $" to clear {r.Crossings} crossing road(s) by {Mod.Settings.BridgeClearance:0.#} m" + (r.Uncleared > 0 ? $". {r.Uncleared} crossing(s) fall on a slope and aren't cleared: pick nodes further out" : "")
                : "";
            var slopes = $", approach slopes {PathInfo.Distance(r.RampLength)} each, steepest {r.MaxGrade * 100f:0.0}% (limit {r.GradeLimit * 100f:0}%)";
            var shortNote = r.TooShort ? $". Too short for this height: needs at least {PathInfo.Distance(1.5f * math.abs(r.Height) / (r.GradeLimit * 0.9f) * 2f)}, pick nodes further apart" : "";
            var sides = r.SideRoads > 0 ? $". {r.SideRoads} side road(s) joined in the middle move with it" : "";
            return $"{PathInfo.Describe(EntityManager, nodes, edges)}, {what}{crossings}{slopes}{shortNote}{sides}";
        }

        /// <summary>Crossings are looked up once per stretch, since that checks every road in the city.</summary>
        private List<LiftEdit.Crossing> CrossingsFor(List<Entity> nodes, List<Entity> edges)
        {
            if (Mod.Settings.BridgeMode != LiftMode.OverCrossings)
                return new List<LiftEdit.Crossing>();
            var key = (nodes[0], nodes[nodes.Count - 1], edges.Count);
            if (!m_Crossings.TryGetValue(key, out var list))
            {
                if (m_Crossings.Count > 64)
                    m_Crossings.Clear();
                list = new List<LiftEdit.Crossing>();
                LiftEdit.FindCrossings(EntityManager, m_EdgeQuery, nodes, edges, list);
                m_Crossings[key] = list;
            }
            return list;
        }
    }
}
