using System;

namespace ZeroUI.Core.Input.Masking
{
    /// <summary>
    /// Type of slot or literal within an input mask.
    /// </summary>
    public enum MaskTokenType : byte
    {
        /// <summary>Fixed constant character (e.g. '.', ':', '-', '/'). Non-editable.</summary>
        Literal = 0,

        /// <summary>Required digit 0-9 ('0').</summary>
        DigitRequired = 1,

        /// <summary>Optional digit 0-9 or space ('9').</summary>
        DigitOptional = 2,

        /// <summary>Required ASCII letter a-z, A-Z ('L').</summary>
        LetterRequired = 3,

        /// <summary>Optional letter or space ('?').</summary>
        LetterOptional = 4,

        /// <summary>Required alphanumeric character 0-9, a-z, A-Z ('A').</summary>
        AlphanumericRequired = 5,

        /// <summary>Optional alphanumeric character or space ('a').</summary>
        AlphanumericOptional = 6,

        /// <summary>Hexadecimal digit 0-9, a-f, A-F ('H').</summary>
        Hex = 7,

        /// <summary>Any character ('&').</summary>
        Any = 8
    }

    /// <summary>
    /// Lightweight, zero-allocation representation of a single position in an input mask.
    /// </summary>
    public readonly struct MaskToken : IEquatable<MaskToken>
    {
        public MaskTokenType Type { get; }
        public char LiteralChar { get; }

        public bool IsEditable => Type != MaskTokenType.Literal;

        public MaskToken(MaskTokenType type, char literalChar = '\0')
        {
            Type = type;
            LiteralChar = literalChar;
        }

        /// <summary>
        /// Validates whether a character satisfies this token's rule.
        /// </summary>
        public bool Matches(char c)
        {
            switch (Type)
            {
                case MaskTokenType.Literal:
                    return c == LiteralChar;

                case MaskTokenType.DigitRequired:
                    return c >= '0' && c <= '9';

                case MaskTokenType.DigitOptional:
                    return (c >= '0' && c <= '9') || c == ' ';

                case MaskTokenType.LetterRequired:
                    return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

                case MaskTokenType.LetterOptional:
                    return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == ' ';

                case MaskTokenType.AlphanumericRequired:
                    return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

                case MaskTokenType.AlphanumericOptional:
                    return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == ' ';

                case MaskTokenType.Hex:
                    return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

                case MaskTokenType.Any:
                    return true;

                default:
                    return false;
            }
        }

        public bool Equals(MaskToken other) => Type == other.Type && LiteralChar == other.LiteralChar;
        public override bool Equals(object? obj) => obj is MaskToken other && Equals(other);
        public override int GetHashCode() => (int)Type ^ (LiteralChar << 8);
        public static bool operator ==(MaskToken left, MaskToken right) => left.Equals(right);
        public static bool operator !=(MaskToken left, MaskToken right) => !left.Equals(right);

        public override string ToString() => IsEditable ? Type.ToString() : $"Literal('{LiteralChar}')";
    }
}
