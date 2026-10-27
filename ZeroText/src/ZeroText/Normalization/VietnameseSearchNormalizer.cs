using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace ZeroText.Normalization
{
    /// <summary>
    /// High-performance, zero-allocation Vietnamese search normalizer and SEO slug generator.
    /// Converts diacritical Vietnamese text into canonical normalized search tokens or URLs directly on stack spans.
    /// </summary>
    public static class VietnameseSearchNormalizer
    {
        /// <summary>
        /// Normalizes Vietnamese text (both precomposed NFC and decomposed NFD) into unaccented, lowercase characters
        /// directly into the output span, preserving original whitespace/punctuation while stripping combining diacritical marks.
        /// Returns the actual number of characters written (which may be less than input.Length when NFD combining marks are stripped).
        /// </summary>
        public static int NormalizeToSpan(ReadOnlySpan<char> input, Span<char> output)
        {
            if (input.IsEmpty || output.IsEmpty) return 0;

            int written = 0;
            int i = 0;
            int len = input.Length;
            int outLen = output.Length;

            while (i < len && written < outLen)
            {
                char c = input[i++];
                if (c < 0x80)
                {
                    output[written++] = (uint)(c - 'A') <= ('Z' - 'A') ? (char)(c | 0x20) : c;
                    while (i < len && written < outLen)
                    {
                        char ac = input[i];
                        if (ac >= 0x80) break;
                        output[written++] = (uint)(ac - 'A') <= ('Z' - 'A') ? (char)(ac | 0x20) : ac;
                        i++;
                    }
                    continue;
                }

                // Skip Unicode Combining Diacritical Marks (NFD)
                if (c >= '\u0300' && c <= '\u036F')
                {
                    continue;
                }

                if (c == 'Đ' || c == 'đ')
                {
                    output[written++] = 'd';
                }
                else
                {
                    char stripped = StripDiacritic(c);
                    output[written++] = (uint)(stripped - 'A') <= ('Z' - 'A') ? (char)(stripped | 0x20) : stripped;
                }
            }

            return written;
        }

        /// <summary>
        /// Normalizes Vietnamese text (both NFC and NFD) into unaccented, lowercase characters in the output span.
        /// </summary>
        public static void Normalize(ReadOnlySpan<char> input, Span<char> output)
        {
            int written = NormalizeToSpan(input, output);
            if (written < output.Length)
            {
                output.Slice(written).Clear();
            }
        }

        /// <summary>
        /// Normalizes Vietnamese text (both NFC and NFD) into an unaccented, lowercase string,
        /// slicing to the exact normalized length when NFD combining marks are stripped.
        /// </summary>
        public static string Normalize(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            var span = input.AsSpan();
            if (span.Length <= 256)
            {
                Span<char> buffer = stackalloc char[span.Length];
                int written = NormalizeToSpan(span, buffer);
                return buffer.Slice(0, written).ToString();
            }

            char[] rented = ArrayPool<char>.Shared.Rent(span.Length);
            try
            {
                int written = NormalizeToSpan(span, rented);
                return new string(rented, 0, written);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Normalizes Vietnamese text into unaccented, lowercase search tokens directly into destination span.
        /// Replaces 'Đ'/'đ' with 'd', strips NFC &amp; NFD tone marks, removes special symbols, and condenses spaces.
        /// Zero heap allocation.
        /// </summary>
        /// <param name="source">Raw input text span.</param>
        /// <param name="destination">Output buffer span.</param>
        /// <returns>Number of characters written to destination.</returns>
        public static int NormalizeForSearch(ReadOnlySpan<char> source, Span<char> destination)
        {
            if (source.IsEmpty || destination.IsEmpty) return 0;

            int written = 0;
            bool lastWasSpace = false;

            for (int i = 0; i < source.Length && written < destination.Length; i++)
            {
                char c = source[i];

                // Fast path for ASCII characters
                if (c < 0x80)
                {
                    if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                    {
                        destination[written++] = c;
                        lastWasSpace = false;
                    }
                    else if (c >= 'A' && c <= 'Z')
                    {
                        destination[written++] = (char)(c | 0x20);
                        lastWasSpace = false;
                    }
                    else if ((c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '_' || c == '-') && !lastWasSpace && written > 0)
                    {
                        destination[written++] = ' ';
                        lastWasSpace = true;
                    }
                    continue;
                }

                // Skip Unicode Combining Diacritical Marks (U+0300..U+036F) in NFD strings
                if (c >= '\u0300' && c <= '\u036F')
                {
                    continue;
                }

                if (c == 'Đ' || c == 'đ')
                {
                    c = 'd';
                }
                else
                {
                    c = StripDiacritic(c);
                }

                // Lowercase
                if (c >= 'A' && c <= 'Z')
                {
                    c = (char)(c + 32);
                }

                // Keep alphanumeric
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    destination[written++] = c;
                    lastWasSpace = false;
                }
                else if ((char.IsWhiteSpace(c) || c == '_' || c == '-') && !lastWasSpace && written > 0)
                {
                    destination[written++] = ' ';
                    lastWasSpace = true;
                }
            }

            // Remove trailing space
            if (written > 0 && destination[written - 1] == ' ')
            {
                written--;
            }

            return written;
        }

        /// <summary>
        /// Tries to normalize Vietnamese text into destination span.
        /// Returns false if destination is smaller than source length.
        /// </summary>
        public static bool TryNormalizeToBuffer(ReadOnlySpan<char> source, Span<char> destination, out int charsWritten)
        {
            if (source.IsEmpty)
            {
                charsWritten = 0;
                return true;
            }

            if (destination.Length < source.Length)
            {
                charsWritten = 0;
                return false;
            }

            charsWritten = NormalizeForSearch(source, destination);
            return true;
        }

        /// <summary>
        /// Converts Vietnamese string into an unaccented, lowercase search string.
        /// </summary>
        public static string ToSearchKeyword(string? source)
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;

            var span = source.AsSpan();
            if (span.Length <= 256)
            {
                Span<char> buffer = stackalloc char[span.Length];
                int written = NormalizeForSearch(span, buffer);
                return buffer.Slice(0, written).ToString();
            }

            char[] rented = ArrayPool<char>.Shared.Rent(span.Length);
            try
            {
                int written = NormalizeForSearch(span, rented);
                return new string(rented, 0, written);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Generates a clean, SEO-friendly slug with hyphen separators (e.g. "don-hang-so-123").
        /// Zero heap allocation.
        /// </summary>
        public static int ToSlug(ReadOnlySpan<char> source, Span<char> destination)
        {
            if (source.IsEmpty || destination.IsEmpty) return 0;

            int written = 0;
            bool lastWasHyphen = false;

            for (int i = 0; i < source.Length && written < destination.Length; i++)
            {
                char c = source[i];

                if (c < 0x80)
                {
                    if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                    {
                        destination[written++] = c;
                        lastWasHyphen = false;
                    }
                    else if (c >= 'A' && c <= 'Z')
                    {
                        destination[written++] = (char)(c | 0x20);
                        lastWasHyphen = false;
                    }
                    else if ((c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '-' || c == '_' || c == '/') && !lastWasHyphen && written > 0)
                    {
                        destination[written++] = '-';
                        lastWasHyphen = true;
                    }
                    continue;
                }

                // Skip Unicode Combining Diacritical Marks (NFD)
                if (c >= '\u0300' && c <= '\u036F')
                {
                    continue;
                }

                if (c == 'Đ' || c == 'đ')
                {
                    c = 'd';
                }
                else
                {
                    c = StripDiacritic(c);
                }

                // Lowercase
                if (c >= 'A' && c <= 'Z')
                {
                    c = (char)(c + 32);
                }

                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    destination[written++] = c;
                    lastWasHyphen = false;
                }
                else if ((char.IsWhiteSpace(c) || c == '-' || c == '_' || c == '/') && !lastWasHyphen && written > 0)
                {
                    destination[written++] = '-';
                    lastWasHyphen = true;
                }
            }

            if (written > 0 && destination[written - 1] == '-')
            {
                written--;
            }

            return written;
        }

        /// <summary>
        /// Converts string into a clean SEO slug.
        /// </summary>
        public static string ToSlug(string? source)
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;

            var span = source.AsSpan();
            if (span.Length <= 256)
            {
                Span<char> buffer = stackalloc char[span.Length];
                int written = ToSlug(span, buffer);
                return buffer.Slice(0, written).ToString();
            }

            char[] rented = ArrayPool<char>.Shared.Rent(span.Length);
            try
            {
                int written = ToSlug(span, rented);
                return new string(rented, 0, written);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Removes diacritics (both NFC precomposed and NFD combining marks) from Vietnamese text into a destination span
        /// while preserving letter casing and special symbols. Returns the number of characters written.
        /// </summary>
        public static int RemoveDiacriticsToSpan(ReadOnlySpan<char> source, Span<char> destination)
        {
            if (source.IsEmpty || destination.IsEmpty) return 0;

            int written = 0;
            int i = 0;
            int len = source.Length;
            int destLen = destination.Length;

            while (i < len && written < destLen)
            {
                char c = source[i++];
                if (c < 0x80)
                {
                    destination[written++] = c;
                    while (i < len && written < destLen)
                    {
                        char ac = source[i];
                        if (ac >= 0x80) break;
                        destination[written++] = ac;
                        i++;
                    }
                    continue;
                }

                // Skip Unicode Combining Diacritical Marks (NFD)
                if (c >= '\u0300' && c <= '\u036F')
                {
                    continue;
                }

                destination[written++] = StripDiacriticPreserveCase(c);
            }

            return written;
        }

        /// <summary>
        /// Removes diacritics from Vietnamese string while preserving letter casing and special symbols.
        /// Supports both Unicode NFC and NFD. Zero-allocation on small strings (&lt;= 256 chars via stackalloc).
        /// </summary>
        public static string RemoveDiacritics(string? source)
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;

            var span = source.AsSpan();
            if (span.Length <= 256)
            {
                Span<char> buffer = stackalloc char[span.Length];
                int written = RemoveDiacriticsToSpan(span, buffer);
                return buffer.Slice(0, written).ToString();
            }

            char[] rented = ArrayPool<char>.Shared.Rent(span.Length);
            try
            {
                int written = RemoveDiacriticsToSpan(span, rented);
                return new string(rented, 0, written);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Converts Vietnamese string into unaccented, URL-safe transfer string:
        /// Replaces special characters [~`!@#$%^&amp;*()+=''] with '-', spaces with '_', and strips NFC &amp; NFD diacritics.
        /// Zero-allocation on small strings (&lt;= 256 chars via stackalloc).
        /// </summary>
        public static string UnSignedTransfer(string? source)
        {
            if (string.IsNullOrWhiteSpace(source)) return string.Empty;

            var span = source.AsSpan();
            if (span.Length <= 256)
            {
                Span<char> buffer = stackalloc char[span.Length];
                int written = 0;
                for (int i = 0; i < span.Length; i++)
                {
                    char c = span[i];
                    if (c >= '\u0300' && c <= '\u036F') continue;
                    if (c == ' ') c = '_';
                    else if (IsSpecialTransferChar(c)) c = '-';
                    else if (c >= 0x80) c = StripDiacriticPreserveCase(c);
                    buffer[written++] = c;
                }
                return buffer.Slice(0, written).ToString();
            }

            char[] rented = ArrayPool<char>.Shared.Rent(span.Length);
            try
            {
                int written = 0;
                for (int i = 0; i < span.Length; i++)
                {
                    char c = span[i];
                    if (c >= '\u0300' && c <= '\u036F') continue;
                    if (c == ' ') c = '_';
                    else if (IsSpecialTransferChar(c)) c = '-';
                    else if (c >= 0x80) c = StripDiacriticPreserveCase(c);
                    rented[written++] = c;
                }
                return new string(rented, 0, written);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSpecialTransferChar(char c)
        {
            return c == '~' || c == '`' || c == '!' || c == '@' || c == '#' ||
                   c == '$' || c == '%' || c == '^' || c == '&' || c == '*' ||
                   c == '(' || c == ')' || c == '+' || c == '=' || c == '\'';
        }

        /// <summary>
        /// Strips diacritics from a Vietnamese character while preserving original casing.
        /// Maps 'đ'/'Đ' to 'd'/'D'.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char StripDiacriticPreserveCase(char c)
        {
            switch (c)
            {
                case 'á': case 'à': case 'ả': case 'ã': case 'ạ':
                case 'ă': case 'ắ': case 'ằ': case 'ẳ': case 'ẵ': case 'ặ':
                case 'â': case 'ấ': case 'ầ': case 'ẩ': case 'ẫ': case 'ậ':
                    return 'a';
                case 'Á': case 'À': case 'Ả': case 'Ã': case 'Ạ':
                case 'Ă': case 'Ắ': case 'Ằ': case 'Ẳ': case 'Ẵ': case 'Ặ':
                case 'Â': case 'Ấ': case 'Ầ': case 'Ẩ': case 'Ẫ': case 'Ậ':
                    return 'A';

                case 'é': case 'è': case 'ẻ': case 'ẽ': case 'ẹ':
                case 'ê': case 'ế': case 'ề': case 'ể': case 'ễ': case 'ệ':
                    return 'e';
                case 'É': case 'È': case 'Ẻ': case 'Ẽ': case 'Ẹ':
                case 'Ê': case 'Ế': case 'Ề': case 'Ể': case 'Ễ': case 'Ệ':
                    return 'E';

                case 'í': case 'ì': case 'ỉ': case 'ĩ': case 'ị':
                    return 'i';
                case 'Í': case 'Ì': case 'Ỉ': case 'Ĩ': case 'Ị':
                    return 'I';

                case 'ó': case 'ò': case 'ỏ': case 'õ': case 'ọ':
                case 'ô': case 'ố': case 'ồ': case 'ổ': case 'ỗ': case 'ộ':
                case 'ơ': case 'ớ': case 'ờ': case 'ở': case 'ỡ': case 'ợ':
                    return 'o';
                case 'Ó': case 'Ò': case 'Ỏ': case 'Õ': case 'Ọ':
                case 'Ô': case 'Ố': case 'Ồ': case 'Ổ': case 'Ỗ': case 'Ộ':
                case 'Ơ': case 'Ớ': case 'Ờ': case 'Ở': case 'Ỡ': case 'Ợ':
                    return 'O';

                case 'ú': case 'ù': case 'ủ': case 'ũ': case 'ụ':
                case 'ư': case 'ứ': case 'ừ': case 'ử': case 'ữ': case 'ự':
                    return 'u';
                case 'Ú': case 'Ù': case 'Ủ': case 'Ũ': case 'Ụ':
                case 'Ư': case 'Ứ': case 'Ừ': case 'Ử': case 'Ữ': case 'Ự':
                    return 'U';

                case 'ý': case 'ỳ': case 'ỷ': case 'ỹ': case 'ỵ':
                    return 'y';
                case 'Ý': case 'Ỳ': case 'Ỷ': case 'Ỹ': case 'Ỵ':
                    return 'Y';

                case 'đ': return 'd';
                case 'Đ': return 'D';

                default:
                    return c;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static char StripDiacritic(char c)
        {
            switch (c)
            {
                case 'á': case 'à': case 'ả': case 'ã': case 'ạ':
                case 'ă': case 'ắ': case 'ằ': case 'ẳ': case 'ẵ': case 'ặ':
                case 'â': case 'ấ': case 'ầ': case 'ẩ': case 'ẫ': case 'ậ':
                case 'Á': case 'À': case 'Ả': case 'Ã': case 'Ạ':
                case 'Ă': case 'Ắ': case 'Ằ': case 'Ẳ': case 'Ẵ': case 'Ặ':
                case 'Â': case 'Ấ': case 'Ầ': case 'Ẩ': case 'Ẫ': case 'Ậ':
                    return 'a';

                case 'é': case 'è': case 'ẻ': case 'ẽ': case 'ẹ':
                case 'ê': case 'ế': case 'ề': case 'ể': case 'ễ': case 'ệ':
                case 'É': case 'È': case 'Ẻ': case 'Ẽ': case 'Ẹ':
                case 'Ê': case 'Ế': case 'Ề': case 'Ể': case 'Ễ': case 'Ệ':
                    return 'e';

                case 'í': case 'ì': case 'ỉ': case 'ĩ': case 'ị':
                case 'Í': case 'Ì': case 'Ỉ': case 'Ĩ': case 'Ị':
                    return 'i';

                case 'ó': case 'ò': case 'ỏ': case 'õ': case 'ọ':
                case 'ô': case 'ố': case 'ồ': case 'ổ': case 'ỗ': case 'ộ':
                case 'ơ': case 'ớ': case 'ờ': case 'ở': case 'ỡ': case 'ợ':
                    return 'o';
                case 'Ó': case 'Ò': case 'Ỏ': case 'Õ': case 'Ọ':
                case 'Ô': case 'Ố': case 'Ồ': case 'Ổ': case 'Ỗ': case 'Ộ':
                case 'Ơ': case 'Ớ': case 'Ờ': case 'Ở': case 'Ỡ': case 'Ợ':
                    return 'o';

                case 'ú': case 'ù': case 'ủ': case 'ũ': case 'ụ':
                case 'ư': case 'ứ': case 'ừ': case 'ử': case 'ữ': case 'ự':
                case 'Ú': case 'Ù': case 'Ủ': case 'Ũ': case 'Ụ':
                case 'Ư': case 'Ứ': case 'Ừ': case 'Ử': case 'Ữ': case 'Ự':
                    return 'u';

                case 'ý': case 'ỳ': case 'ỷ': case 'ỹ': case 'ỵ':
                case 'Ý': case 'Ỳ': case 'Ỷ': case 'Ỹ': case 'Ỵ':
                    return 'y';

                default:
                    return c;
            }
        }
    }
}
