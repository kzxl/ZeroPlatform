using System;
using System.Collections.Generic;
using System.Globalization;
using ZeroPrimitives;

namespace ZeroReports.Data
{
    public enum AggregateFunction
    {
        Sum,
        Count,
        Avg,
        Min,
        Max
    }

    /// <summary>
    /// Evaluates aggregate functions (SUM, COUNT, AVG, MIN, MAX) over report data source groups using ZeroPrimitives.FastConvert.
    /// </summary>
    public static class AggregateEvaluator
    {
        public static double Evaluate(
            IReportDataSource dataSource,
            IReadOnlyList<int> recordIndices,
            AggregateFunction function,
            string fieldName)
        {
            if (recordIndices == null || recordIndices.Count == 0)
            {
                return 0.0;
            }

            if (function == AggregateFunction.Count)
            {
                if (string.IsNullOrEmpty(fieldName) || fieldName == "*")
                {
                    return recordIndices.Count;
                }

                int nonNullCount = 0;
                foreach (int idx in recordIndices)
                {
                    if (dataSource.GetValue(idx, fieldName) != null) nonNullCount++;
                }
                return nonNullCount;
            }

            double sum = 0.0;
            double min = double.MaxValue;
            double max = double.MinValue;
            int count = 0;

            foreach (int idx in recordIndices)
            {
                object? val = dataSource.GetValue(idx, fieldName);
                if (val == null) continue;

                // Reusing ZeroPrimitives.FastConvert for register unboxing
                double num = FastConvert.AsDouble(val, defaultValue: 0.0);

                sum += num;
                count++;
                if (num < min) min = num;
                if (num > max) max = num;
            }

            if (count == 0) return 0.0;

            switch (function)
            {
                case AggregateFunction.Sum:
                    return sum;
                case AggregateFunction.Avg:
                    return sum / count;
                case AggregateFunction.Min:
                    return min;
                case AggregateFunction.Max:
                    return max;
                default:
                    return sum;
            }
        }

        private static readonly System.Text.RegularExpressions.Regex AggregatePattern =
            new System.Text.RegularExpressions.Regex(@"\[(SUM|COUNT|AVG|MIN|MAX)\(([^)]*)\)(?::([^\]]+))?\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>
        /// Parses and evaluates an expression string like "SUM(TotalAmount)" or template with embedded tokens like "Grand Total: [SUM(Amount):C0]" over record indices.
        /// </summary>
        public static string EvaluateExpression(
            string expression,
            IReportDataSource dataSource,
            IReadOnlyList<int> recordIndices,
            string format = "")
        {
            if (string.IsNullOrEmpty(expression)) return string.Empty;

            // 1. Scan for embedded aggregate tokens like [SUM(Amount):C0] or [COUNT(Id)]
            if (expression.IndexOf('(') >= 0 && expression.IndexOf(')') >= 0)
            {
                if (AggregatePattern.IsMatch(expression))
                {
                    return AggregatePattern.Replace(expression, match =>
                    {
                        string funcStr = match.Groups[1].Value.ToUpperInvariant();
                        string field = match.Groups[2].Value.Trim();
                        string localFormat = match.Groups[3].Success ? match.Groups[3].Value : format;

                        AggregateFunction func;
                        switch (funcStr)
                        {
                            case "SUM": func = AggregateFunction.Sum; break;
                            case "COUNT": func = AggregateFunction.Count; break;
                            case "AVG": func = AggregateFunction.Avg; break;
                            case "MIN": func = AggregateFunction.Min; break;
                            case "MAX": func = AggregateFunction.Max; break;
                            default: return match.Value;
                        }

                        double result = Evaluate(dataSource, recordIndices, func, field);
                        if (!string.IsNullOrEmpty(localFormat))
                        {
                            return result.ToString(localFormat, CultureInfo.CurrentCulture);
                        }
                        return result.ToString("G", CultureInfo.InvariantCulture);
                    });
                }

                // 2. Direct simple expression like "SUM(Amount)"
                string raw = expression.Trim();
                if (raw.StartsWith("[") && raw.EndsWith("]"))
                {
                    raw = raw.Substring(1, raw.Length - 2).Trim();
                }

                int parenOpen = raw.IndexOf('(');
                int parenClose = raw.LastIndexOf(')');

                if (parenOpen > 0 && parenClose > parenOpen)
                {
                    string funcName = raw.Substring(0, parenOpen).Trim().ToUpperInvariant();
                    string field = raw.Substring(parenOpen + 1, parenClose - parenOpen - 1).Trim();

                    AggregateFunction? func = null;
                    if (funcName == "SUM") func = AggregateFunction.Sum;
                    else if (funcName == "COUNT") func = AggregateFunction.Count;
                    else if (funcName == "AVG") func = AggregateFunction.Avg;
                    else if (funcName == "MIN") func = AggregateFunction.Min;
                    else if (funcName == "MAX") func = AggregateFunction.Max;

                    if (func.HasValue)
                    {
                        double result = Evaluate(dataSource, recordIndices, func.Value, field);
                        if (!string.IsNullOrEmpty(format))
                        {
                            return result.ToString(format, CultureInfo.CurrentCulture);
                        }
                        return result.ToString("G", CultureInfo.InvariantCulture);
                    }
                }
            }

            return expression;
        }
    }
}
