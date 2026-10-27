using System;

namespace ZeroUI.Core.Input
{
    /// <summary>
    /// Layout style for touch-screen virtual keyboards.
    /// </summary>
    public enum VirtualKeyboardLayout
    {
        /// <summary>
        /// Standard QWERTY layout with number row, symbols, spacebar, and action keys.
        /// </summary>
        AlphaNumeric = 0,

        /// <summary>
        /// Compact 10-key numeric keypad with decimal, sign, backspace, clear, and enter.
        /// Ideal for industrial parameter tuning, PIN codes, and numeric editors.
        /// </summary>
        Numpad = 1
    }

    /// <summary>
    /// Type of key in a virtual keyboard layout.
    /// </summary>
    public enum VirtualKeyType
    {
        Character = 0,
        Backspace = 1,
        Enter = 2,
        Space = 3,
        Shift = 4,
        Clear = 5,
        Escape = 6,
        Tab = 7,
        SymbolToggle = 8
    }

    /// <summary>
    /// Event arguments describing a key press on a virtual keyboard.
    /// </summary>
    public class VirtualKeyEventArgs : EventArgs
    {
        public string Text { get; }
        public VirtualKeyType KeyType { get; }

        public VirtualKeyEventArgs(string text, VirtualKeyType keyType = VirtualKeyType.Character)
        {
            Text = text ?? string.Empty;
            KeyType = keyType;
        }
    }
}
