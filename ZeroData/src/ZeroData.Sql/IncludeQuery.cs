using ZeroData.Sql.ChangeTracking;
using ZeroData.Sql.Mapping;
using ZeroData.Sql.Sql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroData.Sql
{
    /// <summary>
    /// Fluent query builder that allows selective FK loading via Include().
    /// When Include() is used, ONLY specified FKs are loaded (not all).
    /// Usage: db.Table.Include(x => x.Nav1).Include(x => x.Nav2).Where(...)
    /// </summary>
    public class IncludeQuery<T> where T : class
    {
        private readonly Table<T> _table;
        private readonly SqlContext _context;
        private readonly ChangeTracker _changeTracker;
        private readonly List<PropertyInfo> _includes = new List<PropertyInfo>();
        private List<string> _orderByClauses;
        private int? _skip;
        private int? _take;

        internal IncludeQuery(Table<T> table, SqlContext context, ChangeTracker changeTracker)
        {
            _table = table;
            _context = context;
            _changeTracker = changeTracker;
        }

        /// <summary>
        /// Specifies an additional FK navigation property to eagerly load.
        /// </summary>
        public IncludeQuery<T> Include(Expression<Func<T, object>> expression)
        {
            var prop = ExtractProperty(expression);
            if (prop != null && !_includes.Contains(prop))
                _includes.Add(prop);
            return this;
        }

        #region OrderBy / ThenBy

        /// <summary>
        /// Sorts results in ascending order.
        /// </summary>
        public IncludeQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _orderByClauses = new List<string>();
            _orderByClauses.Add($"{_context.Dialect.QuoteIdentifier(Table<T>.ExtractColumnName(keySelector))} ASC");
            return this;
        }

        /// <summary>
        /// Sorts results in descending order.
        /// </summary>
        public IncludeQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _orderByClauses = new List<string>();
            _orderByClauses.Add($"{_context.Dialect.QuoteIdentifier(Table<T>.ExtractColumnName(keySelector))} DESC");
            return this;
        }

        /// <summary>
        /// Adds a secondary ascending sort.
        /// </summary>
        public IncludeQuery<T> ThenBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            if (_orderByClauses == null)
                throw new InvalidOperationException("ThenBy must be called after OrderBy or OrderByDescending.");
            _orderByClauses.Add($"{_context.Dialect.QuoteIdentifier(Table<T>.ExtractColumnName(keySelector))} ASC");
            return this;
        }

        /// <summary>
        /// Adds a secondary descending sort.
        /// </summary>
        public IncludeQuery<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            if (_orderByClauses == null)
                throw new InvalidOperationException("ThenByDescending must be called after OrderBy or OrderByDescending.");
            _orderByClauses.Add($"{_context.Dialect.QuoteIdentifier(Table<T>.ExtractColumnName(keySelector))} DESC");
            return this;
        }

        #endregion

        #region Skip / Take

        /// <summary>
        /// Skips N rows.
        /// </summary>
        public IncludeQuery<T> Skip(int count) { _skip = count; return this; }

        /// <summary>
        /// Takes at most N rows.
        /// </summary>
        public IncludeQuery<T> Take(int count) { _take = count; return this; }

        #endregion

        #region Terminal Methods (Sync)

        /// <summary>
        /// Filters entities with a predicate, loading only specified FK associations.
        /// </summary>
        public List<T> Where(Expression<Func<T, bool>> predicate)
        {
            var mapping = MappingCache.GetMapping<T>();
            var (fullSql, dp) = BuildSql(mapping, predicate);

            _context.EnsureConnectionOpen();
            var results = _context.Connection.Query<T>(fullSql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout).ToList();
            TrackResults(results, mapping);
            LoadIncludedAssociations(results, mapping);
            return results;
        }

        /// <summary>
        /// Returns first matching entity with selective FK loading.
        /// </summary>
        public T FirstOrDefault(Expression<Func<T, bool>> predicate)
        {
            if (_take == null) _take = 1;
            var mapping = MappingCache.GetMapping<T>();
            var (fullSql, dp) = BuildSql(mapping, predicate);

            _context.EnsureConnectionOpen();
            var entity = _context.Connection.QueryFirstOrDefault<T>(fullSql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout);
            if (entity != null)
            {
                TrackSingle(entity, mapping);
                LoadIncludedAssociations(new List<T> { entity }, mapping);
            }
            return entity;
        }

        /// <summary>
        /// Executes query respecting OrderBy/Skip/Take and loads included FKs.
        /// </summary>
        public List<T> ToList()
        {
            return Where(null);
        }

        #endregion

        #region Terminal Methods (Async)

        /// <summary>
        /// Async Where with selective FK loading.
        /// </summary>
        public async Task<List<T>> WhereAsync(Expression<Func<T, bool>> predicate,
            CancellationToken ct = default)
        {
            var mapping = MappingCache.GetMapping<T>();
            var (fullSql, dp) = BuildSql(mapping, predicate);

            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var results = (await _context.Connection.QueryAsync<T>(
                new CommandDefinition(fullSql, dp, transaction: _context.Transaction,
                    commandTimeout: _context.CommandTimeout, cancellationToken: ct))
                .ConfigureAwait(false)).ToList();
            TrackResults(results, mapping);
            await LoadIncludedAssociationsAsync(results, mapping, ct).ConfigureAwait(false);
            return results;
        }

        /// <summary>
        /// Async FirstOrDefault with selective FK loading.
        /// </summary>
        public async Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate,
            CancellationToken ct = default)
        {
            if (_take == null) _take = 1;
            var mapping = MappingCache.GetMapping<T>();
            var (fullSql, dp) = BuildSql(mapping, predicate);

            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var entity = await _context.Connection.QueryFirstOrDefaultAsync<T>(fullSql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout)
                .ConfigureAwait(false);
            if (entity != null)
            {
                TrackSingle(entity, mapping);
                await LoadIncludedAssociationsAsync(new List<T> { entity }, mapping, ct)
                    .ConfigureAwait(false);
            }
            return entity;
        }

        /// <summary>
        /// Async ToList with selective FK loading.
        /// </summary>
        public async Task<List<T>> ToListAsync(CancellationToken ct = default)
        {
            return await WhereAsync(null, ct).ConfigureAwait(false);
        }

        #endregion

        #region Private Helpers

        private void LoadIncludedAssociations(List<T> entities, EntityMapping mapping)
        {
            if (entities.Count == 0 || _includes.Count == 0) return;

            // Load many-to-one FK associations
            if (mapping.Associations != null)
            {
                foreach (var navProp in _includes)
                {
                    var assoc = mapping.Associations
                        .FirstOrDefault(a => a.IsForeignKey && a.Property.Name == navProp.Name);
                    if (assoc != null)
                        LoadFkAssociation(entities, assoc);
                }
            }

            // Load one-to-many collection associations
            if (mapping.CollectionAssociations != null)
            {
                foreach (var navProp in _includes)
                {
                    var assoc = mapping.CollectionAssociations
                        .FirstOrDefault(a => a.Property.Name == navProp.Name);
                    if (assoc != null)
                        LoadCollectionAssociation(entities, assoc);
                }
            }
        }

        private async Task LoadIncludedAssociationsAsync(List<T> entities, EntityMapping mapping,
            CancellationToken ct)
        {
            if (entities.Count == 0 || _includes.Count == 0) return;

            // Load many-to-one FK associations
            if (mapping.Associations != null)
            {
                foreach (var navProp in _includes)
                {
                    var assoc = mapping.Associations
                        .FirstOrDefault(a => a.IsForeignKey && a.Property.Name == navProp.Name);
                    if (assoc != null)
                        await LoadFkAssociationAsync(entities, assoc, ct).ConfigureAwait(false);
                }
            }

            // Load one-to-many collection associations
            if (mapping.CollectionAssociations != null)
            {
                foreach (var navProp in _includes)
                {
                    var assoc = mapping.CollectionAssociations
                        .FirstOrDefault(a => a.Property.Name == navProp.Name);
                    if (assoc != null)
                        await LoadCollectionAssociationAsync(entities, assoc, ct).ConfigureAwait(false);
                }
            }
        }

        private void LoadFkAssociation(List<T> entities, AssociationMapping assoc)
        {
            var fkProp = typeof(T).GetProperty(assoc.ThisKey);
            if (fkProp == null) return;

            var fkValues = entities
                .Select(e => fkProp.GetValue(e))
                .Where(v => v != null && !v.Equals(GetDefault(fkProp.PropertyType)))
                .Distinct().ToList();
            if (fkValues.Count == 0) return;

            var relatedMapping = MappingCache.GetMapping(assoc.OtherType);
            var quotedTable = SqlGenerator.QuoteTableName(relatedMapping.TableName, _context.Dialect);

            var dp = new DynamicParameters();
            var paramNames = new List<string>();
            for (int i = 0; i < fkValues.Count; i++)
            {
                paramNames.Add($"@fk{i}");
                dp.Add($"@fk{i}", fkValues[i]);
            }

            var sql = $"SELECT * FROM {quotedTable} WHERE {_context.Dialect.QuoteIdentifier(assoc.OtherKey)} IN ({string.Join(", ", paramNames)})";
            _context.EnsureConnectionOpen();
            var related = _context.Connection.Query(assoc.OtherType, sql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout).ToList();

            var otherKeyProp = assoc.OtherType.GetProperty(assoc.OtherKey);
            if (otherKeyProp == null) return;

            var lookup = new Dictionary<object, object>();
            foreach (var r in related)
            {
                var k = otherKeyProp.GetValue(r);
                if (k != null) lookup[k] = r;
            }

            foreach (var entity in entities)
            {
                var fk = fkProp.GetValue(entity);
                if (fk != null && lookup.TryGetValue(fk, out var rel))
                    assoc.Property.SetValue(entity, rel);
            }
        }

        private async Task LoadFkAssociationAsync(List<T> entities, AssociationMapping assoc,
            CancellationToken ct)
        {
            var fkProp = typeof(T).GetProperty(assoc.ThisKey);
            if (fkProp == null) return;

            var fkValues = entities
                .Select(e => fkProp.GetValue(e))
                .Where(v => v != null && !v.Equals(GetDefault(fkProp.PropertyType)))
                .Distinct().ToList();
            if (fkValues.Count == 0) return;

            var relatedMapping = MappingCache.GetMapping(assoc.OtherType);
            var quotedTable = SqlGenerator.QuoteTableName(relatedMapping.TableName, _context.Dialect);

            var dp = new DynamicParameters();
            var paramNames = new List<string>();
            for (int i = 0; i < fkValues.Count; i++)
            {
                paramNames.Add($"@fk{i}");
                dp.Add($"@fk{i}", fkValues[i]);
            }

            var sql = $"SELECT * FROM {quotedTable} WHERE {_context.Dialect.QuoteIdentifier(assoc.OtherKey)} IN ({string.Join(", ", paramNames)})";
            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var related = (await _context.Connection.QueryAsync(assoc.OtherType, sql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout)
                .ConfigureAwait(false)).ToList();

            var otherKeyProp = assoc.OtherType.GetProperty(assoc.OtherKey);
            if (otherKeyProp == null) return;

            var lookup = new Dictionary<object, object>();
            foreach (var r in related)
            {
                var k = otherKeyProp.GetValue(r);
                if (k != null) lookup[k] = r;
            }

            foreach (var entity in entities)
            {
                var fk = fkProp.GetValue(entity);
                if (fk != null && lookup.TryGetValue(fk, out var rel))
                    assoc.Property.SetValue(entity, rel);
            }
        }

        /// <summary>
        /// Loads a one-to-many collection association using batch IN query.
        /// </summary>
        private void LoadCollectionAssociation(List<T> entities, AssociationMapping assoc)
        {
            var pkProp = typeof(T).GetProperty(assoc.ThisKey);
            if (pkProp == null) return;

            var pkValues = entities.Select(e => pkProp.GetValue(e))
                .Where(v => v != null && !v.Equals(GetDefault(pkProp.PropertyType)))
                .Distinct().ToList();
            if (pkValues.Count == 0) return;

            var childMapping = MappingCache.GetMapping(assoc.OtherType);
            var quotedTable = SqlGenerator.QuoteTableName(childMapping.TableName, _context.Dialect);
            var dp = new DynamicParameters();
            var pn = new List<string>();
            for (int i = 0; i < pkValues.Count; i++) { pn.Add($"@ck{i}"); dp.Add($"@ck{i}", pkValues[i]); }

            var sql = $"SELECT * FROM {quotedTable} WHERE {_context.Dialect.QuoteIdentifier(assoc.OtherKey)} IN ({string.Join(", ", pn)})";
            _context.EnsureConnectionOpen();
            var children = _context.Connection.Query(assoc.OtherType, sql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout).ToList();

            var fkPropOnChild = assoc.OtherType.GetProperty(assoc.OtherKey);
            if (fkPropOnChild == null) return;

            var grouped = new Dictionary<object, System.Collections.IList>();
            foreach (var child in children)
            {
                var fkVal = fkPropOnChild.GetValue(child);
                if (fkVal == null) continue;
                if (!grouped.TryGetValue(fkVal, out var list))
                {
                    list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(assoc.OtherType));
                    grouped[fkVal] = list;
                }
                list.Add(child);
            }

            foreach (var entity in entities)
            {
                var pkVal = pkProp.GetValue(entity);
                if (pkVal != null && grouped.TryGetValue(pkVal, out var cl))
                    assoc.Property.SetValue(entity, cl);
                else
                    assoc.Property.SetValue(entity, (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(assoc.OtherType)));
            }
        }

        /// <summary>
        /// Async LoadCollectionAssociation.
        /// </summary>
        private async Task LoadCollectionAssociationAsync(List<T> entities, AssociationMapping assoc,
            CancellationToken ct)
        {
            var pkProp = typeof(T).GetProperty(assoc.ThisKey);
            if (pkProp == null) return;

            var pkValues = entities.Select(e => pkProp.GetValue(e))
                .Where(v => v != null && !v.Equals(GetDefault(pkProp.PropertyType)))
                .Distinct().ToList();
            if (pkValues.Count == 0) return;

            var childMapping = MappingCache.GetMapping(assoc.OtherType);
            var quotedTable = SqlGenerator.QuoteTableName(childMapping.TableName, _context.Dialect);
            var dp = new DynamicParameters();
            var pn = new List<string>();
            for (int i = 0; i < pkValues.Count; i++) { pn.Add($"@ck{i}"); dp.Add($"@ck{i}", pkValues[i]); }

            var sql = $"SELECT * FROM {quotedTable} WHERE {_context.Dialect.QuoteIdentifier(assoc.OtherKey)} IN ({string.Join(", ", pn)})";
            await _context.EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var children = (await _context.Connection.QueryAsync(assoc.OtherType, sql, dp,
                transaction: _context.Transaction, commandTimeout: _context.CommandTimeout)
                .ConfigureAwait(false)).ToList();

            var fkPropOnChild = assoc.OtherType.GetProperty(assoc.OtherKey);
            if (fkPropOnChild == null) return;

            var grouped = new Dictionary<object, System.Collections.IList>();
            foreach (var child in children)
            {
                var fkVal = fkPropOnChild.GetValue(child);
                if (fkVal == null) continue;
                if (!grouped.TryGetValue(fkVal, out var list))
                {
                    list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(assoc.OtherType));
                    grouped[fkVal] = list;
                }
                list.Add(child);
            }

            foreach (var entity in entities)
            {
                var pkVal = pkProp.GetValue(entity);
                if (pkVal != null && grouped.TryGetValue(pkVal, out var cl))
                    assoc.Property.SetValue(entity, cl);
                else
                    assoc.Property.SetValue(entity, (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(assoc.OtherType)));
            }
        }

        private void TrackResults(List<T> results, EntityMapping mapping)
        {
            if (!_context.ObjectTrackingEnabled) return;
            foreach (var entity in results)
                _changeTracker.TrackLoaded(entity, mapping);
        }

        private void TrackSingle(T entity, EntityMapping mapping)
        {
            if (!_context.ObjectTrackingEnabled) return;
            _changeTracker.TrackLoaded(entity, mapping);
        }

        private static DynamicParameters ToDp(IDictionary<string, object> parameters)
        {
            var dp = new DynamicParameters();
            if (parameters != null)
                foreach (var kv in parameters) dp.Add(kv.Key, kv.Value);
            return dp;
        }

        private static object GetDefault(Type type)
            => type.IsValueType ? Activator.CreateInstance(type) : null;

        private static PropertyInfo ExtractProperty(Expression<Func<T, object>> expression)
        {
            var body = expression.Body;
            if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                body = unary.Operand;
            return body is MemberExpression member && member.Member is PropertyInfo prop ? prop : null;
        }

        /// <summary>
        /// Builds the full SQL including WHERE, ORDER BY, and pagination.
        /// </summary>
        private (string sql, DynamicParameters dp) BuildSql(
            EntityMapping mapping, Expression<Func<T, bool>> predicate)
        {
            var selectAll = SqlGenerator.GenerateSelectAll(mapping, _context.Dialect);
            IDictionary<string, object> parameters = null;

            string fullSql;
            if (predicate != null)
            {
                var builder = new WhereBuilder(mapping, _context.Dialect);
                var (whereSql, whereParams) = builder.Build(predicate);
                parameters = whereParams;
                fullSql = $"{selectAll} WHERE {whereSql}";
            }
            else
            {
                fullSql = selectAll;
            }

            // ORDER BY
            if (_orderByClauses != null && _orderByClauses.Count > 0)
                fullSql += $" ORDER BY {string.Join(", ", _orderByClauses)}";

            // Pagination (dialect-aware)
            var provider = _context.Dialect.ProviderName;
            var usesLimit = provider == "SQLite" || provider == "MySQL" || provider == "PostgreSQL";

            if (_skip.HasValue || _take.HasValue)
            {
                if (usesLimit)
                {
                    if (_take.HasValue) fullSql += $" LIMIT {_take.Value}";
                    else fullSql += " LIMIT -1";
                    if (_skip.HasValue) fullSql += $" OFFSET {_skip.Value}";
                }
                else
                {
                    if (_orderByClauses == null || _orderByClauses.Count == 0)
                        fullSql += " ORDER BY (SELECT NULL)";
                    fullSql += $" OFFSET {_skip ?? 0} ROWS";
                    if (_take.HasValue)
                        fullSql += $" FETCH NEXT {_take.Value} ROWS ONLY";
                }
            }

            return (fullSql, ToDp(parameters));
        }

        #endregion
    }
}
