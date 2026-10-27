using ZeroData.Core;
using ZeroData.Sql.Caching;
using ZeroData.Sql.ChangeTracking;
using ZeroData.Sql.Dialects;
using ZeroData.Sql.Mapping;
using ZeroData.Sql.Resilience;
using ZeroData.Sql.Sql;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroData.Sql
{
    /// <summary>
    /// Lightweight DataContext replacement for .NET Core, backed by native ADO.NET engine.
    /// Manages database connections, transactions, and entity change tracking.
    /// Thread-safe for read operations. Not thread-safe for concurrent writes on the same context instance.
    /// Compatible with System.Data.Linq.DataContext API surface.
    /// Provides both sync and async APIs.
    /// </summary>
    public class SqlContext : IDisposable
    {
        private readonly bool _ownsConnection;
        private readonly ConcurrentDictionary<Type, object> _tables
            = new ConcurrentDictionary<Type, object>();
        private readonly ChangeTracker _changeTracker = new ChangeTracker();
        private bool _disposed;
        private ISqlDialect _dialect;

        /// <summary>
        /// Provides lifecycle hooks for entity save operations.
        /// Register OnBeforeSave/OnAfterSave handlers per entity type.
        /// </summary>
        public SaveHooks Hooks { get; } = new SaveHooks();

        /// <summary>
        /// Exposes ChangeTracker API for querying entity states.
        /// </summary>
        public ChangeTracker ChangeTracker => _changeTracker;

        /// <summary>
        /// Global query filters applied automatically to all queries.
        /// Register per-type predicates (e.g., soft delete, multi-tenant).
        /// </summary>
        public QueryFilterCollection Filters { get; } = new QueryFilterCollection();

        /// <summary>
        /// Query profiler for monitoring execution times and detecting slow queries.
        /// </summary>
        public QueryProfiler Profiler { get; } = new QueryProfiler();

        /// <summary>
        /// Query execution pipeline interceptor.
        /// Provides OnBeforeExecute/OnAfterExecute/OnError hooks.
        /// </summary>
        public QueryInterceptor Interceptor { get; set; }

        /// <summary>
        /// Value converter registry.
        /// Converts model types ⇄ database types (e.g., Enum ↔ string, object ↔ JSON).
        /// </summary>
        public ValueConverterCollection Converters { get; } = new ValueConverterCollection();

        /// <summary>
        /// Second-level query cache. When set, queries using FromCache() or AsCached() will be cached and auto-invalidated on writes.
        /// </summary>
        public IL2QueryCache QueryCache { get; set; }

        /// <summary>
        /// Retry policy for transient database errors.
        /// </summary>
        public RetryPolicy RetryPolicy { get; set; }

        #region Constructors

        public SqlContext(IDbConnection connection, ISqlDialect dialect = null)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _dialect = dialect ?? SqlDialectFactory.GetDialect(connection);
            _ownsConnection = false;
        }

        public SqlContext(string connectionString) : this(connectionString, null, null)
        {
        }

        public SqlContext(string connectionString, string providerName, ISqlDialect dialect = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (providerName != null && Providers.DbProviderRegistry.TryGetFactory(providerName, out var registeredFactory))
            {
                Connection = registeredFactory(connectionString);
                _dialect = dialect ?? SqlDialectFactory.GetDialect(providerName);
            }
            else if (ConnectionFactory != null)
            {
                Connection = ConnectionFactory(connectionString);
                _dialect = dialect ?? (providerName != null ? SqlDialectFactory.GetDialect(providerName) : SqlDialectFactory.GetDialect(Connection));
            }
            else
            {
                var targetProvider = providerName ?? Providers.DbProviderRegistry.DetectProvider(connectionString);
                Connection = Providers.DbProviderRegistry.CreateConnection(connectionString, targetProvider);
                _dialect = dialect ?? SqlDialectFactory.GetDialect(targetProvider);
            }

            _ownsConnection = true;
        }

        #endregion

        #region Static Configuration & Multi-DB Factories

        public static Func<string, IDbConnection> ConnectionFactory { get; set; }

        /// <summary>
        /// Creates a new SqlContext instance using a connection string and optional provider name.
        /// </summary>
        public static SqlContext Create(string connectionString, string providerName = null, ISqlDialect dialect = null)
            => new SqlContext(connectionString, providerName, dialect);

        /// <summary>
        /// Creates a new SqlContext configured for Microsoft SQL Server.
        /// </summary>
        public static SqlContext CreateSqlServer(string connectionString)
            => new SqlContext(connectionString, "SqlServer", new Dialects.SqlServerDialect());

        /// <summary>
        /// Creates a new SqlContext configured for SQLite.
        /// </summary>
        public static SqlContext CreateSqlite(string connectionString)
            => new SqlContext(connectionString, "Sqlite", new Dialects.SqliteDialect());

        /// <summary>
        /// Creates a new SqlContext configured for PostgreSQL.
        /// </summary>
        public static SqlContext CreatePostgreSql(string connectionString)
            => new SqlContext(connectionString, "PostgreSql", new Dialects.PostgreSqlDialect());

        /// <summary>
        /// Creates a new SqlContext configured for MySQL / MariaDB.
        /// </summary>
        public static SqlContext CreateMySql(string connectionString)
            => new SqlContext(connectionString, "MySql", new Dialects.MySqlDialect());

        /// <summary>
        /// Creates a new SqlContext configured for Oracle Database.
        /// </summary>
        public static SqlContext CreateOracle(string connectionString)
            => new SqlContext(connectionString, "Oracle", new Dialects.OracleDialect());

        /// <summary>
        /// Creates a new SqlContext configured for Firebird Database.
        /// </summary>
        public static SqlContext CreateFirebird(string connectionString)
            => new SqlContext(connectionString, "Firebird", new Dialects.FirebirdDialect());

        #endregion

        #region Properties

        public IDbConnection Connection { get; }
        public IDbTransaction Transaction { get; set; }
        public int CommandTimeout { get; set; } = 30;
        public TextWriter Log { get; set; }
        public bool ObjectTrackingEnabled { get; set; } = true;

        /// <summary>
        /// Gets the SQL dialect used by this context.
        /// </summary>
        public ISqlDialect Dialect => _dialect;

        /// <summary>
        /// Specifies which navigation properties to eagerly load when querying.
        /// Compatible with System.Data.Linq.DataLoadOptions.
        /// </summary>
        public DataLoadOptions LoadOptions { get; set; }

        #endregion

        #region Transaction and Savepoint Management

        /// <summary>
        /// Begins a database transaction with the specified isolation level.
        /// </summary>
        public IDbTransaction BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.Unspecified)
        {
            ThrowIfDisposed();
            EnsureConnectionOpen();
            Transaction = isolationLevel == IsolationLevel.Unspecified
                ? Connection.BeginTransaction()
                : Connection.BeginTransaction(isolationLevel);
            return Transaction;
        }

        /// <summary>
        /// Commits the active database transaction and disposes it.
        /// </summary>
        public void CommitTransaction()
        {
            ThrowIfDisposed();
            if (Transaction == null)
                throw new InvalidOperationException("No active transaction to commit.");
            Transaction.Commit();
            Transaction.Dispose();
            Transaction = null;
        }

        /// <summary>
        /// Rolls back the active database transaction and disposes it.
        /// </summary>
        public void RollbackTransaction()
        {
            ThrowIfDisposed();
            if (Transaction == null)
                throw new InvalidOperationException("No active transaction to rollback.");
            Transaction.Rollback();
            Transaction.Dispose();
            Transaction = null;
        }

        /// <summary>
        /// Creates a named savepoint within the current transaction.
        /// </summary>
        public void CreateSavepoint(string name)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Savepoint name cannot be null or whitespace.", nameof(name));
            if (Transaction == null)
                throw new InvalidOperationException("Cannot create a savepoint without an active transaction. Call BeginTransaction() first.");

            var sql = _dialect.GetCreateSavepointSql(name);
            if (!string.IsNullOrWhiteSpace(sql))
            {
                using var cmd = Connection.CreateCommand();
                cmd.Transaction = Transaction;
                cmd.CommandText = sql;
                cmd.CommandTimeout = CommandTimeout;
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Rolls back the current transaction to a named savepoint without aborting the entire transaction.
        /// </summary>
        public void RollbackToSavepoint(string name)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Savepoint name cannot be null or whitespace.", nameof(name));
            if (Transaction == null)
                throw new InvalidOperationException("Cannot rollback to a savepoint without an active transaction.");

            var sql = _dialect.GetRollbackSavepointSql(name);
            if (!string.IsNullOrWhiteSpace(sql))
            {
                using var cmd = Connection.CreateCommand();
                cmd.Transaction = Transaction;
                cmd.CommandText = sql;
                cmd.CommandTimeout = CommandTimeout;
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Releases a named savepoint if supported by the database dialect.
        /// </summary>
        public void ReleaseSavepoint(string name)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Savepoint name cannot be null or whitespace.", nameof(name));
            if (Transaction == null)
                throw new InvalidOperationException("Cannot release a savepoint without an active transaction.");

            var sql = _dialect.GetReleaseSavepointSql(name);
            if (!string.IsNullOrWhiteSpace(sql))
            {
                using var cmd = Connection.CreateCommand();
                cmd.Transaction = Transaction;
                cmd.CommandText = sql;
                cmd.CommandTimeout = CommandTimeout;
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Creates a named savepoint within the current transaction asynchronously.
        /// </summary>
        public async Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Savepoint name cannot be null or whitespace.", nameof(name));
            if (Transaction == null)
                throw new InvalidOperationException("Cannot create a savepoint without an active transaction. Call BeginTransaction() first.");

            var sql = _dialect.GetCreateSavepointSql(name);
            if (!string.IsNullOrWhiteSpace(sql))
            {
                using var cmd = Connection.CreateCommand();
                cmd.Transaction = Transaction;
                cmd.CommandText = sql;
                cmd.CommandTimeout = CommandTimeout;
                if (cmd is DbCommand dbCmd)
                {
                    await dbCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Rolls back the current transaction to a named savepoint asynchronously.
        /// </summary>
        public async Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Savepoint name cannot be null or whitespace.", nameof(name));
            if (Transaction == null)
                throw new InvalidOperationException("Cannot rollback to a savepoint without an active transaction.");

            var sql = _dialect.GetRollbackSavepointSql(name);
            if (!string.IsNullOrWhiteSpace(sql))
            {
                using var cmd = Connection.CreateCommand();
                cmd.Transaction = Transaction;
                cmd.CommandText = sql;
                cmd.CommandTimeout = CommandTimeout;
                if (cmd is DbCommand dbCmd)
                {
                    await dbCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Releases a named savepoint asynchronously if supported by the database dialect.
        /// </summary>
        public async Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Savepoint name cannot be null or whitespace.", nameof(name));
            if (Transaction == null)
                throw new InvalidOperationException("Cannot release a savepoint without an active transaction.");

            var sql = _dialect.GetReleaseSavepointSql(name);
            if (!string.IsNullOrWhiteSpace(sql))
            {
                using var cmd = Connection.CreateCommand();
                cmd.Transaction = Transaction;
                cmd.CommandText = sql;
                cmd.CommandTimeout = CommandTimeout;
                if (cmd is DbCommand dbCmd)
                {
                    await dbCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        #endregion

        #region Core Sync Methods

        public Table<T> GetTable<T>() where T : class
        {
            ThrowIfDisposed();
            return (Table<T>)_tables.GetOrAdd(typeof(T), _ => new Table<T>(this, _changeTracker));
        }

        public void SubmitChanges()
        {
            ThrowIfDisposed();
            DetectAllChanges();
            var changes = _changeTracker.GetPendingChanges();
            if (changes.Count == 0) return;

            EnsureConnectionOpen();
            var ownTx = Transaction == null;
            var tx = Transaction ?? Connection.BeginTransaction();
            try
            {
                ProcessChanges(changes, tx);
                if (ownTx) tx.Commit();
                _changeTracker.AcceptChanges();

                if (QueryCache != null)
                {
                    var changedTypes = changes.Select(c => c.EntityType).Distinct();
                    foreach (var type in changedTypes)
                    {
                        QueryCache.Invalidate(type);
                    }
                }
            }
            catch { if (ownTx) tx.Rollback(); throw; }
            finally { if (ownTx) tx.Dispose(); }
        }

        private string ApplyBeforeExecute(string sql, object param)
        {
            if (Interceptor?.OnBeforeExecute != null)
            {
                return Interceptor.OnBeforeExecute(sql, param) ?? sql;
            }
            return sql;
        }

        private void ApplyAfterExecute(string sql, System.Diagnostics.Stopwatch sw, int rowCount)
        {
            if (sw != null)
            {
                sw.Stop();
                Interceptor?.OnAfterExecute?.Invoke(sql, sw.Elapsed, rowCount);
            }
        }

        private void ApplyError(string sql, Exception ex)
        {
            Interceptor?.OnError?.Invoke(sql, ex);
        }

        public IEnumerable<T> ExecuteQuery<T>(string query, params object[] parameters)
        {
            ThrowIfDisposed();
            EnsureConnectionOpen();
            var (sql, dp) = ConvertParams(query, parameters);
            sql = ApplyBeforeExecute(sql, dp);
            var sw = Profiler.StartQuery();
            try
            {
                var results = Connection.Query<T>(sql, (object)dp, transaction: Transaction, commandTimeout: CommandTimeout, converters: Converters).ToList();
                ApplyAfterExecute(sql, sw, results.Count);
                return results;
            }
            catch (Exception ex)
            {
                ApplyError(sql, ex);
                throw;
            }
        }

        public int ExecuteCommand(string command, params object[] parameters)
        {
            ThrowIfDisposed();
            EnsureConnectionOpen();
            var (sql, dp) = ConvertParams(command, parameters);
            sql = ApplyBeforeExecute(sql, dp);
            var sw = Profiler.StartQuery();
            try
            {
                var affected = Connection.Execute(sql, (object)dp, transaction: Transaction, commandTimeout: CommandTimeout, converters: Converters);
                ApplyAfterExecute(sql, sw, affected);
                return affected;
            }
            catch (Exception ex)
            {
                ApplyError(sql, ex);
                throw;
            }
        }

        /// <summary>
        /// Executes raw SQL and maps results to T using named @parameters.
        /// Unlike ExecuteQuery which uses positional {0} params,
        /// this accepts an anonymous object for named parameters.
        /// Example: db.FromSql&lt;Order&gt;("SELECT * FROM Orders WHERE Status = @status", new { status = "Active" })
        /// </summary>
        public List<T> FromSql<T>(string sql, object parameters = null)
        {
            ThrowIfDisposed();
            EnsureConnectionOpen();
            var effectiveSql = ApplyBeforeExecute(sql, parameters);
            var sw = Profiler.StartQuery();
            try
            {
                var results = Connection.Query<T>(effectiveSql, parameters,
                    transaction: Transaction, commandTimeout: CommandTimeout, converters: Converters).ToList();
                ApplyAfterExecute(effectiveSql, sw, results.Count);
                return results;
            }
            catch (Exception ex)
            {
                ApplyError(effectiveSql, ex);
                throw;
            }
        }

        /// <summary>
        /// Async version of FromSql.
        /// </summary>
        public async Task<List<T>> FromSqlAsync<T>(string sql, object parameters = null,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var effectiveSql = ApplyBeforeExecute(sql, parameters);
            var sw = Profiler.StartQuery();
            try
            {
                var results = (await Connection.QueryAsync<T>(
                    new CommandDefinition(effectiveSql, parameters,
                        transaction: Transaction, commandTimeout: CommandTimeout,
                        cancellationToken: ct), converters: Converters).ConfigureAwait(false)).ToList();
                ApplyAfterExecute(effectiveSql, sw, results.Count);
                return results;
            }
            catch (Exception ex)
            {
                ApplyError(effectiveSql, ex);
                throw;
            }
        }

        #region Stored Procedure Execution

        /// <summary>
        /// Executes a stored procedure and materializes the result set into entities of type T.
        /// </summary>
        public List<T> ExecuteStoredProcedure<T>(string spName, object parameters = null)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(spName))
                throw new ArgumentException("Stored procedure name cannot be null or empty.", nameof(spName));

            EnsureConnectionOpen();
            var sw = Profiler.StartQuery();
            try
            {
                var results = Connection.Query<T>(spName, parameters,
                    transaction: Transaction, commandTimeout: CommandTimeout,
                    commandType: CommandType.StoredProcedure, converters: Converters).ToList();
                ApplyAfterExecute(spName, sw, results.Count);
                return results;
            }
            catch (Exception ex)
            {
                ApplyError(spName, ex);
                throw;
            }
        }

        /// <summary>
        /// Executes a non-query stored procedure and returns the number of affected rows.
        /// </summary>
        public int ExecuteStoredProcedure(string spName, object parameters = null)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(spName))
                throw new ArgumentException("Stored procedure name cannot be null or empty.", nameof(spName));

            EnsureConnectionOpen();
            var sw = Profiler.StartQuery();
            try
            {
                var affected = Connection.Execute(spName, parameters,
                    transaction: Transaction, commandTimeout: CommandTimeout,
                    commandType: CommandType.StoredProcedure, converters: Converters);
                ApplyAfterExecute(spName, sw, affected);
                return affected;
            }
            catch (Exception ex)
            {
                ApplyError(spName, ex);
                throw;
            }
        }

        /// <summary>
        /// Asynchronously executes a stored procedure and materializes the result set into entities of type T.
        /// </summary>
        public async Task<List<T>> ExecuteStoredProcedureAsync<T>(string spName, object parameters = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(spName))
                throw new ArgumentException("Stored procedure name cannot be null or empty.", nameof(spName));

            await EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var sw = Profiler.StartQuery();
            try
            {
                var results = (await Connection.QueryAsync<T>(spName, parameters,
                    transaction: Transaction, commandTimeout: CommandTimeout,
                    commandType: CommandType.StoredProcedure, cancellationToken: ct, converters: Converters).ConfigureAwait(false)).ToList();
                ApplyAfterExecute(spName, sw, results.Count);
                return results;
            }
            catch (Exception ex)
            {
                ApplyError(spName, ex);
                throw;
            }
        }

        /// <summary>
        /// Asynchronously executes a non-query stored procedure and returns the number of affected rows.
        /// </summary>
        public async Task<int> ExecuteStoredProcedureAsync(string spName, object parameters = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(spName))
                throw new ArgumentException("Stored procedure name cannot be null or empty.", nameof(spName));

            await EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var sw = Profiler.StartQuery();
            try
            {
                var affected = await Connection.ExecuteAsync(spName, parameters,
                    transaction: Transaction, commandTimeout: CommandTimeout,
                    commandType: CommandType.StoredProcedure, cancellationToken: ct).ConfigureAwait(false);
                ApplyAfterExecute(spName, sw, affected);
                return affected;
            }
            catch (Exception ex)
            {
                ApplyError(spName, ex);
                throw;
            }
        }

        #endregion

        #region Entity Lifecycle & Audit

        /// <summary>
        /// Gets the original property values of a tracked entity when it was first loaded.
        /// Ideal for audit logging changed properties.
        /// </summary>
        public IReadOnlyDictionary<string, object> GetOriginalValues(object entity)
        {
            ThrowIfDisposed();
            return _changeTracker.GetOriginalValues(entity);
        }

        /// <summary>
        /// Reloads the entity from the database, overwriting its current in-memory properties
        /// with fresh values and resetting its tracked snapshot.
        /// </summary>
        public void Reload<T>(T entity) where T : class
        {
            ThrowIfDisposed();
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var mapping = MappingCache.GetMapping<T>();
            if (mapping.PrimaryKeys.Count == 0)
                throw new InvalidOperationException($"Entity '{typeof(T).Name}' has no primary key defined.");

            var pkCol = mapping.PrimaryKeys[0];
            var pkVal = pkCol.Property.GetValue(entity);

            var fresh = GetTable<T>().Find(pkVal);
            if (fresh == null)
                throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' with key '{pkVal}' was not found in the database.");

            // Copy properties from fresh to existing entity
            foreach (var col in mapping.Columns)
            {
                var val = col.Property.GetValue(fresh);
                if (col.Setter != null)
                    col.Setter(entity, val);
                else
                    col.Property.SetValue(entity, val);
            }

            // Synchronize snapshot in ChangeTracker
            _changeTracker.UpdateSnapshot(entity, mapping);
        }

        /// <summary>
        /// Asynchronously reloads the entity from the database, overwriting its current in-memory properties
        /// with fresh values and resetting its tracked snapshot.
        /// </summary>
        public async Task ReloadAsync<T>(T entity, CancellationToken ct = default) where T : class
        {
            ThrowIfDisposed();
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var mapping = MappingCache.GetMapping<T>();
            if (mapping.PrimaryKeys.Count == 0)
                throw new InvalidOperationException($"Entity '{typeof(T).Name}' has no primary key defined.");

            var pkCol = mapping.PrimaryKeys[0];
            var pkVal = pkCol.Property.GetValue(entity);

            var fresh = await GetTable<T>().FindAsync(pkVal).ConfigureAwait(false);
            if (fresh == null)
                throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' with key '{pkVal}' was not found in the database.");

            // Copy properties from fresh to existing entity
            foreach (var col in mapping.Columns)
            {
                var val = col.Property.GetValue(fresh);
                if (col.Setter != null)
                    col.Setter(entity, val);
                else
                    col.Property.SetValue(entity, val);
            }

            // Synchronize snapshot in ChangeTracker
            _changeTracker.UpdateSnapshot(entity, mapping);
        }

        #endregion

        #endregion

        #region Core Async Methods

        public async Task SubmitChangesAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
            DetectAllChanges();
            var changes = _changeTracker.GetPendingChanges();
            if (changes.Count == 0) return;

            await EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var ownTx = Transaction == null;
            var tx = Transaction ?? Connection.BeginTransaction();
            try
            {
                await ProcessChangesAsync(changes, tx, ct).ConfigureAwait(false);
                if (ownTx) tx.Commit();
                _changeTracker.AcceptChanges();

                if (QueryCache != null)
                {
                    var changedTypes = changes.Select(c => c.EntityType).Distinct();
                    foreach (var type in changedTypes)
                    {
                        QueryCache.Invalidate(type);
                    }
                }
            }
            catch { if (ownTx) tx.Rollback(); throw; }
            finally { if (ownTx) tx.Dispose(); }
        }

        public async Task<IEnumerable<T>> ExecuteQueryAsync<T>(string query, params object[] parameters)
        {
            ThrowIfDisposed();
            await EnsureConnectionOpenAsync().ConfigureAwait(false);
            var (sql, dp) = ConvertParams(query, parameters);
            sql = ApplyBeforeExecute(sql, dp);
            var sw = Profiler.StartQuery();
            try
            {
                var results = (await Connection.QueryAsync<T>(sql, (object)dp,
                    transaction: Transaction, commandTimeout: CommandTimeout, converters: Converters).ConfigureAwait(false)).ToList();
                ApplyAfterExecute(sql, sw, results.Count);
                return results;
            }
            catch (Exception ex)
            {
                ApplyError(sql, ex);
                throw;
            }
        }

        public async Task<int> ExecuteCommandAsync(string command, params object[] parameters)
        {
            ThrowIfDisposed();
            await EnsureConnectionOpenAsync().ConfigureAwait(false);
            var (sql, dp) = ConvertParams(command, parameters);
            sql = ApplyBeforeExecute(sql, dp);
            var sw = Profiler.StartQuery();
            try
            {
                var affected = await Connection.ExecuteAsync(sql, (object)dp,
                    transaction: Transaction, commandTimeout: CommandTimeout).ConfigureAwait(false);
                ApplyAfterExecute(sql, sw, affected);
                return affected;
            }
            catch (Exception ex)
            {
                ApplyError(sql, ex);
                throw;
            }
        }

        #endregion

        #region Modern ZeroData API Shortcuts

        /// <summary>
        /// Gets the Table<T> for the specified entity type (modern alias for GetTable<T>).
        /// </summary>
        public Table<T> Set<T>() where T : class => GetTable<T>();

        /// <summary>
        /// Inserts an entity into its corresponding table.
        /// Modern shortcut for GetTable<T>().Insert(entity).
        /// </summary>
        public void Insert<T>(T entity) where T : class => GetTable<T>().Insert(entity);

        /// <summary>
        /// Inserts multiple entities into their corresponding table.
        /// </summary>
        public void InsertRange<T>(IEnumerable<T> entities) where T : class => GetTable<T>().InsertRange(entities);

        /// <summary>
        /// Adds an entity for insertion (alias for Insert).
        /// </summary>
        public void Add<T>(T entity) where T : class => GetTable<T>().Add(entity);

        /// <summary>
        /// Adds multiple entities for insertion (alias for InsertRange).
        /// </summary>
        public void AddRange<T>(IEnumerable<T> entities) where T : class => GetTable<T>().AddRange(entities);

        /// <summary>
        /// Attaches and marks an entity as modified for update.
        /// </summary>
        public void Update<T>(T entity) where T : class => GetTable<T>().Update(entity);

        /// <summary>
        /// Marks an entity for deletion.
        /// Modern shortcut for GetTable<T>().Delete(entity).
        /// </summary>
        public void Delete<T>(T entity) where T : class => GetTable<T>().Delete(entity);

        /// <summary>
        /// Marks multiple entities for deletion.
        /// </summary>
        public void DeleteRange<T>(IEnumerable<T> entities) where T : class => GetTable<T>().DeleteRange(entities);

        /// <summary>
        /// Deletes an entity directly by its primary key without fetching it first.
        /// Respects SoftDeleteAttribute unless forceHardDelete is true.
        /// </summary>
        public int DeleteById<T>(object id, bool forceHardDelete = false) where T : class
            => GetTable<T>().DeleteById(id, forceHardDelete);

        /// <summary>
        /// Asynchronously deletes an entity directly by primary key without fetching it first.
        /// Respects SoftDeleteAttribute unless forceHardDelete is true.
        /// </summary>
        public Task<int> DeleteByIdAsync<T>(object id, bool forceHardDelete = false, CancellationToken ct = default) where T : class
            => GetTable<T>().DeleteByIdAsync(id, forceHardDelete, ct);

        /// <summary>
        /// Deletes all entities matching the specified predicate directly on the server without loading them into memory.
        /// Respects SoftDeleteAttribute unless forceHardDelete is true.
        /// </summary>
        public int DeleteWhere<T>(Expression<Func<T, bool>> predicate, bool forceHardDelete = false) where T : class
            => GetTable<T>().DeleteWhere(predicate, forceHardDelete);

        /// <summary>
        /// Asynchronously deletes all entities matching the specified predicate directly on the server.
        /// Respects SoftDeleteAttribute unless forceHardDelete is true.
        /// </summary>
        public Task<int> DeleteWhereAsync<T>(Expression<Func<T, bool>> predicate, bool forceHardDelete = false, CancellationToken ct = default) where T : class
            => GetTable<T>().DeleteWhereAsync(predicate, forceHardDelete, ct);

        /// <summary>
        /// Updates all entities matching the specified predicate directly on the database server.
        /// Accepts an anonymous object or dictionary of values.
        /// </summary>
        public int UpdateWhere<T>(Expression<Func<T, bool>> predicate, object updateValues) where T : class
            => GetTable<T>().UpdateWhere(predicate, updateValues);

        /// <summary>
        /// Asynchronously updates all entities matching the specified predicate directly on the database server.
        /// Accepts an anonymous object or dictionary of values.
        /// </summary>
        public Task<int> UpdateWhereAsync<T>(Expression<Func<T, bool>> predicate, object updateValues, CancellationToken ct = default) where T : class
            => GetTable<T>().UpdateWhereAsync(predicate, updateValues, ct);

        /// <summary>
        /// Updates all entities matching the specified predicate directly on the database server using an expression factory.
        /// </summary>
        public int UpdateWhere<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, T>> updateFactory) where T : class
            => GetTable<T>().UpdateWhere(predicate, updateFactory);

        /// <summary>
        /// Asynchronously updates all entities matching the specified predicate directly on the database server using an expression factory.
        /// </summary>
        public Task<int> UpdateWhereAsync<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, T>> updateFactory, CancellationToken ct = default) where T : class
            => GetTable<T>().UpdateWhereAsync(predicate, updateFactory, ct);

        /// <summary>
        /// Detaches an entity from change tracking.
        /// </summary>
        public void Detach<T>(T entity) where T : class => GetTable<T>().Detach(entity);

        /// <summary>
        /// Finds an entity quickly by primary key.
        /// </summary>
        public T Get<T>(object id) where T : class => GetTable<T>().Get(id);

        /// <summary>
        /// Asynchronously finds an entity quickly by primary key.
        /// </summary>
        public Task<T> GetAsync<T>(object id, CancellationToken ct = default) where T : class
            => GetTable<T>().GetAsync(id, ct);

        /// <summary>
        /// Finds an entity by primary key value(s).
        /// </summary>
        public T Find<T>(params object[] keyValues) where T : class => GetTable<T>().Find(keyValues);

        /// <summary>
        /// Asynchronously finds an entity by primary key value(s).
        /// </summary>
        public Task<T> FindAsync<T>(CancellationToken ct = default, params object[] keyValues) where T : class
            => GetTable<T>().FindAsync(ct, keyValues);

        /// <summary>
        /// Modern, ergonomic alias for SubmitChanges().
        /// Commits all pending inserts, updates, and deletes to the database.
        /// </summary>
        public void Save() => SubmitChanges();

        /// <summary>
        /// Modern, ergonomic async alias for SubmitChangesAsync().
        /// </summary>
        public Task SaveAsync(CancellationToken ct = default) => SubmitChangesAsync(ct);

        /// <summary>
        /// SaveChanges alias (EF Core style).
        /// </summary>
        public void SaveChanges() => Save();

        /// <summary>
        /// SaveChangesAsync alias (EF Core style).
        /// </summary>
        public Task SaveChangesAsync(CancellationToken ct = default) => SaveAsync(ct);

        /// <summary>
        /// Queries entities without change tracking (pure read-only).
        /// Low memory allocation, optimal for queries and APIs.
        /// </summary>
        public List<T> Query<T>(System.Linq.Expressions.Expression<Func<T, bool>> predicate = null) where T : class
        {
            var table = GetTable<T>().AsNoTracking();
            return predicate != null ? table.Where(predicate) : table.ToList();
        }

        /// <summary>
        /// Asynchronously queries entities without change tracking (pure read-only).
        /// </summary>
        public Task<List<T>> QueryAsync<T>(System.Linq.Expressions.Expression<Func<T, bool>> predicate = null, CancellationToken ct = default) where T : class
        {
            var table = GetTable<T>().AsNoTracking();
            return predicate != null ? table.WhereAsync(predicate, ct) : table.ToListAsync(ct);
        }

        /// <summary>
        /// Executes a SQL query with named parameters and maps rows to T using native ADO.NET engine.
        /// </summary>
        public List<T> QuerySql<T>(string sql, object parameters = null) => FromSql<T>(sql, parameters);

        /// <summary>
        /// Asynchronously executes a SQL query with named parameters and maps rows to T.
        /// </summary>
        public Task<List<T>> QuerySqlAsync<T>(string sql, object parameters = null, CancellationToken ct = default)
            => FromSqlAsync<T>(sql, parameters, ct);

        /// <summary>
        /// Constant-time single-column Keyset (Seek) pagination.
        /// </summary>
        public List<T> Seek<T, TKey>(Expression<Func<T, TKey>> keySelector, TKey lastSeenKey, int pageSize, bool ascending = true) where T : class
            => GetTable<T>().Seek(keySelector, lastSeenKey, pageSize, ascending);

        /// <summary>
        /// Asynchronously executes constant-time single-column Keyset (Seek) pagination.
        /// </summary>
        public Task<List<T>> SeekAsync<T, TKey>(Expression<Func<T, TKey>> keySelector, TKey lastSeenKey, int pageSize, bool ascending = true, CancellationToken ct = default) where T : class
            => GetTable<T>().SeekAsync(keySelector, lastSeenKey, pageSize, ascending, ct);

        /// <summary>
        /// Two-column composite keyset (seek) pagination.
        /// </summary>
        public List<T> Seek<T, TKey1, TKey2>(
            Expression<Func<T, TKey1>> key1, TKey1 lastSeen1, bool asc1,
            Expression<Func<T, TKey2>> key2, TKey2 lastSeen2, bool asc2,
            int pageSize) where T : class
            => GetTable<T>().Seek(key1, lastSeen1, asc1, key2, lastSeen2, asc2, pageSize);

        /// <summary>
        /// Asynchronously executes two-column composite keyset (seek) pagination.
        /// </summary>
        public Task<List<T>> SeekAsync<T, TKey1, TKey2>(
            Expression<Func<T, TKey1>> key1, TKey1 lastSeen1, bool asc1,
            Expression<Func<T, TKey2>> key2, TKey2 lastSeen2, bool asc2,
            int pageSize, CancellationToken ct = default) where T : class
            => GetTable<T>().SeekAsync(key1, lastSeen1, asc1, key2, lastSeen2, asc2, pageSize, ct);

        /// <summary>
        /// Three-column composite keyset (seek) pagination.
        /// </summary>
        public List<T> Seek<T, TKey1, TKey2, TKey3>(
            Expression<Func<T, TKey1>> key1, TKey1 lastSeen1, bool asc1,
            Expression<Func<T, TKey2>> key2, TKey2 lastSeen2, bool asc2,
            Expression<Func<T, TKey3>> key3, TKey3 lastSeen3, bool asc3,
            int pageSize) where T : class
            => GetTable<T>().Seek(key1, lastSeen1, asc1, key2, lastSeen2, asc2, key3, lastSeen3, asc3, pageSize);

        /// <summary>
        /// Asynchronously executes three-column composite keyset (seek) pagination.
        /// </summary>
        public Task<List<T>> SeekAsync<T, TKey1, TKey2, TKey3>(
            Expression<Func<T, TKey1>> key1, TKey1 lastSeen1, bool asc1,
            Expression<Func<T, TKey2>> key2, TKey2 lastSeen2, bool asc2,
            Expression<Func<T, TKey3>> key3, TKey3 lastSeen3, bool asc3,
            int pageSize, CancellationToken ct = default) where T : class
            => GetTable<T>().SeekAsync(key1, lastSeen1, asc1, key2, lastSeen2, asc2, key3, lastSeen3, asc3, pageSize, ct);

        /// <summary>
        /// Composite keyset (seek) pagination across multiple indexed columns.
        /// </summary>
        public List<T> Seek<T>(IReadOnlyList<SeekColumn> seekColumns, int pageSize) where T : class
            => GetTable<T>().Seek(seekColumns, pageSize);

        /// <summary>
        /// Asynchronously executes composite keyset (seek) pagination across multiple indexed columns.
        /// </summary>
        public Task<List<T>> SeekAsync<T>(IReadOnlyList<SeekColumn> seekColumns, int pageSize, CancellationToken ct = default) where T : class
            => GetTable<T>().SeekAsync(seekColumns, pageSize, ct);

        /// <summary>
        /// Executes a raw SQL command (UPDATE, INSERT, DELETE) with named parameters.
        /// </summary>
        public int ExecuteSql(string sql, object parameters = null)
        {
            ThrowIfDisposed();
            EnsureConnectionOpen();
            return Connection.Execute(sql, parameters, transaction: Transaction, commandTimeout: CommandTimeout);
        }

        /// <summary>
        /// Asynchronously executes a raw SQL command with named parameters.
        /// </summary>
        public async Task<int> ExecuteSqlAsync(string sql, object parameters = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            return await Connection.ExecuteAsync(new CommandDefinition(sql, parameters,
                transaction: Transaction, commandTimeout: CommandTimeout, cancellationToken: ct)).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a SQL query and returns a zero-allocation columnar DataFrame without instantiating POCO entities.
        /// </summary>
        public DataFrame QueryDataFrame(string sql, object parameters = null, int maxRows = -1)
        {
            ThrowIfDisposed();
            EnsureConnectionOpen();
            return Connection.QueryDataFrame(sql, parameters, Transaction, CommandTimeout, maxRows: maxRows);
        }

        /// <summary>
        /// Asynchronously executes a SQL query and returns a zero-allocation columnar DataFrame without instantiating POCO entities.
        /// </summary>
        public async Task<DataFrame> QueryDataFrameAsync(string sql, object parameters = null, int maxRows = -1, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            return await Connection.QueryDataFrameAsync(sql, parameters, Transaction, CommandTimeout, maxRows: maxRows, cancellationToken: ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Bulk inserts data from a columnar DataFrame directly into the specified table without creating POCO entities.
        /// </summary>
        public int BulkInsert(DataFrame dataFrame, string tableName)
        {
            ThrowIfDisposed();
            EnsureConnectionOpen();
            return BulkOperations.BulkInsert(Connection, dataFrame, tableName, Transaction, Dialect);
        }

        /// <summary>
        /// Asynchronously bulk inserts data from a columnar DataFrame directly into the specified table without creating POCO entities.
        /// </summary>
        public async Task<int> BulkInsertAsync(DataFrame dataFrame, string tableName, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            return await BulkOperations.BulkInsertAsync(Connection, dataFrame, tableName, Transaction, Dialect, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Bulk inserts entities using high-speed batched multi-row INSERT statements.
        /// </summary>
        public int BulkInsert<T>(IEnumerable<T> entities) where T : class
        {
            var res = BulkOperations.BulkInsert(Connection, entities, Transaction, Dialect);
            QueryCache?.Invalidate(typeof(T));
            return res;
        }

        /// <summary>
        /// Bulk updates entities using batched UPDATE statements.
        /// </summary>
        public int BulkUpdate<T>(IEnumerable<T> entities) where T : class
        {
            var res = BulkOperations.BulkUpdate(Connection, entities, Transaction, Dialect);
            QueryCache?.Invalidate(typeof(T));
            return res;
        }

        /// <summary>
        /// Bulk deletes entities using batched DELETE statements.
        /// </summary>
        public int BulkDelete<T>(IEnumerable<T> entities) where T : class
        {
            var res = BulkOperations.BulkDelete(Connection, entities, Transaction, Dialect);
            QueryCache?.Invalidate(typeof(T));
            return res;
        }

        /// <summary>
        /// Asynchronously bulk inserts entities using SqlBulkCopy (SQL Server) or batched multi-row INSERT with compiled getters.
        /// </summary>
        public async Task<int> BulkInsertAsync<T>(IEnumerable<T> entities, CancellationToken ct = default) where T : class
        {
            ThrowIfDisposed();
            await EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var res = await BulkOperations.BulkInsertAsync(Connection, entities, Transaction, Dialect, ct).ConfigureAwait(false);
            QueryCache?.Invalidate(typeof(T));
            return res;
        }

        /// <summary>
        /// Asynchronously bulk updates entities using batched UPDATE statements.
        /// </summary>
        public async Task<int> BulkUpdateAsync<T>(IEnumerable<T> entities, CancellationToken ct = default) where T : class
        {
            ThrowIfDisposed();
            await EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var res = await BulkOperations.BulkUpdateAsync(Connection, entities, Transaction, Dialect, ct).ConfigureAwait(false);
            QueryCache?.Invalidate(typeof(T));
            return res;
        }

        /// <summary>
        /// Asynchronously bulk deletes entities using batched DELETE statements.
        /// </summary>
        public async Task<int> BulkDeleteAsync<T>(IEnumerable<T> entities, CancellationToken ct = default) where T : class
        {
            ThrowIfDisposed();
            await EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var res = await BulkOperations.BulkDeleteAsync(Connection, entities, Transaction, Dialect, ct).ConfigureAwait(false);
            QueryCache?.Invalidate(typeof(T));
            return res;
        }

        /// <summary>
        /// Bulk merges (upserts) entities: inserts new records or updates existing records matched by primary key.
        /// </summary>
        public int BulkMerge<T>(IEnumerable<T> entities) where T : class
        {
            ThrowIfDisposed();
            EnsureConnectionOpen();
            var res = BulkOperations.BulkMerge(Connection, entities, Transaction, Dialect);
            QueryCache?.Invalidate(typeof(T));
            return res;
        }

        /// <summary>
        /// Asynchronously bulk merges (upserts) entities: inserts new records or updates existing records matched by primary key.
        /// </summary>
        public async Task<int> BulkMergeAsync<T>(IEnumerable<T> entities, CancellationToken ct = default) where T : class
        {
            ThrowIfDisposed();
            await EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            var res = await BulkOperations.BulkMergeAsync(Connection, entities, Transaction, Dialect, ct).ConfigureAwait(false);
            QueryCache?.Invalidate(typeof(T));
            return res;
        }

        #endregion

        #region Cache & Resilience Helpers

        /// <summary>
        /// Manually invalidates cached queries for entity type T.
        /// </summary>
        public void InvalidateCache<T>() where T : class => QueryCache?.Invalidate(typeof(T));

        /// <summary>
        /// Manually invalidates cached queries for the specified entity type.
        /// </summary>
        public void InvalidateCache(Type entityType) => QueryCache?.Invalidate(entityType);

        /// <summary>
        /// Manually invalidates all entries in the query cache.
        /// </summary>
        public void InvalidateAllCache() => QueryCache?.InvalidateAll();

        /// <summary>
        /// Executes an operation with transient fault retries using the context's RetryPolicy.
        /// </summary>
        public T ExecuteWithRetry<T>(Func<T> operation)
        {
            var policy = RetryPolicy ?? RetryPolicy.Default;
            return policy.Execute(operation);
        }

        /// <summary>
        /// Asynchronously executes an operation with transient fault retries using the context's RetryPolicy.
        /// </summary>
        public Task<T> ExecuteWithRetryAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
        {
            var policy = RetryPolicy ?? RetryPolicy.Default;
            return policy.ExecuteAsync(operation, ct);
        }

        #endregion

        #region Transaction Helpers (Phase 9)

        /// <summary>
        /// Executes an action within an auto-managed transaction.
        /// Commits on success, rolls back on exception.
        /// </summary>
        public void ExecuteInTransaction(Action<SqlContext> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            ThrowIfDisposed();
            EnsureConnectionOpen();
            using (var tx = Connection.BeginTransaction())
            {
                Transaction = tx;
                try
                {
                    action(this);
                    tx.Commit();
                }
                catch { tx.Rollback(); throw; }
                finally { Transaction = null; }
            }
        }

        /// <summary>
        /// Executes an async action within an auto-managed transaction.
        /// Commits on success, rolls back on exception.
        /// </summary>
        public async Task ExecuteInTransactionAsync(Func<SqlContext, Task> action, CancellationToken ct = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            ThrowIfDisposed();
            await EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            using (var tx = Connection.BeginTransaction())
            {
                Transaction = tx;
                try
                {
                    await action(this).ConfigureAwait(false);
                    tx.Commit();
                }
                catch { tx.Rollback(); throw; }
                finally { Transaction = null; }
            }
        }

        #endregion

        #region InsertAndGetId (Phase 8.2)

        /// <summary>
        /// Inserts an entity and immediately returns the generated identity value.
        /// Does not go through SubmitChanges — executes immediately.
        /// </summary>
        public long InsertAndGetId<T>(T entity) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            ThrowIfDisposed();
            EnsureConnectionOpen();

            var mapping = MappingCache.GetMapping<T>();
            var (sql, parameters) = SqlGenerator.GenerateInsert(mapping, entity, _dialect);
            LogSql(sql, parameters);
            Connection.Execute(sql, (object)ToDynamicParameters(parameters),
                transaction: Transaction, commandTimeout: CommandTimeout);

            var pk = mapping.PrimaryKeys.FirstOrDefault(p => p.IsDbGenerated);
            var id = Connection.ExecuteScalar<long>(_dialect.GetLastInsertIdSql(mapping.TableName, pk?.ColumnName),
                transaction: Transaction);
            SetPkValue(pk, entity, id);
            return id;
        }

        /// <summary>
        /// Inserts an entity and immediately returns the generated identity value (async).
        /// </summary>
        public async Task<long> InsertAndGetIdAsync<T>(T entity, CancellationToken ct = default) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            ThrowIfDisposed();
            await EnsureConnectionOpenAsync(ct).ConfigureAwait(false);

            var mapping = MappingCache.GetMapping<T>();
            var (sql, parameters) = SqlGenerator.GenerateInsert(mapping, entity, _dialect);
            LogSql(sql, parameters);
            await Connection.ExecuteAsync(new CommandDefinition(
                sql, (object)ToDynamicParameters(parameters),
                transaction: Transaction, commandTimeout: CommandTimeout,
                cancellationToken: ct)).ConfigureAwait(false);

            var pk = mapping.PrimaryKeys.FirstOrDefault(p => p.IsDbGenerated);
            var id = await Connection.ExecuteScalarAsync<long>(
                new CommandDefinition(_dialect.GetLastInsertIdSql(mapping.TableName, pk?.ColumnName),
                    transaction: Transaction, cancellationToken: ct)).ConfigureAwait(false);
            SetPkValue(pk, entity, id);
            return id;
        }

        #endregion

        #region BulkInsert (Phase 8.1 - Delegated to BulkOperations)

        /// <summary>
        /// Inserts multiple entities directly using high-speed bulk insertion.
        /// </summary>
        public void BulkInsert<T>(IEnumerable<T> entities, int batchSize) where T : class
        {
            ThrowIfDisposed();
            EnsureConnectionOpen();
            BulkOperations.BulkInsert(Connection, entities, Transaction, _dialect);
        }

        /// <summary>
        /// Asynchronously inserts multiple entities using high-speed bulk insertion.
        /// </summary>
        public async Task BulkInsertAsync<T>(IEnumerable<T> entities, int batchSize,
            CancellationToken ct = default) where T : class
        {
            ThrowIfDisposed();
            await EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            await BulkOperations.BulkInsertAsync(Connection, entities, Transaction, _dialect, ct).ConfigureAwait(false);
        }

        #endregion

        /// <summary>
        /// Inserts or updates an entity based on primary key existence.
        /// SQLite: INSERT OR REPLACE. SQL Server: MERGE.
        /// </summary>
        public void Upsert<T>(T entity) where T : class
        {
            ThrowIfDisposed();
            var mapping = MappingCache.GetMapping<T>();
            var sql = SqlGenerator.GenerateUpsert(mapping, entity, _dialect);
            if (sql == null) throw new InvalidOperationException("Upsert requires at least one primary key column.");

            EnsureConnectionOpen();
            Connection.Execute(sql.Value.sql, (object)ToDynamicParameters(sql.Value.parameters),
                transaction: Transaction, commandTimeout: CommandTimeout);
        }

        /// <summary>
        /// Async version of Upsert.
        /// </summary>
        public async Task UpsertAsync<T>(T entity, CancellationToken ct = default) where T : class
        {
            ThrowIfDisposed();
            var mapping = MappingCache.GetMapping<T>();
            var sql = SqlGenerator.GenerateUpsert(mapping, entity, _dialect);
            if (sql == null) throw new InvalidOperationException("Upsert requires at least one primary key column.");

            await EnsureConnectionOpenAsync(ct).ConfigureAwait(false);
            await Connection.ExecuteAsync(new CommandDefinition(
                sql.Value.sql, ToDynamicParameters(sql.Value.parameters),
                transaction: Transaction, commandTimeout: CommandTimeout,
                cancellationToken: ct)).ConfigureAwait(false);
        }

        private static IEnumerable<List<T>> Batch<T>(IEnumerable<T> source, int size)
        {
            var batch = new List<T>(size);
            foreach (var item in source)
            {
                batch.Add(item);
                if (batch.Count >= size)
                {
                    yield return batch;
                    batch = new List<T>(size);
                }
            }
            if (batch.Count > 0) yield return batch;
        }

        #region Graph Insert (parent + child collections)

        /// <summary>
        /// Inserts an entity together with its one-to-many child collections in a single
        /// transaction. The parent is inserted first; its generated key is propagated to each
        /// child's foreign-key property; then children are inserted (recursively for nested graphs).
        /// </summary>
        public void InsertGraph<T>(T entity) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            ThrowIfDisposed();
            EnsureConnectionOpen();

            var ownTx = Transaction == null;
            var tx = Transaction ?? Connection.BeginTransaction();
            var prevTx = Transaction;
            try
            {
                Transaction = tx;
                InsertGraphNode(entity, entity.GetType());
                if (ownTx) tx.Commit();
            }
            catch { if (ownTx) tx.Rollback(); throw; }
            finally
            {
                Transaction = prevTx;
                if (ownTx) tx.Dispose();
            }
        }

        /// <summary>
        /// Async version of InsertGraph.
        /// </summary>
        public async Task InsertGraphAsync<T>(T entity, CancellationToken ct = default) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            ThrowIfDisposed();
            await EnsureConnectionOpenAsync(ct).ConfigureAwait(false);

            var ownTx = Transaction == null;
            var tx = Transaction ?? Connection.BeginTransaction();
            var prevTx = Transaction;
            try
            {
                Transaction = tx;
                await InsertGraphNodeAsync(entity, entity.GetType(), ct).ConfigureAwait(false);
                if (ownTx) tx.Commit();
            }
            catch { if (ownTx) tx.Rollback(); throw; }
            finally
            {
                Transaction = prevTx;
                if (ownTx) tx.Dispose();
            }
        }

        private void InsertGraphNode(object entity, Type entityType)
        {
            var mapping = MappingCache.GetMapping(entityType);

            // Insert the parent and capture its generated key (if any).
            var (sql, parameters) = SqlGenerator.GenerateInsert(mapping, entity, _dialect);
            LogSql(sql, parameters);
            Connection.Execute(sql, (object)ToDynamicParameters(parameters),
                transaction: Transaction, commandTimeout: CommandTimeout);

            var pk = mapping.PrimaryKeys.FirstOrDefault(p => p.IsDbGenerated);
            object pkValue;
            if (pk != null)
            {
                var id = Connection.ExecuteScalar<long>(
                    _dialect.GetLastInsertIdSql(mapping.TableName, pk.ColumnName), transaction: Transaction);
                SetPkValue(pk, entity, id);
                pkValue = pk.Property.GetValue(entity);
            }
            else
            {
                pkValue = mapping.PrimaryKeys.FirstOrDefault()?.Property.GetValue(entity);
            }

            InsertChildCollections(entity, mapping, pkValue);
        }

        private async Task InsertGraphNodeAsync(object entity, Type entityType, CancellationToken ct)
        {
            var mapping = MappingCache.GetMapping(entityType);

            var (sql, parameters) = SqlGenerator.GenerateInsert(mapping, entity, _dialect);
            LogSql(sql, parameters);
            await Connection.ExecuteAsync(new CommandDefinition(
                sql, (object)ToDynamicParameters(parameters),
                transaction: Transaction, commandTimeout: CommandTimeout, cancellationToken: ct)).ConfigureAwait(false);

            var pk = mapping.PrimaryKeys.FirstOrDefault(p => p.IsDbGenerated);
            object pkValue;
            if (pk != null)
            {
                var id = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
                    _dialect.GetLastInsertIdSql(mapping.TableName, pk.ColumnName),
                    transaction: Transaction, cancellationToken: ct)).ConfigureAwait(false);
                SetPkValue(pk, entity, id);
                pkValue = pk.Property.GetValue(entity);
            }
            else
            {
                pkValue = mapping.PrimaryKeys.FirstOrDefault()?.Property.GetValue(entity);
            }

            await InsertChildCollectionsAsync(entity, mapping, pkValue, ct).ConfigureAwait(false);
        }

        private void InsertChildCollections(object parent, EntityMapping mapping, object parentKey)
        {
            if (mapping.CollectionAssociations == null) return;

            foreach (var assoc in mapping.CollectionAssociations)
            {
                var collection = assoc.Property.GetValue(parent) as System.Collections.IEnumerable;
                if (collection == null) continue;

                var childType = assoc.OtherType;
                var childMapping = MappingCache.GetMapping(childType);
                // FK property on the child that references the parent's key.
                var fkProp = childType.GetProperty(assoc.OtherKey);

                foreach (var child in collection)
                {
                    if (child == null) continue;
                    if (fkProp != null && parentKey != null)
                        fkProp.SetValue(child, ConvertTo(parentKey, fkProp.PropertyType));
                    // Recurse so grandchildren are handled too.
                    InsertGraphNode(child, childType);
                }
            }
        }

        private async Task InsertChildCollectionsAsync(object parent, EntityMapping mapping, object parentKey, CancellationToken ct)
        {
            if (mapping.CollectionAssociations == null) return;

            foreach (var assoc in mapping.CollectionAssociations)
            {
                var collection = assoc.Property.GetValue(parent) as System.Collections.IEnumerable;
                if (collection == null) continue;

                var childType = assoc.OtherType;
                var fkProp = childType.GetProperty(assoc.OtherKey);

                foreach (var child in collection)
                {
                    if (child == null) continue;
                    if (fkProp != null && parentKey != null)
                        fkProp.SetValue(child, ConvertTo(parentKey, fkProp.PropertyType));
                    await InsertGraphNodeAsync(child, childType, ct).ConfigureAwait(false);
                }
            }
        }

        private static object ConvertTo(object value, Type targetType)
        {
            if (value == null) return null;
            var t = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (t.IsInstanceOfType(value)) return value;
            return Convert.ChangeType(value, t);
        }

        #endregion

        #region Change Processing

        private void ProcessChanges(IReadOnlyList<TrackedEntity> changes, IDbTransaction tx)
        {
            foreach (var tracked in changes)
            {
                var mapping = MappingCache.GetMapping(tracked.EntityType);
                Hooks.InvokeBeforeSave(tracked.Entity, tracked.EntityType, tracked.State);

                switch (tracked.State)
                {
                    case EntityState.Insert:
                        ExecuteCrud(mapping, tracked.Entity, tx, (m, e) => SqlGenerator.GenerateInsert(m, e, _dialect));
                        SetAutoGeneratedId(mapping, tracked.Entity, tx);
                        break;
                    case EntityState.Update:
                        if (tracked.ChangedProperties != null)
                        {
                            var (sql, parameters) = SqlGenerator.GeneratePartialUpdate(
                                mapping, tracked.Entity, tracked.ChangedProperties, _dialect);
                            if (sql != null)
                            {
                                LogSql(sql, parameters);
                                var affected = Connection.Execute(sql, (object)ToDynamicParameters(parameters),
                                    transaction: tx, commandTimeout: CommandTimeout);
                                CheckConcurrency(mapping, tracked.Entity, affected);
                            }
                        }
                        else
                        {
                            var affected = ExecuteCrud(mapping, tracked.Entity, tx, (m, e) => SqlGenerator.GenerateUpdate(m, e, _dialect));
                            CheckConcurrency(mapping, tracked.Entity, affected);
                        }
                        break;
                    case EntityState.Delete:
                        ExecuteCrud(mapping, tracked.Entity, tx, (m, e) => SqlGenerator.GenerateDelete(m, e, _dialect));
                        break;
                }

                Hooks.InvokeAfterSave(tracked.Entity, tracked.EntityType, tracked.State);
            }
        }

        private async Task ProcessChangesAsync(IReadOnlyList<TrackedEntity> changes, IDbTransaction tx, CancellationToken ct)
        {
            foreach (var tracked in changes)
            {
                var mapping = MappingCache.GetMapping(tracked.EntityType);
                Hooks.InvokeBeforeSave(tracked.Entity, tracked.EntityType, tracked.State);

                switch (tracked.State)
                {
                    case EntityState.Insert:
                        await ExecuteCrudAsync(mapping, tracked.Entity, tx, (m, e) => SqlGenerator.GenerateInsert(m, e, _dialect), ct).ConfigureAwait(false);
                        await SetAutoGeneratedIdAsync(mapping, tracked.Entity, tx, ct).ConfigureAwait(false);
                        break;
                    case EntityState.Update:
                        if (tracked.ChangedProperties != null)
                        {
                            var (sql, parameters) = SqlGenerator.GeneratePartialUpdate(
                                mapping, tracked.Entity, tracked.ChangedProperties, _dialect);
                            if (sql != null)
                            {
                                LogSql(sql, parameters);
                                var affected = await Connection.ExecuteAsync(new CommandDefinition(
                                    sql, (object)ToDynamicParameters(parameters),
                                    transaction: tx, commandTimeout: CommandTimeout,
                                    cancellationToken: ct)).ConfigureAwait(false);
                                CheckConcurrency(mapping, tracked.Entity, affected);
                            }
                        }
                        else
                        {
                            var affected = await ExecuteCrudAsync(mapping, tracked.Entity, tx, (m, e) => SqlGenerator.GenerateUpdate(m, e, _dialect), ct).ConfigureAwait(false);
                            CheckConcurrency(mapping, tracked.Entity, affected);
                        }
                        break;
                    case EntityState.Delete:
                        await ExecuteCrudAsync(mapping, tracked.Entity, tx, (m, e) => SqlGenerator.GenerateDelete(m, e, _dialect), ct).ConfigureAwait(false);
                        break;
                }

                Hooks.InvokeAfterSave(tracked.Entity, tracked.EntityType, tracked.State);
            }
        }

        private int ExecuteCrud(EntityMapping mapping, object entity, IDbTransaction tx,
            Func<EntityMapping, object, (string, IDictionary<string, object>)> generator)
        {
            var (sql, parameters) = generator(mapping, entity);
            ApplyConverters(parameters, mapping);
            LogSql(sql, parameters);
            return Connection.Execute(sql, (object)ToDynamicParameters(parameters),
                transaction: tx, commandTimeout: CommandTimeout);
        }

        private async Task<int> ExecuteCrudAsync(EntityMapping mapping, object entity, IDbTransaction tx,
            Func<EntityMapping, object, (string, IDictionary<string, object>)> generator, CancellationToken ct)
        {
            var (sql, parameters) = generator(mapping, entity);
            ApplyConverters(parameters, mapping);
            LogSql(sql, parameters);
            return await Connection.ExecuteAsync(new CommandDefinition(
                sql, (object)ToDynamicParameters(parameters),
                transaction: tx, commandTimeout: CommandTimeout, cancellationToken: ct)).ConfigureAwait(false);
        }

        /// <summary>
        /// Throws <see cref="ConcurrencyException"/> if a versioned UPDATE affected no rows,
        /// indicating the row was changed or deleted by another process since it was loaded.
        /// </summary>
        private static void CheckConcurrency(EntityMapping mapping, object entity, int affectedRows)
        {
            if (affectedRows == 0 && mapping.VersionColumns != null && mapping.VersionColumns.Count > 0)
                throw new ConcurrencyException(entity, mapping.EntityType);
        }

        #endregion

        #region Identity

        private void SetAutoGeneratedId(EntityMapping mapping, object entity, IDbTransaction tx)
        {
            var pk = mapping.PrimaryKeys.FirstOrDefault(p => p.IsDbGenerated);
            if (pk == null) return;
            var id = Connection.ExecuteScalar<long>(_dialect.GetLastInsertIdSql(mapping.TableName, pk.ColumnName), transaction: tx);
            SetPkValue(pk, entity, id);
        }

        private async Task SetAutoGeneratedIdAsync(EntityMapping mapping, object entity, IDbTransaction tx, CancellationToken ct)
        {
            var pk = mapping.PrimaryKeys.FirstOrDefault(p => p.IsDbGenerated);
            if (pk == null) return;
            var id = await Connection.ExecuteScalarAsync<long>(
                new CommandDefinition(_dialect.GetLastInsertIdSql(mapping.TableName, pk.ColumnName), transaction: tx, cancellationToken: ct)).ConfigureAwait(false);
            SetPkValue(pk, entity, id);
        }

        private static void SetPkValue(ColumnMapping pk, object entity, long id)
        {
            if (id <= 0 || pk == null) return;
            var t = Nullable.GetUnderlyingType(pk.Property.PropertyType) ?? pk.Property.PropertyType;
            var converted = Convert.ChangeType(id, t);
            if (pk.Setter != null)
                pk.Setter(entity, converted);
            else
                pk.Property.SetValue(entity, converted);
        }

        #endregion

        #region Utilities

        private void DetectAllChanges()
        {
            var updates = _changeTracker.DetectAllChanges(type => MappingCache.GetMapping(type));
            if (updates.Count > 0) _changeTracker.AddUpdates(updates);
        }

        private (string sql, DynamicParameters dp) ConvertParams(string query, object[] parameters)
        {
            var (sql, dict) = SqlGenerator.ConvertPositionalParameters(query, parameters);
            LogSql(sql, dict);
            return (sql, ToDynamicParameters(dict));
        }

        internal DynamicParameters ToDynamicParameters(IDictionary<string, object> parameters)
        {
            var dp = new DynamicParameters();
            if (parameters != null)
                foreach (var kv in parameters) dp.Add(kv.Key, kv.Value);
            return dp;
        }

        /// <summary>
        /// Applies registered value converters to parameter values.
        /// Converts model types to database types (e.g., Enum → string).
        /// </summary>
        private void ApplyConverters(IDictionary<string, object> parameters, EntityMapping mapping)
        {
            if (parameters == null || !Converters.HasAny) return;

            var keys = new List<string>(parameters.Keys);
            foreach (var key in keys)
            {
                var value = parameters[key];
                if (value == null) continue;

                var valueType = value.GetType();
                if (Converters.HasConverter(valueType))
                {
                    parameters[key] = Converters.ConvertToDb(value, valueType);
                }
            }
        }

        internal void EnsureConnectionOpen()
        {
            if (Connection.State != ConnectionState.Open) Connection.Open();
        }

        internal async Task EnsureConnectionOpenAsync(CancellationToken ct = default)
        {
            if (Connection.State != ConnectionState.Open)
            {
                if (Connection is DbConnection dbConn)
                    await dbConn.OpenAsync(ct).ConfigureAwait(false);
                else
                    Connection.Open();
            }
        }

        private void LogSql(string sql, IDictionary<string, object> parameters)
        {
            if (Log == null) return;
            Log.WriteLine(sql);
            if (parameters?.Count > 0)
                foreach (var kv in parameters) Log.WriteLine($"  {kv.Key} = {kv.Value}");
            Log.WriteLine();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name, "DataContext accessed after Dispose.");
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Transaction?.Dispose();
            if (_ownsConnection) Connection?.Dispose();
        }

        #endregion
    }
}
