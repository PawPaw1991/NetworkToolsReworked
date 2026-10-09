using System;
using System.Collections.Generic;
using Colossal.Entities;
using Game.Common;
using Game.Net;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace NetworkToolsReworked.Tools
{
    /// <summary>
    /// Reads the roads currently selected in the Move It mod, if it is installed. There is no build-time
    /// reference to Move It: its selection marker component is looked up by name among the loaded
    /// assemblies once, and if it isn't there the selection is simply unavailable.
    /// </summary>
    internal static class MoveItSelection
    {
        /// <summary>Names the selection marker component may have, checked in order.</summary>
        private static readonly string[] kComponentNames = { "MIT_Selected", "Selected" };

        private static bool s_Searched;
        private static Type s_Type;
        private static World s_QueryWorld;
        private static EntityQuery s_Query;

        /// <summary>True if Move It is loaded and its selection marker was found.</summary>
        public static bool Available => FindType() != null;

        /// <summary>
        /// Adds the selected plain roads to <paramref name="edges"/>: roads selected themselves, and roads
        /// whose two end nodes are both selected. Returns false if Move It isn't available.
        /// </summary>
        public static bool Collect(EntityManager em, World world, HashSet<Entity> edges)
        {
            edges.Clear();
            var type = FindType();
            if (type == null)
                return false;

            if (s_QueryWorld != world)
            {
                s_Query = em.CreateEntityQuery(ComponentType.ReadOnly(type));
                s_QueryWorld = world;
            }

            var nodes = new HashSet<Entity>();
            using (var selected = s_Query.ToEntityArray(Allocator.Temp))
            {
                foreach (var entity in selected)
                {
                    if (!em.Exists(entity) || em.HasComponent<Deleted>(entity) || em.HasComponent<Temp>(entity) || em.HasComponent<Owner>(entity))
                        continue;
                    if (em.HasComponent<Edge>(entity))
                        edges.Add(entity);
                    else if (em.HasComponent<Node>(entity))
                        nodes.Add(entity);
                }
            }

            foreach (var node in nodes)
            {
                if (!em.HasBuffer<ConnectedEdge>(node))
                    continue;
                foreach (var connected in em.GetBuffer<ConnectedEdge>(node, isReadOnly: true))
                {
                    var edge = connected.m_Edge;
                    if (em.HasComponent<Owner>(edge) || !em.TryGetComponent(edge, out Edge e))
                        continue;
                    if (nodes.Contains(e.m_Start) && nodes.Contains(e.m_End))
                        edges.Add(edge);
                }
            }

            if (Mod.Settings.DebugLogging)
                Mod.Log.Info($"Move It selection ({type.FullName}): {edges.Count} roads from {nodes.Count} nodes");
            return true;
        }

        private static Type FindType()
        {
            if (s_Searched)
                return s_Type;
            s_Searched = true;

            foreach (var name in kComponentNames)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name.IndexOf("MoveIt", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (System.Reflection.ReflectionTypeLoadException e)
                    {
                        types = e.Types;
                    }

                    foreach (var type in types)
                    {
                        if (type != null && type.Name == name && type.IsValueType && typeof(IComponentData).IsAssignableFrom(type))
                        {
                            s_Type = type;
                            Mod.Log.Info($"Found Move It selection component {type.FullName}");
                            return s_Type;
                        }
                    }
                }
            }

            Mod.Log.Info("Move It selection component not found; Move It selection is unavailable");
            return null;
        }
    }
}
