using System.Collections.Generic;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace NetworkToolsReworked.Edits
{
    /// <summary>
    /// The roundabout central islands the game offers (and any a mod adds): object prefabs whose
    /// NetObjectData carries the roundabout flag.
    /// </summary>
    public static class Islands
    {
        public struct Island
        {
            public Entity Prefab;
            public string Name;
            public string Icon;
        }

        public static ComponentType[] QueryTypes => new[] { ComponentType.ReadOnly<PrefabData>(), ComponentType.ReadOnly<NetObjectData>() };

        /// <summary>Every island, sorted by name.</summary>
        public static List<Island> List(EntityManager em, PrefabSystem prefabSystem, EntityQuery query)
        {
            var list = new List<Island>();
            using (var entities = query.ToEntityArray(Allocator.Temp))
            {
                foreach (var entity in entities)
                {
                    if (!RoundaboutEdit.IsIslandPrefab(em, entity))
                        continue;
                    var prefab = prefabSystem.GetPrefab<PrefabBase>(entity);
                    if (prefab == null)
                        continue;
                    var icon = prefab.TryGet(out UIObject ui) ? ui.m_Icon : string.Empty;
                    list.Add(new Island { Prefab = entity, Name = prefab.name, Icon = icon ?? string.Empty });
                }
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return list;
        }

        /// <summary>The island called <paramref name="name"/>, else the first small one, else the first.</summary>
        public static Entity Pick(List<Island> islands, string name)
        {
            if (islands.Count == 0)
                return Entity.Null;
            foreach (var island in islands)
                if (island.Name == name)
                    return island.Prefab;
            foreach (var island in islands)
                if (island.Name.Contains("Small"))
                    return island.Prefab;
            return islands[0].Prefab;
        }
    }
}
