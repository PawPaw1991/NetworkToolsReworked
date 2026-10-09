using Game;
using Game.Input;
using Game.Tools;
using NetworkToolsReworked.Undo;

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
        Reverse,
        Roundabout,
        Undo,
        Intersect,
        Replace,
        Upgrades,
        Measure,
        Ramp,
        MatchHeight,
        Health,
        Bridge,
        Fillet,
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
        private ReverseToolSystem m_ReverseToolSystem;
        private RoundaboutToolSystem m_RoundaboutToolSystem;
        private UndoToolSystem m_UndoToolSystem;
        private IntersectToolSystem m_IntersectToolSystem;
        private ReplaceToolSystem m_ReplaceToolSystem;
        private UpgradesToolSystem m_UpgradesToolSystem;
        private ProxyAction m_UpgradesAction;
        private MeasureToolSystem m_MeasureToolSystem;
        private ProxyAction m_MeasureAction;
        private RampToolSystem m_RampToolSystem;
        private ProxyAction m_RampAction;
        private MatchHeightToolSystem m_MatchHeightToolSystem;
        private ProxyAction m_MatchHeightAction;
        /// <summary>Tools on the quick-switch keys (Alt+1 to Alt+9), in order.</summary>
        public static readonly ToolId[] QuickTools = { ToolId.Slope, ToolId.Smooth, ToolId.MoveNode, ToolId.Arrange, ToolId.Connect, ToolId.Parallel, ToolId.Ramp, ToolId.Replace, ToolId.Measure };

        private readonly ProxyAction[] m_QuickActions = new ProxyAction[QuickTools.Length];
        private ProxyAction m_StepBackAction;
        private HealthToolSystem m_HealthToolSystem;
        private ProxyAction m_HealthAction;
        private BridgeToolSystem m_BridgeToolSystem;
        private ProxyAction m_BridgeAction;
        private FilletToolSystem m_FilletToolSystem;
        private ProxyAction m_FilletAction;
        private ProxyAction m_AddNodeAction;
        private ProxyAction m_RemoveNodeAction;
        private ProxyAction m_SlopeAction;
        private ProxyAction m_ConnectAction;
        private ProxyAction m_ParallelAction;
        private ProxyAction m_SmoothAction;
        private ProxyAction m_MoveNodeAction;
        private ProxyAction m_ArrangeAction;
        private ProxyAction m_ReverseAction;
        private ProxyAction m_RoundaboutAction;
        private ProxyAction m_UndoAction;
        private ProxyAction m_IntersectAction;
        private ProxyAction m_ReplaceAction;

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
                if (active == m_ReverseToolSystem) return ToolId.Reverse;
                if (active == m_RoundaboutToolSystem) return ToolId.Roundabout;
                if (active == m_UndoToolSystem) return ToolId.Undo;
                if (active == m_IntersectToolSystem) return ToolId.Intersect;
                if (active == m_ReplaceToolSystem) return ToolId.Replace;
                if (active == m_UpgradesToolSystem) return ToolId.Upgrades;
                if (active == m_MeasureToolSystem) return ToolId.Measure;
                if (active == m_RampToolSystem) return ToolId.Ramp;
                if (active == m_MatchHeightToolSystem) return ToolId.MatchHeight;
                if (active == m_HealthToolSystem) return ToolId.Health;
                if (active == m_BridgeToolSystem) return ToolId.Bridge;
                if (active == m_FilletToolSystem) return ToolId.Fillet;
                return ToolId.None;
            }
        }

        /// <summary>The active tool if it has a reviewable preview, otherwise null.</summary>
        public IPreviewTool PreviewTool => m_ToolSystem.activeTool as IPreviewTool;

        public ConnectToolSystem ConnectTool => m_ConnectToolSystem;

        public MoveNodeToolSystem MoveNodeTool => m_MoveNodeToolSystem;

        public RampToolSystem RampTool => m_RampToolSystem;

        public MatchHeightToolSystem MatchHeightTool => m_MatchHeightToolSystem;

        public HealthToolSystem HealthTool => m_HealthToolSystem;

        /// <summary>The active tool if it copies from a picked road first, otherwise null.</summary>
        public PathToolSystem SourceTool => m_ToolSystem.activeTool as PathToolSystem;

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
            m_ReverseToolSystem = World.GetOrCreateSystemManaged<ReverseToolSystem>();
            m_RoundaboutToolSystem = World.GetOrCreateSystemManaged<RoundaboutToolSystem>();
            m_UndoToolSystem = World.GetOrCreateSystemManaged<UndoToolSystem>();
            m_IntersectToolSystem = World.GetOrCreateSystemManaged<IntersectToolSystem>();
            m_ReplaceToolSystem = World.GetOrCreateSystemManaged<ReplaceToolSystem>();

            m_UpgradesToolSystem = World.GetOrCreateSystemManaged<UpgradesToolSystem>();
            m_UpgradesAction = Enable(nameof(Setting.UpgradesTool));
            m_MeasureToolSystem = World.GetOrCreateSystemManaged<MeasureToolSystem>();
            m_MeasureAction = Enable(nameof(Setting.MeasureTool));
            m_RampToolSystem = World.GetOrCreateSystemManaged<RampToolSystem>();
            m_RampAction = Enable(nameof(Setting.RampTool));
            m_MatchHeightToolSystem = World.GetOrCreateSystemManaged<MatchHeightToolSystem>();
            m_MatchHeightAction = Enable(nameof(Setting.MatchHeightTool));
            for (var i = 0; i < QuickTools.Length; i++)
                m_QuickActions[i] = Enable($"QuickTool{i + 1}");
            m_StepBackAction = Enable(nameof(Setting.StepBack));
            m_HealthToolSystem = World.GetOrCreateSystemManaged<HealthToolSystem>();
            m_HealthAction = Enable(nameof(Setting.HealthTool));
            m_BridgeToolSystem = World.GetOrCreateSystemManaged<BridgeToolSystem>();
            m_BridgeAction = Enable(nameof(Setting.BridgeTool));
            m_FilletToolSystem = World.GetOrCreateSystemManaged<FilletToolSystem>();
            m_FilletAction = Enable(nameof(Setting.FilletTool));
            m_AddNodeAction = Enable(nameof(Setting.AddNodeTool));
            m_RemoveNodeAction = Enable(nameof(Setting.RemoveNodeTool));
            m_SlopeAction = Enable(nameof(Setting.SlopeTool));
            m_ConnectAction = Enable(nameof(Setting.ConnectTool));
            m_ParallelAction = Enable(nameof(Setting.ParallelTool));
            m_SmoothAction = Enable(nameof(Setting.SmoothTool));
            m_MoveNodeAction = Enable(nameof(Setting.MoveNodeTool));
            m_ArrangeAction = Enable(nameof(Setting.ArrangeTool));
            m_ReverseAction = Enable(nameof(Setting.ReverseTool));
            m_RoundaboutAction = Enable(nameof(Setting.RoundaboutTool));
            m_UndoAction = Enable(nameof(Setting.UndoTool));
            m_IntersectAction = Enable(nameof(Setting.IntersectTool));
            m_ReplaceAction = Enable(nameof(Setting.ReplaceTool));
        }

        protected override void OnUpdate()
        {
            // Step back only while one of our tools is active, so the key stays free otherwise.
            if (m_StepBackAction.WasPerformedThisFrame() && Current != ToolId.None)
                PreviewTool?.RequestCancel();

            for (var i = 0; i < m_QuickActions.Length; i++)
            {
                if (m_QuickActions[i].WasPerformedThisFrame())
                {
                    Toggle(QuickTools[i]);
                    return;
                }
            }

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
            else if (m_ReverseAction.WasPerformedThisFrame())
                Toggle(ToolId.Reverse);
            else if (m_RoundaboutAction.WasPerformedThisFrame())
                Toggle(ToolId.Roundabout);
            else if (m_UndoAction.WasPerformedThisFrame())
                Toggle(ToolId.Undo);
            else if (m_IntersectAction.WasPerformedThisFrame())
                Toggle(ToolId.Intersect);
            else if (m_ReplaceAction.WasPerformedThisFrame())
                Toggle(ToolId.Replace);
            else if (m_UpgradesAction.WasPerformedThisFrame())
                Toggle(ToolId.Upgrades);
            else if (m_MeasureAction.WasPerformedThisFrame())
                Toggle(ToolId.Measure);
            else if (m_RampAction.WasPerformedThisFrame())
                Toggle(ToolId.Ramp);
            else if (m_MatchHeightAction.WasPerformedThisFrame())
                Toggle(ToolId.MatchHeight);
            else if (m_HealthAction.WasPerformedThisFrame())
                Toggle(ToolId.Health);
            else if (m_BridgeAction.WasPerformedThisFrame())
                Toggle(ToolId.Bridge);
            else if (m_FilletAction.WasPerformedThisFrame())
                Toggle(ToolId.Fillet);
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
                case ToolId.Reverse:
                    m_ToolSystem.activeTool = m_ReverseToolSystem;
                    break;
                case ToolId.Roundabout:
                    m_ToolSystem.activeTool = m_RoundaboutToolSystem;
                    break;
                case ToolId.Undo:
                    m_ToolSystem.activeTool = m_UndoToolSystem;
                    break;
                case ToolId.Intersect:
                    m_ToolSystem.activeTool = m_IntersectToolSystem;
                    break;
                case ToolId.Replace:
                    m_ToolSystem.activeTool = m_ReplaceToolSystem;
                    break;
                case ToolId.Upgrades:
                    m_ToolSystem.activeTool = m_UpgradesToolSystem;
                    break;
                case ToolId.Measure:
                    m_ToolSystem.activeTool = m_MeasureToolSystem;
                    break;
                case ToolId.Ramp:
                    m_ToolSystem.activeTool = m_RampToolSystem;
                    break;
                case ToolId.MatchHeight:
                    m_ToolSystem.activeTool = m_MatchHeightToolSystem;
                    break;
                case ToolId.Health:
                    m_ToolSystem.activeTool = m_HealthToolSystem;
                    break;
                case ToolId.Bridge:
                    m_ToolSystem.activeTool = m_BridgeToolSystem;
                    break;
                case ToolId.Fillet:
                    m_ToolSystem.activeTool = m_FilletToolSystem;
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
