using System;
using System.Runtime.CompilerServices;
using ZeroPrimitives.Internal;

namespace ZeroPrimitives.Buffers
{
    /// <summary>
    /// Ultra-fast, zero-allocation hexadecimal encoder and decoder.
    /// Optimized for RFID EPC/TID strings, checksums, MAC addresses, and industrial IoT payloads.
    /// Compatible with .NET Standard 2.0, .NET Framework 4.6.2, and .NET 8.0+.
    /// </summary>
    public static class FastHex
    {
        private const string HexDigitsUpper = "0123456789ABCDEF";
        private const string HexDigitsLower = "0123456789abcdef";

        private static readonly sbyte[] HexLookupTable = CreateHexLookupTable();

        private static sbyte[] CreateHexLookupTable()
        {
            sbyte[] table = new sbyte[256];
            for (int i = 0; i < 256; i++)
            {
                table[i] = -1;
            }

            for (int i = 0; i < 10; i++)
            {
                table['0' + i] = (sbyte)i;
            }

            for (int i = 0; i < 6; i++)
            {
                table['A' + i] = (sbyte)(10 + i);
                table['a' + i] = (sbyte)(10 + i);
            }

            return table;
        }

        /// <summary>
        /// Encodes a binary span into a hexadecimal character span.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Encode(ReadOnlySpan<byte> source, Span<char> destination, bool lowercase = false)
        {
            if (!TryEncode(source, destination, out int charsWritten, lowercase))
            {
                ThrowHelper.ThrowArgumentException("Destination span is too small for hex encoded output.", nameof(destination));
            }
            return charsWritten;
        }

        /// <summary>
        /// Attempts to encode a binary span into a hexadecimal character span without allocating heap memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool TryEncode(ReadOnlySpan<byte> source, Span<char> destination, out int charsWritten, bool lowercase = false)
        {
            int requiredLength = source.Length * 2;
            if (destination.Length < requiredLength)
            {
                charsWritten = 0;
                return false;
            }

            if (source.IsEmpty)
            {
                charsWritten = 0;
                return true;
            }

            string digits = lowercase ? HexDigitsLower : HexDigitsUpper;

            fixed (byte* srcPtr = source)
            fixed (char* dstPtr = destination)
            fixed (char* digPtr = digits)
            {
                byte* src = srcPtr;
                byte* srcEnd = srcPtr + source.Length;
                char* dst = dstPtr;

                while (src < srcEnd)
                {
                    byte b = *src++;
                    *dst++ = digPtr[b >> 4];
                    *dst++ = digPtr[b & 0x0F];
                }
            }

            charsWritten = requiredLength;
            return true;
        }

        /// <summary>
        /// Decodes a hexadecimal character span into a byte destination span.
        /// Throws FormatException if the hex string has invalid characters or an odd length.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Decode(ReadOnlySpan<char> hex, Span<byte> destination)
        {
            if ((hex.Length & 1) != 0)
            {
                ThrowHelper.ThrowFormatException("Hexadecimal character length must be even.");
            }

            if (destination.Length < hex.Length / 2)
            {
                ThrowHelper.ThrowArgumentException("Destination span is too small to receive decoded bytes.", nameof(destination));
            }

            if (!TryDecode(hex, destination, out int bytesWritten))
            {
                ThrowHelper.ThrowFormatException("Invalid hexadecimal character encountered during decoding.");
            }

            return bytesWritten;
        }

        /// <summary>
        /// Attempts to decode a hexadecimal character span into a byte span without throwing exceptions or allocating heap memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool TryDecode(ReadOnlySpan<char> hex, Span<byte> destination, out int bytesWritten)
        {
            if ((hex.Length & 1) != 0)
            {
                bytesWritten = 0;
                return false;
            }

            int requiredBytes = hex.Length / 2;
            if (destination.Length < requiredBytes)
            {
                bytesWritten = 0;
                return false;
            }

            if (hex.IsEmpty)
            {
                bytesWritten = 0;
                return true;
            }

            fixed (char* hPtr = hex)
            fixed (byte* dPtr = destination)
            fixed (sbyte* lPtr = HexLookupTable)
            {
                char* h = hPtr;
                char* hEnd = hPtr + hex.Length;
                byte* d = dPtr;

                while (h < hEnd)
                {
                    char c1 = *h++;
                    char c2 = *h++;

                    if ((uint)c1 >= 256 || (uint)c2 >= 256)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    sbyte hi = lPtr[c1];
                    sbyte lo = lPtr[c2];

                    if ((hi | lo) < 0)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    *d++ = (byte)(((byte)hi << 4) | (byte)lo);
                }
            }

            bytesWritten = requiredBytes;
            return true;
        }

        /// <summary>
        /// Attempts to decode an ASCII byte span containing hexadecimal characters directly into destination raw bytes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool TryDecode(ReadOnlySpan<byte> hexAscii, Span<byte> destination, out int bytesWritten)
        {
            if ((hexAscii.Length & 1) != 0)
            {
                bytesWritten = 0;
                return false;
            }

            int requiredBytes = hexAscii.Length / 2;
            if (destination.Length < requiredBytes)
            {
                bytesWritten = 0;
                return false;
            }

            if (hexAscii.IsEmpty)
            {
                bytesWritten = 0;
                return true;
            }

            fixed (byte* hPtr = hexAscii)
            fixed (byte* dPtr = destination)
            fixed (sbyte* lPtr = HexLookupTable)
            {
                byte* h = hPtr;
                byte* hEnd = hPtr + hexAscii.Length;
                byte* d = dPtr;

                while (h < hEnd)
                {
                    byte c1 = *h++;
                    byte c2 = *h++;

                    sbyte hi = lPtr[c1];
                    sbyte lo = lPtr[c2];

                    if ((hi | lo) < 0)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    *d++ = (byte)(((byte)hi << 4) | (byte)lo);
                }
            }

            bytesWritten = requiredBytes;
            return true;
        }

        /// <summary>
        /// Encodes a binary span to a new hex string with exactly one heap allocation.
        /// </summary>
        public static unsafe string ToString(ReadOnlySpan<byte> source, bool lowercase = false)
        {
            if (source.IsEmpty) return string.Empty;

            string result = new string('\0', source.Length * 2);
            fixed (char* p = result)
            {
                TryEncode(source, new Span<char>(p, result.Length), out _, lowercase);
            }
            return result;
        }

        /// <summary>
        /// Validates whether a character span represents a valid, even-length hexadecimal string.
        /// </summary>
        public static unsafe bool IsValid(ReadOnlySpan<char> hex)
        {
            if ((hex.Length & 1) != 0) return false;
            if (hex.IsEmpty) return true;

            fixed (char* hPtr = hex)
            fixed (sbyte* lPtr = HexLookupTable)
            {
                char* h = hPtr;
                char* end = hPtr + hex.Length;
                while (h < end)
                {
                    char c = *h++;
                    if ((uint)c >= 256 || lPtr[c] < 0)
                        return false;
                }
            }

            return true;
        }
    }
}
