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
    /// <summary>One road of a layout, independent of where it was taken from.</summary>
    public struct LayoutRoad
    {
        /// <summary>Control points: x and z relative to the layout's anchor, y as height above the ground there.</summary>
        public Bezier4x3 Curve;

        public Entity Prefab;

        /// <summary>Prefab name and type, so a saved layout can find the road type again in another city.</summary>
        public string PrefabName;
        public string PrefabType;

        public bool HasUpgrades;
        public Upgraded Upgrades;

        /// <summary>True for raised or sunken roads (bridges, tunnels), which keep their height above ground.</summary>
        public bool Elevated;
    }

    /// <summary>A group of roads that can be placed again elsewhere: moved, rotated or mirrored.</summary>
    public sealed class Layout
    {
        public string Name = string.Empty;
        public readonly List<LayoutRoad> Roads = new List<LayoutRoad>();

        /// <summary>Main direction of the group (horizontal, unit), which angles are measured from.</summary>
        public float3 Direction = new float3(1f, 0f, 0f);

        public float Length
        {
            get
            {
                var total = 0f;
                foreach (var road in Roads)
                    total += MathUtils.Length(road.Curve.xz);
                return total;
            }
        }
    }

    public enum DuplicateMode
    {
        /// <summary>Same shape, turned by the set angle.</summary>
        Copy,

        /// <summary>Mirror image across an axis at the set angle to the group's main direction.</summary>
        Mirror,
    }

    /// <summary>
    /// Takes a snapshot of roads as a <see cref="Layout"/> and builds a layout again at another spot. Each
    /// road becomes one new course; ends at the same spot share a node, so the copy is joined up the
    /// same way. Heights follow the ground at the new spot: ground roads stay on the ground and
    /// bridges keep their height above it.
    /// </summary>
    public static class LayoutEdit
    {
        public static Layout Capture(EntityManager em, PrefabSystem prefabs, ref TerrainHeightData terrain, IEnumerable<Entity> edges)
        {
            var layout = new Layout();
            var curves = new List<(Entity edge, Bezier4x3 curve)>();
            var min = new float2(float.MaxValue);
            var max = new float2(float.MinValue);
            foreach (var edge in edges)
            {
                var curve = em.GetComponentData<Curve>(edge).m_Bezier;
                curves.Add((edge, curve));
                var bounds = MathUtils.Bounds(curve.xz);
                min = math.min(min, bounds.min);
                max = math.max(max, bounds.max);
            }
            if (curves.Count == 0)
                return layout;

            var anchor = (min + max) * 0.5f;
            var direction = float3.zero;
            foreach (var (edge, curve) in curves)
            {
                var prefab = em.GetComponentData<PrefabRef>(edge).m_Prefab;
                var asset = prefabs.GetPrefab<PrefabBase>(prefab);
                var hasUpgrades = em.TryGetComponent(edge, out Upgraded upgraded);
                var road = new LayoutRoad
                {
                    Curve = Relative(ref terrain, curve, anchor),
                    Prefab = prefab,
                    PrefabName = asset != null ? asset.name : string.Empty,
                    PrefabType = asset != null ? asset.GetType().Name : string.Empty,
                    HasUpgrades = hasUpgrades,
                    Upgrades = upgraded,
                    Elevated = em.HasComponent<Elevation>(edge),
                };
                layout.Roads.Add(road);

                // Longest straight-line run decides the main direction; opposite runs count the same.
                var run = curve.d.xz - curve.a.xz;
                if (math.dot(run, direction.xz) < 0f)
                    run = -run;
                direction += new float3(run.x, 0f, run.y);
            }
            layout.Direction = math.lengthsq(direction) > 0.01f ? math.normalize(direction) : new float3(1f, 0f, 0f);
            return layout;
        }

        /// <summary>Where each road of the layout ends up, for the preview and for building.</summary>
        public static List<Bezier4x3> Place(ref TerrainHeightData terrain, Layout layout, float3 target, DuplicateMode mode, float angle)
        {
            var placed = new List<Bezier4x3>(layout.Roads.Count);
            var m = Transform(layout.Direction, mode, angle);
            foreach (var road in layout.Roads)
            {
                var c = road.Curve;
                placed.Add(new Bezier4x3(
                    Point(ref terrain, c.a, m, target),
                    Point(ref terrain, c.b, m, target),
                    Point(ref terrain, c.c, m, target),
                    Point(ref terrain, c.d, m, target)));
            }
            return placed;
        }

        public static void Emit(EntityCommandBuffer ecb, ref TerrainHeightData terrain, Layout layout, List<Bezier4x3> placed, DuplicateMode mode, int randomSeed)
        {
            for (var i = 0; i < layout.Roads.Count; i++)
            {
                var road = layout.Roads[i];
                if (road.Prefab == Entity.Null)
                    continue;
                var curve = placed[i];
                var startPos = End(ref terrain, curve, start: true, road.Elevated);
                var endPos = End(ref terrain, curve, start: false, road.Elevated);
                var definition = NetDefinitions.Emit(ecb, new CreationDefinition
                {
                    m_Prefab = road.Prefab,
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

                if (road.HasUpgrades)
                {
                    var upgrades = road.Upgrades;
                    // A mirror image has its left and right swapped.
                    if (mode == DuplicateMode.Mirror)
                        (upgrades.m_Flags.m_Left, upgrades.m_Flags.m_Right) = (upgrades.m_Flags.m_Right, upgrades.m_Flags.m_Left);
                    ecb.AddComponent(definition, upgrades);
                }
            }
        }

        /// <summary>2x2 horizontal transform: a turn by the angle, or a mirror across an axis at the angle to the main direction.</summary>
        private static float2x2 Transform(float3 direction, DuplicateMode mode, float angleDegrees)
        {
            var angle = math.radians(angleDegrees);
            if (mode == DuplicateMode.Copy)
            {
                var cos = math.cos(angle);
                var sin = math.sin(angle);
                return new float2x2(cos, -sin, sin, cos);
            }

            // Reflection across a line at angle phi: [[cos 2phi, sin 2phi], [sin 2phi, -cos 2phi]].
            var phi = math.atan2(direction.z, direction.x) + angle;
            var c2 = math.cos(2f * phi);
            var s2 = math.sin(2f * phi);
            return new float2x2(c2, s2, s2, -c2);
        }

        private static float3 Point(ref TerrainHeightData terrain, float3 relative, float2x2 m, float3 target)
        {
            var xz = math.mul(m, relative.xz) + target.xz;
            var p = new float3(xz.x, 0f, xz.y);
            p.y = TerrainUtils.SampleHeight(ref terrain, p) + relative.y;
            return p;
        }

        private static Bezier4x3 Relative(ref TerrainHeightData terrain, Bezier4x3 curve, float2 anchor)
        {
            return new Bezier4x3(Rel(ref terrain, curve.a, anchor), Rel(ref terrain, curve.b, anchor), Rel(ref terrain, curve.c, anchor), Rel(ref terrain, curve.d, anchor));
        }

        private static float3 Rel(ref TerrainHeightData terrain, float3 p, float2 anchor)
        {
            var above = p.y - TerrainUtils.SampleHeight(ref terrain, p);
            return new float3(p.x - anchor.x, above, p.z - anchor.y);
        }

        private static CoursePos End(ref TerrainHeightData terrain, Bezier4x3 curve, bool start, bool elevated)
        {
            var p = start ? curve.a : curve.d;
            var above = p.y - TerrainUtils.SampleHeight(ref terrain, p);
            return new CoursePos
            {
                m_Entity = Entity.Null,
                m_Position = p,
                m_Rotation = NetUtils.GetNodeRotation(start ? MathUtils.StartTangent(curve) : MathUtils.EndTangent(curve)),
                m_Elevation = new float2(elevated && math.abs(above) >= 0.5f ? above : 0f),
                m_CourseDelta = start ? 0f : 1f,
                m_Flags = start ? CoursePosFlags.IsFirst : CoursePosFlags.IsLast,
                m_ParentMesh = -1,
            };
        }
    }
}
