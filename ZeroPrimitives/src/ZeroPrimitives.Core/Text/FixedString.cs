using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ZeroPrimitives.Text
{
    internal static unsafe class FixedStringUtf8Helper
    {
        public static int EncodeUtf8Truncated(ReadOnlySpan<char> chars, byte* dest, int maxBytes, out bool truncated)
        {
            truncated = false;
            if (chars.IsEmpty) return 0;

            if (chars.Length * 3 <= maxBytes)
            {
#if NET8_0_OR_GREATER
                return Encoding.UTF8.GetBytes(chars, new Span<byte>(dest, maxBytes));
#else
                fixed (char* cPtr = chars)
                {
                    return Encoding.UTF8.GetBytes(cPtr, chars.Length, dest, maxBytes);
                }
#endif
            }

            int written = 0;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (c < 0x80)
                {
                    if (written + 1 > maxBytes) { truncated = true; break; }
                    dest[written++] = (byte)c;
                }
                else if (c < 0x800)
                {
                    if (written + 2 > maxBytes) { truncated = true; break; }
                    dest[written++] = (byte)(0xC0 | (c >> 6));
                    dest[written++] = (byte)(0x80 | (c & 0x3F));
                }
                else if (char.IsHighSurrogate(c) && i + 1 < chars.Length && char.IsLowSurrogate(chars[i + 1]))
                {
                    if (written + 4 > maxBytes) { truncated = true; break; }
                    int cp = char.ConvertToUtf32(c, chars[i + 1]);
                    i++;
                    dest[written++] = (byte)(0xF0 | (cp >> 18));
                    dest[written++] = (byte)(0x80 | ((cp >> 12) & 0x3F));
                    dest[written++] = (byte)(0x80 | ((cp >> 6) & 0x3F));
                    dest[written++] = (byte)(0x80 | (cp & 0x3F));
                }
                else if (char.IsSurrogate(c))
                {
                    if (written + 3 > maxBytes) { truncated = true; break; }
                    dest[written++] = 0xEF;
                    dest[written++] = 0xBF;
                    dest[written++] = 0xBD;
                }
                else
                {
                    if (written + 3 > maxBytes) { truncated = true; break; }
                    dest[written++] = (byte)(0xE0 | (c >> 12));
                    dest[written++] = (byte)(0x80 | ((c >> 6) & 0x3F));
                    dest[written++] = (byte)(0x80 | (c & 0x3F));
                }
            }

            return written;
        }

        public static int CopyUtf8Truncated(ReadOnlySpan<byte> utf8, byte* dest, int maxBytes, out bool truncated)
        {
            if (utf8.IsEmpty)
            {
                truncated = false;
                return 0;
            }

            if (utf8.Length <= maxBytes)
            {
                utf8.CopyTo(new Span<byte>(dest, maxBytes));
                truncated = false;
                return utf8.Length;
            }

            truncated = true;
            int len = maxBytes;

            // Back up if truncation lands in the middle of a multi-byte UTF-8 sequence
            while (len > 0 && (utf8[len] & 0xC0) == 0x80)
            {
                len--;
            }

            // Guard against incomplete leading sequence at the end of sliced window
            if (len > 0 && utf8[len - 1] >= 0xC0)
            {
                int seqLen = GetUtf8SequenceLength(utf8[len - 1]);
                if (seqLen > 1) len--;
            }

            if (len > 0)
            {
                utf8.Slice(0, len).CopyTo(new Span<byte>(dest, maxBytes));
            }
            return len;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetUtf8SequenceLength(byte leadByte)
        {
            if (leadByte < 0x80) return 1;
            if ((leadByte & 0xE0) == 0xC0) return 2;
            if ((leadByte & 0xF0) == 0xE0) return 3;
            if ((leadByte & 0xF8) == 0xF0) return 4;
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string DecodeToString(byte* ptr, int length)
        {
            if (length == 0) return string.Empty;
            return Encoding.UTF8.GetString(ptr, length);
        }
    }

    /// <summary>
    /// Blittable, 32-byte fixed UTF-8 string stored inline on stack or unmanaged memory with 0 heap allocation.
    /// Capacity: up to 31 UTF-8 bytes (1 byte reserved for length).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public unsafe struct FixedString32 : IEquatable<FixedString32>, IComparable<FixedString32>
    {
        public const int MaxByteCapacity = 31;
        private byte _length;
        private fixed byte _bytes[MaxByteCapacity];

        public readonly int Length => _length;
        public readonly bool IsEmpty => _length == 0;

        public FixedString32(ReadOnlySpan<char> chars)
        {
            _length = 0;
            if (chars.IsEmpty) return;

            fixed (byte* ptr = _bytes)
            {
                _length = (byte)FixedStringUtf8Helper.EncodeUtf8Truncated(chars, ptr, MaxByteCapacity, out _);
            }
        }

        /// <summary>
        /// Attempts to encode <paramref name="chars"/> into a <see cref="FixedString32"/> without truncation.
        /// Returns <c>true</c> if the entire string fit; otherwise <c>false</c> with <paramref name="result"/> safely truncated on a UTF-8 rune boundary.
        /// </summary>
        public static bool TryCreate(ReadOnlySpan<char> chars, out FixedString32 result)
        {
            result = default;
            if (chars.IsEmpty) return true;

            fixed (byte* ptr = result._bytes)
            {
                result._length = (byte)FixedStringUtf8Helper.EncodeUtf8Truncated(chars, ptr, MaxByteCapacity, out bool truncated);
                return !truncated;
            }
        }

        /// <summary>
        /// Attempts to copy <paramref name="utf8Bytes"/> into a <see cref="FixedString32"/> without truncation.
        /// Returns <c>true</c> if all bytes fit; otherwise <c>false</c> with <paramref name="result"/> safely truncated on a UTF-8 rune boundary.
        /// </summary>
        public static bool TryCreate(ReadOnlySpan<byte> utf8Bytes, out FixedString32 result)
        {
            result = default;
            if (utf8Bytes.IsEmpty) return true;

            fixed (byte* ptr = result._bytes)
            {
                result._length = (byte)FixedStringUtf8Helper.CopyUtf8Truncated(utf8Bytes, ptr, MaxByteCapacity, out bool truncated);
                return !truncated;
            }
        }

        public readonly ReadOnlySpan<byte> AsSpan()
        {
            fixed (byte* ptr = _bytes)
            {
                return new ReadOnlySpan<byte>(ptr, _length);
            }
        }

        public override readonly string ToString()
        {
            fixed (byte* ptr = _bytes)
            {
                return FixedStringUtf8Helper.DecodeToString(ptr, _length);
            }
        }

        public readonly bool Equals(FixedString32 other)
        {
            if (_length != other._length) return false;
            return AsSpan().SequenceEqual(other.AsSpan());
        }

        public override readonly bool Equals(object? obj) => obj is FixedString32 other && Equals(other);

        public override readonly int GetHashCode()
        {
            return (int)ZeroPrimitives.Cryptography.FastHash.Fnv1a32(AsSpan());
        }

        public readonly int CompareTo(FixedString32 other) => AsSpan().SequenceCompareTo(other.AsSpan());

        public static bool operator ==(FixedString32 left, FixedString32 right) => left.Equals(right);
        public static bool operator !=(FixedString32 left, FixedString32 right) => !left.Equals(right);

        public static implicit operator FixedString32(string? value) => new FixedString32(value.AsSpan());
    }

    /// <summary>
    /// Blittable, 64-byte fixed UTF-8 string stored inline on stack or unmanaged memory with 0 heap allocation.
    /// Capacity: up to 63 UTF-8 bytes (1 byte reserved for length).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public unsafe struct FixedString64 : IEquatable<FixedString64>, IComparable<FixedString64>
    {
        public const int MaxByteCapacity = 63;
        private byte _length;
        private fixed byte _bytes[MaxByteCapacity];

        public readonly int Length => _length;
        public readonly bool IsEmpty => _length == 0;

        public FixedString64(ReadOnlySpan<char> chars)
        {
            _length = 0;
            if (chars.IsEmpty) return;

            fixed (byte* ptr = _bytes)
            {
                _length = (byte)FixedStringUtf8Helper.EncodeUtf8Truncated(chars, ptr, MaxByteCapacity, out _);
            }
        }

        /// <summary>
        /// Attempts to encode <paramref name="chars"/> into a <see cref="FixedString64"/> without truncation.
        /// Returns <c>true</c> if the entire string fit; otherwise <c>false</c> with <paramref name="result"/> safely truncated on a UTF-8 rune boundary.
        /// </summary>
        public static bool TryCreate(ReadOnlySpan<char> chars, out FixedString64 result)
        {
            result = default;
            if (chars.IsEmpty) return true;

            fixed (byte* ptr = result._bytes)
            {
                result._length = (byte)FixedStringUtf8Helper.EncodeUtf8Truncated(chars, ptr, MaxByteCapacity, out bool truncated);
                return !truncated;
            }
        }

        /// <summary>
        /// Attempts to copy <paramref name="utf8Bytes"/> into a <see cref="FixedString64"/> without truncation.
        /// Returns <c>true</c> if all bytes fit; otherwise <c>false</c> with <paramref name="result"/> safely truncated on a UTF-8 rune boundary.
        /// </summary>
        public static bool TryCreate(ReadOnlySpan<byte> utf8Bytes, out FixedString64 result)
        {
            result = default;
            if (utf8Bytes.IsEmpty) return true;

            fixed (byte* ptr = result._bytes)
            {
                result._length = (byte)FixedStringUtf8Helper.CopyUtf8Truncated(utf8Bytes, ptr, MaxByteCapacity, out bool truncated);
                return !truncated;
            }
        }

        public readonly ReadOnlySpan<byte> AsSpan()
        {
            fixed (byte* ptr = _bytes)
            {
                return new ReadOnlySpan<byte>(ptr, _length);
            }
        }

        public override readonly string ToString()
        {
            fixed (byte* ptr = _bytes)
            {
                return FixedStringUtf8Helper.DecodeToString(ptr, _length);
            }
        }

        public readonly bool Equals(FixedString64 other)
        {
            if (_length != other._length) return false;
            return AsSpan().SequenceEqual(other.AsSpan());
        }

        public override readonly bool Equals(object? obj) => obj is FixedString64 other && Equals(other);

        public override readonly int GetHashCode()
        {
            return (int)ZeroPrimitives.Cryptography.FastHash.Fnv1a32(AsSpan());
        }

        public readonly int CompareTo(FixedString64 other) => AsSpan().SequenceCompareTo(other.AsSpan());

        public static bool operator ==(FixedString64 left, FixedString64 right) => left.Equals(right);
        public static bool operator !=(FixedString64 left, FixedString64 right) => !left.Equals(right);

        public static implicit operator FixedString64(string? value) => new FixedString64(value.AsSpan());
    }

    /// <summary>
    /// Blittable, 128-byte fixed UTF-8 string stored inline on stack or unmanaged memory with 0 heap allocation.
    /// Capacity: up to 127 UTF-8 bytes (1 byte reserved for length).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 128)]
    public unsafe struct FixedString128 : IEquatable<FixedString128>, IComparable<FixedString128>
    {
        public const int MaxByteCapacity = 127;
        private byte _length;
        private fixed byte _bytes[MaxByteCapacity];

        public readonly int Length => _length;
        public readonly bool IsEmpty => _length == 0;

        public FixedString128(ReadOnlySpan<char> chars)
        {
            _length = 0;
            if (chars.IsEmpty) return;

            fixed (byte* ptr = _bytes)
            {
                _length = (byte)FixedStringUtf8Helper.EncodeUtf8Truncated(chars, ptr, MaxByteCapacity, out _);
            }
        }

        /// <summary>
        /// Attempts to encode <paramref name="chars"/> into a <see cref="FixedString128"/> without truncation.
        /// Returns <c>true</c> if the entire string fit; otherwise <c>false</c> with <paramref name="result"/> safely truncated on a UTF-8 rune boundary.
        /// </summary>
        public static bool TryCreate(ReadOnlySpan<char> chars, out FixedString128 result)
        {
            result = default;
            if (chars.IsEmpty) return true;

            fixed (byte* ptr = result._bytes)
            {
                result._length = (byte)FixedStringUtf8Helper.EncodeUtf8Truncated(chars, ptr, MaxByteCapacity, out bool truncated);
                return !truncated;
            }
        }

        /// <summary>
        /// Attempts to copy <paramref name="utf8Bytes"/> into a <see cref="FixedString128"/> without truncation.
        /// Returns <c>true</c> if all bytes fit; otherwise <c>false</c> with <paramref name="result"/> safely truncated on a UTF-8 rune boundary.
        /// </summary>
        public static bool TryCreate(ReadOnlySpan<byte> utf8Bytes, out FixedString128 result)
        {
            result = default;
            if (utf8Bytes.IsEmpty) return true;

            fixed (byte* ptr = result._bytes)
            {
                result._length = (byte)FixedStringUtf8Helper.CopyUtf8Truncated(utf8Bytes, ptr, MaxByteCapacity, out bool truncated);
                return !truncated;
            }
        }

        public readonly ReadOnlySpan<byte> AsSpan()
        {
            fixed (byte* ptr = _bytes)
            {
                return new ReadOnlySpan<byte>(ptr, _length);
            }
        }

        public override readonly string ToString()
        {
            fixed (byte* ptr = _bytes)
            {
                return FixedStringUtf8Helper.DecodeToString(ptr, _length);
            }
        }

        public readonly bool Equals(FixedString128 other)
        {
            if (_length != other._length) return false;
            return AsSpan().SequenceEqual(other.AsSpan());
        }

        public override readonly bool Equals(object? obj) => obj is FixedString128 other && Equals(other);

        public override readonly int GetHashCode()
        {
            return (int)ZeroPrimitives.Cryptography.FastHash.Fnv1a32(AsSpan());
        }

        public readonly int CompareTo(FixedString128 other) => AsSpan().SequenceCompareTo(other.AsSpan());

        public static bool operator ==(FixedString128 left, FixedString128 right) => left.Equals(right);
        public static bool operator !=(FixedString128 left, FixedString128 right) => !left.Equals(right);

        public static implicit operator FixedString128(string? value) => new FixedString128(value.AsSpan());
    }
}
