using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Input.Masking
{
    /// <summary>
    /// Compiled, immutable definition of an input mask template.
    /// Provides zero-allocation formatting and extraction methods via Span.
    /// </summary>
    public class MaskDefinition
    {
        private readonly MaskToken[] _tokens;
        private readonly int _editableCount;

        public static readonly MaskDefinition IpV4 = new MaskDefinition("000.000.000.000");
        public static readonly MaskDefinition MacAddress = new MaskDefinition("HH:HH:HH:HH:HH:HH");
        public static readonly MaskDefinition IsoDate = new MaskDefinition("0000-00-00");
        public static readonly MaskDefinition IsoTime = new MaskDefinition("00:00:00");
        public static readonly MaskDefinition PhoneUS = new MaskDefinition("(000) 000-0000");
        public static readonly MaskDefinition LotCode = new MaskDefinition(@"\L\O\T-0000-AAAA");

        public string Pattern { get; }
        public IReadOnlyList<MaskToken> Tokens => _tokens;
        public int Length => _tokens.Length;
        public int EditableCount => _editableCount;

        public MaskDefinition(string pattern)
        {
            Pattern = pattern ?? string.Empty;
            var tokenList = new List<MaskToken>(Pattern.Length);
            bool isEscaped = false;
            int editCount = 0;

            for (int i = 0; i < Pattern.Length; i++)
            {
                char c = Pattern[i];

                if (isEscaped)
                {
                    tokenList.Add(new MaskToken(MaskTokenType.Literal, c));
                    isEscaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    isEscaped = true;
                    continue;
                }

                switch (c)
                {
                    case '0':
                        tokenList.Add(new MaskToken(MaskTokenType.DigitRequired));
                        editCount++;
                        break;
                    case '9':
                        tokenList.Add(new MaskToken(MaskTokenType.DigitOptional));
                        editCount++;
                        break;
                    case 'L':
                        tokenList.Add(new MaskToken(MaskTokenType.LetterRequired));
                        editCount++;
                        break;
                    case '?':
                        tokenList.Add(new MaskToken(MaskTokenType.LetterOptional));
                        editCount++;
                        break;
                    case 'A':
                        tokenList.Add(new MaskToken(MaskTokenType.AlphanumericRequired));
                        editCount++;
                        break;
                    case 'a':
                        tokenList.Add(new MaskToken(MaskTokenType.AlphanumericOptional));
                        editCount++;
                        break;
                    case 'H':
                        tokenList.Add(new MaskToken(MaskTokenType.Hex));
                        editCount++;
                        break;
                    case '&':
                        tokenList.Add(new MaskToken(MaskTokenType.Any));
                        editCount++;
                        break;
                    default:
                        tokenList.Add(new MaskToken(MaskTokenType.Literal, c));
                        break;
                }
            }

            _tokens = tokenList.ToArray();
            _editableCount = editCount;
        }

        public MaskToken this[int index] => _tokens[index];

        /// <summary>
        /// Finds the next editable slot at or after startIndex. Returns -1 if none found.
        /// </summary>
        public int FindNextEditable(int startIndex)
        {
            for (int i = Math.Max(0, startIndex); i < _tokens.Length; i++)
            {
                if (_tokens[i].IsEditable) return i;
            }
            return -1;
        }

        /// <summary>
        /// Finds the previous editable slot at or before startIndex. Returns -1 if none found.
        /// </summary>
        public int FindPreviousEditable(int startIndex)
        {
            for (int i = Math.Min(_tokens.Length - 1, startIndex); i >= 0; i--)
            {
                if (_tokens[i].IsEditable) return i;
            }
            return -1;
        }

        /// <summary>
        /// Formats raw unmasked string into the destination buffer according to this mask.
        /// </summary>
        public bool TryFormat(string? rawChars, Span<char> destination, out int charsWritten, char prompt = '_')
        {
            if (rawChars == null)
            {
                charsWritten = 0;
                return false;
            }
            return TryFormat(rawChars.AsSpan(), destination, out charsWritten, prompt);
        }

        /// <summary>
        /// Formats raw unmasked characters into the destination buffer according to this mask.
        /// </summary>
        public bool TryFormat(ReadOnlySpan<char> rawChars, Span<char> destination, out int charsWritten, char prompt = '_')
        {
            charsWritten = 0;
            if (destination.Length < _tokens.Length) return false;

            int rawIndex = 0;

            for (int i = 0; i < _tokens.Length; i++)
            {
                var token = _tokens[i];
                if (!token.IsEditable)
                {
                    destination[i] = token.LiteralChar;
                }
                else if (rawIndex < rawChars.Length)
                {
                    char c = rawChars[rawIndex];
                    if (token.Matches(c))
                    {
                        destination[i] = c;
                        rawIndex++;
                    }
                    else
                    {
                        destination[i] = prompt;
                    }
                }
                else
                {
                    destination[i] = prompt;
                }
            }

            charsWritten = _tokens.Length;
            return true;
        }

        /// <summary>
        /// Extracts raw characters out of a masked text string, skipping literals and prompt characters.
        /// </summary>
        public bool TryExtractRaw(string? maskedText, Span<char> destination, out int charsWritten, char prompt = '_')
        {
            if (maskedText == null)
            {
                charsWritten = 0;
                return false;
            }
            return TryExtractRaw(maskedText.AsSpan(), destination, out charsWritten, prompt);
        }

        /// <summary>
        /// Extracts raw characters out of a masked text string, skipping literals and prompt characters.
        /// </summary>
        public bool TryExtractRaw(ReadOnlySpan<char> maskedText, Span<char> destination, out int charsWritten, char prompt = '_')
        {
            charsWritten = 0;
            int destIndex = 0;
            int maxLen = Math.Min(maskedText.Length, _tokens.Length);

            for (int i = 0; i < maxLen; i++)
            {
                if (_tokens[i].IsEditable)
                {
                    char c = maskedText[i];
                    if (c != prompt && _tokens[i].Matches(c))
                    {
                        if (destIndex >= destination.Length) return false;
                        destination[destIndex++] = c;
                    }
                }
            }

            charsWritten = destIndex;
            return true;
        }
    }
}
