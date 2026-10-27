using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ZeroData.Sql.Dialects;

namespace ZeroData.Sql.Migrations
{
    /// <summary>
    /// Applies and reverts <see cref="Migration"/> instances against a database, tracking applied
    /// migrations in a history table (default name: __LiteSqlMigrations). Dialect-aware; tested on
    /// SQL Server and SQLite.
    /// </summary>
    public class MigrationRunner
    {
        private readonly IDbConnection _connection;
        private readonly ISqlDialect _dialect;
        private readonly string _historyTable;

        public MigrationRunner(IDbConnection connection, ISqlDialect dialect, string historyTable = "__LiteSqlMigrations")
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
            _historyTable = historyTable;
        }

        /// <summary>
        /// Applies all migrations whose Id has not yet been recorded, in ascending Id order.
        /// Each migration runs in its own transaction. Returns the Ids applied.
        /// </summary>
        public IReadOnlyList<string> MigrateUp(IEnumerable<Migration> migrations)
        {
            EnsureOpen();
            EnsureHistoryTable();

            var applied = new HashSet<string>(GetAppliedIds(), StringComparer.Ordinal);
            var pending = migrations
                .Where(m => !applied.Contains(m.Id))
                .OrderBy(m => m.Id, StringComparer.Ordinal)
                .ToList();

            var result = new List<string>();
            foreach (var migration in pending)
            {
                var schema = new SchemaBuilder(_dialect);
                migration.Up(schema);

                using (var tx = _connection.BeginTransaction())
                {
                    try
                    {
                        foreach (var stmt in schema.Statements)
                            _connection.Execute(stmt, transaction: tx);

                        RecordApplied(migration.Id, tx);
                        tx.Commit();
                        result.Add(migration.Id);
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Reverts the most recently applied migration (or a specific one) using its Down().
        /// </summary>
        public bool MigrateDown(Migration migration)
        {
            if (migration == null) throw new ArgumentNullException(nameof(migration));
            EnsureOpen();
            EnsureHistoryTable();

            if (!GetAppliedIds().Contains(migration.Id)) return false;

            var schema = new SchemaBuilder(_dialect);
            migration.Down(schema);

            using (var tx = _connection.BeginTransaction())
            {
                try
                {
                    foreach (var stmt in schema.Statements)
                        _connection.Execute(stmt, transaction: tx);

                    var del = $"DELETE FROM {Quote(_historyTable)} WHERE {_dialect.QuoteIdentifier("MigrationId")} = @id";
                    _connection.Execute(del, new { id = migration.Id }, transaction: tx);
                    tx.Commit();
                    return true;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        /// <summary>Returns the Ids of migrations already applied, in applied order.</summary>
        public List<string> GetAppliedIds()
        {
            EnsureOpen();
            EnsureHistoryTable();
            var sql = $"SELECT {_dialect.QuoteIdentifier("MigrationId")} FROM {Quote(_historyTable)} ORDER BY {_dialect.QuoteIdentifier("AppliedOn")}";
            return _connection.Query<string>(sql).ToList();
        }

        private void EnsureHistoryTable()
        {
            var table = Quote(_historyTable);
            var idCol = _dialect.QuoteIdentifier("MigrationId");
            var dateCol = _dialect.QuoteIdentifier("AppliedOn");
            var dateType = _dialect.GetDbType(typeof(DateTime));
            var idType = _dialect.ProviderName == "SQL Server" ? "NVARCHAR(200)" : "VARCHAR(200)";

            var createSql =
                $"CREATE TABLE {table} ({idCol} {idType} NOT NULL PRIMARY KEY, {dateCol} {dateType} NOT NULL)";

            // Guard with existence check per dialect.
            if (_dialect.ProviderName == "SQL Server")
            {
                _connection.Execute(
                    $"IF OBJECT_ID('{_historyTable}', 'U') IS NULL BEGIN {createSql} END");
            }
            else
            {
                // SQLite / MySQL / PostgreSQL all support IF NOT EXISTS.
                _connection.Execute(createSql.Replace("CREATE TABLE", "CREATE TABLE IF NOT EXISTS"));
            }
        }

        private void RecordApplied(string migrationId, IDbTransaction tx)
        {
            var sql = $"INSERT INTO {Quote(_historyTable)} " +
                      $"({_dialect.QuoteIdentifier("MigrationId")}, {_dialect.QuoteIdentifier("AppliedOn")}) " +
                      $"VALUES (@id, @on)";
            _connection.Execute(sql, new { id = migrationId, on = DateTime.UtcNow }, transaction: tx);
        }

        private void EnsureOpen()
        {
            if (_connection.State != ConnectionState.Open)
                _connection.Open();
        }

        private string Quote(string identifier)
            => string.Join(".", identifier.Split('.').Select(p => _dialect.QuoteIdentifier(p)));
    }
}
