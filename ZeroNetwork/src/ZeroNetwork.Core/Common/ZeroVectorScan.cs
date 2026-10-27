using System;
using System.Runtime.CompilerServices;

namespace ZeroNetwork.Common
{
    /// <summary>
    /// Vectorized byte scanning and delimiter search engine implementing SWAR (SIMD Within A Register)
    /// algorithms on 64-bit words. Replaces sequential BCL <see cref="MemoryExtensions.IndexOf{T}(ReadOnlySpan{T}, T)"/>
    /// and string searches for delimiters like CRLF (\r\n), colon (:), and space ( ) in HTTP and WebSocket parsers.
    /// </summary>
    public static class ZeroVectorScan
    {
        private const ulong Repeat01 = 0x0101010101010101UL;
        private const ulong Repeat80 = 0x8080808080808080UL;

        /// <summary>
        /// Finds the first index of the specified byte within a span using 64-bit parallel SWAR comparison.
        /// Returns -1 if not found.
        /// </summary>
        public static unsafe int IndexOf(ReadOnlySpan<byte> span, byte target)
        {
            if (span.IsEmpty) return -1;

            fixed (byte* pSpan = span)
            {
                byte* ptr = pSpan;
                int len = span.Length;
                int offset = 0;

                ulong pattern = (ulong)target * Repeat01;

                while (len >= 8)
                {
                    ulong word = *(ulong*)ptr;
                    ulong diff = word ^ pattern;
                    ulong zeroBytes = (diff - Repeat01) & ~diff & Repeat80;

                    if (zeroBytes != 0)
                    {
                        int byteIdx = ZeroBitOps.TrailingZeroCount(zeroBytes) >> 3;
                        return offset + byteIdx;
                    }

                    ptr += 8;
                    offset += 8;
                    len -= 8;
                }

                while (len > 0)
                {
                    if (*ptr == target)
                    {
                        return offset;
                    }
                    ptr++;
                    offset++;
                    len--;
                }
            }

            return -1;
        }

        /// <summary>
        /// Finds the index of the HTTP/WebSocket CRLF ("\r\n", 0x0D 0x0A) delimiter sequence.
        /// Returns -1 if not found.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int IndexOfCrLf(ReadOnlySpan<byte> span)
        {
            if (span.Length < 2) return -1;

            int searchLen = span.Length - 1;
            int startIdx = 0;

            while (startIdx < searchLen)
            {
                int crIdx = IndexOf(span.Slice(startIdx, searchLen - startIdx), (byte)'\r');
                if (crIdx < 0) return -1;

                int absoluteIdx = startIdx + crIdx;
                if (span[absoluteIdx + 1] == (byte)'\n')
                {
                    return absoluteIdx;
                }

                startIdx = absoluteIdx + 1;
            }

            return -1;
        }

        /// <summary>
        /// Finds the first index of colon (':', 0x3A) delimiter commonly used in HTTP headers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOfColon(ReadOnlySpan<byte> span)
        {
            return IndexOf(span, (byte)':');
        }

        /// <summary>
        /// Finds the first index of space (' ', 0x20) delimiter commonly used in HTTP request lines.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOfSpace(ReadOnlySpan<byte> span)
        {
            return IndexOf(span, (byte)' ');
        }
    }
}
