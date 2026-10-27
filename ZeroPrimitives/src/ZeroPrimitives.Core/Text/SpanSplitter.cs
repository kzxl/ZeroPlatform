using System;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Text
{
    /// <summary>
    /// High-performance, zero-allocation enumerator for splitting character spans.
    /// Replaces string.Split() with an allocation-free ref struct.
    /// </summary>
    public ref struct SpanSplitter
    {
        private ReadOnlySpan<char> _span;
        private ReadOnlySpan<char> _current;
        private readonly char _separator;
        private readonly bool _removeEmpty;
        private bool _finished;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SpanSplitter(ReadOnlySpan<char> span, char separator, bool removeEmpty = false)
        {
            _span = span;
            _separator = separator;
            _removeEmpty = removeEmpty;
            _current = default;
            _finished = false;
        }

        public readonly ReadOnlySpan<char> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _current;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly SpanSplitter GetEnumerator() => this;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (!_finished)
            {
                int index = _span.IndexOf(_separator);
                if (index < 0)
                {
                    _finished = true;
                    _current = _span;
                    if (_removeEmpty && _current.IsEmpty)
                    {
                        return false;
                    }
                    return true;
                }

                _current = _span.Slice(0, index);
                _span = _span.Slice(index + 1);

                if (_removeEmpty && _current.IsEmpty)
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// High-performance, zero-allocation enumerator for splitting character spans by a multi-character string delimiter.
    /// </summary>
    public ref struct SpanStringSplitter
    {
        private ReadOnlySpan<char> _span;
        private ReadOnlySpan<char> _current;
        private readonly ReadOnlySpan<char> _separator;
        private readonly bool _removeEmpty;
        private bool _finished;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SpanStringSplitter(ReadOnlySpan<char> span, ReadOnlySpan<char> separator, bool removeEmpty = false)
        {
            _span = span;
            _separator = separator;
            _removeEmpty = removeEmpty;
            _current = default;
            _finished = false;
        }

        public readonly ReadOnlySpan<char> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _current;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly SpanStringSplitter GetEnumerator() => this;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_separator.IsEmpty)
            {
                if (!_finished)
                {
                    _finished = true;
                    _current = _span;
                    return !_current.IsEmpty || !_removeEmpty;
                }
                return false;
            }

            while (!_finished)
            {
                int index = _span.IndexOf(_separator);
                if (index < 0)
                {
                    _finished = true;
                    _current = _span;
                    if (_removeEmpty && _current.IsEmpty)
                    {
                        return false;
                    }
                    return true;
                }

                _current = _span.Slice(0, index);
                _span = _span.Slice(index + _separator.Length);

                if (_removeEmpty && _current.IsEmpty)
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// High-performance, zero-allocation enumerator for splitting byte spans by a byte delimiter.
    /// Ideal for parsing network frames, line breaks, or comma-separated binary payloads.
    /// </summary>
    public ref struct SpanByteSplitter
    {
        private ReadOnlySpan<byte> _span;
        private ReadOnlySpan<byte> _current;
        private readonly byte _separator;
        private readonly bool _removeEmpty;
        private bool _finished;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SpanByteSplitter(ReadOnlySpan<byte> span, byte separator, bool removeEmpty = false)
        {
            _span = span;
            _separator = separator;
            _removeEmpty = removeEmpty;
            _current = default;
            _finished = false;
        }

        public readonly ReadOnlySpan<byte> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _current;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly SpanByteSplitter GetEnumerator() => this;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (!_finished)
            {
                int index = _span.IndexOf(_separator);
                if (index < 0)
                {
                    _finished = true;
                    _current = _span;
                    if (_removeEmpty && _current.IsEmpty)
                    {
                        return false;
                    }
                    return true;
                }

                _current = _span.Slice(0, index);
                _span = _span.Slice(index + 1);

                if (_removeEmpty && _current.IsEmpty)
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Extension methods for zero-allocation span splitting.
    /// </summary>
    public static class SpanSplitExtensions
    {
        /// <summary>
        /// Splits a character span by a delimiter character without any heap allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SpanSplitter SplitFast(this ReadOnlySpan<char> span, char separator, bool removeEmpty = false)
            => new SpanSplitter(span, separator, removeEmpty);

        /// <summary>
        /// Splits a string by a delimiter character without any heap allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SpanSplitter SplitFast(this string? str, char separator, bool removeEmpty = false)
            => new SpanSplitter(str != null ? str.AsSpan() : ReadOnlySpan<char>.Empty, separator, removeEmpty);

        /// <summary>
        /// Splits a character span by a string delimiter without any heap allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SpanStringSplitter SplitFast(this ReadOnlySpan<char> span, ReadOnlySpan<char> separator, bool removeEmpty = false)
            => new SpanStringSplitter(span, separator, removeEmpty);

        /// <summary>
        /// Splits a byte span by a byte delimiter without any heap allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SpanByteSplitter SplitFast(this ReadOnlySpan<byte> span, byte separator, bool removeEmpty = false)
            => new SpanByteSplitter(span, separator, removeEmpty);
    }
}
