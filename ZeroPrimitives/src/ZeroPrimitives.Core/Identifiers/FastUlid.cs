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
    /// Represents an ultra-fast, zero-allocation Universally Unique Lexicographically Sortable Identifier (ULID).
    /// Conforms to the ULID specification with 48-bit millisecond timestamp and 80-bit cryptographic randomness.
    /// Serializes to 26 Crockford Base32 characters and integrates seamlessly with Guid and Uuid7.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public readonly struct FastUlid : IEquatable<FastUlid>, IComparable<FastUlid>
    {
        // 16-byte raw data representation (Big-Endian network order)
        [FieldOffset(0)] private readonly ulong _high; // 48-bit timestamp + 16-bit random high
        [FieldOffset(8)] private readonly ulong _low;  // 64-bit random low

        // Crockford Base32 alphabet: 32 characters (excludes I, L, O, U)
        private const string Base32Digits = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        // Reverse lookup table for Base32 character to 5-bit integer
        private static readonly byte[] Base32Lookup = CreateBase32Lookup();

        // Monotonic generator state
        private static long _lastUnixMs;
        private static ulong _lastRandHigh;
        private static ulong _lastRandLow;
        private static readonly object _syncLock = new object();

        [ThreadStatic]
        private static RandomNumberGenerator? _rng;

        [ThreadStatic]
        private static byte[]? _rngBuffer;

        private static RandomNumberGenerator Rng => _rng ??= RandomNumberGenerator.Create();

        public static readonly FastUlid Empty = default;

        public FastUlid(ulong high, ulong low)
        {
            _high = high;
            _low = low;
        }

        public FastUlid(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 16)
                throw new ArgumentException("Byte span must contain at least 16 bytes.", nameof(bytes));

            _high = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(0, 8));
            _low = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(8, 8));
        }

        public FastUlid(Guid guid)
        {
            Span<byte> bytes = stackalloc byte[16];
#if NET8_0_OR_GREATER
            guid.TryWriteBytes(bytes);
#else
            byte[] b = guid.ToByteArray();
            b.AsSpan().CopyTo(bytes);
#endif
            if (BitConverter.IsLittleEndian)
            {
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

        public FastUlid(Uuid7 uuid7)
        {
            Span<byte> bytes = stackalloc byte[16];
            uuid7.TryWriteBytes(bytes);
            _high = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(0, 8));
            _low = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(8, 8));
        }

        /// <summary>
        /// Generates a new monotonically ordered ULID at the current system time.
        /// </summary>
        public static FastUlid NewUlid()
        {
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return Create(nowMs);
        }

        /// <summary>
        /// Generates a monotonically ordered ULID for a specific UTC millisecond timestamp.
        /// </summary>
        public static FastUlid Create(long unixTimeMs)
        {
            if (unixTimeMs < 0 || unixTimeMs > 0x0000FFFFFFFFFFFFL)
                throw new ArgumentOutOfRangeException(nameof(unixTimeMs), "Timestamp must fit within 48 bits.");

#if NET8_0_OR_GREATER
            Span<byte> rand = stackalloc byte[10];
            Rng.GetBytes(rand);
#else
            byte[] buf = _rngBuffer ??= new byte[16];
            Rng.GetBytes(buf);
            ReadOnlySpan<byte> rand = buf.AsSpan(0, 10);
#endif

            ulong randHigh16 = BinaryPrimitives.ReadUInt16BigEndian(rand.Slice(0, 2));
            ulong randLow64 = BinaryPrimitives.ReadUInt64BigEndian(rand.Slice(2, 8));

            ulong finalHigh;
            ulong finalLow;

            lock (_syncLock)
            {
                if (unixTimeMs > _lastUnixMs)
                {
                    _lastUnixMs = unixTimeMs;
                    _lastRandHigh = randHigh16;
                    _lastRandLow = randLow64;
                }
                else
                {
                    // Clock within same millisecond or clock jittered backwards under thread preemption:
                    // Pin to _lastUnixMs and increment 80-bit random counter monotonically
                    unixTimeMs = _lastUnixMs;
                    if (_lastRandLow == ulong.MaxValue)
                    {
                        _lastRandLow = 0;
                        if (_lastRandHigh == 0xFFFF)
                        {
                            // Overflowed 80 bits in 1ms: spin until next millisecond
                            while (unixTimeMs <= _lastUnixMs)
                            {
                                unixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            }
                            _lastUnixMs = unixTimeMs;
                            _lastRandHigh = randHigh16;
                            _lastRandLow = randLow64;
                        }
                        else
                        {
                            _lastRandHigh++;
                        }
                    }
                    else
                    {
                        _lastRandLow++;
                    }
                }

                finalHigh = ((ulong)_lastUnixMs << 16) | (_lastRandHigh & 0xFFFF);
                finalLow = _lastRandLow;
            }

            return new FastUlid(finalHigh, finalLow);
        }

        /// <summary>
        /// Extracts the 48-bit UTC Unix millisecond timestamp.
        /// </summary>
        public long GetUnixTimeMilliseconds() => (long)(_high >> 16);

        /// <summary>
        /// Extracts the timestamp as a DateTimeOffset.
        /// </summary>
        public DateTimeOffset GetDateTimeOffset() => DateTimeOffset.FromUnixTimeMilliseconds(GetUnixTimeMilliseconds());

        /// <summary>
        /// Writes 16 raw big-endian bytes into the destination span.
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
        /// Converts the ULID to a standard Guid.
        /// </summary>
        public Guid ToGuid()
        {
            Span<byte> bytes = stackalloc byte[16];
            BinaryPrimitives.WriteUInt64BigEndian(bytes.Slice(0, 8), _high);
            BinaryPrimitives.WriteUInt64BigEndian(bytes.Slice(8, 8), _low);

            if (BitConverter.IsLittleEndian)
            {
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
        /// Converts the ULID to an RFC 9562 Uuid7 structure.
        /// </summary>
        public Uuid7 ToUuid7()
        {
            Span<byte> bytes = stackalloc byte[16];
            TryWriteBytes(bytes);
            return new Uuid7(bytes);
        }

        /// <summary>
        /// Formats this ULID into 26 Crockford Base32 characters in the destination span with 0 heap allocation.
        /// </summary>
        public bool TryFormat(Span<char> destination, out int charsWritten)
        {
            if (destination.Length < 26)
            {
                charsWritten = 0;
                return false;
            }

            Span<byte> b = stackalloc byte[16];
            BinaryPrimitives.WriteUInt64BigEndian(b.Slice(0, 8), _high);
            BinaryPrimitives.WriteUInt64BigEndian(b.Slice(8, 8), _low);

            // Crockford Base32 26 characters
            // Byte 0: 3 bits (char 0), 5 bits (char 1)
            destination[0] = Base32Digits[(b[0] >> 5) & 7];
            destination[1] = Base32Digits[b[0] & 31];

            // Bytes 1..5 -> Chars 2..9 (8 chars = 40 bits)
            Encode5Bytes(b.Slice(1, 5), destination.Slice(2, 8));

            // Bytes 6..10 -> Chars 10..17 (8 chars = 40 bits)
            Encode5Bytes(b.Slice(6, 5), destination.Slice(10, 8));

            // Bytes 11..15 -> Chars 18..25 (8 chars = 40 bits)
            Encode5Bytes(b.Slice(11, 5), destination.Slice(18, 8));

            charsWritten = 26;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Encode5Bytes(ReadOnlySpan<byte> src, Span<char> dst)
        {
            byte a = src[0];
            byte b = src[1];
            byte c = src[2];
            byte d = src[3];
            byte e = src[4];

            dst[0] = Base32Digits[(a >> 3) & 31];
            dst[1] = Base32Digits[((a & 7) << 2) | ((b >> 6) & 3)];
            dst[2] = Base32Digits[(b >> 1) & 31];
            dst[3] = Base32Digits[((b & 1) << 4) | ((c >> 4) & 15)];
            dst[4] = Base32Digits[((c & 15) << 1) | ((d >> 7) & 1)];
            dst[5] = Base32Digits[(d >> 2) & 31];
            dst[6] = Base32Digits[((d & 3) << 3) | ((e >> 5) & 7)];
            dst[7] = Base32Digits[e & 31];
        }

        /// <summary>
        /// Attempts to parse 26 Crockford Base32 characters into a FastUlid.
        /// </summary>
        public static bool TryParse(ReadOnlySpan<char> input, out FastUlid result)
        {
            if (input.Length != 26)
            {
                result = default;
                return false;
            }

            Span<byte> b = stackalloc byte[16];

            byte c0 = DecodeChar(input[0]);
            byte c1 = DecodeChar(input[1]);

            // c0 only contains 3 bits (must be <= 7 to avoid 128-bit overflow)
            if (c0 > 7 || c1 == 0xFF)
            {
                result = default;
                return false;
            }

            b[0] = (byte)((c0 << 5) | c1);

            if (!Decode5Bytes(input.Slice(2, 8), b.Slice(1, 5)) ||
                !Decode5Bytes(input.Slice(10, 8), b.Slice(6, 5)) ||
                !Decode5Bytes(input.Slice(18, 8), b.Slice(11, 5)))
            {
                result = default;
                return false;
            }

            result = new FastUlid(b);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool Decode5Bytes(ReadOnlySpan<char> src, Span<byte> dst)
        {
            byte c0 = DecodeChar(src[0]);
            byte c1 = DecodeChar(src[1]);
            byte c2 = DecodeChar(src[2]);
            byte c3 = DecodeChar(src[3]);
            byte c4 = DecodeChar(src[4]);
            byte c5 = DecodeChar(src[5]);
            byte c6 = DecodeChar(src[6]);
            byte c7 = DecodeChar(src[7]);

            if ((c0 | c1 | c2 | c3 | c4 | c5 | c6 | c7) == 0xFF ||
                c0 == 0xFF || c1 == 0xFF || c2 == 0xFF || c3 == 0xFF ||
                c4 == 0xFF || c5 == 0xFF || c6 == 0xFF || c7 == 0xFF)
            {
                return false;
            }

            dst[0] = (byte)((c0 << 3) | (c1 >> 2));
            dst[1] = (byte)((c1 << 6) | (c2 << 1) | (c3 >> 4));
            dst[2] = (byte)((c3 << 4) | (c4 >> 1));
            dst[3] = (byte)((c4 << 7) | (c5 << 2) | (c6 >> 3));
            dst[4] = (byte)((c6 << 5) | c7);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte DecodeChar(char c)
        {
            return (uint)c < 128 ? Base32Lookup[c] : (byte)0xFF;
        }

        public static FastUlid Parse(ReadOnlySpan<char> input)
        {
            if (!TryParse(input, out FastUlid ulid))
                throw new FormatException($"Input string '{input.ToString()}' was not in a valid Crockford Base32 ULID format.");
            return ulid;
        }

        public override string ToString()
        {
            Span<char> buffer = stackalloc char[26];
            TryFormat(buffer, out _);
#if NET8_0_OR_GREATER
            return new string(buffer);
#else
            return buffer.ToString();
#endif
        }

        public static explicit operator Guid(FastUlid u) => u.ToGuid();
        public static explicit operator FastUlid(Guid g) => new FastUlid(g);
        public static explicit operator Uuid7(FastUlid u) => u.ToUuid7();
        public static explicit operator FastUlid(Uuid7 u) => new FastUlid(u);

        public override bool Equals(object? obj) => obj is FastUlid other && Equals(other);

        public bool Equals(FastUlid other) => _high == other._high && _low == other._low;

        public override int GetHashCode()
        {
#if NET8_0_OR_GREATER
            return HashCode.Combine(_high, _low);
#else
            unchecked
            {
                return (_high.GetHashCode() * 397) ^ _low.GetHashCode();
            }
#endif
        }

        public int CompareTo(FastUlid other)
        {
            int highCmp = _high.CompareTo(other._high);
            return highCmp != 0 ? highCmp : _low.CompareTo(other._low);
        }

        public static bool operator ==(FastUlid left, FastUlid right) => left.Equals(right);
        public static bool operator !=(FastUlid left, FastUlid right) => !left.Equals(right);
        public static bool operator <(FastUlid left, FastUlid right) => left.CompareTo(right) < 0;
        public static bool operator <=(FastUlid left, FastUlid right) => left.CompareTo(right) <= 0;
        public static bool operator >(FastUlid left, FastUlid right) => left.CompareTo(right) > 0;
        public static bool operator >=(FastUlid left, FastUlid right) => left.CompareTo(right) >= 0;

        private static byte[] CreateBase32Lookup()
        {
            byte[] table = new byte[128];
            for (int i = 0; i < table.Length; i++)
            {
                table[i] = 0xFF; // Invalid marker
            }

            for (byte i = 0; i < Base32Digits.Length; i++)
            {
                char c = Base32Digits[i];
                table[c] = i;
                if (char.IsLetter(c))
                {
                    table[char.ToLowerInvariant(c)] = i;
                }
            }

            // Crockford Base32 aliases
            table['o'] = 0;
            table['O'] = 0;
            table['i'] = 1;
            table['I'] = 1;
            table['l'] = 1;
            table['L'] = 1;

            return table;
        }
    }
}
