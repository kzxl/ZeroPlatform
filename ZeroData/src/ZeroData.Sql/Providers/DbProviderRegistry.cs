using System;
using System.Collections.Concurrent;
using System.Data;

namespace ZeroData.Sql.Providers
{
    /// <summary>
    /// Thread-safe registry for database connection factories by provider name.
    /// Allows applications to seamlessly use multiple database types (e.g. SQLite + SQL Server + PostgreSQL)
    /// without relying on a single static connection factory.
    /// </summary>
    public static class DbProviderRegistry
    {
        public const string SqlServer = "SqlServer";
        public const string Sqlite = "Sqlite";
        public const string PostgreSql = "PostgreSql";
        public const string MySql = "MySql";
        public const string Oracle = "Oracle";
        public const string Firebird = "Firebird";
        public const string Ansi = "ANSI";

        private static readonly ConcurrentDictionary<string, Func<string, IDbConnection>> Factories
            = new ConcurrentDictionary<string, Func<string, IDbConnection>>(StringComparer.OrdinalIgnoreCase);

        static DbProviderRegistry()
        {
            // Register common aliases that redirect to standard names
            RegisterAlias("Microsoft.Data.SqlClient", "SqlServer");
            RegisterAlias("System.Data.SqlClient", "SqlServer");
            RegisterAlias("MSSQL", "SqlServer");

            RegisterAlias("Microsoft.Data.Sqlite", "Sqlite");
            RegisterAlias("System.Data.SQLite", "Sqlite");

            RegisterAlias("Npgsql", "PostgreSql");
            RegisterAlias("Postgres", "PostgreSql");

            RegisterAlias("MySqlConnector", "MySql");
            RegisterAlias("MySql.Data.MySqlClient", "MySql");
            RegisterAlias("MariaDB", "MySql");

            RegisterAlias("Oracle.ManagedDataAccess.Client", "Oracle");
            RegisterAlias("Oracle.DataAccess.Client", "Oracle");

            RegisterAlias("FirebirdSql.Data.FirebirdClient", "Firebird");
        }

        private static void RegisterAlias(string alias, string targetProvider)
        {
            Factories[alias] = cs =>
            {
                if (Factories.TryGetValue(targetProvider, out var targetFactory))
                    return targetFactory(cs);

                throw new InvalidOperationException(
                    $"Provider '{alias}' maps to '{targetProvider}', but no factory has been registered for '{targetProvider}'. " +
                    $"Please call DbProviderRegistry.Register(\"{targetProvider}\", cs => new YourDbConnection(cs));");
            };
        }

        /// <summary>
        /// Registers a factory function for the specified provider name and optional aliases.
        /// Example: DbProviderRegistry.Register("SqlServer", cs => new SqlConnection(cs));
        /// </summary>
        public static void Register(string providerName, Func<string, IDbConnection> factory, params string[] aliases)
        {
            if (string.IsNullOrWhiteSpace(providerName))
                throw new ArgumentNullException(nameof(providerName));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            Factories[providerName] = factory;

            if (aliases != null)
            {
                foreach (var alias in aliases)
                {
                    if (!string.IsNullOrWhiteSpace(alias))
                        Factories[alias] = factory;
                }
            }
        }

        /// <summary>
        /// Registers a factory function for the specified provider name and optional aliases.
        /// Alias of <see cref="Register"/>.
        /// </summary>
        public static void RegisterProvider(string providerName, Func<string, IDbConnection> factory, params string[] aliases)
        {
            Register(providerName, factory, aliases);
        }

        /// <summary>
        /// Attempts to get a registered factory for the specified provider name.
        /// </summary>
        public static bool TryGetFactory(string providerName, out Func<string, IDbConnection> factory)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                factory = null;
                return false;
            }
            return Factories.TryGetValue(providerName, out factory);
        }

        /// <summary>
        /// Creates a database connection for the given connection string and optional provider name.
        /// If providerName is null, the provider is automatically detected from connection string heuristics.
        /// </summary>
        public static IDbConnection CreateConnection(string connectionString, string providerName = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            var targetProvider = providerName ?? DetectProvider(connectionString);

            if (Factories.TryGetValue(targetProvider, out var factory))
            {
                return factory(connectionString);
            }

            throw new InvalidOperationException(
                $"No database connection factory is registered for provider '{targetProvider}'. " +
                $"Register one using: DbProviderRegistry.Register(\"{targetProvider}\", cs => new YourConnection(cs)); " +
                $"or pass an existing IDbConnection directly to new SqlContext(connection).");
        }

        /// <summary>
        /// Heuristically detects the database provider from connection string patterns.
        /// </summary>
        public static string DetectProvider(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return "SqlServer";

            var cs = connectionString.ToLowerInvariant();

            // SQLite
            if ((cs.Contains("data source=") || cs.Contains("datasource=") || cs.Contains("filename=")) &&
                (cs.Contains(".db") || cs.Contains(".sqlite") || cs.Contains(".sqlite3") || cs.Contains(":memory:") || cs.Contains("mode=")))
            {
                return "Sqlite";
            }

            // Oracle (check before PostgreSQL because Oracle TNS strings contain HOST=)
            if (cs.Contains("(description=") || cs.Contains("tns_admin") || cs.Contains("dba privilege") || cs.Contains("orcl") || cs.Contains("user id=hr") || cs.Contains(".world"))
            {
                return "Oracle";
            }

            // PostgreSQL
            if (cs.Contains("host=") || cs.Contains("port=5432") || cs.Contains("search path") || cs.Contains("sslmode=require"))
            {
                return "PostgreSql";
            }

            // MySQL / MariaDB
            if (cs.Contains("port=3306") || cs.Contains("uid=") || cs.Contains("allowuservariables") || cs.Contains("allow zero datetime"))
            {
                return "MySql";
            }

            // Firebird
            if (cs.Contains(".fdb") || cs.Contains("user=sysdba") || cs.Contains("dialect="))
            {
                return "Firebird";
            }

            // Default fallback
            return "SqlServer";
        }

        /// <summary>
        /// Clears all registered connection factories.
        /// </summary>
        public static void Clear()
        {
            Factories.Clear();
        }
    }
}
