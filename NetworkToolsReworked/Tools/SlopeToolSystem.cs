using System.Collections.Generic;
using Colossal.Entities;
using Game.Prefabs;
using Game.Simulation;
using NetworkToolsReworked.Edits;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace NetworkToolsReworked.Tools
{
    /// <summary>
    /// Re-grades, smooths or straightens the road between two picked nodes; see <see cref="SlopeEdit"/>.
    /// The shape options live in the settings; the end height changes apply to the current pick only.
    /// </summary>
    public partial class SlopeToolSystem : PathToolSystem
    {
        private TerrainSystem m_TerrainSystem;
        private ShapeResult m_Result;
        private bool m_HasResult;

        public override string toolID => "NetworkToolsReworked.SlopeTool";

        /// <summary>Height change at the start node, in metres, for the current pick.</summary>
        public float StartOffset { get; set; }

        /// <summary>Height change at the end node, in metres, for the current pick.</summary>
        public float EndOffset { get; set; }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
        }

        protected override void OnSelectionCleared()
        {
            StartOffset = 0f;
            EndOffset = 0f;
            m_HasResult = false;
        }

        protected override bool EmitPath(EntityCommandBuffer ecb, List<Entity> nodes, List<Entity> edges, int randomSeed)
        {
            var settings = Mod.Settings;
            var shape = new ShapeParams
            {
                Profile = settings.SlopeProfile,
                Ease = math.clamp(settings.SlopeEase, 0f, 50f) / 100f,
                Arch = settings.SlopeArch,
                StartOffset = StartOffset,
                EndOffset = EndOffset,
                Curve = settings.CurveMode,
                CurveStrength = math.clamp(settings.CurveStrength, 0f, 100f) / 100f,
                KeepEndDirections = settings.CurveKeepEnds,
            };

            var terrain = m_TerrainSystem.GetHeightData();
            m_HasResult = SlopeEdit.Emit(EntityManager, ecb, ref terrain, nodes, edges, shape, randomSeed, out m_Result);
            return m_HasResult;
        }

        /// <summary>Draws the new shape, coloured by grade against each road's own maximum.</summary>
        protected override void DrawPath(ToolOverlay overlay, List<Entity> nodes, List<Entity> edges, bool locked)
        {
            if (!m_HasResult)
            {
                base.DrawPath(overlay, nodes, edges, locked);
                return;
            }

            for (var i = 0; i < edges.Count; i++)
                overlay.Bezier(m_Result.Curves[i], overlay.Width(edges[i]), GradeColor(edges[i], m_Result.EdgeMaxGrade[i]));
        }

        protected override string Describe(List<Entity> nodes, List<Entity> edges)
        {
            var count = edges.Count == 1 ? "1 segment" : $"{edges.Count} segments";
            var r = m_Result;
            var grade = r.Length > 0.01f ? r.Rise / r.Length * 100f : 0f;
            var kept = r.InPlace ? ", nodes stay in place" : "";
            return $"{count}, {PathInfo.Distance(r.Length)}, height {PathInfo.Signed(r.Rise)} m, average {PathInfo.Signed(grade)}%, steepest {r.MaxGrade * 100f:0.0}%{kept}";
        }

        private Color GradeColor(Entity edge, float grade)
        {
            var max = 0.12f;
            if (EntityManager.TryGetComponent(edge, out PrefabRef prefab) &&
                EntityManager.TryGetComponent(prefab.m_Prefab, out NetGeometryData geometry) &&
                geometry.m_MaxSlopeSteepness > 0f)
                max = geometry.m_MaxSlopeSteepness;

            // Green when gentle, yellow at two thirds of the road's limit, red at the limit.
            var ratio = math.saturate(grade / max);
            var color = ratio < 0.67f
                ? Color.Lerp(new Color(0.25f, 0.9f, 0.4f), new Color(1f, 0.85f, 0.2f), ratio / 0.67f)
                : Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.25f, 0.2f), (ratio - 0.67f) / 0.33f);
            color.a = 0.45f;
            return color;
        }
    }
}
