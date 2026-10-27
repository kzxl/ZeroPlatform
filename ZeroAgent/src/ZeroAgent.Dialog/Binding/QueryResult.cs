using System;
using System.Collections.Generic;

namespace ZeroAgent.Dialog.Binding
{
    /// <summary>
    /// Represents the standardized tabular result of a database or data source execution.
    /// </summary>
    public sealed class QueryResult
    {
        public bool Success { get; }
        public int RowsAffected { get; }
        public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; }
        public string? ErrorMessage { get; }

        public QueryResult(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, int rowsAffected = 0)
        {
            Success = true;
            Rows = rows ?? Array.Empty<IReadOnlyDictionary<string, object?>>();
            RowsAffected = rowsAffected > 0 ? rowsAffected : Rows.Count;
            ErrorMessage = null;
        }

        public QueryResult(string errorMessage)
        {
            Success = false;
            Rows = Array.Empty<IReadOnlyDictionary<string, object?>>();
            RowsAffected = 0;
            ErrorMessage = errorMessage ?? "Unknown database execution error";
        }

        public static QueryResult Succeeded(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, int rowsAffected = 0)
            => new QueryResult(rows, rowsAffected);

        public static QueryResult Failed(string errorMessage)
            => new QueryResult(errorMessage);
    }
}
