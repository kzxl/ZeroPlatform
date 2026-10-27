using System;

namespace ZeroData.Sql.Dialects
{
    /// <summary>
    /// Defines SQL dialect-specific behavior for different database providers.
    /// Allows LiteSql to support multiple databases (SQL Server, MySQL, PostgreSQL, etc.)
    /// </summary>
    public interface ISqlDialect
    {
        /// <summary>
        /// Gets the name of the database provider (e.g., "SQL Server", "MySQL", "PostgreSQL")
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Quotes an identifier (table name, column name) according to database rules.
        /// SQL Server: [Name], MySQL: `Name`, PostgreSQL: "Name"
        /// </summary>
        string QuoteIdentifier(string identifier);

        /// <summary>
        /// Generates LIMIT/OFFSET clause for pagination.
        /// SQL Server: OFFSET x ROWS FETCH NEXT y ROWS ONLY
        /// MySQL/PostgreSQL: LIMIT y OFFSET x
        /// </summary>
        string GetLimitClause(int? skip, int? take);

        /// <summary>
        /// Gets the SQL for retrieving the last inserted identity/auto-increment value.
        /// SQL Server: SELECT SCOPE_IDENTITY()
        /// MySQL: SELECT LAST_INSERT_ID()
        /// PostgreSQL: RETURNING id
        /// </summary>
        string GetLastInsertIdSql(string tableName, string columnName);

        /// <summary>
        /// Gets the parameter prefix for this database.
        /// SQL Server/MySQL: @
        /// PostgreSQL: $
        /// Oracle: :
        /// </summary>
        string ParameterPrefix { get; }

        /// <summary>
        /// Checks if the database supports RETURNING clause (PostgreSQL).
        /// </summary>
        bool SupportsReturningClause { get; }

        /// <summary>
        /// Checks if the database supports OUTPUT clause (SQL Server).
        /// </summary>
        bool SupportsOutputClause { get; }

        /// <summary>
        /// Gets the SQL for checking if a table exists.
        /// </summary>
        string GetTableExistsSql(string tableName);

        /// <summary>
        /// Converts a CLR type to database-specific type name.
        /// </summary>
        string GetDbType(Type clrType);

        /// <summary>
        /// Gets the SQL for creating an auto-increment/identity column.
        /// SQL Server: IDENTITY(1,1)
        /// MySQL: AUTO_INCREMENT
        /// PostgreSQL: SERIAL or GENERATED ALWAYS AS IDENTITY
        /// </summary>
        string GetAutoIncrementSql();

        /// <summary>
        /// Escapes a string value for SQL (prevents SQL injection in dynamic SQL).
        /// </summary>
        string EscapeStringValue(string value);

        /// <summary>
        /// Indicates whether the dialect supports VALUES (...), (...) multi-row INSERT syntax.
        /// SQL Server, PostgreSQL, MySQL, SQLite, Firebird: true. Oracle: false (uses INSERT ALL).
        /// </summary>
        bool SupportsMultiRowValues { get; }

        /// <summary>
        /// Generates a bulk insert SQL statement for the dialect.
        /// </summary>
        string GenerateBulkInsertSql(string tableName, System.Collections.Generic.IReadOnlyList<string> columns, System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<string>> parameterRows);

        /// <summary>
        /// Generates a bulk merge / upsert SQL statement for the dialect based on primary key columns.
        /// </summary>
        string GenerateBulkMergeSql(string tableName, System.Collections.Generic.IReadOnlyList<string> columns, System.Collections.Generic.IReadOnlyList<string> primaryKeyColumns, System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<string>> parameterRows);

        /// <summary>
        /// Gets a value indicating whether this database dialect supports transaction savepoints.
        /// </summary>
        bool SupportsSavepoints { get; }

        /// <summary>
        /// Generates SQL to create a transaction savepoint.
        /// </summary>
        string GetCreateSavepointSql(string name);

        /// <summary>
        /// Generates SQL to rollback a transaction to a named savepoint.
        /// </summary>
        string GetRollbackSavepointSql(string name);

        /// <summary>
        /// Generates SQL to release a named savepoint. Returns null or empty string if not supported/needed.
        /// </summary>
        string GetReleaseSavepointSql(string name);
    }
}
