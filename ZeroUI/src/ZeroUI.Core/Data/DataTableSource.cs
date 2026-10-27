using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using ZeroUI.Core.Common;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// High-performance zero-allocation virtual data adapter wrapping ADO.NET DataTable and DataView.
    /// Provides 100% backward compatibility for legacy WinForms and WPF data grids.
    /// </summary>
    public class DataTableSource : IZeroVirtualSource, IZeroSortableSource, IZeroEditableSource, IZeroItemSource
    {
        private readonly DataTable? _table;
        private readonly DataView _view;
        private readonly List<TableColumnBinding> _bindings = new List<TableColumnBinding>();

        public DataTable? Table => _table;
        public DataView View => _view;

        public int TotalRowCount => _view.Count;
        public int TotalColumnCount => _bindings.Count;

        public DataTableSource(DataTable table)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _view = table.DefaultView;
            AutoGenerateBindings();
        }

        public DataTableSource(DataTable table, IEnumerable<ZeroColumn> columns)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _view = table.DefaultView;
            if (columns != null)
            {
                ConfigureFromColumns(columns);
            }
            else
            {
                AutoGenerateBindings();
            }
        }

        public DataTableSource(DataView view)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _table = view.Table;
            AutoGenerateBindings();
        }

        public DataTableSource(DataView view, IEnumerable<ZeroColumn> columns)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _table = view.Table;
            if (columns != null)
            {
                ConfigureFromColumns(columns);
            }
            else
            {
                AutoGenerateBindings();
            }
        }

        public DataRowView GetRowView(int index) => _view[index];

        public DataRow? GetRow(int index) => (index >= 0 && index < _view.Count) ? _view[index].Row : null;

        object? IZeroItemSource.GetItem(int index) => GetRow(index);

        public void GetCellValue(int rowIndex, int columnIndex, ref CellValueBuffer buffer)
        {
            if ((uint)rowIndex >= (uint)_view.Count || (uint)columnIndex >= (uint)_bindings.Count)
            {
                return;
            }

            var binding = _bindings[columnIndex];
            var row = _view[rowIndex];
            object val = row[binding.ColumnOrdinal];

            if (val == null || val == DBNull.Value)
            {
                buffer.Text = ReadOnlySpan<char>.Empty;
                buffer.Alignment = binding.Alignment;
                return;
            }

            string str = FormatValue(val, binding.FormatString);
            buffer.Text = str.AsSpan();
            buffer.Alignment = binding.Alignment;
        }

        public int CompareRows(int rowA, int rowB, int columnIndex)
        {
            if ((uint)rowA >= (uint)_view.Count || (uint)rowB >= (uint)_view.Count ||
                (uint)columnIndex >= (uint)_bindings.Count)
            {
                return 0;
            }

            int ordinal = _bindings[columnIndex].ColumnOrdinal;
            object valA = _view[rowA][ordinal];
            object valB = _view[rowB][ordinal];

            bool isNullA = valA == null || valA == DBNull.Value;
            bool isNullB = valB == null || valB == DBNull.Value;

            if (isNullA && isNullB) return 0;
            if (isNullA) return -1;
            if (isNullB) return 1;

            if (valA is IComparable comp)
            {
                try
                {
                    return comp.CompareTo(valB);
                }
                catch
                {
                    // Fallback to string comparison on incompatible types
                }
            }

            if (string.Compare(valA?.ToString(), valB?.ToString(), StringComparison.OrdinalIgnoreCase) is int res)
            {
                return res;
            }
            return 0;
        }

        public bool IsCellEditable(int rowIndex, int columnIndex)
        {
            if ((uint)columnIndex >= (uint)_bindings.Count) return false;
            return !_bindings[columnIndex].ReadOnly;
        }

        public bool SetCellValue(int rowIndex, int columnIndex, string textValue)
        {
            if ((uint)rowIndex >= (uint)_view.Count || (uint)columnIndex >= (uint)_bindings.Count)
            {
                return false;
            }

            var binding = _bindings[columnIndex];
            if (binding.ReadOnly) return false;

            try
            {
                var row = _view[rowIndex];
                if (string.IsNullOrEmpty(textValue))
                {
                    row[binding.ColumnOrdinal] = DBNull.Value;
                }
                else
                {
                    object parsed = Convert.ChangeType(textValue, binding.DataType, CultureInfo.CurrentCulture);
                    row[binding.ColumnOrdinal] = parsed;
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
            if (_table == null) return cols;

            for (int i = 0; i < _table.Columns.Count; i++)
            {
                var dtCol = _table.Columns[i];
                var zc = new ZeroColumn(dtCol.ColumnName, dtCol.Caption ?? dtCol.ColumnName)
                {
                    ColumnType = ResolveColumnType(dtCol.DataType),
                    Alignment = ResolveAlignment(dtCol.DataType),
                    Width = Math.Max(90, Math.Min(250, (dtCol.Caption ?? dtCol.ColumnName).Length * 12 + 30))
                };
                cols.Add(zc);
            }

            return cols;
        }

        private void AutoGenerateBindings()
        {
            _bindings.Clear();
            if (_table == null) return;

            for (int i = 0; i < _table.Columns.Count; i++)
            {
                var col = _table.Columns[i];
                _bindings.Add(new TableColumnBinding
                {
                    ColumnName = col.ColumnName,
                    ColumnOrdinal = i,
                    DataType = col.DataType,
                    Alignment = ResolveAlignment(col.DataType),
                    ReadOnly = col.ReadOnly
                });
            }
        }

        private void ConfigureFromColumns(IEnumerable<ZeroColumn> columns)
        {
            _bindings.Clear();
            if (_table == null) return;

            foreach (var zc in columns)
            {
                if (string.IsNullOrEmpty(zc.FieldName)) continue;

                int ordinal = _table.Columns.IndexOf(zc.FieldName);
                if (ordinal >= 0)
                {
                    var col = _table.Columns[ordinal];
                    _bindings.Add(new TableColumnBinding
                    {
                        ColumnName = col.ColumnName,
                        ColumnOrdinal = ordinal,
                        DataType = col.DataType,
                        Alignment = zc.Alignment != CellAlignment.Left ? zc.Alignment : ResolveAlignment(col.DataType),
                        FormatString = zc.DisplayFormat,
                        ReadOnly = col.ReadOnly
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

        private sealed class TableColumnBinding
        {
            public string ColumnName { get; set; } = string.Empty;
            public int ColumnOrdinal { get; set; }
            public Type DataType { get; set; } = typeof(object);
            public CellAlignment Alignment { get; set; }
            public string? FormatString { get; set; }
            public bool ReadOnly { get; set; }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="DataTableSource"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroDataTableSource is deprecated. Use DataTableSource instead.")]
    public class ZeroDataTableSource : DataTableSource
    {
        public ZeroDataTableSource(DataTable table) : base(table) { }
        public ZeroDataTableSource(DataTable table, IEnumerable<ZeroColumn> columns) : base(table, columns) { }
        public ZeroDataTableSource(DataView view) : base(view) { }
        public ZeroDataTableSource(DataView view, IEnumerable<ZeroColumn> columns) : base(view, columns) { }
    }
}
