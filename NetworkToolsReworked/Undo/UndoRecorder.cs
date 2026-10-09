using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Mathematics;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkToolsReworked.Undo
{
    /// <summary>A road as it was before an edit: enough to build it again through definitions.</summary>
    public struct RoadSnapshot
    {
        public Entity Prefab;
        public Bezier4x3 Curve;
        public float2 StartElevation;
        public float2 EndElevation;
        public bool HasElevation;
        public bool HasUpgrades;
        public Upgraded Upgrades;
    }

    /// <summary>One applied edit: the roads it removed or changed, and the curves of the roads it left behind.</summary>
    public sealed class UndoStep
    {
        public string Tool;

        /// <summary>What the edit did, for the history list, e.g. "Slope: 5 segments, 240 m, height +6.0 m".</summary>
        public string Label;
        public readonly List<RoadSnapshot> Originals = new List<RoadSnapshot>();
        public readonly List<Bezier4x3> Results = new List<Bezier4x3>();
    }

    /// <summary>
    /// Collects what the current frame's definitions change. Tools call <see cref="Begin"/> every frame
    /// before emitting and <see cref="Commit"/> on the frame they apply.
    /// </summary>
    public static class UndoRecorder
    {
        private static EntityManager s_EntityManager;
        private static UndoStep s_Current;
        private static readonly HashSet<Entity> s_Seen = new HashSet<Entity>();

        public static void Begin(EntityManager em, string tool)
        {
            s_EntityManager = em;
            s_Current = new UndoStep { Tool = tool };
            s_Seen.Clear();
        }

        /// <summary>Stops recording without keeping anything (used while undoing).</summary>
        public static void Suspend() => s_Current = null;

        /// <param name="description">The tool's description of the edit (its panel summary).</param>
        public static void Commit(string description)
        {
            if (s_Current != null && (s_Current.Originals.Count > 0 || s_Current.Results.Count > 0))
            {
                s_Current.Label = Label(s_Current.Tool, description);
                UndoHistory.Push(s_Current);
            }
            s_Current = null;
        }

        /// <summary>Short tool name ("Slope" from "NetworkToolsReworked.SlopeTool").</summary>
        public static string ToolName(string toolId) => toolId.Replace("NetworkToolsReworked.", "").Replace("Tool", "");

        private static string Label(string tool, string description)
        {
            const int max = 110;
            var text = string.IsNullOrEmpty(description) ? ToolName(tool) : $"{ToolName(tool)}: {description}";
            text = text.Replace("\n", " ").Replace("\t", " ");
            return text.Length > max ? text.Substring(0, max - 1) + "…" : text;
        }

        /// <summary>Called for every definition we emit.</summary>
        internal static void Record(CreationDefinition definition, NetCourse course)
        {
            if (s_Current == null)
                return;
            if (definition.m_Original != Entity.Null)
                RecordOriginal(definition.m_Original);
            if ((definition.m_Flags & CreationFlags.Delete) == 0 && course.m_Length > 0.01f)
                s_Current.Results.Add(course.m_Curve);
        }

        /// <summary>Snapshots an existing road the edit removes or changes (once per road).</summary>
        internal static void RecordOriginal(Entity edge)
        {
            if (s_Current == null || !s_Seen.Add(edge))
                return;
            var em = s_EntityManager;
            if (!em.TryGetComponent(edge, out Edge e) || !em.TryGetComponent(edge, out Curve curve))
                return;

            var snapshot = new RoadSnapshot
            {
                Prefab = em.GetComponentData<PrefabRef>(edge).m_Prefab,
                Curve = curve.m_Bezier,
                HasElevation = em.TryGetComponent(edge, out Elevation elevation),
            };
            snapshot.StartElevation = em.TryGetComponent(e.m_Start, out Elevation startNode) ? startNode.m_Elevation : new float2(elevation.m_Elevation.x);
            snapshot.EndElevation = em.TryGetComponent(e.m_End, out Elevation endNode) ? endNode.m_Elevation : new float2(elevation.m_Elevation.y);
            snapshot.HasUpgrades = em.TryGetComponent(edge, out snapshot.Upgrades);
            s_Current.Originals.Add(snapshot);
        }

        /// <summary>Adds a resulting road curve that isn't visible in our own definitions (e.g. the halves of a split).</summary>
        internal static void RecordResult(Bezier4x3 curve)
        {
            s_Current?.Results.Add(curve);
        }
    }

    /// <summary>Applied edits, newest last. Kept for the session only.</summary>
    public static class UndoHistory
    {
        private const int kMaxSteps = 30;
        private static readonly List<UndoStep> s_Steps = new List<UndoStep>();

        public static int Count => s_Steps.Count;

        public static UndoStep Peek() => s_Steps.Count > 0 ? s_Steps[s_Steps.Count - 1] : null;

        /// <summary>All steps, oldest first.</summary>
        public static IReadOnlyList<UndoStep> Steps => s_Steps;

        /// <summary>Bumped whenever the history changes, so the panel knows when to refresh.</summary>
        public static int Version { get; private set; }

        public static void Push(UndoStep step)
        {
            s_Steps.Add(step);
            if (s_Steps.Count > kMaxSteps)
                s_Steps.RemoveAt(0);
            Version++;
        }

        public static void Pop()
        {
            if (s_Steps.Count > 0)
                s_Steps.RemoveAt(s_Steps.Count - 1);
            Version++;
        }

        public static void Clear()
        {
            s_Steps.Clear();
            Version++;
        }
    }
}
