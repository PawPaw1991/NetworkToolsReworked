using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Mathematics;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkToolsReworked.Edits
{
    /// <summary>How a Parallel copy's offset changes along the stretch.</summary>
    public enum ParallelTaper
    {
        /// <summary>The same offset all along.</summary>
        Off,

        /// <summary>Starts at the start node and moves out to the full offset: a lane split or exit.</summary>
        Split,

        /// <summary>Starts at the full offset and comes in to the end node: a merge.</summary>
        Merge,
    }

    /// <summary>
    /// Emits a new road alongside a chain of edges, offset sideways and vertically. Each edge becomes
    /// one new course of the same type and upgrades; new nodes are shared between consecutive courses
    /// so the copy is one continuous road. With a taper the offset changes smoothly along the stretch
    /// from (or to) nothing, and the copy joins the original road at that end node.
    /// </summary>
    public static class ParallelEdit
    {
        /// <param name="offset">Sideways distance in metres; positive is to the right of start → end.</param>
        /// <param name="height">Vertical offset in metres.</param>
        /// <param name="reverse">Build the copy running end → start (for one-way roads).</param>
        public static void Emit(EntityManager em, EntityCommandBuffer ecb, List<Entity> nodes, List<Entity> edges, float offset, float height, bool reverse, int randomSeed)
        {
            Emit(em, ecb, nodes, edges, offset, height, reverse, ParallelTaper.Off, randomSeed);
        }

        public static void Emit(EntityManager em, EntityCommandBuffer ecb, List<Entity> nodes, List<Entity> edges, float offset, float height, bool reverse, ParallelTaper taper, int randomSeed)
        {
            if (math.abs(offset) < 0.5f && math.abs(height) < 0.5f)
                return;
            if (taper != ParallelTaper.Off)
            {
                EmitTapered(em, ecb, nodes, edges, offset, height, reverse, taper, randomSeed);
                return;
            }

            var curves = new Bezier4x3[edges.Count];
            for (var i = 0; i < edges.Count; i++)
            {
                var bezier = em.GetComponentData<Curve>(edges[i]).m_Bezier;
                if (em.GetComponentData<Edge>(edges[i]).m_Start != nodes[i])
                    bezier = MathUtils.Invert(bezier);
                curves[i] = bezier;
            }

            // Offset each node once, along the average direction of the edges meeting there, so
            // consecutive courses share exactly the same new node position.
            var positions = new float3[nodes.Count];
            var elevations = new float[nodes.Count];
            for (var i = 0; i < nodes.Count; i++)
            {
                var direction = float3.zero;
                if (i > 0) direction += Flat(MathUtils.EndTangent(curves[i - 1]));
                if (i < edges.Count) direction += Flat(MathUtils.StartTangent(curves[i]));
                positions[i] = em.GetComponentData<Node>(nodes[i]).m_Position + Right(direction) * offset + new float3(0f, height, 0f);
                elevations[i] = (em.TryGetComponent(nodes[i], out Elevation elevation) ? elevation.m_Elevation.x : 0f) + height;
            }

            for (var i = 0; i < edges.Count; i++)
            {
                var source = curves[i];
                var a = positions[i];
                var d = positions[i + 1];

                // Keep the source curve's handle directions, scaled to the new end-to-end distance.
                var scale = math.distance(a, d) / math.max(math.distance(source.a, source.d), 0.01f);
                var curve = new Bezier4x3(a, a + (source.b - source.a) * scale, d + (source.c - source.d) * scale, d);
                var e0 = elevations[i];
                var e1 = elevations[i + 1];
                if (reverse)
                {
                    curve = MathUtils.Invert(curve);
                    (e0, e1) = (e1, e0);
                }

                var startPos = NewEnd(curve, start: true, e0);
                var endPos = NewEnd(curve, start: false, e1);
                ClearSharedFlags(ref startPos, ref endPos, i, edges.Count, reverse);

                var definition = NetDefinitions.Emit(ecb, new CreationDefinition
                {
                    m_Prefab = em.GetComponentData<PrefabRef>(edges[i]).m_Prefab,
                    m_RandomSeed = randomSeed,
                    m_Flags = CreationFlags.SubElevation,
                }, new NetCourse
                {
                    m_Curve = curve,
                    m_StartPosition = startPos,
                    m_EndPosition = endPos,
                    m_Elevation = new float2(e0, e1),
                    m_Length = MathUtils.Length(curve),
                    m_FixedIndex = -1,
                });

                if (em.TryGetComponent(edges[i], out Upgraded upgraded))
                    ecb.AddComponent(definition, upgraded);
            }
        }

        /// <summary>
        /// Copy whose offset (and height offset) eases from nothing to full, or back. Each control point is
        /// moved by the offset at its own place along the stretch, so the copy bends out smoothly; the end
        /// with no offset is joined to the original road's node there.
        /// </summary>
        private static void EmitTapered(EntityManager em, EntityCommandBuffer ecb, List<Entity> nodes, List<Entity> edges, float offset, float height, bool reverse, ParallelTaper taper, int randomSeed)
        {
            var count = edges.Count;
            var curves = new Bezier4x3[count];
            var along = new float[count + 1];
            for (var i = 0; i < count; i++)
            {
                var bezier = em.GetComponentData<Curve>(edges[i]).m_Bezier;
                if (em.GetComponentData<Edge>(edges[i]).m_Start != nodes[i])
                    bezier = MathUtils.Invert(bezier);
                curves[i] = bezier;
                along[i + 1] = along[i] + MathUtils.Length(bezier.xz);
            }
            var total = math.max(along[count], 0.01f);

            // Share of the full offset at a distance along the stretch.
            float Share(float s)
            {
                var u = math.saturate(s / total);
                if (taper == ParallelTaper.Merge)
                    u = 1f - u;
                return u * u * (3f - 2f * u);
            }

            var positions = new float3[count + 1];
            var elevations = new float[count + 1];
            for (var i = 0; i <= count; i++)
            {
                var direction = float3.zero;
                if (i > 0) direction += Flat(MathUtils.EndTangent(curves[i - 1]));
                if (i < count) direction += Flat(MathUtils.StartTangent(curves[i]));
                var share = Share(along[i]);
                positions[i] = em.GetComponentData<Node>(nodes[i]).m_Position + Right(direction) * offset * share + new float3(0f, height * share, 0f);
                elevations[i] = (em.TryGetComponent(nodes[i], out Elevation elevation) ? elevation.m_Elevation.x : 0f) + height * share;
            }

            var joined = taper == ParallelTaper.Split ? 0 : count;
            for (var i = 0; i < count; i++)
            {
                var source = curves[i];
                var length = along[i + 1] - along[i];
                var b = source.b + Right(Flat(MathUtils.Tangent(source, 1f / 3f))) * offset * Share(along[i] + length / 3f) + new float3(0f, height * Share(along[i] + length / 3f), 0f);
                var c = source.c + Right(Flat(MathUtils.Tangent(source, 2f / 3f))) * offset * Share(along[i] + length * 2f / 3f) + new float3(0f, height * Share(along[i] + length * 2f / 3f), 0f);
                var curve = new Bezier4x3(positions[i], b, c, positions[i + 1]);

                var startPos = i == joined ? Existing(em, nodes[i], curve, start: true) : NewEnd(curve, start: true, elevations[i]);
                var endPos = i + 1 == joined ? Existing(em, nodes[i + 1], curve, start: false) : NewEnd(curve, start: false, elevations[i + 1]);
                if (i == joined)
                    curve.a = startPos.m_Position;
                if (i + 1 == joined)
                    curve.d = endPos.m_Position;
                if (reverse)
                {
                    curve = MathUtils.Invert(curve);
                    (startPos, endPos) = (Flip(endPos, curve, start: true), Flip(startPos, curve, start: false));
                }
                ClearSharedFlags(ref startPos, ref endPos, i, count, reverse);

                var definition = NetDefinitions.Emit(ecb, new CreationDefinition
                {
                    m_Prefab = em.GetComponentData<PrefabRef>(edges[i]).m_Prefab,
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

                if (em.TryGetComponent(edges[i], out Upgraded upgraded))
                    ecb.AddComponent(definition, upgraded);
            }
        }

        /// <summary>An end on the original road's node, where a tapered copy joins it.</summary>
        private static CoursePos Existing(EntityManager em, Entity node, Bezier4x3 curve, bool start)
        {
            return new CoursePos
            {
                m_Entity = node,
                m_Position = em.GetComponentData<Node>(node).m_Position,
                m_Rotation = NetUtils.GetNodeRotation(start ? MathUtils.StartTangent(curve) : MathUtils.EndTangent(curve)),
                m_Elevation = em.TryGetComponent(node, out Elevation elevation) ? elevation.m_Elevation : float2.zero,
                m_CourseDelta = start ? 0f : 1f,
                m_Flags = start ? CoursePosFlags.IsFirst : CoursePosFlags.IsLast,
                m_ParentMesh = -1,
            };
        }

        /// <summary>
        /// The copy's inner nodes are shared by the courses either side, so they carry no end flags
        /// (IsFirst/IsLast mark loose road ends; see SlopeEdit.ChainEnd). Only new nodes are cleared;
        /// ends on an existing node keep theirs.
        /// </summary>
        private static void ClearSharedFlags(ref CoursePos startPos, ref CoursePos endPos, int index, int count, bool reverse)
        {
            var firstShared = index > 0;
            var lastShared = index + 1 < count;
            if (reverse)
                (firstShared, lastShared) = (lastShared, firstShared);
            if (firstShared && startPos.m_Entity == Entity.Null)
                startPos.m_Flags = 0;
            if (lastShared && endPos.m_Entity == Entity.Null)
                endPos.m_Flags = 0;
        }

        /// <summary>The same end used at the other end of a reversed course.</summary>
        private static CoursePos Flip(CoursePos pos, Bezier4x3 curve, bool start)
        {
            pos.m_Rotation = NetUtils.GetNodeRotation(start ? MathUtils.StartTangent(curve) : MathUtils.EndTangent(curve));
            pos.m_CourseDelta = start ? 0f : 1f;
            pos.m_Flags = start ? CoursePosFlags.IsFirst : CoursePosFlags.IsLast;
            return pos;
        }

        private static CoursePos NewEnd(Bezier4x3 curve, bool start, float elevation)
        {
            return new CoursePos
            {
                m_Entity = Entity.Null,
                m_Position = start ? curve.a : curve.d,
                m_Rotation = NetUtils.GetNodeRotation(start ? MathUtils.StartTangent(curve) : MathUtils.EndTangent(curve)),
                m_Elevation = new float2(elevation),
                m_CourseDelta = start ? 0f : 1f,
                m_Flags = start ? CoursePosFlags.IsFirst : CoursePosFlags.IsLast,
                m_ParentMesh = -1,
            };
        }

        private static float3 Flat(float3 v)
        {
            v.y = 0f;
            return math.lengthsq(v) > 0.000001f ? math.normalize(v) : float3.zero;
        }

        /// <summary>Unit vector to the right of a horizontal travel direction.</summary>
        private static float3 Right(float3 direction)
        {
            var right = new float3(direction.z, 0f, -direction.x);
            return math.lengthsq(right) > 0.000001f ? math.normalize(right) : float3.zero;
        }
    }
}
