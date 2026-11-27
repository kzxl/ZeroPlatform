using System;
using System.Runtime.CompilerServices;
using ZeroPrimitives.Buffers;
using ZeroPrimitives.Numerics;

namespace ZeroPrimitives.Cryptography
{
    /// <summary>
    /// Deterministic, cross-process 64-bit xxHash64 (non-cryptographic) for idempotency keys, row fingerprinting, and change detection.
    /// Zero heap allocation; produces identical hashes across .NET Framework 4.6.2, .NET Standard 2.0, and .NET 8.0+.
    /// </summary>
    public static class FastXxHash64
    {
        internal const ulong Prime64_1 = 0x9E3779B185EBCA87UL;
        internal const ulong Prime64_2 = 0xC2B2AE3D27D4EB4FUL;
        internal const ulong Prime64_3 = 0x165667B19E3779F9UL;
        internal const ulong Prime64_4 = 0x85EBCA77C2B2AE63UL;
        internal const ulong Prime64_5 = 0x27D4EB2F165667C5UL;

        public static unsafe ulong Hash(ReadOnlySpan<byte> data, ulong seed = 0)
        {
            int len = data.Length;
            ulong h64;

            fixed (byte* basePtr = data)
            {
                byte* ptr = basePtr;
                byte* end = basePtr + len;

                if (len >= 32)
                {
                    ulong v1 = seed + Prime64_1 + Prime64_2;
                    ulong v2 = seed + Prime64_2;
                    ulong v3 = seed;
                    ulong v4 = seed - Prime64_1;

                    byte* limit = end - 32;
                    do
                    {
                        v1 = Round(v1, Unsafe.ReadUnaligned<ulong>(ptr));
                        v2 = Round(v2, Unsafe.ReadUnaligned<ulong>(ptr + 8));
                        v3 = Round(v3, Unsafe.ReadUnaligned<ulong>(ptr + 16));
                        v4 = Round(v4, Unsafe.ReadUnaligned<ulong>(ptr + 24));
                        ptr += 32;
                    } while (ptr <= limit);

                    h64 = RotateLeft64(v1, 1) +
                          RotateLeft64(v2, 7) +
                          RotateLeft64(v3, 12) +
                          RotateLeft64(v4, 18);

                    h64 = MergeRound(h64, v1);
                    h64 = MergeRound(h64, v2);
                    h64 = MergeRound(h64, v3);
                    h64 = MergeRound(h64, v4);
                }
                else
                {
                    h64 = seed + Prime64_5;
                }

                h64 += (ulong)len;

                while (ptr + 8 <= end)
                {
                    ulong k1 = Round(0, Unsafe.ReadUnaligned<ulong>(ptr));
                    h64 ^= k1;
                    h64 = (RotateLeft64(h64, 27) * Prime64_1) + Prime64_4;
                    ptr += 8;
                }

                if (ptr + 4 <= end)
                {
                    h64 ^= Unsafe.ReadUnaligned<uint>(ptr) * Prime64_1;
                    h64 = (RotateLeft64(h64, 23) * Prime64_2) + Prime64_3;
                    ptr += 4;
                }

                while (ptr < end)
                {
                    h64 ^= (*ptr++) * Prime64_5;
                    h64 = RotateLeft64(h64, 11) * Prime64_1;
                }
            }

            return Avalanche(h64);
        }

        public static unsafe ulong Hash(ReadOnlySpan<char> chars, ulong seed = 0)
        {
            if (chars.IsEmpty) return Hash(ReadOnlySpan<byte>.Empty, seed);
            fixed (char* p = chars)
            {
                return Hash(new ReadOnlySpan<byte>(p, checked(chars.Length * sizeof(char))), seed);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong RotateLeft64(ulong value, int offset)
            => (value << offset) | (value >> (64 - offset));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong Round(ulong acc, ulong input)
        {
            acc += input * Prime64_2;
            acc = RotateLeft64(acc, 31);
            acc *= Prime64_1;
            return acc;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong MergeRound(ulong acc, ulong val)
        {
            val = Round(0, val);
            acc ^= val;
            acc = (acc * Prime64_1) + Prime64_4;
            return acc;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong Avalanche(ulong h64)
        {
            h64 ^= h64 >> 33;
            h64 *= Prime64_2;
            h64 ^= h64 >> 29;
            h64 *= Prime64_3;
            h64 ^= h64 >> 32;
            return h64;
        }
    }

    /// <summary>
    /// Incremental streaming xxHash64 state for multi-field ERP row fingerprinting and idempotency hashing without intermediate buffer allocation.
    /// </summary>
    public unsafe struct XxHash64State
    {
        private readonly ulong _seed;
        private ulong _v1;
        private ulong _v2;
        private ulong _v3;
        private ulong _v4;
        private ulong _totalLen;
        private int _bufLen;
        private fixed byte _buffer[32];

        public XxHash64State(ulong seed = 0)
        {
            _seed = seed;
            _v1 = seed + FastXxHash64.Prime64_1 + FastXxHash64.Prime64_2;
            _v2 = seed + FastXxHash64.Prime64_2;
            _v3 = seed;
            _v4 = seed - FastXxHash64.Prime64_1;
            _totalLen = 0;
            _bufLen = 0;
        }

        public void Append(ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty) return;

            _totalLen += (ulong)data.Length;

            fixed (byte* srcBase = data)
            fixed (byte* bufPtr = _buffer)
            {
                byte* src = srcBase;
                byte* srcEnd = srcBase + data.Length;

                if (_bufLen + data.Length < 32)
                {
                    Buffer.MemoryCopy(src, bufPtr + _bufLen, 32 - _bufLen, data.Length);
                    _bufLen += data.Length;
                    return;
                }

                if (_bufLen > 0)
                {
                    int fill = 32 - _bufLen;
                    Buffer.MemoryCopy(src, bufPtr + _bufLen, fill, fill);
                    _v1 = FastXxHash64.Round(_v1, Unsafe.ReadUnaligned<ulong>(bufPtr));
                    _v2 = FastXxHash64.Round(_v2, Unsafe.ReadUnaligned<ulong>(bufPtr + 8));
                    _v3 = FastXxHash64.Round(_v3, Unsafe.ReadUnaligned<ulong>(bufPtr + 16));
                    _v4 = FastXxHash64.Round(_v4, Unsafe.ReadUnaligned<ulong>(bufPtr + 24));
                    src += fill;
                    _bufLen = 0;
                }

                byte* limit = srcEnd - 32;
                while (src <= limit)
                {
                    _v1 = FastXxHash64.Round(_v1, Unsafe.ReadUnaligned<ulong>(src));
                    _v2 = FastXxHash64.Round(_v2, Unsafe.ReadUnaligned<ulong>(src + 8));
                    _v3 = FastXxHash64.Round(_v3, Unsafe.ReadUnaligned<ulong>(src + 16));
                    _v4 = FastXxHash64.Round(_v4, Unsafe.ReadUnaligned<ulong>(src + 24));
                    src += 32;
                }

                int remaining = (int)(srcEnd - src);
                if (remaining > 0)
                {
                    Buffer.MemoryCopy(src, bufPtr, 32, remaining);
                    _bufLen = remaining;
                }
            }
        }

        public void Append(ReadOnlySpan<char> chars)
        {
            if (chars.IsEmpty) return;
            fixed (char* p = chars)
            {
                Append(new ReadOnlySpan<byte>(p, checked(chars.Length * sizeof(char))));
            }
        }

        public void Append(int value)
        {
            Span<byte> buf = stackalloc byte[4];
            Unsafe.WriteUnaligned(ref buf[0], value);
            Append(buf);
        }

        public void Append(long value)
        {
            Span<byte> buf = stackalloc byte[8];
            Unsafe.WriteUnaligned(ref buf[0], value);
            Append(buf);
        }

        public void Append(decimal value, bool normalizeScale = true)
        {
            if (normalizeScale)
            {
                value = DecimalMath.Normalize(value);
            }

            Span<byte> buf = stackalloc byte[16];
#if NET8_0_OR_GREATER
            Span<int> bits = stackalloc int[4];
            decimal.GetBits(value, bits);
            for (int i = 0; i < 4; i++)
                Unsafe.WriteUnaligned(ref buf[i * 4], bits[i]);
#else
            Unsafe.WriteUnaligned(ref buf[0], value);
#endif
            Append(buf);
        }

        /// <summary>
        /// Computes the final 64-bit hash without mutating the accumulated state.
        /// </summary>
        public ulong Digest()
        {
            ulong h64;
            if (_totalLen >= 32)
            {
                h64 = FastXxHash64.RotateLeft64(_v1, 1) +
                      FastXxHash64.RotateLeft64(_v2, 7) +
                      FastXxHash64.RotateLeft64(_v3, 12) +
                      FastXxHash64.RotateLeft64(_v4, 18);

                h64 = FastXxHash64.MergeRound(h64, _v1);
                h64 = FastXxHash64.MergeRound(h64, _v2);
                h64 = FastXxHash64.MergeRound(h64, _v3);
                h64 = FastXxHash64.MergeRound(h64, _v4);
            }
            else
            {
                h64 = _seed + FastXxHash64.Prime64_5;
            }

            h64 += _totalLen;

            fixed (byte* bufPtr = _buffer)
            {
                byte* ptr = bufPtr;
                byte* end = bufPtr + _bufLen;

                while (ptr + 8 <= end)
                {
                    ulong k1 = FastXxHash64.Round(0, Unsafe.ReadUnaligned<ulong>(ptr));
                    h64 ^= k1;
                    h64 = (FastXxHash64.RotateLeft64(h64, 27) * FastXxHash64.Prime64_1) + FastXxHash64.Prime64_4;
                    ptr += 8;
                }

                if (ptr + 4 <= end)
                {
                    h64 ^= Unsafe.ReadUnaligned<uint>(ptr) * FastXxHash64.Prime64_1;
                    h64 = (FastXxHash64.RotateLeft64(h64, 23) * FastXxHash64.Prime64_2) + FastXxHash64.Prime64_3;
                    ptr += 4;
                }

                while (ptr < end)
                {
                    h64 ^= (*ptr++) * FastXxHash64.Prime64_5;
                    h64 = FastXxHash64.RotateLeft64(h64, 11) * FastXxHash64.Prime64_1;
                }
            }

            return FastXxHash64.Avalanche(h64);
        }
    }
}
