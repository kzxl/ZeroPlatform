using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroData.Sql.Dialects
{
    /// <summary>
    /// Generic ANSI SQL:2008 dialect implementation.
    /// Acts as a universal fallback for any standard relational database
    /// (DuckDB, ClickHouse, DB2, Informix, ODBC, OLEDB, etc.)
    /// </summary>
    public class AnsiSqlDialect : ISqlDialect
    {
        public string ProviderName => "ANSI SQL";

        public string ParameterPrefix => "@";

        public bool SupportsReturningClause => false;

        public bool SupportsOutputClause => false;

        public bool SupportsMultiRowValues => true;

        /// <summary>
        /// ANSI SQL standard double quotes for identifiers.
        /// </summary>
        public string QuoteIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                throw new ArgumentNullException(nameof(identifier));

            identifier = identifier.Replace("\"", "\"\"");
            return $"\"{identifier}\"";
        }

        /// <summary>
        /// ANSI SQL standard OFFSET/FETCH syntax.
        /// </summary>
        public string GetLimitClause(int? skip, int? take)
        {
            if (!skip.HasValue && !take.HasValue)
                return string.Empty;

            var clause = string.Empty;

            if (skip.HasValue && skip.Value > 0)
            {
                clause = $"OFFSET {skip.Value} ROWS";
                if (take.HasValue && take.Value > 0)
                    clause += $" FETCH NEXT {take.Value} ROWS ONLY";
            }
            else if (take.HasValue && take.Value > 0)
            {
                clause = $"OFFSET 0 ROWS FETCH NEXT {take.Value} ROWS ONLY";
            }

            return clause.Trim();
        }

        public string GetLastInsertIdSql(string tableName, string columnName)
        {
            return "SELECT @@IDENTITY";
        }

        public string GetAutoIncrementSql()
        {
            return "IDENTITY(1,1)";
        }

        public string GetTableExistsSql(string tableName)
        {
            return $"SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{EscapeStringValue(tableName)}') THEN 1 ELSE 0 END";
        }

        public string GetDbType(Type clrType)
        {
            if (clrType == typeof(int)) return "INTEGER";
            if (clrType == typeof(long)) return "BIGINT";
            if (clrType == typeof(short)) return "SMALLINT";
            if (clrType == typeof(byte)) return "SMALLINT";
            if (clrType == typeof(bool)) return "BOOLEAN";
            if (clrType == typeof(decimal)) return "DECIMAL(18,2)";
            if (clrType == typeof(double)) return "DOUBLE PRECISION";
            if (clrType == typeof(float)) return "FLOAT";
            if (clrType == typeof(string)) return "VARCHAR(4000)";
            if (clrType == typeof(DateTime)) return "TIMESTAMP";
            if (clrType == typeof(DateTimeOffset)) return "TIMESTAMP WITH TIME ZONE";
            if (clrType == typeof(Guid)) return "VARCHAR(36)";
            if (clrType == typeof(byte[])) return "BLOB";

            var underlyingType = Nullable.GetUnderlyingType(clrType);
            if (underlyingType != null)
                return GetDbType(underlyingType);

            return "VARCHAR(4000)";
        }

        public string EscapeStringValue(string value)
        {
            if (value == null)
                return "NULL";

            return value.Replace("'", "''");
        }

        public string GenerateBulkInsertSql(string tableName, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> parameterRows)
        {
            var cols = string.Join(", ", columns);
            var rows = string.Join(", ", parameterRows.Select(r => $"({string.Join(", ", r)})"));
            return $"INSERT INTO {tableName} ({cols}) VALUES {rows}";
        }

        public string GenerateBulkMergeSql(string tableName, IReadOnlyList<string> columns, IReadOnlyList<string> primaryKeyColumns, IReadOnlyList<IReadOnlyList<string>> parameterRows)
        {
            var cols = string.Join(", ", columns);
            var rows = string.Join(", ", parameterRows.Select(r => $"({string.Join(", ", r)})"));
            var onClause = string.Join(" AND ", primaryKeyColumns.Select(pk => $"t.{pk} = s.{pk}"));
            var updateCols = columns.Where(c => !primaryKeyColumns.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();

            var updateClause = updateCols.Count > 0
                ? $"WHEN MATCHED THEN UPDATE SET {string.Join(", ", updateCols.Select(c => $"t.{c} = s.{c}"))}"
                : "";

            var insertCols = string.Join(", ", columns);
            var insertVals = string.Join(", ", columns.Select(c => $"s.{c}"));

            return $"MERGE INTO {tableName} t USING (VALUES {rows}) s ({cols}) ON ({onClause}) {updateClause} WHEN NOT MATCHED THEN INSERT ({insertCols}) VALUES ({insertVals})";
        }

        public bool SupportsSavepoints => true;

        public string GetCreateSavepointSql(string name) => $"SAVEPOINT {QuoteIdentifier(name)};";

        public string GetRollbackSavepointSql(string name) => $"ROLLBACK TO SAVEPOINT {QuoteIdentifier(name)};";

        public string GetReleaseSavepointSql(string name) => $"RELEASE SAVEPOINT {QuoteIdentifier(name)};";
    }
}
