using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using ZeroPrimitives.Time;

namespace ZeroPrimitives.Text
{
    /// <summary>
    /// Zero-allocation, RFC 4180 compliant UTF-8 CSV streaming writer with pooled buffer growth
    /// and optional Excel/spreadsheet formula-injection neutralization.
    /// </summary>
    public ref struct FastCsvWriter
    {
        private Span<byte> _buffer;
        private byte[]? _arrayToReturnToPool;
        private int _pos;
        private readonly byte _delimiter;
        private readonly bool _neutralizeFormulaInjection;
        private bool _hasFieldsInCurrentRow;

        public FastCsvWriter(Span<byte> initialBuffer, char delimiter = ',', bool neutralizeFormulaInjection = false)
        {
            if (delimiter > 127)
                throw new ArgumentOutOfRangeException(nameof(delimiter), "Delimiter must be an ASCII character.");

            _buffer = initialBuffer;
            _arrayToReturnToPool = null;
            _pos = 0;
            _delimiter = (byte)delimiter;
            _neutralizeFormulaInjection = neutralizeFormulaInjection;
            _hasFieldsInCurrentRow = false;
        }

        public FastCsvWriter(int initialCapacity, char delimiter = ',', bool neutralizeFormulaInjection = false)
        {
            if (delimiter > 127)
                throw new ArgumentOutOfRangeException(nameof(delimiter), "Delimiter must be an ASCII character.");

            _arrayToReturnToPool = ArrayPool<byte>.Shared.Rent(Math.Max(64, initialCapacity));
            _buffer = _arrayToReturnToPool;
            _pos = 0;
            _delimiter = (byte)delimiter;
            _neutralizeFormulaInjection = neutralizeFormulaInjection;
            _hasFieldsInCurrentRow = false;
        }

        public readonly int BytesWritten
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pos;
        }

        public readonly ReadOnlySpan<byte> WrittenSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Slice(0, _pos);
        }

        /// <summary>
        /// Writes a UTF-8 Byte Order Mark (<c>EF BB BF</c>) so Excel opens UTF-8 CSV files with Vietnamese diacritics intact.
        /// </summary>
        public void WriteUtf8Bom()
        {
            EnsureCapacity(3);
            _buffer[_pos++] = 0xEF;
            _buffer[_pos++] = 0xBB;
            _buffer[_pos++] = 0xBF;
        }

        public void WriteEmptyField()
        {
            WriteDelimiterIfNeeded();
        }

        public void WriteField(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                WriteDelimiterIfNeeded();
                return;
            }
            WriteField(value.AsSpan());
        }

        public unsafe void WriteField(scoped ReadOnlySpan<char> value)
        {
            WriteDelimiterIfNeeded();
            if (value.IsEmpty) return;

            bool needsFormulaPrefix = _neutralizeFormulaInjection && IsFormulaTrigger(value[0]);
            bool needsQuoting = needsFormulaPrefix;
            int quoteCount = 0;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '"')
                {
                    needsQuoting = true;
                    quoteCount++;
                }
                else if (c == _delimiter || c == '\r' || c == '\n')
                {
                    needsQuoting = true;
                }
            }

            if (!needsQuoting)
            {
                WriteUtf8Chars(value);
                return;
            }

            WriteByte((byte)'"');
            if (needsFormulaPrefix)
            {
                WriteByte((byte)'\'');
            }

            if (quoteCount == 0)
            {
                WriteUtf8Chars(value);
            }
            else
            {
                int segmentStart = 0;
                for (int i = 0; i < value.Length; i++)
                {
                    if (value[i] == '"')
                    {
                        if (i > segmentStart)
                        {
                            WriteUtf8Chars(value.Slice(segmentStart, i - segmentStart));
                        }
                        EnsureCapacity(2);
                        _buffer[_pos++] = (byte)'"';
                        _buffer[_pos++] = (byte)'"';
                        segmentStart = i + 1;
                    }
                }
                if (segmentStart < value.Length)
                {
                    WriteUtf8Chars(value.Slice(segmentStart));
                }
            }

            WriteByte((byte)'"');
        }

        public void WriteField(int value)
        {
            WriteDelimiterIfNeeded();
            EnsureCapacity(12);
            FastFormat.TryFormatInt32Utf8(value, _buffer.Slice(_pos), out int written);
            _pos += written;
        }

        public void WriteField(long value)
        {
            WriteDelimiterIfNeeded();
            EnsureCapacity(24);
            FastFormat.TryFormatInt64Utf8(value, _buffer.Slice(_pos), out int written);
            _pos += written;
        }

        public void WriteField(decimal value)
        {
            WriteDelimiterIfNeeded();
            EnsureCapacity(40);
            FastFormat.TryFormatDecimalUtf8(value, _buffer.Slice(_pos), out int written);
            _pos += written;
        }

        public void WriteField(CompactDate value)
        {
            WriteDelimiterIfNeeded();
            EnsureCapacity(10);
            value.TryFormatUtf8(_buffer.Slice(_pos), out int written);
            _pos += written;
        }

        public void WriteField(DateTime value, bool includeTime = false)
        {
            WriteDelimiterIfNeeded();
            if (includeTime)
            {
                EnsureCapacity(32);
                FastFormat.TryFormatIsoDateTimeUtf8(value, _buffer.Slice(_pos), out int written, includeMilliseconds: false, useT: true);
                _pos += written;
            }
            else
            {
                EnsureCapacity(10);
                FastFormat.TryFormatIsoDateUtf8(value, _buffer.Slice(_pos), out int written);
                _pos += written;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void NextRow(bool useCrLf = true)
        {
            if (useCrLf)
            {
                EnsureCapacity(2);
                _buffer[_pos++] = (byte)'\r';
                _buffer[_pos++] = (byte)'\n';
            }
            else
            {
                WriteByte((byte)'\n');
            }
            _hasFieldsInCurrentRow = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteDelimiterIfNeeded()
        {
            if (_hasFieldsInCurrentRow)
            {
                WriteByte(_delimiter);
            }
            else
            {
                _hasFieldsInCurrentRow = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFormulaTrigger(char firstChar)
        {
            return firstChar == '=' || firstChar == '+' || firstChar == '-' ||
                   firstChar == '@' || firstChar == '\t' || firstChar == '\r';
        }

        private unsafe void WriteUtf8Chars(scoped ReadOnlySpan<char> chars)
        {
            if (chars.IsEmpty) return;
            int maxBytes = chars.Length * 3;
            EnsureCapacity(maxBytes);
#if NET8_0_OR_GREATER
            _pos += Encoding.UTF8.GetBytes(chars, _buffer.Slice(_pos));
#else
            fixed (char* src = chars)
            fixed (byte* dst = _buffer.Slice(_pos))
            {
                _pos += Encoding.UTF8.GetBytes(src, chars.Length, dst, _buffer.Length - _pos);
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteByte(byte b)
        {
            if (_pos >= _buffer.Length) Grow(1);
            _buffer[_pos++] = b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int needed)
        {
            if (_pos + needed > _buffer.Length) Grow(needed);
        }

        private void Grow(int additionalCapacity)
        {
            int required = checked(_pos + additionalCapacity);
            int newCap = Math.Max(required, Math.Max(_buffer.Length * 2, 128));
            byte[] poolArray = ArrayPool<byte>.Shared.Rent(newCap);
            _buffer.Slice(0, _pos).CopyTo(poolArray);

            byte[]? toReturn = _arrayToReturnToPool;
            _buffer = _arrayToReturnToPool = poolArray;
            if (toReturn != null)
            {
                ArrayPool<byte>.Shared.Return(toReturn);
            }
        }

        public override unsafe string ToString()
        {
            if (_pos == 0) return string.Empty;
#if NET8_0_OR_GREATER
            return Encoding.UTF8.GetString(_buffer.Slice(0, _pos));
#else
            fixed (byte* p = _buffer)
            {
                return Encoding.UTF8.GetString(p, _pos);
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            byte[]? toReturn = _arrayToReturnToPool;
            this = default;
            if (toReturn != null)
            {
                ArrayPool<byte>.Shared.Return(toReturn);
            }
        }
    }
}
