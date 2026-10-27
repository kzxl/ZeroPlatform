using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using ZeroData.Sql.Dialects;
using ZeroData.Sql.Mapping;

namespace ZeroData.Sql.Sql
{
    /// <summary>
    /// One participant in a multi-table join: its mapping, alias (t1, t2, ...), join type,
    /// and the ON predicate (null for the first/driving table).
    /// </summary>
    internal class JoinSource
    {
        public EntityMapping Mapping;
        public string Alias;
        public JoinType JoinType;
        public LambdaExpression On;
    }

    /// <summary>
    /// Builds SQL for joins across 2+ tables. Lambda parameters are positional: the j-th parameter
    /// of an ON/WHERE/SELECT lambda corresponds to the j-th joined table (alias t{j+1}).
    /// Targeted at SQL Server; quoting is dialect-aware.
    /// </summary>
    internal class MultiJoinBuilder
    {
        private readonly ISqlDialect _dialect;
        private readonly Dictionary<string, object> _parameters = new Dictionary<string, object>();
        private int _paramIndex;

        public MultiJoinBuilder(ISqlDialect dialect)
        {
            _dialect = dialect ?? SqlGenerator.DefaultDialect;
        }

        public (string Sql, IDictionary<string, object> Parameters) Build(
            List<JoinSource> sources,
            LambdaExpression selector,
            List<LambdaExpression> wheres)
        {
            _parameters.Clear();
            _paramIndex = 0;

            var selectClause = BuildSelect(selector, sources);

            var sb = new StringBuilder();
            sb.Append($"SELECT {selectClause}");
            sb.Append($" FROM {QuoteTable(sources[0].Mapping)} AS {sources[0].Alias}");

            for (int i = 1; i < sources.Count; i++)
            {
                var src = sources[i];
                var keyword = src.JoinType == JoinType.Inner ? "INNER JOIN" : "LEFT JOIN";
                var on = Translate(src.On.Body, src.On.Parameters, sources);
                sb.Append($" {keyword} {QuoteTable(src.Mapping)} AS {src.Alias} ON {on}");
            }

            if (wheres != null && wheres.Count > 0)
            {
                var clauses = wheres.Select(w => Translate(w.Body, w.Parameters, sources));
                sb.Append($" WHERE {string.Join(" AND ", clauses)}");
            }

            return (sb.ToString(), _parameters);
        }

        private string QuoteTable(EntityMapping mapping)
            => SqlGenerator.QuoteTableName(mapping.TableName, _dialect);

        // ---- SELECT projection ----

        private string BuildSelect(LambdaExpression selector, List<JoinSource> sources)
        {
            var body = selector.Body;
            var cols = new List<string>();

            if (body is NewExpression newExpr)
            {
                for (int i = 0; i < newExpr.Arguments.Count; i++)
                {
                    var alias = newExpr.Members?[i]?.Name;
                    var colSql = Translate(newExpr.Arguments[i], selector.Parameters, sources);
                    cols.Add(alias != null ? $"{colSql} AS {_dialect.QuoteIdentifier(alias)}" : colSql);
                }
            }
            else if (body is MemberInitExpression initExpr)
            {
                foreach (var b in initExpr.Bindings)
                {
                    if (b is MemberAssignment ma)
                    {
                        var colSql = Translate(ma.Expression, selector.Parameters, sources);
                        cols.Add($"{colSql} AS {_dialect.QuoteIdentifier(b.Member.Name)}");
                    }
                }
            }
            else
            {
                throw new NotSupportedException(
                    "Join result selector must be an anonymous type (new { ... }) or DTO (new T { ... }).");
            }

            return string.Join(", ", cols);
        }

        // ---- expression translation (ON / WHERE / SELECT argument) ----

        private string Translate(Expression expr, IReadOnlyList<ParameterExpression> parameters, List<JoinSource> sources)
        {
            while (expr is UnaryExpression u && (u.NodeType == ExpressionType.Convert || u.NodeType == ExpressionType.ConvertChecked))
                expr = u.Operand;

            switch (expr)
            {
                case BinaryExpression b:
                    return TranslateBinary(b, parameters, sources);

                case UnaryExpression notExpr when notExpr.NodeType == ExpressionType.Not:
                    return $"NOT ({Translate(notExpr.Operand, parameters, sources)})";

                case MemberExpression m when IsTableMember(m, parameters):
                    return TranslateColumn(m, parameters, sources);

                case MemberExpression m:
                    return AddParameter(Evaluate(m));

                case ConstantExpression c:
                    return c.Value == null ? "NULL" : AddParameter(c.Value);

                case MethodCallExpression mc:
                    return TranslateMethod(mc, parameters, sources);

                default:
                    return AddParameter(Evaluate(expr));
            }
        }

        private string TranslateBinary(BinaryExpression b, IReadOnlyList<ParameterExpression> parameters, List<JoinSource> sources)
        {
            if (b.NodeType == ExpressionType.Equal || b.NodeType == ExpressionType.NotEqual)
            {
                if (IsNull(b.Right))
                    return $"{Translate(b.Left, parameters, sources)} IS {(b.NodeType == ExpressionType.Equal ? "NULL" : "NOT NULL")}";
                if (IsNull(b.Left))
                    return $"{Translate(b.Right, parameters, sources)} IS {(b.NodeType == ExpressionType.Equal ? "NULL" : "NOT NULL")}";
            }

            var left = Translate(b.Left, parameters, sources);
            var right = Translate(b.Right, parameters, sources);
            var op = OperatorFor(b.NodeType);
            return (b.NodeType == ExpressionType.AndAlso || b.NodeType == ExpressionType.OrElse)
                ? $"({left} {op} {right})"
                : $"{left} {op} {right}";
        }

        private string TranslateMethod(MethodCallExpression mc, IReadOnlyList<ParameterExpression> parameters, List<JoinSource> sources)
        {
            if (mc.Object != null && mc.Object.Type == typeof(string) && mc.Object is MemberExpression sm && IsTableMember(sm, parameters))
            {
                var col = TranslateColumn(sm, parameters, sources);
                var arg = Evaluate(mc.Arguments[0]);
                switch (mc.Method.Name)
                {
                    case "Contains": return $"{col} LIKE {AddParameter($"%{EscapeLike(arg)}%")} ESCAPE '\\'";
                    case "StartsWith": return $"{col} LIKE {AddParameter($"{EscapeLike(arg)}%")} ESCAPE '\\'";
                    case "EndsWith": return $"{col} LIKE {AddParameter($"%{EscapeLike(arg)}")} ESCAPE '\\'";
                }
            }
            return AddParameter(Evaluate(mc));
        }

        private string TranslateColumn(MemberExpression m, IReadOnlyList<ParameterExpression> parameters, List<JoinSource> sources)
        {
            var pe = (ParameterExpression)m.Expression;
            var idx = IndexOfParam(parameters, pe);
            if (idx < 0 || idx >= sources.Count)
                throw new InvalidOperationException($"Cannot resolve join table for parameter '{pe.Name}'.");

            var src = sources[idx];
            var col = src.Mapping.Columns.FirstOrDefault(c => c.Property.Name == m.Member.Name);
            if (col == null)
                throw new InvalidOperationException(
                    $"Property '{m.Member.Name}' is not a mapped column of '{src.Mapping.EntityType.Name}'.");
            return $"{src.Alias}.{_dialect.QuoteIdentifier(col.ColumnName)}";
        }

        private static bool IsTableMember(MemberExpression m, IReadOnlyList<ParameterExpression> parameters)
            => m.Expression is ParameterExpression pe && IndexOfParam(parameters, pe) >= 0;

        private static int IndexOfParam(IReadOnlyList<ParameterExpression> parameters, ParameterExpression pe)
        {
            for (int i = 0; i < parameters.Count; i++)
                if (ReferenceEquals(parameters[i], pe) || parameters[i].Name == pe.Name) return i;
            return -1;
        }

        private static string OperatorFor(ExpressionType type)
        {
            switch (type)
            {
                case ExpressionType.Equal: return "=";
                case ExpressionType.NotEqual: return "<>";
                case ExpressionType.LessThan: return "<";
                case ExpressionType.LessThanOrEqual: return "<=";
                case ExpressionType.GreaterThan: return ">";
                case ExpressionType.GreaterThanOrEqual: return ">=";
                case ExpressionType.AndAlso: return "AND";
                case ExpressionType.OrElse: return "OR";
                case ExpressionType.Add: return "+";
                case ExpressionType.Subtract: return "-";
                case ExpressionType.Multiply: return "*";
                case ExpressionType.Divide: return "/";
                default: throw new NotSupportedException($"Operator '{type}' is not supported in a join expression.");
            }
        }

        private static bool IsNull(Expression e)
        {
            if (e is ConstantExpression c && c.Value == null) return true;
            if (e is UnaryExpression u && u.NodeType == ExpressionType.Convert) return IsNull(u.Operand);
            return false;
        }

        private static string EscapeLike(object value)
        {
            if (value == null) return null;
            return value.ToString().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        }

        private static object Evaluate(Expression expr)
        {
            if (expr is ConstantExpression c) return c.Value;
            var lambda = Expression.Lambda<Func<object>>(Expression.Convert(expr, typeof(object)));
            return ExpressionCache.GetOrAddFunc<object>(lambda)();
        }

        private string AddParameter(object value)
        {
            var name = $"@j{_paramIndex++}";
            _parameters[name] = value;
            return name;
        }
    }
}
