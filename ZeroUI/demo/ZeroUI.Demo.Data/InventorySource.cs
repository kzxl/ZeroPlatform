using System;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;

namespace ZeroUI.Demo.Data
{
    public sealed class InventorySource : IZeroVirtualSource, IZeroSortableSource, IZeroEditableSource
    {
        private readonly InventoryItem[] _items;

        public static InventoryItem[] Generate(int count) => MockDataGenerator.Generate(count);

        public InventorySource(InventoryItem[] items)
        {
            _items = items;
        }

        public int TotalRowCount => _items.Length;
        public int TotalColumnCount => 11;
        public InventoryItem[] Items => _items;

        public void GetCellValue(int rowIndex, int columnIndex, ref CellValueBuffer buffer)
        {
            if (rowIndex < 0 || rowIndex >= _items.Length) return;
            ref readonly var item = ref _items[rowIndex];

            switch (columnIndex)
            {
                case 0:
                    buffer.Text = item.IsActive ? "true".AsSpan() : "false".AsSpan();
                    buffer.Alignment = CellAlignment.Center;
                    break;
                case 1:
                    buffer.Text = item.Category.AsSpan();
                    buffer.Alignment = CellAlignment.Left;
                    break;
                case 2:
                    buffer.Text = item.Id.ToString().AsSpan();
                    buffer.Alignment = CellAlignment.Right;
                    break;
                case 3:
                    buffer.Text = item.ItemCode.AsSpan();
                    buffer.Alignment = CellAlignment.Left;
                    break;
                case 4:
                    buffer.Text = item.ItemName.AsSpan();
                    buffer.Alignment = CellAlignment.Left;
                    break;
                case 5:
                    buffer.Text = item.Quantity.ToString("N0").AsSpan();
                    buffer.Alignment = CellAlignment.Right;
                    break;
                case 6:
                    buffer.Text = item.UnitPrice.ToString("N2").AsSpan();
                    buffer.Alignment = CellAlignment.Right;
                    break;
                case 7:
                    buffer.Text = item.TotalAmount.ToString("N2").AsSpan();
                    buffer.Alignment = CellAlignment.Right;
                    break;
                case 8:
                    buffer.DataBarPercent = (float)item.YieldRate;
                    buffer.Text = item.YieldRate.ToString("P0").AsSpan();
                    buffer.Alignment = CellAlignment.Center;
                    break;
                case 9:
                    buffer.Text = item.LotNumber.AsSpan();
                    buffer.Alignment = CellAlignment.Center;
                    break;
                case 10:
                    buffer.Text = item.Status.AsSpan();
                    buffer.Alignment = CellAlignment.Center;
                    buffer.HasCustomBackground = true;
                    if (item.Status == "Passed OQC" || item.Status == "In Warehouse" || item.Status == "Completed")
                    {
                        buffer.BackColor = 0x00E8F8E8; // Soft Green
                        buffer.TextColor = 0x001B692A;
                    }
                    else if (item.Status == "Pending IQC" || item.Status == "Pending Inspection")
                    {
                        buffer.BackColor = 0x00FFF8E7; // Soft Orange
                        buffer.TextColor = 0x00A05A00;
                    }
                    else if (item.Status == "SMT Feeding")
                    {
                        buffer.BackColor = 0x00EBF4FF; // Soft Blue
                        buffer.TextColor = 0x001B5EBE;
                    }
                    else if (item.Status == "QC Quarantine" || item.Status == "On Hold" || item.Status == "Inspection Hold")
                    {
                        buffer.BackColor = 0x00F3E8FF; // Soft Purple
                        buffer.TextColor = 0x006B21A8;
                    }
                    else
                    {
                        buffer.BackColor = 0x00FEE8E8; // Soft Red
                        buffer.TextColor = 0x00B3261E;
                    }
                    break;
            }
        }

        public bool IsCellEditable(int rowIndex, int columnIndex)
        {
            return columnIndex != 2 && columnIndex != 7;
        }

        public bool SetCellValue(int rowIndex, int columnIndex, string value)
        {
            if (rowIndex < 0 || rowIndex >= _items.Length) return false;
            ref var item = ref _items[rowIndex];

            switch (columnIndex)
            {
                case 0:
                    item.IsActive = bool.TryParse(value, out bool b) && b;
                    return true;
                case 1:
                    item.Category = value;
                    return true;
                case 3:
                    item.ItemCode = value;
                    return true;
                case 4:
                    item.ItemName = value;
                    return true;
                case 5:
                    if (int.TryParse(value.Replace(",", ""), out int qty))
                    {
                        item.Quantity = qty;
                        item.TotalAmount = qty * item.UnitPrice;
                        return true;
                    }
                    return false;
                case 6:
                    if (double.TryParse(value.Replace(",", ""), out double price))
                    {
                        item.UnitPrice = price;
                        item.TotalAmount = item.Quantity * price;
                        return true;
                    }
                    return false;
                case 9:
                    item.LotNumber = value;
                    return true;
                case 10:
                    item.Status = value;
                    return true;
                default:
                    return false;
            }
        }

        public int CompareRows(int rowA, int rowB, int columnIndex)
        {
            if (rowA < 0 || rowA >= _items.Length || rowB < 0 || rowB >= _items.Length) return 0;
            ref readonly var a = ref _items[rowA];
            ref readonly var b = ref _items[rowB];

            return columnIndex switch
            {
                0 => a.IsActive.CompareTo(b.IsActive),
                1 => string.Compare(a.Category, b.Category, StringComparison.OrdinalIgnoreCase),
                2 => a.Id.CompareTo(b.Id),
                3 => string.Compare(a.ItemCode, b.ItemCode, StringComparison.OrdinalIgnoreCase),
                4 => string.Compare(a.ItemName, b.ItemName, StringComparison.OrdinalIgnoreCase),
                5 => a.Quantity.CompareTo(b.Quantity),
                6 => a.UnitPrice.CompareTo(b.UnitPrice),
                7 => a.TotalAmount.CompareTo(b.TotalAmount),
                8 => a.YieldRate.CompareTo(b.YieldRate),
                9 => string.Compare(a.LotNumber, b.LotNumber, StringComparison.OrdinalIgnoreCase),
                10 => string.Compare(a.Status, b.Status, StringComparison.OrdinalIgnoreCase),
                _ => a.Id.CompareTo(b.Id)
            };
        }

        public void Sort(int columnIndex, bool ascending)
        {
            Comparison<InventoryItem> comparison = columnIndex switch
            {
                0 => (a, b) => a.IsActive.CompareTo(b.IsActive),
                1 => (a, b) => string.Compare(a.Category, b.Category, StringComparison.OrdinalIgnoreCase),
                2 => (a, b) => a.Id.CompareTo(b.Id),
                3 => (a, b) => string.Compare(a.ItemCode, b.ItemCode, StringComparison.OrdinalIgnoreCase),
                4 => (a, b) => string.Compare(a.ItemName, b.ItemName, StringComparison.OrdinalIgnoreCase),
                5 => (a, b) => a.Quantity.CompareTo(b.Quantity),
                6 => (a, b) => a.UnitPrice.CompareTo(b.UnitPrice),
                7 => (a, b) => a.TotalAmount.CompareTo(b.TotalAmount),
                8 => (a, b) => a.YieldRate.CompareTo(b.YieldRate),
                9 => (a, b) => string.Compare(a.LotNumber, b.LotNumber, StringComparison.OrdinalIgnoreCase),
                10 => (a, b) => string.Compare(a.Status, b.Status, StringComparison.OrdinalIgnoreCase),
                _ => (a, b) => a.Id.CompareTo(b.Id)
            };

            if (!ascending)
            {
                var orig = comparison;
                comparison = (a, b) => orig(b, a);
            }

            Array.Sort(_items, comparison);
        }
    }
}
