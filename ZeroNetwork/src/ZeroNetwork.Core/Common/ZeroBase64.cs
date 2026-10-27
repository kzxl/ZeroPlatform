using System;

namespace ZeroNetwork.Common
{
    /// <summary>
    /// Ultra-high-performance, zero-allocation Base64 encoder and decoder adhering to RFC 4648.
    /// Replaces BCL <see cref="Convert.ToBase64String(byte[])"/> and <see cref="Convert.FromBase64String(string)"/>
    /// with direct Span-to-Span transformation on stack or pre-allocated buffers.
    /// </summary>
    public static class ZeroBase64
    {
        private const string EncodingTable = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

        private static readonly byte[] EncodingBytes = new byte[64]
        {
            (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F', (byte)'G', (byte)'H',
            (byte)'I', (byte)'J', (byte)'K', (byte)'L', (byte)'M', (byte)'N', (byte)'O', (byte)'P',
            (byte)'Q', (byte)'R', (byte)'S', (byte)'T', (byte)'U', (byte)'V', (byte)'W', (byte)'X',
            (byte)'Y', (byte)'Z', (byte)'a', (byte)'b', (byte)'c', (byte)'d', (byte)'e', (byte)'f',
            (byte)'g', (byte)'h', (byte)'i', (byte)'j', (byte)'k', (byte)'l', (byte)'m', (byte)'n',
            (byte)'o', (byte)'p', (byte)'q', (byte)'r', (byte)'s', (byte)'t', (byte)'u', (byte)'v',
            (byte)'w', (byte)'x', (byte)'y', (byte)'z', (byte)'0', (byte)'1', (byte)'2', (byte)'3',
            (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9', (byte)'+', (byte)'/'
        };

        private static readonly sbyte[] DecodingTable = new sbyte[256];

        static ZeroBase64()
        {
            for (int i = 0; i < DecodingTable.Length; i++)
            {
                DecodingTable[i] = -1;
            }

            for (int i = 0; i < EncodingBytes.Length; i++)
            {
                DecodingTable[EncodingBytes[i]] = (sbyte)i;
            }
        }

        /// <summary>
        /// Calculates the exact character or byte length required to Base64-encode a payload of specified byte length.
        /// </summary>
        public static int GetEncodedLength(int sourceLength)
        {
            if (sourceLength < 0) throw new ArgumentOutOfRangeException(nameof(sourceLength));
            if (sourceLength == 0) return 0;
            return ((sourceLength + 2) / 3) * 4;
        }

        /// <summary>
        /// Calculates the maximum decoded byte length from a Base64-encoded character length.
        /// </summary>
        public static int GetMaxDecodedLength(int sourceLength)
        {
            if (sourceLength < 0) throw new ArgumentOutOfRangeException(nameof(sourceLength));
            if (sourceLength == 0) return 0;
            return (sourceLength * 3) / 4;
        }

        /// <summary>
        /// Encodes binary data into Base64 characters without heap allocation.
        /// </summary>
        /// <param name="source">The input binary data.</param>
        /// <param name="destination">The destination character buffer (must be at least <see cref="GetEncodedLength"/> characters).</param>
        /// <returns>The number of characters written to the destination.</returns>
        public static unsafe int Encode(ReadOnlySpan<byte> source, Span<char> destination)
        {
            int requiredLength = GetEncodedLength(source.Length);
            if (destination.Length < requiredLength)
                throw new ArgumentException("Destination span is too short for Base64 encoding.", nameof(destination));

            if (source.IsEmpty) return 0;

            fixed (byte* pSrc = source)
            fixed (char* pDst = destination)
            fixed (char* pTable = EncodingTable)
            {
                byte* src = pSrc;
                char* dst = pDst;
                int fullTriples = source.Length / 3;

                for (int i = 0; i < fullTriples; i++)
                {
                    uint b0 = *src++;
                    uint b1 = *src++;
                    uint b2 = *src++;

                    uint triple = (b0 << 16) | (b1 << 8) | b2;

                    *dst++ = pTable[(triple >> 18) & 0x3F];
                    *dst++ = pTable[(triple >> 12) & 0x3F];
                    *dst++ = pTable[(triple >> 6) & 0x3F];
                    *dst++ = pTable[triple & 0x3F];
                }

                int remainder = source.Length % 3;
                if (remainder == 1)
                {
                    uint b0 = *src;
                    uint triple = b0 << 16;

                    *dst++ = pTable[(triple >> 18) & 0x3F];
                    *dst++ = pTable[(triple >> 12) & 0x3F];
                    *dst++ = '=';
                    *dst++ = '=';
                }
                else if (remainder == 2)
                {
                    uint b0 = *src++;
                    uint b1 = *src;
                    uint triple = (b0 << 16) | (b1 << 8);

                    *dst++ = pTable[(triple >> 18) & 0x3F];
                    *dst++ = pTable[(triple >> 12) & 0x3F];
                    *dst++ = pTable[(triple >> 6) & 0x3F];
                    *dst++ = '=';
                }

                return (int)(dst - pDst);
            }
        }

        /// <summary>
        /// Encodes binary data into UTF-8 Base64 bytes directly without intermediate char or string allocation.
        /// </summary>
        public static int EncodeToUtf8(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            int requiredLength = GetEncodedLength(source.Length);
            if (destination.Length < requiredLength)
                throw new ArgumentException("Destination span is too short for Base64 encoding.", nameof(destination));

            if (source.IsEmpty) return 0;

            int srcIdx = 0;
            int dstIdx = 0;
            int fullTriples = source.Length / 3;

            for (int i = 0; i < fullTriples; i++)
            {
                uint b0 = source[srcIdx++];
                uint b1 = source[srcIdx++];
                uint b2 = source[srcIdx++];

                uint triple = (b0 << 16) | (b1 << 8) | b2;

                destination[dstIdx++] = EncodingBytes[(triple >> 18) & 0x3F];
                destination[dstIdx++] = EncodingBytes[(triple >> 12) & 0x3F];
                destination[dstIdx++] = EncodingBytes[(triple >> 6) & 0x3F];
                destination[dstIdx++] = EncodingBytes[triple & 0x3F];
            }

            int remainder = source.Length - srcIdx;
            if (remainder == 1)
            {
                uint b0 = source[srcIdx];
                uint triple = b0 << 16;

                destination[dstIdx++] = EncodingBytes[(triple >> 18) & 0x3F];
                destination[dstIdx++] = EncodingBytes[(triple >> 12) & 0x3F];
                destination[dstIdx++] = (byte)'=';
                destination[dstIdx++] = (byte)'=';
            }
            else if (remainder == 2)
            {
                uint b0 = source[srcIdx++];
                uint b1 = source[srcIdx];
                uint triple = (b0 << 16) | (b1 << 8);

                destination[dstIdx++] = EncodingBytes[(triple >> 18) & 0x3F];
                destination[dstIdx++] = EncodingBytes[(triple >> 12) & 0x3F];
                destination[dstIdx++] = EncodingBytes[(triple >> 6) & 0x3F];
                destination[dstIdx++] = (byte)'=';
            }

            return dstIdx;
        }

        /// <summary>
        /// Decodes Base64 characters into binary bytes without heap allocation.
        /// </summary>
        /// <param name="source">The Base64 character span.</param>
        /// <param name="destination">The destination byte span.</param>
        /// <returns>The number of decoded bytes written.</returns>
        public static int Decode(ReadOnlySpan<char> source, Span<byte> destination)
        {
            if (source.IsEmpty) return 0;
            if ((source.Length & 3) != 0)
                throw new FormatException("Invalid Base64 sequence length (must be a multiple of 4).");

            int padding = 0;
            if (source.Length >= 2)
            {
                if (source[source.Length - 1] == '=') padding++;
                if (source[source.Length - 2] == '=') padding++;
            }

            int requiredDecoded = (source.Length * 3) / 4 - padding;
            if (destination.Length < requiredDecoded)
                throw new ArgumentException("Destination span is too small for Base64 decoding.", nameof(destination));

            int srcIdx = 0;
            int dstIdx = 0;
            int blocks = source.Length / 4;

            for (int i = 0; i < blocks - (padding > 0 ? 1 : 0); i++)
            {
                char c0 = source[srcIdx++];
                char c1 = source[srcIdx++];
                char c2 = source[srcIdx++];
                char c3 = source[srcIdx++];

                if (c0 > 255 || c1 > 255 || c2 > 255 || c3 > 255)
                    throw new FormatException("Invalid Base64 character.");

                int v0 = DecodingTable[c0];
                int v1 = DecodingTable[c1];
                int v2 = DecodingTable[c2];
                int v3 = DecodingTable[c3];

                if ((v0 | v1 | v2 | v3) < 0)
                    throw new FormatException("Invalid Base64 character.");

                uint triple = ((uint)v0 << 18) | ((uint)v1 << 12) | ((uint)v2 << 6) | (uint)v3;

                destination[dstIdx++] = (byte)(triple >> 16);
                destination[dstIdx++] = (byte)(triple >> 8);
                destination[dstIdx++] = (byte)triple;
            }

            if (padding > 0)
            {
                char c0 = source[srcIdx++];
                char c1 = source[srcIdx++];
                char c2 = source[srcIdx++];
                char c3 = source[srcIdx++];

                if (c0 > 255 || c1 > 255)
                    throw new FormatException("Invalid Base64 character in final block.");

                int v0 = DecodingTable[c0];
                int v1 = DecodingTable[c1];
                if ((v0 | v1) < 0)
                    throw new FormatException("Invalid Base64 character in final block.");

                if (padding == 1)
                {
                    if (c2 > 255) throw new FormatException("Invalid Base64 character in final block.");
                    int v2 = DecodingTable[c2];
                    if (v2 < 0 || c3 != '=')
                        throw new FormatException("Invalid Base64 padding.");

                    uint triple = ((uint)v0 << 18) | ((uint)v1 << 12) | ((uint)v2 << 6);
                    destination[dstIdx++] = (byte)(triple >> 16);
                    destination[dstIdx++] = (byte)(triple >> 8);
                }
                else if (padding == 2)
                {
                    if (c2 != '=' || c3 != '=')
                        throw new FormatException("Invalid Base64 padding.");

                    uint triple = ((uint)v0 << 18) | ((uint)v1 << 12);
                    destination[dstIdx++] = (byte)(triple >> 16);
                }
            }

            return dstIdx;
        }

        /// <summary>
        /// Convenience method returning a newly allocated string from Base64 binary encoding.
        /// </summary>
        public static string ToBase64String(ReadOnlySpan<byte> source)
        {
            if (source.IsEmpty) return string.Empty;
            int len = GetEncodedLength(source.Length);
#if NET8_0_OR_GREATER
            return string.Create(len, source.ToArray(), (chars, state) =>
            {
                Encode(state, chars);
            });
#else
            char[] buffer = new char[len];
            Encode(source, buffer);
            return new string(buffer);
#endif
        }
    }
}
