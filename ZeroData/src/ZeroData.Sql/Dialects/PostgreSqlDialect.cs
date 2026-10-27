using System;

namespace ZeroData.Sql.Dialects
{
    /// <summary>
    /// PostgreSQL dialect implementation.
    /// Supports PostgreSQL 9.5+.
    /// </summary>
    public class PostgreSqlDialect : ISqlDialect
    {
        public string ProviderName => "PostgreSQL";

        public string ParameterPrefix => "@";

        public bool SupportsReturningClause => true;

        public bool SupportsOutputClause => false;

        /// <summary>
        /// PostgreSQL uses double quotes for identifiers: "TableName"
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
        /// PostgreSQL uses LIMIT/OFFSET syntax
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
        /// PostgreSQL uses RETURNING clause to get last inserted ID
        /// </summary>
        public string GetLastInsertIdSql(string tableName, string columnName)
        {
            return $"RETURNING {QuoteIdentifier(columnName)}";
        }

        /// <summary>
        /// PostgreSQL uses SERIAL or GENERATED ALWAYS AS IDENTITY for auto-increment
        /// </summary>
        public string GetAutoIncrementSql()
        {
            return "GENERATED ALWAYS AS IDENTITY";
        }

        public string GetTableExistsSql(string tableName)
        {
            return $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '{EscapeStringValue(tableName)}'";
        }

        public string GetDbType(Type clrType)
        {
            if (clrType == typeof(int)) return "INTEGER";
            if (clrType == typeof(long)) return "BIGINT";
            if (clrType == typeof(short)) return "SMALLINT";
            if (clrType == typeof(byte)) return "SMALLINT"; // PostgreSQL doesn't have TINYINT
            if (clrType == typeof(bool)) return "BOOLEAN";
            if (clrType == typeof(decimal)) return "NUMERIC(18,2)";
            if (clrType == typeof(double)) return "DOUBLE PRECISION";
            if (clrType == typeof(float)) return "REAL";
            if (clrType == typeof(string)) return "TEXT";
            if (clrType == typeof(DateTime)) return "TIMESTAMP";
            if (clrType == typeof(DateTimeOffset)) return "TIMESTAMPTZ";
            if (clrType == typeof(Guid)) return "UUID";
            if (clrType == typeof(byte[])) return "BYTEA";

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
                var updateClause = string.Join(", ", System.Linq.Enumerable.Select(updateCols, c => $"{c} = EXCLUDED.{c}"));
                return $"INSERT INTO {tableName} ({cols}) VALUES {rows} ON CONFLICT ({pks}) DO UPDATE SET {updateClause}";
            }
            return $"INSERT INTO {tableName} ({cols}) VALUES {rows} ON CONFLICT ({pks}) DO NOTHING";
        }

        public bool SupportsSavepoints => true;

        public string GetCreateSavepointSql(string name) => $"SAVEPOINT {QuoteIdentifier(name)};";

        public string GetRollbackSavepointSql(string name) => $"ROLLBACK TO SAVEPOINT {QuoteIdentifier(name)};";

        public string GetReleaseSavepointSql(string name) => $"RELEASE SAVEPOINT {QuoteIdentifier(name)};";
    }
}
