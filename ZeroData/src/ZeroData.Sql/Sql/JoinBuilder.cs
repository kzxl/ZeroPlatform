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
    /// Builds SQL JOIN queries from LINQ expressions.
    /// Supports INNER JOIN and LEFT JOIN operations.
    /// </summary>
    public class JoinBuilder
    {
        private readonly EntityMapping _mapping1;
        private readonly EntityMapping _mapping2;
        private readonly ISqlDialect _dialect;
        private readonly IDictionary<string, object> _parameters;
        private int _paramIndex;

        public JoinBuilder(EntityMapping mapping1, EntityMapping mapping2, ISqlDialect dialect = null)
        {
            _mapping1 = mapping1 ?? throw new ArgumentNullException(nameof(mapping1));
            _mapping2 = mapping2 ?? throw new ArgumentNullException(nameof(mapping2));
            _dialect = dialect ?? SqlGenerator.DefaultDialect;
            _parameters = new Dictionary<string, object>();
        }

        /// <summary>
        /// Builds a complete JOIN SQL query with optional WHERE predicates over both entities.
        /// </summary>
        public (string Sql, IDictionary<string, object> Parameters) BuildJoinQuery<T1, T2, TResult>(
            Expression<Func<T1, object>> outerKeySelector,
            Expression<Func<T2, object>> innerKeySelector,
            Expression<Func<T1, T2, TResult>> resultSelector,
            JoinType joinType,
            IReadOnlyList<Expression<Func<T1, T2, bool>>> wherePredicates)
        {
            _parameters.Clear();
            _paramIndex = 0;

            var outerKey = ExtractColumnName(outerKeySelector.Body, _mapping1);
            var innerKey = ExtractColumnName(innerKeySelector.Body, _mapping2);
            var selectClause = BuildSelectClause(resultSelector);

            var joinKeyword = joinType == JoinType.Inner ? "INNER JOIN" : "LEFT JOIN";
            var table1 = SqlGenerator.QuoteTableName(_mapping1.TableName, _dialect);
            var table2 = SqlGenerator.QuoteTableName(_mapping2.TableName, _dialect);
            var joinClause = $"{joinKeyword} {table2} AS t2 ON t1.{_dialect.QuoteIdentifier(outerKey)} = t2.{_dialect.QuoteIdentifier(innerKey)}";

            var sql = new StringBuilder();
            sql.Append($"SELECT {selectClause}");
            sql.Append($" FROM {table1} AS t1");
            sql.Append($" {joinClause}");

            if (wherePredicates != null && wherePredicates.Count > 0)
            {
                var clauses = new List<string>();
                foreach (var predicate in wherePredicates)
                    clauses.Add(VisitJoinWhere(predicate.Body, predicate.Parameters[0], predicate.Parameters[1]));
                sql.Append($" WHERE {string.Join(" AND ", clauses)}");
            }

            return (sql.ToString(), _parameters);
        }

        /// <summary>
        /// Translates a WHERE predicate referencing both joined entities into aliased SQL.
        /// </summary>
        private string VisitJoinWhere(Expression expr, ParameterExpression p1, ParameterExpression p2)
        {
            switch (expr)
            {
                case UnaryExpression unary when unary.NodeType == ExpressionType.Convert:
                    return VisitJoinWhere(unary.Operand, p1, p2);

                case UnaryExpression unary when unary.NodeType == ExpressionType.Not:
                    return $"NOT ({VisitJoinWhere(unary.Operand, p1, p2)})";

                case BinaryExpression binary:
                    return VisitJoinWhereBinary(binary, p1, p2);

                case MemberExpression member when IsEntityMember(member, p1, p2):
                    return TranslateSelectArgument(member, p1, p2);

                case MethodCallExpression method:
                    return VisitJoinWhereMethod(method, p1, p2);

                case ConstantExpression constant:
                    return constant.Value == null ? "NULL" : AddParameter(constant.Value);

                default:
                    return AddParameter(EvaluateJoinValue(expr));
            }
        }

        private string VisitJoinWhereBinary(BinaryExpression binary, ParameterExpression p1, ParameterExpression p2)
        {
            // null comparison handling
            if ((binary.NodeType == ExpressionType.Equal || binary.NodeType == ExpressionType.NotEqual))
            {
                Expression memberSide = null;
                if (IsNull(binary.Right)) memberSide = binary.Left;
                else if (IsNull(binary.Left)) memberSide = binary.Right;
                if (memberSide != null)
                {
                    var col = VisitJoinWhere(memberSide, p1, p2);
                    return binary.NodeType == ExpressionType.Equal ? $"{col} IS NULL" : $"{col} IS NOT NULL";
                }
            }

            var left = VisitJoinWhere(binary.Left, p1, p2);
            var right = VisitJoinWhere(binary.Right, p1, p2);

            string op;
            switch (binary.NodeType)
            {
                case ExpressionType.Equal: op = "="; break;
                case ExpressionType.NotEqual: op = "<>"; break;
                case ExpressionType.LessThan: op = "<"; break;
                case ExpressionType.LessThanOrEqual: op = "<="; break;
                case ExpressionType.GreaterThan: op = ">"; break;
                case ExpressionType.GreaterThanOrEqual: op = ">="; break;
                case ExpressionType.AndAlso: op = "AND"; break;
                case ExpressionType.OrElse: op = "OR"; break;
                default:
                    throw new NotSupportedException($"Binary operator '{binary.NodeType}' is not supported in join WHERE.");
            }

            if (binary.NodeType == ExpressionType.AndAlso || binary.NodeType == ExpressionType.OrElse)
                return $"({left} {op} {right})";
            return $"{left} {op} {right}";
        }

        private string VisitJoinWhereMethod(MethodCallExpression method, ParameterExpression p1, ParameterExpression p2)
        {
            if (method.Object != null && method.Object.Type == typeof(string)
                && IsEntityMember(method.Object as MemberExpression, p1, p2))
            {
                var column = VisitJoinWhere(method.Object, p1, p2);
                var arg = EvaluateJoinValue(method.Arguments[0]);
                switch (method.Method.Name)
                {
                    case "Contains": return $"{column} LIKE {AddParameter($"%{Escape(arg)}%")} ESCAPE '\\'";
                    case "StartsWith": return $"{column} LIKE {AddParameter($"{Escape(arg)}%")} ESCAPE '\\'";
                    case "EndsWith": return $"{column} LIKE {AddParameter($"%{Escape(arg)}")} ESCAPE '\\'";
                }
            }
            return AddParameter(EvaluateJoinValue(method));
        }

        private bool IsEntityMember(MemberExpression member, ParameterExpression p1, ParameterExpression p2)
        {
            return member != null && member.Expression is ParameterExpression pe
                && (pe.Name == p1.Name || pe.Name == p2.Name);
        }

        private static bool IsNull(Expression expr)
        {
            if (expr is ConstantExpression c && c.Value == null) return true;
            if (expr is UnaryExpression u && u.NodeType == ExpressionType.Convert) return IsNull(u.Operand);
            return false;
        }

        private static string Escape(object value)
        {
            if (value == null) return null;
            return value.ToString()
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
        }

        private static object EvaluateJoinValue(Expression expr)
        {
            if (expr is ConstantExpression c) return c.Value;
            if (expr is UnaryExpression u && u.NodeType == ExpressionType.Convert)
                return EvaluateJoinValue(u.Operand);
            var lambda = Expression.Lambda<Func<object>>(Expression.Convert(expr, typeof(object)));
            return ExpressionCache.GetOrAddFunc<object>(lambda)();
        }

        private string AddParameter(object value)
        {
            var name = $"@j{_paramIndex++}";
            _parameters[name] = value;
            return name;
        }

        /// <summary>
        /// Extracts column name from a key selector expression.
        /// </summary>
        private string ExtractColumnName(Expression expr, EntityMapping mapping)
        {
            // Unwrap Convert if present
            if (expr is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                expr = unary.Operand;

            if (expr is MemberExpression member && member.Expression is ParameterExpression)
            {
                var propName = member.Member.Name;
                var col = mapping.Columns.FirstOrDefault(c => c.Property.Name == propName);
                if (col == null)
                    throw new InvalidOperationException(
                        $"Property '{propName}' is not a mapped column of '{mapping.EntityType.Name}'.");
                return col.ColumnName;
            }

            throw new NotSupportedException(
                $"Join key expression '{expr}' must be a simple property access (e.g., x => x.Id).");
        }

        /// <summary>
        /// Builds SELECT clause from result selector expression.
        /// </summary>
        private string BuildSelectClause<T1, T2, TResult>(Expression<Func<T1, T2, TResult>> resultSelector)
        {
            var body = resultSelector.Body;
            var columns = new List<string>();

            if (body is NewExpression newExpr)
            {
                // Anonymous type: new { o.Id, c.Name }
                for (int i = 0; i < newExpr.Arguments.Count; i++)
                {
                    var arg = newExpr.Arguments[i];
                    var alias = newExpr.Members?[i]?.Name;

                    var columnSql = TranslateSelectArgument(arg, resultSelector.Parameters[0], resultSelector.Parameters[1]);

                    if (!string.IsNullOrEmpty(alias))
                        columns.Add($"{columnSql} AS {_dialect.QuoteIdentifier(alias)}");
                    else
                        columns.Add(columnSql);
                }
            }
            else if (body is MemberInitExpression initExpr)
            {
                // DTO: new DTO { OrderId = o.Id, CustomerName = c.Name }
                foreach (var binding in initExpr.Bindings)
                {
                    if (binding is MemberAssignment assignment)
                    {
                        var columnSql = TranslateSelectArgument(assignment.Expression, resultSelector.Parameters[0], resultSelector.Parameters[1]);
                        columns.Add($"{columnSql} AS {_dialect.QuoteIdentifier(binding.Member.Name)}");
                    }
                }
            }
            else
            {
                throw new NotSupportedException(
                    $"Result selector expression type '{body.NodeType}' is not supported. " +
                    $"Use anonymous types (new {{ }}) or DTOs (new DTO {{ }}).");
            }

            return string.Join(", ", columns);
        }

        /// <summary>
        /// Translates a single SELECT argument to SQL column reference.
        /// </summary>
        private string TranslateSelectArgument(Expression expr, ParameterExpression param1, ParameterExpression param2)
        {
            // Unwrap Convert if present
            if (expr is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                expr = unary.Operand;

            if (expr is MemberExpression member && member.Expression is ParameterExpression param)
            {
                var propName = member.Member.Name;

                // Determine which table this property belongs to
                if (param.Name == param1.Name)
                {
                    var col = _mapping1.Columns.FirstOrDefault(c => c.Property.Name == propName);
                    if (col == null)
                        throw new InvalidOperationException(
                            $"Property '{propName}' is not a mapped column of '{_mapping1.EntityType.Name}'.");
                    return $"t1.{_dialect.QuoteIdentifier(col.ColumnName)}";
                }
                else if (param.Name == param2.Name)
                {
                    var col = _mapping2.Columns.FirstOrDefault(c => c.Property.Name == propName);
                    if (col == null)
                        throw new InvalidOperationException(
                            $"Property '{propName}' is not a mapped column of '{_mapping2.EntityType.Name}'.");
                    return $"t2.{_dialect.QuoteIdentifier(col.ColumnName)}";
                }
            }

            throw new NotSupportedException(
                $"Select argument '{expr}' is not supported. " +
                $"Use simple property access (e.g., o.Id, c.Name).");
        }
    }
}
