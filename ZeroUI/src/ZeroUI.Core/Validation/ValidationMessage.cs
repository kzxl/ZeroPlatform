using System;

namespace ZeroUI.Core.Validation
{
    /// <summary>
    /// Immutable value representing an individual validation feedback item.
    /// </summary>
    public readonly struct ValidationMessage : IEquatable<ValidationMessage>
    {
        public string Text { get; }
        public ValidationSeverity Severity { get; }
        public string? PropertyName { get; }

        public ValidationMessage(string text, ValidationSeverity severity = ValidationSeverity.Error, string? propertyName = null)
        {
            Text = text ?? string.Empty;
            Severity = severity;
            PropertyName = propertyName;
        }

        public bool Equals(ValidationMessage other) =>
            Text == other.Text && Severity == other.Severity && PropertyName == other.PropertyName;

        public override bool Equals(object? obj) => obj is ValidationMessage other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (Text != null ? Text.GetHashCode() : 0);
                hash = (hash * 397) ^ (int)Severity;
                hash = (hash * 397) ^ (PropertyName != null ? PropertyName.GetHashCode() : 0);
                return hash;
            }
        }

        public override string ToString() => $"[{Severity}] {Text}";

        public static bool operator ==(ValidationMessage left, ValidationMessage right) => left.Equals(right);
        public static bool operator !=(ValidationMessage left, ValidationMessage right) => !left.Equals(right);
    }
}
