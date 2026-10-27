namespace ZeroUI.Core.Validation
{
    /// <summary>
    /// Determines when validation rules are evaluated against an editor.
    /// </summary>
    public enum ValidationTrigger
    {
        /// <summary>
        /// Rules are only evaluated when explicitly requested via Validate() call.
        /// </summary>
        Manual = 0,

        /// <summary>
        /// Rules are automatically evaluated whenever EditValue changes.
        /// </summary>
        ValueChanged = 1,

        /// <summary>
        /// Rules are evaluated when the editor loses user input focus.
        /// </summary>
        FocusLost = 2
    }
}
