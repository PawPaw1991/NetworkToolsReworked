using Colossal.Entities;
using Colossal.Mathematics;
using Game.Net;
using Game.Prefabs;
using Game.Rendering;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace NetworkToolsReworked.Tools
{
    /// <summary>
    /// Draws tool feedback (hovered and picked nodes, the affected path, guide lines) with the vanilla
    /// overlay renderer. Drawing happens on the main thread, so the buffer is fetched once per frame.
    /// </summary>
    public sealed class ToolOverlay
    {
        public static readonly Color Hover = new Color(1f, 1f, 1f, 0.55f);
        public static readonly Color Start = new Color(0.25f, 0.9f, 0.4f, 0.85f);
        public static readonly Color End = new Color(1f, 0.65f, 0.15f, 0.85f);
        public static readonly Color Path = new Color(0.3f, 0.7f, 1f, 0.35f);
        public static readonly Color Locked = new Color(1f, 0.65f, 0.15f, 0.35f);
        public static readonly Color Invalid = new Color(1f, 0.25f, 0.2f, 0.7f);

        private readonly OverlayRenderSystem m_OverlayRenderSystem;
        private readonly EntityManager m_EntityManager;
        private OverlayRenderSystem.Buffer m_Buffer;
        private bool m_HasBuffer;

        public ToolOverlay(World world)
        {
            m_OverlayRenderSystem = world.GetOrCreateSystemManaged<OverlayRenderSystem>();
            m_EntityManager = world.EntityManager;
        }

        private OverlayRenderSystem.Buffer Buffer
        {
            get
            {
                if (!m_HasBuffer)
                {
                    m_Buffer = m_OverlayRenderSystem.GetBuffer(out var dependencies);
                    dependencies.Complete();
                    m_HasBuffer = true;
                }
                return m_Buffer;
            }
        }

        /// <summary>Call at the start of each tool update so the next draw fetches a fresh buffer.</summary>
        public void BeginFrame() => m_HasBuffer = false;

        public void Node(Entity node, Color color)
        {
            if (!m_EntityManager.TryGetComponent(node, out Game.Net.Node data))
                return;
            Buffer.DrawCircle(color, data.m_Position, Width(node) + 2f);
        }

        public void Edge(Entity edge, Color color)
        {
            if (!m_EntityManager.TryGetComponent(edge, out Curve curve))
                return;
            Buffer.DrawCurve(color, curve.m_Bezier, Width(edge));
        }

        public void Bezier(Bezier4x3 curve, float width, Color color) => Buffer.DrawCurve(color, curve, width);

        public void Point(float3 position, float diameter, Color color) => Buffer.DrawCircle(color, position, diameter);

        public void Line(float3 from, float3 to, float width, Color color) => Buffer.DrawLine(color, new Line3.Segment(from, to), width);

        /// <summary>Road width of an edge or node's prefab, used to size highlights.</summary>
        public float Width(Entity entity)
        {
            if (m_EntityManager.TryGetComponent(entity, out PrefabRef prefab) &&
                m_EntityManager.TryGetComponent(prefab.m_Prefab, out NetGeometryData geometry))
                return math.max(geometry.m_DefaultWidth, 2f);
            return 8f;
        }
    }
}
