using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using ZeroData.Sql.Dialects;

namespace ZeroAgent.Tools.Data
{
    /// <summary>
    /// Generates sanitized, parameterized SQL queries from AnalyticalQuerySpec with dialect awareness.
    /// Defends against SQL injection through identifier validation and strict parameterization.
    /// </summary>
    public static class DynamicSqlBuilder
    {
        private static readonly Regex SafeIdRegex = new Regex(@"^[a-zA-Z_][a-zA-Z0-9_\.]*$", RegexOptions.Compiled);
        private static readonly Regex DangerousSqlRegex = new Regex(@"(;|--|/\*|\*/|\bxp_|\bdrop\b|\bdelete\b|\bupdate\b|\binsert\b|\balter\b|\bexec\b|\btruncate\b|\bmerge\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static SqlBuildResult Build(AnalyticalQuerySpec spec, IReadOnlyDictionary<string, TableMetadata>? catalog = null, ISqlDialect? dialect = null)
        {
            dialect ??= new SqlServerDialect();
            var result = new SqlBuildResult();

            if (spec == null || string.IsNullOrWhiteSpace(spec.Table))
                return Fail(result, "Query specification or target table name cannot be empty.");

            if (DangerousSqlRegex.IsMatch(spec.Table) || !SafeIdRegex.IsMatch(spec.Table))
                return Fail(result, $"Invalid or dangerous table name: '{spec.Table}'.");

            var sb = new StringBuilder("SELECT ");
            int paramIndex = 0;

            // 1. SELECT Columns
            if (spec.SelectColumns == null || spec.SelectColumns.Count == 0) sb.Append("*");
            else
            {
                for (int i = 0; i < spec.SelectColumns.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    string col = spec.SelectColumns[i].Trim();
                    if (DangerousSqlRegex.IsMatch(col)) return Fail(result, $"Malicious expression in SELECT: '{col}'.");
                    sb.Append(FormatSelectExpression(col, dialect));
                }
            }

            // 2. FROM clause
            sb.Append(" FROM ").Append(QuoteIdentifier(spec.Table, dialect));

            // 3. JOINs
            if (spec.Joins != null)
            {
                foreach (var j in spec.Joins)
                {
                    if (string.IsNullOrWhiteSpace(j.Table) || DangerousSqlRegex.IsMatch(j.Table) || !SafeIdRegex.IsMatch(j.Table))
                        return Fail(result, $"Invalid JOIN table: '{j.Table}'.");

                    string jType = (j.JoinType ?? "INNER").ToUpperInvariant();
                    if (jType != "INNER" && jType != "LEFT" && jType != "RIGHT") jType = "INNER";
                    sb.Append($" {jType} JOIN ").Append(QuoteIdentifier(j.Table, dialect))
                      .Append(" ON ").Append(QuoteIdentifier(j.LeftColumn, dialect)).Append(" = ").Append(QuoteIdentifier(j.RightColumn, dialect));
                }
            }

            // 4. WHERE clause
            if (spec.Filters != null && spec.Filters.Count > 0)
            {
                sb.Append(" WHERE ");
                if (!AppendConditions(sb, spec.Filters, result.Parameters, dialect, ref paramIndex, out string? err))
                    return Fail(result, err ?? "Invalid WHERE filter condition.");
            }

            // 5. GROUP BY
            if (spec.GroupBy != null && spec.GroupBy.Count > 0)
            {
                sb.Append(" GROUP BY ");
                for (int i = 0; i < spec.GroupBy.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(QuoteIdentifier(spec.GroupBy[i].Trim(), dialect));
                }
            }

            // 6. HAVING
            if (spec.Having != null && spec.Having.Count > 0)
            {
                sb.Append(" HAVING ");
                if (!AppendConditions(sb, spec.Having, result.Parameters, dialect, ref paramIndex, out string? err))
                    return Fail(result, err ?? "Invalid HAVING filter condition.");
            }

            // 7. ORDER BY
            if (spec.OrderBy != null && spec.OrderBy.Count > 0)
            {
                sb.Append(" ORDER BY ");
                for (int i = 0; i < spec.OrderBy.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(QuoteIdentifier(spec.OrderBy[i].Column.Trim(), dialect)).Append(spec.OrderBy[i].Descending ? " DESC" : " ASC");
                }
            }
            else if ((spec.Limit.HasValue || spec.Offset.HasValue) && dialect.ProviderName == "SQL Server")
            {
                sb.Append(" ORDER BY (SELECT NULL)");
            }

            // 8. Pagination (Limit / Offset)
            string limitClause = dialect.GetLimitClause(spec.Offset, spec.Limit);
            if (!string.IsNullOrWhiteSpace(limitClause)) sb.Append(" ").Append(limitClause);

            result.Sql = sb.ToString();
            result.IsValid = true;
            return result;
        }

        private static bool AppendConditions(StringBuilder sb, List<WhereConditionSpec> conds, Dictionary<string, object?> parameters, ISqlDialect dialect, ref int paramIdx, out string? err)
        {
            err = null;
            for (int i = 0; i < conds.Count; i++)
            {
                var cond = conds[i];
                if (i > 0) sb.Append((cond.LogicalOp ?? "AND").ToUpperInvariant() == "OR" ? " OR " : " AND ");

                if (string.IsNullOrWhiteSpace(cond.Column) || DangerousSqlRegex.IsMatch(cond.Column))
                {
                    err = $"Invalid condition column: '{cond.Column}'.";
                    return false;
                }

                string op = (cond.Operator ?? "=").Trim().ToUpperInvariant();
                string qCol = FormatSelectExpression(cond.Column, dialect);

                if (op == "IS NULL" || op == "IS NOT NULL") sb.Append($"{qCol} {op}");
                else if (op == "BETWEEN")
                {
                    string p1 = $"{dialect.ParameterPrefix}p{paramIdx++}", p2 = $"{dialect.ParameterPrefix}p{paramIdx++}";
                    parameters[p1] = cond.Value;
                    parameters[p2] = cond.SecondValue;
                    sb.Append($"{qCol} BETWEEN {p1} AND {p2}");
                }
                else
                {
                    string pName = $"{dialect.ParameterPrefix}p{paramIdx++}";
                    parameters[pName] = cond.Value;
                    sb.Append($"{qCol} {op} {pName}");
                }
            }
            return true;
        }

        private static string FormatSelectExpression(string expr, ISqlDialect dialect)
        {
            if (expr == "*") return "*";
            var match = Regex.Match(expr, @"^(COUNT|SUM|AVG|MIN|MAX)\s*\(\s*(\*|[a-zA-Z0-9_\.]+)\s*\)(\s+AS\s+([a-zA-Z0-9_]+))?$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string func = match.Groups[1].Value.ToUpperInvariant();
                string arg = match.Groups[2].Value == "*" ? "*" : QuoteIdentifier(match.Groups[2].Value, dialect);
                string res = $"{func}({arg})";
                if (match.Groups[4].Success) res += $" AS {QuoteIdentifier(match.Groups[4].Value, dialect)}";
                return res;
            }
            var aliasMatch = Regex.Match(expr, @"^([a-zA-Z0-9_\.]+)\s+AS\s+([a-zA-Z0-9_]+)$", RegexOptions.IgnoreCase);
            if (aliasMatch.Success)
                return $"{QuoteIdentifier(aliasMatch.Groups[1].Value, dialect)} AS {QuoteIdentifier(aliasMatch.Groups[2].Value, dialect)}";
            return QuoteIdentifier(expr, dialect);
        }

        private static string QuoteIdentifier(string id, ISqlDialect dialect)
        {
            if (id.Contains("."))
            {
                var parts = id.Split('.');
                return $"{dialect.QuoteIdentifier(parts[0])}.{dialect.QuoteIdentifier(parts[1])}";
            }
            return dialect.QuoteIdentifier(id);
        }

        private static SqlBuildResult Fail(SqlBuildResult res, string error)
        {
            res.IsValid = false;
            res.ErrorMessage = error;
            return res;
        }
    }
}
