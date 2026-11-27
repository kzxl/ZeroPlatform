using System;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Time
{
    /// <summary>
    /// Half-open calendar date interval <c>[Start, EndExclusive)</c> for accounting periods, price list validity, contracts, and depreciation schedules.
    /// </summary>
    public readonly struct DateRange : IEquatable<DateRange>
    {
        public static readonly DateRange Empty = default;

        public CompactDate Start { get; }
        public CompactDate EndExclusive { get; }

        public DateRange(CompactDate start, CompactDate endExclusive)
        {
            if (endExclusive < start)
                throw new ArgumentException("EndExclusive must be greater than or equal to Start.", nameof(endExclusive));

            Start = start;
            EndExclusive = endExclusive;
        }

        public bool IsEmpty => EndExclusive == Start;
        public int TotalDays => EndExclusive - Start;

        public CompactDate EndInclusive
        {
            get
            {
                if (IsEmpty)
                    throw new InvalidOperationException("An empty DateRange has no EndInclusive date.");
                return EndExclusive.AddDays(-1);
            }
        }

        /// <summary>
        /// Creates a <see cref="DateRange"/> covering <paramref name="start"/> through <paramref name="endInclusive"/> inclusive.
        /// </summary>
        public static DateRange Inclusive(CompactDate start, CompactDate endInclusive)
        {
            if (endInclusive < start)
                throw new ArgumentException("EndInclusive must be greater than or equal to Start.", nameof(endInclusive));
            return new DateRange(start, endInclusive.AddDays(1));
        }

        public static DateRange ForMonth(int year, int month)
        {
            var start = new CompactDate(year, month, 1);
            return new DateRange(start, start.AddMonths(1));
        }

        public static DateRange ForQuarter(int year, int quarter)
        {
            if ((uint)(quarter - 1) >= 4U)
                throw new ArgumentOutOfRangeException(nameof(quarter), "Quarter must be between 1 and 4.");
            var start = new CompactDate(year, ((quarter - 1) * 3) + 1, 1);
            return new DateRange(start, start.AddMonths(3));
        }

        public static DateRange ForYear(int year)
        {
            var start = new CompactDate(year, 1, 1);
            return new DateRange(start, new CompactDate(year + 1, 1, 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(CompactDate date)
            => date >= Start && date < EndExclusive;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(DateRange other)
            => !IsEmpty && !other.IsEmpty && other.Start >= Start && other.EndExclusive <= EndExclusive;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(DateRange other)
            => !IsEmpty && !other.IsEmpty && Start < other.EndExclusive && other.Start < EndExclusive;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAdjacentTo(DateRange other)
            => !IsEmpty && !other.IsEmpty && (EndExclusive == other.Start || other.EndExclusive == Start);

        public bool TryIntersect(DateRange other, out DateRange intersection)
        {
            if (!Overlaps(other))
            {
                intersection = Empty;
                return false;
            }

            CompactDate maxStart = Start > other.Start ? Start : other.Start;
            CompactDate minEnd = EndExclusive < other.EndExclusive ? EndExclusive : other.EndExclusive;
            intersection = new DateRange(maxStart, minEnd);
            return true;
        }

        public bool TryUnion(DateRange other, out DateRange union)
        {
            if (IsEmpty) { union = other; return !other.IsEmpty; }
            if (other.IsEmpty) { union = this; return true; }

            if (!Overlaps(other) && !IsAdjacentTo(other))
            {
                union = Empty;
                return false;
            }

            CompactDate minStart = Start < other.Start ? Start : other.Start;
            CompactDate maxEnd = EndExclusive > other.EndExclusive ? EndExclusive : other.EndExclusive;
            union = new DateRange(minStart, maxEnd);
            return true;
        }

        /// <summary>
        /// Subtracts <paramref name="excluded"/> from this range, writing 0, 1, or 2 remaining sub-ranges into <paramref name="destination"/>.
        /// </summary>
        public int Subtract(DateRange excluded, Span<DateRange> destination)
        {
            if (IsEmpty) return 0;
            if (!Overlaps(excluded))
            {
                if (destination.Length < 1) throw new ArgumentException("Destination span too small.", nameof(destination));
                destination[0] = this;
                return 1;
            }

            int count = 0;
            if (Start < excluded.Start)
            {
                if (count >= destination.Length) throw new ArgumentException("Destination span too small.", nameof(destination));
                destination[count++] = new DateRange(Start, excluded.Start);
            }
            if (excluded.EndExclusive < EndExclusive)
            {
                if (count >= destination.Length) throw new ArgumentException("Destination span too small.", nameof(destination));
                destination[count++] = new DateRange(excluded.EndExclusive, EndExclusive);
            }
            return count;
        }

        /// <summary>
        /// Splits this range along calendar month boundaries into <paramref name="destination"/>, returning the number of slices written.
        /// </summary>
        public int SplitByMonth(Span<DateRange> destination)
        {
            if (IsEmpty) return 0;

            int count = 0;
            CompactDate cursor = Start;
            while (cursor < EndExclusive)
            {
                if (count >= destination.Length)
                    throw new ArgumentException("Destination span is too small to hold all monthly slices.", nameof(destination));

                CompactDate nextMonth = cursor.StartOfMonth().AddMonths(1);
                CompactDate sliceEnd = nextMonth < EndExclusive ? nextMonth : EndExclusive;
                destination[count++] = new DateRange(cursor, sliceEnd);
                cursor = sliceEnd;
            }

            return count;
        }

        public bool Equals(DateRange other) => Start == other.Start && EndExclusive == other.EndExclusive;
        public override bool Equals(object? obj) => obj is DateRange other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Start.GetHashCode() * 397) ^ EndExclusive.GetHashCode();
            }
        }

        public override string ToString() => $"[{Start}, {EndExclusive})";

        public static bool operator ==(DateRange left, DateRange right) => left.Equals(right);
        public static bool operator !=(DateRange left, DateRange right) => !left.Equals(right);
    }
}
