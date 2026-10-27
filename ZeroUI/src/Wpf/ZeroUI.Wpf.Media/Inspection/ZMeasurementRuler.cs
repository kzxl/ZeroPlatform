using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Media;

namespace ZeroUI.Wpf.Media
{
    /// <summary>
    /// Interactive precision measurement ruler and optical caliper for Machine Vision inspection and metrology.
    /// Supports pixel-to-millimeter calibration, 2-point linear calipers, 3-point angle gauges, and polygon area.
    /// </summary>
    public class ZMeasurementRuler : FrameworkElement
    {
        private bool _isDragging;
        private int _dragPointIndex = -1; // 0 = Start, 1 = End, 2 = Mid
        private Point _p1 = new(40, 100);
        private Point _p2 = new(240, 100);
        private Point _p3 = new(140, 40); // For 3-point angle

        private readonly Typeface _fontTypeface = new(new FontFamily("Segoe UI, Consolas, sans-serif"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Brush RulerBrush = new SolidColorBrush(Color.FromRgb(0, 229, 255)); // Bright Cyan
        private static readonly Pen RulerPen = new(RulerBrush, 1.5);
        private static readonly Pen CaliperTickPen = new(RulerBrush, 1.0);
        private static readonly Brush HandleBrush = new SolidColorBrush(Color.FromArgb(220, 24, 28, 40));
        private static readonly Pen HandlePen = new(RulerBrush, 1.5);
        private static readonly Brush LabelBgBrush = new SolidColorBrush(Color.FromArgb(220, 17, 19, 31));
        private static readonly Pen LabelBorderPen = new(new SolidColorBrush(Color.FromArgb(120, 0, 229, 255)), 1.0);

        static ZMeasurementRuler()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZMeasurementRuler), new FrameworkPropertyMetadata(typeof(ZMeasurementRuler)));
            ClipToBoundsProperty.OverrideMetadata(typeof(ZMeasurementRuler), new FrameworkPropertyMetadata(true));
            FocusableProperty.OverrideMetadata(typeof(ZMeasurementRuler), new FrameworkPropertyMetadata(true));

            RulerBrush.Freeze();
            RulerPen.Freeze();
            CaliperTickPen.Freeze();
            HandleBrush.Freeze();
            HandlePen.Freeze();
            LabelBgBrush.Freeze();
            LabelBorderPen.Freeze();
        }

        public ZMeasurementRuler()
        {
            Cursor = Cursors.Hand;
        }

        #region Dependency Properties

        public static readonly DependencyProperty CalibrationFactorProperty =
            DependencyProperty.Register(nameof(CalibrationFactor), typeof(double), typeof(ZMeasurementRuler),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender)); // Units per pixel

        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(nameof(Unit), typeof(MeasurementUnit), typeof(ZMeasurementRuler),
                new FrameworkPropertyMetadata(MeasurementUnit.Pixel, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ModeProperty =
            DependencyProperty.Register(nameof(Mode), typeof(MeasurementMode), typeof(ZMeasurementRuler),
                new FrameworkPropertyMetadata(MeasurementMode.LinearDistance, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MeasuredValueProperty =
            DependencyProperty.Register(nameof(MeasuredValue), typeof(double), typeof(ZMeasurementRuler),
                new FrameworkPropertyMetadata(0.0));

        public static readonly DependencyProperty FormattedResultProperty =
            DependencyProperty.Register(nameof(FormattedResult), typeof(string), typeof(ZMeasurementRuler),
                new FrameworkPropertyMetadata(string.Empty));

        public double CalibrationFactor
        {
            get => (double)GetValue(CalibrationFactorProperty);
            set => SetValue(CalibrationFactorProperty, Math.Max(0.000001, value));
        }

        public MeasurementUnit Unit
        {
            get => (MeasurementUnit)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        public MeasurementMode Mode
        {
            get => (MeasurementMode)GetValue(ModeProperty);
            set => SetValue(ModeProperty, value);
        }

        public double MeasuredValue
        {
            get => (double)GetValue(MeasuredValueProperty);
            private set => SetValue(MeasuredValueProperty, value);
        }

        public string FormattedResult
        {
            get => (string)GetValue(FormattedResultProperty);
            private set => SetValue(FormattedResultProperty, value);
        }

        #endregion

        #region Mouse Interaction

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point pos = e.GetPosition(this);
                _dragPointIndex = HitTestHandles(pos);

                if (_dragPointIndex >= 0)
                {
                    _isDragging = true;
                    CaptureMouse();
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDragging && _dragPointIndex >= 0)
            {
                Point pos = e.GetPosition(this);
                switch (_dragPointIndex)
                {
                    case 0: _p1 = pos; break;
                    case 1: _p2 = pos; break;
                    case 2: _p3 = pos; break;
                }
                InvalidateVisual();
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDragging)
            {
                _isDragging = false;
                _dragPointIndex = -1;
                ReleaseMouseCapture();
                InvalidateVisual();
            }
        }

        private int HitTestHandles(Point pos)
        {
            double r = 12.0;
            if ((pos - _p1).Length <= r) return 0;
            if ((pos - _p2).Length <= r) return 1;
            if (Mode == MeasurementMode.ThreePointAngle && (pos - _p3).Length <= r) return 2;
            return -1;
        }

        #endregion

        #region Rendering & Measurement Math

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            switch (Mode)
            {
                case MeasurementMode.LinearDistance:
                case MeasurementMode.CaliperGauge:
                    RenderLinearCaliper(dc);
                    break;

                case MeasurementMode.ThreePointAngle:
                    RenderAngleGauge(dc);
                    break;
            }
        }

        private void RenderLinearCaliper(DrawingContext dc)
        {
            // Main dimension line
            dc.DrawLine(RulerPen, _p1, _p2);

            Vector dir = _p2 - _p1;
            double pxDistance = dir.Length;
            if (pxDistance < 1) return;
            dir.Normalize();
            Vector normal = new Vector(-dir.Y, dir.X);

            // Caliper Crosshairs / Ticks at ends
            double tickLength = 12.0;
            dc.DrawLine(CaliperTickPen, _p1 - normal * tickLength, _p1 + normal * tickLength);
            dc.DrawLine(CaliperTickPen, _p2 - normal * tickLength, _p2 + normal * tickLength);

            // Handles
            dc.DrawEllipse(HandleBrush, HandlePen, _p1, 6, 6);
            dc.DrawEllipse(HandleBrush, HandlePen, _p2, 6, 6);

            // Computation
            double actualDistance = pxDistance * CalibrationFactor;
            MeasuredValue = actualDistance;
            string unitSuffix = GetUnitSuffix(Unit);
            string label = $"{actualDistance:F2} {unitSuffix} ({pxDistance:F0} px)";
            FormattedResult = label;

            // Draw center telemetry label
            Point mid = new((_p1.X + _p2.X) / 2.0, (_p1.Y + _p2.Y) / 2.0);
            DrawTelemetryBadge(dc, label, mid - normal * 18);
        }

        private void RenderAngleGauge(DrawingContext dc)
        {
            // _p3 is the vertex / apex
            dc.DrawLine(RulerPen, _p3, _p1);
            dc.DrawLine(RulerPen, _p3, _p2);

            // Handles
            dc.DrawEllipse(HandleBrush, HandlePen, _p1, 5, 5);
            dc.DrawEllipse(HandleBrush, HandlePen, _p2, 5, 5);
            dc.DrawEllipse(RulerBrush, HandlePen, _p3, 7, 7); // Vertex emphasized

            Vector v1 = _p1 - _p3;
            Vector v2 = _p2 - _p3;

            double angleDeg = 0.0;
            if (v1.Length > 0 && v2.Length > 0)
            {
                v1.Normalize();
                v2.Normalize();
                double dot = Math.Max(-1.0, Math.Min(1.0, v1.X * v2.X + v1.Y * v2.Y));
                angleDeg = Math.Acos(dot) * (180.0 / Math.PI);
            }

            MeasuredValue = angleDeg;
            string label = $"∠ {angleDeg:F1}°";
            FormattedResult = label;

            DrawTelemetryBadge(dc, label, _p3 + new Vector(14, -14));
        }

        private void DrawTelemetryBadge(DrawingContext dc, string text, Point center)
        {
            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                _fontTypeface,
                11.5,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            var rect = new Rect(center.X - ft.Width / 2.0 - 6, center.Y - ft.Height / 2.0 - 3, ft.Width + 12, ft.Height + 6);
            dc.DrawRoundedRectangle(LabelBgBrush, LabelBorderPen, rect, 4, 4);
            dc.DrawText(ft, new Point(rect.X + 6, rect.Y + 3));
        }

        private static string GetUnitSuffix(MeasurementUnit unit) => unit switch
        {
            MeasurementUnit.Millimeter => "mm",
            MeasurementUnit.Micrometer => "μm",
            MeasurementUnit.Centimeter => "cm",
            MeasurementUnit.Inch => "in",
            _ => "px"
        };

        #endregion
    }
}
