using System;
using System.Collections.Concurrent;
using System.Data;

namespace ZeroData.Sql.Dialects
{
    /// <summary>
    /// High-performance, extensible factory for creating and caching SQL dialect instances.
    /// Uses ConcurrentDictionary type-caching for O(1) zero-allocation lookup during execution.
    /// Supports custom dialect registration for any database provider.
    /// </summary>
    public static class SqlDialectFactory
    {
        private static readonly ConcurrentDictionary<Type, ISqlDialect> TypeCache
            = new ConcurrentDictionary<Type, ISqlDialect>();

        private static readonly ConcurrentDictionary<string, Func<ISqlDialect>> CustomDialects
            = new ConcurrentDictionary<string, Func<ISqlDialect>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers a custom dialect factory for a specific provider name or connection type pattern.
        /// </summary>
        public static void RegisterDialect(string providerPattern, Func<ISqlDialect> factory)
        {
            if (string.IsNullOrEmpty(providerPattern))
                throw new ArgumentNullException(nameof(providerPattern));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            CustomDialects[providerPattern] = factory;
            TypeCache.Clear(); // Invalidate cached type resolutions
        }

        /// <summary>
        /// Registers a specific dialect instance for an IDbConnection type.
        /// </summary>
        public static void RegisterDialect<TConnection>(ISqlDialect dialect) where TConnection : IDbConnection
        {
            if (dialect == null)
                throw new ArgumentNullException(nameof(dialect));

            TypeCache[typeof(TConnection)] = dialect;
        }

        /// <summary>
        /// Creates or retrieves a cached dialect instance based on the connection type.
        /// Uses fast O(1) concurrent caching to avoid repeated reflection and string allocations.
        /// </summary>
        public static ISqlDialect GetDialect(IDbConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            return TypeCache.GetOrAdd(connection.GetType(), ResolveDialectForType);
        }

        private static ISqlDialect ResolveDialectForType(Type connectionType)
        {
            var typeName = connectionType.Name.ToLowerInvariant();
            var fullName = (connectionType.FullName ?? string.Empty).ToLowerInvariant();

            // Check custom dialect registrations first
            foreach (var kvp in CustomDialects)
            {
                var pattern = kvp.Key.ToLowerInvariant();
                if (typeName.Contains(pattern) || fullName.Contains(pattern))
                {
                    return kvp.Value();
                }
            }

            // Built-in detection:
            // 1. SQL Server
            if (typeName.Contains("sqlconnection") || typeName.Contains("sqlclient"))
                return new SqlServerDialect();

            // 2. SQLite
            if (typeName.Contains("sqliteconnection") || typeName.Contains("sqlite"))
                return new SqliteDialect();

            // 3. PostgreSQL / CockroachDB
            if (typeName.Contains("npgsqlconnection") || typeName.Contains("postgres") || typeName.Contains("cockroach"))
                return new PostgreSqlDialect();

            // 4. MySQL / MariaDB
            if (typeName.Contains("mysqlconnection") || typeName.Contains("mysql") || typeName.Contains("mariadb"))
                return new MySqlDialect();

            // 5. Oracle
            if (typeName.Contains("oracleconnection") || typeName.Contains("oracle") || typeName.Contains("odp"))
                return new OracleDialect();

            // 6. Firebird
            if (typeName.Contains("fbconnection") || typeName.Contains("firebird"))
                return new FirebirdDialect();

            // Universal fallback: ANSI SQL
            return new AnsiSqlDialect();
        }

        /// <summary>
        /// Creates a dialect instance based on provider name.
        /// </summary>
        public static ISqlDialect GetDialect(string providerName)
        {
            if (string.IsNullOrEmpty(providerName))
                throw new ArgumentNullException(nameof(providerName));

            // Check custom dialect registrations
            if (CustomDialects.TryGetValue(providerName, out var factory))
                return factory();

            var provider = providerName.ToLowerInvariant();

            if (provider.Contains("sqlserver") || provider.Contains("mssql") || provider.Contains("system.data.sqlclient") || provider.Contains("microsoft.data.sqlclient"))
                return new SqlServerDialect();

            if (provider.Contains("sqlite") || provider.Contains("system.data.sqlite") || provider.Contains("microsoft.data.sqlite"))
                return new SqliteDialect();

            if (provider.Contains("postgres") || provider.Contains("npgsql") || provider.Contains("cockroach"))
                return new PostgreSqlDialect();

            if (provider.Contains("mysql") || provider.Contains("mariadb") || provider.Contains("mysqlconnector"))
                return new MySqlDialect();

            if (provider.Contains("oracle") || provider.Contains("odp"))
                return new OracleDialect();

            if (provider.Contains("firebird") || provider.Contains("firebirdsql"))
                return new FirebirdDialect();

            if (provider.Contains("ansi") || provider.Contains("duckdb") || provider.Contains("odbc") || provider.Contains("oledb"))
                return new AnsiSqlDialect();

            // Fallback to ANSI SQL rather than throwing
            return new AnsiSqlDialect();
        }

        /// <summary>
        /// Gets all standard built-in dialects.
        /// </summary>
        public static ISqlDialect[] GetAllDialects()
        {
            return new ISqlDialect[]
            {
                new SqlServerDialect(),
                new SqliteDialect(),
                new PostgreSqlDialect(),
                new MySqlDialect(),
                new OracleDialect(),
                new FirebirdDialect(),
                new AnsiSqlDialect()
            };
        }
    }
}
