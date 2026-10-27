using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZeroUI.Core.Media;

namespace ZeroUI.Wpf.Media
{
    /// <summary>
    /// Annotation model representing a geometric markup or defect region.
    /// </summary>
    public class VisualAnnotationItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public AnnotationShapeType ShapeType { get; set; } = AnnotationShapeType.BoundingBox;
        public AnnotationSeverity Severity { get; set; } = AnnotationSeverity.Defect;
        public string Label { get; set; } = "Defect";
        public Point StartPoint { get; set; }
        public Point EndPoint { get; set; }
        public List<Point> Points { get; set; } = new();
        public Color StrokeColor { get; set; } = Color.FromRgb(243, 139, 168); // Soft Red
        public Color FillColor { get; set; } = Color.FromArgb(40, 243, 139, 168);
        public double StrokeThickness { get; set; } = 2.0;
        public bool IsSelected { get; set; }
    }

    /// <summary>
    /// Enterprise interactive canvas for image annotation, quality defect tagging, and forensic callouts.
    /// Supports direct vector drawing, gizmo selection, and burning overlay into output bitmaps.
    /// </summary>
    public class ZAnnotationCanvas : FrameworkElement
    {
        private bool _isDrawing;
        private Point _drawStart;
        private Point _drawCurrent;
        private VisualAnnotationItem? _selectedItem;

        private readonly Typeface _labelTypeface = new(new FontFamily("Segoe UI, Arial, sans-serif"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Brush SelectedHandleBrush = new SolidColorBrush(Color.FromRgb(0, 229, 255));
        private static readonly Pen SelectedHandlePen = new Pen(new SolidColorBrush(Colors.White), 1.0);

        static ZAnnotationCanvas()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZAnnotationCanvas), new FrameworkPropertyMetadata(typeof(ZAnnotationCanvas)));
            ClipToBoundsProperty.OverrideMetadata(typeof(ZAnnotationCanvas), new FrameworkPropertyMetadata(true));
            FocusableProperty.OverrideMetadata(typeof(ZAnnotationCanvas), new FrameworkPropertyMetadata(true));

            SelectedHandleBrush.Freeze();
            SelectedHandlePen.Freeze();
        }

        public ZAnnotationCanvas()
        {
            Annotations = new ObservableCollection<VisualAnnotationItem>();
            Annotations.CollectionChanged += (s, e) => InvalidateVisual();
            Cursor = Cursors.Cross;
        }

        #region Dependency Properties

        public static readonly DependencyProperty AnnotationsProperty =
            DependencyProperty.Register(nameof(Annotations), typeof(ObservableCollection<VisualAnnotationItem>), typeof(ZAnnotationCanvas),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnAnnotationsChanged));

        public static readonly DependencyProperty CurrentToolProperty =
            DependencyProperty.Register(nameof(CurrentTool), typeof(AnnotationShapeType), typeof(ZAnnotationCanvas),
                new FrameworkPropertyMetadata(AnnotationShapeType.BoundingBox));

        public static readonly DependencyProperty CurrentSeverityProperty =
            DependencyProperty.Register(nameof(CurrentSeverity), typeof(AnnotationSeverity), typeof(ZAnnotationCanvas),
                new FrameworkPropertyMetadata(AnnotationSeverity.Defect));

        public static readonly DependencyProperty IsDrawingModeProperty =
            DependencyProperty.Register(nameof(IsDrawingMode), typeof(bool), typeof(ZAnnotationCanvas),
                new FrameworkPropertyMetadata(true));

        public static readonly DependencyProperty DefaultStrokeThicknessProperty =
            DependencyProperty.Register(nameof(DefaultStrokeThickness), typeof(double), typeof(ZAnnotationCanvas),
                new FrameworkPropertyMetadata(2.0));

        public ObservableCollection<VisualAnnotationItem> Annotations
        {
            get => (ObservableCollection<VisualAnnotationItem>)GetValue(AnnotationsProperty);
            set => SetValue(AnnotationsProperty, value);
        }

        public AnnotationShapeType CurrentTool
        {
            get => (AnnotationShapeType)GetValue(CurrentToolProperty);
            set => SetValue(CurrentToolProperty, value);
        }

        public AnnotationSeverity CurrentSeverity
        {
            get => (AnnotationSeverity)GetValue(CurrentSeverityProperty);
            set => SetValue(CurrentSeverityProperty, value);
        }

        public bool IsDrawingMode
        {
            get => (bool)GetValue(IsDrawingModeProperty);
            set => SetValue(IsDrawingModeProperty, value);
        }

        public double DefaultStrokeThickness
        {
            get => (double)GetValue(DefaultStrokeThicknessProperty);
            set => SetValue(DefaultStrokeThicknessProperty, value);
        }

        public VisualAnnotationItem? SelectedAnnotation
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem != value)
                {
                    if (_selectedItem != null) _selectedItem.IsSelected = false;
                    _selectedItem = value;
                    if (_selectedItem != null) _selectedItem.IsSelected = true;
                    InvalidateVisual();
                }
            }
        }

        private static void OnAnnotationsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZAnnotationCanvas canvas)
            {
                if (e.OldValue is ObservableCollection<VisualAnnotationItem> oldColl)
                {
                    oldColl.CollectionChanged -= canvas.OnCollectionChanged;
                }
                if (e.NewValue is ObservableCollection<VisualAnnotationItem> newColl)
                {
                    newColl.CollectionChanged += canvas.OnCollectionChanged;
                }
                canvas.InvalidateVisual();
            }
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

        #endregion

        #region Mouse & Keyboard Interaction

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point pos = e.GetPosition(this);

                if (!IsDrawingMode)
                {
                    // Hit-test existing annotations
                    VisualAnnotationItem? hit = HitTestAnnotation(pos);
                    SelectedAnnotation = hit;
                    return;
                }

                _isDrawing = true;
                _drawStart = pos;
                _drawCurrent = pos;
                CaptureMouse();
                InvalidateVisual();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDrawing)
            {
                _drawCurrent = e.GetPosition(this);
                InvalidateVisual();
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDrawing)
            {
                _isDrawing = false;
                ReleaseMouseCapture();

                double dx = Math.Abs(_drawCurrent.X - _drawStart.X);
                double dy = Math.Abs(_drawCurrent.Y - _drawStart.Y);

                if (dx > 4 || dy > 4)
                {
                    Color stroke = GetSeverityColor(CurrentSeverity);
                    Color fill = Color.FromArgb(35, stroke.R, stroke.G, stroke.B);

                    var item = new VisualAnnotationItem
                    {
                        ShapeType = CurrentTool,
                        Severity = CurrentSeverity,
                        Label = $"{CurrentSeverity} #{Annotations.Count + 1}",
                        StartPoint = _drawStart,
                        EndPoint = _drawCurrent,
                        StrokeColor = stroke,
                        FillColor = fill,
                        StrokeThickness = DefaultStrokeThickness
                    };

                    Annotations.Add(item);
                    SelectedAnnotation = item;
                }

                InvalidateVisual();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Delete && SelectedAnnotation != null)
            {
                Annotations.Remove(SelectedAnnotation);
                SelectedAnnotation = null;
                InvalidateVisual();
                e.Handled = true;
            }
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            // Render existing annotations
            if (Annotations != null)
            {
                foreach (var item in Annotations)
                {
                    DrawAnnotationItem(dc, item);
                }
            }

            // Render in-progress drawing preview
            if (_isDrawing)
            {
                Color stroke = GetSeverityColor(CurrentSeverity);
                var previewItem = new VisualAnnotationItem
                {
                    ShapeType = CurrentTool,
                    Severity = CurrentSeverity,
                    Label = "Drawing...",
                    StartPoint = _drawStart,
                    EndPoint = _drawCurrent,
                    StrokeColor = stroke,
                    FillColor = Color.FromArgb(30, stroke.R, stroke.G, stroke.B),
                    StrokeThickness = DefaultStrokeThickness
                };
                DrawAnnotationItem(dc, previewItem);
            }
        }

        private void DrawAnnotationItem(DrawingContext dc, VisualAnnotationItem item)
        {
            var pen = new Pen(new SolidColorBrush(item.StrokeColor), item.StrokeThickness);
            pen.Freeze();
            var fillBrush = new SolidColorBrush(item.FillColor);
            fillBrush.Freeze();

            double x = Math.Min(item.StartPoint.X, item.EndPoint.X);
            double y = Math.Min(item.StartPoint.Y, item.EndPoint.Y);
            double w = Math.Abs(item.EndPoint.X - item.StartPoint.X);
            double h = Math.Abs(item.EndPoint.Y - item.StartPoint.Y);

            switch (item.ShapeType)
            {
                case AnnotationShapeType.BoundingBox:
                    dc.DrawRectangle(fillBrush, pen, new Rect(x, y, w, h));
                    DrawBadge(dc, item.Label, new Point(x, y - 18), item.StrokeColor);
                    break;

                case AnnotationShapeType.Ellipse:
                    dc.DrawEllipse(fillBrush, pen, new Point(x + w / 2.0, y + h / 2.0), w / 2.0, h / 2.0);
                    DrawBadge(dc, item.Label, new Point(x, y - 18), item.StrokeColor);
                    break;

                case AnnotationShapeType.Arrow:
                    DrawArrow(dc, item.StartPoint, item.EndPoint, pen);
                    DrawBadge(dc, item.Label, item.EndPoint, item.StrokeColor);
                    break;

                case AnnotationShapeType.Line:
                    dc.DrawLine(pen, item.StartPoint, item.EndPoint);
                    break;

                case AnnotationShapeType.TextCallout:
                    dc.DrawRectangle(fillBrush, pen, new Rect(x, y, w, h));
                    DrawBadge(dc, item.Label, new Point(x + 4, y + 4), item.StrokeColor);
                    break;

                case AnnotationShapeType.BlurPixelate:
                    // Semi-transparent checkerboard blur indicator
                    var blurBrush = new SolidColorBrush(Color.FromArgb(180, 20, 20, 30));
                    dc.DrawRectangle(blurBrush, pen, new Rect(x, y, w, h));
                    DrawBadge(dc, "🔒 MASKED", new Point(x + 4, y + 4), Color.FromRgb(148, 163, 184));
                    break;
            }

            if (item.IsSelected)
            {
                // Draw 4 resize handle gizmos
                DrawHandle(dc, new Point(x, y));
                DrawHandle(dc, new Point(x + w, y));
                DrawHandle(dc, new Point(x, y + h));
                DrawHandle(dc, new Point(x + w, y + h));
            }
        }

        private void DrawBadge(DrawingContext dc, string text, Point origin, Color color)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                _labelTypeface,
                11.0,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            var bgRect = new Rect(origin.X, origin.Y, ft.Width + 8, ft.Height + 4);
            var bgBrush = new SolidColorBrush(color);
            bgBrush.Freeze();

            dc.DrawRoundedRectangle(bgBrush, null, bgRect, 3, 3);
            dc.DrawText(ft, new Point(origin.X + 4, origin.Y + 2));
        }

        private static void DrawArrow(DrawingContext dc, Point start, Point end, Pen pen)
        {
            dc.DrawLine(pen, start, end);

            Vector dir = end - start;
            if (dir.Length < 1) return;
            dir.Normalize();

            Vector normal = new Vector(-dir.Y, dir.X);
            double arrowSize = 10.0;
            Point p1 = end - dir * arrowSize + normal * (arrowSize / 2.0);
            Point p2 = end - dir * arrowSize - normal * (arrowSize / 2.0);

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(end, true, true);
                ctx.LineTo(p1, true, false);
                ctx.LineTo(p2, true, false);
            }
            geo.Freeze();
            dc.DrawGeometry(pen.Brush, null, geo);
        }

        private static void DrawHandle(DrawingContext dc, Point p)
        {
            dc.DrawRectangle(SelectedHandleBrush, SelectedHandlePen, new Rect(p.X - 4, p.Y - 4, 8, 8));
        }

        private VisualAnnotationItem? HitTestAnnotation(Point pos)
        {
            if (Annotations == null) return null;
            for (int i = Annotations.Count - 1; i >= 0; i--)
            {
                var item = Annotations[i];
                double x = Math.Min(item.StartPoint.X, item.EndPoint.X);
                double y = Math.Min(item.StartPoint.Y, item.EndPoint.Y);
                double w = Math.Abs(item.EndPoint.X - item.StartPoint.X);
                double h = Math.Abs(item.EndPoint.Y - item.StartPoint.Y);

                var rect = new Rect(x - 4, y - 4, w + 8, h + 8);
                if (rect.Contains(pos)) return item;
            }
            return null;
        }

        private static Color GetSeverityColor(AnnotationSeverity severity) => severity switch
        {
            AnnotationSeverity.Ok => Color.FromRgb(166, 227, 161),       // Green
            AnnotationSeverity.Warning => Color.FromRgb(249, 226, 175),  // Amber
            AnnotationSeverity.Defect => Color.FromRgb(243, 139, 168),   // Red
            AnnotationSeverity.Critical => Color.FromRgb(235, 77, 75),   // Crimson
            _ => Color.FromRgb(129, 140, 248)                            // Blue/Purple Info
        };

        #endregion

        #region Bitmap Burning Utility

        /// <summary>
        /// Burns all annotations directly onto the target BitmapSource.
        /// Useful for report generation, QC archiving, and printing.
        /// </summary>
        public BitmapSource BurnAnnotationsToBitmap(BitmapSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawImage(source, new Rect(0, 0, source.PixelWidth, source.PixelHeight));

                // Scale factor between Canvas coordinates and native bitmap resolution
                double scaleX = ActualWidth > 0 ? source.PixelWidth / ActualWidth : 1.0;
                double scaleY = ActualHeight > 0 ? source.PixelHeight / ActualHeight : 1.0;

                foreach (var item in Annotations)
                {
                    var scaledItem = new VisualAnnotationItem
                    {
                        ShapeType = item.ShapeType,
                        Severity = item.Severity,
                        Label = item.Label,
                        StartPoint = new Point(item.StartPoint.X * scaleX, item.StartPoint.Y * scaleY),
                        EndPoint = new Point(item.EndPoint.X * scaleX, item.EndPoint.Y * scaleY),
                        StrokeColor = item.StrokeColor,
                        FillColor = item.FillColor,
                        StrokeThickness = item.StrokeThickness * scaleX
                    };
                    DrawAnnotationItem(dc, scaledItem);
                }
            }

            var rtb = new RenderTargetBitmap(source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        #endregion
    }
}
