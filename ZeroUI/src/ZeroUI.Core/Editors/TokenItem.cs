using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Represents a discrete badge or chip token within a <c>TokenEdit</c>.
    /// </summary>
    public class TokenItem : IEquatable<TokenItem>
    {
        public string Text { get; set; } = string.Empty;
        public string Token => Text;
        public object? Value { get; set; }
        public string? Glyph { get; set; }
        public string? ColorHex { get; set; }
        public object? Tag { get; set; }

        public TokenItem() { }

        public TokenItem(string text)
        {
            Text = text ?? string.Empty;
            Value = text;
        }

        public TokenItem(string text, object? value)
        {
            Text = text ?? string.Empty;
            Value = value ?? text;
        }

        public TokenItem(string text, object? value, string? glyph, string? colorHex = null)
        {
            Text = text ?? string.Empty;
            Value = value ?? text;
            Glyph = glyph;
            ColorHex = colorHex;
        }

        public override string ToString() => Text;

        public bool Equals(TokenItem? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Text, other.Text, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj) => obj is TokenItem other && Equals(other);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Text);

        public static implicit operator TokenItem(string text) => new TokenItem(text);
        public static implicit operator string(TokenItem item) => item?.Text ?? string.Empty;

        public static bool operator ==(TokenItem? left, TokenItem? right) => Equals(left, right);
        public static bool operator !=(TokenItem? left, TokenItem? right) => !Equals(left, right);
    }

    /// <summary>
    /// Event arguments raised when the tokens collection in <c>TokenEdit</c> is modified.
    /// </summary>
    public class TokenChangedEventArgs : EventArgs
    {
        public TokenChangeAction Action { get; }
        public TokenItem? Item { get; }

        public TokenChangedEventArgs(TokenChangeAction action, TokenItem? item = null)
        {
            Action = action;
            Item = item;
        }
    }

    public enum TokenChangeAction
    {
        Added,
        Removed,
        Cleared
    }
}
