using System;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Text
{
    /// <summary>
    /// High-performance, zero-allocation sequential alpha sequence generator and incrementer.
    /// Implements Bijective Base-26 increment: A->B, Z->AA, AZ->BA, ZZ->AAA.
    /// Useful for Excel column indexing, serial label numbering, and sequential revision codes.
    /// </summary>
    public static class AlphaSequence
    {
        /// <summary>
        /// Increments an alphabetical sequence in-place without heap allocations.
        /// Example: "A" -> "B", "Z" -> "AA", "AZ" -> "BA", "ZZ" -> "AAA".
        /// </summary>
        /// <param name="input">Input alpha sequence.</param>
        /// <param name="output">Target buffer to receive the incremented sequence.</param>
        /// <param name="charsWritten">The actual number of characters written to output.</param>
        /// <returns>True if increment succeeded and fit in output; otherwise false.</returns>
        public static bool TryIncrement(ReadOnlySpan<char> input, Span<char> output, out int charsWritten)
        {
            input = SpanTextOps.TrimAsciiWhitespace(input);
            if (input.IsEmpty)
            {
                if (output.IsEmpty)
                {
                    charsWritten = 0;
                    return false;
                }
                output[0] = 'A';
                charsWritten = 1;
                return true;
            }

            // Check if all characters are 'Z' or 'z' (overflow condition expanding sequence length)
            bool allZ = true;
            for (int j = 0; j < input.Length; j++)
            {
                char c = input[j];
                if (c != 'Z' && c != 'z')
                {
                    allZ = false;
                    break;
                }
            }

            int requiredLength = allZ ? input.Length + 1 : input.Length;
            if (output.Length < requiredLength)
            {
                charsWritten = 0;
                return false;
            }

            if (allZ)
            {
                output.Slice(0, requiredLength).Fill('A');
                charsWritten = requiredLength;
                return true;
            }

            // Copy input into output (normalizing to uppercase)
            for (int k = 0; k < input.Length; k++)
            {
                char c = input[k];
                output[k] = (c >= 'a' && c <= 'z') ? (char)(c - 32) : c;
            }

            int i = input.Length - 1;
            while (i >= 0)
            {
                char c = output[i];
                if (c < 'Z' && c >= 'A')
                {
                    output[i] = (char)(c + 1);
                    charsWritten = input.Length;
                    return true;
                }
                else if (c == 'Z')
                {
                    output[i] = 'A';
                    i--;
                }
                else
                {
                    // If non-alpha encountered, treat as 'A'
                    output[i] = 'A';
                    i--;
                }
            }

            // If we somehow wrapped around (fallback)
            output[0] = 'A';
            charsWritten = input.Length;
            return true;
        }

        /// <summary>
        /// Increments an alpha sequence from ReadOnlySpan, returning the new string.
        /// Uses stack allocation for strings up to 64 characters to eliminate array allocations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Increment(ReadOnlySpan<char> input)
        {
            input = SpanTextOps.TrimAsciiWhitespace(input);
            if (input.IsEmpty) return "A";

            int maxLen = input.Length + 2;
            if (maxLen <= 64)
            {
                Span<char> buffer = stackalloc char[maxLen];
                if (TryIncrement(input, buffer, out int written))
                {
#if NET8_0_OR_GREATER
                    return new string(buffer.Slice(0, written));
#else
                    return new string(buffer.Slice(0, written).ToArray());
#endif
                }
            }

            // Fallback for extremely long sequences
            char[] heapBuf = new char[maxLen];
            if (TryIncrement(input, heapBuf.AsSpan(), out int heapWritten))
            {
                return new string(heapBuf, 0, heapWritten);
            }

            return "A";
        }

        /// <summary>
        /// Increments an alpha sequence string, returning the next sequence string.
        /// Example: "A"->"B", "Z"->"AA", "AA"->"AB", "AZ"->"BA", "ZZ"->"AAA".
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Increment(string? input)
        {
            if (string.IsNullOrEmpty(input)) return "A";
            return Increment(input.AsSpan());
        }
    }
}
