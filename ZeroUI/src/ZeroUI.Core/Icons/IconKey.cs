using System;

namespace ZeroUI.Core.Icons
{
    /// <summary>
    /// Standardized icon keys representing enterprise UI actions, files, navigation, status, and industrial processes.
    /// Provides a strongly typed Single Source of Truth for icons across ZeroUI.
    /// </summary>
    public enum IconKey
    {
        #region CRUD & Actions

        /// <summary>Add or create new entity.</summary>
        Add,

        /// <summary>Edit or modify entity.</summary>
        Edit,

        /// <summary>Delete or remove entity.</summary>
        Delete,

        /// <summary>Save or persist changes.</summary>
        Save,

        /// <summary>Refresh or reload data.</summary>
        Refresh,

        /// <summary>Search or find records.</summary>
        Search,

        /// <summary>Filter or funnel criteria.</summary>
        Filter,

        /// <summary>Clear current inputs or filters.</summary>
        Clear,

        /// <summary>Close or dismiss view/dialog.</summary>
        Close,

        /// <summary>Copy to clipboard.</summary>
        Copy,

        /// <summary>Cut to clipboard.</summary>
        Cut,

        /// <summary>Paste from clipboard.</summary>
        Paste,

        /// <summary>Duplicate entity.</summary>
        Duplicate,

        /// <summary>Undo last operation.</summary>
        Undo,

        /// <summary>Redo last undone operation.</summary>
        Redo,

        #endregion

        #region File & Data Transfer

        /// <summary>Export data or report.</summary>
        Export,

        /// <summary>Import data from file.</summary>
        Import,

        /// <summary>Print document.</summary>
        Print,

        /// <summary>Download package or file.</summary>
        Download,

        /// <summary>Upload package or file.</summary>
        Upload,

        /// <summary>Folder or directory.</summary>
        Folder,

        /// <summary>Document or sheet.</summary>
        Document,

        /// <summary>Generic file.</summary>
        File,

        #endregion

        #region Navigation

        /// <summary>Directional left arrow.</summary>
        ArrowLeft,

        /// <summary>Directional right arrow.</summary>
        ArrowRight,

        /// <summary>Directional upward arrow.</summary>
        ArrowUp,

        /// <summary>Directional downward arrow.</summary>
        ArrowDown,

        /// <summary>Navigate back.</summary>
        Back,

        /// <summary>Navigate forward.</summary>
        Forward,

        /// <summary>Navigate to home/root.</summary>
        Home,

        /// <summary>Toggle main navigation menu.</summary>
        Menu,

        /// <summary>More options or overflow.</summary>
        More,

        #endregion

        #region Alignment & Distribution

        /// <summary>Align elements to left edge.</summary>
        AlignLeft,

        /// <summary>Align elements along horizontal center.</summary>
        AlignCenter,

        /// <summary>Align elements to right edge.</summary>
        AlignRight,

        /// <summary>Align elements to top edge.</summary>
        AlignTop,

        /// <summary>Align elements along vertical middle.</summary>
        AlignMiddle,

        /// <summary>Align elements to bottom edge.</summary>
        AlignBottom,

        /// <summary>Distribute elements evenly across horizontal span.</summary>
        DistributeHorizontal,

        /// <summary>Distribute elements evenly across vertical span.</summary>
        DistributeVertical,

        #endregion

        #region Industrial, Workflow & SCADA

        /// <summary>Link or connect diagram steps.</summary>
        Connect,

        /// <summary>Unlink or disconnect steps.</summary>
        Unlink,

        /// <summary>Change shape or node geometry.</summary>
        Shape,

        /// <summary>Color palette or accent picker.</summary>
        Palette,

        /// <summary>Configuration or system settings.</summary>
        Settings,

        /// <summary>Informational note or help indicator.</summary>
        Info,

        /// <summary>Warning alert.</summary>
        Warning,

        /// <summary>Success or verified status.</summary>
        Success,

        /// <summary>Error or critical failure indicator.</summary>
        Error,

        /// <summary>Lock security.</summary>
        Lock,

        /// <summary>Unlock security.</summary>
        Unlock,

        /// <summary>Zoom in viewport.</summary>
        ZoomIn,

        /// <summary>Zoom out viewport.</summary>
        ZoomOut,

        /// <summary>Fit canvas to viewport.</summary>
        ZoomFit,

        /// <summary>Play, start, or run process.</summary>
        Play,

        /// <summary>Pause active process.</summary>
        Pause,

        /// <summary>Stop active process.</summary>
        Stop,

        /// <summary>Step or task node.</summary>
        Step,

        /// <summary>Swimlane, boundary frame, or container.</summary>
        Swimlane,

        /// <summary>Auto-arrange flow layout.</summary>
        AutoLayout,

        /// <summary>Fullscreen toggle.</summary>
        Fullscreen

        #endregion
    }

    /// <summary>
    /// Extension methods and resolution helpers for <see cref="IconKey"/>.
    /// </summary>
    public static class IconKeyExtensions
    {
        /// <summary>
        /// Converts the <see cref="IconKey"/> to a normalized lowercase key string (e.g. "save", "align-left").
        /// </summary>
        public static string ToKeyString(this IconKey key)
        {
            return key.ToString();
        }

        /// <summary>
        /// Attempts to parse a string representation of an icon key (case-insensitive, ignoring dashes or underscores).
        /// </summary>
        public static bool TryParseKey(string? value, out IconKey key)
        {
            key = IconKey.Document;
            if (string.IsNullOrWhiteSpace(value)) return false;

            string clean = value!.Replace("-", "").Replace("_", "").Trim();
            return Enum.TryParse(clean, true, out key);
        }
    }
}
