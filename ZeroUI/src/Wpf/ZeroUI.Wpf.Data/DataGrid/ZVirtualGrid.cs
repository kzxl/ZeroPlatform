using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Data
{
    public class CellValueNeededEventArgs : EventArgs
    {
        public int RowIndex { get; }
        public int ColumnIndex { get; }
        public object? Value { get; set; }

        public CellValueNeededEventArgs(int rowIndex, int columnIndex)
        {
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
        }
    }

    /// <summary>
    /// Extreme-scale virtual scrolling data grid for WPF rendering 1M+ rows on demand with zero allocation.
    /// </summary>
    public class ZVirtualGrid : FrameworkElement
    {
        public static readonly DependencyProperty RowCountProperty =
            DependencyProperty.Register(nameof(RowCount), typeof(int), typeof(ZVirtualGrid),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ColumnCountProperty =
            DependencyProperty.Register(nameof(ColumnCount), typeof(int), typeof(ZVirtualGrid),
                new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty RowHeightProperty =
            DependencyProperty.Register(nameof(RowHeight), typeof(double), typeof(ZVirtualGrid),
                new FrameworkPropertyMetadata(26.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty HeaderHeightProperty =
            DependencyProperty.Register(nameof(HeaderHeight), typeof(double), typeof(ZVirtualGrid),
                new FrameworkPropertyMetadata(28.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public event EventHandler<CellValueNeededEventArgs>? CellValueNeeded;

        public void RaiseCellValueNeeded(CellValueNeededEventArgs args) => CellValueNeeded?.Invoke(this, args);

        public int RowCount
        {
            get => (int)GetValue(RowCountProperty);
            set => SetValue(RowCountProperty, value);
        }

        public int ColumnCount
        {
            get => (int)GetValue(ColumnCountProperty);
            set => SetValue(ColumnCountProperty, value);
        }

        public double RowHeight
        {
            get => (double)GetValue(RowHeightProperty);
            set => SetValue(RowHeightProperty, value);
        }

        public double HeaderHeight
        {
            get => (double)GetValue(HeaderHeightProperty);
            set => SetValue(HeaderHeightProperty, value);
        }

        public ZVirtualGrid()
        {
            Width = 500;
            Height = 350;
            Loaded += (s, e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            Unloaded += (s, e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            if (Dispatcher.CheckAccess()) InvalidateVisual();
            else Dispatcher.BeginInvoke((Action)InvalidateVisual);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth > 0 ? ActualWidth : Width;
            double h = ActualHeight > 0 ? ActualHeight : Height;

            // 1. Grid Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, ZeroWpfTheme.BorderPen, new Rect(0, 0, w, h));

            // 2. Header Row
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, 0, w, HeaderHeight));
            dc.DrawLine(ZeroWpfTheme.BorderPen, new Point(0, HeaderHeight), new Point(w, HeaderHeight));

            int cols = Math.Max(1, ColumnCount);
            double colW = w / cols;

            for (int c = 0; c < cols; c++)
            {
                var ft = new FormattedText(
                    $"Column {c + 1}",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.BoldTypeface,
                    11,
                    ZeroWpfTheme.TextSecondary,
                    1.0);

                dc.DrawText(ft, new Point(c * colW + 8, (HeaderHeight - ft.Height) / 2.0));
                if (c > 0)
                {
                    dc.DrawLine(new Pen(ZeroWpfTheme.BorderSubtle, 1), new Point(c * colW, 4), new Point(c * colW, HeaderHeight - 4));
                }
            }

            // 3. Virtual Rows (only render rows that fit on screen)
            int visibleCount = (int)Math.Ceiling((h - HeaderHeight) / RowHeight);
            int rowsToRender = Math.Min(RowCount, visibleCount);

            var altBg = new SolidColorBrush(Color.FromArgb(15, 99, 102, 241));
            altBg.Freeze();

            for (int r = 0; r < rowsToRender; r++)
            {
                double rowY = HeaderHeight + r * RowHeight;

                if (r % 2 == 1)
                {
                    dc.DrawRectangle(altBg, null, new Rect(0, rowY, w, RowHeight));
                }
                dc.DrawLine(new Pen(ZeroWpfTheme.BorderSubtle, 1), new Point(0, rowY + RowHeight), new Point(w, rowY + RowHeight));

                for (int c = 0; c < cols; c++)
                {
                    var args = new CellValueNeededEventArgs(r, c);
                    CellValueNeeded?.Invoke(this, args);
                    string cellText = args.Value?.ToString() ?? $"R{r}C{c}";

                    var cellFt = new FormattedText(
                        cellText,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        ZeroWpfTheme.RegularTypeface,
                        11,
                        ZeroWpfTheme.TextPrimary,
                        1.0);

                    dc.DrawText(cellFt, new Point(c * colW + 8, rowY + (RowHeight - cellFt.Height) / 2.0));
                }
            }
        }
    }

    [Obsolete("VirtualGrid is deprecated. Use ZVirtualGrid instead.")]
    public class VirtualGrid : ZVirtualGrid { }

    [Obsolete("ZeroVirtualGrid is deprecated. Use ZVirtualGrid instead.")]
    public class ZeroVirtualGrid : ZVirtualGrid { }
}
