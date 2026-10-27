using ZeroData.Sql.Dialects;
using ZeroData.Sql.Mapping;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace ZeroData.Sql.Sql
{
    /// <summary>
    /// Translates LINQ Expression trees (lambda predicates) into SQL WHERE clauses
    /// with parameterized values for use with Dapper.
    /// 
    /// Supported expressions:
    /// - Binary: ==, !=, &lt;, &lt;=, &gt;, &gt;=
    /// - Logical: &amp;&amp;, ||
    /// - Unary: ! (not)
    /// - Member access: entity.Property
    /// - Constants and closures
    /// - null checks: x == null → IS NULL, x != null → IS NOT NULL
    /// - String: Contains, StartsWith, EndsWith
    /// - Collection: list.Contains(x) → IN (...)
    /// </summary>
    public class WhereBuilder
    {
        private readonly EntityMapping _mapping;
        private readonly ISqlDialect _dialect;
        private readonly IDictionary<string, object> _parameters = new Dictionary<string, object>();
        private int _paramIndex;
        private string _paramPrefix = "w";

        public WhereBuilder(EntityMapping mapping, ISqlDialect dialect = null)
        {
            _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
            _dialect = dialect ?? SqlGenerator.DefaultDialect;
        }

        /// <summary>
        /// Translates a lambda expression into a SQL WHERE clause.
        /// Returns the WHERE clause (without "WHERE" keyword) and parameters.
        /// </summary>
        public (string Sql, IDictionary<string, object> Parameters) Build<T>(
            Expression<Func<T, bool>> predicate) where T : class
        {
            _parameters.Clear();
            _paramIndex = 0;

            var sql = VisitCondition(predicate.Body);
            return (sql, _parameters);
        }

        /// <summary>
        /// Translates a non-generic LambdaExpression into SQL WHERE clause.
        /// Used by global query filters.
        /// </summary>
        public (string Sql, IDictionary<string, object> Parameters) BuildFromLambda(
            LambdaExpression predicate)
        {
            // Use @f prefix for filter params to avoid collision with @w user params
            // Don't reset _paramIndex — allows multiple sequential calls to produce unique names
            _paramPrefix = "f";

            var sql = VisitCondition(predicate.Body);
            _paramPrefix = "w"; // Reset for next use
            return (sql, _parameters.Count > 0 ? new Dictionary<string, object>(_parameters) : null);
        }

        private string VisitCondition(Expression expression)
        {
            if (IsEntityBooleanMember(expression))
            {
                var col = Visit(expression);
                return IsPostgreSql() ? $"{col} = TRUE" : $"{col} = 1";
            }

            return Visit(expression);
        }

        private string Visit(Expression expression)
        {
            switch (expression)
            {
                case BinaryExpression binary:
                    return VisitBinary(binary);

                case UnaryExpression unary:
                    return VisitUnary(unary);

                case MemberExpression member:
                    return VisitMember(member);

                case ConstantExpression constant:
                    return VisitConstant(constant);

                case MethodCallExpression method:
                    return VisitMethodCall(method);

                default:
                    throw new NotSupportedException(
                        $"Expression type '{expression.NodeType}' is not supported in WHERE clause.");
            }
        }

        private string VisitBinary(BinaryExpression binary)
        {
            // Handle null comparisons specially: x == null → IS NULL
            if (IsNullComparison(binary, out var nullMemberSql, out var isNullCheck))
            {
                return isNullCheck
                    ? $"{nullMemberSql} IS NULL"
                    : $"{nullMemberSql} IS NOT NULL";
            }

            if (binary.NodeType == ExpressionType.Coalesce)
            {
                var left = Visit(binary.Left);
                var right = Visit(binary.Right);
                return $"COALESCE({left}, {right})";
            }

            if (binary.NodeType == ExpressionType.AndAlso || binary.NodeType == ExpressionType.OrElse)
            {
                var left = VisitCondition(binary.Left);
                var right = VisitCondition(binary.Right);
                var op = binary.NodeType == ExpressionType.AndAlso ? "AND" : "OR";

                // Wrap OR conditions in parentheses
                if (binary.NodeType == ExpressionType.OrElse)
                    return $"({left} {op} {right})";

                return $"{left} {op} {right}";
            }
            else
            {
                var left = Visit(binary.Left);
                var right = Visit(binary.Right);

                string op;
                switch (binary.NodeType)
                {
                    case ExpressionType.Equal: op = "="; break;
                    case ExpressionType.NotEqual: op = "<>"; break;
                    case ExpressionType.LessThan: op = "<"; break;
                    case ExpressionType.LessThanOrEqual: op = "<="; break;
                    case ExpressionType.GreaterThan: op = ">"; break;
                    case ExpressionType.GreaterThanOrEqual: op = ">="; break;
                    case ExpressionType.Add: op = "+"; break;
                    case ExpressionType.Subtract: op = "-"; break;
                    case ExpressionType.Multiply: op = "*"; break;
                    case ExpressionType.Divide: op = "/"; break;
                    case ExpressionType.Modulo: op = "%"; break;
                    default:
                        throw new NotSupportedException(
                            $"Binary operator '{binary.NodeType}' is not supported.");
                }

                return $"{left} {op} {right}";
            }
        }

        private string VisitUnary(UnaryExpression unary)
        {
            if (unary.NodeType == ExpressionType.Not)
            {
                var operand = VisitCondition(unary.Operand);
                return $"NOT ({operand})";
            }

            if (unary.NodeType == ExpressionType.Convert)
            {
                return Visit(unary.Operand);
            }

            throw new NotSupportedException(
                $"Unary operator '{unary.NodeType}' is not supported.");
        }

        private string VisitMember(MemberExpression member)
        {
            // Check if this is an entity property (e.g., p.Name)
            if (IsEntityMember(member, out _))
            {
                var columnMapping = _mapping.Columns
                    .FirstOrDefault(c => c.Property.Name == member.Member.Name);

                if (columnMapping != null)
                    return _dialect.QuoteIdentifier(columnMapping.ColumnName);

                // Fallback: use member name as column name
                return _dialect.QuoteIdentifier(member.Member.Name);
            }

            // Nullable<T>.HasValue translated to IS NOT NULL
            if (member.Member.Name == "HasValue" &&
                member.Member.DeclaringType != null &&
                member.Member.DeclaringType.IsGenericType &&
                member.Member.DeclaringType.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                ContainsEntityParameter(member.Expression))
            {
                var inner = Visit(member.Expression);
                return $"{inner} IS NOT NULL";
            }

            // Nullable<T>.Value unwraps to underlying column
            if (member.Member.Name == "Value" &&
                member.Member.DeclaringType != null &&
                member.Member.DeclaringType.IsGenericType &&
                member.Member.DeclaringType.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                ContainsEntityParameter(member.Expression))
            {
                return Visit(member.Expression);
            }

            // String .Length property translated to LEN / LENGTH
            if (member.Member.Name == "Length" && member.Type == typeof(int) && ContainsEntityParameter(member.Expression))
            {
                var inner = Visit(member.Expression);
                return IsSqlServer() ? $"LEN({inner})" : $"LENGTH({inner})";
            }

            // DateTime member access: Year, Month, Day, Date, Hour, Minute, Second
            if ((member.Member.DeclaringType == typeof(DateTime) || member.Expression?.Type == typeof(DateTime)) &&
                ContainsEntityParameter(member.Expression))
            {
                switch (member.Member.Name)
                {
                    case "Year":
                    case "Month":
                    case "Day":
                    case "Date":
                    case "Hour":
                    case "Minute":
                    case "Second":
                        var inner = Visit(member.Expression);
                        return TranslateDateTimeMember(member.Member.Name, inner);
                }
            }

            // Otherwise, evaluate the expression to get its value
            var value = EvaluateExpression(member);
            return AddParameter(value);
        }

        private string VisitConstant(ConstantExpression constant)
        {
            if (constant.Value == null)
                return "NULL";

            return AddParameter(constant.Value);
        }

        private string VisitMethodCall(MethodCallExpression method)
        {
            // Static string methods: string.IsNullOrEmpty, string.IsNullOrWhiteSpace
            if (method.Method.DeclaringType == typeof(string))
            {
                if (method.Method.Name == "IsNullOrEmpty" && method.Arguments.Count == 1)
                {
                    var col = Visit(method.Arguments[0]);
                    return $"({col} IS NULL OR {col} = '')";
                }

                if (method.Method.Name == "IsNullOrWhiteSpace" && method.Arguments.Count == 1)
                {
                    var col = Visit(method.Arguments[0]);
                    return $"({col} IS NULL OR TRIM({col}) = '')";
                }
            }

            // String instance methods: Contains, StartsWith, EndsWith, ToLower, ToUpper, Trim, Replace
            if (method.Object != null && method.Object.Type == typeof(string))
            {
                switch (method.Method.Name)
                {
                    case "ToLower":
                        return $"LOWER({Visit(method.Object)})";

                    case "ToUpper":
                        return $"UPPER({Visit(method.Object)})";

                    case "Trim":
                        if (method.Arguments.Count == 0)
                            return $"TRIM({Visit(method.Object)})";
                        break;

                    case "TrimStart":
                        if (method.Arguments.Count == 0)
                            return $"LTRIM({Visit(method.Object)})";
                        break;

                    case "TrimEnd":
                        if (method.Arguments.Count == 0)
                            return $"RTRIM({Visit(method.Object)})";
                        break;

                    case "Replace":
                        if (method.Arguments.Count == 2)
                        {
                            var column = Visit(method.Object);
                            var oldVal = Visit(method.Arguments[0]);
                            var newVal = Visit(method.Arguments[1]);
                            return $"REPLACE({column}, {oldVal}, {newVal})";
                        }
                        break;

                    case "Contains":
                    {
                        var column = Visit(method.Object);
                        var arg = EvaluateExpression(method.Arguments[0]);
                        var escaped = EscapeLike(arg);
                        return $"{column} LIKE {AddParameter($"%{escaped}%")} ESCAPE '\\'";
                    }

                    case "StartsWith":
                    {
                        var column = Visit(method.Object);
                        var arg = EvaluateExpression(method.Arguments[0]);
                        var escaped = EscapeLike(arg);
                        return $"{column} LIKE {AddParameter($"{escaped}%")} ESCAPE '\\'";
                    }

                    case "EndsWith":
                    {
                        var column = Visit(method.Object);
                        var arg = EvaluateExpression(method.Arguments[0]);
                        var escaped = EscapeLike(arg);
                        return $"{column} LIKE {AddParameter($"%{escaped}")} ESCAPE '\\'";
                    }
                }
            }

            // IEnumerable.Contains: list.Contains(x) → x IN (@p0, @p1, ...)
            if (method.Method.Name == "Contains")
            {
                // Static Enumerable.Contains(source, item) or List<T>.Contains(item)
                Expression collectionExpr, itemExpr;

                if (method.Object != null)
                {
                    // Instance method: list.Contains(x)
                    collectionExpr = method.Object;
                    itemExpr = method.Arguments[0];
                }
                else if (method.Arguments.Count == 2)
                {
                    // Static method: Enumerable.Contains(list, x)
                    collectionExpr = method.Arguments[0];
                    itemExpr = method.Arguments[1];
                }
                else
                {
                    throw new NotSupportedException("Unsupported Contains overload.");
                }

                var column = Visit(itemExpr);
                var collection = EvaluateExpression(collectionExpr) as IEnumerable;
                if (collection == null)
                    throw new InvalidOperationException("Contains target must be a collection.");

                var paramNames = new List<string>();
                foreach (var item in collection)
                {
                    paramNames.Add(AddParameter(item));
                }

                if (paramNames.Count == 0)
                    return "1 = 0"; // Empty collection → always false

                return $"{column} IN ({string.Join(", ", paramNames)})";
            }

            // Math static methods: Abs, Round, Floor, Ceiling
            if (method.Method.DeclaringType == typeof(Math) && ContainsEntityParameter(method))
            {
                switch (method.Method.Name)
                {
                    case "Abs" when method.Arguments.Count == 1:
                        return $"ABS({Visit(method.Arguments[0])})";

                    case "Round" when method.Arguments.Count == 1:
                        return $"ROUND({Visit(method.Arguments[0])})";

                    case "Round" when method.Arguments.Count == 2:
                        return $"ROUND({Visit(method.Arguments[0])}, {Visit(method.Arguments[1])})";

                    case "Floor" when method.Arguments.Count == 1:
                        return $"FLOOR({Visit(method.Arguments[0])})";

                    case "Ceiling" when method.Arguments.Count == 1:
                        return $"CEILING({Visit(method.Arguments[0])})";
                }
            }

            throw new NotSupportedException(
                $"Method '{method.Method.Name}' is not supported in WHERE clause.");
        }

        private bool IsEntityMember(Expression expr, out MemberExpression member)
        {
            if (expr is MemberExpression m && IsParameterReference(m.Expression))
            {
                member = m;
                return true;
            }
            member = null;
            return false;
        }

        private static bool IsParameterReference(Expression expr)
        {
            if (expr is ParameterExpression)
                return true;
            if (expr is UnaryExpression u && u.NodeType == ExpressionType.Convert)
                return IsParameterReference(u.Operand);
            return false;
        }

        private bool IsEntityBooleanMember(Expression expr)
        {
            if (IsEntityMember(expr, out var m))
            {
                var type = m.Type;
                return type == typeof(bool) || type == typeof(bool?);
            }

            if (expr is MemberExpression member &&
                member.Member.Name == "Value" &&
                member.Type == typeof(bool) &&
                ContainsEntityParameter(member.Expression))
            {
                return true;
            }

            return false;
        }

        private static bool ContainsEntityParameter(Expression expr)
        {
            if (expr == null) return false;
            if (expr is ParameterExpression) return true;
            if (expr is MemberExpression m) return ContainsEntityParameter(m.Expression);
            if (expr is MethodCallExpression call)
            {
                if (ContainsEntityParameter(call.Object)) return true;
                foreach (var arg in call.Arguments)
                {
                    if (ContainsEntityParameter(arg)) return true;
                }
            }
            if (expr is UnaryExpression u) return ContainsEntityParameter(u.Operand);
            if (expr is BinaryExpression b) return ContainsEntityParameter(b.Left) || ContainsEntityParameter(b.Right);
            return false;
        }

        private bool IsSqlServer()
        {
            return _dialect is SqlServerDialect ||
                   string.Equals(_dialect?.ProviderName, "SQL Server", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(_dialect?.ProviderName, "SqlServer", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsPostgreSql()
        {
            return _dialect is PostgreSqlDialect || string.Equals(_dialect?.ProviderName, "PostgreSQL", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsMySql()
        {
            return _dialect is MySqlDialect ||
                   string.Equals(_dialect?.ProviderName, "MySQL", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(_dialect?.ProviderName, "MySql", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsSqlite()
        {
            return _dialect is SqliteDialect ||
                   string.Equals(_dialect?.ProviderName, "SQLite", StringComparison.OrdinalIgnoreCase);
        }

        private string TranslateDateTimeMember(string memberName, string inner)
        {
            if (IsSqlServer())
            {
                switch (memberName)
                {
                    case "Year": return $"YEAR({inner})";
                    case "Month": return $"MONTH({inner})";
                    case "Day": return $"DAY({inner})";
                    case "Date": return $"CAST({inner} AS DATE)";
                    case "Hour": return $"DATEPART(hour, {inner})";
                    case "Minute": return $"DATEPART(minute, {inner})";
                    case "Second": return $"DATEPART(second, {inner})";
                }
            }
            else if (IsPostgreSql())
            {
                switch (memberName)
                {
                    case "Year": return $"EXTRACT(YEAR FROM {inner})";
                    case "Month": return $"EXTRACT(MONTH FROM {inner})";
                    case "Day": return $"EXTRACT(DAY FROM {inner})";
                    case "Date": return $"CAST({inner} AS DATE)";
                    case "Hour": return $"EXTRACT(HOUR FROM {inner})";
                    case "Minute": return $"EXTRACT(MINUTE FROM {inner})";
                    case "Second": return $"EXTRACT(SECOND FROM {inner})";
                }
            }
            else if (IsMySql())
            {
                switch (memberName)
                {
                    case "Year": return $"YEAR({inner})";
                    case "Month": return $"MONTH({inner})";
                    case "Day": return $"DAY({inner})";
                    case "Date": return $"DATE({inner})";
                    case "Hour": return $"HOUR({inner})";
                    case "Minute": return $"MINUTE({inner})";
                    case "Second": return $"SECOND({inner})";
                }
            }
            else // SQLite or default
            {
                switch (memberName)
                {
                    case "Year": return $"CAST(strftime('%Y', {inner}) AS INTEGER)";
                    case "Month": return $"CAST(strftime('%m', {inner}) AS INTEGER)";
                    case "Day": return $"CAST(strftime('%d', {inner}) AS INTEGER)";
                    case "Date": return $"date({inner})";
                    case "Hour": return $"CAST(strftime('%H', {inner}) AS INTEGER)";
                    case "Minute": return $"CAST(strftime('%M', {inner}) AS INTEGER)";
                    case "Second": return $"CAST(strftime('%S', {inner}) AS INTEGER)";
                }
            }

            throw new NotSupportedException($"DateTime member '{memberName}' is not supported.");
        }

        private bool IsNullComparison(BinaryExpression binary, out string memberSql, out bool isNull)
        {
            memberSql = null;
            isNull = false;

            if (binary.NodeType != ExpressionType.Equal && binary.NodeType != ExpressionType.NotEqual)
                return false;

            Expression memberExpr = null;

            if (IsNullExpression(binary.Right))
                memberExpr = binary.Left;
            else if (IsNullExpression(binary.Left))
                memberExpr = binary.Right;

            if (memberExpr == null)
                return false;

            memberSql = Visit(memberExpr);
            isNull = binary.NodeType == ExpressionType.Equal;
            return true;
        }

        private bool IsNullExpression(Expression expression)
        {
            if (expression is ConstantExpression constant && constant.Value == null)
                return true;

            // Handle nullable conversions: (object)null
            if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                return IsNullExpression(unary.Operand);

            return false;
        }

        private string AddParameter(object value)
        {
            var paramName = $"@{_paramPrefix}{_paramIndex++}";
            _parameters[paramName] = value;
            return paramName;
        }

        /// <summary>
        /// Escapes LIKE wildcard characters (%, _) and the escape char (\) in user input,
        /// so they are matched literally. Paired with an "ESCAPE '\\'" clause.
        /// </summary>
        private static string EscapeLike(object value)
        {
            if (value == null) return null;
            return value.ToString()
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
        }

        /// <summary>
        /// Evaluates a constant or closure expression to get its runtime value.
        /// </summary>
        private object EvaluateExpression(Expression expression)
        {
            // Try fast path for common closure pattern: () => someLocal
            if (expression is MemberExpression member)
            {
                // Handle nested member access: closure.field.property
                object target = null;
                if (member.Expression != null)
                    target = EvaluateExpression(member.Expression);

                var field = member.Member as FieldInfo;
                if (field != null)
                    return field.GetValue(target);

                var prop = member.Member as PropertyInfo;
                if (prop != null)
                    return prop.GetValue(target);
            }

            if (expression is ConstantExpression constant)
                return constant.Value;

            // Handle inline array creation: new[] { 1, 2, 3 }
            if (expression is NewArrayExpression newArray)
            {
                var items = new List<object>();
                foreach (var elem in newArray.Expressions)
                    items.Add(EvaluateExpression(elem));
                return items;
            }

            // Handle conversions (e.g., implicit casts)
            if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                return EvaluateExpression(unary.Operand);

            // Fallback: compile and invoke (with caching)
            var lambda = Expression.Lambda<Func<object>>(
                Expression.Convert(expression, typeof(object)));
            var fn = ExpressionCache.GetOrAddFunc<object>(lambda);
            return fn();
        }
    }
}
