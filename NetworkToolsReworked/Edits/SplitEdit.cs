using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkToolsReworked.Edits
{
    public enum SplitMode
    {
        /// <summary>Cut every segment into the same number of equal parts.</summary>
        EqualParts,

        /// <summary>Cut every segment into parts of about the set length.</summary>
        EveryDistance,

        /// <summary>Remove nodes the road doesn't need: where it just carries on with no side road.</summary>
        Simplify,
    }

    public struct SplitResult
    {
        /// <summary>Nodes added (split) or removed (simplify).</summary>
        public int Nodes;

        /// <summary>Simplify: removable nodes kept because merging there would bend the road too much.</summary>
        public int Kept;

        /// <summary>New pieces, for the preview.</summary>
        public List<Bezier4x3> Curves;
    }

    /// <summary>
    /// Adds nodes along a stretch (each segment cut into equal parts or parts of a set length), or takes
    /// out nodes it doesn't need. A node is only taken out where exactly two segments of the same type,
    /// upgrades, direction and bridge or ground state meet, and only when one curve through the
    /// neighbours stays within the tolerance of the old road. Segments are rebuilt through definitions;
    /// the stretch's end nodes and any node with a side road stay.
    /// </summary>
    public static class SplitEdit
    {
        private const int kSamples = 64;

        public static bool Emit(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, List<Entity> nodes, List<Entity> edges, SplitMode mode, int parts, float spacing, float tolerance, int randomSeed, out SplitResult result)
        {
            result = new SplitResult { Curves = new List<Bezier4x3>() };
            return mode == SplitMode.Simplify
                ? Simplify(em, ecb, ref terrain, nodes, edges, tolerance, randomSeed, ref result)
                : Split(em, ecb, ref terrain, edges, mode, parts, spacing, randomSeed, ref result);
        }

        private static bool Split(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, List<Entity> edges, SplitMode mode, int parts, float spacing, int randomSeed, ref SplitResult result)
        {
            var none = new Dictionary<Entity, float3>();
            foreach (var edge in edges)
            {
                var curve = em.GetComponentData<Curve>(edge).m_Bezier;
                var table = LengthTable(curve);
                var length = table[kSamples];
                var count = mode == SplitMode.EqualParts ? math.max(parts, 1) : math.max(1, (int)math.round(length / math.max(spacing, 4f)));
                if (count < 2 || length / count < 2f)
                    continue;

                var e = em.GetComponentData<Edge>(edge);
                var hasElevation = em.HasComponent<Elevation>(edge);
                var prefab = em.GetComponentData<PrefabRef>(edge).m_Prefab;
                var hasUpgrades = em.TryGetComponent(edge, out Upgraded upgraded);
                NetDefinitions.DeleteEdge(em, ecb, edge, randomSeed);

                var t0 = 0f;
                for (var k = 1; k <= count; k++)
                {
                    var t1 = k == count ? 1f : TAt(table, length * k / count);
                    var piece = MathUtils.Cut(curve, new float2(t0, t1));
                    var startPos = k == 1 ? SlopeEdit.ChainEnd(em, ref terrain, e.m_Start, none, piece, start: true, hasElevation) : FreeEnd(ref terrain, piece, true, hasElevation);
                    var endPos = k == count ? SlopeEdit.ChainEnd(em, ref terrain, e.m_End, none, piece, start: false, hasElevation) : FreeEnd(ref terrain, piece, false, hasElevation);
                    EmitPiece(ecb, prefab, piece, startPos, endPos, hasUpgrades, upgraded, randomSeed);
                    result.Curves.Add(piece);
                    t0 = t1;
                }
                result.Nodes += count - 1;
            }
            return result.Nodes > 0;
        }

        private static bool Simplify(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, List<Entity> nodes, List<Entity> edges, float tolerance, int randomSeed, ref SplitResult result)
        {
            var count = edges.Count;
            var forward = new bool[count];
            var curves = new Bezier4x3[count];
            for (var i = 0; i < count; i++)
            {
                var b = em.GetComponentData<Curve>(edges[i]).m_Bezier;
                forward[i] = em.GetComponentData<Edge>(edges[i]).m_Start == nodes[i];
                curves[i] = forward[i] ? b : MathUtils.Invert(b);
            }

            // Walk the stretch, growing each merged piece while the next node can go and the one curve
            // still fits the old road.
            var i0 = 0;
            while (i0 < count)
            {
                var j = i0;
                var best = i0;
                var bestCurve = curves[i0];
                while (j + 1 < count && Removable(em, nodes[j + 1], edges[j], edges[j + 1], forward[j], forward[j + 1]))
                {
                    j++;
                    var fitted = Fit(curves, i0, j);
                    if (Deviation(fitted, curves, i0, j) > tolerance)
                    {
                        result.Kept++;
                        break;
                    }
                    best = j;
                    bestCurve = fitted;
                }

                if (best > i0)
                {
                    EmitMerged(em, ecb, ref terrain, nodes, edges, i0, best, bestCurve, forward[i0], randomSeed);
                    result.Curves.Add(bestCurve);
                    result.Nodes += best - i0;
                }
                i0 = best + 1;
            }
            return result.Nodes > 0;
        }

        private static bool Removable(EntityManager em, Entity node, Entity a, Entity b, bool forwardA, bool forwardB)
        {
            if (!NetDefinitions.CanMergeAtNode(em, node, out _, out _) || forwardA != forwardB)
                return false;
            if (em.HasComponent<Elevation>(a) != em.HasComponent<Elevation>(b))
                return false;
            var hasA = em.TryGetComponent(a, out Upgraded ua);
            var hasB = em.TryGetComponent(b, out Upgraded ub);
            return hasA == hasB && (!hasA || ua.m_Flags == ub.m_Flags);
        }

        /// <summary>One curve from node i0 to node j + 1, keeping the end directions.</summary>
        private static Bezier4x3 Fit(Bezier4x3[] curves, int i0, int j)
        {
            return NetUtils.FitCurve(curves[i0].a, MathUtils.StartTangent(curves[i0]), MathUtils.EndTangent(curves[j]), curves[j].d);
        }

        /// <summary>Furthest any old curve between i0 and j strays from the fitted one, metres.</summary>
        private static float Deviation(Bezier4x3 fitted, Bezier4x3[] curves, int i0, int j)
        {
            var samples = new float3[kSamples + 1];
            for (var k = 0; k <= kSamples; k++)
                samples[k] = MathUtils.Position(fitted, (float)k / kSamples);

            var worst = 0f;
            for (var i = i0; i <= j; i++)
            {
                for (var k = 0; k <= 8; k++)
                {
                    var p = MathUtils.Position(curves[i], k / 8f);
                    var nearest = float.MaxValue;
                    for (var s = 0; s < kSamples; s++)
                        nearest = math.min(nearest, DistanceToSegment(p, samples[s], samples[s + 1]));
                    worst = math.max(worst, nearest);
                }
            }
            return worst;
        }

        private static void EmitMerged(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, List<Entity> nodes, List<Entity> edges, int i0, int j, Bezier4x3 curve, bool forward, int randomSeed)
        {
            var hasElevation = em.HasComponent<Elevation>(edges[i0]);
            var prefab = em.GetComponentData<PrefabRef>(edges[i0]).m_Prefab;
            var hasUpgrades = em.TryGetComponent(edges[i0], out Upgraded upgraded);
            for (var i = i0; i <= j; i++)
                NetDefinitions.DeleteEdge(em, ecb, edges[i], randomSeed);
            for (var i = i0 + 1; i <= j; i++)
                NetDefinitions.DeleteNode(em, ecb, nodes[i], randomSeed);

            // Keep the road's own direction of travel.
            var from = nodes[i0];
            var to = nodes[j + 1];
            if (!forward)
            {
                curve = MathUtils.Invert(curve);
                (from, to) = (to, from);
            }
            var none = new Dictionary<Entity, float3>();
            var startPos = SlopeEdit.ChainEnd(em, ref terrain, from, none, curve, start: true, hasElevation);
            var endPos = SlopeEdit.ChainEnd(em, ref terrain, to, none, curve, start: false, hasElevation);
            EmitPiece(ecb, prefab, curve, startPos, endPos, hasUpgrades, upgraded, randomSeed);
        }

        private static void EmitPiece(EntityCommandBuffer ecb, Entity prefab, Bezier4x3 curve, CoursePos startPos, CoursePos endPos, bool hasUpgrades, Upgraded upgraded, int randomSeed)
        {
            var definition = NetDefinitions.Emit(ecb, new CreationDefinition
            {
                m_Prefab = prefab,
                m_RandomSeed = randomSeed,
                m_Flags = CreationFlags.SubElevation,
            }, new NetCourse
            {
                m_Curve = curve,
                m_StartPosition = startPos,
                m_EndPosition = endPos,
                m_Elevation = new float2(startPos.m_Elevation.x, endPos.m_Elevation.x),
                m_Length = MathUtils.Length(curve),
                m_FixedIndex = -1,
            });
            if (hasUpgrades)
                ecb.AddComponent(definition, upgraded);
        }

        private static CoursePos FreeEnd(ref TerrainHeightData terrain, Bezier4x3 piece, bool start, bool hasElevation)
        {
            var p = start ? piece.a : piece.d;
            return new CoursePos
            {
                m_Entity = Entity.Null,
                m_Position = p,
                m_Rotation = NetUtils.GetNodeRotation(start ? MathUtils.StartTangent(piece) : MathUtils.EndTangent(piece)),
                m_Elevation = new float2(hasElevation ? p.y - TerrainUtils.SampleHeight(ref terrain, p) : 0f),
                m_CourseDelta = start ? 0f : 1f,
                // A split point is shared by the pieces either side of it, so it carries no end flags
                // (see SlopeEdit.ChainEnd).
                m_ParentMesh = -1,
            };
        }

        private static float DistanceToSegment(float3 p, float3 a, float3 b)
        {
            var ab = b - a;
            var t = math.saturate(math.dot(p - a, ab) / math.max(math.lengthsq(ab), 1e-6f));
            return math.distance(p, a + ab * t);
        }

        private static float[] LengthTable(Bezier4x3 curve)
        {
            var table = new float[kSamples + 1];
            var previous = curve.a;
            for (var k = 1; k <= kSamples; k++)
            {
                var p = MathUtils.Position(curve, (float)k / kSamples);
                table[k] = table[k - 1] + math.distance(previous, p);
                previous = p;
            }
            return table;
        }

        private static float TAt(float[] table, float length)
        {
            for (var k = 1; k <= kSamples; k++)
            {
                if (table[k] >= length)
                {
                    var span = table[k] - table[k - 1];
                    return (k - 1 + (span > 1e-5f ? (length - table[k - 1]) / span : 0f)) / kSamples;
                }
            }
            return 1f;
        }
    }
}
