using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ZeroData.Core;

namespace ZeroAgent.Tools.Data
{
    /// <summary>
    /// In-memory columnar analytics engine for executing AnalyticalQuerySpec on DataFrame tables.
    /// Provides zero-dependency, local execution of filters, aggregations, grouping, and ordering.
    /// </summary>
    internal static class DataFrameAnalyticsEngine
    {
        public static List<Dictionary<string, object?>> Execute(DataFrame df, AnalyticalQuerySpec spec)
        {
            var matchedRowIndices = new List<int>();
            for (int r = 0; r < df.RowCount; r++)
            {
                if (MatchesFilters(df, r, spec.Filters)) matchedRowIndices.Add(r);
            }

            List<Dictionary<string, object?>> results;
            if (spec.GroupBy != null && spec.GroupBy.Count > 0)
                results = ExecuteGroupBy(df, matchedRowIndices, spec);
            else if (HasAggregates(spec.SelectColumns))
                results = new List<Dictionary<string, object?>> { ComputeAggregatesForGroup(df, matchedRowIndices, spec.SelectColumns) };
            else
                results = ProjectRows(df, matchedRowIndices, spec.SelectColumns);

            if (spec.Having != null && spec.Having.Count > 0)
                results = results.Where(row => MatchesRowDictFilters(row, spec.Having)).ToList();

            if (spec.OrderBy != null && spec.OrderBy.Count > 0)
            {
                foreach (var ord in spec.OrderBy)
                {
                    results = ord.Descending
                        ? results.OrderByDescending(row => row.TryGetValue(ord.Column, out var v) ? v : null).ToList()
                        : results.OrderBy(row => row.TryGetValue(ord.Column, out var v) ? v : null).ToList();
                }
            }

            int skip = spec.Offset ?? 0;
            var paged = results.Skip(skip);
            if (spec.Limit.HasValue) paged = paged.Take(spec.Limit.Value);
            return paged.ToList();
        }

        private static bool MatchesFilters(DataFrame df, int rowIdx, List<WhereConditionSpec>? filters)
        {
            if (filters == null || filters.Count == 0) return true;
            bool result = true;
            for (int i = 0; i < filters.Count; i++)
            {
                var f = filters[i];
                if (!df.HasColumn(f.Column)) continue;
                var val = df[f.Column].GetValue(rowIdx);
                bool currentMatch = CompareValues(val, f.Operator, f.Value, f.SecondValue);
                string logic = (f.LogicalOp ?? "AND").ToUpperInvariant();
                result = i == 0 ? currentMatch : (logic == "OR" ? (result || currentMatch) : (result && currentMatch));
            }
            return result;
        }

        private static bool MatchesRowDictFilters(Dictionary<string, object?> row, List<WhereConditionSpec> filters)
        {
            foreach (var f in filters)
            {
                row.TryGetValue(f.Column, out var val);
                if (!CompareValues(val, f.Operator, f.Value, f.SecondValue)) return false;
            }
            return true;
        }

        private static bool CompareValues(object? actual, string op, object? expected, object? secondExpected)
        {
            op = (op ?? "=").Trim().ToUpperInvariant();
            if (op == "IS NULL") return actual == null;
            if (op == "IS NOT NULL") return actual != null;
            if (actual == null || expected == null) return false;

            if (double.TryParse(actual.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double actNum) &&
                double.TryParse(expected.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double expNum))
            {
                switch (op)
                {
                    case "=": return Math.Abs(actNum - expNum) < 1e-9;
                    case "!=": case "<>": return Math.Abs(actNum - expNum) >= 1e-9;
                    case ">": return actNum > expNum;
                    case ">=": return actNum >= expNum;
                    case "<": return actNum < expNum;
                    case "<=": return actNum <= expNum;
                    case "BETWEEN":
                        double.TryParse(secondExpected?.ToString() ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out double num2);
                        return actNum >= Math.Min(expNum, num2) && actNum <= Math.Max(expNum, num2);
                }
            }

            string sAct = actual.ToString() ?? "", sExp = expected.ToString() ?? "";
            if (op == "LIKE") return sAct.IndexOf(sExp.Trim('%'), StringComparison.OrdinalIgnoreCase) >= 0;
            if (op == "!=" || op == "<>") return !string.Equals(sAct, sExp, StringComparison.OrdinalIgnoreCase);
            return string.Equals(sAct, sExp, StringComparison.OrdinalIgnoreCase);
        }

        private static List<Dictionary<string, object?>> ExecuteGroupBy(DataFrame df, List<int> rowIndices, AnalyticalQuerySpec spec)
        {
            var groups = rowIndices.GroupBy(r => string.Join("||", spec.GroupBy.Select(col => df.HasColumn(col) ? df[col].GetValue(r)?.ToString() : "")));
            var result = new List<Dictionary<string, object?>>();
            foreach (var group in groups)
            {
                var row = new Dictionary<string, object?>();
                int sampleIdx = group.First();
                foreach (var gCol in spec.GroupBy) if (df.HasColumn(gCol)) row[gCol] = df[gCol].GetValue(sampleIdx);
                var aggRow = ComputeAggregatesForGroup(df, group.ToList(), spec.SelectColumns);
                foreach (var kvp in aggRow) row[kvp.Key] = kvp.Value;
                result.Add(row);
            }
            return result;
        }

        private static Dictionary<string, object?> ComputeAggregatesForGroup(DataFrame df, List<int> rowIndices, List<string>? selectColumns)
        {
            var row = new Dictionary<string, object?>();
            if (selectColumns == null || selectColumns.Count == 0) return row;

            foreach (var colExpr in selectColumns)
            {
                var m = Regex.Match(colExpr, @"^(COUNT|SUM|AVG|MIN|MAX)\s*\(\s*(\*|[a-zA-Z0-9_]+)\s*\)(\s+AS\s+([a-zA-Z0-9_]+))?$", RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                string func = m.Groups[1].Value.ToUpperInvariant(), target = m.Groups[2].Value, outName = m.Groups[4].Success ? m.Groups[4].Value : colExpr;

                if (func == "COUNT") row[outName] = rowIndices.Count;
                else if (df.HasColumn(target))
                {
                    var vals = rowIndices.Select(i => Convert.ToDouble(df[target].GetValue(i) ?? 0)).ToList();
                    if (vals.Count == 0) row[outName] = 0;
                    else if (func == "SUM") row[outName] = vals.Sum();
                    else if (func == "AVG") row[outName] = Math.Round(vals.Average(), 2);
                    else if (func == "MIN") row[outName] = vals.Min();
                    else if (func == "MAX") row[outName] = vals.Max();
                }
            }
            return row;
        }

        private static List<Dictionary<string, object?>> ProjectRows(DataFrame df, List<int> rowIndices, List<string>? selectColumns)
        {
            var cols = (selectColumns == null || selectColumns.Count == 0 || selectColumns.Contains("*"))
                ? df.ColumnNames : selectColumns.Where(c => df.HasColumn(c)).ToArray();

            var list = new List<Dictionary<string, object?>>(rowIndices.Count);
            foreach (var r in rowIndices)
            {
                var dict = new Dictionary<string, object?>(cols.Count);
                foreach (var c in cols) dict[c] = df[c].GetValue(r);
                list.Add(dict);
            }
            return list;
        }

        private static bool HasAggregates(List<string>? selectColumns) =>
            selectColumns != null && selectColumns.Any(c => Regex.IsMatch(c, @"^(COUNT|SUM|AVG|MIN|MAX)\s*\(", RegexOptions.IgnoreCase));
    }
}
