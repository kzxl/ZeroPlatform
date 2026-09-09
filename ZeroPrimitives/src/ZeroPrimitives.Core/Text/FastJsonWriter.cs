using System;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace ZeroPrimitives.Text
{
    /// <summary>
    /// Micro, zero-allocation UTF-8 JSON streaming writer.
    /// Operates directly on stack spans or rented byte pools without serialization graph overhead.
    /// </summary>
    public ref struct FastJsonWriter
    {
        private Span<byte> _buffer;
        private byte[]? _arrayToReturnToPool;
        private int _pos;
        private int _depth;
        private uint _stateBitmask; // Bit per depth: 0 = start of container (no comma), 1 = has elements (needs comma)

        public FastJsonWriter(Span<byte> initialBuffer)
        {
            _buffer = initialBuffer;
            _arrayToReturnToPool = null;
            _pos = 0;
            _depth = 0;
            _stateBitmask = 0;
        }

        public FastJsonWriter(int initialCapacity)
        {
            _arrayToReturnToPool = ArrayPool<byte>.Shared.Rent(initialCapacity);
            _buffer = _arrayToReturnToPool;
            _pos = 0;
            _depth = 0;
            _stateBitmask = 0;
        }

        public int BytesWritten
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pos;
        }

        public ReadOnlySpan<byte> WrittenSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Slice(0, _pos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteStartObject()
        {
            PrepareValue();
            WriteByte((byte)'{');
            _depth++;
            SetNeedsComma(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEndObject()
        {
            if (_depth > 0) _depth--;
            WriteByte((byte)'}');
            SetNeedsComma(true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteStartArray()
        {
            PrepareValue();
            WriteByte((byte)'[');
            _depth++;
            SetNeedsComma(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEndArray()
        {
            if (_depth > 0) _depth--;
            WriteByte((byte)']');
            SetNeedsComma(true);
        }

        public void WritePropertyName(ReadOnlySpan<char> name)
        {
            PrepareValue();
            WriteEscapedString(name);
            WriteByte((byte)':');
            SetNeedsComma(false);
        }

        public void WriteString(ReadOnlySpan<char> propertyName, ReadOnlySpan<char> value)
        {
            WritePropertyName(propertyName);
            WriteEscapedString(value);
            SetNeedsComma(true);
        }

        public void WriteString(ReadOnlySpan<char> value)
        {
            PrepareValue();
            WriteEscapedString(value);
            SetNeedsComma(true);
        }

        public void WriteNumber(ReadOnlySpan<char> propertyName, int value)
        {
            WritePropertyName(propertyName);
            WriteNumberRaw(value);
            SetNeedsComma(true);
        }

        public void WriteNumber(ReadOnlySpan<char> propertyName, long value)
        {
            WritePropertyName(propertyName);
            WriteNumberRaw(value);
            SetNeedsComma(true);
        }

        public void WriteNumber(ReadOnlySpan<char> propertyName, decimal value)
        {
            WritePropertyName(propertyName);
            WriteNumberRaw(value);
            SetNeedsComma(true);
        }

        public void WriteNumber(ReadOnlySpan<char> propertyName, double value)
        {
            WritePropertyName(propertyName);
            WriteNumberRaw(value);
            SetNeedsComma(true);
        }

        public void WriteBoolean(ReadOnlySpan<char> propertyName, bool value)
        {
            WritePropertyName(propertyName);
            WriteBytes(value ? "true"u8 : "false"u8);
            SetNeedsComma(true);
        }

        public void WriteNull(ReadOnlySpan<char> propertyName)
        {
            WritePropertyName(propertyName);
            WriteBytes("null"u8);
            SetNeedsComma(true);
        }

        public void WriteRaw(ReadOnlySpan<byte> utf8Json)
        {
            PrepareValue();
            WriteBytes(utf8Json);
            SetNeedsComma(true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PrepareValue()
        {
            if (NeedsComma())
            {
                WriteByte((byte)',');
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool NeedsComma()
        {
            if (_depth == 0) return false;
            return ((_stateBitmask >> _depth) & 1) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetNeedsComma(bool needsComma)
        {
            if (_depth == 0) return;
            if (needsComma)
                _stateBitmask |= (1u << _depth);
            else
                _stateBitmask &= ~(1u << _depth);
        }

        private void WriteEscapedString(ReadOnlySpan<char> str)
        {
            WriteByte((byte)'"');
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                switch (c)
                {
                    case '"': WriteBytes("\\\""u8); break;
                    case '\\': WriteBytes("\\\\"u8); break;
                    case '\b': WriteBytes("\\b"u8); break;
                    case '\f': WriteBytes("\\f"u8); break;
                    case '\n': WriteBytes("\\n"u8); break;
                    case '\r': WriteBytes("\\r"u8); break;
                    case '\t': WriteBytes("\\t"u8); break;
                    default:
                        if (c < 32)
                        {
                            WriteBytes("\\u00"u8);
                            WriteHexByte((byte)c);
                        }
                        else if (c < 128)
                        {
                            WriteByte((byte)c);
                        }
                        else if (c < 0x800)
                        {
                            WriteByte((byte)(0xC0 | (c >> 6)));
                            WriteByte((byte)(0x80 | (c & 0x3F)));
                        }
                        else
                        {
                            WriteByte((byte)(0xE0 | (c >> 12)));
                            WriteByte((byte)(0x80 | ((c >> 6) & 0x3F)));
                            WriteByte((byte)(0x80 | (c & 0x3F)));
                        }
                        break;
                }
            }
            WriteByte((byte)'"');
        }

        private void WriteNumberRaw(long val)
        {
#if NET8_0_OR_GREATER
            EnsureCapacity(32);
            if (val.TryFormat(_buffer.Slice(_pos), out int written, default, CultureInfo.InvariantCulture))
            {
                _pos += written;
                return;
            }
#endif
            string s = val.ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < s.Length; i++) WriteByte((byte)s[i]);
        }

        private void WriteNumberRaw(decimal val)
        {
#if NET8_0_OR_GREATER
            EnsureCapacity(40);
            if (val.TryFormat(_buffer.Slice(_pos), out int written, default, CultureInfo.InvariantCulture))
            {
                _pos += written;
                return;
            }
#endif
            string s = val.ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < s.Length; i++) WriteByte((byte)s[i]);
        }

        private void WriteNumberRaw(double val)
        {
#if NET8_0_OR_GREATER
            EnsureCapacity(32);
            if (val.TryFormat(_buffer.Slice(_pos), out int written, default, CultureInfo.InvariantCulture))
            {
                _pos += written;
                return;
            }
#endif
            string s = val.ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < s.Length; i++) WriteByte((byte)s[i]);
        }

        private void WriteHexByte(byte b)
        {
            const string HexChars = "0123456789abcdef";
            WriteByte((byte)HexChars[b >> 4]);
            WriteByte((byte)HexChars[b & 0x0F]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteByte(byte b)
        {
            if (_pos >= _buffer.Length) Grow(1);
            _buffer[_pos++] = b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteBytes(ReadOnlySpan<byte> bytes)
        {
            if (_pos + bytes.Length > _buffer.Length) Grow(bytes.Length);
            bytes.CopyTo(_buffer.Slice(_pos));
            _pos += bytes.Length;
        }

        private void EnsureCapacity(int needed)
        {
            if (_pos + needed > _buffer.Length) Grow(needed);
        }

        private void Grow(int additionalCapacity)
        {
            int required = checked(_pos + additionalCapacity);
            int newCap = Math.Max(required, Math.Max(_buffer.Length * 2, 64));
            byte[] poolArray = ArrayPool<byte>.Shared.Rent(newCap);
            _buffer.Slice(0, _pos).CopyTo(poolArray);

            byte[]? toReturn = _arrayToReturnToPool;
            _buffer = _arrayToReturnToPool = poolArray;
            if (toReturn != null)
            {
                ArrayPool<byte>.Shared.Return(toReturn);
            }
        }

#if !NET8_0_OR_GREATER
        public override string ToString() => Encoding.UTF8.GetString(_buffer.Slice(0, _pos).ToArray());
#else
        public override string ToString() => Encoding.UTF8.GetString(_buffer.Slice(0, _pos));
#endif

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
