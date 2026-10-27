using System;
using System.Runtime.CompilerServices;

namespace ZeroGraphics.Imaging
{
    internal static class MathCompat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ClampToByte(int value)
        {
            if (value < 0) return 0;
            if (value > 255) return 255;
            return (byte)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

#if NETFRAMEWORK
    internal static class MathF
    {
        public const float PI = (float)Math.PI;
        public const float E = (float)Math.E;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sqrt(float x) => (float)Math.Sqrt(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Abs(float x) => Math.Abs(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Min(float x, float y) => Math.Min(x, y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Max(float x, float y) => Math.Max(x, y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Pow(float x, float y) => (float)Math.Pow(x, y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Exp(float x) => (float)Math.Exp(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sin(float x) => (float)Math.Sin(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cos(float x) => (float)Math.Cos(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Floor(float x) => (float)Math.Floor(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Ceiling(float x) => (float)Math.Ceiling(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Round(float x) => (float)Math.Round(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sign(float x) => Math.Sign(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Clamp(float value, float min, float max) => MathCompat.Clamp(value, min, max);
    }
#endif
}
