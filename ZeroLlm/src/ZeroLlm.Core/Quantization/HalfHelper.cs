using System;
using System.Runtime.CompilerServices;

namespace ZeroLlm.Core.Quantization
{
    /// <summary>
    /// High-performance IEEE-754 binary16 (half-precision float) converter.
    /// Provides hardware intrinsic conversions on .NET 8+ with zero-allocation bit-manipulation fallback.
    /// </summary>
    public static class HalfHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float HalfToFloat(ushort h)
        {
#if NET8_0_OR_GREATER
            return (float)BitConverter.UInt16BitsToHalf(h);
#else
            uint sign = (uint)(h & 0x8000) << 16;
            int exp = (h & 0x7C00) >> 10;
            uint mantissa = (uint)(h & 0x03FF);

            if (exp == 0)
            {
                if (mantissa == 0)
                {
                    return BitConverter.ToSingle(BitConverter.GetBytes(sign), 0);
                }
                while ((mantissa & 0x0400) == 0)
                {
                    mantissa <<= 1;
                    exp--;
                }
                exp++;
                mantissa &= ~0x0400u;
            }
            else if (exp == 31)
            {
                exp = 255;
            }
            else
            {
                exp = exp - 15 + 127;
            }

            uint f = sign | ((uint)exp << 23) | (mantissa << 13);
            return BitConverter.ToSingle(BitConverter.GetBytes(f), 0);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort FloatToHalf(float f)
        {
#if NET8_0_OR_GREATER
            return BitConverter.HalfToUInt16Bits((Half)f);
#else
            byte[] bytes = BitConverter.GetBytes(f);
            uint bits = BitConverter.ToUInt32(bytes, 0);

            uint sign = (bits >> 16) & 0x8000;
            int exp = (int)((bits >> 23) & 0xFF) - 127 + 15;
            uint mantissa = bits & 0x007FFFFF;

            if (exp <= 0)
            {
                return (ushort)sign;
            }
            if (exp >= 31)
            {
                return (ushort)(sign | 0x7C00);
            }

            return (ushort)(sign | ((uint)exp << 10) | (mantissa >> 13));
#endif
        }
    }
}
