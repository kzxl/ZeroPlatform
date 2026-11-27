using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZeroPrimitives.Parsing;
using ZeroPrimitives.Text;

namespace ZeroPrimitives.Core.Identifiers
{
    /// <summary>
    /// 8-byte, k-ordered 64-bit distributed identifier (<c>BIGINT</c>-native):
    /// 1 sign bit (<c>0</c>) + 41 timestamp ms bits + 10 node bits (<c>0..1023</c>) + 12 sequence bits (<c>0..4095</c>).
    /// Cuts clustered primary key and secondary index size by 50% compared to 16-byte GUIDs/UUIDs.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 8)]
    public readonly struct SnowflakeId : IEquatable<SnowflakeId>, IComparable<SnowflakeId>
    {
        /// <summary>Default custom epoch: 2024-01-01T00:00:00Z UTC in Unix milliseconds.</summary>
        public const long DefaultEpochUnixMs = 1704067200000L;
        public const int MaxNodeId = 1023;
        public const int MaxSequence = 4095;
        private const long MaxTimestampDelta = (1L << 41) - 1L;

        private readonly long _value;

        public static readonly SnowflakeId Empty = default;

        public SnowflakeId(long value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "SnowflakeId value must be non-negative.");
            _value = value;
        }

        public long Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _value;
        }

        public int NodeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (int)((_value >> 12) & MaxNodeId);
        }

        public int Sequence
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (int)(_value & MaxSequence);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetTimestampMilliseconds(long epochUnixMs = DefaultEpochUnixMs)
            => (_value >> 22) + epochUnixMs;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DateTimeOffset GetDateTimeOffset(long epochUnixMs = DefaultEpochUnixMs)
            => DateTimeOffset.FromUnixTimeMilliseconds(GetTimestampMilliseconds(epochUnixMs));

        public static SnowflakeId Compose(
            long timestampUnixMs,
            int nodeId,
            int sequence,
            long epochUnixMs = DefaultEpochUnixMs)
        {
            long delta = timestampUnixMs - epochUnixMs;
            if (delta < 0 || delta > MaxTimestampDelta)
                throw new ArgumentOutOfRangeException(nameof(timestampUnixMs), "Timestamp delta must fit in 41 bits.");
            if ((uint)nodeId > MaxNodeId)
                throw new ArgumentOutOfRangeException(nameof(nodeId), "NodeId must be between 0 and 1023.");
            if ((uint)sequence > MaxSequence)
                throw new ArgumentOutOfRangeException(nameof(sequence), "Sequence must be between 0 and 4095.");

            long raw = (delta << 22) | ((long)nodeId << 12) | (long)sequence;
            return new SnowflakeId(raw);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SnowflakeId MinValueForTimestamp(long timestampUnixMs, long epochUnixMs = DefaultEpochUnixMs)
            => Compose(timestampUnixMs, 0, 0, epochUnixMs);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SnowflakeId MaxValueForTimestamp(long timestampUnixMs, long epochUnixMs = DefaultEpochUnixMs)
            => Compose(timestampUnixMs, MaxNodeId, MaxSequence, epochUnixMs);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFormat(Span<char> destination, out int charsWritten)
            => FastFormat.TryFormatInt64(_value, destination, out charsWritten);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFormatUtf8(Span<byte> destination, out int bytesWritten)
            => FastFormat.TryFormatInt64Utf8(_value, destination, out bytesWritten);

        public static bool TryParse(ReadOnlySpan<char> input, out SnowflakeId result)
        {
            if (FastNumberParser.TryParseInt64(input, out long val) && val >= 0)
            {
                result = new SnowflakeId(val);
                return true;
            }
            result = default;
            return false;
        }

        public static bool TryParse(ReadOnlySpan<byte> utf8Input, out SnowflakeId result)
        {
            if (FastNumberParser.TryParseInt64(utf8Input, out long val) && val >= 0)
            {
                result = new SnowflakeId(val);
                return true;
            }
            result = default;
            return false;
        }

        public override unsafe string ToString()
        {
            Span<char> chars = stackalloc char[24];
            TryFormat(chars, out int written);
#if NET8_0_OR_GREATER
            return new string(chars.Slice(0, written));
#else
            fixed (char* p = chars)
                return new string(p, 0, written);
#endif
        }

        public bool Equals(SnowflakeId other) => _value == other._value;
        public override bool Equals(object? obj) => obj is SnowflakeId other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public int CompareTo(SnowflakeId other) => _value.CompareTo(other._value);

        public static bool operator ==(SnowflakeId left, SnowflakeId right) => left._value == right._value;
        public static bool operator !=(SnowflakeId left, SnowflakeId right) => left._value != right._value;
        public static bool operator <(SnowflakeId left, SnowflakeId right) => left._value < right._value;
        public static bool operator <=(SnowflakeId left, SnowflakeId right) => left._value <= right._value;
        public static bool operator >(SnowflakeId left, SnowflakeId right) => left._value > right._value;
        public static bool operator >=(SnowflakeId left, SnowflakeId right) => left._value >= right._value;

        public static explicit operator long(SnowflakeId id) => id._value;
    }

    /// <summary>
    /// Thread-safe, strictly monotonic <see cref="SnowflakeId"/> generator with sequence-rollover advancement and clock-drift protection.
    /// </summary>
    public sealed class SnowflakeGenerator
    {
        private readonly int _nodeId;
        private readonly long _epochUnixMs;
        private readonly int _maxClockDriftMs;
        private readonly object _sync = new object();

        private long _lastTimestampMs;
        private int _sequence;

        public SnowflakeGenerator(
            int nodeId,
            long epochUnixMs = SnowflakeId.DefaultEpochUnixMs,
            int maxClockDriftMs = 5)
        {
            if ((uint)nodeId > SnowflakeId.MaxNodeId)
                throw new ArgumentOutOfRangeException(nameof(nodeId), "NodeId must be between 0 and 1023.");
            if (maxClockDriftMs < 0)
                throw new ArgumentOutOfRangeException(nameof(maxClockDriftMs), "MaxClockDriftMs must be non-negative.");

            _nodeId = nodeId;
            _epochUnixMs = epochUnixMs;
            _maxClockDriftMs = maxClockDriftMs;
            _lastTimestampMs = epochUnixMs - 1;
        }

        public int NodeId => _nodeId;
        public long EpochUnixMs => _epochUnixMs;

        /// <summary>
        /// Generates the next strictly monotonic <see cref="SnowflakeId"/>.
        /// </summary>
        public SnowflakeId NextId()
        {
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            lock (_sync)
            {
                if (nowMs < _lastTimestampMs)
                {
                    long drift = _lastTimestampMs - nowMs;
                    if (drift > _maxClockDriftMs)
                    {
                        throw new InvalidOperationException(
                            $"System clock moved backwards by {drift}ms, exceeding configured tolerance of {_maxClockDriftMs}ms.");
                    }
                    nowMs = _lastTimestampMs;
                }

                if (nowMs == _lastTimestampMs)
                {
                    _sequence = (_sequence + 1) & SnowflakeId.MaxSequence;
                    if (_sequence == 0)
                    {
                        _lastTimestampMs++;
                        nowMs = _lastTimestampMs;
                    }
                }
                else
                {
                    _lastTimestampMs = nowMs;
                    _sequence = 0;
                }

                return SnowflakeId.Compose(nowMs, _nodeId, _sequence, _epochUnixMs);
            }
        }
    }
}
