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
    /// Shared execution backing for multi-table join chains. Accumulates join sources and
    /// WHERE predicates, then builds + runs the query. Lambda parameters are positional:
    /// the j-th parameter maps to the j-th joined table.
    /// </summary>
    internal sealed class MultiJoinState
    {
        public readonly IDbConnection Connection;
        public readonly IDbTransaction Transaction;
        public readonly ISqlDialect Dialect;
        public readonly List<JoinSource> Sources = new List<JoinSource>();
        public readonly List<LambdaExpression> Wheres = new List<LambdaExpression>();

        public MultiJoinState(IDbConnection connection, IDbTransaction transaction, ISqlDialect dialect)
        {
            Connection = connection;
            Transaction = transaction;
            Dialect = dialect ?? SqlGenerator.DefaultDialect;
        }

        public void Add<T>(JoinType type, LambdaExpression on) where T : class
        {
            Sources.Add(new JoinSource
            {
                Mapping = MappingCache.GetMapping<T>(),
                Alias = $"t{Sources.Count + 1}",
                JoinType = type,
                On = on
            });
        }

        public (string Sql, IDictionary<string, object> Parameters) Build(LambdaExpression selector)
        {
            var builder = new MultiJoinBuilder(Dialect);
            return builder.Build(Sources, selector, Wheres);
        }

        public List<TResult> Execute<TResult>(LambdaExpression selector)
        {
            var (sql, ps) = Build(selector);
            if (ProjectionMaterializer.RequiresManualMaterialization(typeof(TResult)))
            {
                var rows = Connection.Query(sql, ps, Transaction);
                return ProjectionMaterializer.Materialize<TResult>(rows.Cast<object>());
            }
            return Connection.Query<TResult>(sql, ps, Transaction).ToList();
        }

        public async Task<List<TResult>> ExecuteAsync<TResult>(LambdaExpression selector)
        {
            var (sql, ps) = Build(selector);
            if (ProjectionMaterializer.RequiresManualMaterialization(typeof(TResult)))
            {
                var rows = await Connection.QueryAsync(sql, ps, Transaction).ConfigureAwait(false);
                return ProjectionMaterializer.Materialize<TResult>(rows.Cast<object>());
            }
            return (await Connection.QueryAsync<TResult>(sql, ps, Transaction).ConfigureAwait(false)).ToList();
        }
    }

    /// <summary>Two-table join chain. Add a third table with Join/LeftJoin.</summary>
    public sealed class JoinChain<T1, T2>
        where T1 : class where T2 : class
    {
        private readonly MultiJoinState _state;
        internal JoinChain(MultiJoinState state) { _state = state; }

        public JoinChain<T1, T2> Where(Expression<Func<T1, T2, bool>> predicate)
        {
            _state.Wheres.Add(predicate);
            return this;
        }

        public JoinChain<T1, T2, T3> Join<T3>(Table<T3> table, Expression<Func<T1, T2, T3, bool>> on) where T3 : class
        {
            _state.Add<T3>(JoinType.Inner, on);
            return new JoinChain<T1, T2, T3>(_state);
        }

        public JoinChain<T1, T2, T3> LeftJoin<T3>(Table<T3> table, Expression<Func<T1, T2, T3, bool>> on) where T3 : class
        {
            _state.Add<T3>(JoinType.Left, on);
            return new JoinChain<T1, T2, T3>(_state);
        }

        public List<TResult> Select<TResult>(Expression<Func<T1, T2, TResult>> selector) => _state.Execute<TResult>(selector);
        public Task<List<TResult>> SelectAsync<TResult>(Expression<Func<T1, T2, TResult>> selector) => _state.ExecuteAsync<TResult>(selector);
        public string ToSql<TResult>(Expression<Func<T1, T2, TResult>> selector) => _state.Build(selector).Sql;
    }

    /// <summary>Three-table join chain. Add a fourth table with Join/LeftJoin.</summary>
    public sealed class JoinChain<T1, T2, T3>
        where T1 : class where T2 : class where T3 : class
    {
        private readonly MultiJoinState _state;
        internal JoinChain(MultiJoinState state) { _state = state; }

        public JoinChain<T1, T2, T3> Where(Expression<Func<T1, T2, T3, bool>> predicate)
        {
            _state.Wheres.Add(predicate);
            return this;
        }

        public JoinChain<T1, T2, T3, T4> Join<T4>(Table<T4> table, Expression<Func<T1, T2, T3, T4, bool>> on) where T4 : class
        {
            _state.Add<T4>(JoinType.Inner, on);
            return new JoinChain<T1, T2, T3, T4>(_state);
        }

        public JoinChain<T1, T2, T3, T4> LeftJoin<T4>(Table<T4> table, Expression<Func<T1, T2, T3, T4, bool>> on) where T4 : class
        {
            _state.Add<T4>(JoinType.Left, on);
            return new JoinChain<T1, T2, T3, T4>(_state);
        }

        public List<TResult> Select<TResult>(Expression<Func<T1, T2, T3, TResult>> selector) => _state.Execute<TResult>(selector);
        public Task<List<TResult>> SelectAsync<TResult>(Expression<Func<T1, T2, T3, TResult>> selector) => _state.ExecuteAsync<TResult>(selector);
        public string ToSql<TResult>(Expression<Func<T1, T2, T3, TResult>> selector) => _state.Build(selector).Sql;
    }

    /// <summary>Four-table join chain (terminal — extend similarly for 5+).</summary>
    public sealed class JoinChain<T1, T2, T3, T4>
        where T1 : class where T2 : class where T3 : class where T4 : class
    {
        private readonly MultiJoinState _state;
        internal JoinChain(MultiJoinState state) { _state = state; }

        public JoinChain<T1, T2, T3, T4> Where(Expression<Func<T1, T2, T3, T4, bool>> predicate)
        {
            _state.Wheres.Add(predicate);
            return this;
        }

        public List<TResult> Select<TResult>(Expression<Func<T1, T2, T3, T4, TResult>> selector) => _state.Execute<TResult>(selector);
        public Task<List<TResult>> SelectAsync<TResult>(Expression<Func<T1, T2, T3, T4, TResult>> selector) => _state.ExecuteAsync<TResult>(selector);
        public string ToSql<TResult>(Expression<Func<T1, T2, T3, T4, TResult>> selector) => _state.Build(selector).Sql;
    }

    /// <summary>
    /// Entry points for multi-table joins. Start with JoinMany to build a chain that supports 3+ tables.
    /// </summary>
    public static class MultiJoinExtensions
    {
        /// <summary>
        /// Starts a multi-table join chain: db.GetTable&lt;A&gt;().JoinMany(db.GetTable&lt;B&gt;(), (a,b) =&gt; a.Id == b.AId)
        /// then chain .Join/.LeftJoin/.Where and finish with .Select(...).
        /// </summary>
        public static JoinChain<T1, T2> JoinMany<T1, T2>(
            this Table<T1> outer, Table<T2> inner, Expression<Func<T1, T2, bool>> on)
            where T1 : class where T2 : class
        {
            if (outer == null) throw new ArgumentNullException(nameof(outer));
            if (inner == null) throw new ArgumentNullException(nameof(inner));
            if (on == null) throw new ArgumentNullException(nameof(on));

            var state = new MultiJoinState(outer.Connection, outer.Transaction, outer.Dialect);
            state.Add<T1>(JoinType.Inner, null);   // driving table (no ON)
            state.Add<T2>(JoinType.Inner, WrapOn<T1, T2>(on));
            return new JoinChain<T1, T2>(state);
        }

        /// <summary>Starts a multi-table chain with a LEFT JOIN as the second table.</summary>
        public static JoinChain<T1, T2> LeftJoinMany<T1, T2>(
            this Table<T1> outer, Table<T2> inner, Expression<Func<T1, T2, bool>> on)
            where T1 : class where T2 : class
        {
            if (outer == null) throw new ArgumentNullException(nameof(outer));
            if (inner == null) throw new ArgumentNullException(nameof(inner));
            if (on == null) throw new ArgumentNullException(nameof(on));

            var state = new MultiJoinState(outer.Connection, outer.Transaction, outer.Dialect);
            state.Add<T1>(JoinType.Inner, null);
            state.Add<T2>(JoinType.Left, WrapOn<T1, T2>(on));
            return new JoinChain<T1, T2>(state);
        }

        // The 2-table ON lambda already has positional parameters (t1, t2) — pass through.
        private static LambdaExpression WrapOn<T1, T2>(Expression<Func<T1, T2, bool>> on) => on;
    }
}
