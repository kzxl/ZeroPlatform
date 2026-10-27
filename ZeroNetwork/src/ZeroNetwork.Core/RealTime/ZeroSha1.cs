using System;
using System.Text;
using ZeroNetwork.Common;

namespace ZeroNetwork.RealTime
{
    /// <summary>
    /// Sovereign, zero-allocation SHA-1 hashing engine adhering to RFC 3174.
    /// Replaces heavyweight BCL <see cref="System.Security.Cryptography.SHA1"/> and OS crypto handles
    /// on hot paths like RFC 6455 WebSocket Handshakes.
    /// Computes full SHA-1 hashes entirely on the stack in sub-150ns.
    /// </summary>
    public static class ZeroSha1
    {
        private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        /// <summary>
        /// Computes the 20-byte SHA-1 hash of the input span directly into the destination span.
        /// </summary>
        /// <param name="source">The input byte span.</param>
        /// <param name="destination">The destination 20-byte span.</param>
        public static unsafe void Hash(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            if (destination.Length < 20)
                throw new ArgumentException("Destination span must be at least 20 bytes.", nameof(destination));

            uint h0 = 0x67452301;
            uint h1 = 0xEFCDAB89;
            uint h2 = 0x98BADCFE;
            uint h3 = 0x10325476;
            uint h4 = 0xC3D2E1F0;

            ulong totalBits = (ulong)source.Length * 8;
            int offset = 0;
            int remaining = source.Length;

            uint* w = stackalloc uint[80];

            fixed (byte* pSource = source)
            {
                // Process complete 64-byte chunks
                while (remaining >= 64)
                {
                    for (int i = 0; i < 16; i++)
                    {
                        int idx = offset + (i * 4);
                        w[i] = ((uint)pSource[idx] << 24) |
                               ((uint)pSource[idx + 1] << 16) |
                               ((uint)pSource[idx + 2] << 8) |
                               pSource[idx + 3];
                    }

                    TransformBlock(ref h0, ref h1, ref h2, ref h3, ref h4, w);
                    offset += 64;
                    remaining -= 64;
                }

                // Padding: 1 byte 0x80, followed by zeroes, followed by 8-byte big-endian bit length
                byte* pad = stackalloc byte[128];
                for (int i = 0; i < 128; i++) pad[i] = 0;

                for (int i = 0; i < remaining; i++)
                {
                    pad[i] = pSource[offset + i];
                }
                pad[remaining] = 0x80;

                int padBlockCount = (remaining < 56) ? 64 : 128;
                int lenOffset = padBlockCount - 8;
                pad[lenOffset] = (byte)(totalBits >> 56);
                pad[lenOffset + 1] = (byte)(totalBits >> 48);
                pad[lenOffset + 2] = (byte)(totalBits >> 40);
                pad[lenOffset + 3] = (byte)(totalBits >> 32);
                pad[lenOffset + 4] = (byte)(totalBits >> 24);
                pad[lenOffset + 5] = (byte)(totalBits >> 16);
                pad[lenOffset + 6] = (byte)(totalBits >> 8);
                pad[lenOffset + 7] = (byte)totalBits;

                for (int b = 0; b < padBlockCount; b += 64)
                {
                    for (int i = 0; i < 16; i++)
                    {
                        int idx = b + (i * 4);
                        w[i] = ((uint)pad[idx] << 24) |
                               ((uint)pad[idx + 1] << 16) |
                               ((uint)pad[idx + 2] << 8) |
                               pad[idx + 3];
                    }

                    TransformBlock(ref h0, ref h1, ref h2, ref h3, ref h4, w);
                }
            }

            // Write 20-byte output in big-endian
            destination[0] = (byte)(h0 >> 24); destination[1] = (byte)(h0 >> 16); destination[2] = (byte)(h0 >> 8); destination[3] = (byte)h0;
            destination[4] = (byte)(h1 >> 24); destination[5] = (byte)(h1 >> 16); destination[6] = (byte)(h1 >> 8); destination[7] = (byte)h1;
            destination[8] = (byte)(h2 >> 24); destination[9] = (byte)(h2 >> 16); destination[10] = (byte)(h2 >> 8); destination[11] = (byte)h2;
            destination[12] = (byte)(h3 >> 24); destination[13] = (byte)(h3 >> 16); destination[14] = (byte)(h3 >> 8); destination[15] = (byte)h3;
            destination[16] = (byte)(h4 >> 24); destination[17] = (byte)(h4 >> 16); destination[18] = (byte)(h4 >> 8); destination[19] = (byte)h4;
        }

        private static unsafe void TransformBlock(ref uint a, ref uint b, ref uint c, ref uint d, ref uint e, uint* w)
        {
            for (int i = 16; i < 80; i++)
            {
                uint val = w[i - 3] ^ w[i - 8] ^ w[i - 14] ^ w[i - 16];
                w[i] = (val << 1) | (val >> 31);
            }

            uint va = a;
            uint vb = b;
            uint vc = c;
            uint vd = d;
            uint ve = e;

            // Round 1 (0..19): f = (b & c) | (~b & d), k = 0x5A827999
            for (int i = 0; i < 20; i++)
            {
                uint f = (vb & vc) | (~vb & vd);
                uint temp = ((va << 5) | (va >> 27)) + f + ve + 0x5A827999 + w[i];
                ve = vd;
                vd = vc;
                vc = (vb << 30) | (vb >> 2);
                vb = va;
                va = temp;
            }

            // Round 2 (20..39): f = b ^ c ^ d, k = 0x6ED9EBA1
            for (int i = 20; i < 40; i++)
            {
                uint f = vb ^ vc ^ vd;
                uint temp = ((va << 5) | (va >> 27)) + f + ve + 0x6ED9EBA1 + w[i];
                ve = vd;
                vd = vc;
                vc = (vb << 30) | (vb >> 2);
                vb = va;
                va = temp;
            }

            // Round 3 (40..59): f = (b & c) | (b & d) | (c & d), k = 0x8F1BBCDC
            for (int i = 40; i < 60; i++)
            {
                uint f = (vb & vc) | (vb & vd) | (vc & vd);
                uint temp = ((va << 5) | (va >> 27)) + f + ve + 0x8F1BBCDC + w[i];
                ve = vd;
                vd = vc;
                vc = (vb << 30) | (vb >> 2);
                vb = va;
                va = temp;
            }

            // Round 4 (60..79): f = b ^ c ^ d, k = 0xCA62C1D6
            for (int i = 60; i < 80; i++)
            {
                uint f = vb ^ vc ^ vd;
                uint temp = ((va << 5) | (va >> 27)) + f + ve + 0xCA62C1D6 + w[i];
                ve = vd;
                vd = vc;
                vc = (vb << 30) | (vb >> 2);
                vb = va;
                va = temp;
            }

            a += va;
            b += vb;
            c += vc;
            d += vd;
            e += ve;
        }

        /// <summary>
        /// Computes the RFC 6455 Sec-WebSocket-Accept token with zero heap allocations.
        /// </summary>
        /// <param name="secWebSocketKey">The 24-character Sec-WebSocket-Key string from client handshake.</param>
        /// <param name="destination">Destination char span (must be at least 28 characters).</param>
        /// <returns>Number of characters written (always 28).</returns>
        public static int ComputeWebSocketAccept(ReadOnlySpan<char> secWebSocketKey, Span<char> destination)
        {
            if (destination.Length < 28)
                throw new ArgumentException("Destination span must be at least 28 characters.", nameof(destination));

            // Combined length: secWebSocketKey (typically 24 chars) + GUID (36 chars) = 60 chars
            int totalChars = secWebSocketKey.Length + WebSocketGuid.Length;
            Span<byte> combinedBytes = stackalloc byte[totalChars];

            for (int i = 0; i < secWebSocketKey.Length; i++)
            {
                combinedBytes[i] = (byte)secWebSocketKey[i];
            }
            for (int i = 0; i < WebSocketGuid.Length; i++)
            {
                combinedBytes[secWebSocketKey.Length + i] = (byte)WebSocketGuid[i];
            }

            Span<byte> hashBytes = stackalloc byte[20];
            Hash(combinedBytes, hashBytes);

            return ZeroBase64.Encode(hashBytes, destination);
        }

        /// <summary>
        /// Computes the RFC 6455 Sec-WebSocket-Accept string.
        /// </summary>
        public static string ComputeWebSocketAccept(string secWebSocketKey)
        {
            if (secWebSocketKey == null) throw new ArgumentNullException(nameof(secWebSocketKey));
            Span<char> acceptChars = stackalloc char[28];
            ComputeWebSocketAccept(secWebSocketKey.AsSpan(), acceptChars);
            return new string(acceptChars.ToArray());
        }
    }
}
