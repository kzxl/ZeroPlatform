using System;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;

namespace ZeroUI.Demo.Data
{
    public struct BoardMaterialItem
    {
        public string MaterialCode;
        public int PartlistQty;
        public int RawStock;
        public int WipStock;

        public BoardMaterialItem(string code, int partlistQty, int rawStock, int wipStock)
        {
            MaterialCode = code;
            PartlistQty = partlistQty;
            RawStock = rawStock;
            WipStock = wipStock;
        }
    }

    public sealed class MesBoardSource : IZeroVirtualSource
    {
        private readonly BoardMaterialItem[] _items;
        private readonly char[] _formatBuffer = new char[32];

        public MesBoardSource()
        {
            _items = new[]
            {
                new BoardMaterialItem("BOA437", 1, 347, 0),
                new BoardMaterialItem("BOA472", 0, 18, 0),
                new BoardMaterialItem("BOA536", 1, 4, 0),
                new BoardMaterialItem("BOA541", 1, 1017, 0)
            };
        }

        public int TotalRowCount => _items.Length;
        public int TotalColumnCount => 4;

        public void GetCellValue(int rowIndex, int columnIndex, ref CellValueBuffer buffer)
        {
            if (rowIndex < 0 || rowIndex >= _items.Length) return;

            ref readonly var item = ref _items[rowIndex];

            switch (columnIndex)
            {
                case 0:
                    buffer.Text = item.MaterialCode.AsSpan();
                    break;
                case 1:
                    buffer.Text = item.PartlistQty.ToString().AsSpan();
                    break;
                case 2:
                    if (item.RawStock.TryFormat(_formatBuffer, out int charsWritten, "N0"))
                    {
                        buffer.Text = _formatBuffer.AsSpan(0, charsWritten);
                    }
                    else
                    {
                        buffer.Text = item.RawStock.ToString().AsSpan();
                    }
                    break;
                case 3:
                    buffer.Text = item.WipStock.ToString().AsSpan();
                    break;
            }
        }
    }
}
