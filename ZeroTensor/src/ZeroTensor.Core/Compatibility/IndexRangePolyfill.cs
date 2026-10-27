#if !NETCOREAPP3_0_OR_GREATER && !NETSTANDARD2_1_OR_GREATER
using System.Runtime.CompilerServices;

namespace System
{
    /// <summary>
    /// Represents a type that can be used to index a collection either from the start or the end.
    /// Polyfill for .NET Standard 2.0 and .NET Framework 4.6.2.
    /// </summary>
    public readonly struct Index : IEquatable<Index>
    {
        private readonly int _value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Index(int value, bool fromEnd = false)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Index value must be non-negative.");
            }

            _value = fromEnd ? ~value : value;
        }

        private Index(int value)
        {
            _value = value;
        }

        public static Index Start => new Index(0);
        public static Index End => new Index(~0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Index FromStart(int value) => (value < 0)
            ? throw new ArgumentOutOfRangeException(nameof(value), "Index value must be non-negative.")
            : new Index(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Index FromEnd(int value) => (value < 0)
            ? throw new ArgumentOutOfRangeException(nameof(value), "Index value must be non-negative.")
            : new Index(~value);

        public int Value => _value < 0 ? ~_value : _value;
        public bool IsFromEnd => _value < 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetOffset(int length)
        {
            int offset = _value;
            if (IsFromEnd)
            {
                offset += length + 1;
            }
            return offset;
        }

        public override bool Equals(object? obj) => obj is Index index && _value == index._value;
        public bool Equals(Index other) => _value == other._value;
        public override int GetHashCode() => _value;
        public static implicit operator Index(int value) => FromStart(value);
        public override string ToString() => IsFromEnd ? $"^{Value}" : ((uint)Value).ToString();
    }

    /// <summary>
    /// Represents a range that has start and end indexes.
    /// Polyfill for .NET Standard 2.0 and .NET Framework 4.6.2.
    /// </summary>
    public readonly struct Range : IEquatable<Range>
    {
        public Index Start { get; }
        public Index End { get; }

        public Range(Index start, Index end)
        {
            Start = start;
            End = end;
        }

        public override bool Equals(object? obj) =>
            obj is Range range && range.Start.Equals(Start) && range.End.Equals(End);

        public bool Equals(Range other) => other.Start.Equals(Start) && other.End.Equals(End);
        public override int GetHashCode() => (Start.GetHashCode() * 397) ^ End.GetHashCode();
        public override string ToString() => $"{Start}..{End}";
        public static Range StartAt(Index start) => new Range(start, Index.End);
        public static Range EndAt(Index end) => new Range(Index.Start, end);
        public static Range All => new Range(Index.Start, Index.End);
    }
}
#endif
