using System;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Specifies the semantic action type of an embedded editor action button.
    /// </summary>
    public enum EditorButtonKind
    {
        Custom = 0,
        BrowseFile = 1,
        BrowseFolder = 2,
        Clear = 3,
        Copy = 4,
        DropDown = 5,
        Search = 6,
        Undo = 7,
        Redo = 8
    }

    /// <summary>
    /// Common configuration model for action buttons embedded within a <c>ButtonEdit</c>.
    /// </summary>
    public class EditorButtonModel
    {
        public EditorButtonKind Kind { get; set; } = EditorButtonKind.Custom;
        public string? Text { get; set; }
        public string? ToolTip { get; set; }
        public object? Tag { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool Visible { get; set; } = true;

        public EditorButtonModel() { }

        public EditorButtonModel(EditorButtonKind kind, string? toolTip = null)
        {
            Kind = kind;
            ToolTip = toolTip ?? GetDefaultToolTip(kind);
        }

        public static string GetDefaultToolTip(EditorButtonKind kind) => kind switch
        {
            EditorButtonKind.BrowseFile => "Browse file...",
            EditorButtonKind.BrowseFolder => "Browse folder...",
            EditorButtonKind.Clear => "Clear input",
            EditorButtonKind.Copy => "Copy to clipboard",
            EditorButtonKind.DropDown => "Show options",
            EditorButtonKind.Search => "Search",
            EditorButtonKind.Undo => "Undo",
            EditorButtonKind.Redo => "Redo",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Event arguments raised when an embedded editor button is clicked.
    /// </summary>
    public class EditorButtonClickEventArgs : EventArgs
    {
        public EditorButtonKind Kind { get; }
        public object? Tag { get; }
        public bool Handled { get; set; }

        public EditorButtonClickEventArgs(EditorButtonKind kind, object? tag = null)
        {
            Kind = kind;
            Tag = tag;
        }
    }
}
