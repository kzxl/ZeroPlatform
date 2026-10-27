using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// Industrial Andon LED Light Tower (Stack Light) with pulse/blink support.
    /// Standard tier order: Red, Amber/Yellow, Green, Blue, White.
    /// </summary>
    public class ZLedTower : FrameworkElement
    {
        public static readonly DependencyProperty RedOnProperty =
            DependencyProperty.Register(nameof(RedOn), typeof(bool), typeof(ZLedTower), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty YellowOnProperty =
            DependencyProperty.Register(nameof(YellowOn), typeof(bool), typeof(ZLedTower), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GreenOnProperty =
            DependencyProperty.Register(nameof(GreenOn), typeof(bool), typeof(ZLedTower), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BlueOnProperty =
            DependencyProperty.Register(nameof(BlueOn), typeof(bool), typeof(ZLedTower), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty WhiteOnProperty =
            DependencyProperty.Register(nameof(WhiteOn), typeof(bool), typeof(ZLedTower), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public bool RedOn { get => (bool)GetValue(RedOnProperty); set => SetValue(RedOnProperty, value); }
        public bool YellowOn { get => (bool)GetValue(YellowOnProperty); set => SetValue(YellowOnProperty, value); }
        public bool GreenOn { get => (bool)GetValue(GreenOnProperty); set => SetValue(GreenOnProperty, value); }
        public bool BlueOn { get => (bool)GetValue(BlueOnProperty); set => SetValue(BlueOnProperty, value); }
        public bool WhiteOn { get => (bool)GetValue(WhiteOnProperty); set => SetValue(WhiteOnProperty, value); }

        public bool RedBlink { get; set; } = false;
        private bool _blinkState = false;
        private readonly DispatcherTimer _blinkTimer;

        public ZLedTower()
        {
            ClipToBounds = true;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _blinkTimer.Tick += (s, e) =>
            {
                if (RedBlink)
                {
                    _blinkState = !_blinkState;
                    InvalidateVisual();
                }
            };
            _blinkTimer.Start();
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

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            #if NETFRAMEWORK
            double dpi = 1.0;
            #else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // Background Card
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, 0, w, h));
            dc.DrawRectangle(null, ZeroWpfTheme.BorderPen, new Rect(0.5, 0.5, w - 1, h - 1));

            var titleFt = CreateFormattedText("Andon Tower", ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(titleFt, new Point(12, 10));

            double lampW = Math.Min(48, w * 0.4);
            double lampH = 22;
            double centerX = w / 2.0;
            double lampX = centerX - lampW / 2.0;
            double startY = 36;

            // Cap
            dc.DrawRoundedRectangle(ZeroWpfTheme.BgInput, ZeroWpfTheme.BorderPen, new Rect(lampX - 2, startY - 6, lampW + 4, 6), 3, 3);

            // Lamps array: Red, Yellow, Green, Blue, White
            (string Name, bool IsActive, Color ActiveColor)[] tiers = new[]
            {
                ("RED", RedOn && (!RedBlink || _blinkState), Color.FromRgb(243, 139, 168)),
                ("YEL", YellowOn, Color.FromRgb(249, 226, 175)),
                ("GRN", GreenOn, Color.FromRgb(166, 227, 161)),
                ("BLU", BlueOn, Color.FromRgb(137, 180, 250)),
                ("WHT", WhiteOn, Color.FromRgb(245, 245, 245))
            };

            for (int i = 0; i < tiers.Length; i++)
            {
                var t = tiers[i];
                double ly = startY + i * (lampH + 2);

                Brush brush;
                if (t.IsActive)
                {
                    brush = new SolidColorBrush(t.ActiveColor);
                    brush.Freeze();
                }
                else
                {
                    // Dim/off state
                    brush = new SolidColorBrush(Color.FromArgb(40, t.ActiveColor.R, t.ActiveColor.G, t.ActiveColor.B));
                    brush.Freeze();
                }

                // Lamp cylinder
                dc.DrawRoundedRectangle(brush, ZeroWpfTheme.BorderPen, new Rect(lampX, ly, lampW, lampH), 2, 2);

                // Lamp label indicator
                var tierFt = CreateFormattedText(t.Name, ZeroWpfTheme.BoldTypeface, 9.0, t.IsActive ? Brushes.Black : ZeroWpfTheme.TextMuted, dpi);
                dc.DrawText(tierFt, new Point(centerX - tierFt.Width / 2.0, ly + (lampH - tierFt.Height) / 2.0));
            }

            // Pole & Stand
            double poleY = startY + tiers.Length * (lampH + 2);
            double poleH = Math.Max(16, h - poleY - 14);
            dc.DrawRectangle(ZeroWpfTheme.BorderDefault, null, new Rect(centerX - 4, poleY, 8, poleH));

            // Base plate
            dc.DrawRoundedRectangle(ZeroWpfTheme.BgInput, ZeroWpfTheme.BorderPen, new Rect(centerX - 24, poleY + poleH, 48, 8), 2, 2);
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
    /// Legacy alias for <see cref="ZLedTower"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("LedTower is deprecated and will be removed in 5 release cycles. Please migrate to ZLedTower instead.")]
    public class LedTower : ZLedTower { }

    /// <summary>
    /// Legacy alias for <see cref="ZLedTower"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroLedTower is deprecated and will be removed in 5 release cycles. Please migrate to ZLedTower instead.")]
    public class ZeroLedTower : ZLedTower { }

    #endregion

}
