using System;
#if NET8_0_OR_GREATER
using System.Numerics;
#endif

namespace ZeroNetwork.Common
{
    /// <summary>
    /// High-performance bit manipulation operations providing hardware-accelerated intrinsics
    /// on .NET 8+ and branchless De Bruijn bit-twiddling algorithms on .NET Framework 4.6.2 and .NET Standard 2.0.
    /// Eliminates loops and Math.Log calls across hash tables, ring buffers, and binary serialization.
    /// </summary>
    public static class ZeroBitOps
    {
        // De Bruijn multiplication lookup table for 32-bit TrailingZeroCount
        private static readonly byte[] DeBruijnTrailingZeroTable = new byte[32]
        {
            0, 1, 28, 2, 29, 14, 24, 3, 30, 22, 20, 15, 25, 17, 4, 8,
            31, 27, 13, 23, 21, 19, 16, 7, 26, 12, 18, 6, 11, 5, 10, 9
        };

        /// <summary>
        /// Determines whether the specified integer is a power of two.
        /// </summary>
        public static bool IsPowerOfTwo(uint value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        /// <summary>
        /// Determines whether the specified 64-bit integer is a power of two.
        /// </summary>
        public static bool IsPowerOfTwo(ulong value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        /// <summary>
        /// Rounds a 32-bit unsigned integer up to the next power of two.
        /// If the value is already a power of two, it is returned unchanged.
        /// </summary>
        public static uint RoundUpToPowerOfTwo(uint value)
        {
            if (value <= 1) return 1;
#if NET8_0_OR_GREATER
            return BitOperations.RoundUpToPowerOf2(value);
#else
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
        /// Rounds a 64-bit unsigned integer up to the next power of two.
        /// </summary>
        public static ulong RoundUpToPowerOfTwo(ulong value)
        {
            if (value <= 1) return 1;
#if NET8_0_OR_GREATER
            return BitOperations.RoundUpToPowerOf2(value);
#else
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

        /// <summary>
        /// Returns the population count (number of set bits / Hamming weight) of a 32-bit unsigned integer.
        /// </summary>
        public static int PopCount(uint value)
        {
#if NET8_0_OR_GREATER
            return BitOperations.PopCount(value);
#else
            // Branchless parallel bit counter
            value -= ((value >> 1) & 0x55555555);
            value = (value & 0x33333333) + ((value >> 2) & 0x33333333);
            return (int)((((value + (value >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24);
#endif
        }

        /// <summary>
        /// Returns the population count (number of set bits / Hamming weight) of a 64-bit unsigned integer.
        /// </summary>
        public static int PopCount(ulong value)
        {
#if NET8_0_OR_GREATER
            return BitOperations.PopCount(value);
#else
            return PopCount((uint)value) + PopCount((uint)(value >> 32));
#endif
        }

        /// <summary>
        /// Counts the number of trailing zero bits in a 32-bit unsigned integer.
        /// Returns 32 if the input is 0.
        /// </summary>
        public static int TrailingZeroCount(uint value)
        {
#if NET8_0_OR_GREATER
            return BitOperations.TrailingZeroCount(value);
#else
            if (value == 0) return 32;
            // De Bruijn multiplication for O(1) CTZ
            return DeBruijnTrailingZeroTable[((uint)((value & -(int)value) * 0x077CB531U)) >> 27];
#endif
        }

        /// <summary>
        /// Counts the number of trailing zero bits in a 64-bit unsigned integer.
        /// Returns 64 if the input is 0.
        /// </summary>
        public static int TrailingZeroCount(ulong value)
        {
#if NET8_0_OR_GREATER
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
        /// Counts the number of leading zero bits in a 32-bit unsigned integer.
        /// Returns 32 if the input is 0.
        /// </summary>
        public static int LeadingZeroCount(uint value)
        {
#if NET8_0_OR_GREATER
            return BitOperations.LeadingZeroCount(value);
#else
            if (value == 0) return 32;
            int n = 0;
            if ((value & 0xFFFF0000) == 0) { n += 16; value <<= 16; }
            if ((value & 0xFF000000) == 0) { n += 8; value <<= 8; }
            if ((value & 0xF0000000) == 0) { n += 4; value <<= 4; }
            if ((value & 0xC0000000) == 0) { n += 2; value <<= 2; }
            if ((value & 0x80000000) == 0) { n += 1; }
            return n;
#endif
        }

        /// <summary>
        /// Counts the number of leading zero bits in a 64-bit unsigned integer.
        /// Returns 64 if the input is 0.
        /// </summary>
        public static int LeadingZeroCount(ulong value)
        {
#if NET8_0_OR_GREATER
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
        /// Returns the floor of the base-2 logarithm of the specified integer.
        /// Returns 0 if value is 0.
        /// </summary>
        public static int Log2(uint value)
        {
#if NET8_0_OR_GREATER
            return BitOperations.Log2(value);
#else
            if (value == 0) return 0;
            return 31 - LeadingZeroCount(value);
#endif
        }

        /// <summary>
        /// Returns the floor of the base-2 logarithm of the specified 64-bit integer.
        /// Returns 0 if value is 0.
        /// </summary>
        public static int Log2(ulong value)
        {
#if NET8_0_OR_GREATER
            return BitOperations.Log2(value);
#else
            if (value == 0) return 0;
            return 63 - LeadingZeroCount(value);
#endif
        }

        /// <summary>
        /// Rotates the specified 32-bit unsigned integer left by the specified number of bits.
        /// </summary>
        public static uint RotateLeft(uint value, int offset)
        {
#if NET8_0_OR_GREATER
            return BitOperations.RotateLeft(value, offset);
#else
            return (value << offset) | (value >> (32 - offset));
#endif
        }

        /// <summary>
        /// Rotates the specified 32-bit unsigned integer right by the specified number of bits.
        /// </summary>
        public static uint RotateRight(uint value, int offset)
        {
#if NET8_0_OR_GREATER
            return BitOperations.RotateRight(value, offset);
#else
            return (value >> offset) | (value << (32 - offset));
#endif
        }

        /// <summary>
        /// Rotates the specified 64-bit unsigned integer left by the specified number of bits.
        /// </summary>
        public static ulong RotateLeft(ulong value, int offset)
        {
#if NET8_0_OR_GREATER
            return BitOperations.RotateLeft(value, offset);
#else
            return (value << offset) | (value >> (64 - offset));
#endif
        }

        /// <summary>
        /// Rotates the specified 64-bit unsigned integer right by the specified number of bits.
        /// </summary>
        public static ulong RotateRight(ulong value, int offset)
        {
#if NET8_0_OR_GREATER
            return BitOperations.RotateRight(value, offset);
#else
            return (value >> offset) | (value << (64 - offset));
#endif
        }
    }
}
