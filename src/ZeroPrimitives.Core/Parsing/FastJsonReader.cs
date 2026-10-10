using System;
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
#if NET8_0_OR_GREATER
            return Encoding.UTF8.GetString(_valueSpan);
#else
            if (_valueSpan.IsEmpty) return string.Empty;
            fixed (byte* p = _valueSpan)
            {
                return Encoding.UTF8.GetString(p, _valueSpan.Length);
            }
#endif
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
