using System;
using System.Globalization;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// High-performance, zero-allocation recursive descent arithmetic expression evaluator.
    /// Evaluates expressions like "25 * 4", "100 / 2 + 10", "(15 + 5) * 2", "12.5 * 2" with decimal precision.
    /// </summary>
    public static class MathExpressionParser
    {
        public static bool TryEvaluate(string? expression, out decimal result)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                result = 0m;
                return false;
            }

            return TryEvaluate(expression.AsSpan(), out result);
        }

        public static bool TryEvaluate(ReadOnlySpan<char> expression, out decimal result)
        {
            try
            {
                var parser = new SpanParser(expression);
                result = parser.ParseExpression();
                parser.SkipWhitespace();
                return parser.IsEnd;
            }
            catch
            {
                result = 0m;
                return false;
            }
        }

        public static decimal Evaluate(string expression)
        {
            if (TryEvaluate(expression, out decimal result))
            {
                return result;
            }
            throw new FormatException($"Invalid math expression: '{expression}'");
        }

        private ref struct SpanParser
        {
            private readonly ReadOnlySpan<char> _span;
            private int _pos;

            public SpanParser(ReadOnlySpan<char> span)
            {
                _span = span;
                _pos = 0;
            }

            public bool IsEnd => _pos >= _span.Length;

            public void SkipWhitespace()
            {
                while (_pos < _span.Length && char.IsWhiteSpace(_span[_pos]))
                {
                    _pos++;
                }
            }

            public decimal ParseExpression()
            {
                SkipWhitespace();
                decimal value = ParseTerm();

                while (true)
                {
                    SkipWhitespace();
                    if (_pos >= _span.Length) break;

                    char op = _span[_pos];
                    if (op != '+' && op != '-') break;

                    _pos++;
                    decimal next = ParseTerm();
                    if (op == '+') value += next;
                    else value -= next;
                }

                return value;
            }

            private decimal ParseTerm()
            {
                SkipWhitespace();
                decimal value = ParsePower();

                while (true)
                {
                    SkipWhitespace();
                    if (_pos >= _span.Length) break;

                    char op = _span[_pos];
                    if (op != '*' && op != '/' && op != '%') break;

                    _pos++;
                    decimal next = ParsePower();
                    if (op == '*')
                    {
                        value *= next;
                    }
                    else if (op == '/')
                    {
                        if (next == 0m) throw new DivideByZeroException();
                        value /= next;
                    }
                    else
                    {
                        if (next == 0m) throw new DivideByZeroException();
                        value %= next;
                    }
                }

                return value;
            }

            private decimal ParsePower()
            {
                SkipWhitespace();
                decimal value = ParseFactor();

                SkipWhitespace();
                if (_pos < _span.Length && _span[_pos] == '^')
                {
                    _pos++;
                    decimal exponent = ParsePower();
                    double dBase = (double)value;
                    double dExp = (double)exponent;
                    double dResult = Math.Pow(dBase, dExp);
                    if (double.IsNaN(dResult) || double.IsInfinity(dResult))
                    {
                        throw new OverflowException();
                    }
                    value = (decimal)dResult;
                }

                return value;
            }

            private decimal ParseFactor()
            {
                SkipWhitespace();
                if (_pos >= _span.Length) throw new FormatException("Unexpected end of expression");

                char c = _span[_pos];

                // Unary + or -
                if (c == '+')
                {
                    _pos++;
                    return ParseFactor();
                }
                if (c == '-')
                {
                    _pos++;
                    return -ParseFactor();
                }

                // Parentheses
                if (c == '(')
                {
                    _pos++;
                    decimal value = ParseExpression();
                    SkipWhitespace();
                    if (_pos >= _span.Length || _span[_pos] != ')')
                    {
                        throw new FormatException("Missing closing parenthesis ')'");
                    }
                    _pos++;
                    return value;
                }

                // Number
                int start = _pos;
                while (_pos < _span.Length && (char.IsDigit(_span[_pos]) || _span[_pos] == '.'))
                {
                    _pos++;
                }

                if (_pos == start)
                {
                    throw new FormatException($"Unexpected character '{c}' at position {start}");
                }

                var numSpan = _span.Slice(start, _pos - start);
#if NETCOREAPP || NET8_0_OR_GREATER
                if (!decimal.TryParse(numSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number))
#else
                if (!decimal.TryParse(numSpan.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number))
#endif
                {
                    throw new FormatException($"Failed to parse number '{numSpan.ToString()}'");
                }

                return number;
            }
        }
    }
}
