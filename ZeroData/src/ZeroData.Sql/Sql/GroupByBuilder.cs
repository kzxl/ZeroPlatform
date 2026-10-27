using ZeroData.Sql.Dialects;
using ZeroData.Sql.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace ZeroData.Sql.Sql
{
    /// <summary>
    /// Translates LINQ GroupBy expressions into SQL GROUP BY queries with aggregates.
    /// Handles single and multi-column grouping, aggregate functions (Count, Sum, Average, Min, Max),
    /// HAVING clauses, and ORDER BY on aggregates or keys.
    /// </summary>
    public class GroupByBuilder
    {
        private readonly EntityMapping _mapping;
        private readonly ISqlDialect _dialect;
        private readonly IDictionary<string, object> _parameters;
        private int _paramIndex;
        private List<GroupByColumn> _groupByColumns;

        public GroupByBuilder(EntityMapping mapping, ISqlDialect dialect = null)
        {
            _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
            _dialect = dialect ?? SqlGenerator.DefaultDialect;
            _parameters = new Dictionary<string, object>();
        }

        /// <summary>
        /// Builds the complete GROUP BY SQL query.
        /// </summary>
        /// <param name="tableName">The table name (e.g., "[Orders]")</param>
        /// <param name="groupByExpression">The GroupBy keySelector expression</param>
        /// <param name="selectExpression">The Select projection expression</param>
        /// <param name="whereClause">Optional WHERE clause SQL</param>
        /// <param name="whereParameters">Optional WHERE clause parameters</param>
        /// <param name="havingExpression">Optional HAVING predicate expression</param>
        /// <param name="orderByClauses">Optional ORDER BY clauses</param>
        /// <returns>Complete SQL query and parameters</returns>
        public (string Sql, IDictionary<string, object> Parameters) BuildGroupByQuery(
            string tableName,
            LambdaExpression groupByExpression,
            LambdaExpression selectExpression,
            string whereClause,
            IDictionary<string, object> whereParameters,
            LambdaExpression havingExpression,
            List<string> orderByClauses)
        {
            _parameters.Clear();
            _paramIndex = 0;

            // Copy WHERE parameters if present
            if (whereParameters != null)
            {
                foreach (var kvp in whereParameters)
                    _parameters[kvp.Key] = kvp.Value;
            }

            // Extract GROUP BY columns
            _groupByColumns = ExtractGroupByColumns(groupByExpression);

            // Build SELECT clause with aggregates
            var selectClause = BuildSelectClause(selectExpression);

            // Build GROUP BY clause. A single constant key (o => 1) means "all rows in one group";
            // emit no GROUP BY at all so it works across providers (SQL Server rejects GROUP BY 1).
            bool literalGroup = _groupByColumns.Count == 1 && _groupByColumns[0].IsLiteral;
            var groupByClause = literalGroup
                ? null
                : string.Join(", ", _groupByColumns.Select(c => _dialect.QuoteIdentifier(c.ColumnName)));

            // Build HAVING clause if present
            string havingClause = null;
            if (havingExpression != null)
            {
                havingClause = BuildHavingClause(havingExpression);
            }

            // Build ORDER BY clause if present
            string orderByClause = null;
            if (orderByClauses != null && orderByClauses.Count > 0)
            {
                orderByClause = string.Join(", ", orderByClauses);
            }

            // Combine all parts
            var sql = new StringBuilder();
            sql.Append($"SELECT {selectClause}");
            sql.Append($" FROM {tableName}");

            if (!string.IsNullOrEmpty(whereClause))
                sql.Append($" WHERE {whereClause}");

            if (!string.IsNullOrEmpty(groupByClause))
                sql.Append($" GROUP BY {groupByClause}");

            if (!string.IsNullOrEmpty(havingClause))
                sql.Append($" HAVING {havingClause}");

            if (!string.IsNullOrEmpty(orderByClause))
                sql.Append($" ORDER BY {orderByClause}");

            return (sql.ToString(), _parameters);
        }

        /// <summary>
        /// Extracts GROUP BY column names from the keySelector expression.
        /// Handles both single column (o => o.CustomerId) and multi-column (o => new { o.CustomerId, o.Year }).
        /// </summary>
        private List<GroupByColumn> ExtractGroupByColumns(LambdaExpression keySelector)
        {
            var columns = new List<GroupByColumn>();
            var body = keySelector.Body;

            // Unwrap Convert if present
            if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                body = unary.Operand;

            if (body is NewExpression newExpr)
            {
                // Multi-column: new { o.CustomerId, o.Year }
                for (int i = 0; i < newExpr.Arguments.Count; i++)
                {
                    var arg = newExpr.Arguments[i];
                    var memberName = newExpr.Members?[i]?.Name;

                    var propName = GetPropertyName(arg);
                    if (propName == null)
                        throw new NotSupportedException(
                            $"GroupBy key argument '{arg}' must be a simple property access.");

                    var col = FindColumn(propName);
                    columns.Add(new GroupByColumn
                    {
                        ColumnName = col.ColumnName,
                        PropertyName = memberName ?? propName
                    });
                }
            }
            else if (body is MemberExpression member)
            {
                // Single column: o => o.CustomerId
                var propName = GetPropertyName(member);
                if (propName == null)
                    throw new NotSupportedException(
                        $"GroupBy key '{member}' must be a property of the entity.");

                var col = FindColumn(propName);
                columns.Add(new GroupByColumn
                {
                    ColumnName = col.ColumnName,
                    PropertyName = propName
                });
            }
            else if (body is ConstantExpression constant)
            {
                // Constant grouping: o => 1 (all rows in one group). Emit as a literal, unquoted.
                columns.Add(new GroupByColumn
                {
                    ColumnName = constant.Value?.ToString() ?? "1",
                    PropertyName = "Key",
                    IsLiteral = true
                });
            }
            else
            {
                throw new NotSupportedException(
                    $"GroupBy key expression type '{body.NodeType}' is not supported. " +
                    $"Use a single property (o => o.CustomerId) or anonymous type (o => new {{ o.CustomerId, o.Year }}).");
            }

            return columns;
        }

        /// <summary>
        /// Translates the Select expression to SQL SELECT clause with aggregates.
        /// Handles g.Key, g.Key.PropertyName, and aggregate functions.
        /// </summary>
        private string BuildSelectClause(LambdaExpression selectExpression)
        {
            var body = selectExpression.Body;
            var columns = new List<string>();

            if (body is NewExpression newExpr)
            {
                // Anonymous type: new { g.Key, Count = g.Count(), Total = g.Sum(x => x.Amount) }
                for (int i = 0; i < newExpr.Arguments.Count; i++)
                {
                    var arg = newExpr.Arguments[i];
                    var alias = newExpr.Members?[i]?.Name;

                    var columnSql = TranslateSelectArgument(arg);

                    if (!string.IsNullOrEmpty(alias))
                        columns.Add($"{columnSql} AS {_dialect.QuoteIdentifier(alias)}");
                    else
                        columns.Add(columnSql);
                }
            }
            else if (body is MemberInitExpression initExpr)
            {
                // DTO: new DTO { CustomerId = g.Key, Count = g.Count() }
                foreach (var binding in initExpr.Bindings)
                {
                    if (binding is MemberAssignment assignment)
                    {
                        var columnSql = TranslateSelectArgument(assignment.Expression);
                        columns.Add($"{columnSql} AS {_dialect.QuoteIdentifier(binding.Member.Name)}");
                    }
                }
            }
            else
            {
                throw new NotSupportedException(
                    $"Select expression type '{body.NodeType}' is not supported after GroupBy. " +
                    $"Use anonymous types (new {{ }}) or DTOs (new DTO {{ }}).");
            }

            return string.Join(", ", columns);
        }

        /// <summary>
        /// Translates a single Select argument (g.Key, g.Count(), etc.) to SQL.
        /// </summary>
        private string TranslateSelectArgument(Expression expr)
        {
            // Unwrap Convert if present
            if (expr is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                expr = unary.Operand;

            // Check for aggregate method calls: g.Count(), g.Sum(x => x.Amount)
            if (expr is MethodCallExpression methodCall)
            {
                return TranslateAggregate(methodCall);
            }

            // Check for g.Key access
            if (expr is MemberExpression member)
            {
                return TranslateKeyAccess(member);
            }

            throw new NotSupportedException(
                $"Select argument '{expr}' is not supported. " +
                $"Use g.Key, g.Key.PropertyName, or aggregate functions (g.Count(), g.Sum(), etc.).");
        }

        /// <summary>
        /// Translates g.Key or g.Key.PropertyName to SQL column name(s).
        /// </summary>
        private string TranslateKeyAccess(MemberExpression member)
        {
            // Check if this is g.Key
            if (member.Member.Name == "Key" && member.Expression is ParameterExpression)
            {
                // Single column GroupBy: g.Key → [CustomerId]
                if (_groupByColumns.Count == 1)
                {
                    return _groupByColumns[0].IsLiteral
                        ? _groupByColumns[0].ColumnName
                        : _dialect.QuoteIdentifier(_groupByColumns[0].ColumnName);
                }
                else
                {
                    throw new InvalidOperationException(
                        "Cannot use g.Key directly with multi-column GroupBy. " +
                        "Use g.Key.PropertyName instead (e.g., g.Key.CustomerId, g.Key.Year).");
                }
            }

            // Check if this is g.Key.PropertyName
            if (member.Expression is MemberExpression innerMember &&
                innerMember.Member.Name == "Key" &&
                innerMember.Expression is ParameterExpression)
            {
                // Multi-column GroupBy: g.Key.CustomerId → [CustomerId]
                var propertyName = member.Member.Name;
                var column = _groupByColumns.FirstOrDefault(c => c.PropertyName == propertyName);

                if (column == null)
                    throw new InvalidOperationException(
                        $"Property '{propertyName}' is not part of the GroupBy key.");

                return _dialect.QuoteIdentifier(column.ColumnName);
            }

            throw new NotSupportedException(
                $"Member access '{member}' is not supported in GroupBy Select. " +
                $"Use g.Key or g.Key.PropertyName.");
        }

        /// <summary>
        /// Translates aggregate method calls to SQL aggregate functions.
        /// Supports: Count(), LongCount(), Sum(), Average(), Min(), Max()
        /// </summary>
        private string TranslateAggregate(MethodCallExpression methodCall)
        {
            var methodName = methodCall.Method.Name;

            // Check if this is called on the IGrouping parameter
            if (methodCall.Object == null && methodCall.Arguments.Count > 0)
            {
                // Extension method: Enumerable.Count(g), Enumerable.Sum(g, x => x.Amount)
                var firstArg = methodCall.Arguments[0];

                if (!(firstArg is ParameterExpression))
                    throw new NotSupportedException(
                        $"Aggregate method '{methodName}' must be called on the grouping parameter (g).");

                switch (methodName)
                {
                    case "Count":
                    case "LongCount":
                        // Conditional count: g.Count(x => predicate) → SUM(CASE WHEN <pred> THEN 1 ELSE 0 END)
                        if (methodCall.Arguments.Count >= 2 && methodCall.Arguments[1] is LambdaExpression countPredicate)
                        {
                            var condition = TranslateValueExpression(countPredicate.Body);
                            return $"SUM(CASE WHEN {condition} THEN 1 ELSE 0 END)";
                        }
                        // Plain count of all rows in the group.
                        return "COUNT(*)";

                    case "Sum":
                    case "Average":
                    case "Min":
                    case "Max":
                        if (methodCall.Arguments.Count < 2)
                            throw new InvalidOperationException(
                                $"{methodName} requires a selector expression (e.g., g.{methodName}(x => x.Amount)).");

                        var selector = methodCall.Arguments[1] as LambdaExpression;
                        if (selector == null)
                            throw new InvalidOperationException(
                                $"{methodName} selector must be a lambda expression.");

                        var inner = TranslateValueExpression(selector.Body);
                        // Map Average to AVG (not AVERAGE)
                        var sqlFunc = methodName == "Average" ? "AVG" : methodName.ToUpperInvariant();
                        return $"{sqlFunc}({inner})";

                    default:
                        throw new NotSupportedException(
                            $"Aggregate method '{methodName}' is not supported. " +
                            $"Use Count(), Sum(), Average(), Min(), or Max().");
                }
            }

            throw new NotSupportedException(
                $"Method call '{methodCall.Method.Name}' is not supported in GroupBy Select.");
        }

        /// <summary>
        /// Builds the HAVING clause from a predicate expression.
        /// Translates aggregate predicates like g.Count() > 10 to HAVING COUNT(*) > @h0.
        /// </summary>
        private string BuildHavingClause(LambdaExpression havingExpression)
        {
            return VisitHavingExpression(havingExpression.Body);
        }

        private string VisitHavingExpression(Expression expr)
        {
            // Unwrap Convert/ConvertChecked expressions (e.g., nullable lifting)
            while (expr is UnaryExpression unaryConv &&
                   (unaryConv.NodeType == ExpressionType.Convert || unaryConv.NodeType == ExpressionType.ConvertChecked))
                expr = unaryConv.Operand;

            switch (expr)
            {
                case BinaryExpression binary:
                    return VisitHavingBinary(binary);

                case UnaryExpression unaryExpr when unaryExpr.NodeType == ExpressionType.Not:
                    return $"NOT ({VisitHavingExpression(unaryExpr.Operand)})";

                case MethodCallExpression methodCall:
                    return TranslateAggregate(methodCall);

                case ConstantExpression constant:
                    return AddParameter(constant.Value);

                case MemberExpression member when member.Member.Name == "Key" || IsKeyAccess(member):
                    // Handle g.Key in HAVING clause
                    return TranslateKeyAccessForHaving(member);

                case MemberExpression member:
                    // Closure / captured variable — evaluate to a parameter value
                    return AddParameter(EvaluateValue(member));

                default:
                    throw new NotSupportedException(
                        $"Expression type '{expr.NodeType}' is not supported in HAVING clause.");
            }
        }

        private static bool IsKeyAccess(MemberExpression member)
        {
            return member.Expression is MemberExpression inner && inner.Member.Name == "Key";
        }

        private static object EvaluateValue(Expression expr)
        {
            if (expr is ConstantExpression c) return c.Value;
            var lambda = Expression.Lambda<Func<object>>(Expression.Convert(expr, typeof(object)));
            return ExpressionCache.GetOrAddFunc<object>(lambda)();
        }

        private string VisitHavingBinary(BinaryExpression binary)
        {
            var left = VisitHavingExpression(binary.Left);
            var right = VisitHavingExpression(binary.Right);

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
                    throw new NotSupportedException(
                        $"Binary operator '{binary.NodeType}' is not supported in HAVING clause.");
            }

            // Wrap AND/OR conditions in parentheses for clarity
            if (binary.NodeType == ExpressionType.AndAlso || binary.NodeType == ExpressionType.OrElse)
                return $"({left} {op} {right})";

            return $"{left} {op} {right}";
        }

        /// <summary>
        /// Extracts the column name from a selector expression (x => x.Amount).
        /// </summary>
        private string ExtractColumnFromSelector(LambdaExpression selector)
        {
            var body = selector.Body;

            // Unwrap Convert if present
            if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                body = unary.Operand;

            if (body is MemberExpression member && member.Expression is ParameterExpression)
            {
                var propName = member.Member.Name;
                var col = FindColumn(propName);
                return col.ColumnName;
            }

            // Handle constant values in HAVING clause
            if (body is ConstantExpression constant)
            {
                return AddParameter(constant.Value);
            }

            throw new NotSupportedException(
                $"Selector expression '{selector}' must be a simple property access (x => x.PropertyName).");
        }

        /// <summary>
        /// Translates an aggregate selector or conditional-count predicate body into SQL.
        /// Supports member access, arithmetic (x.A * x.B), comparisons, AND/OR, and constants.
        /// </summary>
        private string TranslateValueExpression(Expression expr)
        {
            while (expr is UnaryExpression u &&
                   (u.NodeType == ExpressionType.Convert || u.NodeType == ExpressionType.ConvertChecked))
                expr = u.Operand;

            switch (expr)
            {
                case MemberExpression member when member.Expression is ParameterExpression:
                    return _dialect.QuoteIdentifier(FindColumn(member.Member.Name).ColumnName);

                case MemberExpression member:
                    // captured closure value
                    return AddParameter(EvaluateValue(member));

                case ConstantExpression constant:
                    return constant.Value == null ? "NULL" : AddParameter(constant.Value);

                case BinaryExpression binary:
                    return TranslateValueBinary(binary);

                case UnaryExpression notExpr when notExpr.NodeType == ExpressionType.Not:
                    return $"NOT ({TranslateValueExpression(notExpr.Operand)})";

                default:
                    throw new NotSupportedException(
                        $"Expression '{expr}' is not supported inside an aggregate selector.");
            }
        }

        private string TranslateValueBinary(BinaryExpression binary)
        {
            // null comparison
            if (binary.NodeType == ExpressionType.Equal || binary.NodeType == ExpressionType.NotEqual)
            {
                if (IsNullConstant(binary.Right))
                    return $"{TranslateValueExpression(binary.Left)} IS {(binary.NodeType == ExpressionType.Equal ? "NULL" : "NOT NULL")}";
                if (IsNullConstant(binary.Left))
                    return $"{TranslateValueExpression(binary.Right)} IS {(binary.NodeType == ExpressionType.Equal ? "NULL" : "NOT NULL")}";
            }

            var left = TranslateValueExpression(binary.Left);
            var right = TranslateValueExpression(binary.Right);

            string op;
            switch (binary.NodeType)
            {
                case ExpressionType.Add: op = "+"; break;
                case ExpressionType.Subtract: op = "-"; break;
                case ExpressionType.Multiply: op = "*"; break;
                case ExpressionType.Divide: op = "/"; break;
                case ExpressionType.Modulo: op = "%"; break;
                case ExpressionType.Equal: op = "="; break;
                case ExpressionType.NotEqual: op = "<>"; break;
                case ExpressionType.LessThan: op = "<"; break;
                case ExpressionType.LessThanOrEqual: op = "<="; break;
                case ExpressionType.GreaterThan: op = ">"; break;
                case ExpressionType.GreaterThanOrEqual: op = ">="; break;
                case ExpressionType.AndAlso: op = "AND"; break;
                case ExpressionType.OrElse: op = "OR"; break;
                default:
                    throw new NotSupportedException(
                        $"Operator '{binary.NodeType}' is not supported inside an aggregate selector.");
            }

            return $"({left} {op} {right})";
        }

        private static bool IsNullConstant(Expression expr)
        {
            if (expr is ConstantExpression c && c.Value == null) return true;
            if (expr is UnaryExpression u && u.NodeType == ExpressionType.Convert) return IsNullConstant(u.Operand);
            return false;
        }

        /// <summary>
        /// Gets the property name from an expression, unwrapping Convert if needed.
        /// </summary>
        private string GetPropertyName(Expression expr)
        {
            // Unwrap Convert (e.g., (object)x.Id)
            if (expr is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                expr = unary.Operand;

            if (expr is MemberExpression member && member.Expression is ParameterExpression)
                return member.Member.Name;

            return null;
        }

        /// <summary>
        /// Finds the column mapping for a given property name.
        /// </summary>
        private ColumnMapping FindColumn(string propertyName)
        {
            var col = _mapping.Columns.FirstOrDefault(c => c.Property.Name == propertyName);
            if (col == null)
                throw new InvalidOperationException(
                    $"Property '{propertyName}' is not a mapped column of '{_mapping.EntityType.Name}'.");
            return col;
        }

        /// <summary>
        /// Translates g.Key or g.Key.PropertyName to SQL column name(s) for HAVING clause.
        /// </summary>
        private string TranslateKeyAccessForHaving(MemberExpression member)
        {
            // Check if this is g.Key
            if (member.Member.Name == "Key" && member.Expression is ParameterExpression)
            {
                // Single column GroupBy: g.Key → [CustomerId]
                if (_groupByColumns.Count == 1)
                {
                    return _dialect.QuoteIdentifier(_groupByColumns[0].ColumnName);
                }
                else
                {
                    throw new InvalidOperationException(
                        "Cannot use g.Key directly with multi-column GroupBy in HAVING clause. " +
                        "Use g.Key.PropertyName instead (e.g., g.Key.CustomerId, g.Key.Year).");
                }
            }

            // Check if this is g.Key.PropertyName
            if (member.Expression is MemberExpression innerMember &&
                innerMember.Member.Name == "Key" &&
                innerMember.Expression is ParameterExpression)
            {
                // Multi-column GroupBy: g.Key.CustomerId → [CustomerId]
                var propertyName = member.Member.Name;
                var column = _groupByColumns.FirstOrDefault(c => c.PropertyName == propertyName);

                if (column == null)
                    throw new InvalidOperationException(
                        $"Property '{propertyName}' is not part of the GroupBy key.");

                return _dialect.QuoteIdentifier(column.ColumnName);
            }

            throw new NotSupportedException(
                $"Member access '{member}' is not supported in HAVING clause. " +
                $"Use g.Key or g.Key.PropertyName.");
        }

        /// <summary>
        /// Adds a parameter for HAVING clause values.
        /// Uses @h prefix to avoid collision with WHERE parameters (@w).
        /// </summary>
        private string AddParameter(object value)
        {
            var paramName = $"@h{_paramIndex++}";
            _parameters[paramName] = value;
            return paramName;
        }
    }

    /// <summary>
    /// Represents a GROUP BY column with its SQL name and optional property name.
    /// </summary>
    internal class GroupByColumn
    {
        public string ColumnName { get; set; }
        public string PropertyName { get; set; }
        public bool IsLiteral { get; set; }
    }
}
