using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Numerics
{
    /// <summary>
    /// Exact-sum proportional allocator using the Largest Remainder (Hamilton) method with zero heap allocation on small spans.
    /// Guarantees <c>Sum(destination) == total</c> without 1-unit rounding drift across invoice lines, taxes, discounts, and cost allocations.
    /// </summary>
    public static class ProportionalAllocator
    {
        private const int StackThreshold = 64;

        /// <summary>
        /// Allocates <paramref name="total"/> proportionally across <paramref name="weights"/> rounded to <paramref name="decimals"/> places,
        /// preserving exact sum conservation (<c>Sum(destination) == total</c>).
        /// </summary>
        public static void Allocate(
            decimal total,
            ReadOnlySpan<decimal> weights,
            Span<decimal> destination,
            int decimals = 0)
        {
            if (destination.Length != weights.Length)
                throw new ArgumentException("Destination span must match weights span length.", nameof(destination));
            if ((uint)decimals > 28U)
                throw new ArgumentOutOfRangeException(nameof(decimals), "Decimal places must be between 0 and 28.");

            if (DecimalMath.Round(total, decimals, RoundingMode.TowardZero) != total)
                throw new ArgumentException("Total exceeds the precision specified by decimals.", nameof(total));

            int count = weights.Length;
            if (count == 0) return;

            decimal weightSum = 0m;
            for (int i = 0; i < count; i++)
            {
                decimal w = weights[i];
                if (w < 0m)
                    throw new ArgumentOutOfRangeException(nameof(weights), "Weights must be non-negative.");
                weightSum += w;
            }

            if (total == 0m)
            {
                destination.Clear();
                return;
            }

            if (weightSum <= 0m)
                throw new ArgumentException("Sum of weights must be positive when total is non-zero.", nameof(weights));

            decimal factor = DecimalMath.GetPowerOf10(decimals);
            decimal minorUnit = 1m / factor;
            bool negative = total < 0m;
            decimal absTotal = negative ? -total : total;
            decimal totalUnits = absTotal * factor;

            decimal[]? rentedRem = null;
            int[]? rentedIdx = null;
            Span<decimal> remainders = count <= StackThreshold
                ? stackalloc decimal[count]
                : (rentedRem = ArrayPool<decimal>.Shared.Rent(count)).AsSpan(0, count);
            Span<int> indices = count <= StackThreshold
                ? stackalloc int[count]
                : (rentedIdx = ArrayPool<int>.Shared.Rent(count)).AsSpan(0, count);

            try
            {
                decimal allocatedUnits = 0m;
                for (int i = 0; i < count; i++)
                {
                    indices[i] = i;
                    decimal w = weights[i];
                    if (w == 0m)
                    {
                        destination[i] = 0m;
                        remainders[i] = -1m;
                        continue;
                    }

                    decimal exactUnits = ComputeExactUnits(totalUnits, w, weightSum);
                    decimal floored = Math.Floor(exactUnits);
                    remainders[i] = exactUnits - floored;
                    destination[i] = floored * minorUnit;
                    allocatedUnits += floored;
                }

                decimal leftover = totalUnits - allocatedUnits;
                if (leftover > 0m)
                {
                    int extraCount = (int)Math.Round(leftover, 0, MidpointRounding.AwayFromZero);
                    SortIndicesByRemainderDesc(indices, remainders, count);
                    for (int k = 0; k < extraCount && k < count; k++)
                    {
                        int idx = indices[k];
                        if (weights[idx] == 0m) break;
                        destination[idx] += minorUnit;
                    }
                }
                else if (leftover < 0m)
                {
                    int trimCount = (int)Math.Round(-leftover, 0, MidpointRounding.AwayFromZero);
                    SortIndicesByRemainderDesc(indices, remainders, count);
                    for (int k = count - 1; k >= 0 && trimCount > 0; k--)
                    {
                        int idx = indices[k];
                        if (destination[idx] >= minorUnit)
                        {
                            destination[idx] -= minorUnit;
                            trimCount--;
                        }
                    }
                }

                if (negative)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (destination[i] != 0m)
                            destination[i] = -destination[i];
                    }
                }
            }
            finally
            {
                if (rentedRem != null) ArrayPool<decimal>.Shared.Return(rentedRem);
                if (rentedIdx != null) ArrayPool<int>.Shared.Return(rentedIdx);
            }
        }

        /// <summary>
        /// Allocates an integer <paramref name="total"/> proportionally across <paramref name="weights"/> using exact 128-bit arithmetic,
        /// preserving <c>Sum(destination) == total</c>.
        /// </summary>
        public static void Allocate(long total, ReadOnlySpan<long> weights, Span<long> destination)
        {
            if (destination.Length != weights.Length)
                throw new ArgumentException("Destination span must match weights span length.", nameof(destination));

            int count = weights.Length;
            if (count == 0) return;

#if NET8_0_OR_GREATER
            UInt128 weightSum = 0;
            for (int i = 0; i < count; i++)
            {
                long w = weights[i];
                if (w < 0)
                    throw new ArgumentOutOfRangeException(nameof(weights), "Weights must be non-negative.");
                weightSum += (ulong)w;
            }

            if (total == 0)
            {
                destination.Clear();
                return;
            }

            if (weightSum == 0)
                throw new ArgumentException("Sum of weights must be positive when total is non-zero.", nameof(weights));

            bool negative = total < 0;
            ulong absTotal = negative ? unchecked((ulong)(0L - total)) : (ulong)total;

            UInt128[]? rentedRem = null;
            int[]? rentedIdx = null;
            Span<UInt128> remainders = count <= StackThreshold
                ? stackalloc UInt128[count]
                : (rentedRem = ArrayPool<UInt128>.Shared.Rent(count)).AsSpan(0, count);
            Span<int> indices = count <= StackThreshold
                ? stackalloc int[count]
                : (rentedIdx = ArrayPool<int>.Shared.Rent(count)).AsSpan(0, count);

            try
            {
                ulong allocated = 0;
                for (int i = 0; i < count; i++)
                {
                    indices[i] = i;
                    long w = weights[i];
                    if (w == 0)
                    {
                        destination[i] = 0;
                        remainders[i] = 0;
                        continue;
                    }

                    UInt128 product = (UInt128)absTotal * (ulong)w;
                    UInt128 q = product / weightSum;
                    UInt128 r = product % weightSum;
                    destination[i] = unchecked((long)(ulong)q);
                    remainders[i] = r;
                    allocated += (ulong)q;
                }

                ulong leftover = absTotal - allocated;
                if (leftover > 0)
                {
                    SortIndicesByUInt128RemainderDesc(indices, remainders, weights, count);
                    for (ulong k = 0; k < leftover && (int)k < count; k++)
                    {
                        int idx = indices[(int)k];
                        if (weights[idx] == 0) break;
                        destination[idx]++;
                    }
                }

                if (negative)
                {
                    for (int i = 0; i < count; i++)
                    {
                        destination[i] = -destination[i];
                    }
                }
            }
            finally
            {
                if (rentedRem != null) ArrayPool<UInt128>.Shared.Return(rentedRem);
                if (rentedIdx != null) ArrayPool<int>.Shared.Return(rentedIdx);
            }
#else
            UInt128Pair weightSum = default;
            for (int i = 0; i < count; i++)
            {
                long w = weights[i];
                if (w < 0)
                    throw new ArgumentOutOfRangeException(nameof(weights), "Weights must be non-negative.");
                weightSum = UInt128Pair.Add(weightSum, (ulong)w);
            }

            if (total == 0)
            {
                destination.Clear();
                return;
            }

            if (weightSum.IsZero)
                throw new ArgumentException("Sum of weights must be positive when total is non-zero.", nameof(weights));

            bool negative = total < 0;
            ulong absTotal = negative ? unchecked((ulong)(0L - total)) : (ulong)total;

            UInt128Pair[]? rentedRem = null;
            int[]? rentedIdx = null;
            Span<UInt128Pair> remainders = count <= StackThreshold
                ? stackalloc UInt128Pair[count]
                : (rentedRem = ArrayPool<UInt128Pair>.Shared.Rent(count)).AsSpan(0, count);
            Span<int> indices = count <= StackThreshold
                ? stackalloc int[count]
                : (rentedIdx = ArrayPool<int>.Shared.Rent(count)).AsSpan(0, count);

            try
            {
                ulong allocated = 0;
                for (int i = 0; i < count; i++)
                {
                    indices[i] = i;
                    long w = weights[i];
                    if (w == 0)
                    {
                        destination[i] = 0;
                        remainders[i] = default;
                        continue;
                    }

                    UInt128Pair product = UInt128Pair.Mul64(absTotal, (ulong)w);
                    UInt128Pair.DivRem(product, weightSum, out UInt128Pair q, out UInt128Pair r);
                    destination[i] = unchecked((long)q.Lo);
                    remainders[i] = r;
                    allocated += q.Lo;
                }

                ulong leftover = absTotal - allocated;
                if (leftover > 0)
                {
                    SortIndicesByPairRemainderDesc(indices, remainders, weights, count);
                    for (ulong k = 0; k < leftover && (int)k < count; k++)
                    {
                        int idx = indices[(int)k];
                        if (weights[idx] == 0) break;
                        destination[idx]++;
                    }
                }

                if (negative)
                {
                    for (int i = 0; i < count; i++)
                    {
                        destination[i] = -destination[i];
                    }
                }
            }
            finally
            {
                if (rentedRem != null) ArrayPool<UInt128Pair>.Shared.Return(rentedRem);
                if (rentedIdx != null) ArrayPool<int>.Shared.Return(rentedIdx);
            }
#endif
        }

        /// <summary>
        /// Splits <paramref name="total"/> equally across all slots of <paramref name="destination"/> with exact sum conservation.
        /// </summary>
        public static void AllocateEqual(decimal total, Span<decimal> destination, int decimals = 0)
        {
            if ((uint)decimals > 28U)
                throw new ArgumentOutOfRangeException(nameof(decimals), "Decimal places must be between 0 and 28.");
            if (DecimalMath.Round(total, decimals, RoundingMode.TowardZero) != total)
                throw new ArgumentException("Total exceeds the precision specified by decimals.", nameof(total));

            int count = destination.Length;
            if (count == 0) return;
            if (total == 0m)
            {
                destination.Clear();
                return;
            }

            decimal factor = DecimalMath.GetPowerOf10(decimals);
            decimal minorUnit = 1m / factor;
            bool negative = total < 0m;
            decimal absTotal = negative ? -total : total;
            decimal totalUnits = absTotal * factor;

            decimal baseUnits = Math.Floor(totalUnits / count);
            decimal baseValue = baseUnits * minorUnit;
            int remainderUnits = (int)Math.Round(totalUnits - (baseUnits * count), 0, MidpointRounding.AwayFromZero);

            for (int i = 0; i < count; i++)
            {
                decimal val = i < remainderUnits ? (baseValue + minorUnit) : baseValue;
                destination[i] = negative && val != 0m ? -val : val;
            }
        }

        /// <summary>
        /// Splits <paramref name="total"/> equally across all slots of <paramref name="destination"/> with exact sum conservation.
        /// </summary>
        public static void AllocateEqual(long total, Span<long> destination)
        {
            int count = destination.Length;
            if (count == 0) return;
            if (total == 0)
            {
                destination.Clear();
                return;
            }

            bool negative = total < 0;
            ulong absTotal = negative ? unchecked((ulong)(0L - total)) : (ulong)total;
            ulong uCount = (ulong)count;
            ulong baseVal = absTotal / uCount;
            ulong rem = absTotal % uCount;

            for (int i = 0; i < count; i++)
            {
                long val = unchecked((long)((ulong)i < rem ? baseVal + 1 : baseVal));
                destination[i] = negative ? -val : val;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static decimal ComputeExactUnits(decimal totalUnits, decimal weight, decimal weightSum)
        {
            try
            {
                return (totalUnits * weight) / weightSum;
            }
            catch (OverflowException)
            {
                return totalUnits * (weight / weightSum);
            }
        }

        private static void SortIndicesByRemainderDesc(Span<int> indices, ReadOnlySpan<decimal> remainders, int count)
        {
            for (int i = 1; i < count; i++)
            {
                int keyIdx = indices[i];
                decimal keyRem = remainders[keyIdx];
                int j = i - 1;
                while (j >= 0)
                {
                    int curIdx = indices[j];
                    decimal curRem = remainders[curIdx];
                    if (curRem > keyRem || (curRem == keyRem && curIdx < keyIdx))
                        break;
                    indices[j + 1] = curIdx;
                    j--;
                }
                indices[j + 1] = keyIdx;
            }
        }

#if NET8_0_OR_GREATER
        private static void SortIndicesByUInt128RemainderDesc(
            Span<int> indices,
            ReadOnlySpan<UInt128> remainders,
            ReadOnlySpan<long> weights,
            int count)
        {
            for (int i = 1; i < count; i++)
            {
                int keyIdx = indices[i];
                UInt128 keyRem = remainders[keyIdx];
                bool keyNonZero = weights[keyIdx] > 0;
                int j = i - 1;
                while (j >= 0)
                {
                    int curIdx = indices[j];
                    bool curNonZero = weights[curIdx] > 0;
                    if (curNonZero != keyNonZero)
                    {
                        if (curNonZero) break;
                    }
                    else
                    {
                        UInt128 curRem = remainders[curIdx];
                        if (curRem > keyRem || (curRem == keyRem && curIdx < keyIdx))
                            break;
                    }
                    indices[j + 1] = curIdx;
                    j--;
                }
                indices[j + 1] = keyIdx;
            }
        }
#else
        private readonly struct UInt128Pair : IComparable<UInt128Pair>
        {
            public readonly ulong Hi;
            public readonly ulong Lo;

            public UInt128Pair(ulong hi, ulong lo)
            {
                Hi = hi;
                Lo = lo;
            }

            public bool IsZero => Hi == 0 && Lo == 0;

            public int CompareTo(UInt128Pair other)
            {
                int cmp = Hi.CompareTo(other.Hi);
                return cmp != 0 ? cmp : Lo.CompareTo(other.Lo);
            }

            public static UInt128Pair Add(UInt128Pair a, ulong b)
            {
                ulong lo = unchecked(a.Lo + b);
                ulong carry = lo < a.Lo ? 1UL : 0UL;
                return new UInt128Pair(a.Hi + carry, lo);
            }

            public static UInt128Pair Sub(UInt128Pair a, UInt128Pair b)
            {
                ulong lo = unchecked(a.Lo - b.Lo);
                ulong borrow = a.Lo < b.Lo ? 1UL : 0UL;
                return new UInt128Pair(a.Hi - b.Hi - borrow, lo);
            }

            public static UInt128Pair Mul64(ulong a, ulong b)
            {
                ulong aLo = (uint)a;
                ulong aHi = a >> 32;
                ulong bLo = (uint)b;
                ulong bHi = b >> 32;

                ulong p0 = aLo * bLo;
                ulong p1 = aLo * bHi;
                ulong p2 = aHi * bLo;
                ulong p3 = aHi * bHi;

                ulong mid = (p0 >> 32) + (uint)p1 + (uint)p2;
                ulong lo = (p0 & 0xFFFFFFFFUL) | (mid << 32);
                ulong hi = p3 + (p1 >> 32) + (p2 >> 32) + (mid >> 32);
                return new UInt128Pair(hi, lo);
            }

            public static void DivRem(UInt128Pair dividend, UInt128Pair divisor, out UInt128Pair quotient, out UInt128Pair remainder)
            {
                if (dividend.CompareTo(divisor) < 0)
                {
                    quotient = default;
                    remainder = dividend;
                    return;
                }

                ulong qHi = 0, qLo = 0;
                UInt128Pair rem = default;

                for (int bit = 127; bit >= 0; bit--)
                {
                    // Shift rem left by 1 and bring in dividend bit
                    ulong nextBit = bit >= 64 ? ((dividend.Hi >> (bit - 64)) & 1UL) : ((dividend.Lo >> bit) & 1UL);
                    rem = new UInt128Pair((rem.Hi << 1) | (rem.Lo >> 63), (rem.Lo << 1) | nextBit);

                    if (rem.CompareTo(divisor) >= 0)
                    {
                        rem = Sub(rem, divisor);
                        if (bit >= 64) qHi |= 1UL << (bit - 64);
                        else qLo |= 1UL << bit;
                    }
                }

                quotient = new UInt128Pair(qHi, qLo);
                remainder = rem;
            }
        }

        private static void SortIndicesByPairRemainderDesc(
            Span<int> indices,
            ReadOnlySpan<UInt128Pair> remainders,
            ReadOnlySpan<long> weights,
            int count)
        {
            for (int i = 1; i < count; i++)
            {
                int keyIdx = indices[i];
                UInt128Pair keyRem = remainders[keyIdx];
                bool keyNonZero = weights[keyIdx] > 0;
                int j = i - 1;
                while (j >= 0)
                {
                    int curIdx = indices[j];
                    bool curNonZero = weights[curIdx] > 0;
                    if (curNonZero != keyNonZero)
                    {
                        if (curNonZero) break;
                    }
                    else
                    {
                        int cmp = remainders[curIdx].CompareTo(keyRem);
                        if (cmp > 0 || (cmp == 0 && curIdx < keyIdx))
                            break;
                    }
                    indices[j + 1] = curIdx;
                    j--;
                }
                indices[j + 1] = keyIdx;
            }
        }
#endif
    }
}
