using System;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Event arguments for handling new or unlisted values typed into lookup editors.
    /// Preserved in ZeroUI.WinForms.Editors namespace for seamless backward compatibility.
    /// </summary>
    public sealed class ProcessNewValueEventArgs : ZeroUI.Core.Editors.ProcessNewValueEventArgs
    {
        public ProcessNewValueEventArgs(string displayText) : base(displayText)
        {
        }
    }
}
