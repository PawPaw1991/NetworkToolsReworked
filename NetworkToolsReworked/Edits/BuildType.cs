using Game.Common;
using Unity.Entities;

namespace NetworkToolsReworked.Edits
{
    /// <summary>
    /// The road type new roads are built as, shared by every tool that builds new roads (Connect,
    /// Parallel, Roundabout, Ramp, Helix). Nothing chosen means each tool uses the type of the road
    /// it starts from. A choice of a different kind (a track when building off a road) is ignored.
    /// </summary>
    public static class BuildType
    {
        public static Entity Prefab { get; private set; }

        public static string Name { get; private set; } = string.Empty;

        public static void Set(Entity prefab, string name)
        {
            Prefab = prefab;
            Name = name ?? string.Empty;
        }

        public static void Clear() => Set(Entity.Null, string.Empty);

        /// <summary>True if a type is chosen and still loaded.</summary>
        public static bool IsSet(EntityManager em)
        {
            if (Prefab == Entity.Null)
                return false;
            if (!em.Exists(Prefab) || em.HasComponent<Deleted>(Prefab))
            {
                Clear();
                return false;
            }
            return true;
        }

        /// <summary>The chosen type if it is the same kind of network as <paramref name="roadPrefab"/>, else that prefab.</summary>
        public static Entity For(EntityManager em, Entity roadPrefab)
        {
            if (!IsSet(em))
                return roadPrefab;
            if (roadPrefab == Entity.Null || RestyleEdit.KindOf(em, Prefab) == RestyleEdit.KindOf(em, roadPrefab))
                return Prefab;
            return roadPrefab;
        }
    }
}
