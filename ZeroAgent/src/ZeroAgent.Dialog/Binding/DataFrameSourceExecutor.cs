using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroData.Core;

namespace ZeroAgent.Dialog.Binding
{
    /// <summary>
    /// In-memory fast tabular query executor using ZeroData.DataFrame.
    /// Operates at microsecond speeds without socket I/O.
    /// </summary>
    public sealed class DataFrameSourceExecutor : IDataSourceExecutor
    {
        private readonly ConcurrentDictionary<string, DataFrame> _dataFrames
            = new ConcurrentDictionary<string, DataFrame>(StringComparer.OrdinalIgnoreCase);

        public string ProviderName => "DataFrame";

        public void RegisterDataFrame(string name, DataFrame dataFrame)
        {
            if (string.IsNullOrWhiteSpace(name) || dataFrame == null) return;
            _dataFrames[name] = dataFrame;
        }

        public Task<QueryResult> ExecuteAsync(
            string connectionKey,
            string query,
            IReadOnlyDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
        {
            string targetKey = !string.IsNullOrWhiteSpace(connectionKey) && _dataFrames.ContainsKey(connectionKey)
                ? connectionKey
                : _dataFrames.Keys.FirstOrDefault(k => query.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) ?? connectionKey;

            if (!_dataFrames.TryGetValue(targetKey, out var df) || df.RowCount == 0)
            {
                return Task.FromResult(QueryResult.Succeeded(Array.Empty<IReadOnlyDictionary<string, object?>>()));
            }

            var rows = new List<IReadOnlyDictionary<string, object?>>();
            var colNames = df.ColumnNames;

            for (int r = 0; r < df.RowCount; r++)
            {
                bool match = true;
                if (parameters != null && parameters.Count > 0)
                {
                    foreach (var kvp in parameters)
                    {
                        string colName = kvp.Key.TrimStart('@');
                        if (df.HasColumn(colName) && kvp.Value != null)
                        {
                            object? val = df[colName].GetValue(r);
                            string cellStr = val?.ToString() ?? string.Empty;
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
                    var rowDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var col in colNames)
                    {
                        rowDict[col] = df[col].GetValue(r);
                    }
                    rows.Add(rowDict);
                }
            }

            return Task.FromResult(QueryResult.Succeeded(rows));
        }
    }
}
