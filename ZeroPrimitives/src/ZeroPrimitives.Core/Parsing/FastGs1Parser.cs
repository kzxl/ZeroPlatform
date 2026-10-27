using System;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Parsing
{
    /// <summary>
    /// Represents a parsed GS1 Application Identifier (AI) and its associated value.
    /// Zero heap allocation.
    /// </summary>
    public readonly ref struct Gs1Element
    {
        public readonly ReadOnlySpan<char> Ai;
        public readonly ReadOnlySpan<char> Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Gs1Element(ReadOnlySpan<char> ai, ReadOnlySpan<char> value)
        {
            Ai = ai;
            Value = value;
        }

        public bool IsGtin => Ai.SequenceEqual("01".AsSpan()) || Ai.SequenceEqual("02".AsSpan());
        public bool IsLot => Ai.SequenceEqual("10".AsSpan());
        public bool IsExpirationDate => Ai.SequenceEqual("17".AsSpan());
        public bool IsProductionDate => Ai.SequenceEqual("11".AsSpan());
        public bool IsSerial => Ai.SequenceEqual("21".AsSpan());
        public bool IsCount => Ai.SequenceEqual("30".AsSpan()) || Ai.SequenceEqual("37".AsSpan());

        /// <summary>
        /// Attempts to parse YYMMDD GS1 date into a DateTime.
        /// </summary>
        public bool TryGetDate(out DateTime date)
        {
            date = default;
            if (Value.Length != 6) return false;

            int yy = (Value[0] - '0') * 10 + (Value[1] - '0');
            int mm = (Value[2] - '0') * 10 + (Value[3] - '0');
            int dd = (Value[4] - '0') * 10 + (Value[5] - '0');

            if (mm < 1 || mm > 12) return false;

            // GS1 year rule: 51-99 is 1951-1999, 00-50 is 2000-2050 (or current century window)
            int year = (yy >= 51) ? 1900 + yy : 2000 + yy;

            // If day is 00, it denotes the last day of the given month
            if (dd == 0)
            {
                dd = DateTime.DaysInMonth(year, mm);
            }
            else if (dd > DateTime.DaysInMonth(year, mm))
            {
                return false;
            }

            date = new DateTime(year, mm, dd);
            return true;
        }
    }

    /// <summary>
    /// Zero-allocation, high-throughput GS1-128 and GS1 DataMatrix barcode parser.
    /// Supports both raw FNC1-delimited streams (ASCII 29 / \u001d) and human-readable bracketed format "(01)...(17)...".
    /// </summary>
    public ref struct FastGs1Parser
    {
        private ReadOnlySpan<char> _remaining;
        private readonly bool _isBracketed;

        public FastGs1Parser(ReadOnlySpan<char> barcode)
        {
            _remaining = barcode.Trim();
            _isBracketed = _remaining.Length > 0 && _remaining[0] == '(';
        }

        public bool MoveNext(out Gs1Element element)
        {
            element = default;
            if (_remaining.IsEmpty) return false;

            // Skip leading FNC1 or Group Separator (ASCII 29)
            while (!_remaining.IsEmpty && (_remaining[0] == '\u001d' || _remaining[0] == '\x1D'))
            {
                _remaining = _remaining.Slice(1);
            }

            if (_remaining.IsEmpty) return false;

            if (_isBracketed || _remaining[0] == '(')
            {
                return ParseBracketed(out element);
            }

            return ParseStandardStream(out element);
        }

        private bool ParseBracketed(out Gs1Element element)
        {
            element = default;
            if (_remaining.IsEmpty || _remaining[0] != '(') return false;

            int closeParen = _remaining.IndexOf(')');
            if (closeParen <= 1) return false;

            var ai = _remaining.Slice(1, closeParen - 1);
            _remaining = _remaining.Slice(closeParen + 1);

            int nextParen = _remaining.IndexOf('(');
            ReadOnlySpan<char> val;
            if (nextParen >= 0)
            {
                val = _remaining.Slice(0, nextParen);
                _remaining = _remaining.Slice(nextParen);
            }
            else
            {
                val = _remaining;
                _remaining = ReadOnlySpan<char>.Empty;
            }

            element = new Gs1Element(ai, val);
            return true;
        }

        private bool ParseStandardStream(out Gs1Element element)
        {
            element = default;
            if (_remaining.Length < 2) return false;

            // Determine AI length and fixed value length
            if (!TryResolveAi(_remaining, out var ai, out int fixedValLen, out int maxVarLen))
                return false;

            _remaining = _remaining.Slice(ai.Length);

            if (fixedValLen > 0)
            {
                if (_remaining.Length < fixedValLen) return false;
                var val = _remaining.Slice(0, fixedValLen);
                _remaining = _remaining.Slice(fixedValLen);
                element = new Gs1Element(ai, val);
                return true;
            }
            else
            {
                // Variable length - terminated by FNC1 (\u001d) or end of string
                int term = -1;
                for (int i = 0; i < _remaining.Length; i++)
                {
                    if (_remaining[i] == '\u001d' || _remaining[i] == '\x1D')
                    {
                        term = i;
                        break;
                    }
                }

                int take = (term >= 0) ? term : _remaining.Length;
                if (maxVarLen > 0 && take > maxVarLen) take = maxVarLen;

                var val = _remaining.Slice(0, take);
                if (term >= 0)
                {
                    _remaining = _remaining.Slice(term + 1);
                }
                else
                {
                    _remaining = _remaining.Slice(take);
                }

                element = new Gs1Element(ai, val);
                return true;
            }
        }

        private static bool TryResolveAi(ReadOnlySpan<char> span, out ReadOnlySpan<char> ai, out int fixedLen, out int maxVarLen)
        {
            ai = default;
            fixedLen = 0;
            maxVarLen = 0;

            if (span.Length < 2) return false;

            // 2-digit AIs
            var two = span.Slice(0, 2);
            if (two.SequenceEqual("00".AsSpan())) { ai = two; fixedLen = 18; return true; } // SSCC
            if (two.SequenceEqual("01".AsSpan())) { ai = two; fixedLen = 14; return true; } // GTIN
            if (two.SequenceEqual("02".AsSpan())) { ai = two; fixedLen = 14; return true; } // Content GTIN
            if (two.SequenceEqual("10".AsSpan())) { ai = two; fixedLen = 0; maxVarLen = 20; return true; } // Batch/Lot
            if (two.SequenceEqual("11".AsSpan())) { ai = two; fixedLen = 6; return true; }  // Production Date
            if (two.SequenceEqual("12".AsSpan())) { ai = two; fixedLen = 6; return true; }  // Due Date
            if (two.SequenceEqual("13".AsSpan())) { ai = two; fixedLen = 6; return true; }  // Packaging Date
            if (two.SequenceEqual("15".AsSpan())) { ai = two; fixedLen = 6; return true; }  // Best Before
            if (two.SequenceEqual("17".AsSpan())) { ai = two; fixedLen = 6; return true; }  // Expiry Date
            if (two.SequenceEqual("20".AsSpan())) { ai = two; fixedLen = 2; return true; }  // Variant
            if (two.SequenceEqual("21".AsSpan())) { ai = two; fixedLen = 0; maxVarLen = 20; return true; } // Serial Number
            if (two.SequenceEqual("30".AsSpan())) { ai = two; fixedLen = 0; maxVarLen = 8; return true; }  // Count
            if (two.SequenceEqual("37".AsSpan())) { ai = two; fixedLen = 0; maxVarLen = 8; return true; }  // Units

            // 3-digit AIs
            if (span.Length >= 3)
            {
                var three = span.Slice(0, 3);
                if (three.SequenceEqual("240".AsSpan()) || three.SequenceEqual("241".AsSpan()) || three.SequenceEqual("250".AsSpan()))
                {
                    ai = three; fixedLen = 0; maxVarLen = 30; return true;
                }
                if (three.SequenceEqual("400".AsSpan()) || three.SequenceEqual("420".AsSpan()))
                {
                    ai = three; fixedLen = 0; maxVarLen = 30; return true;
                }
                if (three.SequenceEqual("410".AsSpan()) || three.SequenceEqual("411".AsSpan()) || three.SequenceEqual("412".AsSpan()) || three.SequenceEqual("413".AsSpan()))
                {
                    ai = three; fixedLen = 13; return true;
                }
            }

            // 4-digit AIs (Weight, dimensions: 3100-3105, 3200-3205)
            if (span.Length >= 4)
            {
                var four = span.Slice(0, 4);
                if (four.StartsWith("310".AsSpan()) || four.StartsWith("320".AsSpan()))
                {
                    ai = four; fixedLen = 6; return true;
                }
            }

            // Fallback: assume 2-digit AI variable length up to 30
            ai = two;
            fixedLen = 0;
            maxVarLen = 30;
            return true;
        }
    }
}
