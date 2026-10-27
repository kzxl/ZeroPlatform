using System;

namespace ZeroUI.Core.Theme
{
    /// <summary>
    /// Represents a complete visual skin for ZeroUI containing name, mode, and palette tokens.
    /// Provides cross-platform styling across WinForms, WPF, and Web engines.
    /// </summary>
    public class ZeroSkin
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsDark { get; set; }
        public ZeroPaletteTokens Tokens { get; set; } = new ZeroPaletteTokens();

        public ZeroSkin() { }

        public ZeroSkin(string name, string displayName, bool isDark, ZeroPaletteTokens tokens)
        {
            Name = name;
            DisplayName = displayName;
            IsDark = isDark;
            Tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        }

        public override string ToString() => DisplayName;
    }
}
