using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ZeroUI.Core.Spreadsheet
{
    /// <summary>
    /// Recursive descent formula parser and dependency evaluator supporting arithmetic, cell references, ranges,
    /// standard spreadsheet functions (SUM, AVERAGE, MIN, MAX, COUNT, IF), and circular reference detection.
    /// </summary>
    public static class SpreadsheetFormulaEngine
    {
        public static void RecalculateWorksheet(SpreadsheetWorksheet ws)
        {
            if (ws == null) return;

            var evaluating = new HashSet<CellAddress>();

            foreach (var cell in ws.GetPopulatedCells())
            {
                if (cell.HasFormula)
                {
                    try
                    {
                        EvaluateCell(ws, cell, evaluating);
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.StartsWith("#CIRCULAR!", StringComparison.Ordinal))
                        {
                            cell.Error = "#CIRCULAR!";
                            cell.EvaluatedValue = null;
                        }
                    }
                }
            }
        }

        public static object? EvaluateCell(SpreadsheetWorksheet ws, SpreadsheetCell cell, HashSet<CellAddress> evaluating)
        {
            if (!cell.HasFormula)
                return cell.EvaluatedValue;

            if (evaluating.Contains(cell.Address))
            {
                cell.Error = "#CIRCULAR!";
                cell.EvaluatedValue = null;
                throw new InvalidOperationException("#CIRCULAR! Dependency cycle detected at " + cell.Address.Name);
            }

            evaluating.Add(cell.Address);
            try
            {
                string formula = cell.Formula;
                object? result = EvaluateExpression(ws, formula, evaluating);
                cell.Error = null;
                cell.EvaluatedValue = result;
                return result;
            }
            catch (DivideByZeroException)
            {
                cell.Error = "#DIV/0!";
                cell.EvaluatedValue = null;
                return null;
            }
            catch (Exception ex)
            {
                if (ex.Message.StartsWith("#CIRCULAR!", StringComparison.Ordinal))
                {
                    cell.Error = "#CIRCULAR!";
                    cell.EvaluatedValue = null;
                    throw;
                }
                cell.Error = ex.Message.StartsWith("#", StringComparison.Ordinal) ? ex.Message : "#VALUE!";
                cell.EvaluatedValue = null;
                return null;
            }
            finally
            {
                evaluating.Remove(cell.Address);
            }
        }

        public static object? EvaluateExpression(SpreadsheetWorksheet ws, string expression, HashSet<CellAddress>? evaluating = null)
        {
            if (string.IsNullOrWhiteSpace(expression)) return null;

            evaluating ??= new HashSet<CellAddress>();
            var parser = new ExpressionParser(ws, expression.Trim(), evaluating);
            return parser.Parse();
        }

        #region Internal Recursive Descent Expression Parser

        private sealed class ExpressionParser
        {
            private readonly SpreadsheetWorksheet _ws;
            private readonly string _text;
            private readonly HashSet<CellAddress> _evaluating;
            private int _pos;

            public ExpressionParser(SpreadsheetWorksheet ws, string text, HashSet<CellAddress> evaluating)
            {
                _ws = ws;
                _text = text;
                _evaluating = evaluating;
                _pos = 0;
            }

            public object? Parse()
            {
                object? result = ParseComparison();
                SkipWhitespace();
                return result;
            }

            private void SkipWhitespace()
            {
                while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
                    _pos++;
            }

            private char Peek()
            {
                SkipWhitespace();
                return _pos < _text.Length ? _text[_pos] : '\0';
            }

            private char Next()
            {
                SkipWhitespace();
                return _pos < _text.Length ? _text[_pos++] : '\0';
            }

            // Comparison: =, <>, <, <=, >, >=
            private object? ParseComparison()
            {
                object? left = ParseAddSub();
                SkipWhitespace();

                if (_pos < _text.Length)
                {
                    string op = "";
                    if (_text[_pos] == '=' || _text[_pos] == '<' || _text[_pos] == '>')
                    {
                        op += _text[_pos++];
                        if (_pos < _text.Length && (_text[_pos] == '=' || _text[_pos] == '>'))
                        {
                            op += _text[_pos++];
                        }
                    }

                    if (!string.IsNullOrEmpty(op))
                    {
                        object? right = ParseAddSub();
                        double dLeft = ToDouble(left);
                        double dRight = ToDouble(right);

                        return op switch
                        {
                            "=" => Math.Abs(dLeft - dRight) < 1e-9,
                            "<>" => Math.Abs(dLeft - dRight) >= 1e-9,
                            "<" => dLeft < dRight,
                            "<=" => dLeft <= dRight,
                            ">" => dLeft > dRight,
                            ">=" => dLeft >= dRight,
                            _ => left
                        };
                    }
                }

                return left;
            }

            // Addition & Subtraction: +, -
            private object? ParseAddSub()
            {
                object? left = ParseMulDiv();

                while (true)
                {
                    char c = Peek();
                    if (c == '+' || c == '-')
                    {
                        Next();
                        object? right = ParseMulDiv();
                        double dL = ToDouble(left);
                        double dR = ToDouble(right);
                        left = (c == '+') ? (dL + dR) : (dL - dR);
                    }
                    else break;
                }

                return left;
            }

            // Multiplication & Division: *, /
            private object? ParseMulDiv()
            {
                object? left = ParsePower();

                while (true)
                {
                    char c = Peek();
                    if (c == '*' || c == '/')
                    {
                        Next();
                        object? right = ParsePower();
                        double dL = ToDouble(left);
                        double dR = ToDouble(right);

                        if (c == '/')
                        {
                            if (Math.Abs(dR) < 1e-12) throw new DivideByZeroException();
                            left = dL / dR;
                        }
                        else
                        {
                            left = dL * dR;
                        }
                    }
                    else break;
                }

                return left;
            }

            // Power: ^
            private object? ParsePower()
            {
                object? left = ParseUnary();

                if (Peek() == '^')
                {
                    Next();
                    object? right = ParsePower(); // right-associative
                    double dL = ToDouble(left);
                    double dR = ToDouble(right);
                    return Math.Pow(dL, dR);
                }

                return left;
            }

            // Unary: +, -
            private object? ParseUnary()
            {
                char c = Peek();
                if (c == '-')
                {
                    Next();
                    return -ToDouble(ParseUnary());
                }
                if (c == '+')
                {
                    Next();
                    return ParseUnary();
                }
                return ParsePrimary();
            }

            // Primary: Number, String literal, Function call, Cell ref, (Subexpression)
            private object? ParsePrimary()
            {
                char c = Peek();

                if (c == '(')
                {
                    Next(); // consume '('
                    object? inner = ParseComparison();
                    SkipWhitespace();
                    if (Peek() == ')') Next();
                    return inner;
                }

                // String literal: "..."
                if (c == '"')
                {
                    Next();
                    int start = _pos;
                    while (_pos < _text.Length && _text[_pos] != '"')
                        _pos++;
                    string str = _text.Substring(start, _pos - start);
                    if (_pos < _text.Length) Next(); // consume closing quote
                    return str;
                }

                // Number
                if (char.IsDigit(c) || c == '.')
                {
                    int start = _pos;
                    while (_pos < _text.Length && (char.IsDigit(_text[_pos]) || _text[_pos] == '.'))
                        _pos++;
                    string numStr = _text.Substring(start, _pos - start);
                    if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                        return val;
                    throw new FormatException($"Invalid number token '{numStr}'");
                }

                // Identifier: Function name or Cell Reference
                if (char.IsLetter(c))
                {
                    int start = _pos;
                    while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] == '_'))
                        _pos++;
                    string id = _text.Substring(start, _pos - start);

                    SkipWhitespace();
                    if (Peek() == '(')
                    {
                        // Function Call: ID(...)
                        Next(); // consume '('
                        var args = ParseArguments();
                        return ExecuteFunction(id.ToUpperInvariant(), args);
                    }

                    // Check if Range: e.g. A1:B10
                    if (Peek() == ':')
                    {
                        Next(); // consume ':'
                        int start2 = _pos;
                        while (_pos < _text.Length && char.IsLetterOrDigit(_text[_pos]))
                            _pos++;
                        string id2 = _text.Substring(start2, _pos - start2);

                        if (CellAddress.TryParse(id, out var a1) && CellAddress.TryParse(id2, out var a2))
                        {
                            return new CellRange(a1, a2);
                        }
                    }

                    // Single Cell Reference: e.g. A1, B12
                    if (CellAddress.TryParse(id, out var cellAddr))
                    {
                        var targetCell = _ws.GetCell(cellAddr);
                        if (targetCell == null) return 0.0;

                        if (targetCell.HasFormula)
                        {
                            return EvaluateCell(_ws, targetCell, _evaluating);
                        }
                        return targetCell.EvaluatedValue ?? 0.0;
                    }

                    throw new FormatException($"#NAME? Unknown token '{id}'");
                }

                throw new FormatException($"Unexpected token at position {_pos} ('{c}')");
            }

            private List<object?> ParseArguments()
            {
                var args = new List<object?>();
                SkipWhitespace();
                if (Peek() == ')')
                {
                    Next();
                    return args;
                }

                while (true)
                {
                    object? arg = ParseArgumentItem();
                    args.Add(arg);

                    SkipWhitespace();
                    char c = Peek();
                    if (c == ',')
                    {
                        Next();
                    }
                    else if (c == ')')
                    {
                        Next();
                        break;
                    }
                    else
                    {
                        break;
                    }
                }

                return args;
            }

            private object? ParseArgumentItem()
            {
                // Check if range e.g. B2:B10
                SkipWhitespace();
                int mark = _pos;

                // Lookahead for range "A1:B10"
                int colonIdx = -1;
                int endIdx = _pos;
                while (endIdx < _text.Length && _text[endIdx] != ',' && _text[endIdx] != ')')
                {
                    if (_text[endIdx] == ':') colonIdx = endIdx;
                    endIdx++;
                }

                if (colonIdx > mark)
                {
                    string candidate = _text.Substring(mark, endIdx - mark).Trim();
                    if (CellRange.TryParse(candidate, out var range))
                    {
                        _pos = endIdx;
                        return range;
                    }
                }

                return ParseComparison();
            }

            private object? ExecuteFunction(string funcName, List<object?> args)
            {
                switch (funcName)
                {
                    case "SUM":
                        return Aggregate(args, (acc, val) => acc + val, 0.0);

                    case "AVERAGE":
                        var nums = FlattenNumbers(args);
                        if (nums.Count == 0) throw new DivideByZeroException();
                        double sum = 0;
                        for (int i = 0; i < nums.Count; i++) sum += nums[i];
                        return sum / nums.Count;

                    case "MIN":
                        var minList = FlattenNumbers(args);
                        if (minList.Count == 0) return 0.0;
                        double minVal = double.MaxValue;
                        for (int i = 0; i < minList.Count; i++) if (minList[i] < minVal) minVal = minList[i];
                        return minVal;

                    case "MAX":
                        var maxList = FlattenNumbers(args);
                        if (maxList.Count == 0) return 0.0;
                        double maxVal = double.MinValue;
                        for (int i = 0; i < maxList.Count; i++) if (maxList[i] > maxVal) maxVal = maxList[i];
                        return maxVal;

                    case "COUNT":
                        return (double)FlattenNumbers(args).Count;

                    case "IF":
                        if (args.Count < 2) throw new FormatException("#VALUE! IF requires at least 2 arguments.");
                        bool cond = IsTruthy(args[0]);
                        if (cond) return args[1];
                        return (args.Count >= 3) ? args[2] : 0.0;

                    default:
                        throw new FormatException($"#NAME? Unknown function '{funcName}'");
                }
            }

            private double Aggregate(List<object?> args, Func<double, double, double> op, double initial)
            {
                var nums = FlattenNumbers(args);
                double acc = initial;
                for (int i = 0; i < nums.Count; i++)
                {
                    acc = op(acc, nums[i]);
                }
                return acc;
            }

            private List<double> FlattenNumbers(List<object?> args)
            {
                var list = new List<double>();
                for (int i = 0; i < args.Count; i++)
                {
                    var arg = args[i];
                    if (arg is CellRange range)
                    {
                        foreach (var addr in range.GetAddresses())
                        {
                            var cell = _ws.GetCell(addr);
                            if (cell != null)
                            {
                                object? val = cell.HasFormula ? EvaluateCell(_ws, cell, _evaluating) : cell.EvaluatedValue;
                                if (TryConvertToDouble(val, out double d))
                                {
                                    list.Add(d);
                                }
                            }
                        }
                    }
                    else if (TryConvertToDouble(arg, out double singleD))
                    {
                        list.Add(singleD);
                    }
                }
                return list;
            }

            private static bool TryConvertToDouble(object? obj, out double val)
            {
                if (obj is double d) { val = d; return true; }
                if (obj is decimal dec) { val = (double)dec; return true; }
                if (obj is int i) { val = i; return true; }
                if (obj is long l) { val = l; return true; }
                if (obj is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                {
                    val = parsed;
                    return true;
                }
                val = 0;
                return false;
            }

            private static double ToDouble(object? obj)
            {
                if (TryConvertToDouble(obj, out double d)) return d;
                return 0.0;
            }

            private static bool IsTruthy(object? obj)
            {
                if (obj is bool b) return b;
                if (obj is double d) return Math.Abs(d) > 1e-9;
                if (obj is int i) return i != 0;
                if (obj is string s) return !string.IsNullOrEmpty(s) && !s.Equals("false", StringComparison.OrdinalIgnoreCase);
                return obj != null;
            }
        }

        #endregion
    }
}
