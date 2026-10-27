using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZeroData.Sql.Dialects;

namespace ZeroData.Sql.Migrations
{
    /// <summary>
    /// Fluent builder that produces dialect-aware DDL (CREATE/DROP TABLE, ADD/DROP COLUMN,
    /// CREATE/DROP INDEX). Generated statements are collected and returned for execution.
    /// </summary>
    public class SchemaBuilder
    {
        private readonly ISqlDialect _dialect;
        private readonly List<string> _statements = new List<string>();

        public SchemaBuilder(ISqlDialect dialect)
        {
            _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        /// <summary>The DDL statements accumulated so far.</summary>
        public IReadOnlyList<string> Statements => _statements;

        /// <summary>
        /// Creates a table. Configure columns via the builder action.
        /// </summary>
        public SchemaBuilder CreateTable(string tableName, Action<TableBuilder> configure)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var tb = new TableBuilder(_dialect);
            configure(tb);

            var cols = tb.BuildColumnDefinitions();
            if (cols.Count == 0)
                throw new InvalidOperationException($"CreateTable('{tableName}') must define at least one column.");

            var sb = new StringBuilder();
            sb.Append($"CREATE TABLE {Quote(tableName)} (");
            sb.Append(string.Join(", ", cols));

            var pk = tb.PrimaryKeyColumns;
            if (pk.Count > 0)
                sb.Append($", PRIMARY KEY ({string.Join(", ", pk.Select(Quote))})");

            sb.Append(")");
            _statements.Add(sb.ToString());
            return this;
        }

        /// <summary>Drops a table.</summary>
        public SchemaBuilder DropTable(string tableName)
        {
            _statements.Add($"DROP TABLE {Quote(tableName)}");
            return this;
        }

        /// <summary>Adds a column to an existing table.</summary>
        public SchemaBuilder AddColumn(string tableName, Action<ColumnBuilder> configure)
        {
            var cb = new ColumnBuilder(_dialect);
            configure(cb);
            _statements.Add($"ALTER TABLE {Quote(tableName)} ADD {cb.Build()}");
            return this;
        }

        /// <summary>Drops a column from a table.</summary>
        public SchemaBuilder DropColumn(string tableName, string columnName)
        {
            _statements.Add($"ALTER TABLE {Quote(tableName)} DROP COLUMN {Quote(columnName)}");
            return this;
        }

        /// <summary>Creates an index over one or more columns.</summary>
        public SchemaBuilder CreateIndex(string tableName, string indexName, bool unique, params string[] columns)
        {
            if (columns == null || columns.Length == 0)
                throw new ArgumentException("At least one column is required for an index.", nameof(columns));
            var uniq = unique ? "UNIQUE " : "";
            _statements.Add($"CREATE {uniq}INDEX {Quote(indexName)} ON {Quote(tableName)} ({string.Join(", ", columns.Select(Quote))})");
            return this;
        }

        /// <summary>Drops an index. SQL Server requires the table-qualified form.</summary>
        public SchemaBuilder DropIndex(string tableName, string indexName)
        {
            if (_dialect.ProviderName == "SQL Server")
                _statements.Add($"DROP INDEX {Quote(indexName)} ON {Quote(tableName)}");
            else
                _statements.Add($"DROP INDEX {Quote(indexName)}");
            return this;
        }

        /// <summary>Adds an arbitrary raw SQL statement.</summary>
        public SchemaBuilder Sql(string rawSql)
        {
            if (!string.IsNullOrWhiteSpace(rawSql))
                _statements.Add(rawSql);
            return this;
        }

        private string Quote(string identifier)
        {
            // Support schema.table
            return string.Join(".", identifier.Split('.').Select(p => _dialect.QuoteIdentifier(p)));
        }
    }

    /// <summary>
    /// Builds the column list (and primary-key set) for a CREATE TABLE statement.
    /// </summary>
    public class TableBuilder
    {
        private readonly ISqlDialect _dialect;
        private readonly List<ColumnBuilder> _columns = new List<ColumnBuilder>();

        internal TableBuilder(ISqlDialect dialect) { _dialect = dialect; }

        public ColumnBuilder Column(string name, string sqlType)
        {
            var cb = new ColumnBuilder(_dialect).Named(name, sqlType);
            _columns.Add(cb);
            return cb;
        }

        public ColumnBuilder Int(string name) => Column(name, _dialect.GetDbType(typeof(int)));
        public ColumnBuilder Long(string name) => Column(name, _dialect.GetDbType(typeof(long)));
        public ColumnBuilder Bool(string name) => Column(name, _dialect.GetDbType(typeof(bool)));
        public ColumnBuilder Decimal(string name) => Column(name, _dialect.GetDbType(typeof(decimal)));
        public ColumnBuilder DateTime(string name) => Column(name, _dialect.GetDbType(typeof(DateTime)));
        public ColumnBuilder String(string name, int? maxLength = null)
        {
            string type;
            if (_dialect.ProviderName == "SQL Server")
                type = maxLength.HasValue ? $"NVARCHAR({maxLength})" : "NVARCHAR(MAX)";
            else if (_dialect.ProviderName == "MySQL")
                type = maxLength.HasValue ? $"VARCHAR({maxLength})" : "TEXT";
            else
                type = maxLength.HasValue ? $"VARCHAR({maxLength})" : "TEXT";
            return Column(name, type);
        }

        internal List<string> BuildColumnDefinitions() => _columns.Select(c => c.Build()).ToList();

        internal List<string> PrimaryKeyColumns =>
            _columns.Where(c => c.IsPrimaryKey && !c.IsInlinePrimaryKey).Select(c => c.Name).ToList();
    }

    /// <summary>
    /// Builds a single column definition with type, nullability, identity, default, and PK flags.
    /// </summary>
    public class ColumnBuilder
    {
        private readonly ISqlDialect _dialect;
        private string _name;
        private string _type;
        private bool _notNull;
        private bool _identity;
        private bool _primaryKey;
        private bool _unique;
        private string _default;

        internal ColumnBuilder(ISqlDialect dialect) { _dialect = dialect; }

        /// <summary>
        /// Sets the column name and raw SQL type (e.g. "INTEGER", "NVARCHAR(100)").
        /// Used by AddColumn and advanced scenarios.
        /// </summary>
        public ColumnBuilder Named(string name, string type)
        {
            _name = name;
            _type = type;
            return this;
        }

        public string Name => _name;
        public bool IsPrimaryKey => _primaryKey;
        /// <summary>True when the PK is emitted inline (single identity PK on SQL Server/SQLite).</summary>
        public bool IsInlinePrimaryKey { get; private set; }

        public ColumnBuilder NotNull() { _notNull = true; return this; }
        public ColumnBuilder Identity() { _identity = true; return this; }
        public ColumnBuilder Unique() { _unique = true; return this; }
        public ColumnBuilder Default(string defaultExpr) { _default = defaultExpr; return this; }
        public ColumnBuilder PrimaryKey() { _primaryKey = true; _notNull = true; return this; }

        internal string Build()
        {
            var sb = new StringBuilder();
            sb.Append($"{_dialect.QuoteIdentifier(_name)} {_type}");

            // Identity / auto-increment
            if (_identity)
            {
                if (_dialect.ProviderName == "SQLite")
                {
                    // SQLite: INTEGER PRIMARY KEY AUTOINCREMENT must be inline.
                    sb.Clear();
                    sb.Append($"{_dialect.QuoteIdentifier(_name)} INTEGER PRIMARY KEY AUTOINCREMENT");
                    IsInlinePrimaryKey = true;
                    return sb.ToString();
                }
                sb.Append($" {_dialect.GetAutoIncrementSql()}");
            }

            if (_notNull) sb.Append(" NOT NULL");
            if (_unique) sb.Append(" UNIQUE");
            if (!string.IsNullOrEmpty(_default)) sb.Append($" DEFAULT {_default}");

            // Inline single-column PK for SQL Server identity is fine, but we emit a table-level PK
            // for the general case (handled by TableBuilder). For SQLite identity we already returned.
            return sb.ToString();
        }
    }
}
