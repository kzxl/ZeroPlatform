using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ZeroData.Core;
using ZeroPrimitives;

namespace ZeroReports.Data
{
    /// <summary>
    /// Factory helpers for constructing report data sources from collections, objects, and columnar DataFrames.
    /// </summary>
    public static class ReportDataSource
    {
        public static IReportDataSource FromDataFrame(DataFrame dataFrame)
        {
            return new DataFrameReportDataSource(dataFrame);
        }

        public static IReportDataSource FromList<T>(IEnumerable<T> items)
        {
            return new ObjectReportDataSource<T>(items);
        }

        public static IReportDataSource FromEnumerable(IEnumerable items)
        {
            if (items == null) return new ObjectReportDataSource<object>(Array.Empty<object>());
            var list = new List<object>();
            foreach (var it in items)
            {
                if (it != null) list.Add(it);
            }
            return new ObjectReportDataSource<object>(list);
        }

        public static IReportDataSource FromDictionaries(IEnumerable<IReadOnlyDictionary<string, object?>> rows)
        {
            return new DictionaryReportDataSource(rows);
        }

        /// <summary>
        /// Groups data source records by a specified field with optional key sorting.
        /// </summary>
        public static List<DataGroup> GroupRecords(IReportDataSource source, string fieldName, bool ascending = true)
        {
            var groupsDict = new Dictionary<string, DataGroup>(StringComparer.OrdinalIgnoreCase);
            var groupList = new List<DataGroup>();

            for (int i = 0; i < source.RecordCount; i++)
            {
                object? val = source.GetValue(i, fieldName);
                string keyStr = val?.ToString() ?? string.Empty;

                if (!groupsDict.TryGetValue(keyStr, out var group))
                {
                    group = new DataGroup(fieldName, val);
                    groupsDict[keyStr] = group;
                    groupList.Add(group);
                }

                group.RecordIndices.Add(i);
            }

            groupList.Sort((a, b) =>
            {
                string sa = a.Key?.ToString() ?? string.Empty;
                string sb = b.Key?.ToString() ?? string.Empty;
                int cmp = string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase);
                return ascending ? cmp : -cmp;
            });

            return groupList;
        }
    }

    /// <summary>
    /// Strongly-typed report data source wrapping any IEnumerable using cached property reflection.
    /// </summary>
    public class ObjectReportDataSource<T> : IReportDataSource
    {
        private readonly List<T> _items;
        private readonly Dictionary<string, PropertyInfo> _properties;
        private readonly List<string> _fieldNames;

        public int RecordCount => _items.Count;
        public IReadOnlyList<string> FieldNames => _fieldNames;

        public ObjectReportDataSource(IEnumerable<T> items)
        {
            _items = new List<T>(items ?? Array.Empty<T>());
            _properties = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            _fieldNames = new List<string>();

            var itemType = typeof(T);
            if (itemType == typeof(object) && _items.Count > 0 && _items[0] != null)
            {
                itemType = _items[0]!.GetType();
            }

            var props = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in props)
            {
                _properties[p.Name] = p;
                _fieldNames.Add(p.Name);
            }
        }

        public object? GetValue(int recordIndex, string fieldName)
        {
            if (recordIndex < 0 || recordIndex >= _items.Count) return null;
            var item = _items[recordIndex];
            if (item == null) return null;

            if (_properties.TryGetValue(fieldName, out var prop))
            {
                return prop.GetValue(item, null);
            }

            return null;
        }
    }

    /// <summary>
    /// Dynamic report data source wrapping key-value dictionaries.
    /// </summary>
    public class DictionaryReportDataSource : IReportDataSource
    {
        private readonly List<IReadOnlyDictionary<string, object?>> _rows;
        private readonly List<string> _fieldNames;

        public int RecordCount => _rows.Count;
        public IReadOnlyList<string> FieldNames => _fieldNames;

        public DictionaryReportDataSource(IEnumerable<IReadOnlyDictionary<string, object?>> rows)
        {
            _rows = new List<IReadOnlyDictionary<string, object?>>(rows ?? Array.Empty<IReadOnlyDictionary<string, object?>>());
            var fieldSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in _rows)
            {
                foreach (var k in row.Keys)
                {
                    fieldSet.Add(k);
                }
            }

            _fieldNames = new List<string>(fieldSet);
        }

        public object? GetValue(int recordIndex, string fieldName)
        {
            if (recordIndex < 0 || recordIndex >= _rows.Count) return null;
            var row = _rows[recordIndex];
            if (row != null && row.TryGetValue(fieldName, out var val))
            {
                return val;
            }
            return null;
        }
    }

    /// <summary>
    /// High-performance report data source wrapping a columnar ZeroData.Core DataFrame with zero memory copying.
    /// </summary>
    public class DataFrameReportDataSource : IReportDataSource
    {
        private readonly DataFrame _df;

        public int RecordCount => _df.RowCount;
        public IReadOnlyList<string> FieldNames => _df.ColumnNames;

        public DataFrameReportDataSource(DataFrame df)
        {
            _df = df ?? throw new ArgumentNullException(nameof(df));
        }

        public object? GetValue(int recordIndex, string fieldName)
        {
            if (recordIndex < 0 || recordIndex >= _df.RowCount) return null;
            if (!_df.HasColumn(fieldName)) return null;
            return _df.GetColumn(fieldName).GetValue(recordIndex);
        }
    }
}
