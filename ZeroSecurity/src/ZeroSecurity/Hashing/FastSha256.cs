using System;
using ZeroSecurity.Common;

namespace ZeroSecurity.Hashing;

/// <summary>
/// Pure C#, zero-allocation implementation of the SHA-256 (FIPS 180-4) cryptographic hash algorithm.
/// Operates directly on ReadOnlySpan and outputs into Span with zero GC heap pressure.
/// Supports both one-shot and incremental streaming hashing.
/// </summary>
public static class FastSha256
{
    public const int HashSizeInBytes = 32;
    public const int BlockSizeInBytes = 64;

    private static readonly uint[] K =
    {
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
    };

    /// <summary>
    /// Computes the 32-byte SHA-256 hash of the input span and writes into the destination span.
    /// </summary>
    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        Sha256Incremental inc = default;
        inc.Init();
        inc.Update(source);
        inc.Final(destination);
    }

    /// <summary>
    /// Computes the SHA-256 hash of the input span and returns as a lowercase 64-character hex string.
    /// </summary>
    public static string HashHex(ReadOnlySpan<byte> source)
    {
        Span<byte> hash = stackalloc byte[HashSizeInBytes];
        Hash(source, hash);
        return FastHex.ToHex(hash);
    }

    /// <summary>
    /// Zero-allocation, stack-allocated incremental SHA-256 streaming state.
    /// </summary>
    public unsafe struct Sha256Incremental
    {
        private uint _h0, _h1, _h2, _h3, _h4, _h5, _h6, _h7;
        private ulong _totalBytes;
        private int _bufferLen;
        private fixed byte _buffer[64];

        public void Init()
        {
            _h0 = 0x6a09e667;
            _h1 = 0xbb67ae85;
            _h2 = 0x3c6ef372;
            _h3 = 0xa54ff53a;
            _h4 = 0x510e527f;
            _h5 = 0x9b05688c;
            _h6 = 0x1f83d9ab;
            _h7 = 0x5be0cd19;
            _totalBytes = 0;
            _bufferLen = 0;
        }

        public void Update(ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty) return;
            _totalBytes += (ulong)data.Length;

            int offset = 0;
            fixed (byte* bufPtr = _buffer)
            {
                if (_bufferLen > 0)
                {
                    int toCopy = Math.Min(64 - _bufferLen, data.Length);
                    for (int i = 0; i < toCopy; i++)
                    {
                        bufPtr[_bufferLen + i] = data[i];
                    }
                    _bufferLen += toCopy;
                    offset += toCopy;

                    if (_bufferLen == 64)
                    {
                        Span<uint> w = stackalloc uint[64];
                        ProcessBlock(new ReadOnlySpan<byte>(bufPtr, 64), w, ref _h0, ref _h1, ref _h2, ref _h3, ref _h4, ref _h5, ref _h6, ref _h7);
                        _bufferLen = 0;
                    }
                }

                Span<uint> wBlock = stackalloc uint[64];
                while (offset + 64 <= data.Length)
                {
                    ProcessBlock(data.Slice(offset, 64), wBlock, ref _h0, ref _h1, ref _h2, ref _h3, ref _h4, ref _h5, ref _h6, ref _h7);
                    offset += 64;
                }

                int rem = data.Length - offset;
                if (rem > 0)
                {
                    for (int i = 0; i < rem; i++)
                    {
                        bufPtr[i] = data[offset + i];
                    }
                    _bufferLen = rem;
                }
            }
        }

        public void Final(Span<byte> destination)
        {
            if (destination.Length < HashSizeInBytes)
                throw new ArgumentException("Destination span must be at least 32 bytes.", nameof(destination));

            fixed (byte* bufPtr = _buffer)
            {
                Span<byte> block = new Span<byte>(bufPtr, 64);
                Span<uint> w = stackalloc uint[64];

                block[_bufferLen] = 0x80;
                if (_bufferLen >= 56)
                {
                    block.Slice(_bufferLen + 1).Clear();
                    ProcessBlock(block, w, ref _h0, ref _h1, ref _h2, ref _h3, ref _h4, ref _h5, ref _h6, ref _h7);
                    block.Clear();
                }
                else
                {
                    block.Slice(_bufferLen + 1, 56 - (_bufferLen + 1)).Clear();
                }

                ulong totalBits = _totalBytes * 8UL;
                block[56] = (byte)(totalBits >> 56);
                block[57] = (byte)(totalBits >> 48);
                block[58] = (byte)(totalBits >> 40);
                block[59] = (byte)(totalBits >> 32);
                block[60] = (byte)(totalBits >> 24);
                block[61] = (byte)(totalBits >> 16);
                block[62] = (byte)(totalBits >> 8);
                block[63] = (byte)(totalBits);

                ProcessBlock(block, w, ref _h0, ref _h1, ref _h2, ref _h3, ref _h4, ref _h5, ref _h6, ref _h7);

                WriteBigEndian(_h0, destination.Slice(0, 4));
                WriteBigEndian(_h1, destination.Slice(4, 4));
                WriteBigEndian(_h2, destination.Slice(8, 4));
                WriteBigEndian(_h3, destination.Slice(12, 4));
                WriteBigEndian(_h4, destination.Slice(16, 4));
                WriteBigEndian(_h5, destination.Slice(20, 4));
                WriteBigEndian(_h6, destination.Slice(24, 4));
                WriteBigEndian(_h7, destination.Slice(28, 4));
            }
        }
    }

    internal static void ProcessBlock(ReadOnlySpan<byte> block, Span<uint> w,
        ref uint h0, ref uint h1, ref uint h2, ref uint h3,
        ref uint h4, ref uint h5, ref uint h6, ref uint h7)
    {
        for (int i = 0; i < 16; i++)
        {
            int idx = i * 4;
            w[i] = ((uint)block[idx] << 24) |
                   ((uint)block[idx + 1] << 16) |
                   ((uint)block[idx + 2] << 8) |
                   (uint)block[idx + 3];
        }

        for (int i = 16; i < 64; i++)
        {
            uint s0 = RotateRight(w[i - 15], 7) ^ RotateRight(w[i - 15], 18) ^ (w[i - 15] >> 3);
            uint s1 = RotateRight(w[i - 2], 17) ^ RotateRight(w[i - 2], 19) ^ (w[i - 2] >> 10);
            w[i] = w[i - 16] + s0 + w[i - 7] + s1;
        }

        uint a = h0, b = h1, c = h2, d = h3, e = h4, f = h5, g = h6, h = h7;

        for (int i = 0; i < 64; i++)
        {
            uint s1 = RotateRight(e, 6) ^ RotateRight(e, 11) ^ RotateRight(e, 25);
            uint ch = (e & f) ^ (~e & g);
            uint temp1 = h + s1 + ch + K[i] + w[i];
            uint s0 = RotateRight(a, 2) ^ RotateRight(a, 13) ^ RotateRight(a, 22);
            uint maj = (a & b) ^ (a & c) ^ (b & c);
            uint temp2 = s0 + maj;

            h = g;
            g = f;
            f = e;
            e = d + temp1;
            d = c;
            c = b;
            b = a;
            a = temp1 + temp2;
        }

        h0 += a;
        h1 += b;
        h2 += c;
        h3 += d;
        h4 += e;
        h5 += f;
        h6 += g;
        h7 += h;
    }

    private static uint RotateRight(uint value, int count) => (value >> count) | (value << (32 - count));

    private static void WriteBigEndian(uint value, Span<byte> dest)
    {
        dest[0] = (byte)(value >> 24);
        dest[1] = (byte)(value >> 16);
        dest[2] = (byte)(value >> 8);
        dest[3] = (byte)value;
    }
}
