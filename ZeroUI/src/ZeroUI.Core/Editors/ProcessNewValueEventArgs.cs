using System;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Event arguments for handling new or unlisted values typed into lookup editors.
    /// Enables the 'Add New Record' action to create entities on-the-fly.
    /// </summary>
    public class ProcessNewValueEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the text typed by the user in the lookup search box.
        /// </summary>
        public string DisplayText { get; }

        /// <summary>
        /// Gets or sets the newly created object or key value.
        /// </summary>
        public object? NewValue { get; set; }

        /// <summary>
        /// Gets or sets whether the new value has been handled and committed.
        /// </summary>
        public bool Handled { get; set; }

        public ProcessNewValueEventArgs(string displayText)
        {
            DisplayText = displayText ?? string.Empty;
        }
    }
}
