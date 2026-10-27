using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Memory;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// High-performance Single-Visual OT protocol and binary memory packet inspector.
    /// Virtualizes large raw buffers with zero-copy reading, 4-zone layout (Offset, Hex, ASCII, Value Inspector),
    /// and protocol dissector color highlighting (Modbus, Siemens S7, CAN bus).
    /// </summary>
    public class ZHexInspector : FrameworkElement
    {
        private HexViewEngine _engine;
        private int _selectedOffset = 0;
        private int _scrollY = 0;
        private int _rowHeight = 22;
        private int _headerHeight = 28;
        private int _inspectorHeight = 36;

        private readonly Typeface _monoTypeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private readonly Typeface _monoBold = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        public HexViewEngine Engine => _engine;

        public int SelectedOffset
        {
            get => _selectedOffset;
            set
            {
                int clamped = Math.Max(0, Math.Min(_engine.TotalBytes - 1, value));
                if (_selectedOffset != clamped)
                {
                    _selectedOffset = clamped;
                    InvalidateVisual();
                }
            }
        }

        public ZHexInspector()
        {
            ClipToBounds = true;
            Focusable = true;
            Cursor = Cursors.Arrow;

            _engine = new HexViewEngine(ReadOnlyMemory<byte>.Empty, bytesPerRow: 16);
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public void SetBuffer(ReadOnlyMemory<byte> buffer)
        {
            _engine.Buffer = buffer;
            _selectedOffset = 0;
            _scrollY = 0;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 10 || h <= 10) return;

            // 1. Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, w, h));

            int bytesPerRow = _engine.BytesPerRow;
            double offsetColWidth = 80;
            double byteColWidth = 24;
            double hexAreaWidth = bytesPerRow * byteColWidth + 16;
            double hexStartX = offsetColWidth + 12;
            double asciiStartX = hexStartX + hexAreaWidth + 12;

            // 2. Draw Column Headers
            DrawHeader(dc, w, bytesPerRow, offsetColWidth, hexStartX, byteColWidth, asciiStartX);

            // 3. Draw Virtual Rows
            int totalRows = _engine.TotalRows;
            double bodyHeight = Math.Max(0, h - _headerHeight - _inspectorHeight);
            int startRow = Math.Max(0, _scrollY / _rowHeight);
            int visibleRows = (int)(bodyHeight / _rowHeight) + 2;
            int endRow = Math.Min(totalRows - 1, startRow + visibleRows);

            var clipBody = new RectangleGeometry(new Rect(0, _headerHeight, w, bodyHeight));
            clipBody.Freeze();
            dc.PushClip(clipBody);

            for (int r = startRow; r <= endRow && r < totalRows; r++)
            {
                double rowY = _headerHeight + (r * _rowHeight) - _scrollY;

                if (_engine.TryGetRow(r, out int rowOffset, out ReadOnlySpan<byte> rowBytes))
                {
                    DrawRow(dc, r, rowOffset, rowBytes, rowY, offsetColWidth, hexStartX, byteColWidth, asciiStartX);
                }
            }

            dc.Pop(); // Restore clip

            // 4. Draw Bottom Value Inspector Bar
            DrawInspectorBar(dc, w, h);
        }

        private void DrawHeader(DrawingContext dc, double w, int bytesPerRow, double offsetColW, double hexStartX, double byteColW, double asciiStartX)
        {
            Rect hdrRect = new Rect(0, 0, w, _headerHeight);
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, hdrRect);
            dc.DrawLine(new Pen(ZeroWpfTheme.BorderDefault, 1), new Point(0, _headerHeight), new Point(w, _headerHeight));

            // Offset column header
            var ftOffset = new FormattedText("OFFSET", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _monoBold, 11, ZeroWpfTheme.TextMuted, 1.0);
            dc.DrawText(ftOffset, new Point(14, 6));

            // Hex headers: 00 01 02 ... 0F
            for (int i = 0; i < bytesPerRow; i++)
            {
                string colHex = i.ToString("X2");
                var ftHex = new FormattedText(colHex, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _monoBold, 10.5, ZeroWpfTheme.TextMuted, 1.0);
                dc.DrawText(ftHex, new Point(hexStartX + i * byteColW, 6));
            }

            // ASCII header
            var ftAscii = new FormattedText("DECODED TEXT", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _monoBold, 11, ZeroWpfTheme.TextMuted, 1.0);
            dc.DrawText(ftAscii, new Point(asciiStartX, 6));
        }

        private void DrawRow(DrawingContext dc, int rowIdx, int rowOffset, ReadOnlySpan<byte> rowBytes, double y,
            double offsetColW, double hexStartX, double byteColW, double asciiStartX)
        {
            // Row alternating background
            if (rowIdx % 2 == 1)
            {
                var altBg = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255));
                altBg.Freeze();
                dc.DrawRectangle(altBg, null, new Rect(0, y, ActualWidth, _rowHeight));
            }

            // 1. Offset address
            string offsetStr = rowOffset.ToString("X8");
            var ftOffset = new FormattedText(offsetStr, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _monoTypeface, 11, ZeroWpfTheme.TextSecondary, 1.0);
            dc.DrawText(ftOffset, new Point(14, y + 3));

            // 2. Hex Bytes & ASCII
            for (int i = 0; i < rowBytes.Length; i++)
            {
                int byteOffset = rowOffset + i;
                byte val = rowBytes[i];
                bool isSelected = (byteOffset == _selectedOffset);

                // Check Protocol Dissector Segment
                var seg = _engine.Dissector.FindSegment(byteOffset);

                double byteX = hexStartX + i * byteColW;
                Rect hexCellRect = new Rect(byteX - 2, y + 1, byteColW - 1, _rowHeight - 2);

                // Segment Highlight Pill
                if (seg != null)
                {
                    var segColor = Color.FromArgb((byte)((seg.ColorArgb >> 24) & 0xFF), (byte)((seg.ColorArgb >> 16) & 0xFF), (byte)((seg.ColorArgb >> 8) & 0xFF), (byte)(seg.ColorArgb & 0xFF));
                    var segBg = new SolidColorBrush(Color.FromArgb(50, segColor.R, segColor.G, segColor.B));
                    segBg.Freeze();
                    dc.DrawRoundedRectangle(segBg, null, hexCellRect, 3, 3);
                }

                // Selection Box
                if (isSelected)
                {
                    dc.DrawRoundedRectangle(ZeroWpfTheme.SelectionBackground, new Pen(ZeroWpfTheme.PrimaryAccent, 1.5), hexCellRect, 3, 3);
                }

                // Hex text
                string hexStr = val.ToString("X2");
                Brush textBrush = isSelected ? ZeroWpfTheme.SelectionForeground : (val == 0 ? ZeroWpfTheme.TextMuted : ZeroWpfTheme.TextPrimary);
                var ftByte = new FormattedText(hexStr, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _monoTypeface, 11, textBrush, 1.0);
                dc.DrawText(ftByte, new Point(byteX, y + 3));

                // ASCII text
                char c = (val >= 32 && val <= 126) ? (char)val : '.';
                double asciiX = asciiStartX + i * 11;
                Rect asciiCellRect = new Rect(asciiX - 1, y + 1, 11, _rowHeight - 2);

                if (isSelected)
                {
                    dc.DrawRectangle(ZeroWpfTheme.SelectionBackground, null, asciiCellRect);
                }

                var ftChar = new FormattedText(c.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _monoTypeface, 11,
                    isSelected ? ZeroWpfTheme.SelectionForeground : (c == '.' ? ZeroWpfTheme.TextMuted : ZeroWpfTheme.TextPrimary), 1.0);
                dc.DrawText(ftChar, new Point(asciiX, y + 3));
            }
        }

        private void DrawInspectorBar(DrawingContext dc, double w, double h)
        {
            double barY = h - _inspectorHeight;
            Rect barRect = new Rect(0, barY, w, _inspectorHeight);
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, barRect);
            dc.DrawLine(new Pen(ZeroWpfTheme.BorderDefault, 1), new Point(0, barY), new Point(w, barY));

            if (_engine.TotalBytes == 0) return;

            _engine.InspectOffset(_selectedOffset, isLittleEndian: false,
                out byte u8, out short s16, out ushort u16, out int s32, out uint u32,
                out float f32, out double f64, out string bitStr);

            var seg = _engine.Dissector.FindSegment(_selectedOffset);
            string segInfo = seg != null ? $" • [{seg.Title}]" : "";

            string text = $"Offset: 0x{_selectedOffset:X4} ({_selectedOffset}){segInfo}  |  UInt8: {u8} (0x{u8:X2})  |  Int16: {s16}  |  UInt16: {u16}  |  Int32: {s32}  |  Float: {f32:0.###}  |  Bits: {bitStr}";

            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _monoBold, 11, ZeroWpfTheme.TextPrimary, 1.0);
            dc.DrawText(ft, new Point(14, barY + 9));
        }

        #region Mouse Hit-Testing & Scrolling

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            Point pt = e.GetPosition(this);
            if (pt.Y < _headerHeight || pt.Y > ActualHeight - _inspectorHeight) return;

            int bytesPerRow = _engine.BytesPerRow;
            double offsetColWidth = 80;
            double byteColWidth = 24;
            double hexStartX = offsetColWidth + 12;
            double hexAreaWidth = bytesPerRow * byteColWidth;
            double asciiStartX = hexStartX + hexAreaWidth + 12;

            int row = (int)((pt.Y - _headerHeight + _scrollY) / _rowHeight);
            if (row < 0 || row >= _engine.TotalRows) return;

            int col = -1;

            // Clicked in Hex area
            if (pt.X >= hexStartX && pt.X < hexStartX + hexAreaWidth)
            {
                col = (int)((pt.X - hexStartX) / byteColWidth);
            }
            // Clicked in ASCII area
            else if (pt.X >= asciiStartX && pt.X < asciiStartX + bytesPerRow * 11)
            {
                col = (int)((pt.X - asciiStartX) / 11);
            }

            if (col >= 0 && col < bytesPerRow)
            {
                int targetOffset = row * bytesPerRow + col;
                if (targetOffset < _engine.TotalBytes)
                {
                    SelectedOffset = targetOffset;
                }
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            int maxScroll = Math.Max(0, (_engine.TotalRows * _rowHeight) - (int)(ActualHeight - _headerHeight - _inspectorHeight));
            int delta = e.Delta > 0 ? -(_rowHeight * 3) : (_rowHeight * 3);
            _scrollY = Math.Max(0, Math.Min(maxScroll, _scrollY + delta));
            InvalidateVisual();
            e.Handled = true;
        }

        #endregion
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged += OnThemeChanged;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
    }
    private void OnThemeChanged() => InvalidateVisual();
}
    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZHexInspector"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("HexInspector is deprecated and will be removed in 5 release cycles. Please migrate to ZHexInspector instead.")]
    public class HexInspector : ZHexInspector { }

    /// <summary>
    /// Legacy alias for <see cref="ZHexInspector"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroHexInspector is deprecated and will be removed in 5 release cycles. Please migrate to ZHexInspector instead.")]
    public class ZeroHexInspector : ZHexInspector { }

    #endregion

}
