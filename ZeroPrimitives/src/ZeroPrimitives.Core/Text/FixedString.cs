using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ZeroPrimitives.Text
{
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
                Span<byte> dest = new Span<byte>(ptr, MaxByteCapacity);
#if NET8_0_OR_GREATER
                if (!Encoding.UTF8.TryGetBytes(chars, dest, out int written))
                {
                    written = Encoding.UTF8.GetBytes(chars.Slice(0, Math.Min(chars.Length, MaxByteCapacity)), dest);
                }
                _length = (byte)written;
#else
                fixed (char* cPtr = chars)
                {
                    int len = Math.Min(chars.Length, MaxByteCapacity);
                    int written = Encoding.UTF8.GetBytes(cPtr, len, ptr, MaxByteCapacity);
                    _length = (byte)written;
                }
#endif
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
            if (_length == 0) return string.Empty;
            fixed (byte* ptr = _bytes)
            {
                return Encoding.UTF8.GetString(ptr, _length);
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
                Span<byte> dest = new Span<byte>(ptr, MaxByteCapacity);
#if NET8_0_OR_GREATER
                if (!Encoding.UTF8.TryGetBytes(chars, dest, out int written))
                {
                    written = Encoding.UTF8.GetBytes(chars.Slice(0, Math.Min(chars.Length, MaxByteCapacity)), dest);
                }
                _length = (byte)written;
#else
                fixed (char* cPtr = chars)
                {
                    int len = Math.Min(chars.Length, MaxByteCapacity);
                    int written = Encoding.UTF8.GetBytes(cPtr, len, ptr, MaxByteCapacity);
                    _length = (byte)written;
                }
#endif
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
            if (_length == 0) return string.Empty;
            fixed (byte* ptr = _bytes)
            {
                return Encoding.UTF8.GetString(ptr, _length);
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
}
