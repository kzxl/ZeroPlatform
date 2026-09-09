using System;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Text
{
    /// <summary>
    /// Ultra-fast, stack-allocated, mutable character buffer.
    /// Provides zero-allocation string building with automatic ArrayPool spillover.
    /// </summary>
    public ref struct ValueStringBuilder
    {
        private Span<char> _chars;
        private char[]? _arrayToReturnToPool;
        private int _pos;

        public ValueStringBuilder(Span<char> initialBuffer)
        {
            _arrayToReturnToPool = null;
            _chars = initialBuffer;
            _pos = 0;
        }

        public ValueStringBuilder(int initialCapacity)
        {
            _arrayToReturnToPool = ArrayPool<char>.Shared.Rent(initialCapacity);
            _chars = _arrayToReturnToPool;
            _pos = 0;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pos;
            set
            {
                if (value < 0 || value > _chars.Length)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _pos = value;
            }
        }

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _chars.Length;
        }

        public ref char this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _chars[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<char> AsSpan() => _chars.Slice(0, _pos);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<char> AsSpan(int start, int length) => _chars.Slice(start, length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(char c)
        {
            int pos = _pos;
            if ((uint)pos < (uint)_chars.Length)
            {
                _chars[pos] = c;
                _pos = pos + 1;
            }
            else
            {
                GrowAndAppend(c);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(string? s)
        {
            if (s == null) return;
            Append(s.AsSpan());
        }

        public void Append(ReadOnlySpan<char> value)
        {
            int pos = _pos;
            if (pos > _chars.Length - value.Length)
            {
                Grow(value.Length);
            }

            value.CopyTo(_chars.Slice(_pos));
            _pos += value.Length;
        }

        public void Append(int value)
        {
#if NET8_0_OR_GREATER
            Span<char> dest = _chars.Slice(_pos);
            if (value.TryFormat(dest, out int charsWritten, default, CultureInfo.InvariantCulture))
            {
                _pos += charsWritten;
                return;
            }
            Grow(16);
            value.TryFormat(_chars.Slice(_pos), out charsWritten, default, CultureInfo.InvariantCulture);
            _pos += charsWritten;
#else
            Append(value.ToString(CultureInfo.InvariantCulture));
#endif
        }

        public void Append(long value)
        {
#if NET8_0_OR_GREATER
            Span<char> dest = _chars.Slice(_pos);
            if (value.TryFormat(dest, out int charsWritten, default, CultureInfo.InvariantCulture))
            {
                _pos += charsWritten;
                return;
            }
            Grow(32);
            value.TryFormat(_chars.Slice(_pos), out charsWritten, default, CultureInfo.InvariantCulture);
            _pos += charsWritten;
#else
            Append(value.ToString(CultureInfo.InvariantCulture));
#endif
        }

        public void Append(decimal value)
        {
#if NET8_0_OR_GREATER
            Span<char> dest = _chars.Slice(_pos);
            if (value.TryFormat(dest, out int charsWritten, default, CultureInfo.InvariantCulture))
            {
                _pos += charsWritten;
                return;
            }
            Grow(40);
            value.TryFormat(_chars.Slice(_pos), out charsWritten, default, CultureInfo.InvariantCulture);
            _pos += charsWritten;
#else
            Append(value.ToString(CultureInfo.InvariantCulture));
#endif
        }

        public void Append(double value)
        {
#if NET8_0_OR_GREATER
            Span<char> dest = _chars.Slice(_pos);
            if (value.TryFormat(dest, out int charsWritten, default, CultureInfo.InvariantCulture))
            {
                _pos += charsWritten;
                return;
            }
            Grow(32);
            value.TryFormat(_chars.Slice(_pos), out charsWritten, default, CultureInfo.InvariantCulture);
            _pos += charsWritten;
#else
            Append(value.ToString(CultureInfo.InvariantCulture));
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(bool value)
        {
            Append(value ? "True" : "False");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendLine()
        {
            Append(Environment.NewLine);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendLine(string? s)
        {
            Append(s);
            AppendLine();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendLine(ReadOnlySpan<char> span)
        {
            Append(span);
            AppendLine();
        }

        public Span<char> AppendSpan(int length)
        {
            int origPos = _pos;
            if (origPos > _chars.Length - length)
            {
                Grow(length);
            }

            _pos = origPos + length;
            return _chars.Slice(origPos, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => _pos = 0;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void GrowAndAppend(char c)
        {
            Grow(1);
            Append(c);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Grow(int additionalCapacityBeyondPos)
        {
            int requiredCapacity = checked(_pos + additionalCapacityBeyondPos);
            int newCapacity = Math.Max(requiredCapacity, Math.Max(_chars.Length * 2, 64));

            char[] poolArray = ArrayPool<char>.Shared.Rent(newCapacity);
            _chars.Slice(0, _pos).CopyTo(poolArray);

            char[]? toReturn = _arrayToReturnToPool;
            _chars = _arrayToReturnToPool = poolArray;

            if (toReturn != null)
            {
                ArrayPool<char>.Shared.Return(toReturn);
            }
        }

        public override string ToString() => AsSpan().ToString();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            char[]? toReturn = _arrayToReturnToPool;
            this = default;
            if (toReturn != null)
            {
                ArrayPool<char>.Shared.Return(toReturn);
            }
        }
    }
}
