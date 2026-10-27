using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Scada.Safety;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    public enum ValveState
    {
        Closed,
        Open,
        MidPosition,
        Fault
    }

    /// <summary>
    /// Industrial standard P&ID process valve with actuator and analog opening percentage for ZeroUI WPF.
    /// Supports direct telemetry binding via <see cref="IScadaBindable"/>.
    /// </summary>
    public class ZIndustrialValve : FrameworkElement, IScadaBindable
    {
        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register(nameof(State), typeof(ValveState), typeof(ZIndustrialValve),
                new FrameworkPropertyMetadata(ValveState.Open, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StatusFlagsProperty =
            DependencyProperty.Register(nameof(StatusFlags), typeof(DeviceStatusFlags), typeof(ZIndustrialValve),
                new FrameworkPropertyMetadata(DeviceStatusFlags.None, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty OpeningPercentProperty =
            DependencyProperty.Register(nameof(OpeningPercent), typeof(double), typeof(ZIndustrialValve),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TagLabelProperty =
            DependencyProperty.Register(nameof(TagLabel), typeof(string), typeof(ZIndustrialValve),
                new FrameworkPropertyMetadata("FCV-102", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BoundTagPathProperty =
            DependencyProperty.Register(nameof(BoundTagPath), typeof(string), typeof(ZIndustrialValve),
                new FrameworkPropertyMetadata(null));

        public ValveState State
        {
            get => (ValveState)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        public DeviceStatusFlags StatusFlags
        {
            get => (DeviceStatusFlags)GetValue(StatusFlagsProperty);
            set => SetValue(StatusFlagsProperty, value);
        }

        public double OpeningPercent
        {
            get => (double)GetValue(OpeningPercentProperty);
            set => SetValue(OpeningPercentProperty, Math.Max(0.0, Math.Min(100.0, value)));
        }

        public string TagLabel
        {
            get => (string)GetValue(TagLabelProperty);
            set => SetValue(TagLabelProperty, value ?? "");
        }

        public string? BoundTagPath
        {
            get => (string?)GetValue(BoundTagPathProperty);
            set => SetValue(BoundTagPathProperty, value);
        }

        private static readonly Typeface SegoeBold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface SegoeNormal = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private static readonly Brush OpenGreen = Freeze(new SolidColorBrush(Color.FromRgb(34, 197, 94)));
        private static readonly Brush ClosedGray = Freeze(new SolidColorBrush(Color.FromRgb(148, 163, 184)));
        private static readonly Brush MidAmber = Freeze(new SolidColorBrush(Color.FromRgb(245, 158, 11)));
        private static readonly Brush FaultRed = Freeze(new SolidColorBrush(Color.FromRgb(239, 68, 68)));
        private static readonly Brush DarkText = Freeze(new SolidColorBrush(Color.FromRgb(248, 250, 252)));
        private static readonly Brush LightText = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42)));
        private static readonly Pen DarkBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(71, 85, 105)), 1.5));
        private static readonly Pen LightBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(203, 213, 225)), 1.5));

        private static T Freeze<T>(T freezable) where T : Freezable
        {
            if (freezable.CanFreeze) freezable.Freeze();
            return freezable;
        }

        public ZIndustrialValve()
        {
            ClipToBounds = true;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            Unloaded += (s, e) => ZeroTagEngine.UnregisterBindable(this);
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag?.Value == null) return;
            if (double.TryParse(tag.Value.ToString(), out var pos))
            {
                OpeningPercent = pos;
                if (pos <= 0) State = ValveState.Closed;
                else if (pos >= 100) State = ValveState.Open;
                else State = ValveState.MidPosition;
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(130, 110);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            bool isDark = ZeroWpfTheme.IsDark;
            Brush bodyBrush = State switch
            {
                ValveState.Open => OpenGreen,
                ValveState.Closed => ClosedGray,
                ValveState.MidPosition => MidAmber,
                _ => FaultRed
            };

            Brush textBrush = isDark ? DarkText : LightText;
            Pen borderPen = isDark ? DarkBorderPen : LightBorderPen;

            #if !NETFRAMEWORK
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // 1. Tag Label Header
            var tagText = new FormattedText(TagLabel, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 11, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(tagText, new Point(8, 6));

            // 2. Center coordinates for P&ID valve body
            double cx = w * 0.5;
            double cy = h * 0.62;
            double halfW = Math.Min(w * 0.28, 36.0);
            double halfH = Math.Min(h * 0.2, 22.0);

            // Left Triangle
            var leftTri = new StreamGeometry();
            using (var ctx = leftTri.Open())
            {
                ctx.BeginFigure(new Point(cx - halfW, cy - halfH), true, true);
                ctx.LineTo(new Point(cx, cy), true, false);
                ctx.LineTo(new Point(cx - halfW, cy + halfH), true, false);
            }
            leftTri.Freeze();
            dc.DrawGeometry(bodyBrush, borderPen, leftTri);

            // Right Triangle
            var rightTri = new StreamGeometry();
            using (var ctx = rightTri.Open())
            {
                ctx.BeginFigure(new Point(cx + halfW, cy - halfH), true, true);
                ctx.LineTo(new Point(cx, cy), true, false);
                ctx.LineTo(new Point(cx + halfW, cy + halfH), true, false);
            }
            rightTri.Freeze();
            dc.DrawGeometry(bodyBrush, borderPen, rightTri);

            // Valve Stem (Neck)
            double stemTop = cy - halfH - 12;
            dc.DrawLine(borderPen, new Point(cx, cy), new Point(cx, stemTop));

            // Actuator (Diaphragm dome or circular handwheel)
            double actRadius = 14;
            dc.DrawEllipse(bodyBrush, borderPen, new Point(cx, stemTop - actRadius), actRadius, actRadius);

            // 3. Opening % Readout
            string pctStr = $"{OpeningPercent:0}%";
            var pctText = new FormattedText(pctStr, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeNormal, 10, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(pctText, new Point(w - pctText.Width - 8, 6));

            // Status string at bottom
            string stateStr = State.ToString().ToUpperInvariant();
            var stateText = new FormattedText(stateStr, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 9.5, bodyBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(stateText, new Point(cx - stateText.Width / 2, h - 16));
        }
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroTagEngine.RegisterBindable(this);
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
    /// Legacy alias for <see cref="ZIndustrialValve"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("IndustrialValve is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialValve instead.")]
    public class IndustrialValve : ZIndustrialValve { }

    /// <summary>
    /// Legacy alias for <see cref="ZIndustrialValve"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroIndustrialValve is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialValve instead.")]
    public class ZeroIndustrialValve : ZIndustrialValve { }

    #endregion

}
