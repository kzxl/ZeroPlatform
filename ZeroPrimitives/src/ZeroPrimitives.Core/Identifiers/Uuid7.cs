using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace ZeroPrimitives.Core.Identifiers
{
    /// <summary>
    /// Represents an RFC 9562 compliant Version 7 Universally Unique Identifier (UUIDv7).
    /// Features 48-bit millisecond timestamp ordering, sub-millisecond monotonic sequence counters,
    /// and 62 bits of cryptographic entropy with zero managed heap allocation.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public readonly struct Uuid7 : IEquatable<Uuid7>, IComparable<Uuid7>
    {
        // 16-byte raw data layout (Big-Endian network order for proper chronological sorting)
        [FieldOffset(0)] private readonly ulong _high; // 48-bit time + 4-bit ver (7) + 12-bit seq/rand_a
        [FieldOffset(8)] private readonly ulong _low;  // 2-bit var (2) + 62-bit rand_b

        // Generator static state for thread-safe monotonic clock sequencing
        private static long _lastUnixMs;
        private static int _sequence;
        private static readonly object _syncLock = new object();

        [ThreadStatic]
        private static RandomNumberGenerator? _rng;

        [ThreadStatic]
        private static byte[]? _rngBuffer;

        private static RandomNumberGenerator Rng => _rng ??= RandomNumberGenerator.Create();

        public static readonly Uuid7 Empty = default;

        public Uuid7(ulong high, ulong low)
        {
            _high = high;
            _low = low;
        }

        public Uuid7(Guid guid)
        {
            Span<byte> bytes = stackalloc byte[16];
#if NET8_0_OR_GREATER
            guid.TryWriteBytes(bytes);
#else
            byte[] b = guid.ToByteArray();
            b.AsSpan().CopyTo(bytes);
#endif
            // Account for Windows Guid mixed-endian storage (Data1 LE, Data2 LE, Data3 LE, Data4 BE)
            if (BitConverter.IsLittleEndian)
            {
                // Reverse Data1 (int), Data2 (short), Data3 (short) to restore big-endian byte order
                byte t0 = bytes[0]; byte t1 = bytes[1]; byte t2 = bytes[2]; byte t3 = bytes[3];
                bytes[0] = t3; bytes[1] = t2; bytes[2] = t1; bytes[3] = t0;

                byte t4 = bytes[4]; byte t5 = bytes[5];
                bytes[4] = t5; bytes[5] = t4;

                byte t6 = bytes[6]; byte t7 = bytes[7];
                bytes[6] = t7; bytes[7] = t6;
            }

            _high = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(0, 8));
            _low = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(8, 8));
        }

        public Uuid7(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 16)
                throw new ArgumentException("Byte span must contain at least 16 bytes.", nameof(bytes));

            _high = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(0, 8));
            _low = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(8, 8));
        }

        /// <summary>
        /// Generates a new cryptographically random, monotonically ordered UUIDv7 at the current system time.
        /// </summary>
        public static Uuid7 NewUuid()
        {
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return Create(nowMs);
        }

        /// <summary>
        /// Generates a UUIDv7 from a specific UTC millisecond timestamp.
        /// </summary>
        public static Uuid7 Create(long unixTimeMs)
        {
#if NET8_0_OR_GREATER
            Span<byte> rand = stackalloc byte[10];
            Rng.GetBytes(rand);
#else
            byte[] buf = _rngBuffer ??= new byte[16];
            Rng.GetBytes(buf);
            ReadOnlySpan<byte> rand = buf.AsSpan(0, 10);
#endif

            ulong high;
            lock (_syncLock)
            {
                if (unixTimeMs > _lastUnixMs)
                {
                    _lastUnixMs = unixTimeMs;
                    // Seed sequence with 10 bits of entropy, leaving at least 3072 increments in the current ms
                    _sequence = (rand[0] << 8 | rand[1]) & 0x03FF;
                }
                else
                {
                    // Clock did not advance or skewed backward slightly: increment monotonic counter
                    _sequence++;
                    if (_sequence > 0x0FFF)
                    {
                        // Sequence rollover within same millisecond: advance timestamp by 1 ms per RFC 9562 §6.2
                        _lastUnixMs++;
                        _sequence = 0;
                    }
                }

                // High 64 bits: [48 bits timestamp] [4 bits version 0b0111] [12 bits sequence]
                high = ((ulong)_lastUnixMs << 16) | (0x7000UL | (ushort)_sequence);
            }

            // Low 64 bits: [2 bits variant 0b10] [62 bits entropy]
            ulong randLow = BinaryPrimitives.ReadUInt64BigEndian(rand.Slice(2, 8));
            ulong low = (0x8000000000000000UL | (randLow & 0x3FFFFFFFFFFFFFFFUL));

            return new Uuid7(high, low);
        }

        /// <summary>
        /// Extracts the UTC Unix millisecond timestamp embedded within this UUIDv7.
        /// </summary>
        public long GetUnixTimeMilliseconds() => (long)(_high >> 16);

        /// <summary>
        /// Extracts the timestamp as a DateTimeOffset.
        /// </summary>
        public DateTimeOffset GetDateTimeOffset() => DateTimeOffset.FromUnixTimeMilliseconds(GetUnixTimeMilliseconds());

        /// <summary>
        /// Converts the UUIDv7 to a standard System.Guid.
        /// </summary>
        public Guid ToGuid()
        {
            Span<byte> bytes = stackalloc byte[16];
            BinaryPrimitives.WriteUInt64BigEndian(bytes.Slice(0, 8), _high);
            BinaryPrimitives.WriteUInt64BigEndian(bytes.Slice(8, 8), _low);

            if (BitConverter.IsLittleEndian)
            {
                // Convert Big-Endian byte order to .NET Guid mixed-endian
                byte t0 = bytes[0]; byte t1 = bytes[1]; byte t2 = bytes[2]; byte t3 = bytes[3];
                bytes[0] = t3; bytes[1] = t2; bytes[2] = t1; bytes[3] = t0;

                byte t4 = bytes[4]; byte t5 = bytes[5];
                bytes[4] = t5; bytes[5] = t4;

                byte t6 = bytes[6]; byte t7 = bytes[7];
                bytes[6] = t7; bytes[7] = t6;
            }

#if NET8_0_OR_GREATER
            return new Guid(bytes);
#else
            return new Guid(bytes.ToArray());
#endif
        }

        /// <summary>
        /// Converts this UUIDv7 into a <see cref="Guid"/> whose bytes are permuted to match SQL Server <c>UNIQUEIDENTIFIER</c> (<c>SqlGuid</c>) sort order,
        /// preventing clustered B-Tree index fragmentation.
        /// </summary>
        public Guid ToSqlServerGuid()
        {
            Span<byte> raw = stackalloc byte[16];
            BinaryPrimitives.WriteUInt64BigEndian(raw.Slice(0, 8), _high);
            BinaryPrimitives.WriteUInt64BigEndian(raw.Slice(8, 8), _low);
            return EncodeSqlServerGuid(raw);
        }

        /// <summary>
        /// Reconstructs the original <see cref="Uuid7"/> from a SQL Server ordered <see cref="Guid"/> created by <see cref="ToSqlServerGuid"/>.
        /// </summary>
        public static Uuid7 FromSqlServerGuid(Guid sqlGuid)
        {
            Span<byte> raw = stackalloc byte[16];
            DecodeSqlServerGuid(sqlGuid, raw);
            return new Uuid7(raw);
        }

        internal static Guid EncodeSqlServerGuid(ReadOnlySpan<byte> raw)
        {
            // SqlGuid compares bytes in order: [10..15], [8..9], [6..7], [4..5], [0..3]
            // Place chronological MSB raw[0..5] into g[10..15] and raw[6..7] into g[8..9].
#if NET8_0_OR_GREATER
            Span<byte> g = stackalloc byte[16];
            g[10] = raw[0]; g[11] = raw[1]; g[12] = raw[2]; g[13] = raw[3]; g[14] = raw[4]; g[15] = raw[5];
            g[8] = raw[6];  g[9] = raw[7];
            g[6] = raw[8];  g[7] = raw[9];
            g[4] = raw[10]; g[5] = raw[11];
            g[0] = raw[12]; g[1] = raw[13]; g[2] = raw[14]; g[3] = raw[15];
            return new Guid(g);
#else
            int a = raw[12] | (raw[13] << 8) | (raw[14] << 16) | (raw[15] << 24);
            short b = (short)(raw[10] | (raw[11] << 8));
            short c = (short)(raw[8] | (raw[9] << 8));
            return new Guid(a, b, c, raw[6], raw[7], raw[0], raw[1], raw[2], raw[3], raw[4], raw[5]);
#endif
        }

        internal static void DecodeSqlServerGuid(Guid sqlGuid, Span<byte> raw)
        {
            Span<byte> g = stackalloc byte[16];
#if NET8_0_OR_GREATER
            sqlGuid.TryWriteBytes(g);
#else
            sqlGuid.ToByteArray().AsSpan().CopyTo(g);
#endif
            raw[0] = g[10]; raw[1] = g[11]; raw[2] = g[12]; raw[3] = g[13]; raw[4] = g[14]; raw[5] = g[15];
            raw[6] = g[8];  raw[7] = g[9];
            raw[8] = g[6];  raw[9] = g[7];
            raw[10] = g[4]; raw[11] = g[5];
            raw[12] = g[0]; raw[13] = g[1]; raw[14] = g[2]; raw[15] = g[3];
        }

        /// <summary>
        /// Returns the minimum possible <see cref="Uuid7"/> value for a given UTC millisecond timestamp (useful for primary-key range queries).
        /// </summary>
        public static Uuid7 MinValueForTimestamp(long unixTimeMs)
        {
            if (unixTimeMs < 0 || unixTimeMs > 0x0000FFFFFFFFFFFFL)
                throw new ArgumentOutOfRangeException(nameof(unixTimeMs), "Timestamp must fit within 48 bits.");
            ulong high = ((ulong)unixTimeMs << 16) | 0x7000UL;
            ulong low = 0x8000000000000000UL;
            return new Uuid7(high, low);
        }

        /// <summary>
        /// Returns the maximum possible <see cref="Uuid7"/> value for a given UTC millisecond timestamp (useful for primary-key range queries).
        /// </summary>
        public static Uuid7 MaxValueForTimestamp(long unixTimeMs)
        {
            if (unixTimeMs < 0 || unixTimeMs > 0x0000FFFFFFFFFFFFL)
                throw new ArgumentOutOfRangeException(nameof(unixTimeMs), "Timestamp must fit within 48 bits.");
            ulong high = ((ulong)unixTimeMs << 16) | 0x7FFFUL;
            ulong low = 0xBFFFFFFFFFFFFFFFUL;
            return new Uuid7(high, low);
        }

        /// <summary>
        /// Writes the 16 raw big-endian bytes into the destination span.
        /// </summary>
        public bool TryWriteBytes(Span<byte> destination)
        {
            if (destination.Length < 16) return false;
            BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(0, 8), _high);
            BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(8, 8), _low);
            return true;
        }

        public byte[] ToByteArray()
        {
            byte[] arr = new byte[16];
            TryWriteBytes(arr);
            return arr;
        }

        /// <summary>
        /// Formats this UUIDv7 into the destination span (supports 'D' hyphenated, 'N' 32 hex, 'B' braces, 'P' parentheses).
        /// </summary>
        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default)
        {
            char fmt = format.IsEmpty ? 'D' : char.ToUpperInvariant(format[0]);
            int requiredLength = (fmt == 'N') ? 32 : ((fmt == 'B' || fmt == 'P') ? 38 : 36);

            if (destination.Length < requiredLength)
            {
                charsWritten = 0;
                return false;
            }

            Span<byte> bytes = stackalloc byte[16];
            BinaryPrimitives.WriteUInt64BigEndian(bytes.Slice(0, 8), _high);
            BinaryPrimitives.WriteUInt64BigEndian(bytes.Slice(8, 8), _low);

            const string hexDigits = "0123456789abcdef";
            int dstIdx = 0;

            if (fmt == 'B') destination[dstIdx++] = '{';
            if (fmt == 'P') destination[dstIdx++] = '(';

            for (int i = 0; i < 16; i++)
            {
                if (fmt != 'N' && (i == 4 || i == 6 || i == 8 || i == 10))
                {
                    destination[dstIdx++] = '-';
                }
                byte b = bytes[i];
                destination[dstIdx++] = hexDigits[b >> 4];
                destination[dstIdx++] = hexDigits[b & 0x0F];
            }

            if (fmt == 'B') destination[dstIdx++] = '}';
            if (fmt == 'P') destination[dstIdx++] = ')';

            charsWritten = dstIdx;
            return true;
        }

        /// <summary>
        /// Formats this UUIDv7 as ASCII/UTF-8 bytes into <paramref name="destination"/> (supports 'D', 'N', 'B', 'P').
        /// </summary>
        public bool TryFormatUtf8(Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format = default)
        {
            char fmt = format.IsEmpty ? 'D' : char.ToUpperInvariant(format[0]);
            int requiredLength = (fmt == 'N') ? 32 : ((fmt == 'B' || fmt == 'P') ? 38 : 36);

            if (destination.Length < requiredLength)
            {
                bytesWritten = 0;
                return false;
            }

            Span<byte> bytes = stackalloc byte[16];
            BinaryPrimitives.WriteUInt64BigEndian(bytes.Slice(0, 8), _high);
            BinaryPrimitives.WriteUInt64BigEndian(bytes.Slice(8, 8), _low);

            const string hexDigits = "0123456789abcdef";
            int dstIdx = 0;

            if (fmt == 'B') destination[dstIdx++] = (byte)'{';
            if (fmt == 'P') destination[dstIdx++] = (byte)'(';

            for (int i = 0; i < 16; i++)
            {
                if (fmt != 'N' && (i == 4 || i == 6 || i == 8 || i == 10))
                {
                    destination[dstIdx++] = (byte)'-';
                }
                byte b = bytes[i];
                destination[dstIdx++] = (byte)hexDigits[b >> 4];
                destination[dstIdx++] = (byte)hexDigits[b & 0x0F];
            }

            if (fmt == 'B') destination[dstIdx++] = (byte)'}';
            if (fmt == 'P') destination[dstIdx++] = (byte)')';

            bytesWritten = dstIdx;
            return true;
        }

        public override unsafe string ToString()
        {
            Span<char> chars = stackalloc char[36];
            TryFormat(chars, out int written, "D".AsSpan());
#if NET8_0_OR_GREATER
            return new string(chars.Slice(0, written));
#else
            fixed (char* p = chars)
                return new string(p, 0, written);
#endif
        }

        public unsafe string ToString(string? format)
        {
            var fmtSpan = format.AsSpan();
            char fmt = fmtSpan.IsEmpty ? 'D' : char.ToUpperInvariant(fmtSpan[0]);
            int len = (fmt == 'N') ? 32 : ((fmt == 'B' || fmt == 'P') ? 38 : 36);
            Span<char> chars = stackalloc char[len];
            TryFormat(chars, out int written, fmtSpan);
#if NET8_0_OR_GREATER
            return new string(chars.Slice(0, written));
#else
            fixed (char* p = chars)
                return new string(p, 0, written);
#endif
        }

        public static bool TryParse(ReadOnlySpan<char> input, out Uuid7 result)
        {
            result = default;
            var trimmed = input.Trim();
            if (trimmed.Length == 38 && ((trimmed[0] == '{' && trimmed[37] == '}') || (trimmed[0] == '(' && trimmed[37] == ')')))
            {
                trimmed = trimmed.Slice(1, 36);
            }

            Span<byte> bytes = stackalloc byte[16];
            int byteIdx = 0;

            if (trimmed.Length == 36 && trimmed[8] == '-' && trimmed[13] == '-' && trimmed[18] == '-' && trimmed[23] == '-')
            {
                for (int i = 0; i < 36; i++)
                {
                    if (i == 8 || i == 13 || i == 18 || i == 23) continue;

                    int hi = FromHexChar(trimmed[i]);
                    int lo = FromHexChar(trimmed[++i]);
                    if (hi < 0 || lo < 0) return false;
                    bytes[byteIdx++] = (byte)((hi << 4) | lo);
                }
            }
            else if (trimmed.Length == 32)
            {
                for (int i = 0; i < 32; i += 2)
                {
                    int hi = FromHexChar(trimmed[i]);
                    int lo = FromHexChar(trimmed[i + 1]);
                    if (hi < 0 || lo < 0) return false;
                    bytes[byteIdx++] = (byte)((hi << 4) | lo);
                }
            }
            else
            {
                return false;
            }

            result = new Uuid7(bytes);
            return true;
        }

        /// <summary>
        /// Parses a UTF-8 byte span (formats 'D', 'N', 'B', or 'P') into a <see cref="Uuid7"/> without heap allocation.
        /// </summary>
        public static bool TryParse(ReadOnlySpan<byte> utf8Input, out Uuid7 result)
        {
            result = default;
            int start = 0;
            int end = utf8Input.Length - 1;
            while (start <= end && (utf8Input[start] == ' ' || utf8Input[start] == '\t' || utf8Input[start] == '\r' || utf8Input[start] == '\n'))
                start++;
            while (end >= start && (utf8Input[end] == ' ' || utf8Input[end] == '\t' || utf8Input[end] == '\r' || utf8Input[end] == '\n'))
                end--;

            var trimmed = start <= end ? utf8Input.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
            if (trimmed.Length == 38 && ((trimmed[0] == '{' && trimmed[37] == '}') || (trimmed[0] == '(' && trimmed[37] == ')')))
            {
                trimmed = trimmed.Slice(1, 36);
            }

            Span<byte> bytes = stackalloc byte[16];
            int byteIdx = 0;

            if (trimmed.Length == 36 && trimmed[8] == '-' && trimmed[13] == '-' && trimmed[18] == '-' && trimmed[23] == '-')
            {
                for (int i = 0; i < 36; i++)
                {
                    if (i == 8 || i == 13 || i == 18 || i == 23) continue;

                    int hi = FromHexByte(trimmed[i]);
                    int lo = FromHexByte(trimmed[++i]);
                    if (hi < 0 || lo < 0) return false;
                    bytes[byteIdx++] = (byte)((hi << 4) | lo);
                }
            }
            else if (trimmed.Length == 32)
            {
                for (int i = 0; i < 32; i += 2)
                {
                    int hi = FromHexByte(trimmed[i]);
                    int lo = FromHexByte(trimmed[i + 1]);
                    if (hi < 0 || lo < 0) return false;
                    bytes[byteIdx++] = (byte)((hi << 4) | lo);
                }
            }
            else
            {
                return false;
            }

            result = new Uuid7(bytes);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FromHexByte(byte b)
        {
            if (b >= (byte)'0' && b <= (byte)'9') return b - '0';
            if (b >= (byte)'a' && b <= (byte)'f') return b - 'a' + 10;
            if (b >= (byte)'A' && b <= (byte)'F') return b - 'A' + 10;
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FromHexChar(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        public bool Equals(Uuid7 other) => _high == other._high && _low == other._low;
        public override bool Equals(object? obj) => obj is Uuid7 other && Equals(other);
        public override int GetHashCode() => unchecked((int)(_high ^ (_high >> 32) ^ _low ^ (_low >> 32)));

        public int CompareTo(Uuid7 other)
        {
            int cmp = _high.CompareTo(other._high);
            if (cmp != 0) return cmp;
            return _low.CompareTo(other._low);
        }

        public static bool operator ==(Uuid7 left, Uuid7 right) => left.Equals(right);
        public static bool operator !=(Uuid7 left, Uuid7 right) => !left.Equals(right);
        public static bool operator <(Uuid7 left, Uuid7 right) => left.CompareTo(right) < 0;
        public static bool operator <=(Uuid7 left, Uuid7 right) => left.CompareTo(right) <= 0;
        public static bool operator >(Uuid7 left, Uuid7 right) => left.CompareTo(right) > 0;
        public static bool operator >=(Uuid7 left, Uuid7 right) => left.CompareTo(right) >= 0;

        public static explicit operator Guid(Uuid7 u) => u.ToGuid();
        public static explicit operator Uuid7(Guid g) => new Uuid7(g);
    }
}
