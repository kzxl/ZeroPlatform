using System;
using System.Collections.Generic;

namespace ZeroStorage.Core.TimeSeries
{
    /// <summary>
    /// High-performance, zero-allocation columnar container for multi-metric time-series data.
    /// Stores data in parallel typed contiguous arrays to completely eliminate boxing/unboxing overhead.
    /// </summary>
    public sealed class ColumnarBatch
    {
        public MultiMetricSchema Schema { get; }
        public int RowCount { get; private set; }
        public int Capacity => Timestamps.Length;
        public long[] Timestamps { get; }

        private readonly Array[] _columns;

        public ColumnarBatch(MultiMetricSchema schema, int capacity)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity cannot be negative.");

            Timestamps = new long[capacity];
            _columns = new Array[schema.Fields.Count];

            for (int f = 0; f < schema.Fields.Count; f++)
            {
                switch (schema.Fields[f].Type)
                {
                    case MultiMetricType.Float64:
                        _columns[f] = new double[capacity];
                        break;
                    case MultiMetricType.Int64:
                        _columns[f] = new long[capacity];
                        break;
                    case MultiMetricType.Int32:
                        _columns[f] = new int[capacity];
                        break;
                    case MultiMetricType.Boolean:
                        _columns[f] = new bool[capacity];
                        break;
                    case MultiMetricType.Decimal:
                        _columns[f] = new decimal[capacity];
                        break;
                    case MultiMetricType.String:
                        _columns[f] = new string[capacity];
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported field type '{schema.Fields[f].Type}' at index {f}.");
                }
            }

            RowCount = 0;
        }

        public void SetRowCount(int count)
        {
            if (count < 0 || count > Capacity)
                throw new ArgumentOutOfRangeException(nameof(count), $"RowCount must be between 0 and Capacity ({Capacity}).");
            RowCount = count;
        }

        #region Column Accessors by Index

        public double[] GetFloat64Column(int fieldIndex)
        {
            ValidateFieldIndex(fieldIndex, MultiMetricType.Float64);
            return (double[])_columns[fieldIndex];
        }

        public Span<double> GetFloat64Span(int fieldIndex) => GetFloat64Column(fieldIndex).AsSpan(0, RowCount);

        public long[] GetInt64Column(int fieldIndex)
        {
            ValidateFieldIndex(fieldIndex, MultiMetricType.Int64);
            return (long[])_columns[fieldIndex];
        }

        public Span<long> GetInt64Span(int fieldIndex) => GetInt64Column(fieldIndex).AsSpan(0, RowCount);

        public int[] GetInt32Column(int fieldIndex)
        {
            ValidateFieldIndex(fieldIndex, MultiMetricType.Int32);
            return (int[])_columns[fieldIndex];
        }

        public Span<int> GetInt32Span(int fieldIndex) => GetInt32Column(fieldIndex).AsSpan(0, RowCount);

        public bool[] GetBooleanColumn(int fieldIndex)
        {
            ValidateFieldIndex(fieldIndex, MultiMetricType.Boolean);
            return (bool[])_columns[fieldIndex];
        }

        public Span<bool> GetBooleanSpan(int fieldIndex) => GetBooleanColumn(fieldIndex).AsSpan(0, RowCount);

        public decimal[] GetDecimalColumn(int fieldIndex)
        {
            ValidateFieldIndex(fieldIndex, MultiMetricType.Decimal);
            return (decimal[])_columns[fieldIndex];
        }

        public Span<decimal> GetDecimalSpan(int fieldIndex) => GetDecimalColumn(fieldIndex).AsSpan(0, RowCount);

        public string[] GetStringColumn(int fieldIndex)
        {
            ValidateFieldIndex(fieldIndex, MultiMetricType.String);
            return (string[])_columns[fieldIndex];
        }

        #endregion

        #region Column Accessors by Name

        public int FindFieldIndex(string fieldName)
        {
            for (int i = 0; i < Schema.Fields.Count; i++)
            {
                if (string.Equals(Schema.Fields[i].Name, fieldName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            throw new KeyNotFoundException($"Field '{fieldName}' does not exist in schema.");
        }

        public double[] GetFloat64Column(string fieldName) => GetFloat64Column(FindFieldIndex(fieldName));
        public long[] GetInt64Column(string fieldName) => GetInt64Column(FindFieldIndex(fieldName));
        public int[] GetInt32Column(string fieldName) => GetInt32Column(FindFieldIndex(fieldName));
        public bool[] GetBooleanColumn(string fieldName) => GetBooleanColumn(FindFieldIndex(fieldName));
        public decimal[] GetDecimalColumn(string fieldName) => GetDecimalColumn(FindFieldIndex(fieldName));
        public string[] GetStringColumn(string fieldName) => GetStringColumn(FindFieldIndex(fieldName));

        #endregion

        #region Conversions and Interop

        public static ColumnarBatch FromRows(MultiMetricSchema schema, IReadOnlyList<MultiMetricRow> rows)
        {
            if (schema == null) throw new ArgumentNullException(nameof(schema));
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            var batch = new ColumnarBatch(schema, rows.Count);
            batch.SetRowCount(rows.Count);

            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                batch.Timestamps[r] = row.TimestampMs;

                for (int f = 0; f < schema.Fields.Count; f++)
                {
                    var val = row.Values[f];
                    switch (schema.Fields[f].Type)
                    {
                        case MultiMetricType.Float64:
                            ((double[])batch._columns[f])[r] = val is double d ? d : (val == null ? 0.0 : Convert.ToDouble(val));
                            break;
                        case MultiMetricType.Int64:
                            ((long[])batch._columns[f])[r] = val is long l ? l : (val == null ? 0L : Convert.ToInt64(val));
                            break;
                        case MultiMetricType.Int32:
                            ((int[])batch._columns[f])[r] = val is int iv ? iv : (val == null ? 0 : Convert.ToInt32(val));
                            break;
                        case MultiMetricType.Boolean:
                            ((bool[])batch._columns[f])[r] = val is bool b && b;
                            break;
                        case MultiMetricType.Decimal:
                            ((decimal[])batch._columns[f])[r] = val is decimal dec ? dec : (val == null ? 0m : Convert.ToDecimal(val));
                            break;
                        case MultiMetricType.String:
                            ((string[])batch._columns[f])[r] = val?.ToString() ?? string.Empty;
                            break;
                    }
                }
            }

            return batch;
        }

        public List<MultiMetricRow> ToRows()
        {
            var rows = new List<MultiMetricRow>(RowCount);
            int fieldCount = Schema.Fields.Count;

            for (int r = 0; r < RowCount; r++)
            {
                var values = new object?[fieldCount];
                for (int f = 0; f < fieldCount; f++)
                {
                    switch (Schema.Fields[f].Type)
                    {
                        case MultiMetricType.Float64:
                            values[f] = ((double[])_columns[f])[r];
                            break;
                        case MultiMetricType.Int64:
                            values[f] = ((long[])_columns[f])[r];
                            break;
                        case MultiMetricType.Int32:
                            values[f] = ((int[])_columns[f])[r];
                            break;
                        case MultiMetricType.Boolean:
                            values[f] = ((bool[])_columns[f])[r];
                            break;
                        case MultiMetricType.Decimal:
                            values[f] = ((decimal[])_columns[f])[r];
                            break;
                        case MultiMetricType.String:
                            values[f] = ((string[])_columns[f])[r];
                            break;
                    }
                }
                rows.Add(new MultiMetricRow(Timestamps[r], values));
            }

            return rows;
        }

        private void ValidateFieldIndex(int index, MultiMetricType expectedType)
        {
            if (index < 0 || index >= Schema.Fields.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range [0..{Schema.Fields.Count - 1}].");
            if (Schema.Fields[index].Type != expectedType)
                throw new InvalidOperationException($"Field at index {index} is '{Schema.Fields[index].Type}', expected '{expectedType}'.");
        }

        #endregion
    }
}
