namespace NetworkToolsReworked.Tools
{
    public enum ToolPhase
    {
        /// <summary>Waiting for a road to copy from (its type or upgrades), before the start node.</summary>
        PickSource,

        /// <summary>Waiting for the start node.</summary>
        PickStart,

        /// <summary>Start picked; the preview follows the hovered end node.</summary>
        PickEnd,

        /// <summary>Both ends locked; the preview stays put and follows option changes until applied.</summary>
        Review,
    }

    /// <summary>A two-node tool whose preview can be locked, reviewed and adjusted before it is applied.</summary>
    public interface IPreviewTool
    {
        ToolPhase Phase { get; }

        /// <summary>Short description of the current preview (length, grade, ...), empty if there is none.</summary>
        string Summary { get; }

        /// <summary>Applies the locked preview on the next update. Ignored outside <see cref="ToolPhase.Review"/>.</summary>
        void RequestApply();

        /// <summary>Steps back one phase on the next update, like a right-click.</summary>
        void RequestCancel();
    }
}
