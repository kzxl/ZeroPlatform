using ZeroData.Core;
using ZeroData.Sql.Caching;
using ZeroData.Sql.ChangeTracking;
using ZeroData.Sql.Mapping;
using ZeroData.Sql.Sql;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroData.Sql
{
    /// <summary>
    /// Represents a database table for entity type T.
    /// Compatible with System.Data.Linq.Table&lt;T&gt;.
    /// Provides sync + async CRUD and querying.
    /// </summary>
    public class Table<T> : IEnumerable<T> where T : class
    {
        private readonly SqlContext _context;
        private readonly ChangeTracker _changeTracker;
        private bool _noTracking;
        private bool _ignoreFilters;
        private string _queryTag;
        private List<string> _orderByClauses;
        private int? _skip;
        private int? _take;
        // Deferred predicates accumulated via WhereIf/AndWhere before a terminal operation.
        private List<Expression<Func<T, bool>>> _pendingPredicates;
        // Raw WHERE fragments (e.g. EXISTS / IN subqueries) accumulated before a terminal operation.
        private List<(string Sql, IDictionary<string, object> Parameters)> _rawWhereFragments;
        private int _subqueryAliasSeq;
        private int _subqueryParamSeed;
        private TimeSpan? _cacheDuration;

        internal Table(SqlContext context, ChangeTracker changeTracker)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _changeTracker = changeTracker ?? throw new ArgumentNullException(nameof(changeTracker));
        }

        // Internal properties for Join support
        internal System.Data.IDbConnection Connection => _context.Connection;
        internal System.Data.IDbTransaction Transaction => _context.Transaction;
        internal SqlContext Context => _context;
        internal ZeroData.Sql.Dialects.ISqlDialect Dialect => _context.Dialect;

        /// <summary>
        /// Records a predicate to be combined (AND) with the next terminal query operation.
        /// Used by WhereIf and AndWhere for deferred, accumulated filtering.
        /// </summary>
        internal void AddPendingPredicate(Expression<Func<T, bool>> predicate)
        {
            if (predicate == null) return;
            if (_pendingPredicates == null)
                _pendingPredicates = new List<Expression<Func<T, bool>>>();
            _pendingPredicates.Add(predicate);
        }

        /// <summary>
        /// Combines all accumulated pending predicates with the supplied predicate using AND.
        /// Returns null if there is nothing to filter on.
        /// </summary>
        private Expression<Func<T, bool>> CombinePending(Expression<Func<T, bool>> predicate)
        {
            var all = new List<Expression<Func<T, bool>>>();
            if (_pendingPredicates != null) all.AddRange(_pendingPredicates);
            if (predicate != null) all.Add(predicate);
            if (all.Count == 0) return null;

            var combined = all[0];
            for (int i = 1; i < all.Count; i++)
                combined = AndAlso(combined, all[i]);
            return combined;
        }

        /// <summary>
        /// Builds a new lambda that ANDs two predicates over a shared parameter.
        /// </summary>
        private static Expression<Func<T, bool>> AndAlso(
            Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
        {
            var param = Expression.Parameter(typeof(T), "x");
            var leftBody = new ParameterRebinder(left.Parameters[0], param).Visit(left.Body);
            var rightBody = new ParameterRebinder(right.Parameters[0], param).Visit(right.Body);
            return Expression.Lambda<Func<T, bool>>(
                Expression.AndAlso(leftBody, rightBody), param);
        }

        /// <summary>
        /// Rewrites a lambda body to use a replacement parameter so two lambdas can be merged.
        /// </summary>
        private sealed class ParameterRebinder : ExpressionVisitor
        {
            private readonly ParameterExpression _from;
            private readonly ParameterExpression _to;
            public ParameterRebinder(ParameterExpression from, ParameterExpression to)
            {
                _from = from;
                _to = to;
            }
            protected override Expression VisitParameter(ParameterExpression node)
                => node == _from ? _to : base.VisitParameter(node);
        }

        #region Insert / Delete / Attach

        /// <summary>
        /// Modern, ergonomic shortcut for InsertOnSubmit.
        /// </summary>
        public void Insert(T entity) => InsertOnSubmit(entity);

        /// <summary>
        /// Modern, ergonomic shortcut for InsertAllOnSubmit.
        /// </summary>
        public void InsertRange(IEnumerable<T> entities) => InsertAllOnSubmit(entities);

        /// <summary>
        /// Adds an entity for insertion (EF / modern collection style).
        /// </summary>
        public void Add(T entity) => InsertOnSubmit(entity);

        /// <summary>
        /// Adds multiple entities for insertion.
        /// </summary>
        public void AddRange(IEnumerable<T> entities) => InsertAllOnSubmit(entities);

        /// <summary>
        /// Modern, ergonomic shortcut for DeleteOnSubmit.
        /// </summary>
        public void Delete(T entity) => DeleteOnSubmit(entity);

        /// <summary>
        /// Modern, ergonomic shortcut for DeleteAllOnSubmit.
        /// </summary>
        public void DeleteRange(IEnumerable<T> entities) => DeleteAllOnSubmit(entities);

        /// <summary>
        /// Removes an entity (EF / collection style).
        /// </summary>
        public void Remove(T entity) => DeleteOnSubmit(entity);

        /// <summary>
        /// Removes multiple entities.
        /// </summary>
        public void RemoveRange(IEnumerable<T> entities) => DeleteAllOnSubmit(entities);

        /// <summary>
        /// Attaches and marks an entity as modified for update.
        /// </summary>
        public void Update(T entity) => Attach(entity, asModified: true);

        /// <summary>
        /// Deletes an entity directly by primary key without fetching it first.
        /// Executes a single DELETE (or soft-delete UPDATE) statement on the database server.
        /// </summary>
        public int DeleteById(object id, bool forceHardDelete = false)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            var mapping = MappingCache.GetMapping<T>();
            var pks = mapping.PrimaryKeys.ToList();
            if (pks.Count != 1)
                throw new InvalidOperationException($"DeleteById requires a single primary key, but {typeof(T).Name} has {pks.Count}.");

            var pk = pks[0];
            var dialect = _context.Dialect;

            var softDelete = !forceHardDelete ? mapping.EntityType
                .GetCustomAttributes(typeof(SoftDeleteAttribute), true)
                .FirstOrDefault() as SoftDeleteAttribute : null;

            string sql;
            var dp = new DynamicParameters();
            dp.Add("@id", id);

            if (softDelete != null)
            {
                var trueLiteral = dialect.ProviderName == "PostgreSQL" ? "TRUE" : "1";
                sql = $"UPDATE {SqlGenerator.QuoteTableName(mapping.TableName, dialect)} SET {dialect.QuoteIdentifier(softDelete.ColumnName)} = {trueLiteral} WHERE {dialect.QuoteIdentifier(pk.ColumnName)} = @id";
            }
            else
            {
                sql = $"DELETE FROM {SqlGenerator.QuoteTableName(mapping.TableName, dialect)} WHERE {dialect.QuoteIdentifier(pk.ColumnName)} = @id";
            }

            _context.EnsureConnectionOpen();
            var rows = _context.Connection.Execute(sql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout);

            _changeTracker.DetachByKey(mapping, id);
            _context.QueryCache?.Invalidate(typeof(T));
            return rows;
        }

        /// <summary>
        /// Asynchronously deletes an entity directly by primary key without fetching it first.
        /// </summary>
        public async Task<int> DeleteByIdAsync(object id, bool forceHardDelete = false, CancellationToken ct = default)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            var mapping = MappingCache.GetMapping<T>();
            var pks = mapping.PrimaryKeys.ToList();
            if (pks.Count != 1)
                throw new InvalidOperationException($"DeleteById requires a single primary key, but {typeof(T).Name} has {pks.Count}.");

            var pk = pks[0];
            var dialect = _context.Dialect;

            var softDelete = !forceHardDelete ? mapping.EntityType
                .GetCustomAttributes(typeof(SoftDeleteAttribute), true)
                .FirstOrDefault() as SoftDeleteAttribute : null;

            string sql;
            var dp = new DynamicParameters();
            dp.Add("@id", id);

            if (softDelete != null)
            {
                var trueLiteral = dialect.ProviderName == "PostgreSQL" ? "TRUE" : "1";
                sql = $"UPDATE {SqlGenerator.QuoteTableName(mapping.TableName, dialect)} SET {dialect.QuoteIdentifier(softDelete.ColumnName)} = {trueLiteral} WHERE {dialect.QuoteIdentifier(pk.ColumnName)} = @id";
            }
            else
            {
                sql = $"DELETE FROM {SqlGenerator.QuoteTableName(mapping.TableName, dialect)} WHERE {dialect.QuoteIdentifier(pk.ColumnName)} = @id";
            }

            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var rows = await _context.Connection.ExecuteAsync(new CommandDefinition(sql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout, cancellationToken: ct)).ConfigureAwait(false);

            _changeTracker.DetachByKey(mapping, id);
            _context.QueryCache?.Invalidate(typeof(T));
            return rows;
        }

        /// <summary>
        /// Deletes all entities matching the specified predicate directly on the server without loading them into memory.
        /// Respects SoftDeleteAttribute unless forceHardDelete is true.
        /// </summary>
        public int DeleteWhere(Expression<Func<T, bool>> predicate, bool forceHardDelete = false)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var mapping = MappingCache.GetMapping<T>();
            var dialect = _context.Dialect;
            var builder = new WhereBuilder(mapping, dialect);
            var (whereSql, parameters) = builder.Build(predicate);

            var softDelete = !forceHardDelete ? mapping.EntityType
                .GetCustomAttributes(typeof(SoftDeleteAttribute), true)
                .FirstOrDefault() as SoftDeleteAttribute : null;

            string sql;
            if (softDelete != null)
            {
                var trueLiteral = dialect.ProviderName == "PostgreSQL" ? "TRUE" : "1";
                sql = $"UPDATE {SqlGenerator.QuoteTableName(mapping.TableName, dialect)} SET {dialect.QuoteIdentifier(softDelete.ColumnName)} = {trueLiteral} WHERE {whereSql}";
            }
            else
            {
                sql = $"DELETE FROM {SqlGenerator.QuoteTableName(mapping.TableName, dialect)} WHERE {whereSql}";
            }

            var dp = new DynamicParameters();
            if (parameters != null)
            {
                foreach (var kv in parameters) dp.Add(kv.Key, kv.Value);
            }

            _context.EnsureConnectionOpen();
            var rows = _context.Connection.Execute(sql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout);

            DetachMatchingTracked(predicate);
            _context.QueryCache?.Invalidate(typeof(T));
            return rows;
        }

        /// <summary>
        /// Asynchronously deletes all entities matching the specified predicate directly on the server.
        /// </summary>
        public async Task<int> DeleteWhereAsync(Expression<Func<T, bool>> predicate, bool forceHardDelete = false, CancellationToken ct = default)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var mapping = MappingCache.GetMapping<T>();
            var dialect = _context.Dialect;
            var builder = new WhereBuilder(mapping, dialect);
            var (whereSql, parameters) = builder.Build(predicate);

            var softDelete = !forceHardDelete ? mapping.EntityType
                .GetCustomAttributes(typeof(SoftDeleteAttribute), true)
                .FirstOrDefault() as SoftDeleteAttribute : null;

            string sql;
            if (softDelete != null)
            {
                var trueLiteral = dialect.ProviderName == "PostgreSQL" ? "TRUE" : "1";
                sql = $"UPDATE {SqlGenerator.QuoteTableName(mapping.TableName, dialect)} SET {dialect.QuoteIdentifier(softDelete.ColumnName)} = {trueLiteral} WHERE {whereSql}";
            }
            else
            {
                sql = $"DELETE FROM {SqlGenerator.QuoteTableName(mapping.TableName, dialect)} WHERE {whereSql}";
            }

            var dp = new DynamicParameters();
            if (parameters != null)
            {
                foreach (var kv in parameters) dp.Add(kv.Key, kv.Value);
            }

            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var rows = await _context.Connection.ExecuteAsync(new CommandDefinition(sql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout, cancellationToken: ct)).ConfigureAwait(false);

            DetachMatchingTracked(predicate);
            _context.QueryCache?.Invalidate(typeof(T));
            return rows;
        }

        /// <summary>
        /// Updates all entities matching the specified predicate directly on the database server.
        /// Accepts an anonymous object, dictionary, or member factory expression.
        /// Example: table.UpdateWhere(x => x.Status == "Pending", new { Status = "Processed" });
        /// </summary>
        public int UpdateWhere(Expression<Func<T, bool>> predicate, object updateValues)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            if (updateValues == null) throw new ArgumentNullException(nameof(updateValues));

            var (sql, dp) = BuildUpdateWhereSql(predicate, updateValues);
            _context.EnsureConnectionOpen();
            var rows = _context.Connection.Execute(sql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout);

            DetachMatchingTracked(predicate);
            _context.QueryCache?.Invalidate(typeof(T));
            return rows;
        }

        /// <summary>
        /// Asynchronously updates all entities matching the specified predicate directly on the database server.
        /// Accepts an anonymous object, dictionary, or member factory expression.
        /// </summary>
        public async Task<int> UpdateWhereAsync(Expression<Func<T, bool>> predicate, object updateValues, CancellationToken ct = default)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            if (updateValues == null) throw new ArgumentNullException(nameof(updateValues));

            var (sql, dp) = BuildUpdateWhereSql(predicate, updateValues);
            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var rows = await _context.Connection.ExecuteAsync(new CommandDefinition(sql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout, cancellationToken: ct)).ConfigureAwait(false);

            DetachMatchingTracked(predicate);
            _context.QueryCache?.Invalidate(typeof(T));
            return rows;
        }

        /// <summary>
        /// Updates all entities matching the specified predicate directly on the server using an expression factory.
        /// Example: table.UpdateWhere(x => x.Status == "Pending", x => new Order { Status = "Processed" });
        /// </summary>
        public int UpdateWhere(Expression<Func<T, bool>> predicate, Expression<Func<T, T>> updateFactory)
            => UpdateWhere(predicate, (object)updateFactory);

        /// <summary>
        /// Asynchronously updates all entities matching the specified predicate directly on the server using an expression factory.
        /// </summary>
        public Task<int> UpdateWhereAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, T>> updateFactory, CancellationToken ct = default)
            => UpdateWhereAsync(predicate, (object)updateFactory, ct);

        private (string Sql, DynamicParameters Parameters) BuildUpdateWhereSql(Expression<Func<T, bool>> predicate, object updateValues)
        {
            var mapping = MappingCache.GetMapping<T>();
            var dialect = _context.Dialect;
            var table = SqlGenerator.QuoteTableName(mapping.TableName, dialect);
            var builder = new WhereBuilder(mapping, dialect);
            var (whereSql, parameters) = builder.Build(predicate);

            var values = ExtractUpdateValues(updateValues, mapping);
            var setClauses = new List<string>();
            var dp = new DynamicParameters();

            if (parameters != null)
            {
                foreach (var kv in parameters) dp.Add(kv.Key, kv.Value);
            }

            int idx = 0;
            foreach (var kvp in values)
            {
                var col = mapping.Columns.FirstOrDefault(c => string.Equals(c.Property.Name, kvp.Key, StringComparison.OrdinalIgnoreCase)
                                                           || string.Equals(c.ColumnName, kvp.Key, StringComparison.OrdinalIgnoreCase));
                if (col == null || col.IsPrimaryKey || col.IsDbGenerated) continue;

                var paramName = $"@upd_{idx++}_{col.Property.Name}";
                setClauses.Add($"{dialect.QuoteIdentifier(col.ColumnName)} = {paramName}");
                dp.Add(paramName, kvp.Value);
            }

            if (setClauses.Count == 0)
                throw new InvalidOperationException("No valid updatable columns were provided for UpdateWhere.");

            var sql = $"UPDATE {table} SET {string.Join(", ", setClauses)} WHERE {whereSql}";
            return (sql, dp);
        }

        private static IDictionary<string, object> ExtractUpdateValues(object updateValues, EntityMapping mapping)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            if (updateValues is IDictionary<string, object> dict)
            {
                foreach (var kvp in dict) result[kvp.Key] = kvp.Value;
                return result;
            }

            if (updateValues is LambdaExpression lambda)
            {
                if (lambda.Body is MemberInitExpression init)
                {
                    foreach (var binding in init.Bindings)
                    {
                        if (binding is MemberAssignment assignment)
                        {
                            result[assignment.Member.Name] = EvaluateExpression(assignment.Expression);
                        }
                    }
                    return result;
                }
                else if (lambda.Body is NewExpression newExpr && newExpr.Members != null)
                {
                    for (int i = 0; i < newExpr.Members.Count; i++)
                    {
                        result[newExpr.Members[i].Name] = EvaluateExpression(newExpr.Arguments[i]);
                    }
                    return result;
                }
                throw new NotSupportedException($"Unsupported update expression body: {lambda.Body.GetType().Name}. Expected MemberInit or NewExpression.");
            }

            var type = updateValues.GetType();
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;
                result[prop.Name] = prop.GetValue(updateValues, null);
            }

            return result;
        }

        private static object EvaluateExpression(Expression expr)
        {
            if (expr == null) return null;
            if (expr is ConstantExpression c) return c.Value;
            if (expr is MemberExpression member && member.Expression is ConstantExpression innerConst)
            {
                if (member.Member is FieldInfo field) return field.GetValue(innerConst.Value);
                if (member.Member is PropertyInfo prop) return prop.GetValue(innerConst.Value, null);
            }
            if (expr is UnaryExpression u && u.NodeType == ExpressionType.Convert)
            {
                return EvaluateExpression(u.Operand);
            }

            var lambda = Expression.Lambda(expr);
            return lambda.Compile().DynamicInvoke();
        }

        private void DetachMatchingTracked(Expression<Func<T, bool>> predicate)
        {
            try
            {
                var compiled = ExpressionCache.GetOrAdd(predicate);
                var toDetach = _changeTracker.Entries<T>()
                    .Where(e => compiled(e.Entity))
                    .Select(e => e.Entity)
                    .ToList();
                foreach (var entity in toDetach)
                {
                    _changeTracker.Detach(entity);
                }
            }
            catch
            {
                // In case predicate cannot be evaluated in memory (e.g. database-specific methods)
            }
        }

        /// <summary>
        /// Detaches an entity from change tracking.
        /// </summary>
        public void Detach(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _changeTracker.Detach(entity);
        }

        /// <summary>
        /// Fast primary key lookup. Shortcut for Find(id).
        /// </summary>
        public T Get(object id) => Find(id);

        /// <summary>
        /// Asynchronously finds an entity quickly by primary key.
        /// </summary>
        public Task<T> GetAsync(object id, CancellationToken ct = default) => FindInternalAsync(new object[] { id }, ct);

        public void InsertOnSubmit(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _changeTracker.TrackInsert(entity);
        }

        public void InsertAllOnSubmit(IEnumerable<T> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            foreach (var e in entities) InsertOnSubmit(e);
        }

        public void DeleteOnSubmit(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _changeTracker.TrackDelete(entity);
        }

        public void DeleteAllOnSubmit(IEnumerable<T> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            foreach (var e in entities) DeleteOnSubmit(e);
        }

        /// <summary>
        /// Bulk inserts entities bypassing change tracking, using SqlBulkCopy (SQL Server) or multi-row INSERT with compiled getters.
        /// </summary>
        public int BulkInsert(IEnumerable<T> entities) => _context.BulkInsert(entities);

        /// <summary>
        /// Asynchronously bulk inserts entities bypassing change tracking, using SqlBulkCopy (SQL Server) or multi-row INSERT with compiled getters.
        /// </summary>
        public Task<int> BulkInsertAsync(IEnumerable<T> entities, CancellationToken ct = default) => _context.BulkInsertAsync(entities, ct);

        /// <summary>
        /// Bulk updates entities by primary key bypassing change tracking.
        /// </summary>
        public int BulkUpdate(IEnumerable<T> entities) => _context.BulkUpdate(entities);

        /// <summary>
        /// Asynchronously bulk updates entities by primary key bypassing change tracking.
        /// </summary>
        public Task<int> BulkUpdateAsync(IEnumerable<T> entities, CancellationToken ct = default) => _context.BulkUpdateAsync(entities, ct);

        /// <summary>
        /// Bulk deletes entities by primary key bypassing change tracking.
        /// </summary>
        public int BulkDelete(IEnumerable<T> entities) => _context.BulkDelete(entities);

        /// <summary>
        /// Asynchronously bulk deletes entities by primary key bypassing change tracking.
        /// </summary>
        public Task<int> BulkDeleteAsync(IEnumerable<T> entities, CancellationToken ct = default) => _context.BulkDeleteAsync(entities, ct);

        /// <summary>
        /// Attaches an entity for update tracking.
        /// </summary>
        public void Attach(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _changeTracker.TrackLoaded(entity, MappingCache.GetMapping<T>());
        }

        /// <summary>
        /// Attaches with original baseline for change detection.
        /// </summary>
        public void Attach(T entity, T original)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (original == null) throw new ArgumentNullException(nameof(original));
            _changeTracker.TrackLoadedWithOriginal(entity, original, MappingCache.GetMapping<T>());
        }

        /// <summary>
        /// Attaches and optionally marks as modified.
        /// </summary>
        public void Attach(T entity, bool asModified)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (asModified)
                _changeTracker.TrackUpdate(entity, typeof(T));
            else
                Attach(entity);
        }

        #endregion

        #region AsNoTracking

        /// <summary>
        /// Returns a query configuration that skips change tracking.
        /// Loaded entities will NOT be tracked for updates. Best for read-only queries.
        /// Inspired by EF Core's AsNoTracking().
        /// </summary>
        public Table<T> AsNoTracking()
        {
            _noTracking = true;
            return this;
        }

        #endregion

        #region Caching (L2 Query Cache)

        /// <summary>
        /// Enables second-level query caching for this query with the specified duration.
        /// When cached, identical queries within the duration return cached results without querying the database.
        /// Automatically invalidated when entities of type T are modified via SubmitChanges or Bulk operations.
        /// </summary>
        public Table<T> FromCache(TimeSpan duration)
        {
            _cacheDuration = duration;
            return this;
        }

        /// <summary>
        /// Enables second-level query caching with a default duration of 60 seconds (or optional custom duration).
        /// </summary>
        public Table<T> AsCached(TimeSpan? duration = null)
        {
            _cacheDuration = duration ?? TimeSpan.FromSeconds(60);
            return this;
        }

        #endregion

        #region OrderBy / ThenBy

        /// <summary>
        /// Sorts the results in ascending order by the specified column.
        /// Chain with Where(), ToList(), FirstOrDefault() etc.
        /// Example: db.GetTable&lt;T&gt;().OrderBy(x => x.Date).Where(x => x.Active)
        /// </summary>
        public Table<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _orderByClauses = new List<string>();
            _orderByClauses.Add($"{_context.Dialect.QuoteIdentifier(ExtractColumnName(keySelector))} ASC");
            return this;
        }

        /// <summary>
        /// Sorts the results in descending order by the specified column.
        /// </summary>
        public Table<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _orderByClauses = new List<string>();
            _orderByClauses.Add($"{_context.Dialect.QuoteIdentifier(ExtractColumnName(keySelector))} DESC");
            return this;
        }

        /// <summary>
        /// Adds a secondary ascending sort. Must be called after OrderBy/OrderByDescending.
        /// </summary>
        public Table<T> ThenBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            if (_orderByClauses == null)
                throw new InvalidOperationException("ThenBy must be called after OrderBy or OrderByDescending.");
            _orderByClauses.Add($"{_context.Dialect.QuoteIdentifier(ExtractColumnName(keySelector))} ASC");
            return this;
        }

        /// <summary>
        /// Adds a secondary descending sort. Must be called after OrderBy/OrderByDescending.
        /// </summary>
        public Table<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            if (_orderByClauses == null)
                throw new InvalidOperationException("ThenByDescending must be called after OrderBy or OrderByDescending.");
            _orderByClauses.Add($"{_context.Dialect.QuoteIdentifier(ExtractColumnName(keySelector))} DESC");
            return this;
        }

        #endregion

        #region Skip / Take

        /// <summary>
        /// Skips N rows. For SQL Server, OrderBy is required.
        /// </summary>
        public Table<T> Skip(int count)
        {
            _skip = count;
            return this;
        }

        /// <summary>
        /// Takes at most N rows.
        /// </summary>
        public Table<T> Take(int count)
        {
            _take = count;
            return this;
        }

        #endregion

        #region GroupBy

        /// <summary>
        /// Groups results by the specified key selector.
        /// Must be followed by Select() to project aggregates.
        /// Example: db.Orders.GroupBy(o => o.CustomerId).Select(g => new { g.Key, Count = g.Count() })
        /// </summary>
        public GroupByQuery<T, TKey> GroupBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            var mapping = MappingCache.GetMapping<T>();
            var tableName = SqlGenerator.QuoteTableName(mapping.TableName, _context.Dialect);

            // Capture WHERE clause from accumulated predicates (Where/WhereIf/AndWhere) and global filters.
            string whereClause = null;
            IDictionary<string, object> whereParameters = null;

            var whereParts = new List<string>();
            var allParams = new Dictionary<string, object>();

            if (!_ignoreFilters && _context.Filters.HasFilters)
            {
                var filters = _context.Filters.GetFilters(typeof(T));
                if (filters.Count > 0)
                {
                    var filterBuilder = new WhereBuilder(mapping, _context.Dialect);
                    foreach (var filter in filters)
                    {
                        var (fSql, fParams) = filterBuilder.BuildFromLambda(filter);
                        whereParts.Add(fSql);
                        if (fParams != null)
                            foreach (var kv in fParams) allParams[kv.Key] = kv.Value;
                    }
                }
            }

            var pending = CombinePending(null);
            if (pending != null)
            {
                var builder = new WhereBuilder(mapping, _context.Dialect);
                var (wSql, wParams) = builder.Build(pending);
                whereParts.Add("(" + wSql + ")");
                if (wParams != null)
                    foreach (var kv in wParams) allParams[kv.Key] = kv.Value;
            }

            if (whereParts.Count > 0)
            {
                whereClause = string.Join(" AND ", whereParts);
                whereParameters = allParams;
            }

            // Note: OrderBy/Skip/Take before GroupBy are ignored — apply them after GroupBy.

            return new GroupByQuery<T, TKey>(
                _context,
                mapping,
                tableName,
                keySelector,
                whereClause,
                whereParameters,
                _context.Dialect);
        }

        #endregion

        #region Subqueries (EXISTS / IN)

        /// <summary>
        /// Adds a correlated EXISTS subquery to the WHERE clause (accumulated; combined with AND).
        /// Example: db.Customers.WhereExists(db.Orders, (c, o) =&gt; o.CustomerId == c.Id).ToList()
        /// </summary>
        public Table<T> WhereExists<TSub>(Table<TSub> subTable,
            Expression<Func<T, TSub, bool>> correlation) where TSub : class
        {
            AddExists(subTable, correlation, negate: false);
            return this;
        }

        /// <summary>
        /// Adds a correlated NOT EXISTS subquery to the WHERE clause.
        /// </summary>
        public Table<T> WhereNotExists<TSub>(Table<TSub> subTable,
            Expression<Func<T, TSub, bool>> correlation) where TSub : class
        {
            AddExists(subTable, correlation, negate: true);
            return this;
        }

        /// <summary>
        /// Adds an IN subquery: outerKey IN (SELECT innerKey FROM sub [WHERE filter]).
        /// Example: db.Customers.WhereIn(c =&gt; c.Id, db.Orders, o =&gt; o.CustomerId, o =&gt; o.Status == "Active")
        /// </summary>
        public Table<T> WhereIn<TSub, TKey>(
            Expression<Func<T, TKey>> outerKey,
            Table<TSub> subTable,
            Expression<Func<TSub, TKey>> innerKey,
            Expression<Func<TSub, bool>> innerFilter = null) where TSub : class
        {
            AddIn(outerKey, subTable, innerKey, innerFilter, negate: false);
            return this;
        }

        /// <summary>
        /// Adds a NOT IN subquery.
        /// </summary>
        public Table<T> WhereNotIn<TSub, TKey>(
            Expression<Func<T, TKey>> outerKey,
            Table<TSub> subTable,
            Expression<Func<TSub, TKey>> innerKey,
            Expression<Func<TSub, bool>> innerFilter = null) where TSub : class
        {
            AddIn(outerKey, subTable, innerKey, innerFilter, negate: true);
            return this;
        }

        private void AddExists<TSub>(Table<TSub> subTable,
            Expression<Func<T, TSub, bool>> correlation, bool negate) where TSub : class
        {
            if (subTable == null) throw new ArgumentNullException(nameof(subTable));
            if (correlation == null) throw new ArgumentNullException(nameof(correlation));

            var outerMapping = MappingCache.GetMapping<T>();
            var innerMapping = MappingCache.GetMapping<TSub>();
            var alias = $"sq{_subqueryAliasSeq++}";
            var builder = new SubqueryBuilder(outerMapping, innerMapping, _context.Dialect, alias, _subqueryParamSeed);
            var fragment = builder.BuildExists(correlation, negate);
            _subqueryParamSeed = builder.NextParamIndex;
            AddRawWhere(fragment);
        }

        private void AddIn<TSub, TKey>(
            Expression<Func<T, TKey>> outerKey,
            Table<TSub> subTable,
            Expression<Func<TSub, TKey>> innerKey,
            Expression<Func<TSub, bool>> innerFilter, bool negate) where TSub : class
        {
            if (subTable == null) throw new ArgumentNullException(nameof(subTable));
            if (outerKey == null) throw new ArgumentNullException(nameof(outerKey));
            if (innerKey == null) throw new ArgumentNullException(nameof(innerKey));

            var outerMapping = MappingCache.GetMapping<T>();
            var innerMapping = MappingCache.GetMapping<TSub>();
            var alias = $"sq{_subqueryAliasSeq++}";
            var builder = new SubqueryBuilder(outerMapping, innerMapping, _context.Dialect, alias, _subqueryParamSeed);
            var fragment = builder.BuildIn(outerKey, innerKey, innerFilter, negate);
            _subqueryParamSeed = builder.NextParamIndex;
            AddRawWhere(fragment);
        }

        private void AddRawWhere((string Sql, IDictionary<string, object> Parameters) fragment)
        {
            if (_rawWhereFragments == null)
                _rawWhereFragments = new List<(string, IDictionary<string, object>)>();
            _rawWhereFragments.Add(fragment);
        }

        #endregion

        #region Include (selective FK loading)

        /// <summary>
        /// Starts a selective FK loading query.
        /// Only the specified navigation properties will be loaded (not all FKs).
        /// Chain multiple Include() calls for multiple FKs.
        /// Example: db.Orders.Include(o => o.Customer).Include(o => o.Product).Where(...)
        /// </summary>
        public IncludeQuery<T> Include(Expression<Func<T, object>> expression)
        {
            var query = new IncludeQuery<T>(this, _context, _changeTracker);
            return query.Include(expression);
        }

        #endregion

        #region Sync Query Methods

        /// <summary>
        /// Finds an entity by primary key value(s).
        /// </summary>
        public T Find(params object[] keyValues)
        {
            return FindInternal(keyValues);
        }

        /// <summary>
        /// Server-side WHERE using LINQ expression predicate.
        /// Combines any predicates previously accumulated via WhereIf/AndWhere.
        /// </summary>
        public List<T> Where(Expression<Func<T, bool>> predicate)
        {
            return ExecuteWhere(CombinePending(predicate));
        }

        /// <summary>
        /// Accumulates a predicate (combined with AND) without executing the query.
        /// Chain with a terminal operation such as ToList()/FirstOrDefault().
        /// Example: db.Orders.AndWhere(o =&gt; o.Active).AndWhere(o =&gt; o.Amount &gt; 100).ToList()
        /// </summary>
        public Table<T> AndWhere(Expression<Func<T, bool>> predicate)
        {
            AddPendingPredicate(predicate);
            return this;
        }

        /// <summary>
        /// Returns first matching entity or null. Uses TOP/LIMIT 1.
        /// </summary>
        public T FirstOrDefault(Expression<Func<T, bool>> predicate)
        {
            if (_take == null) _take = 1;
            return ExecuteWhere(CombinePending(predicate)).FirstOrDefault();
        }

        /// <summary>
        /// Returns the only matching entity. Throws if zero or more than one match.
        /// </summary>
        public T Single(Expression<Func<T, bool>> predicate)
        {
            return ExecuteWhere(CombinePending(predicate)).Single();
        }

        /// <summary>
        /// Returns the only matching entity, or default if none. Throws if more than one match.
        /// </summary>
        public T SingleOrDefault(Expression<Func<T, bool>> predicate)
        {
            return ExecuteWhere(CombinePending(predicate)).SingleOrDefault();
        }

        /// <summary>
        /// Returns first matching entity. Throws if no match.
        /// </summary>
        public T First(Expression<Func<T, bool>> predicate)
        {
            if (_take == null) _take = 1;
            return ExecuteWhere(CombinePending(predicate)).First();
        }

        /// <summary>
        /// Executes the current query and returns all matching rows as a List.
        /// Respects OrderBy/Skip/Take and any accumulated WhereIf/AndWhere predicates.
        /// </summary>
        public List<T> ToList()
        {
            if (_cacheDuration.HasValue
                || (_pendingPredicates != null && _pendingPredicates.Count > 0)
                || (_rawWhereFragments != null && _rawWhereFragments.Count > 0))
                return ExecuteWhere(CombinePending(null));
            if (_orderByClauses != null || _skip.HasValue || _take.HasValue)
                return ExecuteWhere(null);
            return GetAll();
        }

        /// <summary>
        /// Executes the current query and ingests the results directly into a zero-allocation columnar DataFrame without instantiating POCO entities.
        /// Respects Where, OrderBy, Skip, Take, and query filters.
        /// </summary>
        public DataFrame ToDataFrame(int maxRows = -1)
        {
            var mapping = MappingCache.GetMapping<T>();
            var (sql, dp) = BuildWhereSql(mapping, CombinePending(null), _orderByClauses, _skip, _take, _rawWhereFragments);
            return _context.QueryDataFrame(sql, dp, maxRows);
        }

        /// <summary>
        /// Asynchronously executes the current query and ingests the results directly into a zero-allocation columnar DataFrame without instantiating POCO entities.
        /// Respects Where, OrderBy, Skip, Take, and query filters.
        /// </summary>
        public Task<DataFrame> ToDataFrameAsync(int maxRows = -1, CancellationToken ct = default)
        {
            var mapping = MappingCache.GetMapping<T>();
            var (sql, dp) = BuildWhereSql(mapping, CombinePending(null), _orderByClauses, _skip, _take, _rawWhereFragments);
            return _context.QueryDataFrameAsync(sql, dp, maxRows, ct);
        }

        /// <summary>
        /// Server-side COUNT matching predicate. Respects global query filters.
        /// </summary>
        public int Count(Expression<Func<T, bool>> predicate)
        {
            var mapping = MappingCache.GetMapping<T>();
            var tableName = SqlGenerator.QuoteTableName(mapping.TableName, _context.Dialect);
            var whereParts = new List<string>();
            IDictionary<string, object> allParams = new Dictionary<string, object>();

            // Inject global filters
            if (!_ignoreFilters && _context.Filters.HasFilters)
            {
                var filters = _context.Filters.GetFilters(typeof(T));
                if (filters.Count > 0)
                {
                    var filterBuilder = new WhereBuilder(mapping, _context.Dialect);
                    foreach (var filter in filters)
                    {
                        var (fSql, fParams) = filterBuilder.BuildFromLambda(filter);
                        whereParts.Add(fSql);
                        if (fParams != null)
                            foreach (var kv in fParams) allParams[kv.Key] = kv.Value;
                    }
                }
            }

            var builder = new WhereBuilder(mapping, _context.Dialect);
            var (whereSql, parameters) = builder.Build(predicate);
            whereParts.Add("(" + whereSql + ")");
            if (parameters != null)
                foreach (var kv in parameters) allParams[kv.Key] = kv.Value;

            var sql = $"SELECT COUNT(*) FROM {tableName} WHERE {string.Join(" AND ", whereParts)}";
            _context.EnsureConnectionOpen();
            return _context.Connection.ExecuteScalar<int>(sql, ToDp(allParams),
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout);
        }

        /// <summary>
        /// Server-side existence check.
        /// </summary>
        public bool Any(Expression<Func<T, bool>> predicate) => Count(predicate) > 0;

        /// <summary>
        /// Server-side MAX aggregate.
        /// </summary>
        public TResult Max<TResult>(Expression<Func<T, TResult>> selector)
            => ExecuteAggregate<TResult>("MAX", selector, null);

        /// <summary>
        /// Server-side MAX with WHERE.
        /// </summary>
        public TResult Max<TResult>(Expression<Func<T, TResult>> selector, Expression<Func<T, bool>> predicate)
            => ExecuteAggregate<TResult>("MAX", selector, predicate);

        /// <summary>
        /// Server-side MIN aggregate.
        /// </summary>
        public TResult Min<TResult>(Expression<Func<T, TResult>> selector)
            => ExecuteAggregate<TResult>("MIN", selector, null);

        /// <summary>
        /// Server-side MIN with WHERE.
        /// </summary>
        public TResult Min<TResult>(Expression<Func<T, TResult>> selector, Expression<Func<T, bool>> predicate)
            => ExecuteAggregate<TResult>("MIN", selector, predicate);

        /// <summary>
        /// Server-side SUM aggregate.
        /// </summary>
        public TResult Sum<TResult>(Expression<Func<T, TResult>> selector)
            => ExecuteAggregate<TResult>("SUM", selector, null);

        /// <summary>
        /// Server-side SUM with WHERE.
        /// </summary>
        public TResult Sum<TResult>(Expression<Func<T, TResult>> selector, Expression<Func<T, bool>> predicate)
            => ExecuteAggregate<TResult>("SUM", selector, predicate);

        /// <summary>
        /// Server-side AVERAGE aggregate.
        /// </summary>
        public TResult Average<TResult>(Expression<Func<T, TResult>> selector)
            => ExecuteAggregate<TResult>("AVG", selector, null);

        /// <summary>
        /// Server-side DISTINCT.
        /// </summary>
        public List<T> Distinct()
        {
            var mapping = MappingCache.GetMapping<T>();
            var sql = SqlGenerator.GenerateSelectAll(mapping, _context.Dialect).Replace("SELECT ", "SELECT DISTINCT ");
            _context.EnsureConnectionOpen();
            return _context.Connection.Query<T>(sql,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout, converters: _context.Converters).ToList();
        }

        /// <summary>
        /// Server-side pagination. Returns a PagedResult with Items, TotalCount, TotalPages.
        /// Executes 2 queries: COUNT + paginated SELECT.
        /// Requires OrderBy to be set (for deterministic paging).
        /// </summary>
        public PagedResult<T> ToPagedResult(int page, int pageSize,
            Expression<Func<T, bool>> predicate = null)
        {
            if (page < 1) throw new ArgumentOutOfRangeException(nameof(page), "Page must be >= 1");
            if (pageSize < 1) throw new ArgumentOutOfRangeException(nameof(pageSize), "PageSize must be >= 1");

            var totalCount = Count(predicate ?? (x => true));

            _skip = (page - 1) * pageSize;
            _take = pageSize;
            var items = predicate != null ? ExecuteWhere(predicate) : ExecuteWhere(null);

            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

            return new PagedResult<T>
            {
                Items = items,
                TotalCount = totalCount,
                TotalPages = totalPages,
                CurrentPage = page,
                PageSize = pageSize
            };
        }

        /// <summary>
        /// Async server-side pagination.
        /// </summary>
        public async Task<PagedResult<T>> ToPagedResultAsync(int page, int pageSize,
            Expression<Func<T, bool>> predicate = null, CancellationToken ct = default)
        {
            if (page < 1) throw new ArgumentOutOfRangeException(nameof(page), "Page must be >= 1");
            if (pageSize < 1) throw new ArgumentOutOfRangeException(nameof(pageSize), "PageSize must be >= 1");

            var totalCount = await CountAsync(predicate ?? (x => true), ct).ConfigureAwait(false);

            _skip = (page - 1) * pageSize;
            _take = pageSize;
            var items = predicate != null
                ? await ExecuteWhereAsync(predicate, ct: ct).ConfigureAwait(false)
                : await ExecuteWhereAsync(null, ct: ct).ConfigureAwait(false);

            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

            return new PagedResult<T>
            {
                Items = items,
                TotalCount = totalCount,
                TotalPages = totalPages,
                CurrentPage = page,
                PageSize = pageSize
            };
        }

        public IQueryable<T> AsQueryable() => GetAll().AsQueryable();

        /// <summary>
        /// Disables global query filters for this query.
        /// Allows bypassing soft-delete or multi-tenant filters.
        /// </summary>
        public Table<T> IgnoreFilters()
        {
            _ignoreFilters = true;
            return this;
        }

        /// <summary>
        /// Adds a comment tag to the generated SQL for debugging & profiling.
        /// The tag appears as /* tag */ before the SQL statement.
        /// </summary>
        public Table<T> TagWith(string tag)
        {
            _queryTag = tag;
            return this;
        }

        /// <summary>
        /// Server-side DELETE without loading entities.
        /// Deletes all rows matching the predicate in a single SQL statement.
        /// Respects SoftDeleteAttribute unless hard delete is required.
        /// </summary>
        public int BatchDelete(Expression<Func<T, bool>> predicate) => DeleteWhere(predicate);

        /// <summary>
        /// Async server-side DELETE without loading entities.
        /// </summary>
        public Task<int> BatchDeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
            => DeleteWhereAsync(predicate, forceHardDelete: false, ct: ct);

        /// <summary>
        /// Server-side UPDATE without loading entities.
        /// Sets columns to the values specified in the setter expression.
        /// Example: table.BatchUpdate(x => x.Status == "Active", x => new Product { Price = 100 })
        /// </summary>
        public int BatchUpdate(Expression<Func<T, bool>> predicate,
            Expression<Func<T, T>> setter)
        {
            var mapping = MappingCache.GetMapping<T>();
            var tableName = SqlGenerator.QuoteTableName(mapping.TableName, _context.Dialect);
            var builder = new WhereBuilder(mapping, _context.Dialect);
            var (whereSql, whereParams) = builder.Build(predicate);
            var (setClauses, setParams) = BuildSetClauses(setter, mapping);

            var sql = $"UPDATE {tableName} SET {setClauses} WHERE {whereSql}";
            var allParams = whereParams ?? new Dictionary<string, object>();
            if (setParams != null)
                foreach (var kv in setParams)
                    allParams[kv.Key] = kv.Value;
            _context.EnsureConnectionOpen();
            return _context.Connection.Execute(sql, (object)ToDp(allParams),
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout);
        }

        /// <summary>
        /// Async server-side UPDATE without loading entities.
        /// </summary>
        public async Task<int> BatchUpdateAsync(Expression<Func<T, bool>> predicate,
            Expression<Func<T, T>> setter, CancellationToken ct = default)
        {
            var mapping = MappingCache.GetMapping<T>();
            var tableName = SqlGenerator.QuoteTableName(mapping.TableName, _context.Dialect);
            var builder = new WhereBuilder(mapping, _context.Dialect);
            var (whereSql, whereParams) = builder.Build(predicate);
            var (setClauses, setParams) = BuildSetClauses(setter, mapping);

            var sql = $"UPDATE {tableName} SET {setClauses} WHERE {whereSql}";
            var allParams = whereParams ?? new Dictionary<string, object>();
            if (setParams != null)
                foreach (var kv in setParams)
                    allParams[kv.Key] = kv.Value;
            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            return await _context.Connection.ExecuteAsync(
                new CommandDefinition(sql, ToDp(allParams),
                    transaction: _context.Transaction,
                    commandTimeout: _context.CommandTimeout,
                    cancellationToken: ct)).ConfigureAwait(false);
        }

        /// <summary>
        /// Extracts SET clauses from a MemberInit expression like x => new T { Prop = value }.
        /// </summary>
        private (string setClauses, IDictionary<string, object> parameters) BuildSetClauses(
            Expression<Func<T, T>> setter, EntityMapping mapping)
        {
            if (!(setter.Body is MemberInitExpression init))
                throw new ArgumentException("Setter must be a MemberInitExpression (x => new T { ... })");

            var clauses = new List<string>();
            var parameters = new Dictionary<string, object>();
            int idx = 0;

            foreach (var binding in init.Bindings)
            {
                if (binding is MemberAssignment assign)
                {
                    var propName = assign.Member.Name;
                    var col = mapping.Columns.FirstOrDefault(c => c.Property.Name == propName);
                    if (col == null) continue;

                    var value = Expression.Lambda(assign.Expression).Compile().DynamicInvoke();
                    var paramName = $"@s{idx++}";
                    clauses.Add($"{_context.Dialect.QuoteIdentifier(col.ColumnName)} = {paramName}");
                    parameters[paramName] = value;
                }
            }

            return (string.Join(", ", clauses), parameters);
        }

        /// <summary>
        /// Server-side SELECT projection. Translates expression into SQL column list.
        /// Supports anonymous types, DTOs, and scalar properties.
        /// </summary>
        public List<TResult> Select<TResult>(Expression<Func<T, TResult>> selector)
        {
            return ExecuteSelect(selector, null);
        }

        /// <summary>
        /// Server-side SELECT projection with WHERE clause.
        /// </summary>
        public List<TResult> Select<TResult>(Expression<Func<T, TResult>> selector,
            Expression<Func<T, bool>> predicate)
        {
            return ExecuteSelect(selector, predicate);
        }

        #endregion

        #region Async Query Methods

        /// <summary>
        /// Async Find by primary key.
        /// </summary>
        public async Task<T> FindAsync(params object[] keyValues)
        {
            return await FindInternalAsync(keyValues, default).ConfigureAwait(false);
        }

        /// <summary>
        /// Async Find by primary key with CancellationToken.
        /// </summary>
        public async Task<T> FindAsync(CancellationToken ct, params object[] keyValues)
        {
            return await FindInternalAsync(keyValues, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Async server-side WHERE. Combines any accumulated WhereIf/AndWhere predicates.
        /// </summary>
        public async Task<List<T>> WhereAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        {
            return await ExecuteWhereAsync(CombinePending(predicate), ct: ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Async FirstOrDefault with TOP/LIMIT 1.
        /// </summary>
        public async Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        {
            if (_take == null) _take = 1;
            var results = await ExecuteWhereAsync(CombinePending(predicate), ct: ct).ConfigureAwait(false);
            return results.FirstOrDefault();
        }

        /// <summary>
        /// Async FirstOrDefault without predicate. Returns first row or null.
        /// </summary>
        public async Task<T> FirstOrDefaultAsync(CancellationToken ct = default)
        {
            if (_take == null) _take = 1;
            var results = await ExecuteWhereAsync(CombinePending(null), ct: ct).ConfigureAwait(false);
            return results.FirstOrDefault();
        }

        /// <summary>
        /// Async Single. Returns the only matching entity. Throws if zero or more than one.
        /// </summary>
        public async Task<T> SingleAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        {
            var results = await ExecuteWhereAsync(CombinePending(predicate), ct: ct).ConfigureAwait(false);
            return results.Single();
        }

        /// <summary>
        /// Async SingleOrDefault. Returns the only match or default. Throws if more than one.
        /// </summary>
        public async Task<T> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        {
            var results = await ExecuteWhereAsync(CombinePending(predicate), ct: ct).ConfigureAwait(false);
            return results.SingleOrDefault();
        }

        /// <summary>
        /// Async First. Returns first matching entity. Throws if no match.
        /// </summary>
        public async Task<T> FirstAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        {
            if (_take == null) _take = 1;
            var results = await ExecuteWhereAsync(CombinePending(predicate), ct: ct).ConfigureAwait(false);
            return results.First();
        }

        /// <summary>
        /// Async server-side COUNT with predicate. Respects global query filters.
        /// </summary>
        public async Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        {
            var mapping = MappingCache.GetMapping<T>();
            var tableName = SqlGenerator.QuoteTableName(mapping.TableName, _context.Dialect);
            var whereParts = new List<string>();
            IDictionary<string, object> allParams = new Dictionary<string, object>();

            // Inject global filters
            if (!_ignoreFilters && _context.Filters.HasFilters)
            {
                var filters = _context.Filters.GetFilters(typeof(T));
                if (filters.Count > 0)
                {
                    var filterBuilder = new WhereBuilder(mapping, _context.Dialect);
                    foreach (var filter in filters)
                    {
                        var (fSql, fParams) = filterBuilder.BuildFromLambda(filter);
                        whereParts.Add(fSql);
                        if (fParams != null)
                            foreach (var kv in fParams) allParams[kv.Key] = kv.Value;
                    }
                }
            }

            var builder = new WhereBuilder(mapping, _context.Dialect);
            var (whereSql, parameters) = builder.Build(predicate);
            whereParts.Add("(" + whereSql + ")");
            if (parameters != null)
                foreach (var kv in parameters) allParams[kv.Key] = kv.Value;

            var sql = $"SELECT COUNT(*) FROM {tableName} WHERE {string.Join(" AND ", whereParts)}";
            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            return await _context.Connection.ExecuteScalarAsync<int>(
                new CommandDefinition(sql, ToDp(allParams),
                    transaction: _context.Transaction, commandTimeout: _context.CommandTimeout,
                    cancellationToken: ct)).ConfigureAwait(false);
        }

        /// <summary>
        /// Async server-side COUNT without predicate. Returns total row count.
        /// Respects global query filters.
        /// </summary>
        public async Task<int> CountAsync(CancellationToken ct = default)
        {
            return await CountAsync(x => true, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Async server-side existence check.
        /// </summary>
        public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        {
            return await CountAsync(predicate, ct).ConfigureAwait(false) > 0;
        }

        /// <summary>
        /// Async server-side existence check without predicate.
        /// </summary>
        public async Task<bool> AnyAsync(CancellationToken ct = default)
        {
            return await CountAsync(ct).ConfigureAwait(false) > 0;
        }

        /// <summary>
        /// Async MAX aggregate.
        /// </summary>
        public async Task<TResult> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default)
            => await ExecuteAggregateAsync<TResult>("MAX", selector, null, ct).ConfigureAwait(false);

        /// <summary>
        /// Async MIN aggregate.
        /// </summary>
        public async Task<TResult> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default)
            => await ExecuteAggregateAsync<TResult>("MIN", selector, null, ct).ConfigureAwait(false);

        /// <summary>
        /// Async SUM aggregate.
        /// </summary>
        public async Task<TResult> SumAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default)
            => await ExecuteAggregateAsync<TResult>("SUM", selector, null, ct).ConfigureAwait(false);

        /// <summary>
        /// Async DISTINCT.
        /// </summary>
        public async Task<List<T>> DistinctAsync(CancellationToken ct = default)
        {
            var mapping = MappingCache.GetMapping<T>();
            var sql = SqlGenerator.GenerateSelectAll(mapping, _context.Dialect).Replace("SELECT ", "SELECT DISTINCT ");
            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            return (await _context.Connection.QueryAsync<T>(
                new CommandDefinition(sql, transaction: _context.Transaction,
                    commandTimeout: _context.CommandTimeout, cancellationToken: ct),
                converters: _context.Converters).ConfigureAwait(false)).ToList();
        }

        /// <summary>
        /// Async load all rows. Respects OrderBy/Skip/Take if set.
        /// </summary>
        public async Task<List<T>> ToListAsync(CancellationToken ct = default)
        {
            if (_cacheDuration.HasValue
                || (_pendingPredicates != null && _pendingPredicates.Count > 0)
                || (_rawWhereFragments != null && _rawWhereFragments.Count > 0))
                return await ExecuteWhereAsync(CombinePending(null), ct: ct).ConfigureAwait(false);

            if (_orderByClauses != null || _skip.HasValue || _take.HasValue)
                return await ExecuteWhereAsync(null, ct: ct).ConfigureAwait(false);

            // If global filters are active, go through BuildWhereSql path
            if (!_ignoreFilters && _context.Filters.HasFilters
                && _context.Filters.GetFilters(typeof(T)).Count > 0)
            {
                return await ExecuteWhereAsync(null, ct: ct).ConfigureAwait(false);
            }

            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var mapping = MappingCache.GetMapping<T>();
            var sql = SqlGenerator.GenerateSelectAll(mapping, _context.Dialect);
            var results = (await _context.Connection.QueryAsync<T>(
                new CommandDefinition(sql, transaction: _context.Transaction,
                    commandTimeout: _context.CommandTimeout, cancellationToken: ct),
                converters: _context.Converters).ConfigureAwait(false)).ToList();
            TrackResults(results, mapping);
            await LoadAssociationsAsync(results, mapping, ct).ConfigureAwait(false);
            return results;
        }

        /// <summary>
        /// Async server-side SELECT projection.
        /// </summary>
        public async Task<List<TResult>> SelectAsync<TResult>(Expression<Func<T, TResult>> selector,
            CancellationToken ct = default)
        {
            return await ExecuteSelectAsync(selector, null, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Async server-side SELECT projection with WHERE clause.
        /// </summary>
        public async Task<List<TResult>> SelectAsync<TResult>(Expression<Func<T, TResult>> selector,
            Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        {
            return await ExecuteSelectAsync(selector, predicate, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously streams all matching rows one by one without buffering the entire table in memory.
        /// Ideal for large datasets (100k - 10M records) to maintain a flat memory footprint.
        /// </summary>
        public async IAsyncEnumerable<T> ToStreamAsync([EnumeratorCancellation] CancellationToken ct = default)
        {
            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var mapping = MappingCache.GetMapping<T>();
            var orderBy = _orderByClauses;
            var skip = _skip;
            var take = _take;
            var rawFragments = _rawWhereFragments;
            var predicate = CombinePending(null);
            ResetQueryState();

            var (fullSql, dp) = BuildWhereSql(mapping, predicate, orderBy, skip, take, rawFragments);

            await foreach (var item in _context.Connection.QueryStreamAsync<T>(fullSql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout, cancellationToken: ct, converters: _context.Converters).ConfigureAwait(false))
            {
                yield return item;
            }
        }

        /// <summary>
        /// Constant-time Keyset (Seek) pagination.
        /// Bypasses OFFSET/FETCH by seeking directly on an indexed key column (e.g. Id > lastSeenId).
        /// Stable, constant-time performance across deep pagination pages.
        /// </summary>
        public List<T> Seek<TKey>(
            Expression<Func<T, TKey>> keySelector,
            TKey lastSeenKey,
            int pageSize,
            bool ascending = true)
        {
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than zero.");

            var colName = ExtractColumnName(keySelector);
            var mapping = MappingCache.GetMapping<T>();
            var dialect = _context.Dialect;
            var quotedCol = dialect.QuoteIdentifier(colName);
            var op = ascending ? ">" : "<";
            var orderDir = ascending ? "ASC" : "DESC";
            var paramName = "@seekKey";

            var (fullSql, dp) = BuildWhereSql(mapping, CombinePending(null), null, null, null, _rawWhereFragments);
            ResetQueryState();

            if (dp == null) dp = new DynamicParameters();
            dp.Add(paramName, lastSeenKey);

            var seekCondition = $"{quotedCol} {op} {paramName}";
            var hasWhere = fullSql.IndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase) >= 0;
            var sqlWithSeek = hasWhere ? $"{fullSql} AND ({seekCondition})" : $"{fullSql} WHERE {seekCondition}";

            var provider = dialect.ProviderName;
            var usesLimit = provider == "SQLite" || provider == "MySQL" || provider == "PostgreSQL";

            string querySql;
            if (usesLimit)
            {
                querySql = $"{sqlWithSeek} ORDER BY {quotedCol} {orderDir} LIMIT {pageSize}";
            }
            else
            {
                querySql = sqlWithSeek.Replace("SELECT ", $"SELECT TOP ({pageSize}) ") + $" ORDER BY {quotedCol} {orderDir}";
            }

            _context.EnsureConnectionOpen();
            var results = _context.Connection.Query<T>(querySql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout, converters: _context.Converters).ToList();

            TrackResults(results, mapping);
            LoadAssociations(results, mapping);
            return results;
        }

        /// <summary>
        /// Asynchronously executes constant-time Keyset (Seek) pagination.
        /// </summary>
        public async Task<List<T>> SeekAsync<TKey>(
            Expression<Func<T, TKey>> keySelector,
            TKey lastSeenKey,
            int pageSize,
            bool ascending = true,
            CancellationToken ct = default)
        {
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than zero.");

            var colName = ExtractColumnName(keySelector);
            var mapping = MappingCache.GetMapping<T>();
            var dialect = _context.Dialect;
            var quotedCol = dialect.QuoteIdentifier(colName);
            var op = ascending ? ">" : "<";
            var orderDir = ascending ? "ASC" : "DESC";
            var paramName = "@seekKey";

            var (fullSql, dp) = BuildWhereSql(mapping, CombinePending(null), null, null, null, _rawWhereFragments);
            ResetQueryState();

            if (dp == null) dp = new DynamicParameters();
            dp.Add(paramName, lastSeenKey);

            var seekCondition = $"{quotedCol} {op} {paramName}";
            var hasWhere = fullSql.IndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase) >= 0;
            var sqlWithSeek = hasWhere ? $"{fullSql} AND ({seekCondition})" : $"{fullSql} WHERE {seekCondition}";

            var provider = dialect.ProviderName;
            var usesLimit = provider == "SQLite" || provider == "MySQL" || provider == "PostgreSQL";

            string querySql;
            if (usesLimit)
            {
                querySql = $"{sqlWithSeek} ORDER BY {quotedCol} {orderDir} LIMIT {pageSize}";
            }
            else
            {
                querySql = sqlWithSeek.Replace("SELECT ", $"SELECT TOP ({pageSize}) ") + $" ORDER BY {quotedCol} {orderDir}";
            }

            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var results = (await _context.Connection.QueryAsync<T>(querySql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout, cancellationToken: ct, converters: _context.Converters).ConfigureAwait(false)).ToList();

            TrackResults(results, mapping);
            await LoadAssociationsAsync(results, mapping, ct).ConfigureAwait(false);
            return results;
        }

        /// <summary>
        /// Constant-time Composite Keyset (Seek) pagination across multiple indexed columns.
        /// Bypasses OFFSET/FETCH for ultra-fast deep pagination.
        /// </summary>
        public List<T> Seek(IReadOnlyList<SeekColumn> seekColumns, int pageSize)
        {
            if (seekColumns == null || seekColumns.Count == 0)
                throw new ArgumentException("At least one seek column must be specified.", nameof(seekColumns));
            if (pageSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than zero.");

            var (querySql, dp, mapping) = BuildCompositeSeekSql(seekColumns, pageSize);
            _context.EnsureConnectionOpen();
            var results = _context.Connection.Query<T>(querySql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout, converters: _context.Converters).ToList();

            TrackResults(results, mapping);
            LoadAssociations(results, mapping);
            return results;
        }

        /// <summary>
        /// Asynchronously executes constant-time Composite Keyset (Seek) pagination across multiple indexed columns.
        /// </summary>
        public async Task<List<T>> SeekAsync(IReadOnlyList<SeekColumn> seekColumns, int pageSize, CancellationToken ct = default)
        {
            if (seekColumns == null || seekColumns.Count == 0)
                throw new ArgumentException("At least one seek column must be specified.", nameof(seekColumns));
            if (pageSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than zero.");

            var (querySql, dp, mapping) = BuildCompositeSeekSql(seekColumns, pageSize);
            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var results = (await _context.Connection.QueryAsync<T>(querySql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout, cancellationToken: ct, converters: _context.Converters).ConfigureAwait(false)).ToList();

            TrackResults(results, mapping);
            await LoadAssociationsAsync(results, mapping, ct).ConfigureAwait(false);
            return results;
        }

        /// <summary>
        /// Two-column composite keyset (seek) pagination.
        /// </summary>
        public List<T> Seek<TKey1, TKey2>(
            Expression<Func<T, TKey1>> key1, TKey1 lastSeen1, bool asc1,
            Expression<Func<T, TKey2>> key2, TKey2 lastSeen2, bool asc2,
            int pageSize)
            => Seek(new[] { SeekColumn.Create(key1, lastSeen1, asc1), SeekColumn.Create(key2, lastSeen2, asc2) }, pageSize);

        /// <summary>
        /// Asynchronously executes two-column composite keyset (seek) pagination.
        /// </summary>
        public Task<List<T>> SeekAsync<TKey1, TKey2>(
            Expression<Func<T, TKey1>> key1, TKey1 lastSeen1, bool asc1,
            Expression<Func<T, TKey2>> key2, TKey2 lastSeen2, bool asc2,
            int pageSize, CancellationToken ct = default)
            => SeekAsync(new[] { SeekColumn.Create(key1, lastSeen1, asc1), SeekColumn.Create(key2, lastSeen2, asc2) }, pageSize, ct);

        /// <summary>
        /// Three-column composite keyset (seek) pagination.
        /// </summary>
        public List<T> Seek<TKey1, TKey2, TKey3>(
            Expression<Func<T, TKey1>> key1, TKey1 lastSeen1, bool asc1,
            Expression<Func<T, TKey2>> key2, TKey2 lastSeen2, bool asc2,
            Expression<Func<T, TKey3>> key3, TKey3 lastSeen3, bool asc3,
            int pageSize)
            => Seek(new[] { SeekColumn.Create(key1, lastSeen1, asc1), SeekColumn.Create(key2, lastSeen2, asc2), SeekColumn.Create(key3, lastSeen3, asc3) }, pageSize);

        /// <summary>
        /// Asynchronously executes three-column composite keyset (seek) pagination.
        /// </summary>
        public Task<List<T>> SeekAsync<TKey1, TKey2, TKey3>(
            Expression<Func<T, TKey1>> key1, TKey1 lastSeen1, bool asc1,
            Expression<Func<T, TKey2>> key2, TKey2 lastSeen2, bool asc2,
            Expression<Func<T, TKey3>> key3, TKey3 lastSeen3, bool asc3,
            int pageSize, CancellationToken ct = default)
            => SeekAsync(new[] { SeekColumn.Create(key1, lastSeen1, asc1), SeekColumn.Create(key2, lastSeen2, asc2), SeekColumn.Create(key3, lastSeen3, asc3) }, pageSize, ct);

        private (string QuerySql, DynamicParameters Parameters, EntityMapping Mapping) BuildCompositeSeekSql(
            IReadOnlyList<SeekColumn> seekColumns, int pageSize)
        {
            var mapping = MappingCache.GetMapping<T>();
            var dialect = _context.Dialect;

            var (fullSql, dp) = BuildWhereSql(mapping, CombinePending(null), null, null, null, _rawWhereFragments);
            ResetQueryState();

            if (dp == null) dp = new DynamicParameters();

            var orClauses = new List<string>();
            for (int i = 0; i < seekColumns.Count; i++)
            {
                var andParts = new List<string>();
                for (int j = 0; j < i; j++)
                {
                    var prevCol = seekColumns[j];
                    var prevQuoted = dialect.QuoteIdentifier(prevCol.ColumnName);
                    var prevParam = $"@cseek_eq_{j}_{i}";
                    dp.Add(prevParam, prevCol.LastSeenValue);
                    andParts.Add($"{prevQuoted} = {prevParam}");
                }

                var curCol = seekColumns[i];
                var curQuoted = dialect.QuoteIdentifier(curCol.ColumnName);
                var curOp = curCol.Ascending ? ">" : "<";
                var curParam = $"@cseek_op_{i}";
                dp.Add(curParam, curCol.LastSeenValue);
                andParts.Add($"{curQuoted} {curOp} {curParam}");

                orClauses.Add($"({string.Join(" AND ", andParts)})");
            }

            var seekCondition = string.Join(" OR ", orClauses);
            var hasWhere = fullSql.IndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase) >= 0;
            var sqlWithSeek = hasWhere ? $"{fullSql} AND ({seekCondition})" : $"{fullSql} WHERE {seekCondition}";

            var orderClauses = seekColumns.Select(c => $"{dialect.QuoteIdentifier(c.ColumnName)} {(c.Ascending ? "ASC" : "DESC")}").ToList();
            var orderBySql = string.Join(", ", orderClauses);

            var provider = dialect.ProviderName;
            var usesLimit = provider == "SQLite" || provider == "MySQL" || provider == "PostgreSQL";

            string querySql;
            if (usesLimit)
            {
                querySql = $"{sqlWithSeek} ORDER BY {orderBySql} LIMIT {pageSize}";
            }
            else
            {
                querySql = $"{sqlWithSeek} ORDER BY {orderBySql} OFFSET 0 ROWS FETCH NEXT {pageSize} ROWS ONLY";
            }

            return (querySql, dp, mapping);
        }

        #endregion

        #region IEnumerable

        public IEnumerator<T> GetEnumerator() => GetAll().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        #region Private: Find by PK

        private T FindInternal(object[] keyValues)
        {
            var mapping = MappingCache.GetMapping<T>();
            var pks = mapping.PrimaryKeys.ToList();
            ValidateKeyValues(pks, keyValues);

            var (sql, dp) = BuildFindSql(mapping, pks, keyValues);
            _context.EnsureConnectionOpen();
            var entity = _context.Connection.QueryFirstOrDefault<T>(sql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout, converters: _context.Converters);
            if (entity != null)
            {
                TrackSingle(entity, mapping);
                LoadAssociations(new List<T> { entity }, mapping);
            }
            return entity;
        }

        private async Task<T> FindInternalAsync(object[] keyValues, CancellationToken ct = default)
        {
            var mapping = MappingCache.GetMapping<T>();
            var pks = mapping.PrimaryKeys.ToList();
            ValidateKeyValues(pks, keyValues);

            var (sql, dp) = BuildFindSql(mapping, pks, keyValues);
            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var entity = await _context.Connection.QueryFirstOrDefaultAsync<T>(
                new CommandDefinition(sql, dp, transaction: _context.Transaction,
                    commandTimeout: _context.CommandTimeout, cancellationToken: ct),
                converters: _context.Converters).ConfigureAwait(false);
            if (entity != null)
            {
                TrackSingle(entity, mapping);
                await LoadAssociationsAsync(new List<T> { entity }, mapping, ct).ConfigureAwait(false);
            }
            return entity;
        }

        private (string sql, DynamicParameters dp) BuildFindSql(
            EntityMapping mapping, List<ColumnMapping> pks, object[] keyValues)
        {
            var dialect = _context.Dialect;
            var conditions = new List<string>();
            var dp = new DynamicParameters();
            for (int i = 0; i < pks.Count; i++)
            {
                var paramName = $"@pk{i}";
                conditions.Add($"{dialect.QuoteIdentifier(pks[i].ColumnName)} = {paramName}");
                dp.Add(paramName, keyValues[i]);
            }
            var sql = $"{SqlGenerator.GenerateSelectAll(mapping, _context.Dialect)} WHERE {string.Join(" AND ", conditions)}";
            return (sql, dp);
        }

        private static void ValidateKeyValues(List<ColumnMapping> pks, object[] keyValues)
        {
            if (keyValues == null || keyValues.Length != pks.Count)
                throw new ArgumentException(
                    $"Expected {pks.Count} key value(s) for type {typeof(T).Name}, got {keyValues?.Length ?? 0}.");
        }

        #endregion

        #region Private: Where Execution

        private List<T> ExecuteWhere(Expression<Func<T, bool>> predicate)
        {
            var mapping = MappingCache.GetMapping<T>();
            var orderBy = _orderByClauses;
            var skip = _skip;
            var take = _take;
            var rawFragments = _rawWhereFragments;
            var cacheDuration = _cacheDuration;
            ResetQueryState();

            var (fullSql, dp) = BuildWhereSql(mapping, predicate, orderBy, skip, take, rawFragments);

            if (cacheDuration.HasValue && _context.QueryCache != null)
            {
                var cacheKey = BuildCacheKey(fullSql, dp);
                if (_context.QueryCache.TryGet<List<T>>(cacheKey, out var cachedResults))
                {
                    return cachedResults;
                }

                _context.EnsureConnectionOpen();
                var results = _context.Connection.Query<T>(fullSql, dp,
                    transaction: _context.Transaction, commandTimeout: _context.CommandTimeout, converters: _context.Converters).ToList();
                TrackResults(results, mapping);
                LoadAssociations(results, mapping);
                _context.QueryCache.Set(cacheKey, results, cacheDuration.Value, new[] { typeof(T) });
                return results;
            }

            _context.EnsureConnectionOpen();
            var resultsDirect = _context.Connection.Query<T>(fullSql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout, converters: _context.Converters).ToList();
            TrackResults(resultsDirect, mapping);
            LoadAssociations(resultsDirect, mapping);
            return resultsDirect;
        }

        private async Task<List<T>> ExecuteWhereAsync(Expression<Func<T, bool>> predicate,
            CancellationToken ct = default)
        {
            var mapping = MappingCache.GetMapping<T>();
            var orderBy = _orderByClauses;
            var skip = _skip;
            var take = _take;
            var rawFragments = _rawWhereFragments;
            var cacheDuration = _cacheDuration;
            ResetQueryState();

            var (fullSql, dp) = BuildWhereSql(mapping, predicate, orderBy, skip, take, rawFragments);

            if (cacheDuration.HasValue && _context.QueryCache != null)
            {
                var cacheKey = BuildCacheKey(fullSql, dp);
                if (_context.QueryCache.TryGet<List<T>>(cacheKey, out var cachedResults))
                {
                    return cachedResults;
                }

                await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
                var results = (await _context.Connection.QueryAsync<T>(
                    new CommandDefinition(fullSql, dp, transaction: _context.Transaction,
                        commandTimeout: _context.CommandTimeout, cancellationToken: ct),
                    converters: _context.Converters).ConfigureAwait(false)).ToList();
                TrackResults(results, mapping);
                await LoadAssociationsAsync(results, mapping, ct).ConfigureAwait(false);
                _context.QueryCache.Set(cacheKey, results, cacheDuration.Value, new[] { typeof(T) });
                return results;
            }

            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var resultsDirect = (await _context.Connection.QueryAsync<T>(
                new CommandDefinition(fullSql, dp, transaction: _context.Transaction,
                    commandTimeout: _context.CommandTimeout, cancellationToken: ct),
                converters: _context.Converters).ConfigureAwait(false)).ToList();
            TrackResults(resultsDirect, mapping);
            await LoadAssociationsAsync(resultsDirect, mapping, ct).ConfigureAwait(false);
            return resultsDirect;
        }

        private static string BuildCacheKey(string sql, DynamicParameters dp)
        {
            if (dp == null || dp.Count == 0) return sql;
            var sb = new System.Text.StringBuilder(sql);
            sb.Append(" # params:[");
            foreach (var kv in dp.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                sb.Append(kv.Key).Append('=').Append(kv.Value?.ToString() ?? "NULL").Append(';');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private (string sql, DynamicParameters dp) BuildWhereSql(
            EntityMapping mapping, Expression<Func<T, bool>> predicate,
            List<string> orderByClauses, int? skip, int? take,
            List<(string Sql, IDictionary<string, object> Parameters)> rawFragments = null)
        {
            var selectAll = SqlGenerator.GenerateSelectAll(mapping, _context.Dialect);
            IDictionary<string, object> parameters = null;

            // Combine global filters with user predicate
            var filterClauses = new List<string>();
            IDictionary<string, object> filterParams = null;

            if (!_ignoreFilters && _context.Filters.HasFilters)
            {
                var filters = _context.Filters.GetFilters(typeof(T));
                if (filters.Count > 0)
                {
                    var filterBuilder = new WhereBuilder(mapping, _context.Dialect);
                    var allFilterParams = new Dictionary<string, object>();
                    foreach (var filter in filters)
                    {
                        var (fSql, fParams) = filterBuilder.BuildFromLambda(filter);
                        filterClauses.Add(fSql);
                        if (fParams != null)
                        {
                            foreach (var kv in fParams)
                                allFilterParams[kv.Key] = kv.Value;
                        }
                    }
                    if (allFilterParams.Count > 0)
                        filterParams = allFilterParams;
                }
            }

            string fullSql;
            if (predicate != null)
            {
                var builder = new WhereBuilder(mapping, _context.Dialect);
                var (whereSql, whereParams) = builder.Build(predicate);
                parameters = whereParams;

                if (filterClauses.Count > 0)
                {
                    // Combine: (global filters) AND (user where)
                    var combined = string.Join(" AND ", filterClauses) + " AND (" + whereSql + ")";
                    fullSql = $"{selectAll} WHERE {combined}";
                    MergeParams(ref parameters, filterParams);
                }
                else
                {
                    fullSql = $"{selectAll} WHERE {whereSql}";
                }
            }
            else
            {
                if (filterClauses.Count > 0)
                {
                    fullSql = $"{selectAll} WHERE {string.Join(" AND ", filterClauses)}";
                    parameters = filterParams;
                }
                else
                {
                    fullSql = selectAll;
                }
            }

            // Append accumulated raw WHERE fragments (EXISTS / IN subqueries).
            if (rawFragments != null && rawFragments.Count > 0)
            {
                var hasWhere = fullSql.IndexOf(" WHERE ", StringComparison.Ordinal) >= 0;
                foreach (var frag in rawFragments)
                {
                    fullSql += hasWhere ? $" AND ({frag.Sql})" : $" WHERE ({frag.Sql})";
                    hasWhere = true;
                    if (frag.Parameters != null)
                        MergeParams(ref parameters, frag.Parameters);
                }
            }

            // ORDER BY
            if (orderByClauses != null && orderByClauses.Count > 0)
            {
                fullSql += $" ORDER BY {string.Join(", ", orderByClauses)}";
            }

            // Pagination (dialect-aware)
            fullSql = ApplyPagination(fullSql, orderByClauses, skip, take);

            // Query tag for debugging
            if (_queryTag != null)
                fullSql = $"/* {_queryTag} */ {fullSql}";

            return (fullSql, ToDp(parameters));
        }

        /// <summary>
        /// Appends pagination syntax appropriate for the current dialect.
        /// SQLite/MySQL/PostgreSQL use LIMIT/OFFSET; SQL Server uses OFFSET/FETCH (or TOP).
        /// </summary>
        private string ApplyPagination(string sql, List<string> orderByClauses, int? skip, int? take)
        {
            var provider = _context.Dialect.ProviderName;
            var usesLimit = provider == "SQLite" || provider == "MySQL" || provider == "PostgreSQL";

            if (skip.HasValue || take.HasValue)
            {
                if (usesLimit)
                {
                    if (take.HasValue)
                        sql += $" LIMIT {take.Value}";
                    else
                        sql += " LIMIT -1"; // unlimited (SQLite); MySQL/PG tolerate large LIMIT
                    if (skip.HasValue)
                        sql += $" OFFSET {skip.Value}";
                }
                else
                {
                    // SQL Server: OFFSET ... ROWS FETCH NEXT ... ROWS ONLY (requires ORDER BY)
                    if (orderByClauses == null || orderByClauses.Count == 0)
                        sql += " ORDER BY (SELECT NULL)";
                    sql += $" OFFSET {skip ?? 0} ROWS";
                    if (take.HasValue)
                        sql += $" FETCH NEXT {take.Value} ROWS ONLY";
                }
            }
            else if (take.HasValue && (orderByClauses == null || orderByClauses.Count == 0))
            {
                // Legacy TOP/LIMIT behavior for FirstOrDefault without OrderBy
                if (usesLimit)
                    sql += $" LIMIT {take.Value}";
                else
                    sql = sql.Replace("SELECT ", $"SELECT TOP {take.Value} ");
            }

            return sql;
        }

        #endregion

        #region Private: Select Projection Execution

        private List<TResult> ExecuteSelect<TResult>(Expression<Func<T, TResult>> selector,
            Expression<Func<T, bool>> predicate)
        {
            var mapping = MappingCache.GetMapping<T>();
            var orderBy = _orderByClauses;
            var skip = _skip;
            var take = _take;
            ResetQueryState();

            var (fullSql, dp) = BuildSelectSql(mapping, selector, predicate, orderBy, skip, take);
            _context.EnsureConnectionOpen();
            return _context.Connection.Query<TResult>(fullSql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout, converters: _context.Converters).ToList();
        }

        private async Task<List<TResult>> ExecuteSelectAsync<TResult>(Expression<Func<T, TResult>> selector,
            Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        {
            var mapping = MappingCache.GetMapping<T>();
            var orderBy = _orderByClauses;
            var skip = _skip;
            var take = _take;
            ResetQueryState();

            var (fullSql, dp) = BuildSelectSql(mapping, selector, predicate, orderBy, skip, take);
            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var results = (await _context.Connection.QueryAsync<TResult>(
                new CommandDefinition(fullSql, dp, transaction: _context.Transaction,
                    commandTimeout: _context.CommandTimeout, cancellationToken: ct),
                converters: _context.Converters).ConfigureAwait(false)).ToList();
            return results;
        }

        private (string sql, DynamicParameters dp) BuildSelectSql<TResult>(
            EntityMapping mapping, Expression<Func<T, TResult>> selector,
            Expression<Func<T, bool>> predicate,
            List<string> orderByClauses, int? skip, int? take)
        {
            var selectBuilder = new SelectBuilder(mapping, _context.Dialect);
            var columnList = selectBuilder.Build(selector);
            var tableName = SqlGenerator.QuoteTableName(mapping.TableName, _context.Dialect);

            var whereParts = new List<string>();
            IDictionary<string, object> allParams = new Dictionary<string, object>();

            // Inject global filters
            if (!_ignoreFilters && _context.Filters.HasFilters)
            {
                var filters = _context.Filters.GetFilters(typeof(T));
                if (filters.Count > 0)
                {
                    var filterBuilder = new WhereBuilder(mapping, _context.Dialect);
                    foreach (var filter in filters)
                    {
                        var (fSql, fParams) = filterBuilder.BuildFromLambda(filter);
                        whereParts.Add(fSql);
                        if (fParams != null)
                            foreach (var kv in fParams) allParams[kv.Key] = kv.Value;
                    }
                }
            }

            if (predicate != null)
            {
                var builder = new WhereBuilder(mapping, _context.Dialect);
                var (whereSql, whereParams) = builder.Build(predicate);
                whereParts.Add("(" + whereSql + ")");
                if (whereParams != null)
                    foreach (var kv in whereParams) allParams[kv.Key] = kv.Value;
            }

            var fullSql = whereParts.Count > 0
                ? $"SELECT {columnList} FROM {tableName} WHERE {string.Join(" AND ", whereParts)}"
                : $"SELECT {columnList} FROM {tableName}";

            // ORDER BY
            if (orderByClauses != null && orderByClauses.Count > 0)
                fullSql += $" ORDER BY {string.Join(", ", orderByClauses)}";

            // Pagination (dialect-aware)
            fullSql = ApplyPagination(fullSql, orderByClauses, skip, take);

            return (fullSql, ToDp(allParams));
        }

        #endregion

        #region Private: Aggregate Execution

        private TResult ExecuteAggregate<TResult>(string function,
            LambdaExpression selector, Expression<Func<T, bool>> predicate)
        {
            var mapping = MappingCache.GetMapping<T>();
            var columnName = ExtractColumnNameFromLambda(selector, mapping);
            var tableName = SqlGenerator.QuoteTableName(mapping.TableName, _context.Dialect);

            var whereParts = new List<string>();
            IDictionary<string, object> parameters = null;

            // Inject global filters
            if (!_ignoreFilters && _context.Filters.HasFilters)
            {
                var filters = _context.Filters.GetFilters(typeof(T));
                if (filters.Count > 0)
                {
                    var filterBuilder = new WhereBuilder(mapping, _context.Dialect);
                    parameters = new Dictionary<string, object>();
                    foreach (var filter in filters)
                    {
                        var (fSql, fParams) = filterBuilder.BuildFromLambda(filter);
                        whereParts.Add(fSql);
                        if (fParams != null)
                            foreach (var kv in fParams)
                                parameters[kv.Key] = kv.Value;
                    }
                }
            }

            if (predicate != null)
            {
                var builder = new WhereBuilder(mapping, _context.Dialect);
                var (whereSql, whereParams) = builder.Build(predicate);
                whereParts.Add("(" + whereSql + ")");
                if (parameters == null) parameters = whereParams;
                else if (whereParams != null)
                    foreach (var kv in whereParams)
                        parameters[kv.Key] = kv.Value;
            }

            string sql;
            if (whereParts.Count > 0)
                sql = $"SELECT {function}({_context.Dialect.QuoteIdentifier(columnName)}) FROM {tableName} WHERE {string.Join(" AND ", whereParts)}";
            else
                sql = $"SELECT {function}({_context.Dialect.QuoteIdentifier(columnName)}) FROM {tableName}";

            _context.EnsureConnectionOpen();
            return _context.Connection.ExecuteScalar<TResult>(sql, ToDp(parameters),
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout);
        }

        private async Task<TResult> ExecuteAggregateAsync<TResult>(string function,
            LambdaExpression selector, Expression<Func<T, bool>> predicate, CancellationToken ct)
        {
            var mapping = MappingCache.GetMapping<T>();
            var columnName = ExtractColumnNameFromLambda(selector, mapping);
            var tableName = SqlGenerator.QuoteTableName(mapping.TableName, _context.Dialect);

            string sql;
            IDictionary<string, object> parameters = null;
            if (predicate != null)
            {
                var builder = new WhereBuilder(mapping, _context.Dialect);
                var (whereSql, whereParams) = builder.Build(predicate);
                parameters = whereParams;
                sql = $"SELECT {function}({_context.Dialect.QuoteIdentifier(columnName)}) FROM {tableName} WHERE {whereSql}";
            }
            else
            {
                sql = $"SELECT {function}({_context.Dialect.QuoteIdentifier(columnName)}) FROM {tableName}";
            }

            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            return await _context.Connection.ExecuteScalarAsync<TResult>(
                new CommandDefinition(sql, ToDp(parameters),
                    transaction: _context.Transaction, commandTimeout: _context.CommandTimeout,
                    cancellationToken: ct)).ConfigureAwait(false);
        }

        #endregion

        #region Private: Query State

        private void ResetQueryState()
        {
            _orderByClauses = null;
            _skip = null;
            _take = null;
            _pendingPredicates = null;
            _rawWhereFragments = null;
            _subqueryAliasSeq = 0;
            _subqueryParamSeed = 0;
            _cacheDuration = null;
        }

        #endregion

        #region Private: Helpers

        private List<T> GetAll()
        {
            // If caching or global filters are active, go through BuildWhereSql path
            if (_cacheDuration.HasValue || (!_ignoreFilters && _context.Filters.HasFilters
                && _context.Filters.GetFilters(typeof(T)).Count > 0))
            {
                return ExecuteWhere(null);
            }

            var mapping = MappingCache.GetMapping<T>();
            var results = _context.ExecuteQuery<T>(SqlGenerator.GenerateSelectAll(mapping, _context.Dialect)).ToList();
            TrackResults(results, mapping);
            LoadAssociations(results, mapping);
            return results;
        }

        private void TrackResults(List<T> results, EntityMapping mapping)
        {
            if (_noTracking || !_context.ObjectTrackingEnabled) return;
            foreach (var entity in results)
                _changeTracker.TrackLoaded(entity, mapping);
        }

        private void TrackSingle(T entity, EntityMapping mapping)
        {
            if (_noTracking || !_context.ObjectTrackingEnabled) return;
            _changeTracker.TrackLoaded(entity, mapping);
        }

        private static DynamicParameters ToDp(IDictionary<string, object> parameters)
        {
            var dp = new DynamicParameters();
            if (parameters != null)
                foreach (var kv in parameters) dp.Add(kv.Key, kv.Value);
            return dp;
        }

        private static void MergeParams(ref IDictionary<string, object> target,
            IDictionary<string, object> source)
        {
            if (source == null) return;
            if (target == null) { target = source; return; }
            foreach (var kv in source)
                target[kv.Key] = kv.Value;
        }

        /// <summary>
        /// Batch-loads FK navigation properties for a list of entities.
        /// If LoadOptions is set, only loads registered properties.
        /// If LoadOptions is null, loads ALL FK associations automatically.
        /// Uses IN queries to avoid N+1 problem.
        /// </summary>
        private void LoadAssociations(List<T> entities, EntityMapping mapping)
        {
            if (entities == null || entities.Count == 0) return;

            // Load many-to-one FK associations
            if (mapping.Associations != null && mapping.Associations.Count > 0)
            {
                IEnumerable<AssociationMapping> assocsToLoad;

                if (_context.LoadOptions != null)
                {
                    var loadRules = _context.LoadOptions.GetLoadRules(typeof(T));
                    if (loadRules.Count > 0)
                    {
                        assocsToLoad = mapping.Associations
                            .Where(a => a.IsForeignKey && loadRules.Any(r => r.Name == a.Property.Name));
                        foreach (var assoc in assocsToLoad)
                            LoadFkAssociation(entities, assoc);
                    }
                }
                else
                {
                    assocsToLoad = mapping.Associations.Where(a => a.IsForeignKey);
                    foreach (var assoc in assocsToLoad)
                        LoadFkAssociation(entities, assoc);
                }
            }

            // Load one-to-many collection associations
            if (mapping.CollectionAssociations != null && mapping.CollectionAssociations.Count > 0)
            {
                IEnumerable<AssociationMapping> colAssocsToLoad;

                if (_context.LoadOptions != null)
                {
                    var loadRules = _context.LoadOptions.GetLoadRules(typeof(T));
                    colAssocsToLoad = mapping.CollectionAssociations
                        .Where(a => loadRules.Any(r => r.Name == a.Property.Name));
                }
                else
                {
                    colAssocsToLoad = mapping.CollectionAssociations;
                }

                foreach (var assoc in colAssocsToLoad)
                    LoadCollectionAssociation(entities, assoc);
            }
        }

        /// <summary>
        /// Loads a single FK association for all entities using a batch IN query.
        /// </summary>
        private void LoadFkAssociation(List<T> entities, AssociationMapping assoc)
        {
            // Get the FK column property (e.g. idLeader)
            var fkProp = typeof(T).GetProperty(assoc.ThisKey);
            if (fkProp == null) return;

            // Collect all non-null FK values
            var fkValues = entities
                .Select(e => fkProp.GetValue(e))
                .Where(v => v != null && !v.Equals(GetDefault(fkProp.PropertyType)))
                .Distinct()
                .ToList();

            if (fkValues.Count == 0) return;

            // Get related entity mapping
            var relatedMapping = MappingCache.GetMapping(assoc.OtherType);
            var quotedTable = SqlGenerator.QuoteTableName(relatedMapping.TableName, _context.Dialect);

            // Build batch IN query: SELECT * FROM [dbo].[tbSYS_User] WHERE [id] IN (@p0, @p1, ...)
            var dp = new DynamicParameters();
            var paramNames = new List<string>();
            for (int i = 0; i < fkValues.Count; i++)
            {
                var pName = $"@fk{i}";
                paramNames.Add(pName);
                dp.Add(pName, fkValues[i]);
            }

            var sql = $"SELECT * FROM {quotedTable} WHERE {_context.Dialect.QuoteIdentifier(assoc.OtherKey)} IN ({string.Join(", ", paramNames)})";

            _context.EnsureConnectionOpen();
            // Query as dynamic, then use native engine to map
            var relatedEntities = _context.Connection.Query(
                assoc.OtherType, sql, dp,
                transaction: _context.Transaction,
                commandTimeout: _context.CommandTimeout,
                converters: _context.Converters).ToList();

            // Build lookup: OtherKey value → related entity
            var otherKeyProp = assoc.OtherType.GetProperty(assoc.OtherKey);
            if (otherKeyProp == null) return;

            var lookup = new Dictionary<object, object>();
            foreach (var related in relatedEntities)
            {
                var keyVal = otherKeyProp.GetValue(related);
                if (keyVal != null) lookup[keyVal] = related;
            }

            // Set navigation property on each entity
            foreach (var entity in entities)
            {
                var fkVal = fkProp.GetValue(entity);
                if (fkVal != null && lookup.TryGetValue(fkVal, out var relatedEntity))
                {
                    assoc.Property.SetValue(entity, relatedEntity);
                }
            }
        }

        /// <summary>
        /// Async version of LoadAssociations.
        /// </summary>
        private async Task LoadAssociationsAsync(List<T> entities, EntityMapping mapping,
            CancellationToken ct = default)
        {
            if (entities == null || entities.Count == 0) return;

            // Load many-to-one FK associations
            if (mapping.Associations != null && mapping.Associations.Count > 0)
            {
                IEnumerable<AssociationMapping> assocsToLoad;

                if (_context.LoadOptions != null)
                {
                    var loadRules = _context.LoadOptions.GetLoadRules(typeof(T));
                    if (loadRules.Count > 0)
                    {
                        assocsToLoad = mapping.Associations
                            .Where(a => a.IsForeignKey && loadRules.Any(r => r.Name == a.Property.Name));
                        foreach (var assoc in assocsToLoad)
                            await LoadFkAssociationAsync(entities, assoc, ct).ConfigureAwait(false);
                    }
                }
                else
                {
                    assocsToLoad = mapping.Associations.Where(a => a.IsForeignKey);
                    foreach (var assoc in assocsToLoad)
                        await LoadFkAssociationAsync(entities, assoc, ct).ConfigureAwait(false);
                }
            }

            // Load one-to-many collection associations
            if (mapping.CollectionAssociations != null && mapping.CollectionAssociations.Count > 0)
            {
                IEnumerable<AssociationMapping> colAssocsToLoad;

                if (_context.LoadOptions != null)
                {
                    var loadRules = _context.LoadOptions.GetLoadRules(typeof(T));
                    colAssocsToLoad = mapping.CollectionAssociations
                        .Where(a => loadRules.Any(r => r.Name == a.Property.Name));
                }
                else
                {
                    colAssocsToLoad = mapping.CollectionAssociations;
                }

                foreach (var assoc in colAssocsToLoad)
                    await LoadCollectionAssociationAsync(entities, assoc, ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Async batch FK loading.
        /// </summary>
        private async Task LoadFkAssociationAsync(List<T> entities, AssociationMapping assoc,
            CancellationToken ct = default)
        {
            var fkProp = typeof(T).GetProperty(assoc.ThisKey);
            if (fkProp == null) return;

            var fkValues = entities
                .Select(e => fkProp.GetValue(e))
                .Where(v => v != null && !v.Equals(GetDefault(fkProp.PropertyType)))
                .Distinct()
                .ToList();

            if (fkValues.Count == 0) return;

            var relatedMapping = MappingCache.GetMapping(assoc.OtherType);
            var quotedTable = SqlGenerator.QuoteTableName(relatedMapping.TableName, _context.Dialect);

            var dp = new DynamicParameters();
            var paramNames = new List<string>();
            for (int i = 0; i < fkValues.Count; i++)
            {
                var pName = $"@fk{i}";
                paramNames.Add(pName);
                dp.Add(pName, fkValues[i]);
            }

            var sql = $"SELECT * FROM {quotedTable} WHERE {_context.Dialect.QuoteIdentifier(assoc.OtherKey)} IN ({string.Join(", ", paramNames)})";

            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var relatedEntities = (await _context.Connection.QueryAsync(
                assoc.OtherType, sql, dp,
                transaction: _context.Transaction,
                commandTimeout: _context.CommandTimeout,
                cancellationToken: ct,
                converters: _context.Converters).ConfigureAwait(false)).ToList();

            var otherKeyProp = assoc.OtherType.GetProperty(assoc.OtherKey);
            if (otherKeyProp == null) return;

            var lookup = new Dictionary<object, object>();
            foreach (var related in relatedEntities)
            {
                var keyVal = otherKeyProp.GetValue(related);
                if (keyVal != null) lookup[keyVal] = related;
            }

            foreach (var entity in entities)
            {
                var fkVal = fkProp.GetValue(entity);
                if (fkVal != null && lookup.TryGetValue(fkVal, out var relatedEntity))
                {
                    assoc.Property.SetValue(entity, relatedEntity);
                }
            }
        }

        /// <summary>
        /// Loads a one-to-many collection association for all entities using a batch IN query.
        /// Groups children by FK value and sets the collection property.
        /// </summary>
        private void LoadCollectionAssociation(List<T> entities, AssociationMapping assoc)
        {
            // ThisKey is PK on parent, OtherKey is FK on child
            var pkProp = typeof(T).GetProperty(assoc.ThisKey);
            if (pkProp == null) return;

            var pkValues = entities
                .Select(e => pkProp.GetValue(e))
                .Where(v => v != null && !v.Equals(GetDefault(pkProp.PropertyType)))
                .Distinct()
                .ToList();

            if (pkValues.Count == 0) return;

            var childMapping = MappingCache.GetMapping(assoc.OtherType);
            var quotedTable = SqlGenerator.QuoteTableName(childMapping.TableName, _context.Dialect);

            var dp = new DynamicParameters();
            var paramNames = new List<string>();
            for (int i = 0; i < pkValues.Count; i++)
            {
                var pName = $"@ck{i}";
                paramNames.Add(pName);
                dp.Add(pName, pkValues[i]);
            }

            var sql = $"SELECT * FROM {quotedTable} WHERE {_context.Dialect.QuoteIdentifier(assoc.OtherKey)} IN ({string.Join(", ", paramNames)})";

            _context.EnsureConnectionOpen();
            var children = _context.Connection.Query(
                assoc.OtherType, sql, dp,
                transaction: _context.Transaction,
                commandTimeout: _context.CommandTimeout,
                converters: _context.Converters).ToList();

            // Group children by FK value
            var fkPropOnChild = assoc.OtherType.GetProperty(assoc.OtherKey);
            if (fkPropOnChild == null) return;

            var grouped = new Dictionary<object, System.Collections.IList>();
            foreach (var child in children)
            {
                var fkVal = fkPropOnChild.GetValue(child);
                if (fkVal == null) continue;
                if (!grouped.TryGetValue(fkVal, out var list))
                {
                    list = (System.Collections.IList)Activator.CreateInstance(
                        typeof(List<>).MakeGenericType(assoc.OtherType));
                    grouped[fkVal] = list;
                }
                list.Add(child);
            }

            // Set collection on each parent
            foreach (var entity in entities)
            {
                var pkVal = pkProp.GetValue(entity);
                if (pkVal != null && grouped.TryGetValue(pkVal, out var childList))
                {
                    assoc.Property.SetValue(entity, childList);
                }
                else
                {
                    // Set empty list if no children found
                    var emptyList = (System.Collections.IList)Activator.CreateInstance(
                        typeof(List<>).MakeGenericType(assoc.OtherType));
                    assoc.Property.SetValue(entity, emptyList);
                }
            }
        }

        /// <summary>
        /// Async version of LoadCollectionAssociation.
        /// </summary>
        private async Task LoadCollectionAssociationAsync(List<T> entities, AssociationMapping assoc,
            CancellationToken ct = default)
        {
            var pkProp = typeof(T).GetProperty(assoc.ThisKey);
            if (pkProp == null) return;

            var pkValues = entities
                .Select(e => pkProp.GetValue(e))
                .Where(v => v != null && !v.Equals(GetDefault(pkProp.PropertyType)))
                .Distinct()
                .ToList();

            if (pkValues.Count == 0) return;

            var childMapping = MappingCache.GetMapping(assoc.OtherType);
            var quotedTable = SqlGenerator.QuoteTableName(childMapping.TableName, _context.Dialect);

            var dp = new DynamicParameters();
            var paramNames = new List<string>();
            for (int i = 0; i < pkValues.Count; i++)
            {
                var pName = $"@ck{i}";
                paramNames.Add(pName);
                dp.Add(pName, pkValues[i]);
            }

            var sql = $"SELECT * FROM {quotedTable} WHERE {_context.Dialect.QuoteIdentifier(assoc.OtherKey)} IN ({string.Join(", ", paramNames)})";

            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var children = (await _context.Connection.QueryAsync(
                assoc.OtherType, sql, dp,
                transaction: _context.Transaction,
                commandTimeout: _context.CommandTimeout,
                cancellationToken: ct,
                converters: _context.Converters).ConfigureAwait(false)).ToList();

            var fkPropOnChild = assoc.OtherType.GetProperty(assoc.OtherKey);
            if (fkPropOnChild == null) return;

            var grouped = new Dictionary<object, System.Collections.IList>();
            foreach (var child in children)
            {
                var fkVal = fkPropOnChild.GetValue(child);
                if (fkVal == null) continue;
                if (!grouped.TryGetValue(fkVal, out var list))
                {
                    list = (System.Collections.IList)Activator.CreateInstance(
                        typeof(List<>).MakeGenericType(assoc.OtherType));
                    grouped[fkVal] = list;
                }
                list.Add(child);
            }

            foreach (var entity in entities)
            {
                var pkVal = pkProp.GetValue(entity);
                if (pkVal != null && grouped.TryGetValue(pkVal, out var childList))
                {
                    assoc.Property.SetValue(entity, childList);
                }
                else
                {
                    var emptyList = (System.Collections.IList)Activator.CreateInstance(
                        typeof(List<>).MakeGenericType(assoc.OtherType));
                    assoc.Property.SetValue(entity, emptyList);
                }
            }
        }

        private static object GetDefault(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        /// <summary>
        /// Extracts column name from a LambdaExpression using provided mapping.
        /// Used by aggregate helpers.
        /// </summary>
        private static string ExtractColumnNameFromLambda(LambdaExpression expression, EntityMapping mapping)
        {
            var body = expression.Body;
            if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                body = unary.Operand;

            if (body is MemberExpression member)
            {
                var propertyName = member.Member.Name;
                var col = mapping.Columns.FirstOrDefault(c => c.Property.Name == propertyName);
                return col?.ColumnName ?? propertyName;
            }

            throw new ArgumentException(
                $"Expression must be a member access (e.g. x => x.Property), got: {expression}");
        }

        /// <summary>
        /// Extracts the database column name from a member access expression.
        /// Uses MappingCache to resolve [Column(Name=...)] attribute.
        /// </summary>
        internal static string ExtractColumnName<TKey>(Expression<Func<T, TKey>> expression)
        {
            var body = expression.Body;
            // Handle Convert (boxing) for value types
            if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                body = unary.Operand;

            if (body is MemberExpression member)
            {
                var propertyName = member.Member.Name;
                var mapping = MappingCache.GetMapping<T>();
                var col = mapping.Columns.FirstOrDefault(c => c.Property.Name == propertyName);
                return col?.ColumnName ?? propertyName;
            }

            throw new ArgumentException(
                $"Expression must be a member access (e.g. x => x.Property), got: {expression}");
        }

        #endregion
    }
}
