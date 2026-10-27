using System;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Event arguments for value changes with cancellation support.
    /// </summary>
    /// <typeparam name="TValue">The type of the value being edited.</typeparam>
    public class ValueChangingEventArgs<TValue> : EventArgs
    {
        public TValue OldValue { get; }
        public TValue NewValue { get; set; }
        public bool Cancel { get; set; }

        public ValueChangingEventArgs(TValue oldValue, TValue newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
            Cancel = false;
        }
    }

    /// <summary>
    /// Strongly-typed universal contract for all ZeroUI form editors and input components.
    /// </summary>
    /// <typeparam name="TValue">The primary strongly-typed value managed by the editor.</typeparam>
    public interface IZeroEditor<TValue> : IZeroEditor
    {
        /// <summary>
        /// Gets or sets the strongly-typed value of the editor.
        /// </summary>
        TValue Value { get; set; }

        /// <summary>
        /// Occurs when the strongly-typed Value property changes.
        /// </summary>
        event EventHandler? ValueChanged;

        /// <summary>
        /// Occurs prior to committing a new value, allowing validation or cancellation.
        /// </summary>
        event EventHandler<ValueChangingEventArgs<TValue>>? ValueChanging;
    }
}
