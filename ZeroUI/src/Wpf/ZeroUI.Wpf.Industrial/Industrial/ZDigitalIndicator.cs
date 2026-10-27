using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Scada;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// High-contrast industrial digital telemetry readout indicator for SCADA and MES monitoring in WPF.
    /// Provides 4-tier alarm threshold color transitions (LowLow, Low, High, HighHigh) and direct SCADA tag binding.
    /// </summary>
    public class ZDigitalIndicator : FrameworkElement, IScadaBindable
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(double),
                typeof(ZDigitalIndicator),
                new FrameworkPropertyMetadata(48.7, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(
                nameof(Unit),
                typeof(string),
                typeof(ZDigitalIndicator),
                new FrameworkPropertyMetadata("bar", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TagLabelProperty =
            DependencyProperty.Register(
                nameof(TagLabel),
                typeof(string),
                typeof(ZDigitalIndicator),
                new FrameworkPropertyMetadata("PT-101", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FormatProperty =
            DependencyProperty.Register(
                nameof(Format),
                typeof(string),
                typeof(ZDigitalIndicator),
                new FrameworkPropertyMetadata("0.0", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LowLowAlarmProperty =
            DependencyProperty.Register(
                nameof(LowLowAlarm),
                typeof(double),
                typeof(ZDigitalIndicator),
                new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LowWarningProperty =
            DependencyProperty.Register(
                nameof(LowWarning),
                typeof(double),
                typeof(ZDigitalIndicator),
                new FrameworkPropertyMetadata(20.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty HighWarningProperty =
            DependencyProperty.Register(
                nameof(HighWarning),
                typeof(double),
                typeof(ZDigitalIndicator),
                new FrameworkPropertyMetadata(80.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty HighHighAlarmProperty =
            DependencyProperty.Register(
                nameof(HighHighAlarm),
                typeof(double),
                typeof(ZDigitalIndicator),
                new FrameworkPropertyMetadata(90.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BoundTagPathProperty =
            DependencyProperty.Register(
                nameof(BoundTagPath),
                typeof(string),
                typeof(ZDigitalIndicator),
                new PropertyMetadata(null));

        private bool _isHovered;

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        public string TagLabel
        {
            get => (string)GetValue(TagLabelProperty);
            set => SetValue(TagLabelProperty, value);
        }

        public string Format
        {
            get => (string)GetValue(FormatProperty);
            set => SetValue(FormatProperty, value);
        }

        public double LowLowAlarm
        {
            get => (double)GetValue(LowLowAlarmProperty);
            set => SetValue(LowLowAlarmProperty, value);
        }

        public double LowWarning
        {
            get => (double)GetValue(LowWarningProperty);
            set => SetValue(LowWarningProperty, value);
        }

        public double HighWarning
        {
            get => (double)GetValue(HighWarningProperty);
            set => SetValue(HighWarningProperty, value);
        }

        public double HighHighAlarm
        {
            get => (double)GetValue(HighHighAlarmProperty);
            set => SetValue(HighHighAlarmProperty, value);
        }

        public string? BoundTagPath
        {
            get => (string?)GetValue(BoundTagPathProperty);
            set => SetValue(BoundTagPathProperty, value);
        }

        public ZDigitalIndicator()
        {
            Width = 140;
            Height = 62;
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag != null)
            {
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.BeginInvoke(new Action(() => OnTagValueChanged(tag)));
                    return;
                }
                Value = tag.GetValue<double>();
            }
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            InvalidateVisual();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            InvalidateVisual();
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

        private string FormatValue(double val)
        {
            string fmt = Format;
            if (string.IsNullOrWhiteSpace(fmt)) return val.ToString("0.0", CultureInfo.InvariantCulture);

            try
            {
                if (fmt.Contains("{0"))
                {
                    return string.Format(CultureInfo.InvariantCulture, fmt, val);
                }
                return val.ToString(fmt, CultureInfo.InvariantCulture);
            }
            catch
            {
                return val.ToString("0.0", CultureInfo.InvariantCulture);
            }
        }

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

            double val = Value;
            Brush valueBrush;
            Brush borderBrush;

            if (val <= LowLowAlarm || val >= HighHighAlarm)
            {
                valueBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Critical Red
                borderBrush = valueBrush;
            }
            else if (val <= LowWarning || val >= HighWarning)
            {
                valueBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Warning Amber
                borderBrush = valueBrush;
            }
            else
            {
                valueBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)); // Normal Cyan/Blue
                borderBrush = _isHovered ? new SolidColorBrush(Color.FromRgb(59, 130, 246)) : new SolidColorBrush(Color.FromRgb(51, 65, 85));
            }

            Pen borderPen = new Pen(borderBrush, _isHovered ? 2.0 : 1.2);
            Brush bgBrush = new SolidColorBrush(Color.FromRgb(15, 23, 42)); // Industrial dark slate

            // 1. Draw Panel Card
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(1, 1, Math.Max(1, w - 2), Math.Max(1, h - 2)), 4, 4);

            // 2. Tag Label (Top Left)
            if (!string.IsNullOrEmpty(TagLabel))
            {
                var labelFt = CreateFormattedText(
                    TagLabel,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    10.0,
                    new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    dpi);
                dc.DrawText(labelFt, new Point(8, 6));
            }

            // 3. Main Numeric Readout
            string valText = FormatValue(val);
            var numFt = CreateFormattedText(
                valText,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                20.0,
                valueBrush,
                dpi);
            dc.DrawText(numFt, new Point(8, 24));

            // 4. Engineering Unit (Placed next to value)
            if (!string.IsNullOrEmpty(Unit))
            {
                var unitFt = CreateFormattedText(
                    Unit,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                    11.0,
                    new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    dpi);
                dc.DrawText(unitFt, new Point(12 + numFt.Width, 31));
            }
        }
    }
    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZDigitalIndicator"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DigitalIndicator is deprecated and will be removed in 5 release cycles. Please migrate to ZDigitalIndicator instead.")]
    public class DigitalIndicator : ZDigitalIndicator { }

    /// <summary>
    /// Legacy alias for <see cref="ZDigitalIndicator"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroDigitalIndicator is deprecated and will be removed in 5 release cycles. Please migrate to ZDigitalIndicator instead.")]
    public class ZeroDigitalIndicator : ZDigitalIndicator { }

    #endregion

}
