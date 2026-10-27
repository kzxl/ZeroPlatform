using System;
using System.Collections.Generic;
using System.Globalization;
using ZeroData.Core;
using ZeroUI.Core.Common;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// High-performance, zero-copy virtual data adapter wrapping ZeroData.Core.DataFrame.
    /// Provides register-speed access directly from contiguous column memory buffers without heap boxing.
    /// </summary>
    public class DataFrameSource : IZeroVirtualSource, IZeroSortableSource, IZeroEditableSource, IZeroItemSource
    {
        private readonly DataFrame _dataFrame;
        private readonly List<DataFrameColumnBinding> _bindings = new List<DataFrameColumnBinding>();

        public DataFrame DataFrame => _dataFrame;

        public int TotalRowCount => _dataFrame.RowCount;
        public int TotalColumnCount => _bindings.Count;

        public DataFrameSource(DataFrame dataFrame)
        {
            _dataFrame = dataFrame ?? throw new ArgumentNullException(nameof(dataFrame));
            AutoGenerateBindings();
        }

        public DataFrameSource(DataFrame dataFrame, IEnumerable<ZeroColumn> columns)
        {
            _dataFrame = dataFrame ?? throw new ArgumentNullException(nameof(dataFrame));
            if (columns != null)
            {
                ConfigureFromColumns(columns);
            }
            else
            {
                AutoGenerateBindings();
            }
        }

        public RowView GetRowView(int index) => new RowView(_dataFrame, index);

        object? IZeroItemSource.GetItem(int index)
        {
            if ((uint)index >= (uint)_dataFrame.RowCount) return null;
            var dict = new Dictionary<string, object?>(_dataFrame.ColumnCount, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _dataFrame.ColumnCount; i++)
            {
                dict[_dataFrame.ColumnNames[i]] = _dataFrame.GetColumnByIndex(i).GetValue(index);
            }
            return dict;
        }

        public void GetCellValue(int rowIndex, int columnIndex, ref CellValueBuffer buffer)
        {
            if ((uint)rowIndex >= (uint)_dataFrame.RowCount || (uint)columnIndex >= (uint)_bindings.Count)
            {
                return;
            }

            var binding = _bindings[columnIndex];
            var col = binding.Column;

            if (col.HasNulls && col.IsNull(rowIndex))
            {
                buffer.Text = ReadOnlySpan<char>.Empty;
                buffer.Alignment = binding.Alignment;
                return;
            }

            // High-speed string dictionary resolution
            if (binding.StringDictColumn != null)
            {
                string? str = binding.StringDictColumn.GetString(rowIndex);
                buffer.Text = str != null ? str.AsSpan() : ReadOnlySpan<char>.Empty;
                buffer.Alignment = binding.Alignment;
                return;
            }

            // Primitive typed columns without boxing
            object? val = col.GetValue(rowIndex);
            if (val == null)
            {
                buffer.Text = ReadOnlySpan<char>.Empty;
                buffer.Alignment = binding.Alignment;
                return;
            }

            string text = FormatValue(val, binding.FormatString);
            buffer.Text = text.AsSpan();
            buffer.Alignment = binding.Alignment;
        }

        public int CompareRows(int rowA, int rowB, int columnIndex)
        {
            if ((uint)rowA >= (uint)_dataFrame.RowCount || (uint)rowB >= (uint)_dataFrame.RowCount ||
                (uint)columnIndex >= (uint)_bindings.Count)
            {
                return 0;
            }

            var binding = _bindings[columnIndex];
            var col = binding.Column;

            bool isNullA = col.HasNulls && col.IsNull(rowA);
            bool isNullB = col.HasNulls && col.IsNull(rowB);

            if (isNullA && isNullB) return 0;
            if (isNullA) return -1;
            if (isNullB) return 1;

            if (binding.StringDictColumn != null)
            {
                string? strA = binding.StringDictColumn.GetString(rowA);
                string? strB = binding.StringDictColumn.GetString(rowB);
                return string.Compare(strA, strB, StringComparison.OrdinalIgnoreCase);
            }

            object? valA = col.GetValue(rowA);
            object? valB = col.GetValue(rowB);

            if (valA is IComparable compA && valB != null)
            {
                try
                {
                    return compA.CompareTo(valB);
                }
                catch
                {
                    // Fallback
                }
            }

            return string.Compare(valA?.ToString(), valB?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public bool IsCellEditable(int rowIndex, int columnIndex)
        {
            if ((uint)columnIndex >= (uint)_bindings.Count) return false;
            return !_bindings[columnIndex].ReadOnly;
        }

        public bool SetCellValue(int rowIndex, int columnIndex, string textValue)
        {
            if ((uint)rowIndex >= (uint)_dataFrame.RowCount || (uint)columnIndex >= (uint)_bindings.Count)
            {
                return false;
            }

            var binding = _bindings[columnIndex];
            if (binding.ReadOnly) return false;

            try
            {
                var col = binding.Column;
                if (string.IsNullOrEmpty(textValue))
                {
                    col.SetNull(rowIndex);
                }
                else
                {
                    if (binding.StringDictColumn != null)
                    {
                        binding.StringDictColumn.SetString(rowIndex, textValue);
                    }
                    else
                    {
                        object parsed = Convert.ChangeType(textValue, binding.DataType, CultureInfo.CurrentCulture);
                        col.SetValue(rowIndex, parsed);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public List<ZeroColumn> GenerateColumns()
        {
            var cols = new List<ZeroColumn>();
            for (int i = 0; i < _dataFrame.ColumnCount; i++)
            {
                var col = _dataFrame.GetColumnByIndex(i);
                var zc = new ZeroColumn(col.Name, col.Name)
                {
                    ColumnType = ResolveColumnType(col.DataType),
                    Alignment = ResolveAlignment(col.DataType),
                    Width = Math.Max(90, Math.Min(250, col.Name.Length * 12 + 30))
                };
                cols.Add(zc);
            }
            return cols;
        }

        private void AutoGenerateBindings()
        {
            _bindings.Clear();
            for (int i = 0; i < _dataFrame.ColumnCount; i++)
            {
                var col = _dataFrame.GetColumnByIndex(i);
                _bindings.Add(new DataFrameColumnBinding
                {
                    ColumnName = col.Name,
                    ColumnOrdinal = i,
                    Column = col,
                    StringDictColumn = col as StringDictionaryColumn,
                    DataType = col.DataType,
                    Alignment = ResolveAlignment(col.DataType),
                    ReadOnly = false
                });
            }
        }

        private void ConfigureFromColumns(IEnumerable<ZeroColumn> columns)
        {
            _bindings.Clear();
            foreach (var zc in columns)
            {
                if (string.IsNullOrEmpty(zc.FieldName)) continue;

                if (_dataFrame.HasColumn(zc.FieldName))
                {
                    var col = _dataFrame[zc.FieldName];
                    int ordinal = -1;
                    for (int i = 0; i < _dataFrame.ColumnCount; i++)
                    {
                        if (string.Equals(_dataFrame.ColumnNames[i], zc.FieldName, StringComparison.OrdinalIgnoreCase))
                        {
                            ordinal = i;
                            break;
                        }
                    }

                    _bindings.Add(new DataFrameColumnBinding
                    {
                        ColumnName = col.Name,
                        ColumnOrdinal = ordinal,
                        Column = col,
                        StringDictColumn = col as StringDictionaryColumn,
                        DataType = col.DataType,
                        Alignment = zc.Alignment != CellAlignment.Left ? zc.Alignment : ResolveAlignment(col.DataType),
                        FormatString = zc.DisplayFormat,
                        ReadOnly = false
                    });
                }
            }
        }

        private static CellAlignment ResolveAlignment(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying == typeof(bool)) return CellAlignment.Center;
            if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) || underlying == typeof(TimeSpan))
                return CellAlignment.Center;

            if (underlying == typeof(byte) || underlying == typeof(sbyte) ||
                underlying == typeof(short) || underlying == typeof(ushort) ||
                underlying == typeof(int) || underlying == typeof(uint) ||
                underlying == typeof(long) || underlying == typeof(ulong) ||
                underlying == typeof(float) || underlying == typeof(double) ||
                underlying == typeof(decimal))
            {
                return CellAlignment.Right;
            }

            return CellAlignment.Left;
        }

        private static GridColumnType ResolveColumnType(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying == typeof(bool)) return GridColumnType.Boolean;
            if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset)) return GridColumnType.DateTime;
            if (underlying == typeof(byte) || underlying == typeof(sbyte) ||
                underlying == typeof(short) || underlying == typeof(ushort) ||
                underlying == typeof(int) || underlying == typeof(uint) ||
                underlying == typeof(long) || underlying == typeof(ulong) ||
                underlying == typeof(float) || underlying == typeof(double) ||
                underlying == typeof(decimal))
            {
                return GridColumnType.Numeric;
            }
            return GridColumnType.Text;
        }

        private static string FormatValue(object val, string? format)
        {
            if (!string.IsNullOrEmpty(format) && val is IFormattable formattable)
            {
                return formattable.ToString(format, CultureInfo.CurrentCulture);
            }
            return val.ToString() ?? string.Empty;
        }

        private sealed class DataFrameColumnBinding
        {
            public string ColumnName { get; set; } = string.Empty;
            public int ColumnOrdinal { get; set; }
            public IDataColumn Column { get; set; } = null!;
            public StringDictionaryColumn? StringDictColumn { get; set; }
            public Type DataType { get; set; } = typeof(object);
            public CellAlignment Alignment { get; set; }
            public string? FormatString { get; set; }
            public bool ReadOnly { get; set; }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="DataFrameSource"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroDataFrameSource is deprecated. Use DataFrameSource instead.")]
    public class ZeroDataFrameSource : DataFrameSource
    {
        public ZeroDataFrameSource(DataFrame dataFrame) : base(dataFrame) { }
        public ZeroDataFrameSource(DataFrame dataFrame, IEnumerable<ZeroColumn> columns) : base(dataFrame, columns) { }
    }
}
