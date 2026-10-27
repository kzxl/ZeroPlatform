using System;
using System.Collections.Generic;

namespace ZeroReports.Data
{
    /// <summary>
    /// Abstraction for tabular report data sources supporting field access, grouping, and aggregations.
    /// </summary>
    public interface IReportDataSource
    {
        int RecordCount { get; }
        IReadOnlyList<string> FieldNames { get; }
        object? GetValue(int recordIndex, string fieldName);
    }

    /// <summary>
    /// Represents a grouped segment of records in a report data source.
    /// </summary>
    public class DataGroup
    {
        public string FieldName { get; }
        public object? Key { get; }
        public List<int> RecordIndices { get; } = new List<int>();

        public DataGroup(string fieldName, object? key)
        {
            FieldName = fieldName;
            Key = key;
        }
    }
}
