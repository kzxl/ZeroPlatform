using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using ZeroPrimitives.Text;

namespace ZeroPrimitives.Parsing
{
    /// <summary>
    /// Type of JSON token encountered by FastJsonReader.
    /// </summary>
    public enum FastJsonTokenType : byte
    {
        None = 0,
        StartObject,
        EndObject,
        StartArray,
        EndArray,
        PropertyName,
        String,
        Number,
        True,
        False,
        Null
    }

    /// <summary>
    /// Micro, zero-allocation forward-only UTF-8 JSON stream reader / tokenizer.
    /// Operates directly on ReadOnlySpan&lt;byte&gt; without heap allocations or object model instantiation.
    /// </summary>
    public ref struct FastJsonReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _pos;
        private FastJsonTokenType _tokenType;
        private ReadOnlySpan<byte> _valueSpan;
        private bool _hasError;

        public FastJsonReader(ReadOnlySpan<byte> utf8Json)
        {
            _data = utf8Json;
            _pos = 0;
            _tokenType = FastJsonTokenType.None;
            _valueSpan = default;
            _hasError = false;
        }

        public bool HasError
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _hasError;
        }

        public FastJsonTokenType TokenType
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _tokenType;
        }

        public ReadOnlySpan<byte> ValueSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _valueSpan;
        }

        public int Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe string GetString()
        {
            if (_valueSpan.IsEmpty) return string.Empty;

            if (_valueSpan.IndexOf((byte)'\\') < 0)
            {
#if NET8_0_OR_GREATER
                return Encoding.UTF8.GetString(_valueSpan);
#else
                fixed (byte* p = _valueSpan)
                {
                    return Encoding.UTF8.GetString(p, _valueSpan.Length);
                }
#endif
            }

            return GetUnescapedStringSlow();
        }

        private unsafe string GetUnescapedStringSlow()
        {
            if (_valueSpan.Length <= 256)
            {
                Span<char> stackBuf = stackalloc char[_valueSpan.Length];
                if (TryGetUnescapedString(stackBuf, out int written))
                {
#if NET8_0_OR_GREATER
                    return new string(stackBuf.Slice(0, written));
#else
                    fixed (char* p = stackBuf)
                    {
                        return new string(p, 0, written);
                    }
#endif
                }
            }

            char[] rented = ArrayPool<char>.Shared.Rent(_valueSpan.Length);
            try
            {
                if (TryGetUnescapedString(rented.AsSpan(), out int written))
                {
                    return new string(rented, 0, written);
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }

#if NET8_0_OR_GREATER
            return Encoding.UTF8.GetString(_valueSpan);
#else
            fixed (byte* p = _valueSpan)
            {
                return Encoding.UTF8.GetString(p, _valueSpan.Length);
            }
#endif
        }

        /// <summary>
        /// Decodes and unescapes the current JSON string or property name token into <paramref name="destination"/> with zero heap allocation.
        /// </summary>
        public unsafe bool TryGetUnescapedString(scoped Span<char> destination, out int charsWritten)
        {
            charsWritten = 0;
            if (_valueSpan.IsEmpty) return true;

            if (_valueSpan.IndexOf((byte)'\\') < 0)
            {
#if NET8_0_OR_GREATER
                int needed = Encoding.UTF8.GetCharCount(_valueSpan);
                if (needed > destination.Length) return false;
                charsWritten = Encoding.UTF8.GetChars(_valueSpan, destination);
                return true;
#else
                fixed (byte* src = _valueSpan)
                {
                    int needed = Encoding.UTF8.GetCharCount(src, _valueSpan.Length);
                    if (needed > destination.Length) return false;
                    fixed (char* dst = destination)
                    {
                        charsWritten = Encoding.UTF8.GetChars(src, _valueSpan.Length, dst, destination.Length);
                        return true;
                    }
                }
#endif
            }

            int i = 0;
            int outIdx = 0;
            int len = _valueSpan.Length;

            while (i < len)
            {
                byte b = _valueSpan[i];
                if (b == (byte)'\\')
                {
                    if (i + 1 >= len) return false;
                    byte esc = _valueSpan[i + 1];
                    char unescaped;
                    switch (esc)
                    {
                        case (byte)'"': unescaped = '"'; i += 2; break;
                        case (byte)'\\': unescaped = '\\'; i += 2; break;
                        case (byte)'/': unescaped = '/'; i += 2; break;
                        case (byte)'b': unescaped = '\b'; i += 2; break;
                        case (byte)'f': unescaped = '\f'; i += 2; break;
                        case (byte)'n': unescaped = '\n'; i += 2; break;
                        case (byte)'r': unescaped = '\r'; i += 2; break;
                        case (byte)'t': unescaped = '\t'; i += 2; break;
                        case (byte)'u':
                            if (i + 5 >= len || !TryParseHex4(_valueSpan.Slice(i + 2, 4), out ushort codeUnit))
                                return false;
                            unescaped = (char)codeUnit;
                            i += 6;
                            break;
                        default:
                            return false;
                    }

                    if (outIdx >= destination.Length) return false;
                    destination[outIdx++] = unescaped;
                }
                else if (b < 0x80)
                {
                    if (outIdx >= destination.Length) return false;
                    destination[outIdx++] = (char)b;
                    i++;
                }
                else if ((b & 0xE0) == 0xC0)
                {
                    if (i + 1 >= len) return false;
                    byte b1 = _valueSpan[i + 1];
                    if ((b1 & 0xC0) != 0x80 || b < 0xC2) return false;
                    if (outIdx >= destination.Length) return false;
                    destination[outIdx++] = (char)(((b & 0x1F) << 6) | (b1 & 0x3F));
                    i += 2;
                }
                else if ((b & 0xF0) == 0xE0)
                {
                    if (i + 2 >= len) return false;
                    byte b1 = _valueSpan[i + 1];
                    byte b2 = _valueSpan[i + 2];
                    if ((b1 & 0xC0) != 0x80 || (b2 & 0xC0) != 0x80) return false;
                    int cp = ((b & 0x0F) << 12) | ((b1 & 0x3F) << 6) | (b2 & 0x3F);
                    if (cp < 0x800 || (cp >= 0xD800 && cp <= 0xDFFF)) return false;
                    if (outIdx >= destination.Length) return false;
                    destination[outIdx++] = (char)cp;
                    i += 3;
                }
                else if ((b & 0xF8) == 0xF0)
                {
                    if (i + 3 >= len) return false;
                    byte b1 = _valueSpan[i + 1];
                    byte b2 = _valueSpan[i + 2];
                    byte b3 = _valueSpan[i + 3];
                    if ((b1 & 0xC0) != 0x80 || (b2 & 0xC0) != 0x80 || (b3 & 0xC0) != 0x80) return false;
                    int cp = ((b & 0x07) << 18) | ((b1 & 0x3F) << 12) | ((b2 & 0x3F) << 6) | (b3 & 0x3F);
                    if (cp < 0x10000 || cp > 0x10FFFF) return false;
                    if (outIdx + 1 >= destination.Length) return false;
                    cp -= 0x10000;
                    destination[outIdx++] = (char)(0xD800 + (cp >> 10));
                    destination[outIdx++] = (char)(0xDC00 + (cp & 0x3FF));
                    i += 4;
                }
                else
                {
                    return false;
                }
            }

            charsWritten = outIdx;
            return true;
        }

        /// <summary>
        /// Compares the current token value against a UTF-8 byte sequence without heap allocation.
        /// Handles both unescaped and escaped JSON strings.
        /// </summary>
        public unsafe bool ValueEquals(scoped ReadOnlySpan<byte> utf8Text)
        {
            if (_valueSpan.IndexOf((byte)'\\') < 0)
            {
                return _valueSpan.SequenceEqual(utf8Text);
            }

            // Unescape current value, then compare against decoded utf8Text
            char[]? rentedVal = null;
            char[]? rentedCmp = null;
            try
            {
                Span<char> valBuf = _valueSpan.Length <= 256
                    ? stackalloc char[_valueSpan.Length]
                    : (rentedVal = ArrayPool<char>.Shared.Rent(_valueSpan.Length));
                if (!TryGetUnescapedString(valBuf, out int valWritten)) return false;

                int cmpMaxChars = utf8Text.Length;
                Span<char> cmpBuf = cmpMaxChars <= 256
                    ? stackalloc char[cmpMaxChars]
                    : (rentedCmp = ArrayPool<char>.Shared.Rent(cmpMaxChars));

                int cmpWritten;
                if (utf8Text.IsEmpty)
                {
                    cmpWritten = 0;
                }
                else
                {
#if NET8_0_OR_GREATER
                    cmpWritten = Encoding.UTF8.GetChars(utf8Text, cmpBuf);
#else
                    fixed (byte* src = utf8Text)
                    fixed (char* dst = cmpBuf)
                    {
                        cmpWritten = Encoding.UTF8.GetChars(src, utf8Text.Length, dst, cmpBuf.Length);
                    }
#endif
                }

                return valBuf.Slice(0, valWritten).SequenceEqual(cmpBuf.Slice(0, cmpWritten));
            }
            finally
            {
                if (rentedVal != null) ArrayPool<char>.Shared.Return(rentedVal);
                if (rentedCmp != null) ArrayPool<char>.Shared.Return(rentedCmp);
            }
        }

        /// <summary>
        /// Compares the current token value against a UTF-16 character span without heap allocation.
        /// Handles both unescaped and escaped JSON strings.
        /// </summary>
        public unsafe bool ValueEquals(scoped ReadOnlySpan<char> text)
        {
            if (_valueSpan.IndexOf((byte)'\\') < 0)
            {
                if (text.IsEmpty) return _valueSpan.IsEmpty;
                int maxUtf8 = text.Length * 3;
                if (_valueSpan.Length > maxUtf8) return false;

                if (maxUtf8 <= 256)
                {
                    Span<byte> utf8Buf = stackalloc byte[maxUtf8];
#if NET8_0_OR_GREATER
                    int written = Encoding.UTF8.GetBytes(text, utf8Buf);
#else
                    int written;
                    fixed (char* src = text)
                    fixed (byte* dst = utf8Buf)
                    {
                        written = Encoding.UTF8.GetBytes(src, text.Length, dst, maxUtf8);
                    }
#endif
                    return _valueSpan.SequenceEqual(utf8Buf.Slice(0, written));
                }

                byte[] rented = ArrayPool<byte>.Shared.Rent(maxUtf8);
                try
                {
                    int written;
                    fixed (char* src = text)
                    fixed (byte* dst = rented)
                    {
                        written = Encoding.UTF8.GetBytes(src, text.Length, dst, maxUtf8);
                    }
                    return _valueSpan.SequenceEqual(rented.AsSpan(0, written));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }

            if (_valueSpan.Length <= 256)
            {
                Span<char> buf = stackalloc char[_valueSpan.Length];
                return TryGetUnescapedString(buf, out int written) && buf.Slice(0, written).SequenceEqual(text);
            }

            char[] rentedChars = ArrayPool<char>.Shared.Rent(_valueSpan.Length);
            try
            {
                return TryGetUnescapedString(rentedChars.AsSpan(), out int written) &&
                       rentedChars.AsSpan(0, written).SequenceEqual(text);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rentedChars);
            }
        }

        /// <summary>
        /// Skips the current token value (including full nested objects/arrays).
        /// When positioned on a <see cref="FastJsonTokenType.PropertyName"/>, advances to and skips its associated property value.
        /// </summary>
        public bool Skip()
        {
            if (_hasError) return false;

            if (_tokenType == FastJsonTokenType.PropertyName)
            {
                if (!Read()) return false;
            }

            if (_tokenType == FastJsonTokenType.StartObject || _tokenType == FastJsonTokenType.StartArray)
            {
                int depth = 1;
                while (depth > 0)
                {
                    if (!Read()) return false;
                    if (_tokenType == FastJsonTokenType.StartObject || _tokenType == FastJsonTokenType.StartArray)
                    {
                        depth++;
                    }
                    else if (_tokenType == FastJsonTokenType.EndObject || _tokenType == FastJsonTokenType.EndArray)
                    {
                        depth--;
                    }
                }
            }

            return !_hasError && _tokenType != FastJsonTokenType.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseHex4(ReadOnlySpan<byte> hex, out ushort value)
        {
            value = 0;
            if (hex.Length < 4) return false;
            int acc = 0;
            for (int i = 0; i < 4; i++)
            {
                byte b = hex[i];
                int d;
                if (b >= (byte)'0' && b <= (byte)'9') d = b - '0';
                else if (b >= (byte)'a' && b <= (byte)'f') d = b - 'a' + 10;
                else if (b >= (byte)'A' && b <= (byte)'F') d = b - 'A' + 10;
                else return false;
                acc = (acc << 4) | d;
            }
            value = (ushort)acc;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetInt32()
        {
            if (FastNumberParser.TryParseInt32(_valueSpan, out int val))
                return val;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetInt64()
        {
            if (FastNumberParser.TryParseInt64(_valueSpan, out long val))
                return val;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetDecimal()
        {
            if (FastNumberParser.TryParseDecimal(_valueSpan, out decimal val))
                return val;
            return 0m;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetDouble()
        {
            if (FastNumberParser.TryParseDouble(_valueSpan, out double val))
                return val;
            return 0.0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetBoolean()
        {
            return _tokenType == FastJsonTokenType.True;
        }

        public bool Read()
        {
            if (_hasError) return false;

            SkipWhitespace();

            if (_pos >= _data.Length)
            {
                _tokenType = FastJsonTokenType.None;
                _valueSpan = default;
                return false;
            }

            byte b = _data[_pos++];

            if (b == (byte)',')
            {
                SkipWhitespace();
                if (_pos >= _data.Length) return false;
                b = _data[_pos++];
            }

            switch (b)
            {
                case (byte)'{':
                    _tokenType = FastJsonTokenType.StartObject;
                    _valueSpan = default;
                    return true;

                case (byte)'}':
                    _tokenType = FastJsonTokenType.EndObject;
                    _valueSpan = default;
                    return true;

                case (byte)'[':
                    _tokenType = FastJsonTokenType.StartArray;
                    _valueSpan = default;
                    return true;

                case (byte)']':
                    _tokenType = FastJsonTokenType.EndArray;
                    _valueSpan = default;
                    return true;

                case (byte)'"':
                    ReadStringContent();
                    if (_hasError)
                    {
                        _tokenType = FastJsonTokenType.None;
                        return false;
                    }
                    SkipWhitespace();
                    if (_pos < _data.Length && _data[_pos] == (byte)':')
                    {
                        _pos++; // Skip colon
                        _tokenType = FastJsonTokenType.PropertyName;
                    }
                    else
                    {
                        _tokenType = FastJsonTokenType.String;
                    }
                    return true;

                case (byte)'t': // true
                    if (_pos + 3 <= _data.Length && _data[_pos] == 'r' && _data[_pos + 1] == 'u' && _data[_pos + 2] == 'e')
                    {
                        _pos += 3;
                        _tokenType = FastJsonTokenType.True;
                        _valueSpan = _data.Slice(_pos - 4, 4);
                        return true;
                    }
                    break;

                case (byte)'f': // false
                    if (_pos + 4 <= _data.Length && _data[_pos] == 'a' && _data[_pos + 1] == 'l' && _data[_pos + 2] == 's' && _data[_pos + 3] == 'e')
                    {
                        _pos += 4;
                        _tokenType = FastJsonTokenType.False;
                        _valueSpan = _data.Slice(_pos - 5, 5);
                        return true;
                    }
                    break;

                case (byte)'n': // null
                    if (_pos + 3 <= _data.Length && _data[_pos] == 'u' && _data[_pos + 1] == 'l' && _data[_pos + 2] == 'l')
                    {
                        _pos += 3;
                        _tokenType = FastJsonTokenType.Null;
                        _valueSpan = _data.Slice(_pos - 4, 4);
                        return true;
                    }
                    break;

                default:
                    // Number (starts with digit or minus)
                    if ((b >= '0' && b <= '9') || b == '-')
                    {
                        int start = _pos - 1;
                        while (_pos < _data.Length)
                        {
                            byte c = _data[_pos];
                            if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-')
                            {
                                _pos++;
                            }
                            else
                            {
                                break;
                            }
                        }
                        _valueSpan = _data.Slice(start, _pos - start);
                        _tokenType = FastJsonTokenType.Number;
                        return true;
                    }
                    break;
            }

            return false;
        }

        private void ReadStringContent()
        {
            int start = _pos;
            while (_pos < _data.Length)
            {
                byte c = _data[_pos++];
                if (c == (byte)'\\')
                {
                    if (_pos < _data.Length) _pos++; // Skip escaped char
                }
                else if (c == (byte)'"')
                {
                    _valueSpan = _data.Slice(start, _pos - start - 1);
                    return;
                }
            }

            // Unterminated string: EOF reached without closing quote
            _hasError = true;
            _valueSpan = _data.Slice(start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipWhitespace()
        {
            while (_pos < _data.Length)
            {
                byte c = _data[_pos];
                if (c == (byte)' ' || c == (byte)'\t' || c == (byte)'\r' || c == (byte)'\n')
                {
                    _pos++;
                }
                else
                {
                    break;
                }
            }
        }
    }
}
