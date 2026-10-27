using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroAgent.Dialog.Binding
{
    /// <summary>
    /// In-memory parameterized database source executor for testing, prototyping, and ERP simulation.
    /// Stores table rowsets and filters them based on matching parameter keys.
    /// </summary>
    public sealed class MockDbSourceExecutor : IDataSourceExecutor
    {
        private readonly ConcurrentDictionary<string, List<Dictionary<string, object?>>> _tables
            = new ConcurrentDictionary<string, List<Dictionary<string, object?>>>(StringComparer.OrdinalIgnoreCase);

        public string ProviderName { get; }

        public MockDbSourceExecutor(string providerName = "SqlServer")
        {
            ProviderName = providerName ?? "SqlServer";
        }

        public void AddTable(string tableName, IEnumerable<Dictionary<string, object?>> rows)
        {
            if (string.IsNullOrWhiteSpace(tableName)) return;
            var list = _tables.GetOrAdd(tableName, _ => new List<Dictionary<string, object?>>());
            lock (list)
            {
                list.AddRange(rows);
            }
        }

        public Task<QueryResult> ExecuteAsync(
            string connectionKey,
            string query,
            IReadOnlyDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Task.FromResult(QueryResult.Failed("Query template cannot be empty."));
            }

            // Find referenced table name
            string? matchedTable = _tables.Keys.FirstOrDefault(k => query.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
            if (matchedTable == null || !_tables.TryGetValue(matchedTable, out var rows))
            {
                // If table not found, return empty set or first available
                return Task.FromResult(QueryResult.Succeeded(Array.Empty<IReadOnlyDictionary<string, object?>>()));
            }

            var results = new List<IReadOnlyDictionary<string, object?>>();

            lock (rows)
            {
                foreach (var row in rows)
                {
                    bool match = true;
                    if (parameters != null && parameters.Count > 0)
                    {
                        foreach (var kvp in parameters)
                        {
                            string colName = kvp.Key.TrimStart('@');
                            if (kvp.Value != null && row.TryGetValue(colName, out var cellValue))
                            {
                                string cellStr = cellValue?.ToString() ?? string.Empty;
                                string paramStr = kvp.Value.ToString() ?? string.Empty;
                                if (!cellStr.Equals(paramStr, StringComparison.OrdinalIgnoreCase) &&
                                    cellStr.IndexOf(paramStr, StringComparison.OrdinalIgnoreCase) < 0)
                                {
                                    match = false;
                                    break;
                                }
                            }
                        }
                    }

                    if (match)
                    {
                        results.Add(row);
                    }
                }
            }

            return Task.FromResult(QueryResult.Succeeded(results));
        }
    }
}
