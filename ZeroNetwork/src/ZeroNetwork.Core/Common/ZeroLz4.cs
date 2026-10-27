using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace ZeroNetwork.Common
{
    /// <summary>
    /// Ultra-high-speed, zero-allocation pure C# LZ4 block and stream codec for real-time WebSocket, IPC, and Pub/Sub payloads.
    /// Delivers gigabytes-per-second throughput with zero native DLL dependencies.
    /// Operates on spans and memory streams across .NET 8.0, .NET Standard 2.0, and .NET Framework 4.6.2.
    /// </summary>
    public static class ZeroLz4
    {
        private const int MinMatch = 4;
        private const int HashLog = 12; // 4096 entries (16KB / 32KB pointer array) - fits snugly inside CPU L1 Data Cache
        private const int HashSize = 1 << HashLog;
        private const uint Prime4Bytes = 2654435761U;

        /// <summary>
        /// Calculates the maximum possible compressed size for a given input length.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MaximumOutputLength(int inputLength)
        {
            if (inputLength < 0) throw new ArgumentOutOfRangeException(nameof(inputLength));
            return inputLength + (inputLength / 255) + 16;
        }

        /// <summary>
        /// Compresses input bytes into the destination span using the standard LZ4 block format.
        /// </summary>
        /// <param name="source">Source uncompressed bytes.</param>
        /// <param name="destination">Destination span (must have capacity of at least <see cref="MaximumOutputLength"/>).</param>
        /// <returns>The number of bytes written to the destination span.</returns>
        public static unsafe int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            int srcLen = source.Length;
            if (srcLen == 0) return 0;

            int maxDest = MaximumOutputLength(srcLen);
            if (destination.Length < maxDest)
                throw new ArgumentException($"Destination buffer too small. Must be at least {maxDest} bytes.", nameof(destination));

            if (srcLen < 13) // LZ4 minimum sequence length constraint
            {
                return EmitLastLiterals(source, destination, 0, 0, srcLen);
            }

            // Stack-allocated hash table storing direct pointers (4096 * IntPtr.Size)
            byte** hashTable = stackalloc byte*[HashSize];
            new Span<byte>((void*)hashTable, HashSize * sizeof(byte*)).Clear();

            fixed (byte* srcPtr = source)
            fixed (byte* dstPtr = destination)
            {
                byte* src = srcPtr;
                byte* srcEnd = srcPtr + srcLen;
                byte* srcLimit = srcEnd - 12; // Must leave at least 5 literals + 5 match + 2 offset
                byte* anchor = src;

                byte* dst = dstPtr;
                byte* dstEnd = dstPtr + destination.Length;

                // Prime forward hash
                uint forwardH = (*(uint*)src * Prime4Bytes) >> (32 - HashLog);

                while (src < srcLimit)
                {
                    byte* match;
                    byte* forwardSrc = src;
                    int step = 1;
                    int searchMatchNb = 1 << 6;

                    // Match finder loop with dynamic step skipping
                    do
                    {
                        src = forwardSrc;
                        forwardSrc += step;
                        step = (searchMatchNb++) >> 6;

                        if (forwardSrc > srcLimit)
                        {
                            goto _last_literals;
                        }

                        uint sequence = *(uint*)src;
                        uint hash = forwardH;
                        forwardH = (*(uint*)forwardSrc * Prime4Bytes) >> (32 - HashLog);

                        match = hashTable[hash];
                        hashTable[hash] = src;

                        if (match == null || match == src || (src - match) > 65535)
                            continue;

                        if (*(uint*)match == sequence)
                        {
                            break;
                        }
                    }
                    while (true);

                    // Match found!
                    int litLen = (int)(src - anchor);
                    int matchOffset = (int)(src - match);

                    // 1. Determine match length using hardware-accelerated 64-bit comparisons
                    byte* srcMatchStart = src;
                    src += MinMatch;
                    match += MinMatch;

                    while (src <= srcEnd - 8)
                    {
                        ulong diff = *(ulong*)src ^ *(ulong*)match;
                        if (diff != 0)
                        {
                            int matched = ZeroBitOps.TrailingZeroCount(diff) >> 3;
                            src += matched;
                            match += matched;
                            break;
                        }
                        src += 8;
                        match += 8;
                    }

                    while (src < srcEnd - 5 && *src == *match)
                    {
                        src++;
                        match++;
                    }

                    int matchLen = (int)(src - srcMatchStart);

                    // 2. Encode token
                    int tokenLit = litLen >= 15 ? 15 : litLen;
                    int tokenMatch = (matchLen - MinMatch) >= 15 ? 15 : (matchLen - MinMatch);
                    *dst++ = (byte)((tokenLit << 4) | tokenMatch);

                    // 3. Emit extra literal length if >= 15
                    if (litLen >= 15)
                    {
                        int extra = litLen - 15;
                        while (extra >= 255)
                        {
                            *dst++ = 255;
                            extra -= 255;
                        }
                        *dst++ = (byte)extra;
                    }

                    // 4. Emit literal bytes
                    if (litLen > 0)
                    {
                        UnsafeCopy(anchor, dst, litLen);
                        dst += litLen;
                    }

                    // 5. Emit match offset (UInt16 Little Endian)
                    *(ushort*)dst = (ushort)matchOffset;
                    dst += 2;

                    // 6. Emit extra match length if >= 15
                    if ((matchLen - MinMatch) >= 15)
                    {
                        int extra = matchLen - MinMatch - 15;
                        while (extra >= 255)
                        {
                            *dst++ = 255;
                            extra -= 255;
                        }
                        *dst++ = (byte)extra;
                    }

                    anchor = src;

                    if (src >= srcLimit)
                        break;

                    // Update hash for current anchor
                    forwardH = (*(uint*)src * Prime4Bytes) >> (32 - HashLog);
                }

            _last_literals:
                // Emit trailing literals
                int finalLitLen = (int)(srcEnd - anchor);
                if (finalLitLen > 0)
                {
                    int tokenLit = finalLitLen >= 15 ? 15 : finalLitLen;
                    *dst++ = (byte)(tokenLit << 4);

                    if (finalLitLen >= 15)
                    {
                        int extra = finalLitLen - 15;
                        while (extra >= 255)
                        {
                            *dst++ = 255;
                            extra -= 255;
                        }
                        *dst++ = (byte)extra;
                    }

                    UnsafeCopy(anchor, dst, finalLitLen);
                    dst += finalLitLen;
                }

                return (int)(dst - dstPtr);
            }
        }

        /// <summary>
        /// Decompresses an LZ4 block into the destination span.
        /// </summary>
        /// <param name="source">Compressed LZ4 block.</param>
        /// <param name="destination">Destination buffer where uncompressed bytes are written.</param>
        /// <returns>The number of decompressed bytes written.</returns>
        public static unsafe int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            if (source.IsEmpty) return 0;

            fixed (byte* srcPtr = source)
            fixed (byte* dstPtr = destination)
            {
                byte* src = srcPtr;
                byte* srcEnd = srcPtr + source.Length;

                byte* dst = dstPtr;
                byte* dstEnd = dstPtr + destination.Length;

                while (src < srcEnd)
                {
                    byte token = *src++;
                    int litLen = token >> 4;

                    if (litLen == 15)
                    {
                        byte b;
                        do
                        {
                            if (src >= srcEnd) throw new InvalidDataException("Truncated LZ4 stream in literal length.");
                            b = *src++;
                            litLen += b;
                        } while (b == 255);
                    }

                    // Copy literals
                    if (litLen > 0)
                    {
                        if (src + litLen > srcEnd || dst + litLen > dstEnd)
                            throw new InvalidDataException("LZ4 literal length exceeds bounds.");

                        UnsafeCopy(src, dst, litLen);
                        src += litLen;
                        dst += litLen;
                    }

                    if (src >= srcEnd)
                    {
                        break; // End of block with trailing literals
                    }

                    // Read match offset
                    if (src + 2 > srcEnd) throw new InvalidDataException("Truncated LZ4 match offset.");
                    ushort offset = *(ushort*)src;
                    src += 2;

                    if (offset == 0) throw new InvalidDataException("Invalid LZ4 match offset 0.");

                    byte* match = dst - offset;
                    if (match < dstPtr) throw new InvalidDataException("LZ4 match offset references memory before start of buffer.");

                    int matchLen = (token & 0x0F) + MinMatch;
                    if ((token & 0x0F) == 15)
                    {
                        byte b;
                        do
                        {
                            if (src >= srcEnd) throw new InvalidDataException("Truncated LZ4 stream in match length.");
                            b = *src++;
                            matchLen += b;
                        } while (b == 255);
                    }

                    if (dst + matchLen > dstEnd)
                        throw new InvalidDataException("LZ4 match exceeds destination capacity.");

                    // Copy match: if offset >= matchLen, memory does not self-overlap -> fast block copy
                    if (offset >= matchLen)
                    {
                        UnsafeCopy(match, dst, matchLen);
                        dst += matchLen;
                    }
                    else if (offset == 1)
                    {
                        // Byte-level RLE run (e.g. repeated 0x00)
                        byte val = *match;
                        while (matchLen >= 8)
                        {
                            *(ulong*)dst = (ulong)val * 0x0101010101010101UL;
                            dst += 8;
                            matchLen -= 8;
                        }
                        while (matchLen > 0)
                        {
                            *dst++ = val;
                            matchLen--;
                        }
                    }
                    else
                    {
                        // Self-overlapping cyclic copy
                        for (int i = 0; i < matchLen; i++)
                        {
                            *dst++ = *match++;
                        }
                    }
                }

                return (int)(dst - dstPtr);
            }
        }

        /// <summary>
        /// Compresses a payload prefixing it with a 4-byte uncompressed length header for self-contained framing.
        /// </summary>
        public static byte[] CompressFramed(ReadOnlySpan<byte> source)
        {
            if (source.IsEmpty) return Array.Empty<byte>();

            int maxLen = 4 + MaximumOutputLength(source.Length);
            byte[] output = new byte[maxLen];

            // Write 4-byte original length
            int uncompressedLen = source.Length;
            output[0] = (byte)uncompressedLen;
            output[1] = (byte)(uncompressedLen >> 8);
            output[2] = (byte)(uncompressedLen >> 16);
            output[3] = (byte)(uncompressedLen >> 24);

            int compressed = Compress(source, output.AsSpan(4));
            byte[] trimmed = new byte[4 + compressed];
            Array.Copy(output, 0, trimmed, 0, 4 + compressed);
            return trimmed;
        }

        /// <summary>
        /// Decompresses a self-contained framed payload created by <see cref="CompressFramed"/>.
        /// </summary>
        public static byte[] DecompressFramed(ReadOnlySpan<byte> source)
        {
            if (source.Length < 4) return Array.Empty<byte>();

            int uncompressedLen = source[0] | (source[1] << 8) | (source[2] << 16) | (source[3] << 24);
            if (uncompressedLen < 0 || uncompressedLen > 1024 * 1024 * 64) // 64MB sanity cap
                throw new InvalidDataException("Invalid uncompressed frame length.");

            byte[] result = new byte[uncompressedLen];
            int read = Decompress(source.Slice(4), result);
            if (read != uncompressedLen)
                throw new InvalidDataException($"Decompressed size mismatch. Expected {uncompressedLen}, got {read}.");

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void UnsafeCopy(byte* src, byte* dst, int length)
        {
            while (length >= 8)
            {
                *(ulong*)dst = *(ulong*)src;
                dst += 8;
                src += 8;
                length -= 8;
            }
            if (length >= 4)
            {
                *(uint*)dst = *(uint*)src;
                dst += 4;
                src += 4;
                length -= 4;
            }
            while (length > 0)
            {
                *dst++ = *src++;
                length--;
            }
        }

        private static unsafe int EmitLastLiterals(ReadOnlySpan<byte> source, Span<byte> destination, int srcOffset, int dstOffset, int count)
        {
            fixed (byte* srcPtr = source)
            fixed (byte* dstPtr = destination)
            {
                byte* src = srcPtr + srcOffset;
                byte* dst = dstPtr + dstOffset;

                int tokenLit = count >= 15 ? 15 : count;
                *dst++ = (byte)(tokenLit << 4);

                if (count >= 15)
                {
                    int extra = count - 15;
                    while (extra >= 255)
                    {
                        *dst++ = 255;
                        extra -= 255;
                    }
                    *dst++ = (byte)extra;
                }

                UnsafeCopy(src, dst, count);
                dst += count;
                return (int)(dst - dstPtr);
            }
        }
    }
}
