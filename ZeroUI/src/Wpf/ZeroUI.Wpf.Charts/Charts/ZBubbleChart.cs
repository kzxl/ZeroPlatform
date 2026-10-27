using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Charts
{
    public class BubblePoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Size { get; set; }
        public string Label { get; set; } = string.Empty;
        public Color? Color { get; set; }

        public BubblePoint() { }
        public BubblePoint(double x, double y, double size, string label = "", Color? color = null)
        {
            X = x;
            Y = y;
            Size = size;
            Label = label;
            Color = color;
        }
    }

    public class ZBubbleChart : FrameworkElement
    {
        public static readonly DependencyProperty XAxisTitleProperty =
            DependencyProperty.Register(nameof(XAxisTitle), typeof(string), typeof(ZBubbleChart),
                new FrameworkPropertyMetadata("X Axis", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty YAxisTitleProperty =
            DependencyProperty.Register(nameof(YAxisTitle), typeof(string), typeof(ZBubbleChart),
                new FrameworkPropertyMetadata("Y Axis", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowLabelsProperty =
            DependencyProperty.Register(nameof(ShowLabels), typeof(bool), typeof(ZBubbleChart),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        private List<BubblePoint> _dataSource = new List<BubblePoint>();

        public List<BubblePoint> DataSource
        {
            get => _dataSource;
            set { _dataSource = value ?? new List<BubblePoint>(); InvalidateVisual(); }
        }

        public string XAxisTitle
        {
            get => (string)GetValue(XAxisTitleProperty);
            set => SetValue(XAxisTitleProperty, value);
        }

        public string YAxisTitle
        {
            get => (string)GetValue(YAxisTitleProperty);
            set => SetValue(YAxisTitleProperty, value);
        }

        public bool ShowLabels
        {
            get => (bool)GetValue(ShowLabelsProperty);
            set => SetValue(ShowLabelsProperty, value);
        }

        public ZBubbleChart()
        {
            Width = 400;
            Height = 300;
            Loaded += (s, e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            Unloaded += (s, e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;

            // Default demo data
            _dataSource.Add(new BubblePoint(10, 25, 15, "Node A", Color.FromRgb(52, 152, 219)));
            _dataSource.Add(new BubblePoint(35, 60, 30, "Node B", Color.FromRgb(46, 204, 113)));
            _dataSource.Add(new BubblePoint(55, 40, 22, "Node C", Color.FromRgb(241, 196, 15)));
            _dataSource.Add(new BubblePoint(80, 85, 45, "Node D", Color.FromRgb(231, 76, 60)));
        }

        private void OnThemeChanged()
        {
            if (Dispatcher.CheckAccess()) InvalidateVisual();
            else Dispatcher.BeginInvoke((Action)InvalidateVisual);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth > 0 ? ActualWidth : Width;
            double h = ActualHeight > 0 ? ActualHeight : Height;

            Rect plotRect = new Rect(40, 20, Math.Max(10, w - 60), Math.Max(10, h - 55));
            if (plotRect.Width <= 10 || plotRect.Height <= 10) return;

            // 1. Grid Lines
            var gridPen = new Pen(ZeroWpfTheme.BorderSubtle, 1.0)
            {
                DashStyle = DashStyles.Dash
            };
            for (int i = 1; i <= 4; i++)
            {
                double y = plotRect.Bottom - (plotRect.Height * (i / 5.0));
                dc.DrawLine(gridPen, new Point(plotRect.Left, y), new Point(plotRect.Right, y));

                double x = plotRect.Left + (plotRect.Width * (i / 5.0));
                dc.DrawLine(gridPen, new Point(x, plotRect.Top), new Point(x, plotRect.Bottom));
            }

            // 2. Axes
            var axisPen = new Pen(ZeroWpfTheme.BorderDefault, 1.5);
            dc.DrawLine(axisPen, new Point(plotRect.Left, plotRect.Bottom), new Point(plotRect.Right, plotRect.Bottom));
            dc.DrawLine(axisPen, new Point(plotRect.Left, plotRect.Top), new Point(plotRect.Left, plotRect.Bottom));

            if (_dataSource.Count == 0) return;

            double minX = _dataSource.Min(p => p.X);
            double maxX = _dataSource.Max(p => p.X);
            double minY = _dataSource.Min(p => p.Y);
            double maxY = _dataSource.Max(p => p.Y);
            double minSize = _dataSource.Min(p => p.Size);
            double maxSize = _dataSource.Max(p => p.Size);

            if (Math.Abs(maxX - minX) < double.Epsilon) { minX -= 10; maxX += 10; }
            if (Math.Abs(maxY - minY) < double.Epsilon) { minY -= 10; maxY += 10; }
            if (Math.Abs(maxSize - minSize) < double.Epsilon) { maxSize = minSize + 1; }

            // 3. Render Bubbles
            foreach (var point in _dataSource)
            {
                double cx = plotRect.Left + (point.X - minX) / (maxX - minX) * plotRect.Width;
                double cy = plotRect.Bottom - (point.Y - minY) / (maxY - minY) * plotRect.Height;

                const double minR = 6.0;
                const double maxR = 26.0;
                double r = minR + (point.Size - minSize) / (maxSize - minSize) * (maxR - minR);

                Color baseColor = point.Color ?? Color.FromRgb(99, 102, 241);
                var fillBrush = new SolidColorBrush(Color.FromArgb(160, baseColor.R, baseColor.G, baseColor.B));
                var strokePen = new Pen(new SolidColorBrush(baseColor), 1.5);

                dc.DrawEllipse(fillBrush, strokePen, new Point(cx, cy), r, r);

                if (ShowLabels && !string.IsNullOrEmpty(point.Label) && r >= 10)
                {
                    var ft = new FormattedText(
                        point.Label,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        ZeroWpfTheme.BoldTypeface,
                        10,
                        ZeroWpfTheme.TextPrimary,
                        1.0);

                    dc.DrawText(ft, new Point(cx - ft.Width / 2.0, cy - ft.Height / 2.0));
                }
            }
        }
    }

    [Obsolete("ZeroBubbleChart is deprecated. Use ZBubbleChart instead.")]
    public class ZeroBubbleChart : ZBubbleChart { }
}
