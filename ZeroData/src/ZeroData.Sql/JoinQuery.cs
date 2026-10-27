using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ZeroData.Sql.Dialects;
using ZeroData.Sql.Mapping;
using ZeroData.Sql.Sql;

namespace ZeroData.Sql
{
    /// <summary>
    /// Represents a LINQ-style join query between two tables.
    /// Supports INNER JOIN and LEFT JOIN operations, optional WHERE filtering,
    /// and both sync and async materialization.
    /// </summary>
    public class JoinQuery<T1, T2, TResult> where T1 : class where T2 : class
    {
        private readonly IDbConnection _connection;
        private readonly IDbTransaction _transaction;
        private readonly ISqlDialect _dialect;
        private readonly EntityMapping _mapping1;
        private readonly EntityMapping _mapping2;
        private readonly Expression<Func<T1, object>> _outerKeySelector;
        private readonly Expression<Func<T2, object>> _innerKeySelector;
        private readonly Expression<Func<T1, T2, TResult>> _resultSelector;
        private readonly JoinType _joinType;
        private readonly List<Expression<Func<T1, T2, bool>>> _wherePredicates
            = new List<Expression<Func<T1, T2, bool>>>();

        internal JoinQuery(
            IDbConnection connection,
            IDbTransaction transaction,
            Expression<Func<T1, object>> outerKeySelector,
            Expression<Func<T2, object>> innerKeySelector,
            Expression<Func<T1, T2, TResult>> resultSelector,
            JoinType joinType,
            ISqlDialect dialect = null)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _transaction = transaction;
            _dialect = dialect ?? SqlGenerator.DefaultDialect;
            _mapping1 = MappingCache.GetMapping<T1>();
            _mapping2 = MappingCache.GetMapping<T2>();
            _outerKeySelector = outerKeySelector ?? throw new ArgumentNullException(nameof(outerKeySelector));
            _innerKeySelector = innerKeySelector ?? throw new ArgumentNullException(nameof(innerKeySelector));
            _resultSelector = resultSelector ?? throw new ArgumentNullException(nameof(resultSelector));
            _joinType = joinType;
        }

        /// <summary>
        /// Filters the join results with a WHERE clause that can reference both joined entities.
        /// Multiple calls are combined with AND.
        /// Example: .Where((o, c) =&gt; c.Country == "VN" &amp;&amp; o.Amount &gt; 100)
        /// </summary>
        public JoinQuery<T1, T2, TResult> Where(Expression<Func<T1, T2, bool>> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            _wherePredicates.Add(predicate);
            return this;
        }

        /// <summary>
        /// Executes the join query and returns the results.
        /// </summary>
        public List<TResult> ToList()
        {
            var (sql, parameters) = Build();

            if (ProjectionMaterializer.RequiresManualMaterialization(typeof(TResult)))
            {
                var rows = _connection.Query(sql, parameters, _transaction);
                return ProjectionMaterializer.Materialize<TResult>(rows.Cast<object>());
            }

            return _connection.Query<TResult>(sql, parameters, _transaction).ToList();
        }

        /// <summary>
        /// Async version of ToList.
        /// </summary>
        public async Task<List<TResult>> ToListAsync()
        {
            var (sql, parameters) = Build();

            if (ProjectionMaterializer.RequiresManualMaterialization(typeof(TResult)))
            {
                var rows = await _connection.QueryAsync(sql, parameters, _transaction).ConfigureAwait(false);
                return ProjectionMaterializer.Materialize<TResult>(rows.Cast<object>());
            }

            return (await _connection.QueryAsync<TResult>(sql, parameters, _transaction)
                .ConfigureAwait(false)).ToList();
        }

        /// <summary>
        /// Returns the first result or throws an exception if no results.
        /// </summary>
        public TResult First()
        {
            var results = ToList();
            if (results.Count == 0)
                throw new InvalidOperationException("Sequence contains no elements");
            return results[0];
        }

        /// <summary>
        /// Returns the first result or default if no results.
        /// </summary>
        public TResult FirstOrDefault()
        {
            var results = ToList();
            return results.Count > 0 ? results[0] : default(TResult);
        }

        /// <summary>
        /// Async version of FirstOrDefault.
        /// </summary>
        public async Task<TResult> FirstOrDefaultAsync()
        {
            var results = await ToListAsync().ConfigureAwait(false);
            return results.Count > 0 ? results[0] : default(TResult);
        }

        /// <summary>
        /// Returns the SQL query for debugging purposes.
        /// </summary>
        public string ToSql()
        {
            var (sql, _) = Build();
            return sql;
        }

        private (string Sql, IDictionary<string, object> Parameters) Build()
        {
            var builder = new JoinBuilder(_mapping1, _mapping2, _dialect);
            return builder.BuildJoinQuery(
                _outerKeySelector,
                _innerKeySelector,
                _resultSelector,
                _joinType,
                _wherePredicates);
        }
    }

    /// <summary>
    /// Type of join operation.
    /// </summary>
    public enum JoinType
    {
        Inner,
        Left
    }

    /// <summary>
    /// Extension methods for creating join queries.
    /// </summary>
    public static class JoinExtensions
    {
        /// <summary>
        /// Performs an INNER JOIN between two tables.
        /// </summary>
        public static JoinQuery<T1, T2, TResult> Join<T1, T2, TKey, TResult>(
            this Table<T1> outer,
            Table<T2> inner,
            Expression<Func<T1, TKey>> outerKeySelector,
            Expression<Func<T2, TKey>> innerKeySelector,
            Expression<Func<T1, T2, TResult>> resultSelector)
            where T1 : class
            where T2 : class
        {
            return BuildJoin(outer, inner, outerKeySelector, innerKeySelector, resultSelector, JoinType.Inner);
        }

        /// <summary>
        /// Performs a LEFT JOIN between two tables.
        /// </summary>
        public static JoinQuery<T1, T2, TResult> LeftJoin<T1, T2, TKey, TResult>(
            this Table<T1> outer,
            Table<T2> inner,
            Expression<Func<T1, TKey>> outerKeySelector,
            Expression<Func<T2, TKey>> innerKeySelector,
            Expression<Func<T1, T2, TResult>> resultSelector)
            where T1 : class
            where T2 : class
        {
            return BuildJoin(outer, inner, outerKeySelector, innerKeySelector, resultSelector, JoinType.Left);
        }

        private static JoinQuery<T1, T2, TResult> BuildJoin<T1, T2, TKey, TResult>(
            Table<T1> outer,
            Table<T2> inner,
            Expression<Func<T1, TKey>> outerKeySelector,
            Expression<Func<T2, TKey>> innerKeySelector,
            Expression<Func<T1, T2, TResult>> resultSelector,
            JoinType joinType)
            where T1 : class
            where T2 : class
        {
            if (outer == null) throw new ArgumentNullException(nameof(outer));
            if (inner == null) throw new ArgumentNullException(nameof(inner));

            Expression<Func<T1, object>> outerKey = Expression.Lambda<Func<T1, object>>(
                Expression.Convert(outerKeySelector.Body, typeof(object)),
                outerKeySelector.Parameters);

            Expression<Func<T2, object>> innerKey = Expression.Lambda<Func<T2, object>>(
                Expression.Convert(innerKeySelector.Body, typeof(object)),
                innerKeySelector.Parameters);

            return new JoinQuery<T1, T2, TResult>(
                outer.Connection,
                outer.Transaction,
                outerKey,
                innerKey,
                resultSelector,
                joinType,
                outer.Dialect);
        }
    }
}
