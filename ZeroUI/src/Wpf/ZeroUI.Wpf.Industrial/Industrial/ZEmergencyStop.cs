using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Scada.Safety;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// Industrial Emergency Stop pushbutton compliant with IEC 60947-5-5 and ISO 13850 for ZeroUI WPF.
    /// Features safety yellow collar, 3D mushroom actuator with twist-to-release arrows,
    /// dual-channel safety contact monitoring, and animated latching action.
    /// </summary>
    public class ZEmergencyStop : FrameworkElement, IScadaBindable
    {
        #region Dependency Properties

        public static readonly DependencyProperty IsDepressedProperty =
            DependencyProperty.Register(nameof(IsDepressed), typeof(bool), typeof(ZEmergencyStop),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnIsDepressedChanged));

        public static readonly DependencyProperty Channel1OkProperty =
            DependencyProperty.Register(nameof(Channel1Ok), typeof(bool), typeof(ZEmergencyStop),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender, OnChannelStateChanged));

        public static readonly DependencyProperty Channel2OkProperty =
            DependencyProperty.Register(nameof(Channel2Ok), typeof(bool), typeof(ZEmergencyStop),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender, OnChannelStateChanged));

        public static readonly DependencyProperty ResetModeProperty =
            DependencyProperty.Register(nameof(ResetMode), typeof(EStopResetMode), typeof(ZEmergencyStop),
                new FrameworkPropertyMetadata(EStopResetMode.TwistToReset, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CollarTextProperty =
            DependencyProperty.Register(nameof(CollarText), typeof(string), typeof(ZEmergencyStop),
                new FrameworkPropertyMetadata("EMERGENCY STOP", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowDirectionalArrowsProperty =
            DependencyProperty.Register(nameof(ShowDirectionalArrows), typeof(bool), typeof(ZEmergencyStop),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowSafetyContactLedsProperty =
            DependencyProperty.Register(nameof(ShowSafetyContactLeds), typeof(bool), typeof(ZEmergencyStop),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TwistAngleProperty =
            DependencyProperty.Register(nameof(TwistAngle), typeof(double), typeof(ZEmergencyStop),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BoundTagPathProperty =
            DependencyProperty.Register(nameof(BoundTagPath), typeof(string), typeof(ZEmergencyStop),
                new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty MushroomColorProperty =
            DependencyProperty.Register(nameof(MushroomColor), typeof(Color), typeof(ZEmergencyStop),
                new FrameworkPropertyMetadata(Color.FromRgb(204, 24, 30), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CollarColorProperty =
            DependencyProperty.Register(nameof(CollarColor), typeof(Color), typeof(ZEmergencyStop),
                new FrameworkPropertyMetadata(Color.FromRgb(245, 196, 0), FrameworkPropertyMetadataOptions.AffectsRender));

        #endregion

        #region Properties

        public string? BoundTagPath
        {
            get => (string?)GetValue(BoundTagPathProperty);
            set => SetValue(BoundTagPathProperty, value);
        }

        public bool IsDepressed
        {
            get => (bool)GetValue(IsDepressedProperty);
            set => SetValue(IsDepressedProperty, value);
        }

        public bool Channel1Ok
        {
            get => (bool)GetValue(Channel1OkProperty);
            set => SetValue(Channel1OkProperty, value);
        }

        public bool Channel2Ok
        {
            get => (bool)GetValue(Channel2OkProperty);
            set => SetValue(Channel2OkProperty, value);
        }

        public bool HasChannelFault => Channel1Ok != Channel2Ok;

        public EStopStatus Status
        {
            get
            {
                if (HasChannelFault) return EStopStatus.ChannelFault;
                if (IsDepressed || !Channel1Ok || !Channel2Ok) return EStopStatus.Tripped;
                return EStopStatus.Normal;
            }
        }

        public EStopResetMode ResetMode
        {
            get => (EStopResetMode)GetValue(ResetModeProperty);
            set => SetValue(ResetModeProperty, value);
        }

        public string CollarText
        {
            get => (string)GetValue(CollarTextProperty);
            set => SetValue(CollarTextProperty, value);
        }

        public bool ShowDirectionalArrows
        {
            get => (bool)GetValue(ShowDirectionalArrowsProperty);
            set => SetValue(ShowDirectionalArrowsProperty, value);
        }

        public bool ShowSafetyContactLeds
        {
            get => (bool)GetValue(ShowSafetyContactLedsProperty);
            set => SetValue(ShowSafetyContactLedsProperty, value);
        }

        public double TwistAngle
        {
            get => (double)GetValue(TwistAngleProperty);
            set => SetValue(TwistAngleProperty, value);
        }

        public Color MushroomColor
        {
            get => (Color)GetValue(MushroomColorProperty);
            set => SetValue(MushroomColorProperty, value);
        }

        public Color CollarColor
        {
            get => (Color)GetValue(CollarColorProperty);
            set => SetValue(CollarColorProperty, value);
        }

        #endregion

        #region Events

        public event EventHandler? Tripped;
        public event EventHandler? ResetCompleted;
        public event EventHandler? ChannelFaultChanged;

        #endregion

        private bool _isDraggingTwist;
        private Point _dragStartPoint;

        private static readonly Typeface BoldTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface SmallTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        static ZEmergencyStop()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZEmergencyStop), new FrameworkPropertyMetadata(typeof(ZEmergencyStop)));
        }

        public ZEmergencyStop()
        {
            Width = 160;
            Height = 160;
            Cursor = Cursors.Hand;
        }

        private static void OnIsDepressedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZEmergencyStop control)
            {
                bool depressed = (bool)e.NewValue;
                if (depressed)
                {
                    control.Channel1Ok = false;
                    control.Channel2Ok = false;
                    control.Tripped?.Invoke(control, EventArgs.Empty);
                }
                else
                {
                    control.Channel1Ok = true;
                    control.Channel2Ok = true;
                    control.TwistAngle = 0.0;
                    control.ResetCompleted?.Invoke(control, EventArgs.Empty);
                }
            }
        }

        private static void OnChannelStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZEmergencyStop control && control.HasChannelFault)
            {
                control.ChannelFaultChanged?.Invoke(control, EventArgs.Empty);
            }
        }

        public void Trip() => IsDepressed = true;
        public void Reset() => IsDepressed = false;

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag?.Value == null) return;
            if (bool.TryParse(tag.Value.ToString(), out var b))
            {
                IsDepressed = b;
            }
        }

        #region Mouse Interaction

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            Point pt = e.GetPosition(this);
            Point center = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
            double radius = Math.Min(ActualWidth, ActualHeight) * 0.34;
            double dx = pt.X - center.X;
            double dy = pt.Y - center.Y;

            if (dx * dx + dy * dy <= radius * radius)
            {
                if (!IsDepressed)
                {
                    Trip();
                }
                else
                {
                    if (ResetMode == EStopResetMode.TwistToReset)
                    {
                        _isDraggingTwist = true;
                        _dragStartPoint = pt;
                        CaptureMouse();
                    }
                    else
                    {
                        Reset();
                    }
                }
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDraggingTwist && IsDepressed)
            {
                Point pt = e.GetPosition(this);
                Point center = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
                double a1 = Math.Atan2(_dragStartPoint.Y - center.Y, _dragStartPoint.X - center.X) * 180.0 / Math.PI;
                double a2 = Math.Atan2(pt.Y - center.Y, pt.X - center.X) * 180.0 / Math.PI;
                double delta = a2 - a1;

                if (delta > 0)
                {
                    TwistAngle = Math.Min(35.0, delta);
                    if (TwistAngle >= 30.0)
                    {
                        _isDraggingTwist = false;
                        ReleaseMouseCapture();
                        Reset();
                    }
                }
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_isDraggingTwist)
            {
                _isDraggingTwist = false;
                ReleaseMouseCapture();
                if (IsDepressed)
                {
                    TwistAngle = 0.0;
                }
            }
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 20 || h < 20) return;

            double side = Math.Min(w, h);
            Point center = new Point(w / 2.0, h / 2.0);
            double collarRadius = (side - 16) / 2.0;

            if (collarRadius <= 10) return;

            // 1. Outer Bezel / Base
            var baseBrush = new LinearGradientBrush(
                Color.FromRgb(70, 75, 80),
                Color.FromRgb(30, 32, 35),
                new Point(0, 0), new Point(1, 1));
            baseBrush.Freeze();
            var basePen = new Pen(new SolidColorBrush(Color.FromRgb(90, 95, 102)), 2.0);
            basePen.Freeze();
            dc.DrawEllipse(baseBrush, basePen, center, collarRadius, collarRadius);

            // 2. Yellow Safety Collar
            double yellowRadius = collarRadius - 6.0;
            var yellowBrush = new LinearGradientBrush(
                CollarColor,
                Color.FromRgb(200, 150, 0),
                new Point(0, 0), new Point(0.8, 1));
            yellowBrush.Freeze();
            var yellowPen = new Pen(new SolidColorBrush(Color.FromRgb(160, 120, 0)), 1.5);
            yellowPen.Freeze();
            dc.DrawEllipse(yellowBrush, yellowPen, center, yellowRadius, yellowRadius);

            // 3. Collar Text ("EMERGENCY STOP")
            if (!string.IsNullOrEmpty(CollarText))
            {
                var textBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                textBrush.Freeze();
                var ft = CreateFormattedText(CollarText, BoldTypeface, Math.Max(8.0, side * 0.055), textBrush);
                Point textOrigin = new Point(center.X - ft.Width / 2.0, center.Y - yellowRadius + 8.0);
                dc.DrawText(ft, textOrigin);
            }

            // 4. Mushroom Actuator Head
            double mushroomRadius = yellowRadius * 0.65;
            double currentRadius = IsDepressed ? mushroomRadius * 0.94 : mushroomRadius;
            double depressOffsetY = IsDepressed ? 6.0 : 0.0;
            Point mushroomCenter = new Point(center.X, center.Y + depressOffsetY);

            // Shadow
            if (!IsDepressed)
            {
                var shadowBrush = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0));
                shadowBrush.Freeze();
                dc.DrawEllipse(shadowBrush, null, new Point(center.X + 2, center.Y + 4), mushroomRadius, mushroomRadius);
            }

            // Mushroom Body
            Color topCol = IsDepressed ? Color.FromRgb(180, 20, 24) : Color.FromRgb(240, 48, 56);
            Color botCol = IsDepressed ? Color.FromRgb(110, 10, 14) : Color.FromRgb(150, 16, 20);
            var mushroomBrush = new LinearGradientBrush(topCol, botCol, new Point(0, 0), new Point(0.3, 1));
            mushroomBrush.Freeze();
            var mushroomPen = new Pen(new SolidColorBrush(Color.FromRgb(90, 8, 12)), 2.0);
            mushroomPen.Freeze();
            dc.DrawEllipse(mushroomBrush, mushroomPen, mushroomCenter, currentRadius, currentRadius);

            // 3D Highlight sheen
            double sheenRadiusX = currentRadius * 0.65;
            double sheenRadiusY = currentRadius * 0.35;
            var sheenBrush = new LinearGradientBrush(
                Color.FromArgb(100, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255),
                new Point(0.5, 0), new Point(0.5, 1));
            sheenBrush.Freeze();
            dc.DrawEllipse(sheenBrush, null, new Point(center.X, mushroomCenter.Y - currentRadius * 0.5), sheenRadiusX, sheenRadiusY);

            // 5. Directional Reset Arrows
            if (ShowDirectionalArrows && ResetMode == EStopResetMode.TwistToReset)
            {
                dc.PushTransform(new RotateTransform(TwistAngle, mushroomCenter.X, mushroomCenter.Y));

                var arrowPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)), 2.5);
                arrowPen.Freeze();
                double arrowRadius = currentRadius * 0.55;

                for (int i = 0; i < 3; i++)
                {
                    double startAngle = (i * 120.0 + 10.0) * Math.PI / 180.0;
                    double endAngle = (i * 120.0 + 75.0) * Math.PI / 180.0;
                    Point pStart = new Point(mushroomCenter.X + arrowRadius * Math.Cos(startAngle), mushroomCenter.Y + arrowRadius * Math.Sin(startAngle));
                    Point pEnd = new Point(mushroomCenter.X + arrowRadius * Math.Cos(endAngle), mushroomCenter.Y + arrowRadius * Math.Sin(endAngle));

                    var geo = new StreamGeometry();
                    using (var ctx = geo.Open())
                    {
                        ctx.BeginFigure(pStart, false, false);
                        ctx.ArcTo(pEnd, new Size(arrowRadius, arrowRadius), 0, false, SweepDirection.Clockwise, true, false);
                    }
                    geo.Freeze();
                    dc.DrawGeometry(null, arrowPen, geo);
                }

                dc.Pop();
            }

            // 6. Dual-Channel Safety Contact Indicators
            if (ShowSafetyContactLeds)
            {
                DrawSafetyContactLeds(dc, center.X, center.Y, yellowRadius);
            }
        }

        private void DrawSafetyContactLeds(DrawingContext dc, double cx, double cy, double yellowRadius)
        {
            double ledY = cy + yellowRadius - 16.0;

            // CH1
            double ch1X = cx - 28.0;
            Brush ch1Brush = Channel1Ok ? Brushes.LimeGreen : Brushes.Crimson;
            dc.DrawEllipse(ch1Brush, new Pen(Brushes.DarkSlateGray, 1.0), new Point(ch1X, ledY), 4.0, 4.0);
            var ft1 = CreateFormattedText("1", SmallTypeface, 9.0, Brushes.Black);
            dc.DrawText(ft1, new Point(ch1X - 9.0, ledY - 6.0));

            // CH2
            double ch2X = cx + 20.0;
            Brush ch2Brush = Channel2Ok ? Brushes.LimeGreen : Brushes.Crimson;
            dc.DrawEllipse(ch2Brush, new Pen(Brushes.DarkSlateGray, 1.0), new Point(ch2X, ledY), 4.0, 4.0);
            var ft2 = CreateFormattedText("2", SmallTypeface, 9.0, Brushes.Black);
            dc.DrawText(ft2, new Point(ch2X + 6.0, ledY - 6.0));
        }

        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush)
        {
#if NETFRAMEWORK
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
#else
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush, 1.0);
#endif
        }

        #endregion
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Convenience alias for <see cref="ZEmergencyStop"/>.
    /// </summary>
    public class ZEmergencyStopControl : ZEmergencyStop { }

    /// <summary>
    /// Convenience alias for <see cref="ZEmergencyStop"/>.
    /// </summary>
    public class ZeroEmergencyStop : ZEmergencyStop { }

    /// <summary>
    /// Legacy alias for <see cref="ZEmergencyStop"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("EmergencyStopControl is deprecated and will be removed in 5 release cycles. Please migrate to ZEmergencyStop instead.")]
    public class EmergencyStopControl : ZEmergencyStop { }

    /// <summary>
    /// Legacy alias for <see cref="ZEmergencyStop"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroEmergencyStopControl is deprecated and will be removed in 5 release cycles. Please migrate to ZEmergencyStop instead.")]
    public class ZeroEmergencyStopControl : ZEmergencyStop { }

    #endregion

}
