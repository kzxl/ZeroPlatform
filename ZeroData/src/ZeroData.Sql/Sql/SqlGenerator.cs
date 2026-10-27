using ZeroData.Sql.ChangeTracking;
using ZeroData.Sql.Dialects;
using ZeroData.Sql.Mapping;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;

namespace ZeroData.Sql.Sql
{
    /// <summary>
    /// Generates parameterized SQL statements (INSERT, DELETE, SELECT, UPDATE)
    /// from entity mapping metadata. SQL Server dialect by default.
    /// </summary>
    public static class SqlGenerator
    {
        // OPT-2: SQL template caches per entity type (keyed by type + dialect provider)
        private static readonly ConcurrentDictionary<string, string> _insertSqlCache = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<string, string> _deleteSqlCache = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<string, string> _selectAllCache = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// Default SQL dialect used when none is specified. Defaults to SQL Server.
        /// </summary>
        public static ISqlDialect DefaultDialect { get; set; } = new SqlServerDialect();

        private static string CacheKey(Type type, ISqlDialect dialect)
            => type.FullName + "::" + (dialect ?? DefaultDialect).ProviderName;

        /// <summary>
        /// Generates a SELECT * statement for the entity type using the specified dialect.
        /// </summary>
        public static string GenerateSelectAll(EntityMapping mapping, ISqlDialect dialect = null)
        {
            var d = dialect ?? DefaultDialect;
            return _selectAllCache.GetOrAdd(CacheKey(mapping.EntityType, d),
                _ => $"SELECT * FROM {QuoteTableName(mapping.TableName, d)}");
        }

        /// <summary>
        /// Generates a parameterized INSERT statement and its parameter dictionary.
        /// Skips IsDbGenerated columns.
        /// Returns the generated identity value if any PK is IsDbGenerated.
        /// </summary>
        public static (string Sql, IDictionary<string, object> Parameters) GenerateInsert(
            EntityMapping mapping, object entity)
        {
            return GenerateInsert(mapping, entity, DefaultDialect);
        }

        /// <summary>
        /// Generates a parameterized INSERT statement using the specified dialect.
        /// </summary>
        public static (string Sql, IDictionary<string, object> Parameters) GenerateInsert(
            EntityMapping mapping, object entity, ISqlDialect dialect)
        {
            var d = dialect ?? DefaultDialect;
            var sql = _insertSqlCache.GetOrAdd(CacheKey(mapping.EntityType, d), _ =>
            {
                var columns = mapping.InsertableColumns;
                var columnNames = columns.Select(c => QuoteIdentifier(c.ColumnName, d));
                var paramNames = columns.Select(c => $"@{c.ColumnName}");
                return $"INSERT INTO {QuoteTableName(mapping.TableName, d)} ({string.Join(", ", columnNames)}) " +
                       $"VALUES ({string.Join(", ", paramNames)})";
            });

            var parameters = BuildParameters(mapping.InsertableColumns, entity);
            return (sql, parameters);
        }

        /// <summary>
        /// Generates a parameterized DELETE statement using primary key(s).
        /// If entity has [SoftDelete] attribute, generates UPDATE SET {Column}=1 instead.
        /// </summary>
        public static (string Sql, IDictionary<string, object> Parameters) GenerateDelete(
            EntityMapping mapping, object entity)
        {
            return GenerateDelete(mapping, entity, DefaultDialect);
        }

        /// <summary>
        /// Generates a parameterized DELETE (or soft-delete UPDATE) using the specified dialect.
        /// </summary>
        public static (string Sql, IDictionary<string, object> Parameters) GenerateDelete(
            EntityMapping mapping, object entity, ISqlDialect dialect)
        {
            var d = dialect ?? DefaultDialect;
            if (mapping.PrimaryKeys.Count == 0)
                throw new InvalidOperationException(
                    $"Cannot generate DELETE for '{mapping.EntityType.Name}': no primary key defined.");

            // Check for SoftDelete attribute
            var softDelete = mapping.EntityType
                .GetCustomAttributes(typeof(SoftDeleteAttribute), true)
                .FirstOrDefault() as SoftDeleteAttribute;

            string sqlTemplate;
            if (softDelete != null)
            {
                // Soft delete: UPDATE SET {Column} = <true> WHERE PK = @pk
                var trueLiteral = d.ProviderName == "PostgreSQL" ? "TRUE" : "1";
                var pkClauses = mapping.PrimaryKeys.Select(pk => $"{QuoteIdentifier(pk.ColumnName, d)} = @pk_{pk.ColumnName}");
                sqlTemplate = $"UPDATE {QuoteTableName(mapping.TableName, d)} SET {QuoteIdentifier(softDelete.ColumnName, d)} = {trueLiteral} " +
                              $"WHERE {string.Join(" AND ", pkClauses)}";
            }
            else
            {
                sqlTemplate = _deleteSqlCache.GetOrAdd(CacheKey(mapping.EntityType, d), _ =>
                {
                    var pkClauses = mapping.PrimaryKeys.Select(pk => $"{QuoteIdentifier(pk.ColumnName, d)} = @pk_{pk.ColumnName}");
                    return $"DELETE FROM {QuoteTableName(mapping.TableName, d)} WHERE {string.Join(" AND ", pkClauses)}";
                });
            }

            var parameters = new Dictionary<string, object>();
            foreach (var pk in mapping.PrimaryKeys)
                parameters[$"@pk_{pk.ColumnName}"] = pk.Property.GetValue(entity);

            return (sqlTemplate, parameters);
        }

        /// <summary>
        /// Generates a parameterized UPDATE statement.
        /// Updates all non-PK, non-DbGenerated columns.
        /// </summary>
        public static (string Sql, IDictionary<string, object> Parameters) GenerateUpdate(
            EntityMapping mapping, object entity)
        {
            return GenerateUpdate(mapping, entity, DefaultDialect);
        }

        /// <summary>
        /// Generates a parameterized UPDATE statement using the specified dialect.
        /// </summary>
        public static (string Sql, IDictionary<string, object> Parameters) GenerateUpdate(
            EntityMapping mapping, object entity, ISqlDialect dialect)
        {
            var d = dialect ?? DefaultDialect;
            if (mapping.PrimaryKeys.Count == 0)
                throw new InvalidOperationException(
                    $"Cannot generate UPDATE for '{mapping.EntityType.Name}': no primary key defined.");

            if (mapping.UpdatableColumns.Count == 0)
                throw new InvalidOperationException(
                    $"Cannot generate UPDATE for '{mapping.EntityType.Name}': no updatable columns.");

            var setClauses = mapping.UpdatableColumns
                .Select(c => $"{QuoteIdentifier(c.ColumnName, d)} = @{c.ColumnName}")
                .ToList();

            var whereClause = BuildWhereByPrimaryKeys(mapping, entity, d, out var whereParams);

            // Merge SET parameters with WHERE parameters
            var allParams = BuildParameters(mapping.UpdatableColumns, entity);
            foreach (var kv in whereParams)
            {
                allParams[kv.Key] = kv.Value;
            }

            // Optimistic concurrency: add version check to WHERE and bump integer versions.
            whereClause = ApplyVersionConcurrency(mapping, entity, d, setClauses, allParams, whereClause);

            var sql = $"UPDATE {QuoteTableName(mapping.TableName, d)} " +
                      $"SET {string.Join(", ", setClauses)} " +
                      $"WHERE {whereClause}";

            return (sql, allParams);
        }

        /// <summary>
        /// Generates a partial UPDATE statement — only SET columns that have changed.
        /// Used by dirty update feature when ChangedProperties is available.
        /// </summary>
        public static (string Sql, IDictionary<string, object> Parameters) GeneratePartialUpdate(
            EntityMapping mapping, object entity, IReadOnlyList<string> changedProperties)
        {
            return GeneratePartialUpdate(mapping, entity, changedProperties, DefaultDialect);
        }

        /// <summary>
        /// Generates a partial UPDATE statement using the specified dialect.
        /// </summary>
        public static (string Sql, IDictionary<string, object> Parameters) GeneratePartialUpdate(
            EntityMapping mapping, object entity, IReadOnlyList<string> changedProperties, ISqlDialect dialect)
        {
            var d = dialect ?? DefaultDialect;
            if (mapping.PrimaryKeys.Count == 0)
                throw new InvalidOperationException(
                    $"Cannot generate UPDATE for '{mapping.EntityType.Name}': no primary key defined.");

            // Filter updatable columns to only changed ones
            var changedColumns = mapping.UpdatableColumns
                .Where(c => changedProperties.Contains(c.Property.Name))
                .ToList();

            if (changedColumns.Count == 0)
                return (null, null); // No changes to persist

            var setClauses = changedColumns.Select(c => $"{QuoteIdentifier(c.ColumnName, d)} = @{c.ColumnName}").ToList();
            var whereClause = BuildWhereByPrimaryKeys(mapping, entity, d, out var whereParams);

            var allParams = BuildParameters(changedColumns, entity);
            foreach (var kv in whereParams)
                allParams[kv.Key] = kv.Value;

            // Optimistic concurrency: add version check to WHERE and bump integer versions.
            whereClause = ApplyVersionConcurrency(mapping, entity, d, setClauses, allParams, whereClause);

            var sql = $"UPDATE {QuoteTableName(mapping.TableName, d)} " +
                      $"SET {string.Join(", ", setClauses)} " +
                      $"WHERE {whereClause}";

            return (sql, allParams);
        }

        /// <summary>
        /// Converts L2S-style positional parameters ({0}, {1}, ...) to named Dapper parameters.
        /// </summary>
        public static (string Sql, IDictionary<string, object> Parameters) ConvertPositionalParameters(
            string sql, object[] args)
        {
            if (args == null || args.Length == 0)
                return (sql, new Dictionary<string, object>());

            var parameters = new Dictionary<string, object>();
            for (int i = 0; i < args.Length; i++)
            {
                var paramName = $"@p{i}";
                sql = sql.Replace("{" + i + "}", paramName);
                parameters[paramName] = args[i];
            }

            return (sql, parameters);
        }

        #region Private Helpers

        private static string BuildWhereByPrimaryKeys(
            EntityMapping mapping, object entity, ISqlDialect dialect, out IDictionary<string, object> parameters)
        {
            var d = dialect ?? DefaultDialect;
            parameters = new Dictionary<string, object>();
            var clauses = new List<string>();

            foreach (var pk in mapping.PrimaryKeys)
            {
                var paramName = $"@pk_{pk.ColumnName}";
                clauses.Add($"{QuoteIdentifier(pk.ColumnName, d)} = {paramName}");
                parameters[paramName] = pk.Property.GetValue(entity);
            }

            return string.Join(" AND ", clauses);
        }

        /// <summary>
        /// Applies optimistic concurrency: appends "version = @version_original" to the WHERE,
        /// and for integral version columns adds "version = version + 1" to the SET list so the
        /// value advances on each successful update. No-op when the entity has no version columns.
        /// </summary>
        private static string ApplyVersionConcurrency(
            EntityMapping mapping, object entity, ISqlDialect d,
            List<string> setClauses, IDictionary<string, object> allParams, string whereClause)
        {
            if (mapping.VersionColumns == null || mapping.VersionColumns.Count == 0)
                return whereClause;

            var extra = new List<string>();
            foreach (var ver in mapping.VersionColumns)
            {
                var quoted = QuoteIdentifier(ver.ColumnName, d);
                var origParam = $"@ver_{ver.ColumnName}";
                allParams[origParam] = ver.Property.GetValue(entity);
                extra.Add($"{quoted} = {origParam}");

                // For integral version columns, increment in place (rowversion/timestamp is DB-managed).
                var verType = Nullable.GetUnderlyingType(ver.Property.PropertyType) ?? ver.Property.PropertyType;
                if (verType == typeof(int) || verType == typeof(long) || verType == typeof(short))
                {
                    // Replace any existing SET for this column, then add the increment.
                    setClauses.RemoveAll(s => s.StartsWith($"{quoted} =", StringComparison.Ordinal));
                    setClauses.Add($"{quoted} = {quoted} + 1");
                }
            }

            return string.IsNullOrEmpty(whereClause)
                ? string.Join(" AND ", extra)
                : whereClause + " AND " + string.Join(" AND ", extra);
        }

        private static IDictionary<string, object> BuildParameters(
            IReadOnlyList<ColumnMapping> columns, object entity)
        {
            var parameters = new Dictionary<string, object>();
            foreach (var col in columns)
            {
                parameters[$"@{col.ColumnName}"] = col.Property.GetValue(entity);
            }
            return parameters;
        }

        /// <summary>
        /// Generates an UPSERT (INSERT OR UPDATE) SQL statement.
        /// SQLite: INSERT OR REPLACE. SQL Server: MERGE.
        /// MySQL: INSERT ... ON DUPLICATE KEY UPDATE. PostgreSQL: INSERT ... ON CONFLICT.
        /// Legacy bool overload — kept for backward compatibility.
        /// </summary>
        public static (string sql, IDictionary<string, object> parameters)? GenerateUpsert(
            EntityMapping mapping, object entity, bool isSqlite = true)
        {
            return GenerateUpsert(mapping, entity, isSqlite ? (ISqlDialect)new SqliteDialect() : new SqlServerDialect());
        }

        /// <summary>
        /// Generates a dialect-specific UPSERT statement.
        /// </summary>
        public static (string sql, IDictionary<string, object> parameters)? GenerateUpsert(
            EntityMapping mapping, object entity, ISqlDialect dialect)
        {
            var d = dialect ?? DefaultDialect;
            if (mapping.PrimaryKeys.Count == 0) return null;

            var allCols = mapping.Columns.Where(c => !c.IsDbGenerated).ToList();
            var parameters = BuildParameters(allCols, entity);

            // Also add PK params for MERGE ON clause
            foreach (var pk in mapping.PrimaryKeys)
            {
                var pkParam = $"@pk_{pk.ColumnName}";
                if (!parameters.ContainsKey(pkParam))
                    parameters[pkParam] = pk.Property.GetValue(entity);
            }

            var table = QuoteTableName(mapping.TableName, d);
            var columnNames = allCols.Select(c => QuoteIdentifier(c.ColumnName, d)).ToList();
            var paramNames = allCols.Select(c => $"@{c.ColumnName}").ToList();
            var updateCols = allCols.Where(c => !c.IsPrimaryKey).ToList();

            switch (d.ProviderName)
            {
                case "SQLite":
                {
                    var sql = $"INSERT OR REPLACE INTO {table} " +
                              $"({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", paramNames)})";
                    return (sql, parameters);
                }

                case "MySQL":
                {
                    // INSERT ... ON DUPLICATE KEY UPDATE col = VALUES(col)
                    var setClauses = updateCols.Count > 0
                        ? updateCols.Select(c => $"{QuoteIdentifier(c.ColumnName, d)} = VALUES({QuoteIdentifier(c.ColumnName, d)})")
                        : mapping.PrimaryKeys.Select(pk => $"{QuoteIdentifier(pk.ColumnName, d)} = {QuoteIdentifier(pk.ColumnName, d)}");
                    var sql = $"INSERT INTO {table} ({string.Join(", ", columnNames)}) " +
                              $"VALUES ({string.Join(", ", paramNames)}) " +
                              $"ON DUPLICATE KEY UPDATE {string.Join(", ", setClauses)}";
                    return (sql, parameters);
                }

                case "PostgreSQL":
                {
                    // INSERT ... ON CONFLICT (pk) DO UPDATE SET col = EXCLUDED.col
                    var conflictCols = string.Join(", ", mapping.PrimaryKeys.Select(pk => QuoteIdentifier(pk.ColumnName, d)));
                    string sql;
                    if (updateCols.Count > 0)
                    {
                        var setClauses = updateCols.Select(c => $"{QuoteIdentifier(c.ColumnName, d)} = EXCLUDED.{QuoteIdentifier(c.ColumnName, d)}");
                        sql = $"INSERT INTO {table} ({string.Join(", ", columnNames)}) " +
                              $"VALUES ({string.Join(", ", paramNames)}) " +
                              $"ON CONFLICT ({conflictCols}) DO UPDATE SET {string.Join(", ", setClauses)}";
                    }
                    else
                    {
                        sql = $"INSERT INTO {table} ({string.Join(", ", columnNames)}) " +
                              $"VALUES ({string.Join(", ", paramNames)}) " +
                              $"ON CONFLICT ({conflictCols}) DO NOTHING";
                    }
                    return (sql, parameters);
                }

                default:
                {
                    // SQL Server: MERGE
                    var onClause = string.Join(" AND ",
                        mapping.PrimaryKeys.Select(pk => $"T.{QuoteIdentifier(pk.ColumnName, d)} = S.{QuoteIdentifier(pk.ColumnName, d)}"));
                    var setClauses = updateCols.Select(c => $"T.{QuoteIdentifier(c.ColumnName, d)} = @{c.ColumnName}");
                    var insertCols = allCols.Select(c => QuoteIdentifier(c.ColumnName, d));
                    var insertVals = allCols.Select(c => $"@{c.ColumnName}");

                    var matchedClause = updateCols.Count > 0
                        ? $"WHEN MATCHED THEN UPDATE SET {string.Join(", ", setClauses)} "
                        : "";

                    var sql = $"MERGE {table} AS T " +
                              $"USING (SELECT {string.Join(", ", mapping.PrimaryKeys.Select(pk => $"@pk_{pk.ColumnName} AS {QuoteIdentifier(pk.ColumnName, d)}"))}) AS S " +
                              $"ON {onClause} " +
                              matchedClause +
                              $"WHEN NOT MATCHED THEN INSERT ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", insertVals)});";
                    return (sql, parameters);
                }
            }
        }

        #endregion

        /// <summary>
        /// Quotes a table name for SQL. Handles schema.table format:
        /// "dbo.Users" → "[dbo].[Users]", "Users" → "[Users]".
        /// </summary>
        internal static string QuoteTableName(string tableName)
        {
            return QuoteTableName(tableName, DefaultDialect);
        }

        /// <summary>
        /// Quotes a table name using the specified dialect.
        /// </summary>
        internal static string QuoteTableName(string tableName, ISqlDialect dialect)
        {
            if (string.IsNullOrEmpty(tableName)) return tableName;
            var parts = tableName.Split('.');
            return string.Join(".", parts.Select(p => dialect.QuoteIdentifier(p)));
        }

        /// <summary>
        /// Quotes a column name using the default dialect.
        /// </summary>
        internal static string QuoteIdentifier(string identifier)
        {
            return DefaultDialect.QuoteIdentifier(identifier);
        }

        /// <summary>
        /// Quotes a column name using the specified dialect.
        /// </summary>
        internal static string QuoteIdentifier(string identifier, ISqlDialect dialect)
        {
            return dialect.QuoteIdentifier(identifier);
        }
    }
}
