using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Network;
using ZeroUI.Core.Rendering;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Network
{
    /// <summary>
    /// High-Density Hardware Switch Front-Panel Control (1U rackmount faceplate) for WPF.
    /// Renders 24/48-port RJ45 matrices and SFP/SFP+ optical cages with authentic 60-144 FPS
    /// Ethernet packet burst activity LED blinking, speed-differentiated link indicators,
    /// and zero-allocation hardware drawing.
    /// </summary>
    public class ZSwitchFaceplate : ZeroWpfVisualBase
    {
        #region Dependency Properties

        public static readonly DependencyProperty Rj45PortCountProperty =
            DependencyProperty.Register(
                nameof(Rj45PortCount),
                typeof(int),
                typeof(ZSwitchFaceplate),
                new FrameworkPropertyMetadata(48, FrameworkPropertyMetadataOptions.AffectsRender, OnPortCountsChanged));

        public static readonly DependencyProperty SfpPortCountProperty =
            DependencyProperty.Register(
                nameof(SfpPortCount),
                typeof(int),
                typeof(ZSwitchFaceplate),
                new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsRender, OnPortCountsChanged));

        public int Rj45PortCount
        {
            get => (int)GetValue(Rj45PortCountProperty);
            set => SetValue(Rj45PortCountProperty, value);
        }

        public int SfpPortCount
        {
            get => (int)GetValue(SfpPortCountProperty);
            set => SetValue(SfpPortCountProperty, value);
        }

        private static void OnPortCountsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZSwitchFaceplate faceplate)
            {
                faceplate.RecreateLayout();
            }
        }

        #endregion

        #region Static Frozen Resources (Zero-Alloc)

        private static readonly SolidColorBrush EarBgBrush;
        private static readonly Pen EarBorderPen;
        private static readonly Pen ScrewPen;
        private static readonly SolidColorBrush ScrewBgBrush;

        private static readonly SolidColorBrush Rj45BgBrush;
        private static readonly SolidColorBrush SfpBgBrush;
        private static readonly Pen PortNormalPen;
        private static readonly Pen PortSelectedPen;

        private static readonly SolidColorBrush LedOffBrush;
        private static readonly SolidColorBrush LedGreenBrush;
        private static readonly SolidColorBrush LedCyanBrush;
        private static readonly SolidColorBrush LedAmberBrush;

        private static readonly SolidColorBrush ClipInnerBrush;
        private static readonly Pen ClipBorderPen;

        private static readonly SolidColorBrush StripBgBrush;
        private static readonly Pen StripPen;

        static ZSwitchFaceplate()
        {
            EarBgBrush = new SolidColorBrush(Color.FromRgb(18, 24, 38));
            EarBgBrush.Freeze();

            EarBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(45, 55, 72)), 1.0);
            EarBorderPen.Freeze();

            ScrewPen = new Pen(new SolidColorBrush(Color.FromRgb(71, 85, 105)), 1.0);
            ScrewPen.Freeze();

            ScrewBgBrush = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            ScrewBgBrush.Freeze();

            Rj45BgBrush = new SolidColorBrush(Color.FromRgb(15, 20, 28));
            Rj45BgBrush.Freeze();

            SfpBgBrush = new SolidColorBrush(Color.FromRgb(24, 34, 48));
            SfpBgBrush.Freeze();

            PortNormalPen = new Pen(new SolidColorBrush(Color.FromRgb(40, 49, 66)), 1.0);
            PortNormalPen.Freeze();

            PortSelectedPen = new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 2.0);
            PortSelectedPen.Freeze();

            LedOffBrush = new SolidColorBrush(Color.FromRgb(30, 38, 52));
            LedOffBrush.Freeze();

            LedGreenBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            LedGreenBrush.Freeze();

            LedCyanBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            LedCyanBrush.Freeze();

            LedAmberBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            LedAmberBrush.Freeze();

            ClipInnerBrush = new SolidColorBrush(Color.FromRgb(10, 14, 20));
            ClipInnerBrush.Freeze();

            ClipBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(35, 43, 58)), 0.8);
            ClipBorderPen.Freeze();

            StripBgBrush = new SolidColorBrush(Color.FromRgb(15, 20, 30));
            StripBgBrush.Freeze();

            StripPen = new Pen(new SolidColorBrush(Color.FromRgb(35, 45, 62)), 1.0);
            StripPen.Freeze();
        }

        #endregion

        private SwitchPortLayout _layout;
        private SwitchPort? _selectedPort;
        private readonly Stopwatch _animStopwatch = Stopwatch.StartNew();
        private bool _isRenderingSubscribed;

        // Cached FormattedText
        private readonly FormattedText?[] _portNumFtCache = new FormattedText?[128];
        private string? _cachedBrandText;
        private FormattedText? _cachedBrandFt;
        private string? _cachedStripText;
        private FormattedText? _cachedStripFt;
        private FormattedText? _pwrFt;
        private FormattedText? _sysFt;
        private FormattedText? _poeFt;

        public event EventHandler? SelectedPortChanged;

        public SwitchPortLayout Layout => _layout;

        public SwitchPort? SelectedPort
        {
            get => _selectedPort;
            set
            {
                if (_selectedPort != value)
                {
                    _selectedPort = value;
                    _cachedStripText = null;
                    _cachedStripFt = null;
                    SelectedPortChanged?.Invoke(this, EventArgs.Empty);
                    InvalidateVisual();
                }
            }
        }

        protected override bool AutoAnimate => true;

        public ZSwitchFaceplate()
        {
            _layout = new SwitchPortLayout(48, 4);

            Loaded += OnFaceplateLoaded;
            Unloaded += OnFaceplateUnloaded;
            IsVisibleChanged += OnFaceplateVisibleChanged;
        }

        private void RecreateLayout()
        {
            int rj45 = Math.Max(2, Rj45PortCount);
            int sfp = Math.Max(0, SfpPortCount);
            _layout = new SwitchPortLayout(rj45, sfp);
            _selectedPort = null;
            _cachedBrandText = null;
            _cachedBrandFt = null;
            _cachedStripText = null;
            _cachedStripFt = null;
            InvalidateVisual();
        }

        private void OnFaceplateLoaded(object sender, RoutedEventArgs e)
        {
            UpdateRenderingSubscription();
        }

        private void OnFaceplateUnloaded(object sender, RoutedEventArgs e)
        {
            StopRenderingSubscription();
        }

        private void OnFaceplateVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateRenderingSubscription();
        }

        private void UpdateRenderingSubscription()
        {
            if (IsLoaded && IsVisible)
            {
                if (!_isRenderingSubscribed)
                {
                    CompositionTarget.Rendering += OnCompositionRendering;
                    _isRenderingSubscribed = true;
                }
            }
            else
            {
                StopRenderingSubscription();
            }
        }

        private void StopRenderingSubscription()
        {
            if (_isRenderingSubscribed)
            {
                CompositionTarget.Rendering -= OnCompositionRendering;
                _isRenderingSubscribed = false;
            }
        }

        private void OnCompositionRendering(object? sender, EventArgs e)
        {
            if (IsLoaded && IsVisible)
            {
                InvalidateVisual();
            }
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
            if (w < 200 || h < 80) return;

            #if NETFRAMEWORK
            double dpi = 1.0;
            #else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // Brushed Aluminum Chassis (1U rackmount)
            var chassisBg = ZeroWpfTheme.BgCard;
            var chassisPen = ZeroWpfTheme.BorderPen;
            dc.DrawRoundedRectangle(chassisBg, chassisPen, new Rect(0.5, 0.5, w - 1, h - 1), 6, 6);

            // Rack Mounting Ears
            double earWidth = 24;
            DrawRackEar(dc, new Rect(0, 0, earWidth, h));
            DrawRackEar(dc, new Rect(w - earWidth, 0, earWidth, h));

            double panelLeft = earWidth + 6;
            double panelRight = w - earWidth - 6;
            double panelWidth = panelRight - panelLeft;

            // Branding Bar
            double topBarHeight = 26;
            DrawBrandingBar(dc, new Rect(panelLeft, 6, panelWidth, topBarHeight), dpi);

            // Port Matrix Area
            double portAreaTop = topBarHeight + 8;
            double portAreaHeight = Math.Max(40.0, h - portAreaTop - 34);
            DrawPortMatrix(dc, new Rect(panelLeft, portAreaTop, panelWidth, portAreaHeight), dpi);

            // Diagnostic Inspection Strip
            double statusTop = h - 28;
            DrawInspectionStrip(dc, new Rect(panelLeft, statusTop, panelWidth, 22), dpi);
        }

        private void DrawRackEar(DrawingContext dc, Rect r)
        {
            dc.DrawRectangle(EarBgBrush, EarBorderPen, r);

            // Mounting screw holes
            double holeR = 3.0;
            double cx = r.Left + r.Width / 2.0;

            dc.DrawEllipse(ScrewBgBrush, ScrewPen, new Point(cx, r.Top + 14), holeR, holeR);
            dc.DrawEllipse(ScrewBgBrush, ScrewPen, new Point(cx, r.Bottom - 14), holeR, holeR);
        }

        private void DrawBrandingBar(DrawingContext dc, Rect r, double dpi)
        {
            string brandText = $"ZeroUI L3 Managed Switch - {_layout.Rj45Count}G {_layout.SfpCount}SFP+";
            if (_cachedBrandFt == null || _cachedBrandText != brandText)
            {
                _cachedBrandText = brandText;
                _cachedBrandFt = CreateFormattedText(brandText, ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.TextPrimary, dpi);
            }
            dc.DrawText(_cachedBrandFt, new Point(r.Left + 4, r.Top + 4));

            // System Master LEDs
            double ledX = r.Right - 160;
            double elapsed = _animStopwatch.Elapsed.TotalSeconds;

            // PWR: Constant Green
            DrawLedIndicator(dc, new Point(ledX, r.Top + 12), "PWR", true, LedGreenBrush, ref _pwrFt, dpi);

            // SYS: Heartbeat Blink (1 Hz steady pulse)
            bool sysBlink = ((int)(elapsed * 2.0) % 2) == 0;
            DrawLedIndicator(dc, new Point(ledX + 50, r.Top + 12), "SYS", sysBlink, LedGreenBrush, ref _sysFt, dpi);

            // PoE: Constant Cyan if PoE is configured
            bool poeActive = _layout.CalculateTotalPoeWatts() > 0;
            DrawLedIndicator(dc, new Point(ledX + 100, r.Top + 12), "PoE", poeActive, LedCyanBrush, ref _poeFt, dpi);
        }

        private void DrawLedIndicator(DrawingContext dc, Point p, string label, bool isOn, Brush activeBrush, ref FormattedText? cachedFt, double dpi)
        {
            Brush b = isOn ? activeBrush : LedOffBrush;
            dc.DrawEllipse(b, null, p, 3.5, 3.5);

            if (cachedFt == null)
            {
                cachedFt = CreateFormattedText(label, ZeroWpfTheme.RegularTypeface, 8.0, ZeroWpfTheme.TextSecondary, dpi);
            }
            dc.DrawText(cachedFt, new Point(p.X + 6, p.Y - cachedFt.Height / 2.0));
        }

        private void DrawPortMatrix(DrawingContext dc, Rect r, double dpi)
        {
            int totalRj45 = _layout.Rj45Count;
            int rj45Columns = totalRj45 / 2;
            int totalSfp = _layout.SfpCount;
            int sfpColumns = totalSfp / 2;

            double slotWidth = Math.Min(28.0, (r.Width - 40.0) / (rj45Columns + sfpColumns + 1));
            double slotHeight = (r.Height - 6.0) / 2.0;

            double elapsed = _animStopwatch.Elapsed.TotalSeconds;
            // 20 Hz asynchronous packet traffic clock
            long burstClock = (long)(elapsed * 20.0);

            // Draw RJ45 Ports
            for (int col = 0; col < rj45Columns; col++)
            {
                double x = r.Left + col * (slotWidth + 3);

                // Top port (odd: 1, 3, 5...)
                int topPortIdx = col * 2 + 1;
                Rect topRect = new Rect(x, r.Top, slotWidth, slotHeight);
                DrawSinglePort(dc, topPortIdx, topRect, isSfp: false, burstClock, dpi);

                // Bottom port (even: 2, 4, 6...)
                int botPortIdx = col * 2 + 2;
                Rect botRect = new Rect(x, r.Top + slotHeight + 4, slotWidth, slotHeight);
                DrawSinglePort(dc, botPortIdx, botRect, isSfp: false, burstClock, dpi);
            }

            // Draw SFP+ Ports
            double sfpStartX = r.Left + rj45Columns * (slotWidth + 3) + 16;
            for (int col = 0; col < sfpColumns; col++)
            {
                double x = sfpStartX + col * (slotWidth + 4);

                int topSfpIdx = totalRj45 + col * 2 + 1;
                Rect topRect = new Rect(x, r.Top, slotWidth + 2, slotHeight);
                DrawSinglePort(dc, topSfpIdx, topRect, isSfp: true, burstClock, dpi);

                int botSfpIdx = totalRj45 + col * 2 + 2;
                Rect botRect = new Rect(x, r.Top + slotHeight + 4, slotWidth + 2, slotHeight);
                DrawSinglePort(dc, botSfpIdx, botRect, isSfp: true, burstClock, dpi);
            }
        }

        private void DrawSinglePort(DrawingContext dc, int portNumber, Rect r, bool isSfp, long burstClock, double dpi)
        {
            var port = _layout.FindPort(portNumber);
            bool isSelected = port == _selectedPort;

            // Outer Port Enclosure
            Brush fillBrush = isSfp ? SfpBgBrush : Rj45BgBrush;
            Pen pen = isSelected ? PortSelectedPen : PortNormalPen;
            dc.DrawRoundedRectangle(fillBrush, pen, r, 2, 2);

            // Connector clip recess
            if (!isSfp && r.Width > 14 && r.Height > 18)
            {
                Rect clipRect = new Rect(r.Left + 5, r.Top + 10, r.Width - 10, Math.Max(4, r.Height - 16));
                dc.DrawRectangle(ClipInnerBrush, ClipBorderPen, clipRect);
            }

            // Cached Port Number Label
            FormattedText numFt;
            if (portNumber < _portNumFtCache.Length && _portNumFtCache[portNumber] != null)
            {
                numFt = _portNumFtCache[portNumber]!;
            }
            else
            {
                numFt = CreateFormattedText(portNumber.ToString(), ZeroWpfTheme.RegularTypeface, 7.5, ZeroWpfTheme.TextMuted, dpi);
                if (portNumber < _portNumFtCache.Length)
                {
                    _portNumFtCache[portNumber] = numFt;
                }
            }
            dc.DrawText(numFt, new Point(r.Left + (r.Width - numFt.Width) / 2.0, r.Top + (r.Height - numFt.Height) / 2.0));

            bool linkUp = port?.IsLinkUp ?? false;

            // 1. Link / Speed LED (Left)
            Brush linkBrush;
            if (!linkUp)
            {
                linkBrush = LedOffBrush;
            }
            else
            {
                linkBrush = port?.Speed switch
                {
                    PortSpeed.Speed10G or PortSpeed.Speed25G => LedCyanBrush,
                    PortSpeed.Speed100M => LedAmberBrush,
                    _ => LedGreenBrush // 1G standard
                };
            }
            dc.DrawEllipse(linkBrush, null, new Point(r.Left + 4, r.Top + 4), 2.0, 2.0);

            // 2. Activity LED (Right, dynamic packet burst blinking)
            Brush actBrush;
            if (linkUp && (port?.HasTraffic ?? false))
            {
                // Organic asynchronous Ethernet traffic flicker:
                // Generates rapid packet burst pulses unique to each port's index
                int packetHash = (portNumber * 59) ^ (int)burstClock ^ (int)(burstClock >> 2);
                bool isLedLit = (packetHash & 0x03) != 0; // ~75% duty cycle packet bursts
                actBrush = isLedLit ? LedAmberBrush : LedOffBrush;
            }
            else
            {
                actBrush = LedOffBrush;
            }
            dc.DrawEllipse(actBrush, null, new Point(r.Right - 4, r.Top + 4), 2.0, 2.0);
        }

        private void DrawInspectionStrip(DrawingContext dc, Rect r, double dpi)
        {
            dc.DrawRoundedRectangle(StripBgBrush, StripPen, r, 3, 3);

            string info = _selectedPort != null
                ? $"Port {_selectedPort.PortIndex} | {_selectedPort.Speed} | VLAN {_selectedPort.VlanId} | PoE: {_selectedPort.PoeWatts:F1}W | {_selectedPort.OperationalStatus} | Rx: {_selectedPort.RxBytesPerSec / 1024.0:F1} KB/s, Tx: {_selectedPort.TxBytesPerSec / 1024.0:F1} KB/s"
                : "Select a port to inspect real-time Link, Speed, VLAN, PoE, and Packet Traffic telemetry.";

            if (_cachedStripFt == null || _cachedStripText != info)
            {
                _cachedStripText = info;
                _cachedStripFt = CreateFormattedText(info, ZeroWpfTheme.RegularTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);
            }

            dc.DrawText(_cachedStripFt, new Point(r.Left + 8, r.Top + (r.Height - _cachedStripFt.Height) / 2.0));
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Point p = e.GetPosition(this);

            double earWidth = 24;
            double panelLeft = earWidth + 6;
            double topBarHeight = 26;
            double portAreaTop = topBarHeight + 8;
            double portAreaHeight = Math.Max(40.0, ActualHeight - portAreaTop - 34);

            int totalRj45 = _layout.Rj45Count;
            int rj45Columns = totalRj45 / 2;
            int totalSfp = _layout.SfpCount;
            int sfpColumns = totalSfp / 2;

            double slotWidth = Math.Min(28.0, (ActualWidth - (earWidth * 2) - 52.0) / (rj45Columns + sfpColumns + 1));
            double slotHeight = (portAreaHeight - 6.0) / 2.0;

            // Hit test RJ45
            for (int col = 0; col < rj45Columns; col++)
            {
                double x = panelLeft + col * (slotWidth + 3);
                Rect topRect = new Rect(x, portAreaTop, slotWidth, slotHeight);
                if (topRect.Contains(p))
                {
                    SelectedPort = _layout.FindPort(col * 2 + 1);
                    return;
                }

                Rect botRect = new Rect(x, portAreaTop + slotHeight + 4, slotWidth, slotHeight);
                if (botRect.Contains(p))
                {
                    SelectedPort = _layout.FindPort(col * 2 + 2);
                    return;
                }
            }

            // Hit test SFP+
            double sfpStartX = panelLeft + rj45Columns * (slotWidth + 3) + 16;
            for (int col = 0; col < sfpColumns; col++)
            {
                double x = sfpStartX + col * (slotWidth + 4);
                Rect topRect = new Rect(x, portAreaTop, slotWidth + 2, slotHeight);
                if (topRect.Contains(p))
                {
                    SelectedPort = _layout.FindPort(totalRj45 + col * 2 + 1);
                    return;
                }

                Rect botRect = new Rect(x, portAreaTop + slotHeight + 4, slotWidth + 2, slotHeight);
                if (botRect.Contains(p))
                {
                    SelectedPort = _layout.FindPort(totalRj45 + col * 2 + 2);
                    return;
                }
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSwitchFaceplate"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SwitchFaceplate is deprecated and will be removed in 5 release cycles. Please migrate to ZSwitchFaceplate instead.")]
    public class SwitchFaceplate : ZSwitchFaceplate { }

    /// <summary>
    /// Legacy alias for <see cref="ZSwitchFaceplate"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroSwitchFaceplate is deprecated and will be removed in 5 release cycles. Please migrate to ZSwitchFaceplate instead.")]
    public class ZeroSwitchFaceplate : ZSwitchFaceplate { }

    #endregion

}
