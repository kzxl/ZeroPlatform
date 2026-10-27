using ZeroData.Sql.Mapping;
using ZeroData.Sql.Sql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroData.Sql
{
    /// <summary>
    /// Represents a GROUP BY query that must be followed by Select() to project aggregates.
    /// Provides fluent API for HAVING clause (Where) and ORDER BY on aggregates or keys.
    /// </summary>
    public class GroupByQuery<T, TKey> where T : class
    {
        private readonly SqlContext _context;
        private readonly EntityMapping _mapping;
        private readonly string _tableName;
        private readonly LambdaExpression _groupByExpression;
        private readonly string _whereClause;
        private readonly IDictionary<string, object> _whereParameters;
        private readonly ZeroData.Sql.Dialects.ISqlDialect _dialect;
        private LambdaExpression _havingClause;
        private List<string> _orderByClauses;

        internal GroupByQuery(
            SqlContext context,
            EntityMapping mapping,
            string tableName,
            LambdaExpression groupByExpression,
            string whereClause,
            IDictionary<string, object> whereParameters,
            ZeroData.Sql.Dialects.ISqlDialect dialect = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
            _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
            _groupByExpression = groupByExpression ?? throw new ArgumentNullException(nameof(groupByExpression));
            _whereClause = whereClause;
            _whereParameters = whereParameters;
            _dialect = dialect ?? context.Dialect;
        }

        /// <summary>
        /// Projects the grouped results using aggregate functions.
        /// Required after GroupBy().
        /// Example: .Select(g => new { g.Key, Count = g.Count(), Total = g.Sum(x => x.Amount) })
        /// </summary>
        public List<TResult> Select<TResult>(Expression<Func<IGrouping<TKey, T>, TResult>> selector)
        {
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            var (sql, parameters) = BuildQuery(selector);

            _context.EnsureConnectionOpen();

            if (Sql.ProjectionMaterializer.RequiresManualMaterialization(typeof(TResult)))
            {
                var rows = _context.Connection.Query(
                    sql,
                    parameters,
                    transaction: _context.Transaction,
                    commandTimeout: _context.CommandTimeout);
                return Sql.ProjectionMaterializer.Materialize<TResult>(rows.Cast<object>());
            }

            var results = _context.Connection.Query<TResult>(
                sql,
                parameters,
                transaction: _context.Transaction,
                commandTimeout: _context.CommandTimeout).ToList();

            return results;
        }

        /// <summary>
        /// Async version of Select.
        /// </summary>
        public async Task<List<TResult>> SelectAsync<TResult>(
            Expression<Func<IGrouping<TKey, T>, TResult>> selector,
            CancellationToken cancellationToken = default)
        {
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            var (sql, parameters) = BuildQuery(selector);

            _context.EnsureConnectionOpen();

            if (Sql.ProjectionMaterializer.RequiresManualMaterialization(typeof(TResult)))
            {
                var rows = await _context.Connection.QueryAsync(
                    sql,
                    parameters,
                    transaction: _context.Transaction,
                    commandTimeout: _context.CommandTimeout).ConfigureAwait(false);
                return Sql.ProjectionMaterializer.Materialize<TResult>(rows.Cast<object>());
            }

            var results = await _context.Connection.QueryAsync<TResult>(
                sql,
                parameters,
                transaction: _context.Transaction,
                commandTimeout: _context.CommandTimeout).ConfigureAwait(false);

            return results.ToList();
        }

        /// <summary>
        /// Filters grouped results (HAVING clause).
        /// Can reference aggregate functions like g.Count(), g.Sum(), etc.
        /// Example: .Where(g => g.Count() > 10)
        /// </summary>
        public GroupByQuery<T, TKey> Where(Expression<Func<IGrouping<TKey, T>, bool>> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            _havingClause = predicate;
            return this;
        }

        /// <summary>
        /// Orders results by aggregate or key columns in ascending order.
        /// Example: .OrderBy(x => x.Total)
        /// </summary>
        public GroupByQuery<T, TKey> OrderBy<TOrderKey>(
            Expression<Func<IGrouping<TKey, T>, TOrderKey>> keySelector)
        {
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            if (_orderByClauses == null)
                _orderByClauses = new List<string>();

            var columnName = ExtractOrderByColumn(keySelector);
            _orderByClauses.Add($"{_dialect.QuoteIdentifier(columnName)} ASC");
            return this;
        }

        /// <summary>
        /// Orders results by aggregate or key columns in descending order.
        /// Example: .OrderByDescending(x => x.Total)
        /// </summary>
        public GroupByQuery<T, TKey> OrderByDescending<TOrderKey>(
            Expression<Func<IGrouping<TKey, T>, TOrderKey>> keySelector)
        {
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            if (_orderByClauses == null)
                _orderByClauses = new List<string>();

            var columnName = ExtractOrderByColumn(keySelector);
            _orderByClauses.Add($"{_dialect.QuoteIdentifier(columnName)} DESC");
            return this;
        }

        /// <summary>
        /// Returns the generated SQL query for debugging purposes.
        /// Inspired by FreeSql's ToSql() pattern.
        /// </summary>
        /// <example>
        /// var sql = db.Orders
        ///     .GroupBy(o => o.CustomerId)
        ///     .ToSql(g => new { g.Key, Count = g.Count() });
        /// Console.WriteLine(sql);
        /// </example>
        public string ToSql<TResult>(Expression<Func<IGrouping<TKey, T>, TResult>> selector)
        {
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            var (sql, parameters) = BuildQuery(selector);

            // Replace parameters with their values for debugging.
            // Order by descending key length so "@h10" is replaced before "@h1" (avoids partial-match corruption).
            foreach (var kvp in parameters.OrderByDescending(p => p.Key.Length))
            {
                var key = kvp.Key;
                var value = kvp.Value;

                string valueStr;
                if (value == null)
                {
                    valueStr = "NULL";
                }
                else if (value is string s)
                {
                    valueStr = $"'{s.Replace("'", "''")}'";
                }
                else if (value is DateTime dt)
                {
                    valueStr = $"'{dt:yyyy-MM-dd HH:mm:ss}'";
                }
                else if (value is bool b)
                {
                    valueStr = b ? "1" : "0";
                }
                else
                {
                    valueStr = value.ToString();
                }

                sql = sql.Replace(key, valueStr);
            }

            return sql;
        }

        /// <summary>
        /// Adds a secondary ascending sort.
        /// </summary>
        public GroupByQuery<T, TKey> ThenBy<TOrderKey>(
            Expression<Func<IGrouping<TKey, T>, TOrderKey>> keySelector)
        {
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            if (_orderByClauses == null || _orderByClauses.Count == 0)
                throw new InvalidOperationException("ThenBy must be called after OrderBy or OrderByDescending.");

            var columnName = ExtractOrderByColumn(keySelector);
            _orderByClauses.Add($"{_dialect.QuoteIdentifier(columnName)} ASC");
            return this;
        }

        /// <summary>
        /// Adds a secondary descending sort.
        /// </summary>
        public GroupByQuery<T, TKey> ThenByDescending<TOrderKey>(
            Expression<Func<IGrouping<TKey, T>, TOrderKey>> keySelector)
        {
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            if (_orderByClauses == null || _orderByClauses.Count == 0)
                throw new InvalidOperationException("ThenByDescending must be called after OrderBy or OrderByDescending.");

            var columnName = ExtractOrderByColumn(keySelector);
            _orderByClauses.Add($"{_dialect.QuoteIdentifier(columnName)} DESC");
            return this;
        }

        /// <summary>
        /// Builds the complete GROUP BY SQL query using GroupByBuilder.
        /// </summary>
        private (string Sql, IDictionary<string, object> Parameters) BuildQuery<TResult>(
            Expression<Func<IGrouping<TKey, T>, TResult>> selector)
        {
            var builder = new GroupByBuilder(_mapping, _dialect);

            var (sql, parameters) = builder.BuildGroupByQuery(
                _tableName,
                _groupByExpression,
                selector,
                _whereClause,
                _whereParameters,
                _havingClause,
                _orderByClauses);

            return (sql, parameters);
        }

        /// <summary>
        /// Extracts the column name from an OrderBy expression.
        /// Handles both property access (x => x.Total) and x.Key for ordering by group key.
        /// </summary>
        private string ExtractOrderByColumn<TOrderKey>(Expression<Func<IGrouping<TKey, T>, TOrderKey>> keySelector)
        {
            var body = keySelector.Body;

            // Unwrap Convert if present
            if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                body = unary.Operand;

            // Check for x.Key (ordering by group key)
            if (body is MemberExpression member && member.Member.Name == "Key")
            {
                // Get the GROUP BY columns from the groupBy expression
                var groupByBody = _groupByExpression.Body;
                if (groupByBody is UnaryExpression unaryGroupBy && unaryGroupBy.NodeType == ExpressionType.Convert)
                    groupByBody = unaryGroupBy.Operand;

                // Single column GroupBy: return the column name
                if (groupByBody is MemberExpression groupByMember)
                {
                    var propName = groupByMember.Member.Name;
                    var col = _mapping.Columns.FirstOrDefault(c => c.Property.Name == propName);
                    if (col == null)
                        throw new InvalidOperationException($"Property '{propName}' is not a mapped column.");
                    return col.ColumnName;
                }

                // Multi-column GroupBy: not supported for x.Key directly
                throw new NotSupportedException(
                    "Cannot order by x.Key with multi-column GroupBy. " +
                    "Use the property name from the Select projection instead.");
            }

            // Simple property access: x => x.Total
            if (body is MemberExpression propertyMember)
            {
                return propertyMember.Member.Name;
            }

            // Method call (aggregate): x => x.Count() - not typically used in OrderBy after Select
            // but we'll support it for consistency
            if (body is MethodCallExpression)
            {
                throw new NotSupportedException(
                    "OrderBy on aggregate functions should reference the Select projection property name, " +
                    "not the aggregate function itself. Example: .OrderBy(x => x.Total) where Total is defined in Select.");
            }

            throw new NotSupportedException(
                $"OrderBy expression '{keySelector}' is not supported. " +
                $"Use a property from the Select projection (e.g., .OrderBy(x => x.Total)).");
        }
    }
}
