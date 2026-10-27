using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ZeroUI.Core.Localization;

namespace ZeroUI.Core.Pivot
{
    /// <summary>
    /// Represents a composite key tuple defining a distinct dimensional coordinate (e.g. Country + City or Year + Quarter).
    /// </summary>
    public sealed class PivotKey : IEquatable<PivotKey>, IComparable<PivotKey>
    {
        private readonly object?[] _values;
        private readonly int _hashCode;

        public IReadOnlyList<object?> Values => _values;
        public int Length => _values.Length;

        public object? this[int index] => _values[index];

        public PivotKey(params object?[] values)
        {
            _values = values ?? Array.Empty<object?>();
            int hash = 17;
            for (int i = 0; i < _values.Length; i++)
            {
                hash = hash * 31 + (_values[i]?.GetHashCode() ?? 0);
            }
            _hashCode = hash;
        }

        public override int GetHashCode() => _hashCode;

        public bool Equals(PivotKey? other)
        {
            if (other is null || _values.Length != other._values.Length) return false;
            for (int i = 0; i < _values.Length; i++)
            {
                if (!Equals(_values[i], other._values[i])) return false;
            }
            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as PivotKey);

        public int CompareTo(PivotKey? other)
        {
            if (other is null) return 1;
            int minLen = Math.Min(_values.Length, other._values.Length);
            for (int i = 0; i < minLen; i++)
            {
                var v1 = _values[i];
                var v2 = other._values[i];
                if (v1 == null && v2 == null) continue;
                if (v1 == null) return -1;
                if (v2 == null) return 1;
                if (v1 is IComparable c1 && v2 is IComparable)
                {
                    int cmp = c1.CompareTo(v2);
                    if (cmp != 0) return cmp;
                }
                else
                {
                    int cmp = string.Compare(v1.ToString(), v2.ToString(), StringComparison.OrdinalIgnoreCase);
                    if (cmp != 0) return cmp;
                }
            }
            return _values.Length.CompareTo(other._values.Length);
        }

        public override string ToString() => string.Join(" | ", _values);
    }

    /// <summary>
    /// High-performance numerical accumulator for calculating standard OLAP aggregate measures.
    /// </summary>
    public sealed class PivotAggregateCell
    {
        public int Count { get; private set; }
        public double Sum { get; private set; }
        public double Min { get; private set; } = double.MaxValue;
        public double Max { get; private set; } = double.MinValue;

        public void Add(object? value)
        {
            if (value == null || value is DBNull) return;

            Count++;
            double num = 0.0;
            if (value is IConvertible convertible)
            {
                try
                {
                    num = convertible.ToDouble(CultureInfo.InvariantCulture);
                }
                catch
                {
                    return;
                }
            }
            else
            {
                return;
            }

            Sum += num;
            if (num < Min) Min = num;
            if (num > Max) Max = num;
        }

        public object? GetValue(PivotSummaryType type)
        {
            if (Count == 0) return null;

            switch (type)
            {
                case PivotSummaryType.Count:
                    return Count;
                case PivotSummaryType.Sum:
                    return Sum;
                case PivotSummaryType.Average:
                    return Sum / Count;
                case PivotSummaryType.Min:
                    return Min == double.MaxValue ? 0.0 : Min;
                case PivotSummaryType.Max:
                    return Max == double.MinValue ? 0.0 : Max;
                default:
                    return Sum;
            }
        }
    }

    /// <summary>
    /// Encapsulates the fully materialized cross-tab aggregation result matrix.
    /// </summary>
    public class PivotResultModel
    {
        public IReadOnlyList<PivotGridField> RowFields { get; }
        public IReadOnlyList<PivotGridField> ColumnFields { get; }
        public IReadOnlyList<PivotGridField> DataFields { get; }

        public IReadOnlyList<PivotKey> RowKeys { get; }
        public IReadOnlyList<PivotKey> ColumnKeys { get; }

        // Cells dictionary: (RowKey, ColumnKey, DataFieldIndex) -> Aggregate
        private readonly Dictionary<Tuple<PivotKey, PivotKey, int>, PivotAggregateCell> _cells;
        private readonly Dictionary<Tuple<PivotKey, int>, PivotAggregateCell> _rowTotals;
        private readonly Dictionary<Tuple<PivotKey, int>, PivotAggregateCell> _colTotals;
        private readonly Dictionary<int, PivotAggregateCell> _grandTotals;

        public int RowCount => RowKeys.Count;
        public int ColumnCount => ColumnKeys.Count;

        public PivotResultModel(
            IReadOnlyList<PivotGridField> rowFields,
            IReadOnlyList<PivotGridField> colFields,
            IReadOnlyList<PivotGridField> dataFields,
            IReadOnlyList<PivotKey> rowKeys,
            IReadOnlyList<PivotKey> colKeys,
            Dictionary<Tuple<PivotKey, PivotKey, int>, PivotAggregateCell> cells,
            Dictionary<Tuple<PivotKey, int>, PivotAggregateCell> rowTotals,
            Dictionary<Tuple<PivotKey, int>, PivotAggregateCell> colTotals,
            Dictionary<int, PivotAggregateCell> grandTotals)
        {
            RowFields = rowFields;
            ColumnFields = colFields;
            DataFields = dataFields;
            RowKeys = rowKeys;
            ColumnKeys = colKeys;
            _cells = cells;
            _rowTotals = rowTotals;
            _colTotals = colTotals;
            _grandTotals = grandTotals;
        }

        public object? GetCellValue(int rowIndex, int colIndex, int dataFieldIndex = 0)
        {
            if (rowIndex < 0 || rowIndex >= RowKeys.Count || colIndex < 0 || colIndex >= ColumnKeys.Count)
                return null;

            var rKey = RowKeys[rowIndex];
            var cKey = ColumnKeys[colIndex];
            var key = Tuple.Create(rKey, cKey, dataFieldIndex);

            if (_cells.TryGetValue(key, out var agg))
            {
                var summaryType = dataFieldIndex < DataFields.Count ? DataFields[dataFieldIndex].SummaryType : PivotSummaryType.Sum;
                return agg.GetValue(summaryType);
            }
            return null;
        }

        public object? GetRowTotal(int rowIndex, int dataFieldIndex = 0)
        {
            if (rowIndex < 0 || rowIndex >= RowKeys.Count) return null;
            var rKey = RowKeys[rowIndex];
            var key = Tuple.Create(rKey, dataFieldIndex);
            if (_rowTotals.TryGetValue(key, out var agg))
            {
                var summaryType = dataFieldIndex < DataFields.Count ? DataFields[dataFieldIndex].SummaryType : PivotSummaryType.Sum;
                return agg.GetValue(summaryType);
            }
            return null;
        }

        public object? GetColumnTotal(int colIndex, int dataFieldIndex = 0)
        {
            if (colIndex < 0 || colIndex >= ColumnKeys.Count) return null;
            var cKey = ColumnKeys[colIndex];
            var key = Tuple.Create(cKey, dataFieldIndex);
            if (_colTotals.TryGetValue(key, out var agg))
            {
                var summaryType = dataFieldIndex < DataFields.Count ? DataFields[dataFieldIndex].SummaryType : PivotSummaryType.Sum;
                return agg.GetValue(summaryType);
            }
            return null;
        }

        public object? GetGrandTotal(int dataFieldIndex = 0)
        {
            if (_grandTotals.TryGetValue(dataFieldIndex, out var agg))
            {
                var summaryType = dataFieldIndex < DataFields.Count ? DataFields[dataFieldIndex].SummaryType : PivotSummaryType.Sum;
                return agg.GetValue(summaryType);
            }
            return null;
        }

        public string FormatValue(object? value, int dataFieldIndex = 0)
        {
            if (value == null) return string.Empty;
            string? format = (dataFieldIndex < DataFields.Count) ? DataFields[dataFieldIndex].FormatString : null;
            if (!string.IsNullOrEmpty(format))
            {
                return string.Format(CultureInfo.CurrentCulture, format, value);
            }
            if (value is double d)
            {
                return d.ToString("N2", CultureInfo.CurrentCulture);
            }
            return value.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Multidimensional OLAP Cross-Tab summarization calculation engine.
    /// Takes arbitrary enumerable datasets, partitions dimensions into Row and Column axes,
    /// and aggregates measure cells with high-speed sparse-matrix indexing.
    /// </summary>
    public class PivotEngine
    {
        private readonly List<PivotGridField> _fields = new List<PivotGridField>();
        private IEnumerable? _dataSource;

        public List<PivotGridField> Fields => _fields;

        public IEnumerable? DataSource
        {
            get => _dataSource;
            set => _dataSource = value;
        }

        public PivotGridField AddField(string fieldName, PivotArea area, string? caption = null, PivotSummaryType summaryType = PivotSummaryType.Sum)
        {
            int nextIndex = 0;
            for (int i = 0; i < _fields.Count; i++)
            {
                if (_fields[i].Area == area) nextIndex++;
            }

            var field = new PivotGridField(fieldName, area, caption, summaryType)
            {
                AreaIndex = nextIndex
            };
            _fields.Add(field);
            return field;
        }

        /// <summary>
        /// Evaluates and generates the materialized cross-tab OLAP result matrix.
        /// </summary>
        public PivotResultModel Calculate()
        {
            var rowFields = new List<PivotGridField>();
            var colFields = new List<PivotGridField>();
            var dataFields = new List<PivotGridField>();

            for (int i = 0; i < _fields.Count; i++)
            {
                var f = _fields[i];
                if (!f.Visible) continue;
                switch (f.Area)
                {
                    case PivotArea.RowArea: rowFields.Add(f); break;
                    case PivotArea.ColumnArea: colFields.Add(f); break;
                    case PivotArea.DataArea: dataFields.Add(f); break;
                }
            }

            int CompareFields(PivotGridField a, PivotGridField b)
            {
                int cmp = a.AreaIndex.CompareTo(b.AreaIndex);
                if (cmp != 0) return cmp;
                return _fields.IndexOf(a).CompareTo(_fields.IndexOf(b));
            }

            rowFields.Sort(CompareFields);
            colFields.Sort(CompareFields);
            dataFields.Sort(CompareFields);

            var rowKeySet = new HashSet<PivotKey>();
            var colKeySet = new HashSet<PivotKey>();

            var cells = new Dictionary<Tuple<PivotKey, PivotKey, int>, PivotAggregateCell>();
            var rowTotals = new Dictionary<Tuple<PivotKey, int>, PivotAggregateCell>();
            var colTotals = new Dictionary<Tuple<PivotKey, int>, PivotAggregateCell>();
            var grandTotals = new Dictionary<int, PivotAggregateCell>();

            // Default empty key when no row or column fields are assigned
            var defaultEmptyKey = new PivotKey(Localizer.GetString(StringId.PivotTotal));

            if (_dataSource != null && dataFields.Count > 0)
            {
                var rowExtractors = BuildExtractors(rowFields);
                var colExtractors = BuildExtractors(colFields);
                var dataExtractors = BuildExtractors(dataFields);

                foreach (var item in _dataSource)
                {
                    if (item == null) continue;

                    // Extract Row Key
                    object?[] rVals = new object?[rowFields.Count];
                    for (int r = 0; r < rowFields.Count; r++)
                        rVals[r] = rowExtractors[r](item);
                    var rKey = rowFields.Count > 0 ? new PivotKey(rVals) : defaultEmptyKey;
                    rowKeySet.Add(rKey);

                    // Extract Column Key
                    object?[] cVals = new object?[colFields.Count];
                    for (int c = 0; c < colFields.Count; c++)
                        cVals[c] = colExtractors[c](item);
                    var cKey = colFields.Count > 0 ? new PivotKey(cVals) : defaultEmptyKey;
                    colKeySet.Add(cKey);

                    // Accumulate Data Fields
                    for (int d = 0; d < dataFields.Count; d++)
                    {
                        object? val = dataExtractors[d](item);

                        // 1. Intersection cell
                        var cellKey = Tuple.Create(rKey, cKey, d);
                        if (!cells.TryGetValue(cellKey, out var agg))
                        {
                            agg = new PivotAggregateCell();
                            cells[cellKey] = agg;
                        }
                        agg.Add(val);

                        // 2. Row Total
                        var rTotKey = Tuple.Create(rKey, d);
                        if (!rowTotals.TryGetValue(rTotKey, out var rAgg))
                        {
                            rAgg = new PivotAggregateCell();
                            rowTotals[rTotKey] = rAgg;
                        }
                        rAgg.Add(val);

                        // 3. Column Total
                        var cTotKey = Tuple.Create(cKey, d);
                        if (!colTotals.TryGetValue(cTotKey, out var cAgg))
                        {
                            cAgg = new PivotAggregateCell();
                            colTotals[cTotKey] = cAgg;
                        }
                        cAgg.Add(val);

                        // 4. Grand Total
                        if (!grandTotals.TryGetValue(d, out var gAgg))
                        {
                            gAgg = new PivotAggregateCell();
                            grandTotals[d] = gAgg;
                        }
                        gAgg.Add(val);
                    }
                }
            }

            var sortedRowKeys = new List<PivotKey>(rowKeySet);
            sortedRowKeys.Sort();
            if (rowFields.Count > 0 && rowFields[0].SortOrder == PivotSortOrder.Descending)
            {
                sortedRowKeys.Reverse();
            }

            var sortedColKeys = new List<PivotKey>(colKeySet);
            sortedColKeys.Sort();
            if (colFields.Count > 0 && colFields[0].SortOrder == PivotSortOrder.Descending)
            {
                sortedColKeys.Reverse();
            }

            return new PivotResultModel(
                rowFields,
                colFields,
                dataFields,
                sortedRowKeys,
                sortedColKeys,
                cells,
                rowTotals,
                colTotals,
                grandTotals);
        }

        private static List<Func<object, object?>> BuildExtractors(List<PivotGridField> fields)
        {
            var list = new List<Func<object, object?>>(fields.Count);
            foreach (var f in fields)
            {
                string propName = f.FieldName;
                PropertyInfo? cachedProp = null;
                Type? cachedType = null;

                list.Add(target =>
                {
                    if (target == null) return null;

                    // 1. Check IDictionary<string, object>
                    if (target is IDictionary<string, object> dict)
                    {
                        return dict.TryGetValue(propName, out var v) ? v : null;
                    }

                    // 2. Check general IDictionary
                    if (target is IDictionary idict && idict.Contains(propName))
                    {
                        return idict[propName];
                    }

                    // 3. Reflection with property caching
                    var type = target.GetType();
                    if (cachedType != type)
                    {
                        cachedType = type;
                        cachedProp = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    }

                    return cachedProp?.GetValue(target, null);
                });
            }
            return list;
        }
    }
}
