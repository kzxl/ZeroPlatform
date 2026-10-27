using System;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Universal contract for all ZeroUI form editors and input components.
    /// Provides unified object value access (EditValue), dirty state tracking,
    /// and automated bidirectional DTO form binding.
    /// </summary>
    public interface IZeroEditor
    {
        /// <summary>
        /// Gets or sets the editing value as an object representation.
        /// Automatically performs type conversions where appropriate.
        /// </summary>
        object? EditValue { get; set; }

        /// <summary>
        /// Occurs when the EditValue property is changed by user interaction or programmatic assignment.
        /// </summary>
        event EventHandler? EditValueChanged;

        /// <summary>
        /// Gets or sets whether the value has been modified by the user compared to its initial state.
        /// </summary>
        bool IsModified { get; set; }

        /// <summary>
        /// Gets or sets whether the editor is in read-only mode.
        /// </summary>
        bool ReadOnly { get; set; }

        /// <summary>
        /// Resets the editor to its initial default state.
        /// </summary>
        void Reset();

        /// <summary>
        /// Clears the editor content.
        /// </summary>
        void Clear();
    }
}
