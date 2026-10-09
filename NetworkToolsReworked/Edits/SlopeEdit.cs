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
    /// <summary>
    /// Reshapes a chain of edges between two picked nodes: height profile, arch, end heights, and
    /// horizontal smoothing or straightening. The game can't move an existing road node through
    /// definitions, so when nodes move the chain is rebuilt: old chain edges are deleted, new courses
    /// are emitted through new nodes at the new positions, and other roads at those nodes are
    /// re-pointed to the new nodes (keeping their identity via m_Original). The old nodes are left
    /// orphaned and removed by the game.
    /// </summary>
    public struct ShapeParams
    {
        public SlopeProfile Profile;

        /// <summary>Share of the length (0 to 0.5) at each end that eases into the grade.</summary>
        public float Ease;

        /// <summary>Extra height at the middle in metres; negative for a dip.</summary>
        public float Arch;

        /// <summary>Height change at the start and end node, in metres.</summary>
        public float StartOffset;
        public float EndOffset;

        public CurveMode Curve;

        /// <summary>0 to 1: how far to go from the current curve towards the smoothed or straight one.</summary>
        public float CurveStrength;

        /// <summary>Smooth keeps the road's direction at the two end nodes, so roads beyond them stay aligned.</summary>
        public bool KeepEndDirections;

        /// <summary>
        /// With the Keep profile: line up the grade at every joint too, so the road has no bumps or dips
        /// at its nodes. Node heights stay as they are.
        /// </summary>
        public bool SmoothGrades;

        /// <summary>
        /// Smooth only, 0 to 1: also pull the inner nodes sideways towards an even line between their
        /// neighbours. Any amount above zero moves nodes, so the chain is rebuilt.
        /// </summary>
        public float Relax;
    }

    public struct ShapeResult
    {
        /// <summary>New curve of each chain edge, oriented from the start node.</summary>
        public Bezier4x3[] Curves;
        public float[] EdgeMaxGrade;
        public float Length;
        public float Rise;
        public float MaxGrade;

        /// <summary>True if no node moved and the edges were edited in place.</summary>
        public bool InPlace;
    }

    public static class SlopeEdit
    {
        private const int kMaxSearchNodes = 512;

        /// <summary>Shortest chain of unowned edges from start to end, as ordered node and edge lists.</summary>
        public static bool FindPath(EntityManager em, Entity start, Entity end, List<Entity> nodes, List<Entity> edges)
        {
            nodes.Clear();
            edges.Clear();
            if (start == end || !IsEditableNode(em, start) || !IsEditableNode(em, end))
                return false;

            var distance = new Dictionary<Entity, float> { [start] = 0f };
            var previous = new Dictionary<Entity, (Entity node, Entity edge)>();
            var open = new List<Entity> { start };
            var closed = new HashSet<Entity>();

            while (open.Count > 0 && closed.Count < kMaxSearchNodes)
            {
                var best = 0;
                for (var i = 1; i < open.Count; i++)
                    if (distance[open[i]] < distance[open[best]])
                        best = i;
                var node = open[best];
                open.RemoveAt(best);
                if (!closed.Add(node))
                    continue;
                if (node == end)
                    break;

                foreach (var connected in em.GetBuffer<ConnectedEdge>(node, isReadOnly: true))
                {
                    var edge = connected.m_Edge;
                    if (em.HasComponent<Owner>(edge) || !em.TryGetComponent(edge, out Edge e))
                        continue;
                    var other = e.m_Start == node ? e.m_End : e.m_Start;
                    if ((e.m_Start != node && e.m_End != node) || closed.Contains(other) || !IsEditableNode(em, other))
                        continue;

                    var d = distance[node] + em.GetComponentData<Curve>(edge).m_Length;
                    if (!distance.TryGetValue(other, out var known) || d < known)
                    {
                        distance[other] = d;
                        previous[other] = (node, edge);
                        open.Add(other);
                    }
                }
            }

            if (!previous.ContainsKey(end))
                return false;

            for (var node = end; node != start; node = previous[node].node)
            {
                nodes.Add(node);
                edges.Add(previous[node].edge);
            }
            nodes.Add(start);
            nodes.Reverse();
            edges.Reverse();
            return true;
        }

        /// <summary>
        /// Reshapes the chain. Returns false if the settings change nothing. When no node moves (for
        /// example smoothing only), the edges are edited in place and keep their identity; otherwise
        /// the chain is rebuilt as described on the class.
        /// </summary>
        public static bool Emit(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, List<Entity> nodes, List<Entity> edges, in ShapeParams shape, int randomSeed, out ShapeResult result)
        {
            result = default;
            var keepsCurve = shape.Curve == CurveMode.Keep || shape.CurveStrength <= 0f;
            if (shape.Profile == SlopeProfile.Keep && keepsCurve && !shape.SmoothGrades && shape.Arch == 0f && shape.StartOffset == 0f && shape.EndOffset == 0f)
                return false;

            var count = edges.Count;
            var original = new Bezier4x3[count];
            var originalAlong = new float[count + 1];
            for (var i = 0; i < count; i++)
            {
                var bezier = em.GetComponentData<Curve>(edges[i]).m_Bezier;
                if (em.GetComponentData<Edge>(edges[i]).m_Start != nodes[i])
                    bezier = MathUtils.Invert(bezier);
                original[i] = bezier;
                originalAlong[i + 1] = originalAlong[i] + math.max(MathUtils.Length(bezier.xz), 0.01f);
            }

            // Horizontal shape first; heights follow the new horizontal length.
            var curves = ShapeHorizontal(original, originalAlong, shape);
            var along = new float[count + 1];
            var lengths = new float[count];
            for (var i = 0; i < count; i++)
            {
                lengths[i] = math.max(MathUtils.Length(curves[i].xz), 0.01f);
                along[i + 1] = along[i] + lengths[i];
            }

            var total = along[count];
            var profile = new HeightProfile(original, originalAlong, total, shape,
                em.GetComponentData<Node>(nodes[0]).m_Position.y,
                em.GetComponentData<Node>(nodes[count]).m_Position.y);

            for (var i = 0; i < count; i++)
            {
                var h0 = profile.Height(along[i], out var g0);
                var h1 = profile.Height(along[i + 1], out var g1);
                curves[i].a.y = h0;
                curves[i].d.y = h1;
                curves[i].b.y = h0 + g0 * lengths[i] / 3f;
                curves[i].c.y = h1 - g1 * lengths[i] / 3f;
            }

            if (shape.Profile == SlopeProfile.Keep && shape.SmoothGrades)
                SmoothGrades(curves, original, along, shape.KeepEndDirections);

            result = new ShapeResult
            {
                Curves = curves,
                EdgeMaxGrade = new float[count],
                Length = total,
                Rise = curves[count - 1].d.y - curves[0].a.y,
            };
            for (var i = 0; i < count; i++)
            {
                const int samples = 16;
                for (var k = 0; k <= samples; k++)
                {
                    var tangent = MathUtils.Tangent(curves[i], (float)k / samples);
                    var grade = math.abs(tangent.y) / math.max(math.length(tangent.xz), 0.001f);
                    result.EdgeMaxGrade[i] = math.max(result.EdgeMaxGrade[i], grade);
                }
                result.MaxGrade = math.max(result.MaxGrade, result.EdgeMaxGrade[i]);
            }

            if (!EmitChain(em, ecb, ref terrain, nodes, edges, curves, original, randomSeed, out var inPlace))
                return false;
            result.InPlace = inPlace;
            return true;
        }

        /// <summary>
        /// Replaces the chain's curves with <paramref name="curves"/> (oriented from nodes[0]). Nodes whose
        /// position changes get the rebuild described on the class; if none moves, edges are edited in
        /// place. Returns false if nothing changes.
        /// </summary>
        internal static bool EmitChain(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, List<Entity> nodes, List<Entity> edges, Bezier4x3[] curves, Bezier4x3[] original, int randomSeed, out bool inPlace)
        {
            var count = edges.Count;
            inPlace = false;

            // Nodes that end up somewhere else need the rebuild; the rest keep their entity.
            var newPositions = new Dictionary<Entity, float3>();
            for (var i = 0; i <= count; i++)
            {
                var p = i < count ? curves[i].a : curves[count - 1].d;
                if (math.distancesq(p, em.GetComponentData<Node>(nodes[i]).m_Position) > 0.0001f)
                    newPositions[nodes[i]] = p;
            }

            var changed = newPositions.Count > 0;
            for (var i = 0; i < count && !changed; i++)
                changed = math.distancesq(curves[i].b, original[i].b) > 0.0001f || math.distancesq(curves[i].c, original[i].c) > 0.0001f;
            if (!changed)
                return false;

            inPlace = newPositions.Count == 0;
            if (inPlace)
            {
                for (var i = 0; i < count; i++)
                    EmitInPlace(em, ecb, ref terrain, edges[i], nodes[i], nodes[i + 1], curves[i], randomSeed);
                return true;
            }

            for (var i = 0; i < count; i++)
            {
                var b = curves[i];
                NetDefinitions.DeleteEdge(em, ecb, edges[i], randomSeed);

                var hasElevation = em.HasComponent<Elevation>(edges[i]);
                var startPos = ChainEnd(em, ref terrain, nodes[i], newPositions, b, start: true, hasElevation);
                var endPos = ChainEnd(em, ref terrain, nodes[i + 1], newPositions, b, start: false, hasElevation);

                var course = new NetCourse
                {
                    m_Curve = b,
                    m_StartPosition = startPos,
                    m_EndPosition = endPos,
                    m_Elevation = new float2(startPos.m_Elevation.x, endPos.m_Elevation.x),
                    m_Length = MathUtils.Length(b),
                    m_FixedIndex = -1,
                };

                var definition = NetDefinitions.Emit(ecb, new CreationDefinition
                {
                    m_Prefab = em.GetComponentData<PrefabRef>(edges[i]).m_Prefab,
                    m_RandomSeed = randomSeed,
                    m_Flags = CreationFlags.SubElevation,
                }, course);

                if (em.TryGetComponent(edges[i], out Upgraded upgraded))
                    ecb.AddComponent(definition, upgraded);
            }

            // Re-point the other roads at each moved node, keeping those roads themselves (m_Original),
            // and remove the old node outright, so nothing can stay attached to it.
            var chainEdges = new HashSet<Entity>(edges);
            var handled = new HashSet<Entity>();
            foreach (var node in newPositions.Keys)
            {
                NetDefinitions.DeleteNode(em, ecb, node, randomSeed);
                foreach (var connected in em.GetBuffer<ConnectedEdge>(node, isReadOnly: true))
                {
                    var side = connected.m_Edge;
                    if (chainEdges.Contains(side) || !handled.Add(side) || !em.TryGetComponent(side, out Edge e))
                        continue;
                    if (e.m_Start != node && e.m_End != node)
                        continue;
                    EmitSideEdge(em, ecb, ref terrain, side, e, newPositions, randomSeed);
                }
            }

            return true;
        }

        /// <summary>New horizontal (xz) shape of each oriented chain curve; y is left as it was.</summary>
        private static Bezier4x3[] ShapeHorizontal(Bezier4x3[] original, float[] originalAlong, in ShapeParams shape)
        {
            var count = original.Length;
            var curves = (Bezier4x3[])original.Clone();
            var strength = math.saturate(shape.CurveStrength);
            if (shape.Curve == CurveMode.Keep || strength <= 0f)
                return curves;

            var target = new Bezier4x3[count];
            if (shape.Curve == CurveMode.Transition)
            {
                TransitionCurve(original, originalAlong, target);
            }
            else if (shape.Curve == CurveMode.Straighten)
            {
                var from = original[0].a;
                var to = original[count - 1].d;
                for (var i = 0; i < count; i++)
                {
                    var a = math.lerp(from, to, originalAlong[i] / originalAlong[count]);
                    var d = math.lerp(from, to, originalAlong[i + 1] / originalAlong[count]);
                    target[i] = new Bezier4x3(a, math.lerp(a, d, 1f / 3f), math.lerp(a, d, 2f / 3f), d);
                }
            }
            else
            {
                // Smooth: one direction per joint, from the neighbouring nodes (Catmull-Rom style).
                var points = new float3[count + 1];
                for (var i = 0; i < count; i++)
                    points[i] = original[i].a;
                points[count] = original[count - 1].d;
                RelaxPoints(points, math.saturate(shape.Relax));

                var directions = new float3[count + 1];
                for (var i = 1; i < count; i++)
                    directions[i] = Flat(points[i + 1] - points[i - 1]);

                if (shape.KeepEndDirections)
                {
                    directions[0] = Flat(MathUtils.StartTangent(original[0]));
                    directions[count] = Flat(MathUtils.EndTangent(original[count - 1]));
                }
                else if (count == 1)
                {
                    directions[0] = directions[1] = Flat(points[1] - points[0]);
                }
                else
                {
                    // Free ends mirror the next joint's direction across the end segment, like an arc.
                    directions[0] = Mirror(directions[1], Flat(points[1] - points[0]));
                    directions[count] = Mirror(directions[count - 1], Flat(points[count] - points[count - 1]));
                }

                for (var i = 0; i < count; i++)
                {
                    var a = points[i];
                    var d = points[i + 1];
                    var handle = math.distance(a.xz, d.xz) / 3f;
                    target[i] = new Bezier4x3(a, a + directions[i] * handle, d - directions[i + 1] * handle, d);
                }
            }

            for (var i = 0; i < count; i++)
            {
                var o = original[i];
                var t = target[i];
                curves[i] = new Bezier4x3(LerpXZ(o.a, t.a, strength), LerpXZ(o.b, t.b, strength), LerpXZ(o.c, t.c, strength), LerpXZ(o.d, t.d, strength));
            }
            return curves;
        }

        /// <summary>
        /// Fills <paramref name="target"/> with a curvature-continuous path from the chain's start to its end,
        /// leaving and arriving in the road's current directions with zero curvature at both ends, so the
        /// bend builds up and eases off gradually. It is a quintic Hermite curve (zero second derivative at
        /// the ends); inner nodes are placed along it at the same share of the length as before.
        /// </summary>
        private static void TransitionCurve(Bezier4x3[] original, float[] originalAlong, Bezier4x3[] target)
        {
            var count = original.Length;
            var a = original[0].a;
            var b = original[count - 1].d;
            var chord = Flat(b - a);
            var span = math.distance(a.xz, b.xz);
            var startDir = Flat(MathUtils.StartTangent(original[0]));
            var endDir = Flat(MathUtils.EndTangent(original[count - 1]));
            if (math.lengthsq(startDir) < 0.5f) startDir = chord;
            if (math.lengthsq(endDir) < 0.5f) endDir = chord;
            var v0 = startDir * span;
            var v1 = endDir * span;

            // Arc length table, to place nodes by distance along the new path.
            const int samples = 256;
            var arc = new float[samples + 1];
            var previous = Quintic(a, v0, b, v1, 0f);
            for (var k = 1; k <= samples; k++)
            {
                var p = Quintic(a, v0, b, v1, (float)k / samples);
                arc[k] = arc[k - 1] + math.distance(previous.xz, p.xz);
                previous = p;
            }

            var u = new float[count + 1];
            u[count] = 1f;
            var j = 0;
            for (var i = 1; i < count; i++)
            {
                var s = arc[samples] * originalAlong[i] / originalAlong[count];
                while (j < samples - 1 && arc[j + 1] < s)
                    j++;
                var piece = math.max(arc[j + 1] - arc[j], 0.0001f);
                u[i] = (j + math.saturate((s - arc[j]) / piece)) / samples;
            }

            for (var i = 0; i < count; i++)
            {
                var du = u[i + 1] - u[i];
                var p0 = Quintic(a, v0, b, v1, u[i]);
                var p1 = Quintic(a, v0, b, v1, u[i + 1]);
                target[i] = new Bezier4x3(p0, p0 + QuinticTangent(a, v0, b, v1, u[i]) * du / 3f, p1 - QuinticTangent(a, v0, b, v1, u[i + 1]) * du / 3f, p1);
            }
        }

        private static float3 Quintic(float3 p0, float3 v0, float3 p1, float3 v1, float u)
        {
            float u2 = u * u, u3 = u2 * u, u4 = u3 * u, u5 = u4 * u;
            return (1f - 10f * u3 + 15f * u4 - 6f * u5) * p0
                 + (u - 6f * u3 + 8f * u4 - 3f * u5) * v0
                 + (10f * u3 - 15f * u4 + 6f * u5) * p1
                 + (-4f * u3 + 7f * u4 - 3f * u5) * v1;
        }

        private static float3 QuinticTangent(float3 p0, float3 v0, float3 p1, float3 v1, float u)
        {
            float u2 = u * u, u3 = u2 * u, u4 = u3 * u;
            return (-30f * u2 + 60f * u3 - 30f * u4) * (p0 - p1)
                 + (1f - 18f * u2 + 32f * u3 - 15f * u4) * v0
                 + (-12f * u2 + 28f * u3 - 15f * u4) * v1;
        }

        /// <summary>
        /// Sets one grade per joint from the neighbouring node heights (Catmull-Rom style) and rebuilds the
        /// vertical handles from it, so the grade flows through each node. Node heights don't change.
        /// </summary>
        private static void SmoothGrades(Bezier4x3[] curves, Bezier4x3[] original, float[] along, bool keepEnds)
        {
            var count = curves.Length;
            var heights = new float[count + 1];
            for (var i = 0; i < count; i++)
                heights[i] = curves[i].a.y;
            heights[count] = curves[count - 1].d.y;

            var grades = new float[count + 1];
            for (var i = 1; i < count; i++)
                grades[i] = (heights[i + 1] - heights[i - 1]) / math.max(along[i + 1] - along[i - 1], 0.01f);

            if (keepEnds)
            {
                grades[0] = Grade(MathUtils.StartTangent(original[0]));
                grades[count] = Grade(MathUtils.EndTangent(original[count - 1]));
            }
            else if (count == 1)
            {
                grades[0] = grades[1] = (heights[1] - heights[0]) / math.max(along[1], 0.01f);
            }
            else
            {
                // Free ends: the end segment becomes a parabola that meets the next joint's grade.
                grades[0] = 2f * (heights[1] - heights[0]) / math.max(along[1] - along[0], 0.01f) - grades[1];
                grades[count] = 2f * (heights[count] - heights[count - 1]) / math.max(along[count] - along[count - 1], 0.01f) - grades[count - 1];
            }

            for (var i = 0; i < count; i++)
            {
                var length = along[i + 1] - along[i];
                curves[i].b.y = heights[i] + grades[i] * length / 3f;
                curves[i].c.y = heights[i + 1] - grades[i + 1] * length / 3f;
            }
        }

        /// <summary>Moves inner points towards the midpoint of their neighbours, in a few gentle passes.</summary>
        private static void RelaxPoints(float3[] points, float relax)
        {
            if (relax <= 0f || points.Length < 3)
                return;

            const int passes = 4;
            var next = new float3[points.Length];
            for (var pass = 0; pass < passes; pass++)
            {
                for (var i = 1; i < points.Length - 1; i++)
                {
                    var mid = (points[i - 1] + points[i + 1]) * 0.5f;
                    next[i] = LerpXZ(points[i], mid, relax);
                }
                for (var i = 1; i < points.Length - 1; i++)
                    points[i] = next[i];
            }
        }

        private static float Grade(float3 tangent) => tangent.y / math.max(math.length(tangent.xz), 0.001f);

        private static float3 LerpXZ(float3 from, float3 to, float t) => new float3(math.lerp(from.x, to.x, t), from.y, math.lerp(from.z, to.z, t));

        private static float3 Flat(float3 v) => math.normalizesafe(new float3(v.x, 0f, v.z));

        private static float3 Mirror(float3 direction, float3 axis) => math.normalizesafe(2f * math.dot(direction, axis) * axis - direction);

        /// <summary>Height and grade (rise per metre) at a horizontal distance along the reshaped chain.</summary>
        private readonly struct HeightProfile
        {
            private readonly Bezier4x3[] m_Original;
            private readonly float[] m_OriginalAlong;
            private readonly float m_Total;
            private readonly ShapeParams m_Shape;
            private readonly float m_StartHeight;
            private readonly float m_EndHeight;

            public HeightProfile(Bezier4x3[] original, float[] originalAlong, float total, ShapeParams shape, float startHeight, float endHeight)
            {
                m_Original = original;
                m_OriginalAlong = originalAlong;
                m_Total = total;
                m_Shape = shape;
                m_StartHeight = startHeight;
                m_EndHeight = endHeight;
            }

            public float Height(float s, out float grade)
            {
                var u = math.saturate(s / m_Total);
                float h;
                if (m_Shape.Profile == SlopeProfile.Keep)
                {
                    const float du = 0.002f;
                    h = OriginalHeight(u);
                    grade = (OriginalHeight(math.min(u + du, 1f)) - OriginalHeight(math.max(u - du, 0f))) / ((math.min(u + du, 1f) - math.max(u - du, 0f)) * m_Total);
                }
                else
                {
                    SlopeProfiles.Evaluate(m_Shape.Profile, m_Shape.Ease, u, out var f, out var df);
                    h = m_StartHeight + (m_EndHeight - m_StartHeight) * f;
                    grade = (m_EndHeight - m_StartHeight) * df / m_Total;
                }

                h += math.lerp(m_Shape.StartOffset, m_Shape.EndOffset, u);
                grade += (m_Shape.EndOffset - m_Shape.StartOffset) / m_Total;

                // Arch: a bump (or dip, if negative) peaking at the middle.
                h += m_Shape.Arch * 4f * u * (1f - u);
                grade += m_Shape.Arch * 4f * (1f - 2f * u) / m_Total;
                return h;
            }

            /// <summary>Current road height at a fraction of the original horizontal length.</summary>
            private float OriginalHeight(float u)
            {
                var s = u * m_OriginalAlong[m_OriginalAlong.Length - 1];
                var i = 0;
                while (i < m_Original.Length - 1 && s > m_OriginalAlong[i + 1])
                    i++;
                var t = math.saturate((s - m_OriginalAlong[i]) / (m_OriginalAlong[i + 1] - m_OriginalAlong[i]));
                return MathUtils.Position(m_Original[i], t).y;
            }
        }

        /// <summary>Re-curves an edge whose end nodes stay where they are, keeping the edge (m_Original).</summary>
        private static void EmitInPlace(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, Entity edge, Entity from, Entity to, Bezier4x3 curve, int randomSeed)
        {
            var e = em.GetComponentData<Edge>(edge);
            if (e.m_Start != from)
                curve = MathUtils.Invert(curve);

            var none = new Dictionary<Entity, float3>();
            var hasElevation = em.HasComponent<Elevation>(edge);
            var startPos = ChainEnd(em, ref terrain, e.m_Start, none, curve, start: true, hasElevation);
            var endPos = ChainEnd(em, ref terrain, e.m_End, none, curve, start: false, hasElevation);

            NetDefinitions.Emit(ecb, new CreationDefinition
            {
                m_Original = edge,
                m_Prefab = em.GetComponentData<PrefabRef>(edge).m_Prefab,
                m_RandomSeed = randomSeed,
            }, new NetCourse
            {
                m_Curve = curve,
                m_StartPosition = startPos,
                m_EndPosition = endPos,
                m_Elevation = new float2(startPos.m_Elevation.x, endPos.m_Elevation.x),
                m_Length = MathUtils.Length(curve),
                m_FixedIndex = -1,
            });
        }

        internal static void EmitSideEdge(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, Entity side, Edge e, Dictionary<Entity, float3> newPositions, int randomSeed)
        {
            var b = em.GetComponentData<Curve>(side).m_Bezier;
            var hasElevation = em.HasComponent<Elevation>(side);

            // Shift each moved end, and its neighbouring control point, by the node's movement.
            if (newPositions.TryGetValue(e.m_Start, out var newStart))
            {
                var delta = newStart - b.a;
                b.a += delta;
                b.b += delta;
            }
            if (newPositions.TryGetValue(e.m_End, out var newEnd))
            {
                var delta = newEnd - b.d;
                b.d += delta;
                b.c += delta;
            }

            var startPos = ChainEnd(em, ref terrain, e.m_Start, newPositions, b, start: true, hasElevation);
            var endPos = ChainEnd(em, ref terrain, e.m_End, newPositions, b, start: false, hasElevation);

            NetDefinitions.Emit(ecb, new CreationDefinition
            {
                m_Original = side,
                m_Prefab = em.GetComponentData<PrefabRef>(side).m_Prefab,
                m_RandomSeed = randomSeed,
            }, new NetCourse
            {
                m_Curve = b,
                m_StartPosition = startPos,
                m_EndPosition = endPos,
                m_Elevation = new float2(startPos.m_Elevation.x, endPos.m_Elevation.x),
                m_Length = MathUtils.Length(b),
                m_FixedIndex = -1,
            });
        }

        /// <summary>
        /// One end of a course: an existing node if it isn't moving, otherwise a new node (m_Entity null)
        /// at its new position with its elevation above terrain.
        /// </summary>
        internal static CoursePos ChainEnd(EntityManager em, ref TerrainHeightData terrain, Entity node, Dictionary<Entity, float3> newPositions, Bezier4x3 curve, bool start, bool hasElevation)
        {
            var tangent = start ? MathUtils.StartTangent(curve) : MathUtils.EndTangent(curve);
            var pos = new CoursePos
            {
                m_Rotation = NetUtils.GetNodeRotation(tangent),
                m_CourseDelta = start ? 0f : 1f,
                m_Flags = start ? CoursePosFlags.IsFirst : CoursePosFlags.IsLast,
                m_ParentMesh = -1,
            };

            if (newPositions.TryGetValue(node, out var moved))
            {
                // A new node is shared by several courses, and the game only joins them into one node if
                // they agree exactly. So the elevation is the true height above ground (the same for every
                // course, and it stops the game pulling a raised ground road back down), and the end
                // flags are left off: IsFirst/IsLast mark the loose ends of a drawn road, and on a shared
                // joint they make the game treat each course's end on its own.
                pos.m_Entity = Entity.Null;
                pos.m_Position = moved;
                pos.m_Elevation = new float2(moved.y - TerrainUtils.SampleHeight(ref terrain, moved));
                pos.m_Flags = 0;
            }
            else
            {
                pos.m_Entity = node;
                pos.m_Position = em.GetComponentData<Node>(node).m_Position;
                pos.m_Elevation = em.TryGetComponent(node, out Elevation elevation) ? elevation.m_Elevation : float2.zero;
            }

            return pos;
        }

        internal static bool IsEditableNode(EntityManager em, Entity node)
        {
            return em.HasComponent<Node>(node) && !em.HasComponent<Owner>(node) && em.HasBuffer<ConnectedEdge>(node);
        }
    }
}
