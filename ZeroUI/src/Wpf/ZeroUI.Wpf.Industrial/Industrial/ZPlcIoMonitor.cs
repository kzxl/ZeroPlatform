using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    public class PlcCoilChangedEventArgs : EventArgs
    {
        public int BitIndex { get; }
        public bool NewState { get; }

        public PlcCoilChangedEventArgs(int bitIndex, bool newState)
        {
            BitIndex = bitIndex;
            NewState = newState;
        }
    }

    /// <summary>
    /// Industrial PLC Digital I/O 16-Bit Monitor for SCADA and automation engineering in WPF.
    /// Visualizes 16-bit input bank (DI 00..15) and 16-bit output bank (DO 00..15) with hex register readouts,
    /// LED bit status indicators, and interactive coil toggling.
    /// </summary>
    public class ZPlcIoMonitor : FrameworkElement
    {
        public static readonly DependencyProperty DigitalInputsProperty =
            DependencyProperty.Register(nameof(DigitalInputs), typeof(ushort), typeof(ZPlcIoMonitor),
                new FrameworkPropertyMetadata((ushort)0x0055, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty DigitalOutputsProperty =
            DependencyProperty.Register(nameof(DigitalOutputs), typeof(ushort), typeof(ZPlcIoMonitor),
                new FrameworkPropertyMetadata((ushort)0x0007, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AllowSimulationClickProperty =
            DependencyProperty.Register(nameof(AllowSimulationClick), typeof(bool), typeof(ZPlcIoMonitor),
                new FrameworkPropertyMetadata(true));

        public ushort DigitalInputs
        {
            get => (ushort)GetValue(DigitalInputsProperty);
            set => SetValue(DigitalInputsProperty, value);
        }

        public ushort DigitalOutputs
        {
            get => (ushort)GetValue(DigitalOutputsProperty);
            set => SetValue(DigitalOutputsProperty, value);
        }

        public bool AllowSimulationClick
        {
            get => (bool)GetValue(AllowSimulationClickProperty);
            set => SetValue(AllowSimulationClickProperty, value);
        }

        public event EventHandler<PlcCoilChangedEventArgs>? OutputCoilChanged;

        private readonly Rect[] _doRects = new Rect[16];

        private static readonly Typeface SegoeBold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface SegoeNormal = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private static readonly Typeface ConsolasBold = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        private static readonly Brush DarkCardBg = Freeze(new SolidColorBrush(Color.FromRgb(30, 41, 59)));
        private static readonly Brush LightCardBg = Freeze(new SolidColorBrush(Color.FromRgb(241, 245, 249)));
        private static readonly Brush DarkText = Freeze(new SolidColorBrush(Color.FromRgb(248, 250, 252)));
        private static readonly Brush LightText = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42)));
        private static readonly Brush SubText = Freeze(new SolidColorBrush(Color.FromRgb(148, 163, 184)));
        private static readonly Brush DiOnBrush = Freeze(new SolidColorBrush(Color.FromRgb(52, 211, 153)));
        private static readonly Brush DoOnBrush = Freeze(new SolidColorBrush(Color.FromRgb(251, 191, 36)));
        private static readonly Brush LedOffDark = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42)));
        private static readonly Brush LedOffLight = Freeze(new SolidColorBrush(Color.FromRgb(203, 213, 225)));
        private static readonly Pen DarkBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(71, 85, 105)), 1.0));
        private static readonly Pen LightBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(203, 213, 225)), 1.0));
        private static readonly Pen LedBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(51, 65, 85)), 1.0));

        private static T Freeze<T>(T freezable) where T : Freezable
        {
            if (freezable.CanFreeze) freezable.Freeze();
            return freezable;
        }

        public ZPlcIoMonitor()
        {
            ClipToBounds = true;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public void SetInputBit(int bitIndex, bool state)
        {
            if (bitIndex < 0 || bitIndex > 15) return;
            if (state) DigitalInputs |= (ushort)(1 << bitIndex);
            else DigitalInputs &= (ushort)~(1 << bitIndex);
        }

        public void SetOutputBit(int bitIndex, bool state)
        {
            if (bitIndex < 0 || bitIndex > 15) return;
            if (state) DigitalOutputs |= (ushort)(1 << bitIndex);
            else DigitalOutputs &= (ushort)~(1 << bitIndex);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (!AllowSimulationClick) return;

            Point pt = e.GetPosition(this);
            for (int i = 0; i < 16; i++)
            {
                if (_doRects[i].Contains(pt))
                {
                    bool cur = (DigitalOutputs & (1 << i)) != 0;
                    SetOutputBit(i, !cur);
                    OutputCoilChanged?.Invoke(this, new PlcCoilChangedEventArgs(i, !cur));
                    break;
                }
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(340, 110);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            bool isDark = ZeroWpfTheme.IsDark;
            Brush cardBg = isDark ? DarkCardBg : LightCardBg;
            Brush textBrush = isDark ? DarkText : LightText;
            Pen borderPen = isDark ? DarkBorderPen : LightBorderPen;

            #if NETFRAMEWORK
            double dpi = 1.0;
            #else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // 1. Enclosure Frame
            dc.DrawRectangle(cardBg, borderPen, new Rect(0, 0, w, h));

            // 2. Title & Status Bar
            var titleText = new FormattedText("PLC I/O Bit Matrix (16-DI / 16-DO)", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 11, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(titleText, new Point(10, 6));

            // 3. DI Row
            DrawBitBank(dc, 10, 28, "DI:", DigitalInputs, null, DiOnBrush, isDark, dpi);

            // 4. DO Row
            DrawBitBank(dc, 10, 66, "DO:", DigitalOutputs, _doRects, DoOnBrush, isDark, dpi);
        }

        private void DrawBitBank(DrawingContext dc, double x, double y, string label, ushort register, Rect[]? hitRects, Brush onColor, bool isDark, double dpi)
        {
            double w = ActualWidth;
            Brush textBrush = isDark ? DarkText : LightText;
            Brush ledOff = isDark ? LedOffDark : LedOffLight;

            // Bank Label
            var lblText = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 11, SubText
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(lblText, new Point(x, y + 4));

            // Hex Readout
            string hexStr = $"0x{register:X4}";
            var hexText = new FormattedText(hexStr, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ConsolasBold, 11, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(hexText, new Point(w - hexText.Width - 12, y + 4));

            // 16 Bits
            double startBitX = x + 38;
            double bitW = 14;
            double bitH = 14;
            double spacing = 3;

            for (int i = 15; i >= 0; i--)
            {
                int bitIndex = i;
                bool isBitOn = (register & (1 << bitIndex)) != 0;

                double bx = startBitX + ((15 - i) * (bitW + spacing));
                if (i < 8) bx += 6; // Byte gap

                var r = new Rect(bx, y + 8, bitW, bitH);
                if (hitRects != null)
                {
                    hitRects[bitIndex] = r;
                }

                // LED Circle
                dc.DrawEllipse(isBitOn ? onColor : ledOff, LedBorderPen, new Point(bx + bitW * 0.5, y + 8 + bitH * 0.5), bitW * 0.5, bitH * 0.5);

                // Halo glow when on
                if (isBitOn)
                {
                    var glowBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
                    glowBrush.Freeze();
                    dc.DrawEllipse(glowBrush, null, new Point(bx + bitW * 0.5, y + 8 + bitH * 0.5), bitW * 0.7, bitH * 0.7);
                }

                // Bit label (every second bit)
                if (i % 2 == 0 || i == 15)
                {
                    var numText = new FormattedText($"{i}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeNormal, 8, SubText
                    #if !NETFRAMEWORK
                    , dpi
                    #endif
                    );
                    dc.DrawText(numText, new Point(bx + (bitW - numText.Width) * 0.5, y - 6));
                }
            }
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
    /// Legacy alias for <see cref="ZPlcIoMonitor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PlcIoMonitor is deprecated and will be removed in 5 release cycles. Please migrate to ZPlcIoMonitor instead.")]
    public class PlcIoMonitor : ZPlcIoMonitor { }

    /// <summary>
    /// Legacy alias for <see cref="ZPlcIoMonitor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroPlcIoMonitor is deprecated and will be removed in 5 release cycles. Please migrate to ZPlcIoMonitor instead.")]
    public class ZeroPlcIoMonitor : ZPlcIoMonitor { }

    #endregion

}
