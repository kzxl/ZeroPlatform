using System;
using System.Runtime.CompilerServices;

#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER || NET8_0_OR_GREATER
using System.Numerics;
#endif

namespace ZeroPrimitives.Buffers
{
    /// <summary>
    /// Hardware-accelerated bit manipulation primitives with fallback for legacy runtimes.
    /// Provides parity with System.Numerics.BitOperations on .NET Standard 2.0 and .NET Framework 4.6.2.
    /// </summary>
    public static class BitOps
    {
        private static readonly byte[] TrailingZeroCountDeBruijn = new byte[32]
        {
            0, 1, 28, 2, 29, 14, 24, 3, 30, 22, 20, 15, 25, 17, 4, 8,
            31, 27, 13, 23, 21, 19, 16, 7, 26, 12, 18, 6, 11, 5, 10, 9
        };

        /// <summary>
        /// Returns the population count (number of bits set to 1) of a 32-bit unsigned integer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopCount(uint value)
        {
#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER || NET8_0_OR_GREATER
            return BitOperations.PopCount(value);
#else
            value -= (value >> 1) & 0x55555555U;
            value = (value & 0x33333333U) + ((value >> 2) & 0x33333333U);
            value = (value + (value >> 4)) & 0x0F0F0F0FU;
            return (int)((value * 0x01010101U) >> 24);
#endif
        }

        /// <summary>
        /// Returns the population count (number of bits set to 1) of a 64-bit unsigned integer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopCount(ulong value)
        {
#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER || NET8_0_OR_GREATER
            return BitOperations.PopCount(value);
#else
            return PopCount((uint)value) + PopCount((uint)(value >> 32));
#endif
        }

        /// <summary>
        /// Returns the number of leading zero bits of a 32-bit unsigned integer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroCount(uint value)
        {
#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER || NET8_0_OR_GREATER
            return BitOperations.LeadingZeroCount(value);
#else
            if (value == 0) return 32;
            int n = 0;
            if (value <= 0x0000FFFFU) { n += 16; value <<= 16; }
            if (value <= 0x00FFFFFFU) { n += 8; value <<= 8; }
            if (value <= 0x0FFFFFFFU) { n += 4; value <<= 4; }
            if (value <= 0x3FFFFFFFU) { n += 2; value <<= 2; }
            if (value <= 0x7FFFFFFFU) { n += 1; }
            return n;
#endif
        }

        /// <summary>
        /// Returns the number of leading zero bits of a 64-bit unsigned integer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroCount(ulong value)
        {
#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER || NET8_0_OR_GREATER
            return BitOperations.LeadingZeroCount(value);
#else
            uint hi = (uint)(value >> 32);
            if (hi != 0)
            {
                return LeadingZeroCount(hi);
            }
            return 32 + LeadingZeroCount((uint)value);
#endif
        }

        /// <summary>
        /// Returns the number of trailing zero bits of a 32-bit unsigned integer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroCount(uint value)
        {
#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER || NET8_0_OR_GREATER
            return BitOperations.TrailingZeroCount(value);
#else
            if (value == 0) return 32;
            unchecked
            {
                return TrailingZeroCountDeBruijn[((uint)((value & -value) * 0x077CB531U)) >> 27];
            }
#endif
        }

        /// <summary>
        /// Returns the number of trailing zero bits of a 64-bit unsigned integer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroCount(ulong value)
        {
#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER || NET8_0_OR_GREATER
            return BitOperations.TrailingZeroCount(value);
#else
            uint lo = (uint)value;
            if (lo != 0)
            {
                return TrailingZeroCount(lo);
            }
            return 32 + TrailingZeroCount((uint)(value >> 32));
#endif
        }

        /// <summary>
        /// Rotates the specified 32-bit unsigned integer left by the specified number of bits.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RotateLeft(uint value, int offset)
        {
#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER || NET8_0_OR_GREATER
            return BitOperations.RotateLeft(value, offset);
#else
            return (value << (offset & 31)) | (value >> ((32 - offset) & 31));
#endif
        }

        /// <summary>
        /// Rotates the specified 32-bit unsigned integer right by the specified number of bits.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RotateRight(uint value, int offset)
        {
#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER || NET8_0_OR_GREATER
            return BitOperations.RotateRight(value, offset);
#else
            return (value >> (offset & 31)) | (value << ((32 - offset) & 31));
#endif
        }

        /// <summary>
        /// Determines whether the specified integer is a power of two.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPowerOfTwo(uint value)
        {
#if NET6_0_OR_GREATER
            return BitOperations.IsPow2(value);
#else
            return value > 0 && (value & (value - 1)) == 0;
#endif
        }

        /// <summary>
        /// Determines whether the specified 64-bit integer is a power of two.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPowerOfTwo(ulong value)
        {
#if NET6_0_OR_GREATER
            return BitOperations.IsPow2(value);
#else
            return value > 0 && (value & (value - 1)) == 0;
#endif
        }

        /// <summary>
        /// Rounds the specified 32-bit integer up to the next power of two.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RoundUpToPowerOfTwo(uint value)
        {
#if NET6_0_OR_GREATER
            return BitOperations.RoundUpToPowerOf2(value);
#else
            if (value <= 1) return value;
            if (value > 0x80000000U) return 0;
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
#endif
        }

        /// <summary>
        /// Rounds the specified 64-bit integer up to the next power of two.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong RoundUpToPowerOfTwo(ulong value)
        {
#if NET6_0_OR_GREATER
            return BitOperations.RoundUpToPowerOf2(value);
#else
            if (value <= 1) return value;
            if (value > 0x8000000000000000UL) return 0;
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            value |= value >> 32;
            return value + 1;
#endif
        }
    }
}
