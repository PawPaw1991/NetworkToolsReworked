using Game;
using Game.Input;
using Game.Tools;

namespace NetworkToolsReworked.Tools
{
    /// <summary>Turns the node tools on and off from their key bindings.</summary>
    public partial class ToolActivationSystem : GameSystemBase
    {
        private ToolSystem m_ToolSystem;
        private DefaultToolSystem m_DefaultToolSystem;
        private NodeToolSystem m_NodeToolSystem;
        private ProxyAction m_AddNodeAction;
        private ProxyAction m_RemoveNodeAction;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_DefaultToolSystem = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            m_NodeToolSystem = World.GetOrCreateSystemManaged<NodeToolSystem>();

            m_AddNodeAction = Mod.Settings.GetAction(nameof(Setting.AddNodeTool));
            m_RemoveNodeAction = Mod.Settings.GetAction(nameof(Setting.RemoveNodeTool));
            m_AddNodeAction.shouldBeEnabled = true;
            m_RemoveNodeAction.shouldBeEnabled = true;
        }

        protected override void OnUpdate()
        {
            if (m_AddNodeAction.WasPerformedThisFrame())
                Toggle(NodeToolMode.AddNode);
            else if (m_RemoveNodeAction.WasPerformedThisFrame())
                Toggle(NodeToolMode.RemoveNode);
        }

        private void Toggle(NodeToolMode mode)
        {
            if (m_ToolSystem.activeTool == m_NodeToolSystem && m_NodeToolSystem.Mode == mode)
            {
                m_ToolSystem.activeTool = m_DefaultToolSystem;
                return;
            }

            m_NodeToolSystem.Mode = mode;
            m_ToolSystem.activeTool = m_NodeToolSystem;
        }
    }
}
