using System;
using ZeroPrimitives;

namespace ZeroData.Core
{
    /// <summary>
    /// Delegate for zero-allocation row evaluation using RowView ref struct.
    /// </summary>
    public delegate bool RowPredicate(RowView row);

    /// <summary>
    /// Zero-allocation, stack-only view representing a single row in a DataFrame.
    /// Eliminates per-row object allocations and boxing when iterating over millions of records.
    /// </summary>
    public readonly ref struct RowView
    {
        private readonly DataFrame _df;
        private readonly int _rowIndex;

        public int RowIndex => _rowIndex;

        public RowView(DataFrame df, int rowIndex)
        {
            _df = df;
            _rowIndex = rowIndex;
        }

        public bool IsNull(int columnIndex)
        {
            return _df.GetColumnByIndex(columnIndex).IsNull(_rowIndex);
        }

        public bool IsNull(string columnName)
        {
            return _df.GetColumn(columnName).IsNull(_rowIndex);
        }

        public int GetInt(int columnIndex)
        {
            var col = _df.GetColumnByIndex(columnIndex);
            if (col is DataColumn<int> typed) return typed[_rowIndex];
            return FastConvert.ToInt(col.GetValue(_rowIndex));
        }

        public int GetInt(string columnName)
        {
            var col = _df.GetColumn(columnName);
            if (col is DataColumn<int> typed) return typed[_rowIndex];
            return FastConvert.ToInt(col.GetValue(_rowIndex));
        }

        public long GetLong(int columnIndex)
        {
            var col = _df.GetColumnByIndex(columnIndex);
            if (col is DataColumn<long> typed) return typed[_rowIndex];
            return FastConvert.ToLong(col.GetValue(_rowIndex));
        }

        public long GetLong(string columnName)
        {
            var col = _df.GetColumn(columnName);
            if (col is DataColumn<long> typed) return typed[_rowIndex];
            return FastConvert.ToLong(col.GetValue(_rowIndex));
        }

        public double GetDouble(int columnIndex)
        {
            var col = _df.GetColumnByIndex(columnIndex);
            if (col is DataColumn<double> typed) return typed[_rowIndex];
            return FastConvert.ToDouble(col.GetValue(_rowIndex));
        }

        public double GetDouble(string columnName)
        {
            var col = _df.GetColumn(columnName);
            if (col is DataColumn<double> typed) return typed[_rowIndex];
            return FastConvert.ToDouble(col.GetValue(_rowIndex));
        }

        public decimal GetDecimal(int columnIndex)
        {
            var col = _df.GetColumnByIndex(columnIndex);
            if (col is DataColumn<decimal> typed) return typed[_rowIndex];
            return FastConvert.ToDecimal(col.GetValue(_rowIndex));
        }

        public decimal GetDecimal(string columnName)
        {
            var col = _df.GetColumn(columnName);
            if (col is DataColumn<decimal> typed) return typed[_rowIndex];
            return FastConvert.ToDecimal(col.GetValue(_rowIndex));
        }

        public string? GetString(int columnIndex)
        {
            var col = _df.GetColumnByIndex(columnIndex);
            if (col is DataColumn<string> typed) return typed[_rowIndex];
            return col.GetValue(_rowIndex)?.ToString();
        }

        public string? GetString(string columnName)
        {
            var col = _df.GetColumn(columnName);
            if (col is DataColumn<string> typed) return typed[_rowIndex];
            return col.GetValue(_rowIndex)?.ToString();
        }

        public DateTime GetDateTime(int columnIndex)
        {
            var col = _df.GetColumnByIndex(columnIndex);
            if (col is DataColumn<DateTime> typed) return typed[_rowIndex];
            return FastConvert.ToDateTime(col.GetValue(_rowIndex));
        }

        public DateTime GetDateTime(string columnName)
        {
            var col = _df.GetColumn(columnName);
            if (col is DataColumn<DateTime> typed) return typed[_rowIndex];
            return FastConvert.ToDateTime(col.GetValue(_rowIndex));
        }

        public bool GetBoolean(int columnIndex)
        {
            var col = _df.GetColumnByIndex(columnIndex);
            if (col is DataColumn<bool> typed) return typed[_rowIndex];
            return FastConvert.ToBool(col.GetValue(_rowIndex));
        }

        public bool GetBoolean(string columnName)
        {
            var col = _df.GetColumn(columnName);
            if (col is DataColumn<bool> typed) return typed[_rowIndex];
            return FastConvert.ToBool(col.GetValue(_rowIndex));
        }

        public T Get<T>(int columnIndex)
        {
            var col = _df.GetColumnByIndex(columnIndex);
            if (col is DataColumn<T> typed) return typed[_rowIndex];
            return FastConvert.To<T>(col.GetValue(_rowIndex));
        }

        public T Get<T>(string columnName)
        {
            var col = _df.GetColumn(columnName);
            if (col is DataColumn<T> typed) return typed[_rowIndex];
            return FastConvert.To<T>(col.GetValue(_rowIndex));
        }
    }

    /// <summary>
    /// Enumerator for zero-allocation foreach row traversal over a DataFrame.
    /// </summary>
    public ref struct RowEnumerator
    {
        private readonly DataFrame _df;
        private int _index;

        public RowEnumerator(DataFrame df)
        {
            _df = df;
            _index = -1;
        }

        public RowView Current => new RowView(_df, _index);

        public bool MoveNext()
        {
            int next = _index + 1;
            if (next < _df.RowCount)
            {
                _index = next;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Enumerable wrapper enabling 'foreach (var row in df.Rows)' without heap allocation.
    /// </summary>
    public readonly ref struct RowEnumerable
    {
        private readonly DataFrame _df;

        public RowEnumerable(DataFrame df)
        {
            _df = df;
        }

        public RowEnumerator GetEnumerator() => new RowEnumerator(_df);
    }
}
