using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroAgent.Dialog.Binding
{
    /// <summary>
    /// Pluggable interface for executing parameterized queries across various enterprise data sources
    /// (SQL Server, PostgreSQL, SQLite, In-Memory DataFrame, REST API).
    /// </summary>
    public interface IDataSourceExecutor
    {
        /// <summary>
        /// The provider identification string (e.g. "SqlServer", "Postgres", "Sqlite", "DataFrame", "RestApi").
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Executes a parameterized query safely against the target connection.
        /// </summary>
        Task<QueryResult> ExecuteAsync(
            string connectionKey,
            string query,
            IReadOnlyDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default);
    }
}
