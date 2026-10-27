using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroUI.Core.Scada;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// ISA-18.2 compliant alarm summary control for WPF.
    /// </summary>
    public class ZAlarmSummary : FrameworkElement
    {
        private readonly List<AlarmRecord> _alarms = new List<AlarmRecord>();
        private int _selectedRowIndex = -1;
        private int _scrollOffset = 0;
        private bool _flashState = false;
        private readonly DispatcherTimer _flashTimer;

        private const double SummaryBarHeight = 40.0;
        private const double ToolbarHeight = 36.0;
        private const double HeaderHeight = 30.0;
        private const double RowHeight = 28.0;

        public event EventHandler<AlarmActionEventArgs>? AlarmAcknowledged;
        public event EventHandler<AlarmActionEventArgs>? AlarmShelved;
        public event EventHandler<AlarmSelectedEventArgs>? AlarmDoubleClicked;

        public static readonly DependencyProperty AlarmSourceProperty =
            DependencyProperty.Register(nameof(AlarmSource), typeof(string), typeof(ZAlarmSummary), new PropertyMetadata(string.Empty));

        public string AlarmSource
        {
            get => (string)GetValue(AlarmSourceProperty);
            set => SetValue(AlarmSourceProperty, value);
        }

        public bool ShowAcknowledged { get; set; } = true;
        public bool ShowSuppressed { get; set; } = true;
        public bool ShowShelved { get; set; } = true;
        public AlarmPriority MinPriority { get; set; } = AlarmPriority.Diagnostic;
        public string AreaFilter { get; set; } = string.Empty;
        public bool FlashOnNew { get; set; } = true;

        public int ActiveCount => _alarms.Count(a => a.State == AlarmState.Active);
        public int UnacknowledgedCount => _alarms.Count(a => a.State == AlarmState.Active && a.AcknowledgedTime == null);
        public int CountByPriority(AlarmPriority p) => _alarms.Count(a => a.Priority == p);

        private static readonly Typeface SegoeBold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface SegoeNormal = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private static readonly Brush DarkBg = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42)));
        private static readonly Brush LightBg = Freeze(new SolidColorBrush(Color.FromRgb(255, 255, 255)));
        private static readonly Brush DarkPanelBg = Freeze(new SolidColorBrush(Color.FromRgb(30, 41, 59)));
        private static readonly Brush LightPanelBg = Freeze(new SolidColorBrush(Color.FromRgb(241, 245, 249)));
        
        private static readonly Brush SelectionBrush = Freeze(new SolidColorBrush(Color.FromArgb(80, 59, 130, 246)));
        private static readonly Brush DarkText = Freeze(new SolidColorBrush(Color.FromRgb(248, 250, 252)));
        private static readonly Brush LightText = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42)));
        
        private static readonly Pen DarkBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(51, 65, 85)), 1.0));
        private static readonly Pen LightBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(226, 232, 240)), 1.0));

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

        public ZAlarmSummary()
        {
            ClipToBounds = true;
            _flashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _flashTimer.Tick += (s, e) =>
            {
                _flashState = !_flashState;
                if (FlashOnNew && UnacknowledgedCount > 0)
                {
                    InvalidateVisual();
                }
            };
            
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            _flashTimer.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
            _flashTimer.Stop();
        }

        private void OnThemeChanged() => InvalidateVisual();

        public void SetAlarms(IEnumerable<AlarmRecord> alarms)
        {
            _alarms.Clear();
            foreach (var a in alarms)
            {
                bool include = true;
                if (!ShowAcknowledged && a.State == AlarmState.Acknowledged) include = false;
                if (!ShowSuppressed && a.State == AlarmState.Suppressed) include = false;
                if (!ShowShelved && a.State == AlarmState.Shelved) include = false;
                if (a.Priority > MinPriority) include = false;
                if (!string.IsNullOrEmpty(AreaFilter) && a.Area != AreaFilter) include = false;

                if (include) _alarms.Add(a);
            }
            InvalidateVisual();
        }

        public void AcknowledgeSelected()
        {
            if (_selectedRowIndex >= 0 && _selectedRowIndex < _alarms.Count)
            {
                var a = _alarms[_selectedRowIndex];
                if (a.State == AlarmState.Active && a.AcknowledgedTime == null)
                {
                    a.State = AlarmState.Acknowledged;
                    a.AcknowledgedTime = DateTime.Now;
                    a.AcknowledgedBy = "Operator";
                    AlarmAcknowledged?.Invoke(this, new AlarmActionEventArgs(a));
                    InvalidateVisual();
                }
            }
        }

        public void AcknowledgeAll()
        {
            foreach (var a in _alarms.Where(x => x.State == AlarmState.Active && x.AcknowledgedTime == null))
            {
                a.State = AlarmState.Acknowledged;
                a.AcknowledgedTime = DateTime.Now;
                a.AcknowledgedBy = "Operator";
                AlarmAcknowledged?.Invoke(this, new AlarmActionEventArgs(a));
            }
            InvalidateVisual();
        }

        public void ShelveSelected(TimeSpan duration)
        {
            if (_selectedRowIndex >= 0 && _selectedRowIndex < _alarms.Count)
            {
                var a = _alarms[_selectedRowIndex];
                a.State = AlarmState.Shelved;
                AlarmShelved?.Invoke(this, new AlarmActionEventArgs(a));
                InvalidateVisual();
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            int delta = e.Delta > 0 ? -1 : 1;
            double totalHeader = SummaryBarHeight + ToolbarHeight + HeaderHeight;
            int maxOffset = Math.Max(0, _alarms.Count - (int)((ActualHeight - totalHeader) / RowHeight));
            _scrollOffset = Math.Max(0, Math.Min(maxOffset, _scrollOffset + delta));
            InvalidateVisual();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Point pt = e.GetPosition(this);
            double totalHeader = SummaryBarHeight + ToolbarHeight + HeaderHeight;
            
            if (pt.Y > totalHeader)
            {
                int clickedIndex = _scrollOffset + (int)((pt.Y - totalHeader) / RowHeight);
                if (clickedIndex >= 0 && clickedIndex < _alarms.Count)
                {
                    _selectedRowIndex = clickedIndex;
                    if (e.ClickCount == 2)
                    {
                        AlarmDoubleClicked?.Invoke(this, new AlarmSelectedEventArgs(_alarms[_selectedRowIndex]));
                    }
                    InvalidateVisual();
                }
            }
            else if (pt.Y > SummaryBarHeight && pt.Y <= SummaryBarHeight + ToolbarHeight)
            {
                // Basic Toolbar Clicks
                if (pt.X > 100 && pt.X < 200) AcknowledgeSelected();
                if (pt.X > 210 && pt.X < 310) AcknowledgeAll();
                if (pt.X > 320 && pt.X < 420) ShelveSelected(TimeSpan.FromHours(1));
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(800, 400);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            bool isDark = ZeroWpfTheme.IsDark;
            Brush bg = isDark ? DarkBg : LightBg;
            Brush panelBg = isDark ? DarkPanelBg : LightPanelBg;
            Brush textBrush = isDark ? DarkText : LightText;
            Pen borderPen = isDark ? DarkBorderPen : LightBorderPen;

            #if NETFRAMEWORK
            double dpi = 1.0;
            #else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            dc.DrawRectangle(bg, null, new Rect(0, 0, w, h));
            dc.DrawRectangle(panelBg, null, new Rect(0, 0, w, SummaryBarHeight));

            DrawSummaryBar(dc, w, textBrush, dpi);
            
            dc.DrawRectangle(bg, null, new Rect(0, SummaryBarHeight, w, ToolbarHeight));
            DrawToolbar(dc, textBrush, borderPen, dpi);

            double headerY = SummaryBarHeight + ToolbarHeight;
            dc.DrawRectangle(panelBg, null, new Rect(0, headerY, w, HeaderHeight));
            dc.DrawLine(borderPen, new Point(0, headerY), new Point(w, headerY));
            dc.DrawLine(borderPen, new Point(0, headerY + HeaderHeight), new Point(w, headerY + HeaderHeight));

            double[] colWidths = { 30, 150, 200, 100, 80, 80, 100, 80 };
            string[] colTitles = { "!", "Tag Name", "Description", "Value / Limit", "Area", "State", "Time", "Duration" };
            
            double curX = 10;
            for (int i = 0; i < colTitles.Length; i++)
            {
                var ft = CreateFormattedText(colTitles[i], SegoeBold, 11, textBrush, dpi);
                dc.DrawText(ft, new Point(curX, headerY + (HeaderHeight - ft.Height) * 0.5));
                curX += colWidths[i];
            }

            double contentY = headerY + HeaderHeight;
            int visibleRowCount = (int)((h - contentY) / RowHeight);

            for (int i = 0; i < visibleRowCount; i++)
            {
                int dataIndex = _scrollOffset + i;
                if (dataIndex >= _alarms.Count) break;

                var a = _alarms[dataIndex];
                var rowRect = new Rect(0, contentY, w, RowHeight);

                if (dataIndex == _selectedRowIndex)
                {
                    dc.DrawRectangle(SelectionBrush, null, rowRect);
                }

                dc.DrawLine(borderPen, new Point(0, contentY + RowHeight), new Point(w, contentY + RowHeight));

                bool isUnack = a.State == AlarmState.Active && a.AcknowledgedTime == null;
                bool flash = isUnack && FlashOnNew && _flashState;

                Color priColor = a.Priority switch
                {
                    AlarmPriority.Critical => Colors.Red,
                    AlarmPriority.High => Colors.Orange,
                    AlarmPriority.Medium => Colors.Gold,
                    AlarmPriority.Low => Colors.DodgerBlue,
                    _ => Colors.Gray
                };

                if (flash) priColor = Colors.White;

                dc.DrawRectangle(Freeze(new SolidColorBrush(priColor)), null, new Rect(0, contentY + 2, 5, RowHeight - 4));

                string[] rowTexts = {
                    ((int)a.Priority).ToString(),
                    a.TagName,
                    a.Description,
                    $"{a.Value} / {a.Limit}",
                    a.Area ?? "",
                    a.State.ToString(),
                    a.ActivatedTime.ToString("HH:mm:ss"),
                    (a.ClearedTime ?? DateTime.Now).Subtract(a.ActivatedTime).ToString(@"hh\:mm\:ss")
                };

                curX = 10;
                for (int j = 0; j < rowTexts.Length; j++)
                {
                    var ft = CreateFormattedText(rowTexts[j], SegoeNormal, 11, textBrush, dpi);
                    ft.MaxTextWidth = Math.Max(10, colWidths[j] - 5);
                    ft.Trimming = TextTrimming.CharacterEllipsis;
                    dc.DrawText(ft, new Point(curX, contentY + (RowHeight - ft.Height) * 0.5));
                    curX += colWidths[j];
                }

                contentY += RowHeight;
            }
        }

        private void DrawSummaryBar(DrawingContext dc, double w, Brush textBrush, double dpi)
        {
            Color[] colors = { Colors.Red, Colors.Orange, Colors.Gold, Colors.DodgerBlue, Colors.Gray };
            string[] names = { "Critical", "High", "Medium", "Low", "Diagnostic" };
            
            double cellW = w / 5.0;
            for(int i = 0; i < 5; i++)
            {
                double x = i * cellW;
                dc.DrawRectangle(Freeze(new SolidColorBrush(colors[i])), null, new Rect(x + 5, 5, 10, SummaryBarHeight - 10));
                
                int count = CountByPriority((AlarmPriority)(i + 1));
                var ft = CreateFormattedText($"{names[i]}: {count}", SegoeBold, 12, textBrush, dpi);
                dc.DrawText(ft, new Point(x + 25, (SummaryBarHeight - ft.Height) * 0.5));
            }
        }

        private void DrawToolbar(DrawingContext dc, Brush textBrush, Pen borderPen, double dpi)
        {
            double y = SummaryBarHeight;
            
            var ftFilter = CreateFormattedText("Filter", SegoeNormal, 11, textBrush, dpi);
            dc.DrawText(ftFilter, new Point(10, y + (ToolbarHeight - ftFilter.Height) * 0.5));
            
            dc.DrawRectangle(null, borderPen, new Rect(100, y + 5, 100, 26));
            var ftAckSel = CreateFormattedText("Ack Selected", SegoeNormal, 11, textBrush, dpi);
            dc.DrawText(ftAckSel, new Point(110, y + (ToolbarHeight - ftAckSel.Height) * 0.5));
            
            dc.DrawRectangle(null, borderPen, new Rect(210, y + 5, 100, 26));
            var ftAckAll = CreateFormattedText("Ack All", SegoeNormal, 11, textBrush, dpi);
            dc.DrawText(ftAckAll, new Point(230, y + (ToolbarHeight - ftAckAll.Height) * 0.5));
            
            dc.DrawRectangle(null, borderPen, new Rect(320, y + 5, 100, 26));
            var ftShelve = CreateFormattedText("Shelve", SegoeNormal, 11, textBrush, dpi);
            dc.DrawText(ftShelve, new Point(340, y + (ToolbarHeight - ftShelve.Height) * 0.5));
        }
    }

    #region Backward Compatibility Shims

    [Obsolete("AlarmSummary is deprecated. Please migrate to ZAlarmSummary instead.")]
    public class AlarmSummary : ZAlarmSummary { }

    [Obsolete("ZeroAlarmSummary is deprecated. Please migrate to ZAlarmSummary instead.")]
    public class ZeroAlarmSummary : ZAlarmSummary { }

    #endregion
}
