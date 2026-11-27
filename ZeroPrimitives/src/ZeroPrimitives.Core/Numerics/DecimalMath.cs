using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZeroPrimitives.Numerics
{
    /// <summary>
    /// Explicit rounding modes supported consistently across all target frameworks (.NET Standard 2.0, .NET Framework 4.6.2, .NET 8.0+).
    /// </summary>
    public enum RoundingMode : byte
    {
        /// <summary>Round half to nearest even digit (Banker's rounding).</summary>
        HalfToEven = 0,
        /// <summary>Round half away from zero (Commercial / standard accounting rounding).</summary>
        HalfAwayFromZero = 1,
        /// <summary>Truncate toward zero.</summary>
        TowardZero = 2,
        /// <summary>Round away from zero whenever any discarded fractional digit is non-zero.</summary>
        AwayFromZero = 3,
        /// <summary>Ceiling: round toward positive infinity.</summary>
        TowardPositiveInfinity = 4,
        /// <summary>Floor: round toward negative infinity.</summary>
        TowardNegativeInfinity = 5
    }

    /// <summary>
    /// Domain-agnostic, zero-allocation decimal arithmetic, rounding, normalization, and statistical helpers.
    /// </summary>
    public static class DecimalMath
    {
        private static readonly decimal[] Pow10Table =
        {
            1m,
            10m,
            100m,
            1_000m,
            10_000m,
            100_000m,
            1_000_000m,
            10_000_000m,
            100_000_000m,
            1_000_000_000m,
            10_000_000_000m,
            100_000_000_000m,
            1_000_000_000_000m,
            10_000_000_000_000m,
            100_000_000_000_000m,
            1_000_000_000_000_000m,
            10_000_000_000_000_000m,
            100_000_000_000_000_000m,
            1_000_000_000_000_000_000m,
            10_000_000_000_000_000_000m,
            100_000_000_000_000_000_000m,
            1_000_000_000_000_000_000_000m,
            10_000_000_000_000_000_000_000m,
            100_000_000_000_000_000_000_000m,
            1_000_000_000_000_000_000_000_000m,
            10_000_000_000_000_000_000_000_000m,
            100_000_000_000_000_000_000_000_000m,
            1_000_000_000_000_000_000_000_000_000m,
            10_000_000_000_000_000_000_000_000_000m
        };

#if !NET8_0_OR_GREATER
        [StructLayout(LayoutKind.Explicit)]
        private struct DecimalLayout
        {
            [FieldOffset(0)] public int Flags;
            [FieldOffset(4)] public int Hi;
            [FieldOffset(8)] public int Lo;
            [FieldOffset(12)] public int Mid;
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static decimal GetPowerOf10(int decimals)
        {
            if ((uint)decimals > 28U)
                throw new ArgumentOutOfRangeException(nameof(decimals), "Decimal places must be between 0 and 28.");
            return Pow10Table[decimals];
        }

        /// <summary>
        /// Returns the scale (number of digits after the decimal point, 0..28) of a <see cref="decimal"/> without heap allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetScale(decimal value)
        {
#if NET8_0_OR_GREATER
            return value.Scale;
#else
            return (Unsafe.As<decimal, DecimalLayout>(ref value).Flags >> 16) & 0x7F;
#endif
        }

        /// <summary>
        /// Strips trailing fractional zeros from a <see cref="decimal"/> (e.g. <c>100.5000m</c> becomes <c>100.5m</c>, <c>-0.00m</c> becomes <c>0m</c>).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Normalize(decimal value)
        {
            if (value == 0m) return 0m;
            return value / 1.000000000000000000000000000000000m;
        }

        /// <summary>
        /// Rounds <paramref name="value"/> to <paramref name="decimals"/> places using the specified <see cref="RoundingMode"/>.
        /// </summary>
        public static decimal Round(decimal value, int decimals, RoundingMode mode = RoundingMode.HalfAwayFromZero)
        {
            if ((uint)decimals > 28U)
                throw new ArgumentOutOfRangeException(nameof(decimals), "Decimal places must be between 0 and 28.");

            switch (mode)
            {
                case RoundingMode.HalfToEven:
                    return Math.Round(value, decimals, MidpointRounding.ToEven);

                case RoundingMode.HalfAwayFromZero:
                    return Math.Round(value, decimals, MidpointRounding.AwayFromZero);

                case RoundingMode.TowardZero:
                case RoundingMode.AwayFromZero:
                case RoundingMode.TowardPositiveInfinity:
                case RoundingMode.TowardNegativeInfinity:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }

            if (GetScale(value) <= decimals)
                return value;

            if (decimals == 0)
            {
                return ApplyDirectionalRound(value, mode);
            }

            decimal intPart = Math.Truncate(value);
            decimal fracPart = value - intPart;
            if (fracPart == 0m) return intPart;

            decimal factor = Pow10Table[decimals];
            decimal scaled = fracPart * factor;
            decimal roundedScaled = ApplyDirectionalRound(scaled, mode);
            return intPart + (roundedScaled / factor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static decimal ApplyDirectionalRound(decimal value, RoundingMode mode)
        {
            switch (mode)
            {
                case RoundingMode.TowardZero:
                    return Math.Truncate(value);
                case RoundingMode.TowardPositiveInfinity:
                    return Math.Ceiling(value);
                case RoundingMode.TowardNegativeInfinity:
                    return Math.Floor(value);
                case RoundingMode.AwayFromZero:
                    return value >= 0m ? Math.Ceiling(value) : Math.Floor(value);
                default:
                    return value;
            }
        }

        /// <summary>
        /// Rounds <paramref name="value"/> to the nearest multiple of <paramref name="step"/> (e.g. <c>500m</c> or <c>0.05m</c>).
        /// </summary>
        public static decimal RoundToStep(decimal value, decimal step, RoundingMode mode = RoundingMode.HalfAwayFromZero)
        {
            if (step <= 0m)
                throw new ArgumentOutOfRangeException(nameof(step), "Step must be greater than zero.");

            decimal units = Round(value / step, 0, mode);
            return units * step;
        }

        /// <summary>
        /// Divides <paramref name="numerator"/> by <paramref name="denominator"/>, returning <paramref name="fallback"/> when <paramref name="denominator"/> is zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal SafeDivide(decimal numerator, decimal denominator, decimal fallback = 0m)
        {
            return denominator == 0m ? fallback : numerator / denominator;
        }

        /// <summary>
        /// Divides <paramref name="numerator"/> by <paramref name="denominator"/> and rounds the quotient, returning <paramref name="fallback"/> when <paramref name="denominator"/> is zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal SafeDivide(
            decimal numerator,
            decimal denominator,
            int decimals,
            RoundingMode mode = RoundingMode.HalfAwayFromZero,
            decimal fallback = 0m)
        {
            return denominator == 0m ? fallback : Round(numerator / denominator, decimals, mode);
        }

        /// <summary>
        /// Computes <c>(part / whole) * 100</c> rounded to <paramref name="decimals"/> places, returning <c>0</c> when <paramref name="whole"/> is zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Percentage(
            decimal part,
            decimal whole,
            int decimals = 2,
            RoundingMode mode = RoundingMode.HalfAwayFromZero)
        {
            if (whole == 0m) return 0m;
            return Round((part * 100m) / whole, decimals, mode);
        }

        /// <summary>
        /// Computes the weighted arithmetic average over two aligned spans with zero heap allocation.
        /// </summary>
        public static decimal WeightedAverage(
            ReadOnlySpan<decimal> values,
            ReadOnlySpan<decimal> weights,
            decimal fallback = 0m)
        {
            if (values.Length != weights.Length)
                throw new ArgumentException("Values and weights spans must have the same length.");

            if (values.IsEmpty) return fallback;

            decimal weightedSum = 0m;
            decimal weightSum = 0m;
            for (int i = 0; i < values.Length; i++)
            {
                decimal w = weights[i];
                weightedSum += values[i] * w;
                weightSum += w;
            }

            return weightSum == 0m ? fallback : weightedSum / weightSum;
        }
    }
}
