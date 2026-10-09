using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Mathematics;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkToolsReworked.Edits
{
    public enum LiftMode
    {
        /// <summary>Lift high enough to clear every road crossing the stretch, by the set clearance.</summary>
        OverCrossings,

        /// <summary>Lift by a set height: a bridge or flyover.</summary>
        Raise,

        /// <summary>Lower by a set depth: a tunnel or underpass.</summary>
        Lower,
    }

    public struct LiftResult
    {
        /// <summary>New pieces, in order from the start node.</summary>
        public List<Bezier4x3> Curves;

        /// <summary>Height change in the middle of the stretch, metres (negative when lowered).</summary>
        public float Height;

        /// <summary>Length of each approach slope, metres.</summary>
        public float RampLength;

        public float MaxGrade;
        public float GradeLimit;

        /// <summary>The stretch is too short for this height within the grade limit.</summary>
        public bool TooShort;

        /// <summary>Over crossings: number of roads crossed, and how many aren't cleared (they sit on a slope).</summary>
        public int Crossings;
        public int Uncleared;

        /// <summary>Other roads joined to the stretch between its ends; they get lifted with it.</summary>
        public int SideRoads;
    }

    /// <summary>
    /// Lifts (or lowers) the middle of a stretch of road, with approach slopes at both ends that stay
    /// within the road type's grade limit. The stretch is rebuilt like the Slope tool does: the old
    /// pieces are removed and new ones built through new nodes, with extra nodes where the slopes start
    /// and end so the shape holds. The end nodes stay where they are. Every new point gets its height
    /// above the ground as its elevation, so the game makes the high parts bridges and the deep parts
    /// tunnels, as it does when building with the elevation keys.
    /// </summary>
    public static class LiftEdit
    {
        /// <summary>Longest piece on an approach slope, metres.</summary>
        private const float kRampPiece = 40f;

        /// <summary>The slopes use this share of the grade limit, leaving room for the road's own grade.</summary>
        private const float kGradeShare = 0.9f;

        private const int kTableSamples = 64;

        /// <summary>A road crossing the stretch: where along it (metres from the start) and how high.</summary>
        public struct Crossing
        {
            public float Along;
            public float PathHeight;
            public float OtherHeight;
            public float3 Position;
        }

        /// <summary>Finds the roads crossing the stretch (seen from above), other than those joined to it.</summary>
        public static void FindCrossings(EntityManager em, EntityQuery edgeQuery, List<Entity> nodes, List<Entity> edges, List<Crossing> crossings)
        {
            crossings.Clear();
            var curves = Oriented(em, nodes, edges);
            var ignore = Clearance.Neighbourhood(em, nodes, edges);
            var hits = new List<Crossings.Hit>();
            using var others = edgeQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var start = 0f;
            for (var i = 0; i < curves.Length; i++)
            {
                var length = MathUtils.Length(curves[i].xz);
                foreach (var other in others)
                {
                    if (ignore.Contains(other))
                        continue;
                    hits.Clear();
                    Edits.Crossings.Find(curves[i], em.GetComponentData<Curve>(other).m_Bezier, hits, endMargin: 2f);
                    foreach (var hit in hits)
                        crossings.Add(new Crossing
                        {
                            Along = start + MathUtils.Length(MathUtils.Cut(curves[i], new float2(0f, hit.TA)).xz),
                            PathHeight = hit.PointA.y,
                            OtherHeight = hit.PointB.y,
                            Position = hit.PointA,
                        });
                }
                start += length;
            }
        }

        /// <param name="height">Raise or Lower: metres. Over crossings: ignored.</param>
        /// <param name="clearance">Over crossings: height kept above each crossing road, metres.</param>
        public static bool Emit(EntityManager em, EntityCommandBuffer ecb, ref TerrainHeightData terrain, List<Entity> nodes, List<Entity> edges, LiftMode mode, float height, float clearance, List<Crossing> crossings, int randomSeed, out LiftResult result)
        {
            result = new LiftResult { Curves = new List<Bezier4x3>() };
            var curves = Oriented(em, nodes, edges);
            var count = curves.Length;

            // Horizontal length at each node and the length-to-t table of each piece.
            var along = new float[count + 1];
            var tables = new float[count][];
            for (var i = 0; i < count; i++)
            {
                tables[i] = LengthTable(curves[i]);
                along[i + 1] = along[i] + tables[i][kTableSamples];
            }
            var total = along[count];
            if (total < 1f)
                return false;

            var limit = 1f;
            foreach (var edge in edges)
                limit = math.min(limit, GradeLimit(em, edge));
            result.GradeLimit = limit;

            // Smoothstep slopes are steepest (1.5x their average) at their middle.
            var rampFor = new System.Func<float, float>(h => 1.5f * math.abs(h) / (limit * kGradeShare));

            float lift;
            if (mode == LiftMode.OverCrossings)
            {
                if (crossings.Count == 0)
                    return false;
                lift = 0f;
                foreach (var c in crossings)
                    lift = math.max(lift, c.OtherHeight + clearance - c.PathHeight);
                if (lift < 0.1f)
                    return false;
                result.Crossings = crossings.Count;
            }
            else
            {
                lift = mode == LiftMode.Lower ? -math.abs(height) : math.abs(height);
                if (math.abs(lift) < 0.1f)
                    return false;
            }

            var ramp = rampFor(lift);
            if (2f * ramp > total)
            {
                result.TooShort = true;
                ramp = total * 0.5f;
            }
            result.Height = lift;
            result.RampLength = ramp;

            if (mode == LiftMode.OverCrossings)
                foreach (var c in crossings)
                    if (Offset(c.Along, total, ramp, lift) + c.PathHeight < c.OtherHeight + clearance - 0.05f)
                        result.Uncleared++;

            // Break points: every node, both ends of each slope, and extra points along the slopes.
            var breaks = new List<float>(along);
            AddRamp(breaks, 0f, ramp);
            AddRamp(breaks, total - ramp, total);
            breaks.Sort();
            var points = new List<float>();
            foreach (var s in breaks)
            {
                // Slope points too close to another point are dropped; nodes are always kept, since
                // every piece has to lie on one of the old pieces.
                var isNode = IsNode(along, s);
                if (points.Count > 0 && s - points[points.Count - 1] < 2f)
                {
                    if (!isNode)
                        continue;
                    if (!IsNode(along, points[points.Count - 1]))
                    {
                        points[points.Count - 1] = s;
                        continue;
                    }
                    if (s - points[points.Count - 1] < 0.01f)
                        continue;
                }
                points.Add(s);
            }
            points[0] = 0f;
            points[points.Count - 1] = total;

            // New positions of the inner nodes; the end nodes stay.
            var newPositions = new Dictionary<Entity, float3>();
            for (var i = 1; i < count; i++)
            {
                var p = em.GetComponentData<Node>(nodes[i]).m_Position;
                p.y += Offset(along[i], total, ramp, lift);
                newPositions[nodes[i]] = p;
            }

            for (var i = 0; i < count; i++)
                NetDefinitions.DeleteEdge(em, ecb, edges[i], randomSeed);

            var maxGrade = 0f;
            for (var k = 0; k + 1 < points.Count; k++)
            {
                var sa = points[k];
                var sb = points[k + 1];
                var i = EdgeAt(along, (sa + sb) * 0.5f);
                var ta = TAt(tables[i], sa - along[i]);
                var tb = TAt(tables[i], sb - along[i]);
                var piece = MathUtils.Cut(curves[i], new float2(ta, tb));

                var ha = Offset(sa, total, ramp, lift);
                var hb = Offset(sb, total, ramp, lift);
                var ga = CurveStats.Grade(curves[i], ta) + Slope(sa, total, ramp, lift);
                var gb = CurveStats.Grade(curves[i], tb) + Slope(sb, total, ramp, lift);
                piece.a.y += ha;
                piece.d.y += hb;
                piece.b.y = piece.a.y + ga * math.distance(piece.a.xz, piece.b.xz);
                piece.c.y = piece.d.y - gb * math.distance(piece.c.xz, piece.d.xz);
                maxGrade = math.max(maxGrade, CurveStats.MaxGrade(piece));

                var startPos = End(em, ref terrain, nodes, along, sa, piece, start: true, newPositions);
                var endPos = End(em, ref terrain, nodes, along, sb, piece, start: false, newPositions);
                var definition = NetDefinitions.Emit(ecb, new CreationDefinition
                {
                    m_Prefab = em.GetComponentData<PrefabRef>(edges[i]).m_Prefab,
                    m_RandomSeed = randomSeed,
                    m_Flags = CreationFlags.SubElevation,
                }, new NetCourse
                {
                    m_Curve = piece,
                    m_StartPosition = startPos,
                    m_EndPosition = endPos,
                    m_Elevation = new float2(startPos.m_Elevation.x, endPos.m_Elevation.x),
                    m_Length = MathUtils.Length(piece),
                    m_FixedIndex = -1,
                });
                if (em.TryGetComponent(edges[i], out Upgraded upgraded))
                    ecb.AddComponent(definition, upgraded);
                result.Curves.Add(piece);
            }
            result.MaxGrade = maxGrade;

            // Roads joined to the lifted inner nodes follow them up or down.
            var chain = new HashSet<Entity>(edges);
            var handled = new HashSet<Entity>();
            foreach (var node in newPositions.Keys)
            {
                foreach (var connected in em.GetBuffer<ConnectedEdge>(node, isReadOnly: true))
                {
                    var side = connected.m_Edge;
                    if (chain.Contains(side) || !handled.Add(side) || !em.TryGetComponent(side, out Edge e) || (e.m_Start != node && e.m_End != node))
                        continue;
                    SlopeEdit.EmitSideEdge(em, ecb, ref terrain, side, e, newPositions, randomSeed);
                    result.SideRoads++;
                }
            }
            return true;
        }

        /// <summary>Height added at <paramref name="s"/> metres along: smooth slopes at both ends, level between.</summary>
        private static float Offset(float s, float total, float ramp, float lift)
        {
            if (ramp <= 0f)
                return 0f;
            var u = math.saturate(math.min(s, total - s) / ramp);
            return lift * u * u * (3f - 2f * u);
        }

        /// <summary>Rate of change of <see cref="Offset"/> along the stretch (an added grade).</summary>
        private static float Slope(float s, float total, float ramp, float lift)
        {
            if (ramp <= 0f)
                return 0f;
            var fromStart = s <= total - s;
            var u = math.min(s, total - s) / ramp;
            if (u >= 1f)
                return 0f;
            var d = lift * 6f * u * (1f - u) / ramp;
            return fromStart ? d : -d;
        }

        /// <summary>
        /// One end of a new piece: the original node at either end of the stretch, a lifted inner node, or
        /// a new node on a slope. New points get their height above the ground as their elevation.
        /// </summary>
        private static CoursePos End(EntityManager em, ref TerrainHeightData terrain, List<Entity> nodes, float[] along, float s, Bezier4x3 piece, bool start, Dictionary<Entity, float3> newPositions)
        {
            var node = NodeAt(nodes, along, s);
            if (node != Entity.Null && !newPositions.ContainsKey(node))
                return SlopeEdit.ChainEnd(em, ref terrain, node, newPositions, piece, start, hasElevation: true);

            var p = start ? piece.a : piece.d;
            var elevation = p.y - TerrainUtils.SampleHeight(ref terrain, p);
            if (math.abs(elevation) < 0.5f)
                elevation = 0f;
            return new CoursePos
            {
                m_Entity = Entity.Null,
                m_Position = p,
                m_Rotation = NetUtils.GetNodeRotation(start ? MathUtils.StartTangent(piece) : MathUtils.EndTangent(piece)),
                m_Elevation = new float2(elevation),
                m_CourseDelta = start ? 0f : 1f,
                m_Flags = start ? CoursePosFlags.IsFirst : CoursePosFlags.IsLast,
                m_ParentMesh = -1,
            };
        }

        private static void AddRamp(List<float> breaks, float from, float to)
        {
            var pieces = math.max(1, (int)math.ceil((to - from) / kRampPiece));
            for (var k = 0; k <= pieces; k++)
                breaks.Add(math.lerp(from, to, (float)k / pieces));
        }

        private static bool IsNode(float[] along, float s)
        {
            foreach (var a in along)
                if (math.abs(a - s) < 0.01f)
                    return true;
            return false;
        }

        private static Entity NodeAt(List<Entity> nodes, float[] along, float s)
        {
            for (var i = 0; i < along.Length; i++)
                if (math.abs(along[i] - s) < 0.01f)
                    return nodes[i];
            return Entity.Null;
        }

        private static int EdgeAt(float[] along, float s)
        {
            for (var i = 0; i + 1 < along.Length; i++)
                if (s <= along[i + 1])
                    return i;
            return along.Length - 2;
        }

        /// <summary>Horizontal length from the start at kTableSamples + 1 evenly spaced t values.</summary>
        private static float[] LengthTable(Bezier4x3 curve)
        {
            var table = new float[kTableSamples + 1];
            var previous = curve.a.xz;
            for (var k = 1; k <= kTableSamples; k++)
            {
                var p = MathUtils.Position(curve, (float)k / kTableSamples).xz;
                table[k] = table[k - 1] + math.distance(previous, p);
                previous = p;
            }
            return table;
        }

        private static float TAt(float[] table, float length)
        {
            if (length <= 0f)
                return 0f;
            for (var k = 1; k <= kTableSamples; k++)
            {
                if (table[k] >= length)
                {
                    var span = table[k] - table[k - 1];
                    var f = span > 1e-5f ? (length - table[k - 1]) / span : 0f;
                    return (k - 1 + f) / kTableSamples;
                }
            }
            return 1f;
        }

        private static Bezier4x3[] Oriented(EntityManager em, List<Entity> nodes, List<Entity> edges)
        {
            var curves = new Bezier4x3[edges.Count];
            for (var i = 0; i < edges.Count; i++)
            {
                var b = em.GetComponentData<Curve>(edges[i]).m_Bezier;
                curves[i] = em.GetComponentData<Edge>(edges[i]).m_Start == nodes[i] ? b : MathUtils.Invert(b);
            }
            return curves;
        }

        private static float GradeLimit(EntityManager em, Entity edge)
        {
            if (em.TryGetComponent(edge, out PrefabRef prefab) && em.TryGetComponent(prefab.m_Prefab, out NetGeometryData geometry) && geometry.m_MaxSlopeSteepness > 0f)
                return geometry.m_MaxSlopeSteepness;
            return 0.12f;
        }
    }
}
