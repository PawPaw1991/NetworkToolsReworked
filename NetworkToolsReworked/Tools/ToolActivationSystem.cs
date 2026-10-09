using Game;
using Game.Input;
using Game.Tools;

namespace NetworkToolsReworked.Tools
{
    public enum ToolId
    {
        None,
        AddNode,
        RemoveNode,
        Slope,
        Connect,
        Parallel,
        Smooth,
        MoveNode,
        Arrange,
    }

    /// <summary>Turns the tools on and off, from their key bindings or the tool panel.</summary>
    public partial class ToolActivationSystem : GameSystemBase
    {
        private ToolSystem m_ToolSystem;
        private DefaultToolSystem m_DefaultToolSystem;
        private NodeToolSystem m_NodeToolSystem;
        private SlopeToolSystem m_SlopeToolSystem;
        private ConnectToolSystem m_ConnectToolSystem;
        private ParallelToolSystem m_ParallelToolSystem;
        private SmoothToolSystem m_SmoothToolSystem;
        private MoveNodeToolSystem m_MoveNodeToolSystem;
        private ArrangeToolSystem m_ArrangeToolSystem;
        private ProxyAction m_AddNodeAction;
        private ProxyAction m_RemoveNodeAction;
        private ProxyAction m_SlopeAction;
        private ProxyAction m_ConnectAction;
        private ProxyAction m_ParallelAction;
        private ProxyAction m_SmoothAction;
        private ProxyAction m_MoveNodeAction;
        private ProxyAction m_ArrangeAction;

        public ToolId Current
        {
            get
            {
                var active = m_ToolSystem.activeTool;
                if (active == m_NodeToolSystem)
                    return m_NodeToolSystem.Mode == NodeToolMode.AddNode ? ToolId.AddNode : ToolId.RemoveNode;
                if (active == m_SlopeToolSystem) return ToolId.Slope;
                if (active == m_ConnectToolSystem) return ToolId.Connect;
                if (active == m_ParallelToolSystem) return ToolId.Parallel;
                if (active == m_SmoothToolSystem) return ToolId.Smooth;
                if (active == m_MoveNodeToolSystem) return ToolId.MoveNode;
                if (active == m_ArrangeToolSystem) return ToolId.Arrange;
                return ToolId.None;
            }
        }

        /// <summary>The active tool if it has a reviewable preview, otherwise null.</summary>
        public IPreviewTool PreviewTool => m_ToolSystem.activeTool as IPreviewTool;

        public ConnectToolSystem ConnectTool => m_ConnectToolSystem;

        public MoveNodeToolSystem MoveNodeTool => m_MoveNodeToolSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_DefaultToolSystem = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            m_NodeToolSystem = World.GetOrCreateSystemManaged<NodeToolSystem>();
            m_SlopeToolSystem = World.GetOrCreateSystemManaged<SlopeToolSystem>();
            m_ConnectToolSystem = World.GetOrCreateSystemManaged<ConnectToolSystem>();
            m_ParallelToolSystem = World.GetOrCreateSystemManaged<ParallelToolSystem>();
            m_SmoothToolSystem = World.GetOrCreateSystemManaged<SmoothToolSystem>();
            m_MoveNodeToolSystem = World.GetOrCreateSystemManaged<MoveNodeToolSystem>();
            m_ArrangeToolSystem = World.GetOrCreateSystemManaged<ArrangeToolSystem>();

            m_AddNodeAction = Enable(nameof(Setting.AddNodeTool));
            m_RemoveNodeAction = Enable(nameof(Setting.RemoveNodeTool));
            m_SlopeAction = Enable(nameof(Setting.SlopeTool));
            m_ConnectAction = Enable(nameof(Setting.ConnectTool));
            m_ParallelAction = Enable(nameof(Setting.ParallelTool));
            m_SmoothAction = Enable(nameof(Setting.SmoothTool));
            m_MoveNodeAction = Enable(nameof(Setting.MoveNodeTool));
            m_ArrangeAction = Enable(nameof(Setting.ArrangeTool));
        }

        protected override void OnUpdate()
        {
            if (m_AddNodeAction.WasPerformedThisFrame())
                Toggle(ToolId.AddNode);
            else if (m_RemoveNodeAction.WasPerformedThisFrame())
                Toggle(ToolId.RemoveNode);
            else if (m_SlopeAction.WasPerformedThisFrame())
                Toggle(ToolId.Slope);
            else if (m_ConnectAction.WasPerformedThisFrame())
                Toggle(ToolId.Connect);
            else if (m_ParallelAction.WasPerformedThisFrame())
                Toggle(ToolId.Parallel);
            else if (m_SmoothAction.WasPerformedThisFrame())
                Toggle(ToolId.Smooth);
            else if (m_MoveNodeAction.WasPerformedThisFrame())
                Toggle(ToolId.MoveNode);
            else if (m_ArrangeAction.WasPerformedThisFrame())
                Toggle(ToolId.Arrange);
        }

        /// <summary>Activates a tool, or returns to the default tool if it is already active.</summary>
        public void Toggle(ToolId tool)
        {
            Activate(Current == tool ? ToolId.None : tool);
        }

        public void Activate(ToolId tool)
        {
            switch (tool)
            {
                case ToolId.AddNode:
                case ToolId.RemoveNode:
                    m_NodeToolSystem.Mode = tool == ToolId.AddNode ? NodeToolMode.AddNode : NodeToolMode.RemoveNode;
                    m_ToolSystem.activeTool = m_NodeToolSystem;
                    break;
                case ToolId.Slope:
                    m_ToolSystem.activeTool = m_SlopeToolSystem;
                    break;
                case ToolId.Connect:
                    m_ToolSystem.activeTool = m_ConnectToolSystem;
                    break;
                case ToolId.Parallel:
                    m_ToolSystem.activeTool = m_ParallelToolSystem;
                    break;
                case ToolId.Smooth:
                    m_ToolSystem.activeTool = m_SmoothToolSystem;
                    break;
                case ToolId.MoveNode:
                    m_ToolSystem.activeTool = m_MoveNodeToolSystem;
                    break;
                case ToolId.Arrange:
                    m_ToolSystem.activeTool = m_ArrangeToolSystem;
                    break;
                default:
                    m_ToolSystem.activeTool = m_DefaultToolSystem;
                    break;
            }
        }

        private static ProxyAction Enable(string name)
        {
            var action = Mod.Settings.GetAction(name);
            action.shouldBeEnabled = true;
            return action;
        }
    }
}
