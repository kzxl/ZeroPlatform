using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using ZeroUI.Core.Common;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Universal high-performance data adapter wrapping IList{T}.
    /// Automatically manages compiled property reflection, zero-copy formatting,
    /// type-safe sorting, and in-place editing.
    /// </summary>
    public class ListSource<T> : IZeroVirtualSource, IZeroSortableSource, IZeroEditableSource, IZeroItemSource
    {
        private readonly IList<T> _items;
        private readonly List<ColumnBinding> _bindings = new List<ColumnBinding>();

        public IList<T> Items => _items;

        public int TotalRowCount => _items.Count;
        public int TotalColumnCount => _bindings.Count;

        public ListSource(IList<T> items)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            AutoGenerateBindings();
        }

        public ListSource(IList<T> items, IEnumerable<ZeroColumn> columns)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            if (columns != null)
            {
                ConfigureFromColumns(columns);
            }
            else
            {
                AutoGenerateBindings();
            }
        }

        public T GetItem(int index) => _items[index];

        object? IZeroItemSource.GetItem(int index) => (index >= 0 && index < _items.Count) ? (object?)_items[index] : null;

        public void GetCellValue(int rowIndex, int columnIndex, ref CellValueBuffer buffer)
        {
            if (rowIndex < 0 || rowIndex >= _items.Count || columnIndex < 0 || columnIndex >= _bindings.Count)
            {
                return;
            }

            var item = _items[rowIndex];
            var binding = _bindings[columnIndex];

            string str = binding.Getter(item);
            buffer.Text = str.AsSpan();
            buffer.Alignment = binding.Alignment;
        }

        public int CompareRows(int rowA, int rowB, int columnIndex)
        {
            if (rowA < 0 || rowA >= _items.Count || rowB < 0 || rowB >= _items.Count ||
                columnIndex < 0 || columnIndex >= _bindings.Count)
            {
                return 0;
            }

            return _bindings[columnIndex].Comparer(_items[rowA], _items[rowB]);
        }

        public bool IsCellEditable(int rowIndex, int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= _bindings.Count) return false;
            return _bindings[columnIndex].CanWrite;
        }

        public bool SetCellValue(int rowIndex, int columnIndex, string textValue)
        {
            if (rowIndex < 0 || rowIndex >= _items.Count || columnIndex < 0 || columnIndex >= _bindings.Count)
            {
                return false;
            }

            var binding = _bindings[columnIndex];
            if (!binding.CanWrite || binding.Setter == null) return false;

            try
            {
                binding.Setter(_items[rowIndex], textValue);
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
            foreach (var b in _bindings)
            {
                var col = new ZeroColumn(b.FieldName, b.DisplayName, b.SuggestedWidth, b.Alignment)
                {
                    ReadOnly = !b.CanWrite,
                    ColumnType = b.ColumnType,
                    DisplayFormat = b.DisplayFormat
                };
                cols.Add(col);
            }
            return cols;
        }

        private void AutoGenerateBindings()
        {
            _bindings.Clear();
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in props)
            {
                if (!p.CanRead) continue;
                _bindings.Add(CreateBinding(p, null));
            }
        }

        private void ConfigureFromColumns(IEnumerable<ZeroColumn> columns)
        {
            _bindings.Clear();
            var type = typeof(T);

            int colIndex = 0;
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var col in columns)
            {
                PropertyInfo? prop = null;
                if (!string.IsNullOrEmpty(col.FieldName))
                {
                    prop = type.GetProperty(col.FieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                }

                if (prop == null && colIndex < props.Length)
                {
                    prop = props[colIndex];
                }

                if (prop != null && prop.CanRead)
                {
                    var binding = CreateBinding(prop, col);
                    _bindings.Add(binding);
                }
                colIndex++;
            }
        }

        private ColumnBinding CreateBinding(PropertyInfo prop, ZeroColumn? colOverride)
        {
            var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            string displayName = prop.Name;
            var dispAttr = prop.GetCustomAttribute<DisplayNameAttribute>();
            if (dispAttr != null && !string.IsNullOrEmpty(dispAttr.DisplayName))
            {
                displayName = dispAttr.DisplayName;
            }

            if (colOverride != null && !string.IsNullOrEmpty(colOverride.HeaderText))
            {
                displayName = colOverride.HeaderText;
            }

            CellAlignment align = CellAlignment.Left;
            GridColumnType colType = GridColumnType.Text;
            int width = 120;

            if (propType == typeof(int) || propType == typeof(long) || propType == typeof(short) || propType == typeof(byte))
            {
                align = CellAlignment.Right;
                colType = GridColumnType.Numeric;
                width = 85;
            }
            else if (propType == typeof(double) || propType == typeof(float) || propType == typeof(decimal))
            {
                align = CellAlignment.Right;
                colType = GridColumnType.Numeric;
                width = 110;
            }
            else if (propType == typeof(DateTime))
            {
                align = CellAlignment.Center;
                colType = GridColumnType.DateTime;
                width = 130;
            }
            else if (propType == typeof(bool))
            {
                align = CellAlignment.Center;
                colType = GridColumnType.Boolean;
                width = 75;
            }

            if (colOverride != null)
            {
                align = colOverride.Alignment;
                if (colOverride.Width > 0) width = colOverride.Width;
            }

            string? format = colOverride?.DisplayFormat;

            // Getter
            Func<T, string> getter = item =>
            {
                if (item == null) return string.Empty;
                var val = prop.GetValue(item, null);
                if (val == null) return string.Empty;

                if (!string.IsNullOrEmpty(format))
                {
                    return string.Format(CultureInfo.InvariantCulture, format, val);
                }
                return val.ToString() ?? string.Empty;
            };

            // Setter
            Action<T, string>? setter = null;
            bool canWrite = prop.CanWrite && (colOverride == null || !colOverride.ReadOnly);
            if (canWrite)
            {
                setter = (item, txt) =>
                {
                    if (item == null) return;
                    if (string.IsNullOrWhiteSpace(txt))
                    {
                        if (propType == typeof(string)) prop.SetValue(item, string.Empty, null);
                        else if (Nullable.GetUnderlyingType(prop.PropertyType) != null) prop.SetValue(item, null, null);
                        return;
                    }

                    object converted = Convert.ChangeType(txt.Trim(), propType, CultureInfo.InvariantCulture);
                    prop.SetValue(item, converted, null);
                };
            }

            // Comparer
            Comparison<T> comparer = (a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return -1;
                if (b == null) return 1;

                var valA = prop.GetValue(a, null) as IComparable;
                var valB = prop.GetValue(b, null) as IComparable;

                if (valA == null && valB == null) return 0;
                if (valA == null) return -1;
                if (valB == null) return 1;

                return valA.CompareTo(valB);
            };

            return new ColumnBinding
            {
                FieldName = prop.Name,
                DisplayName = displayName,
                PropertyType = propType,
                Alignment = align,
                ColumnType = colType,
                SuggestedWidth = width,
                DisplayFormat = format,
                CanWrite = canWrite,
                Getter = getter,
                Setter = setter,
                Comparer = comparer
            };
        }

        private sealed class ColumnBinding
        {
            public string FieldName { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public Type PropertyType { get; set; } = typeof(string);
            public CellAlignment Alignment { get; set; }
            public GridColumnType ColumnType { get; set; }
            public int SuggestedWidth { get; set; }
            public string? DisplayFormat { get; set; }
            public bool CanWrite { get; set; }
            public Func<T, string> Getter { get; set; } = _ => string.Empty;
            public Action<T, string>? Setter { get; set; }
            public Comparison<T> Comparer { get; set; } = (_, _) => 0;
        }
    }

    /// <summary>
    /// Obsolete alias for <see cref="ListSource{T}"/> to maintain backward compatibility.
    /// </summary>
    [Obsolete("Use ListSource<T> instead.")]
    public class ZeroListSource<T> : ListSource<T>
    {
        public ZeroListSource(IList<T> items) : base(items) { }
        public ZeroListSource(IList<T> items, IEnumerable<ZeroColumn> columns) : base(items, columns) { }
    }
}
