using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Scada;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    public enum AlarmFilterMode
    {
        All,
        ActiveOnly,
        UnacknowledgedOnly,
        ShelvedOnly
    }

    /// <summary>
    /// High-performance ISA-18.2 compliant industrial alarm grid control for ZeroUI WPF.
    /// Provides real-time event filtering, color-coded severity badges, and operator acknowledgment.
    /// </summary>
    public class ZAlarmGrid : FrameworkElement
    {
        public static readonly DependencyProperty OperatorNameProperty =
            DependencyProperty.Register(nameof(OperatorName), typeof(string), typeof(ZAlarmGrid),
                new FrameworkPropertyMetadata("Operator"));

        public static readonly DependencyProperty FilterModeProperty =
            DependencyProperty.Register(nameof(FilterMode), typeof(AlarmFilterMode), typeof(ZAlarmGrid),
                new FrameworkPropertyMetadata(AlarmFilterMode.ActiveOnly, OnFilterModeChanged));

        public string OperatorName
        {
            get => (string)GetValue(OperatorNameProperty);
            set => SetValue(OperatorNameProperty, value ?? "Operator");
        }

        public AlarmFilterMode FilterMode
        {
            get => (AlarmFilterMode)GetValue(FilterModeProperty);
            set => SetValue(FilterModeProperty, value);
        }

        private static void OnFilterModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZAlarmGrid grid)
            {
                grid.ReloadAlarms();
            }
        }

        private readonly List<ScadaAlarmRecord> _filteredAlarms = new List<ScadaAlarmRecord>();
        private int _selectedRowIndex = -1;
        private int _scrollOffset = 0;

        private const double HeaderHeight = 36.0;
        private const double RowHeight = 28.0;

        private static readonly Typeface SegoeBold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface SegoeNormal = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private static readonly Brush DarkHeaderBg = Freeze(new SolidColorBrush(Color.FromRgb(30, 41, 59)));
        private static readonly Brush LightHeaderBg = Freeze(new SolidColorBrush(Color.FromRgb(226, 232, 240)));
        private static readonly Brush DarkRowAlt = Freeze(new SolidColorBrush(Color.FromRgb(24, 32, 47)));
        private static readonly Brush DarkRow = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42)));
        private static readonly Brush LightRowAlt = Freeze(new SolidColorBrush(Color.FromRgb(248, 250, 252)));
        private static readonly Brush LightRow = Freeze(new SolidColorBrush(Color.FromRgb(255, 255, 255)));
        private static readonly Brush SelectionBrush = Freeze(new SolidColorBrush(Color.FromArgb(80, 59, 130, 246)));
        private static readonly Brush DarkText = Freeze(new SolidColorBrush(Color.FromRgb(248, 250, 252)));
        private static readonly Brush LightText = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42)));
        private static readonly Pen DarkBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(51, 65, 85)), 1.0));
        private static readonly Pen LightBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(203, 213, 225)), 1.0));

        private static T Freeze<T>(T freezable) where T : Freezable
        {
            if (freezable.CanFreeze) freezable.Freeze();
            return freezable;
        }

        #if NETFRAMEWORK
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
        }
        #else
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush, pixelsPerDip);
        }
        #endif

        public ZAlarmGrid()
        {
            ClipToBounds = true;
            Loaded += (s, e) =>
            {
                ScadaAlarmEngine.AlarmStateChanged += OnAlarmEngineChanged;
                ReloadAlarms();
            };
            Unloaded += (s, e) =>
            {
                ScadaAlarmEngine.AlarmStateChanged -= OnAlarmEngineChanged;
            };
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnAlarmEngineChanged(ScadaAlarmRecord record)
        {
            Dispatcher.InvokeAsync(ReloadAlarms);
        }

        public void ReloadAlarms()
        {
            _filteredAlarms.Clear();
            var all = ScadaAlarmEngine.GetActiveAlarms();

            switch (FilterMode)
            {
                case AlarmFilterMode.ActiveOnly:
                    _filteredAlarms.AddRange(all.Where(a => a.IsActive));
                    break;
                case AlarmFilterMode.UnacknowledgedOnly:
                    _filteredAlarms.AddRange(all.Where(a => a.NeedsAck));
                    break;
                case AlarmFilterMode.ShelvedOnly:
                    _filteredAlarms.AddRange(all.Where(a => a.State == ScadaAlarmState.Shelved));
                    break;
                default:
                    _filteredAlarms.AddRange(all);
                    break;
            }

            _filteredAlarms.Sort((a, b) => b.Severity.CompareTo(a.Severity));
            InvalidateVisual();
        }

        public void AcknowledgeSelected()
        {
            if (_selectedRowIndex >= 0 && _selectedRowIndex < _filteredAlarms.Count)
            {
                var record = _filteredAlarms[_selectedRowIndex];
                if (record.NeedsAck)
                {
                    ScadaAlarmEngine.Acknowledge(record.Id, OperatorName);
                }
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            int delta = e.Delta > 0 ? -1 : 1;
            int maxOffset = Math.Max(0, _filteredAlarms.Count - (int)((ActualHeight - HeaderHeight) / RowHeight));
            _scrollOffset = Math.Max(0, Math.Min(maxOffset, _scrollOffset + delta));
            InvalidateVisual();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Point pt = e.GetPosition(this);
            if (pt.Y > HeaderHeight)
            {
                int clickedIndex = _scrollOffset + (int)((pt.Y - HeaderHeight) / RowHeight);
                if (clickedIndex >= 0 && clickedIndex < _filteredAlarms.Count)
                {
                    _selectedRowIndex = clickedIndex;
                    if (e.ClickCount == 2)
                    {
                        AcknowledgeSelected();
                    }
                    InvalidateVisual();
                }
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(600, 260);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            bool isDark = ZeroWpfTheme.IsDark;
            Brush headerBg = isDark ? DarkHeaderBg : LightHeaderBg;
            Brush textBrush = isDark ? DarkText : LightText;
            Pen borderPen = isDark ? DarkBorderPen : LightBorderPen;

            #if NETFRAMEWORK
            double dpi = 1.0;
            #else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // Outer Frame
            dc.DrawRectangle(isDark ? DarkRow : LightRow, borderPen, new Rect(0, 0, w, h));

            // Header Background
            dc.DrawRectangle(headerBg, borderPen, new Rect(0, 0, w, HeaderHeight));

            // Column Header Labels
            double[] colWidths = { 90, 80, 110, w - 480, 70, 70, 60 };
            string[] colTitles = { "TIMESTAMP", "SEVERITY", "TAG PATH", "DESCRIPTION", "VALUE", "STATE", "ACK" };

            double curX = 10;
            for (int i = 0; i < colTitles.Length; i++)
            {
                var ft = CreateFormattedText(colTitles[i], SegoeBold, 10, textBrush, dpi);
                dc.DrawText(ft, new Point(curX, (HeaderHeight - ft.Height) * 0.5));
                curX += colWidths[i];
            }

            // Rows Rendering
            double curY = HeaderHeight;
            int visibleRowCount = (int)((h - HeaderHeight) / RowHeight);

            for (int i = 0; i < visibleRowCount; i++)
            {
                int dataIndex = _scrollOffset + i;
                if (dataIndex >= _filteredAlarms.Count) break;

                var item = _filteredAlarms[dataIndex];
                var rowRect = new Rect(0, curY, w, RowHeight);

                // Row Background
                if (dataIndex == _selectedRowIndex)
                {
                    dc.DrawRectangle(SelectionBrush, null, rowRect);
                }
                else
                {
                    Brush rowBg = (dataIndex % 2 == 0)
                        ? (isDark ? DarkRow : LightRow)
                        : (isDark ? DarkRowAlt : LightRowAlt);
                    dc.DrawRectangle(rowBg, null, rowRect);
                }

                // Row divider
                dc.DrawLine(borderPen, new Point(0, curY + RowHeight), new Point(w, curY + RowHeight));

                // Cell 0: Timestamp
                curX = 10;
                string timeStr = item.ActiveTimestamp.ToLocalTime().ToString("HH:mm:ss");
                DrawCell(dc, timeStr, curX, curY, colWidths[0], textBrush, SegoeNormal, dpi);
                curX += colWidths[0];

                // Cell 1: Severity Badge
                DrawSeverityBadge(dc, item.Severity, curX, curY + (RowHeight - 18) * 0.5, colWidths[1] - 10, dpi);
                curX += colWidths[1];

                // Cell 2: Tag Path
                DrawCell(dc, item.TagPath, curX, curY, colWidths[2], textBrush, SegoeBold, dpi);
                curX += colWidths[2];

                // Cell 3: Description
                DrawCell(dc, item.Description, curX, curY, colWidths[3], textBrush, SegoeNormal, dpi);
                curX += colWidths[3];

                // Cell 4: Value
                DrawCell(dc, item.TriggerValue?.ToString() ?? "-", curX, curY, colWidths[4], textBrush, SegoeNormal, dpi);
                curX += colWidths[4];

                // Cell 5: State
                DrawCell(dc, item.State.ToString(), curX, curY, colWidths[5], textBrush, SegoeNormal, dpi);
                curX += colWidths[5];

                // Cell 6: Ack User
                DrawCell(dc, item.AckUser ?? (item.NeedsAck ? "NO" : "YES"), curX, curY, colWidths[6], textBrush, SegoeNormal, dpi);

                curY += RowHeight;
            }
        }

        private void DrawCell(DrawingContext dc, string text, double x, double y, double maxW, Brush brush, Typeface tf, double dpi)
        {
            var ft = CreateFormattedText(text, tf, 10, brush, dpi);
            ft.MaxTextWidth = Math.Max(10, maxW - 6);
            ft.Trimming = TextTrimming.CharacterEllipsis;
            dc.DrawText(ft, new Point(x, y + (RowHeight - ft.Height) * 0.5));
        }

        private void DrawSeverityBadge(DrawingContext dc, ScadaAlarmSeverity severity, double x, double y, double w, double dpi)
        {
            Color c = severity switch
            {
                ScadaAlarmSeverity.Critical => Color.FromRgb(239, 68, 68),
                ScadaAlarmSeverity.High => Color.FromRgb(249, 115, 22),
                ScadaAlarmSeverity.Medium => Color.FromRgb(245, 158, 11),
                _ => Color.FromRgb(59, 130, 246)
            };

            var badgeBrush = Freeze(new SolidColorBrush(c));
            dc.DrawRoundedRectangle(badgeBrush, null, new Rect(x, y, w, 18), 3, 3);

            var ft = CreateFormattedText(severity.ToString().ToUpperInvariant(), SegoeBold, 8.5, Brushes.White, dpi);
            dc.DrawText(ft, new Point(x + (w - ft.Width) * 0.5, y + (18 - ft.Height) * 0.5));
        }
    
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
    /// Legacy alias for <see cref="ZAlarmGrid"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("AlarmGrid is deprecated and will be removed in 5 release cycles. Please migrate to ZAlarmGrid instead.")]
    public class AlarmGrid : ZAlarmGrid { }

    /// <summary>
    /// Legacy alias for <see cref="ZAlarmGrid"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroAlarmGrid is deprecated and will be removed in 5 release cycles. Please migrate to ZAlarmGrid instead.")]
    public class ZeroAlarmGrid : ZAlarmGrid { }

    #endregion

}
