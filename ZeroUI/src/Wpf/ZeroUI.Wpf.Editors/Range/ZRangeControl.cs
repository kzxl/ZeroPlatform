using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Range;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Range
{
    /// <summary>
    /// High-performance visual timeline and numeric range selection control for WPF.
    /// Employs hardware-accelerated direct vector DrawingContext rendering for 60 FPS fluidity,
    /// interactive thumb dragging, span panning, mouse wheel zooming, and distribution visualization.
    /// </summary>
    public class ZRangeControl : FrameworkElement
    {
        private readonly RangeControlModel _model = new RangeControlModel();
        private IRangeControlClient? _client;

        private RangeHitTestResult _dragTarget = RangeHitTestResult.None;
        private Point _dragStartPoint;
        private double _dragStartValue;
        private double _dragStartSelectedStart;
        private double _dragStartSelectedEnd;

        private const double ThumbWidth = 10.0;
        private const double RulerHeight = 22.0;

        #region Dependency Properties

        public static readonly DependencyProperty ShowRangeThumbsProperty =
            DependencyProperty.Register(nameof(ShowRangeThumbs), typeof(bool), typeof(ZRangeControl),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowBackgroundGraphProperty =
            DependencyProperty.Register(nameof(ShowBackgroundGraph), typeof(bool), typeof(ZRangeControl),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BackgroundGraphTypeProperty =
            DependencyProperty.Register(nameof(BackgroundGraphType), typeof(RangeGraphType), typeof(ZRangeControl),
                new FrameworkPropertyMetadata(RangeGraphType.Area, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowRulerProperty =
            DependencyProperty.Register(nameof(ShowRuler), typeof(bool), typeof(ZRangeControl),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowSelectionProperty =
            DependencyProperty.Register(nameof(ShowSelection), typeof(bool), typeof(ZRangeControl),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowRangeLabelsProperty =
            DependencyProperty.Register(nameof(ShowRangeLabels), typeof(bool), typeof(ZRangeControl),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty EnableZoomProperty =
            DependencyProperty.Register(nameof(EnableZoom), typeof(bool), typeof(ZRangeControl),
                new FrameworkPropertyMetadata(true));

        public static readonly DependencyProperty EnablePanProperty =
            DependencyProperty.Register(nameof(EnablePan), typeof(bool), typeof(ZRangeControl),
                new FrameworkPropertyMetadata(true));

        public static readonly DependencyProperty SnapToIntervalProperty =
            DependencyProperty.Register(nameof(SnapToInterval), typeof(bool), typeof(ZRangeControl),
                new FrameworkPropertyMetadata(false, (d, e) => ((RangeControl)d)._model.SnapToInterval = (bool)e.NewValue));

        public static readonly DependencyProperty TotalRangeStartProperty =
            DependencyProperty.Register(nameof(TotalRangeStart), typeof(double), typeof(ZRangeControl),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, (d, e) => ((RangeControl)d)._model.TotalRangeStart = (double)e.NewValue));

        public static readonly DependencyProperty TotalRangeEndProperty =
            DependencyProperty.Register(nameof(TotalRangeEnd), typeof(double), typeof(ZRangeControl),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender, (d, e) => ((RangeControl)d)._model.TotalRangeEnd = (double)e.NewValue));

        public static readonly DependencyProperty SelectedRangeStartProperty =
            DependencyProperty.Register(nameof(SelectedRangeStart), typeof(double), typeof(ZRangeControl),
                new FrameworkPropertyMetadata(20.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
                    (d, e) => ((RangeControl)d)._model.SelectedRangeStart = (double)e.NewValue));

        public static readonly DependencyProperty SelectedRangeEndProperty =
            DependencyProperty.Register(nameof(SelectedRangeEnd), typeof(double), typeof(ZRangeControl),
                new FrameworkPropertyMetadata(80.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
                    (d, e) => ((RangeControl)d)._model.SelectedRangeEnd = (double)e.NewValue));

        #endregion

        #region Properties

        public bool ShowRangeThumbs
        {
            get => (bool)GetValue(ShowRangeThumbsProperty);
            set => SetValue(ShowRangeThumbsProperty, value);
        }

        public bool ShowBackgroundGraph
        {
            get => (bool)GetValue(ShowBackgroundGraphProperty);
            set => SetValue(ShowBackgroundGraphProperty, value);
        }

        public RangeGraphType BackgroundGraphType
        {
            get => (RangeGraphType)GetValue(BackgroundGraphTypeProperty);
            set => SetValue(BackgroundGraphTypeProperty, value);
        }

        public bool ShowRuler
        {
            get => (bool)GetValue(ShowRulerProperty);
            set => SetValue(ShowRulerProperty, value);
        }

        public bool ShowSelection
        {
            get => (bool)GetValue(ShowSelectionProperty);
            set => SetValue(ShowSelectionProperty, value);
        }

        public bool ShowRangeLabels
        {
            get => (bool)GetValue(ShowRangeLabelsProperty);
            set => SetValue(ShowRangeLabelsProperty, value);
        }

        public bool EnableZoom
        {
            get => (bool)GetValue(EnableZoomProperty);
            set => SetValue(EnableZoomProperty, value);
        }

        public bool EnablePan
        {
            get => (bool)GetValue(EnablePanProperty);
            set => SetValue(EnablePanProperty, value);
        }

        public bool SnapToInterval
        {
            get => (bool)GetValue(SnapToIntervalProperty);
            set => SetValue(SnapToIntervalProperty, value);
        }

        public double TotalRangeStart
        {
            get => (double)GetValue(TotalRangeStartProperty);
            set => SetValue(TotalRangeStartProperty, value);
        }

        public double TotalRangeEnd
        {
            get => (double)GetValue(TotalRangeEndProperty);
            set => SetValue(TotalRangeEndProperty, value);
        }

        public double SelectedRangeStart
        {
            get => (double)GetValue(SelectedRangeStartProperty);
            set => SetValue(SelectedRangeStartProperty, value);
        }

        public double SelectedRangeEnd
        {
            get => (double)GetValue(SelectedRangeEndProperty);
            set => SetValue(SelectedRangeEndProperty, value);
        }

        public RangeDataType DataType
        {
            get => _model.DataType;
            set { _model.DataType = value; InvalidateVisual(); }
        }

        public RangeInterval Interval
        {
            get => _model.Interval;
            set { _model.Interval = value; InvalidateVisual(); }
        }

        public double NumericStep
        {
            get => _model.NumericStep;
            set => _model.NumericStep = value;
        }

        public DateTime SelectedStartDate
        {
            get => _model.SelectedStartDate;
            set { _model.SelectedStartDate = value; SyncModelToDependencyProperties(); InvalidateVisual(); }
        }

        public DateTime SelectedEndDate
        {
            get => _model.SelectedEndDate;
            set { _model.SelectedEndDate = value; SyncModelToDependencyProperties(); InvalidateVisual(); }
        }

        public DateTime TotalStartDate
        {
            get => _model.TotalStartDate;
            set { _model.TotalStartDate = value; SyncModelToDependencyProperties(); InvalidateVisual(); }
        }

        public DateTime TotalEndDate
        {
            get => _model.TotalEndDate;
            set { _model.TotalEndDate = value; SyncModelToDependencyProperties(); InvalidateVisual(); }
        }

        public List<RangeDataPoint> DataPoints => _model.DataPoints;

        #endregion

        public event EventHandler<RangeChangedEventArgs>? RangeSelectionChanged;
        public event EventHandler<RangeChangedEventArgs>? VisibleRangeChanged;

        static ZRangeControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZRangeControl), new FrameworkPropertyMetadata(typeof(ZRangeControl)));
        }

        public ZRangeControl()
        {
            Height = 72;
            MinHeight = 40;
            MinWidth = 120;
            Focusable = true;

            _model.RangeSelectionChanged += (s, e) =>
            {
                SyncModelToDependencyProperties();
                _client?.OnRangeSelectionChanged(e.Start, e.End);
                RangeSelectionChanged?.Invoke(this, e);
                InvalidateVisual();
            };

            _model.VisibleRangeChanged += (s, e) =>
            {
                VisibleRangeChanged?.Invoke(this, e);
                InvalidateVisual();
            };

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void SyncModelToDependencyProperties()
        {
            SetCurrentValue(SelectedRangeStartProperty, _model.SelectedRangeStart);
            SetCurrentValue(SelectedRangeEndProperty, _model.SelectedRangeEnd);
            SetCurrentValue(TotalRangeStartProperty, _model.TotalRangeStart);
            SetCurrentValue(TotalRangeEndProperty, _model.TotalRangeEnd);
        }

        #region Data Points

        public void SetDataPoints(IEnumerable<RangeDataPoint> points)
        {
            _model.DataPoints.Clear();
            if (points != null)
            {
                _model.DataPoints.AddRange(points);
            }
            InvalidateVisual();
        }

        public void AttachClient(IRangeControlClient client)
        {
            _client = client;
            if (client == null) return;

            DataType = client.DataType;
            var bounds = client.GetTotalRangeBounds();
            _model.SetTotalRange(bounds.Start, bounds.End);
            _model.SetVisibleRange(bounds.Start, bounds.End);
            _model.SelectAll();

            var points = client.GetDataPoints();
            if (points != null)
            {
                SetDataPoints(points);
            }
            SyncModelToDependencyProperties();
            InvalidateVisual();
        }

        #endregion

        #region Vector Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            bool isDark = ZeroWpfTheme.IsDark;

            Color bgColor = ZeroWpfTheme.BgCard.Color;
            Color borderColor = ZeroWpfTheme.BorderDefault.Color;
            Color accent = ZeroWpfTheme.PrimaryAccent.Color;
            Color textColor = ZeroWpfTheme.TextPrimary.Color;
            Color mutedColor = ZeroWpfTheme.TextMuted.Color;

            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            // 1. Draw Background Box
            dc.DrawRoundedRectangle(new SolidColorBrush(bgColor), new Pen(new SolidColorBrush(borderColor), 1), bounds, 4, 4);

            Rect trackRect = GetTrackRectangle();
            if (trackRect.Width <= 0 || trackRect.Height <= 0) return;

            // 2. Draw Background Graph
            if (ShowBackgroundGraph && _model.DataPoints.Count > 0)
            {
                DrawBackgroundGraph(dc, trackRect, accent, isDark);
            }

            // 3. Draw Selection & Dimmed Areas
            double selStartX = _model.ValueToPixel(_model.SelectedRangeStart, trackRect.Width) + trackRect.X;
            double selEndX = _model.ValueToPixel(_model.SelectedRangeEnd, trackRect.Width) + trackRect.X;

            if (ShowSelection)
            {
                // Left unselected dim
                if (selStartX > trackRect.Left)
                {
                    var leftDim = new Rect(trackRect.Left, trackRect.Top, selStartX - trackRect.Left, trackRect.Height);
                    dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(isDark ? (byte)100 : (byte)70, isDark ? (byte)10 : (byte)220, isDark ? (byte)12 : (byte)225, isDark ? (byte)16 : (byte)230)), null, leftDim);
                }

                // Right unselected dim
                if (selEndX < trackRect.Right)
                {
                    var rightDim = new Rect(selEndX, trackRect.Top, trackRect.Right - selEndX, trackRect.Height);
                    dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(isDark ? (byte)100 : (byte)70, isDark ? (byte)10 : (byte)220, isDark ? (byte)12 : (byte)225, isDark ? (byte)16 : (byte)230)), null, rightDim);
                }

                // Highlight selected range
                var selRect = new Rect(selStartX, trackRect.Top, Math.Max(1, selEndX - selStartX), trackRect.Height);
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(isDark ? (byte)45 : (byte)35, accent.R, accent.G, accent.B)), null, selRect);

                // Top & bottom accent border lines
                var selBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(160, accent.R, accent.G, accent.B)), 1.5);
                dc.DrawLine(selBorderPen, new Point(selStartX, trackRect.Top), new Point(selEndX, trackRect.Top));
                dc.DrawLine(selBorderPen, new Point(selStartX, trackRect.Bottom), new Point(selEndX, trackRect.Bottom));
            }

            // 4. Draw Ruler Scale
            if (ShowRuler)
            {
                DrawRuler(dc, trackRect, mutedColor, borderColor);
            }

            // 5. Draw Sliding Thumbs
            if (ShowRangeThumbs)
            {
                DrawThumb(dc, selStartX, trackRect, accent, isDark);
                DrawThumb(dc, selEndX, trackRect, accent, isDark);
            }

            // 6. Draw Range Labels
            if (ShowRangeLabels)
            {
                DrawLabels(dc, selStartX, selEndX, trackRect, textColor);
            }
        }

        private void DrawBackgroundGraph(DrawingContext dc, Rect trackRect, Color accent, bool isDark)
        {
            var points = _model.DataPoints;
            if (points.Count == 0) return;

            double maxVal = 1e-6;
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i].Value > maxVal) maxVal = points[i].Value;
            }

            if (BackgroundGraphType == RangeGraphType.Histogram)
            {
                double barWidth = Math.Max(2.0, (trackRect.Width / Math.Max(1, points.Count)) - 1);
                var barBrush = new SolidColorBrush(Color.FromArgb(isDark ? (byte)120 : (byte)160, accent.R, accent.G, accent.B));

                for (int i = 0; i < points.Count; i++)
                {
                    double x = _model.ValueToPixel(points[i].Argument, trackRect.Width) + trackRect.X;
                    if (x < trackRect.Left - barWidth || x > trackRect.Right + barWidth) continue;

                    double barH = trackRect.Height * (points[i].Value / maxVal);
                    double y = trackRect.Bottom - barH;
                    dc.DrawRectangle(barBrush, null, new Rect(x - barWidth / 2.0, y, barWidth, barH));
                }
            }
            else
            {
                // Area or Line
                var streamGeom = new StreamGeometry();
                using (var ctx = streamGeom.Open())
                {
                    ctx.BeginFigure(new Point(trackRect.Left, trackRect.Bottom), isFilled: true, isClosed: true);

                    for (int i = 0; i < points.Count; i++)
                    {
                        double x = _model.ValueToPixel(points[i].Argument, trackRect.Width) + trackRect.X;
                        double y = trackRect.Bottom - (trackRect.Height * (points[i].Value / maxVal));
                        ctx.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: true);
                    }

                    ctx.LineTo(new Point(trackRect.Right, trackRect.Bottom), isStroked: true, isSmoothJoin: false);
                }

                if (BackgroundGraphType == RangeGraphType.Area)
                {
                    var gradient = new LinearGradientBrush(
                        Color.FromArgb(isDark ? (byte)140 : (byte)100, accent.R, accent.G, accent.B),
                        Color.FromArgb(isDark ? (byte)20 : (byte)10, accent.R, accent.G, accent.B),
                        90);
                    dc.DrawGeometry(gradient, null, streamGeom);
                }

                // Line stroke
                var lineGeom = new StreamGeometry();
                using (var ctx = lineGeom.Open())
                {
                    for (int i = 0; i < points.Count; i++)
                    {
                        double x = _model.ValueToPixel(points[i].Argument, trackRect.Width) + trackRect.X;
                        double y = trackRect.Bottom - (trackRect.Height * (points[i].Value / maxVal));
                        if (i == 0) ctx.BeginFigure(new Point(x, y), isFilled: false, isClosed: false);
                        else ctx.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: true);
                    }
                }
                dc.DrawGeometry(null, new Pen(new SolidColorBrush(accent), 1.5), lineGeom);
            }
        }

        private void DrawRuler(DrawingContext dc, Rect trackRect, Color mutedColor, Color borderColor)
        {
            double rulerTop = trackRect.Bottom + 1;
            var linePen = new Pen(new SolidColorBrush(borderColor), 1);
            dc.DrawLine(linePen, new Point(trackRect.Left, rulerTop), new Point(trackRect.Right, rulerTop));

            int numTicks = Math.Max(2, (int)(trackRect.Width / 80));
            double stepValue = _model.VisibleRangeSpan / numTicks;

            var tickPen = new Pen(new SolidColorBrush(borderColor), 1);
            var textBrush = new SolidColorBrush(mutedColor);
            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

            for (int i = 0; i <= numTicks; i++)
            {
                double tickVal = _model.VisibleRangeStart + i * stepValue;
                double x = _model.ValueToPixel(tickVal, trackRect.Width) + trackRect.X;
                if (x < trackRect.Left || x > trackRect.Right) continue;

                dc.DrawLine(tickPen, new Point(x, rulerTop), new Point(x, rulerTop + 4));

                string text = FormatValue(tickVal, isShort: true);
                var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 10, textBrush, 1.0);

                double textX = x - formatted.Width / 2.0;
                if (textX < 2) textX = 2;
                if (textX + formatted.Width > ActualWidth - 2) textX = ActualWidth - 2 - formatted.Width;

                dc.DrawText(formatted, new Point(textX, rulerTop + 5));
            }
        }

        private void DrawThumb(DrawingContext dc, double centerX, Rect trackRect, Color accent, bool isDark)
        {
            double w = ThumbWidth;
            double h = trackRect.Height + 8;
            double x = centerX - w / 2.0;
            double y = trackRect.Top - 4;

            var thumbRect = new Rect(x, y, w, h);
            var fillBrush = new SolidColorBrush(isDark ? Color.FromRgb(33, 38, 45) : Colors.White);
            var strokePen = new Pen(new SolidColorBrush(accent), 1.8);

            dc.DrawRoundedRectangle(fillBrush, strokePen, thumbRect, 3, 3);

            // Grip notches
            double pipX = centerX;
            double pipY = y + h / 2.0;
            var gripPen = new Pen(new SolidColorBrush(accent), 1.2);
            dc.DrawLine(gripPen, new Point(pipX - 1.5, pipY - 4), new Point(pipX - 1.5, pipY + 4));
            dc.DrawLine(gripPen, new Point(pipX + 1.5, pipY - 4), new Point(pipX + 1.5, pipY + 4));
        }

        private void DrawLabels(DrawingContext dc, double startX, double endX, Rect trackRect, Color textColor)
        {
            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var textBrush = new SolidColorBrush(textColor);

            string startStr = FormatValue(_model.SelectedRangeStart, isShort: false);
            string endStr = FormatValue(_model.SelectedRangeEnd, isShort: false);

            var startFormatted = new FormattedText(startStr, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, textBrush, 1.0);
            var endFormatted = new FormattedText(endStr, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, textBrush, 1.0);

            double y = trackRect.Top + 2;
            double labelStartX = Math.Max(trackRect.Left + 4, startX + 6);
            double labelEndX = Math.Min(trackRect.Right - endFormatted.Width - 4, endX - endFormatted.Width - 6);

            if (labelStartX + startFormatted.Width + 8 < labelEndX)
            {
                dc.DrawText(startFormatted, new Point(labelStartX, y));
                dc.DrawText(endFormatted, new Point(labelEndX, y));
            }
            else
            {
                string spanStr = $"{startStr} - {endStr}";
                var spanFormatted = new FormattedText(spanStr, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, textBrush, 1.0);
                double midX = (startX + endX - spanFormatted.Width) / 2.0;
                midX = Math.Max(trackRect.Left + 4, Math.Min(trackRect.Right - spanFormatted.Width - 4, midX));
                dc.DrawText(spanFormatted, new Point(midX, y));
            }
        }

        #endregion

        #region Mouse Interaction

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();

            Point pt = e.GetPosition(this);
            var hit = HitTest(pt);
            _dragTarget = hit;
            _dragStartPoint = pt;

            var trackRect = GetTrackRectangle();
            _dragStartValue = _model.PixelToValue(pt.X - trackRect.X, trackRect.Width);
            _dragStartSelectedStart = _model.SelectedRangeStart;
            _dragStartSelectedEnd = _model.SelectedRangeEnd;

            CaptureMouse();
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point pt = e.GetPosition(this);
            var trackRect = GetTrackRectangle();

            if (_dragTarget == RangeHitTestResult.None)
            {
                var hit = HitTest(pt);
                switch (hit)
                {
                    case RangeHitTestResult.LeftThumb:
                    case RangeHitTestResult.RightThumb:
                        Cursor = Cursors.SizeWE;
                        break;
                    case RangeHitTestResult.SelectionRange:
                        Cursor = EnablePan ? Cursors.SizeAll : Cursors.Arrow;
                        break;
                    default:
                        Cursor = Cursors.Arrow;
                        break;
                }
                return;
            }

            double curVal = _model.PixelToValue(pt.X - trackRect.X, trackRect.Width);

            if (_dragTarget == RangeHitTestResult.LeftThumb && ShowRangeThumbs)
            {
                _model.SelectedRangeStart = Math.Min(curVal, _model.SelectedRangeEnd);
            }
            else if (_dragTarget == RangeHitTestResult.RightThumb && ShowRangeThumbs)
            {
                _model.SelectedRangeEnd = Math.Max(curVal, _model.SelectedRangeStart);
            }
            else if (_dragTarget == RangeHitTestResult.SelectionRange && EnablePan)
            {
                double delta = curVal - _dragStartValue;
                double span = _dragStartSelectedEnd - _dragStartSelectedStart;
                double newStart = _dragStartSelectedStart + delta;
                double newEnd = _dragStartSelectedEnd + delta;

                if (newStart < _model.TotalRangeStart)
                {
                    newStart = _model.TotalRangeStart;
                    newEnd = newStart + span;
                }
                else if (newEnd > _model.TotalRangeEnd)
                {
                    newEnd = _model.TotalRangeEnd;
                    newStart = newEnd - span;
                }

                _model.SetSelectedRange(newStart, newEnd);
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            _dragTarget = RangeHitTestResult.None;
            ReleaseMouseCapture();
            InvalidateVisual();
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!EnableZoom) return;

            var trackRect = GetTrackRectangle();
            Point pt = e.GetPosition(this);
            double ratio = (pt.X - trackRect.X) / Math.Max(1.0, trackRect.Width);
            double factor = e.Delta > 0 ? 1.25 : 0.8;
            _model.Zoom(factor, ratio);
        }

        #endregion

        #region Helpers

        private Rect GetTrackRectangle()
        {
            double top = 4;
            double bottom = ActualHeight - (ShowRuler ? RulerHeight : 4);
            double left = 8;
            double right = ActualWidth - 8;

            return new Rect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        }

        private RangeHitTestResult HitTest(Point pt)
        {
            var trackRect = GetTrackRectangle();
            double selStartX = _model.ValueToPixel(_model.SelectedRangeStart, trackRect.Width) + trackRect.X;
            double selEndX = _model.ValueToPixel(_model.SelectedRangeEnd, trackRect.Width) + trackRect.X;

            double thumbHitPadding = 7.0;

            if (ShowRangeThumbs)
            {
                if (Math.Abs(pt.X - selStartX) <= thumbHitPadding && pt.Y >= trackRect.Top - 6 && pt.Y <= trackRect.Bottom + 6)
                {
                    return RangeHitTestResult.LeftThumb;
                }
                if (Math.Abs(pt.X - selEndX) <= thumbHitPadding && pt.Y >= trackRect.Top - 6 && pt.Y <= trackRect.Bottom + 6)
                {
                    return RangeHitTestResult.RightThumb;
                }
            }

            if (ShowSelection && pt.X >= selStartX && pt.X <= selEndX && pt.Y >= trackRect.Top && pt.Y <= trackRect.Bottom)
            {
                return RangeHitTestResult.SelectionRange;
            }

            return RangeHitTestResult.Background;
        }

        private string FormatValue(double val, bool isShort)
        {
            if (DataType == RangeDataType.DateTime)
            {
                try
                {
                    DateTime dt = DateTime.FromOADate(val);
                    return isShort ? dt.ToString("MM/dd") : dt.ToString("yyyy-MM-dd");
                }
                catch
                {
                    return val.ToString("F1");
                }
            }
            return val.ToString("F1");
        }

        #endregion
    
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

    /// <summary>
    /// Semantic alias specialized for temporal date and time interval sliding.
    /// </summary>
    public class DateTimeRangeSlider : RangeControl
    {
        public DateTimeRangeSlider()
        {
            DataType = RangeDataType.DateTime;
            Interval = RangeInterval.Day;
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZRangeControl"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("RangeControl is deprecated and will be removed in 5 release cycles. Please migrate to ZRangeControl instead.")]
    public class RangeControl : ZRangeControl { }

    /// <summary>
    /// Legacy alias for <see cref="ZRangeControl"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroRangeControl is deprecated and will be removed in 5 release cycles. Please migrate to ZRangeControl instead.")]
    public class ZeroRangeControl : ZRangeControl { }

    #endregion

}
