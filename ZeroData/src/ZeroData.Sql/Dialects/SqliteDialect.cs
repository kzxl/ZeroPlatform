using System;

namespace ZeroData.Sql.Dialects
{
    /// <summary>
    /// SQLite dialect implementation.
    /// Supports SQLite 3.8+.
    /// </summary>
    public class SqliteDialect : ISqlDialect
    {
        public string ProviderName => "SQLite";

        public string ParameterPrefix => "@";

        public bool SupportsReturningClause => true; // SQLite 3.35+ supports RETURNING

        public bool SupportsOutputClause => false;

        /// <summary>
        /// SQLite uses double quotes for identifiers: "TableName"
        /// Can also use square brackets [TableName] but double quotes are standard
        /// </summary>
        public string QuoteIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                throw new ArgumentNullException(nameof(identifier));

            // Escape existing double quotes
            identifier = identifier.Replace("\"", "\"\"");
            return $"\"{identifier}\"";
        }

        /// <summary>
        /// SQLite uses LIMIT/OFFSET syntax
        /// </summary>
        public string GetLimitClause(int? skip, int? take)
        {
            if (!skip.HasValue && !take.HasValue)
                return string.Empty;

            var clause = string.Empty;

            if (take.HasValue && take.Value > 0)
                clause = $"LIMIT {take.Value}";

            if (skip.HasValue && skip.Value > 0)
                clause += $" OFFSET {skip.Value}";

            return clause.Trim();
        }

        /// <summary>
        /// SQLite uses last_insert_rowid() to get last inserted ID
        /// </summary>
        public string GetLastInsertIdSql(string tableName, string columnName)
        {
            return "SELECT last_insert_rowid()";
        }

        /// <summary>
        /// SQLite uses AUTOINCREMENT for auto-increment
        /// </summary>
        public string GetAutoIncrementSql()
        {
            return "AUTOINCREMENT";
        }

        public string GetTableExistsSql(string tableName)
        {
            return $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{EscapeStringValue(tableName)}'";
        }

        public string GetDbType(Type clrType)
        {
            // SQLite has dynamic typing with 5 storage classes: NULL, INTEGER, REAL, TEXT, BLOB
            if (clrType == typeof(int)) return "INTEGER";
            if (clrType == typeof(long)) return "INTEGER";
            if (clrType == typeof(short)) return "INTEGER";
            if (clrType == typeof(byte)) return "INTEGER";
            if (clrType == typeof(bool)) return "INTEGER";
            if (clrType == typeof(decimal)) return "REAL";
            if (clrType == typeof(double)) return "REAL";
            if (clrType == typeof(float)) return "REAL";
            if (clrType == typeof(string)) return "TEXT";
            if (clrType == typeof(DateTime)) return "TEXT"; // Store as ISO8601 string
            if (clrType == typeof(DateTimeOffset)) return "TEXT";
            if (clrType == typeof(Guid)) return "TEXT";
            if (clrType == typeof(byte[])) return "BLOB";

            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(clrType);
            if (underlyingType != null)
                return GetDbType(underlyingType);

            return "TEXT"; // Default fallback
        }

        public string EscapeStringValue(string value)
        {
            if (value == null)
                return "NULL";

            // Escape single quotes by doubling them
            return value.Replace("'", "''");
        }

        public bool SupportsMultiRowValues => true;

        public string GenerateBulkInsertSql(string tableName, System.Collections.Generic.IReadOnlyList<string> columns, System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<string>> parameterRows)
        {
            var cols = string.Join(", ", columns);
            var rows = string.Join(", ", System.Linq.Enumerable.Select(parameterRows, r => $"({string.Join(", ", r)})"));
            return $"INSERT INTO {tableName} ({cols}) VALUES {rows}";
        }

        public string GenerateBulkMergeSql(string tableName, System.Collections.Generic.IReadOnlyList<string> columns, System.Collections.Generic.IReadOnlyList<string> primaryKeyColumns, System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<string>> parameterRows)
        {
            var cols = string.Join(", ", columns);
            var rows = string.Join(", ", System.Linq.Enumerable.Select(parameterRows, r => $"({string.Join(", ", r)})"));
            var pks = string.Join(", ", primaryKeyColumns);
            var updateCols = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(columns, c => !System.Linq.Enumerable.Contains(primaryKeyColumns, c, StringComparer.OrdinalIgnoreCase)));

            if (updateCols.Count > 0)
            {
                var updateClause = string.Join(", ", System.Linq.Enumerable.Select(updateCols, c => $"{c} = excluded.{c}"));
                return $"INSERT INTO {tableName} ({cols}) VALUES {rows} ON CONFLICT ({pks}) DO UPDATE SET {updateClause}";
            }
            return $"INSERT OR IGNORE INTO {tableName} ({cols}) VALUES {rows}";
        }

        public bool SupportsSavepoints => true;

        public string GetCreateSavepointSql(string name) => $"SAVEPOINT {QuoteIdentifier(name)};";

        public string GetRollbackSavepointSql(string name) => $"ROLLBACK TO SAVEPOINT {QuoteIdentifier(name)};";

        public string GetReleaseSavepointSql(string name) => $"RELEASE SAVEPOINT {QuoteIdentifier(name)};";
    }
}
