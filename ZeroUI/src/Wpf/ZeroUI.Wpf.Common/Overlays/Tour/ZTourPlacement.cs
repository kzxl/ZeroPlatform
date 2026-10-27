namespace ZeroUI.Wpf.Overlays
{
    /// <summary>
    /// Specifies the preferred placement of the tour guide popover relative to its target element.
    /// </summary>
    public enum ZTourPlacement
    {
        /// <summary>
        /// Automatically picks the optimal placement avoiding screen clipping.
        /// </summary>
        Auto,

        /// <summary>
        /// Places the popover directly above the target element.
        /// </summary>
        Top,

        /// <summary>
        /// Places the popover directly below the target element.
        /// </summary>
        Bottom,

        /// <summary>
        /// Places the popover to the left of the target element.
        /// </summary>
        Left,

        /// <summary>
        /// Places the popover to the right of the target element.
        /// </summary>
        Right,

        /// <summary>
        /// Centers the popover on the window (ideal for welcome or conclusion steps without a specific target).
        /// </summary>
        Center
    }
}
