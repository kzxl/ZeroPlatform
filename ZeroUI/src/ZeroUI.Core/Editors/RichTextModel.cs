using System;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Features available in the rich text editor.
    /// </summary>
    [Flags]
    public enum RichTextFeatures
    {
        /// <summary>No features enabled.</summary>
        None = 0,
        /// <summary>Bold text formatting.</summary>
        Bold = 1,
        /// <summary>Italic text formatting.</summary>
        Italic = 2,
        /// <summary>Underline text formatting.</summary>
        Underline = 4,
        /// <summary>Strikethrough text formatting.</summary>
        Strikethrough = 8,
        /// <summary>List formatting (bulleted and numbered).</summary>
        Lists = 16,
        /// <summary>Heading levels.</summary>
        Headings = 32,
        /// <summary>Hyperlink insertion.</summary>
        Links = 64,
        /// <summary>Image insertion.</summary>
        Images = 128,
        /// <summary>All features enabled.</summary>
        All = Bold | Italic | Underline | Strikethrough | Lists | Headings | Links | Images
    }

    /// <summary>
    /// Event arguments for link clicked events.
    /// </summary>
    public class LinkClickedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the URL that was clicked.
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkClickedEventArgs"/> class.
        /// </summary>
        /// <param name="url">The clicked URL.</param>
        public LinkClickedEventArgs(string url)
        {
            Url = url;
        }
    }
}
