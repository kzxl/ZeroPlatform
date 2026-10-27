using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ZeroData.Sql.Dialects;
using ZeroData.Sql.Mapping;

namespace ZeroData.Sql.Sql
{
    /// <summary>
    /// Builds correlated subquery WHERE fragments (EXISTS / NOT EXISTS / IN / NOT IN) for SQL Server.
    /// The inner table is given an alias; the correlation predicate may reference both the outer
    /// entity (by qualified table name) and the inner entity (by alias).
    /// </summary>
    internal class SubqueryBuilder
    {
        private readonly EntityMapping _outerMapping;
        private readonly EntityMapping _innerMapping;
        private readonly ISqlDialect _dialect;
        private readonly string _innerAlias;
        private readonly string _outerQualifier;
        private readonly Dictionary<string, object> _parameters = new Dictionary<string, object>();
        private int _paramIndex;

        public SubqueryBuilder(EntityMapping outerMapping, EntityMapping innerMapping,
            ISqlDialect dialect, string innerAlias, int paramSeed)
        {
            _outerMapping = outerMapping;
            _innerMapping = innerMapping;
            _dialect = dialect ?? SqlGenerator.DefaultDialect;
            _innerAlias = innerAlias;
            _paramIndex = paramSeed;
            // Outer columns are qualified by the full (quoted) table name so they don't clash with the inner alias.
            _outerQualifier = SqlGenerator.QuoteTableName(_outerMapping.TableName, _dialect);
        }

        /// <summary>
        /// Builds an EXISTS / NOT EXISTS fragment.
        /// </summary>
        public (string Sql, IDictionary<string, object> Parameters) BuildExists<TOuter, TInner>(
            Expression<Func<TOuter, TInner, bool>> correlation, bool negate)
        {
            var innerTable = SqlGenerator.QuoteTableName(_innerMapping.TableName, _dialect);
            var cond = Visit(correlation.Body, correlation.Parameters[0], correlation.Parameters[1]);
            var keyword = negate ? "NOT EXISTS" : "EXISTS";
            var sql = $"{keyword} (SELECT 1 FROM {innerTable} AS {_innerAlias} WHERE {cond})";
            return (sql, _parameters);
        }

        /// <summary>
        /// Builds an IN / NOT IN fragment: outerKey IN (SELECT innerKey FROM inner [WHERE filter]).
        /// </summary>
        public (string Sql, IDictionary<string, object> Parameters) BuildIn<TOuter, TInner, TKey>(
            Expression<Func<TOuter, TKey>> outerKey,
            Expression<Func<TInner, TKey>> innerKey,
            Expression<Func<TInner, bool>> innerFilter,
            bool negate)
        {
            var innerTable = SqlGenerator.QuoteTableName(_innerMapping.TableName, _dialect);
            var outerCol = $"{_outerQualifier}.{_dialect.QuoteIdentifier(ExtractColumn(outerKey.Body, _outerMapping))}";
            var innerCol = $"{_innerAlias}.{_dialect.QuoteIdentifier(ExtractColumn(innerKey.Body, _innerMapping))}";

            var sub = $"SELECT {innerCol} FROM {innerTable} AS {_innerAlias}";
            if (innerFilter != null)
            {
                var filterSql = VisitInner(innerFilter.Body, innerFilter.Parameters[0]);
                sub += $" WHERE {filterSql}";
            }

            var keyword = negate ? "NOT IN" : "IN";
            var sql = $"{outerCol} {keyword} ({sub})";
            return (sql, _parameters);
        }

        // ---- correlation predicate over (outer, inner) ----

        private string Visit(Expression expr, ParameterExpression outer, ParameterExpression inner)
        {
            while (expr is UnaryExpression u && u.NodeType == ExpressionType.Convert)
                expr = u.Operand;

            switch (expr)
            {
                case BinaryExpression b:
                    return VisitBinary(b, outer, inner);
                case UnaryExpression notExpr when notExpr.NodeType == ExpressionType.Not:
                    return $"NOT ({Visit(notExpr.Operand, outer, inner)})";
                case MemberExpression m when IsParam(m, outer):
                    return $"{_outerQualifier}.{_dialect.QuoteIdentifier(ExtractColumn(m, _outerMapping))}";
                case MemberExpression m when IsParam(m, inner):
                    return $"{_innerAlias}.{_dialect.QuoteIdentifier(ExtractColumn(m, _innerMapping))}";
                case MemberExpression m:
                    return AddParameter(Evaluate(m));
                case ConstantExpression c:
                    return c.Value == null ? "NULL" : AddParameter(c.Value);
                default:
                    return AddParameter(Evaluate(expr));
            }
        }

        private string VisitBinary(BinaryExpression b, ParameterExpression outer, ParameterExpression inner)
        {
            if (b.NodeType == ExpressionType.Equal || b.NodeType == ExpressionType.NotEqual)
            {
                if (IsNull(b.Right)) return $"{Visit(b.Left, outer, inner)} IS {(b.NodeType == ExpressionType.Equal ? "NULL" : "NOT NULL")}";
                if (IsNull(b.Left)) return $"{Visit(b.Right, outer, inner)} IS {(b.NodeType == ExpressionType.Equal ? "NULL" : "NOT NULL")}";
            }
            var left = Visit(b.Left, outer, inner);
            var right = Visit(b.Right, outer, inner);
            var op = OperatorFor(b.NodeType);
            return b.NodeType == ExpressionType.AndAlso || b.NodeType == ExpressionType.OrElse
                ? $"({left} {op} {right})"
                : $"{left} {op} {right}";
        }

        // ---- inner-only filter predicate ----

        private string VisitInner(Expression expr, ParameterExpression inner)
        {
            while (expr is UnaryExpression u && u.NodeType == ExpressionType.Convert)
                expr = u.Operand;

            switch (expr)
            {
                case BinaryExpression b:
                    {
                        if (b.NodeType == ExpressionType.Equal || b.NodeType == ExpressionType.NotEqual)
                        {
                            if (IsNull(b.Right)) return $"{VisitInner(b.Left, inner)} IS {(b.NodeType == ExpressionType.Equal ? "NULL" : "NOT NULL")}";
                            if (IsNull(b.Left)) return $"{VisitInner(b.Right, inner)} IS {(b.NodeType == ExpressionType.Equal ? "NULL" : "NOT NULL")}";
                        }
                        var left = VisitInner(b.Left, inner);
                        var right = VisitInner(b.Right, inner);
                        var op = OperatorFor(b.NodeType);
                        return b.NodeType == ExpressionType.AndAlso || b.NodeType == ExpressionType.OrElse
                            ? $"({left} {op} {right})"
                            : $"{left} {op} {right}";
                    }
                case UnaryExpression notExpr when notExpr.NodeType == ExpressionType.Not:
                    return $"NOT ({VisitInner(notExpr.Operand, inner)})";
                case MemberExpression m when IsParam(m, inner):
                    return $"{_innerAlias}.{_dialect.QuoteIdentifier(ExtractColumn(m, _innerMapping))}";
                case MemberExpression m:
                    return AddParameter(Evaluate(m));
                case ConstantExpression c:
                    return c.Value == null ? "NULL" : AddParameter(c.Value);
                default:
                    return AddParameter(Evaluate(expr));
            }
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
                default: throw new NotSupportedException($"Operator '{type}' is not supported in a subquery predicate.");
            }
        }

        private static bool IsParam(MemberExpression m, ParameterExpression p)
            => m.Expression is ParameterExpression pe && pe.Name == p.Name;

        private static bool IsNull(Expression e)
        {
            if (e is ConstantExpression c && c.Value == null) return true;
            if (e is UnaryExpression u && u.NodeType == ExpressionType.Convert) return IsNull(u.Operand);
            return false;
        }

        private string ExtractColumn(Expression expr, EntityMapping mapping)
        {
            if (expr is UnaryExpression u && u.NodeType == ExpressionType.Convert) expr = u.Operand;
            if (expr is MemberExpression m)
            {
                var col = mapping.Columns.FirstOrDefault(c => c.Property.Name == m.Member.Name);
                if (col == null)
                    throw new InvalidOperationException($"Property '{m.Member.Name}' is not a mapped column of '{mapping.EntityType.Name}'.");
                return col.ColumnName;
            }
            throw new NotSupportedException($"Expected a simple property access, got: {expr}");
        }

        private static object Evaluate(Expression expr)
        {
            if (expr is ConstantExpression c) return c.Value;
            var lambda = Expression.Lambda<Func<object>>(Expression.Convert(expr, typeof(object)));
            return ExpressionCache.GetOrAddFunc<object>(lambda)();
        }

        private string AddParameter(object value)
        {
            var name = $"@sub{_paramIndex++}";
            _parameters[name] = value;
            return name;
        }

        public int NextParamIndex => _paramIndex;
    }
}
