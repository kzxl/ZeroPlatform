using System;
using System.Globalization;
using System.Text;

namespace ZeroPrimitives.Extensions
{
    /// <summary>
    /// Core string extension methods for path manipulation, query strings, trimming, and digit extraction.
    /// </summary>
    public static class StringExtensions
    {
        public static string? NullIfEmpty(this string? input)
            => string.IsNullOrWhiteSpace(input) ? null : input;

        /// <summary>
        /// Truncates string to a maximum length with optional suffix.
        /// </summary>
        public static string Truncate(this string? input, int maxLength, string suffix = "...")
        {
            if (string.IsNullOrEmpty(input) || maxLength <= 0) return string.Empty;
            if (input!.Length <= maxLength) return input;

            int cutLen = Math.Max(0, maxLength - suffix.Length);
            return input.Substring(0, cutLen) + suffix;
        }

        /// <summary>
        /// Removes a specified number of characters from the start or end of the string.
        /// </summary>
        public static string TrimChars(this string? text, int count = 1, bool removeFromStart = false)
        {
            if (string.IsNullOrEmpty(text) || count <= 0) return text ?? string.Empty;
            if (count >= text!.Length) return string.Empty;

            return removeFromStart
                ? text.Substring(count)
                : text.Substring(0, text.Length - count);
        }

        /// <summary>
        /// Combines URL segments cleanly avoiding duplicate slashes.
        /// </summary>
        public static string AppendPathSegments(this string baseUrl, params string[] segments)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) return string.Empty;
            var sb = new StringBuilder(baseUrl.TrimEnd('/'));

            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment)) continue;
                sb.Append('/');
                sb.Append(segment.Trim('/'));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Adds or updates a query parameter in a URL string.
        /// </summary>
        public static string SetQueryParam(this string url, string key, object? value)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            if (value == null) return url;

            string valStr = Uri.EscapeDataString(value.ToString() ?? string.Empty);
            char separator = url.Contains("?") ? '&' : '?';
            return $"{url}{separator}{Uri.EscapeDataString(key)}={valStr}";
        }

        /// <summary>
        /// Extracts only digit characters (0-9) from a string.
        /// </summary>
        public static string ExtractDigits(this string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text!.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= '0' && c <= '9') sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Extracts only digit characters (0-9) and converts them to an integer in a single zero-allocation pass.
        /// Standard replacement for legacy RemoveLetterToInt.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static int ExtractDigitsToInt(this object? value, int defaultValue = 0)
        {
            if (value == null) return defaultValue;
            if (value is int i) return i;
            string str = value.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(str)) return defaultValue;

            long acc = 0;
            bool hasDigits = false;

            for (int idx = 0; idx < str.Length; idx++)
            {
                char c = str[idx];
                if (c >= '0' && c <= '9')
                {
                    acc = (acc * 10) + (c - '0');
                    hasDigits = true;
                    if (acc > int.MaxValue) return int.MaxValue;
                }
            }

            return hasDigits ? (int)acc : defaultValue;
        }

        /// <summary>
        /// Legacy alias for ExtractDigitsToInt.
        /// </summary>
        public static int RemoveLetterToInt(this object? value, int defaultValue = 0)
            => ExtractDigitsToInt(value, defaultValue);

        /// <summary>
        /// Removes Vietnamese diacritical marks (accents) and maps đ/Đ to d/D.
        /// </summary>
        public static string RemoveDiacritics(this string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            string normalized = text!.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);

            foreach (char c in normalized)
            {
                switch (c)
                {
                    case 'đ': sb.Append('d'); continue;
                    case 'Đ': sb.Append('D'); continue;
                }

                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
