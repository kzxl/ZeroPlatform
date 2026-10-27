using System;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Helper utilities for text metric calculations (character count, word count, line count)
    /// and standardized UI counter badge formatting.
    /// </summary>
    public static class TextCounterHelper
    {
        /// <summary>
        /// Formats character counter badge text according to standard rules:
        /// If <paramref name="maxLength"/> > 0 and < 32767: "{currentLength} / {maxLength}"
        /// Otherwise: "{currentLength}" or "{currentLength} chars" if <paramref name="includeSuffix"/> is true.
        /// </summary>
        public static string FormatCharacterCount(int currentLength, int maxLength, bool includeSuffix = false)
        {
            if (maxLength > 0 && maxLength < 32767)
            {
                return $"{currentLength} / {maxLength}";
            }

            return includeSuffix ? $"{currentLength} chars" : currentLength.ToString();
        }

        /// <summary>
        /// High-performance word counter with zero heap allocation.
        /// </summary>
        public static int CountWords(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;

            int count = 0;
            bool inWord = false;
            var span = text.AsSpan();

            for (int i = 0; i < span.Length; i++)
            {
                if (char.IsWhiteSpace(span[i]))
                {
                    inWord = false;
                }
                else if (!inWord)
                {
                    inWord = true;
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// High-performance line counter with zero heap allocation.
        /// </summary>
        public static int CountLines(string? text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            int count = 1;
            var span = text.AsSpan();

            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] == '\n') count++;
            }

            return count;
        }
    }
}
