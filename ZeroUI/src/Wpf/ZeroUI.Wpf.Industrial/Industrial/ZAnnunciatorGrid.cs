using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    public enum IsaAlarmState
    {
        Normal,
        Unacknowledged,
        Acknowledged,
        ReturnToNormal
    }

    public enum IsaAlarmSeverity
    {
        Critical,
        High,
        Medium,
        Low
    }

    public class IsaAlarmTile
    {
        public string TagPath { get; set; } = "";
        public string Title { get; set; } = "";
        public IsaAlarmSeverity Severity { get; set; } = IsaAlarmSeverity.High;
        public IsaAlarmState State { get; set; } = IsaAlarmState.Normal;
        public Rect Bounds { get; internal set; }
        public DateTime TriggeredTime { get; set; }
    }

    /// <summary>
    /// Industrial Alarm Annunciator Grid adhering strictly to standard ISA-18.2 for ZeroUI WPF.
    /// Features fast flash unacknowledged alarms, steady acknowledged alarms, and integrated command bar.
    /// </summary>
    public class ZAnnunciatorGrid : FrameworkElement, IScadaBindable
    {
        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(nameof(Columns), typeof(int), typeof(ZAnnunciatorGrid),
                new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty RowsProperty =
            DependencyProperty.Register(nameof(Rows), typeof(int), typeof(ZAnnunciatorGrid),
                new FrameworkPropertyMetadata(3, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BoundTagPathProperty =
            DependencyProperty.Register(nameof(BoundTagPath), typeof(string), typeof(ZAnnunciatorGrid),
                new FrameworkPropertyMetadata(null));

        public int Columns
        {
            get => (int)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, Math.Max(1, value));
        }

        public int Rows
        {
            get => (int)GetValue(RowsProperty);
            set => SetValue(RowsProperty, Math.Max(1, value));
        }

        public string? BoundTagPath
        {
            get => (string?)GetValue(BoundTagPathProperty);
            set => SetValue(BoundTagPathProperty, value);
        }

        public bool IsSilenced => _isSilenced;

        private readonly List<IsaAlarmTile> _tiles = new List<IsaAlarmTile>();
        private IDisposable? _clockToken;
        private bool _blinkFast;
        private bool _blinkSlow;
        private float _timerFast;
        private float _timerSlow;
        private bool _isTestMode;
        private bool _isSilenced;

        private Rect _btnAckRect;
        private Rect _btnSilenceRect;
        private Rect _btnResetRect;
        private Rect _btnTestRect;

        private static readonly Typeface SegoeBold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface SegoeNormal = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private static readonly Brush DarkTileNormal = Freeze(new SolidColorBrush(Color.FromRgb(30, 41, 59)));
        private static readonly Brush LightTileNormal = Freeze(new SolidColorBrush(Color.FromRgb(241, 245, 249)));
        private static readonly Pen DarkBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(71, 85, 105)), 1.5));
        private static readonly Pen LightBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(203, 213, 225)), 1.5));

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

        public ZAnnunciatorGrid()
        {
            ClipToBounds = true;
            GenerateDefaultTiles();

            Loaded += (s, e) =>
            {
                _clockToken = ZeroAnimationClock.Subscribe(OnAnimationFrame);
                ZeroTagEngine.RegisterBindable(this);
            };
            Unloaded += (s, e) =>
            {
                _clockToken?.Dispose();
                _clockToken = null;
                ZeroTagEngine.UnregisterBindable(this);
            };
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void GenerateDefaultTiles()
        {
            _tiles.Clear();
            string[] tags = {
                "PUMP-101 TRIP", "HIGH PRESS TK-201", "VALVE-302 FAULT", "TEMP HIGH R-101",
                "LOW LEVEL TK-201", "EMERGENCY STOP", "PLC COMMS LOSS", "BEARING VIB M-101",
                "GAS LEAK DETECT", "SMOKE ALARM ZONE1", "FEED WATER TRIP", "COOLING LOSS"
            };

            for (int i = 0; i < 12; i++)
            {
                _tiles.Add(new IsaAlarmTile
                {
                    TagPath = $"ALM-{100 + i}",
                    Title = tags[i],
                    Severity = (IsaAlarmSeverity)(i % 4),
                    State = (i == 0 || i == 1) ? IsaAlarmState.Unacknowledged : (i == 4 ? IsaAlarmState.Acknowledged : IsaAlarmState.Normal)
                });
            }
        }

        private void OnAnimationFrame(double deltaSeconds, long frameCount)
        {
            _timerFast += (float)deltaSeconds;
            if (_timerFast >= 0.3f)
            {
                _timerFast = 0f;
                _blinkFast = !_blinkFast;
                InvalidateVisual();
            }

            _timerSlow += (float)deltaSeconds;
            if (_timerSlow >= 0.8f)
            {
                _timerSlow = 0f;
                _blinkSlow = !_blinkSlow;
                InvalidateVisual();
            }
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
        }

        public void AcknowledgeAll()
        {
            foreach (var tile in _tiles)
            {
                if (tile.State == IsaAlarmState.Unacknowledged)
                {
                    tile.State = IsaAlarmState.Acknowledged;
                }
            }
            InvalidateVisual();
        }

        public void ResetAlarms()
        {
            foreach (var tile in _tiles)
            {
                if (tile.State == IsaAlarmState.ReturnToNormal || tile.State == IsaAlarmState.Acknowledged)
                {
                    tile.State = IsaAlarmState.Normal;
                }
            }
            InvalidateVisual();
        }

        /// <summary>
        /// Clears all alarm tiles in the annunciator matrix.
        /// </summary>
        public void ClearTiles()
        {
            _tiles.Clear();
            InvalidateVisual();
        }

        /// <summary>
        /// Dynamically appends an alarm tile to the matrix.
        /// </summary>
        public void AddAlarm(string tagPath, string title, IsaAlarmSeverity severity)
        {
            _tiles.Add(new IsaAlarmTile
            {
                TagPath = tagPath,
                Title = title,
                Severity = severity,
                State = IsaAlarmState.Normal
            });
            InvalidateVisual();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Point pt = e.GetPosition(this);

            if (_btnAckRect.Contains(pt))
            {
                AcknowledgeAll();
            }
            else if (_btnSilenceRect.Contains(pt))
            {
                _isSilenced = !_isSilenced;
                InvalidateVisual();
            }
            else if (_btnResetRect.Contains(pt))
            {
                ResetAlarms();
            }
            else if (_btnTestRect.Contains(pt))
            {
                _isTestMode = !_isTestMode;
                InvalidateVisual();
            }
            else
            {
                foreach (var tile in _tiles)
                {
                    if (tile.Bounds.Contains(pt))
                    {
                        if (tile.State == IsaAlarmState.Unacknowledged)
                        {
                            tile.State = IsaAlarmState.Acknowledged;
                            InvalidateVisual();
                        }
                        break;
                    }
                }
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(480, 320);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            bool isDark = ZeroWpfTheme.IsDark;
            Pen borderPen = isDark ? DarkBorderPen : LightBorderPen;

            #if NETFRAMEWORK
            double dpi = 1.0;
            #else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // Outer Frame
            dc.DrawRectangle(isDark ? Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42))) : Freeze(new SolidColorBrush(Color.FromRgb(241, 245, 249))), borderPen, new Rect(0, 0, w, h));

            // Command Bar (Bottom 36px)
            double cmdHeight = 36;
            double gridHeight = h - cmdHeight;
            var cmdRect = new Rect(0, gridHeight, w, cmdHeight);
            dc.DrawRectangle(isDark ? Freeze(new SolidColorBrush(Color.FromRgb(30, 41, 59))) : Freeze(new SolidColorBrush(Color.FromRgb(226, 232, 240))), borderPen, cmdRect);

            double btnW = 80;
            double btnH = 24;
            double btnY = gridHeight + (cmdHeight - btnH) * 0.5;

            _btnAckRect = new Rect(8, btnY, btnW, btnH);
            _btnSilenceRect = new Rect(96, btnY, btnW, btnH);
            _btnResetRect = new Rect(184, btnY, btnW, btnH);
            _btnTestRect = new Rect(272, btnY, btnW, btnH);

            DrawButton(dc, _btnAckRect, "ACK", Color.FromRgb(239, 68, 68), dpi);
            DrawButton(dc, _btnSilenceRect, _isSilenced ? "UNSILENCE" : "SILENCE", Color.FromRgb(245, 158, 11), dpi);
            DrawButton(dc, _btnResetRect, "RESET", Color.FromRgb(59, 130, 246), dpi);
            DrawButton(dc, _btnTestRect, _isTestMode ? "END TEST" : "LAMP TEST", Color.FromRgb(148, 163, 184), dpi);

            // Annunciator Grid Matrix
            int cols = Columns;
            int rows = Rows;
            double padding = 4;
            double tileW = (w - (cols + 1) * padding) / cols;
            double tileH = (gridHeight - (rows + 1) * padding) / rows;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int index = r * cols + c;
                    if (index >= _tiles.Count) break;

                    var tile = _tiles[index];
                    double tx = padding + c * (tileW + padding);
                    double ty = padding + r * (tileH + padding);
                    var tRect = new Rect(tx, ty, tileW, tileH);
                    tile.Bounds = tRect;

                    DrawTile(dc, tile, tRect, isDark, dpi);
                }
            }
        }

        private void DrawTile(DrawingContext dc, IsaAlarmTile tile, Rect r, bool isDark, double dpi)
        {
            Color baseColor = tile.Severity switch
            {
                IsaAlarmSeverity.Critical => Color.FromRgb(239, 68, 68),
                IsaAlarmSeverity.High => Color.FromRgb(249, 115, 22),
                IsaAlarmSeverity.Medium => Color.FromRgb(245, 158, 11),
                _ => Color.FromRgb(59, 130, 246)
            };

            bool isLit = false;
            if (_isTestMode)
            {
                isLit = true;
            }
            else
            {
                switch (tile.State)
                {
                    case IsaAlarmState.Unacknowledged:
                        isLit = _blinkFast;
                        break;
                    case IsaAlarmState.Acknowledged:
                        isLit = true;
                        break;
                    case IsaAlarmState.ReturnToNormal:
                        isLit = _blinkSlow;
                        break;
                    default:
                        isLit = false;
                        break;
                }
            }

            Brush bgBrush = isLit
                ? Freeze(new SolidColorBrush(baseColor))
                : (isDark ? DarkTileNormal : LightTileNormal);

            Brush textBrush = isLit
                ? Brushes.White
                : (isDark ? Freeze(new SolidColorBrush(Color.FromRgb(203, 213, 225))) : Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42))));

            dc.DrawRoundedRectangle(bgBrush, new Pen(isLit ? Brushes.White : Freeze(new SolidColorBrush(Color.FromRgb(71, 85, 105))), 1.0), r, 4, 4);

            // Title Text
            var ft = CreateFormattedText(tile.Title, SegoeBold, 10, textBrush, dpi);
            ft.MaxTextWidth = Math.Max(10, r.Width - 10);
            ft.MaxTextHeight = Math.Max(10, r.Height - 10);
            ft.TextAlignment = TextAlignment.Center;
            dc.DrawText(ft, new Point(r.X + (r.Width - ft.Width) * 0.5, r.Y + (r.Height - ft.Height) * 0.5));
        }

        private void DrawButton(DrawingContext dc, Rect r, string text, Color accent, double dpi)
        {
            var btnBg = Freeze(new SolidColorBrush(Color.FromRgb(45, 55, 72)));
            dc.DrawRoundedRectangle(btnBg, new Pen(Freeze(new SolidColorBrush(accent)), 1.2), r, 3, 3);

            var ft = CreateFormattedText(text, SegoeBold, 10, Brushes.White, dpi);
            dc.DrawText(ft, new Point(r.X + (r.Width - ft.Width) * 0.5, r.Y + (r.Height - ft.Height) * 0.5));
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
    /// Legacy alias for <see cref="ZAnnunciatorGrid"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("AnnunciatorGrid is deprecated and will be removed in 5 release cycles. Please migrate to ZAnnunciatorGrid instead.")]
    public class AnnunciatorGrid : ZAnnunciatorGrid { }

    /// <summary>
    /// Legacy alias for <see cref="ZAnnunciatorGrid"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroAnnunciatorGrid is deprecated and will be removed in 5 release cycles. Please migrate to ZAnnunciatorGrid instead.")]
    public class ZeroAnnunciatorGrid : ZAnnunciatorGrid { }

    #endregion

}
