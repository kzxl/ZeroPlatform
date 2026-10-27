using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Analytics;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Charts
{
    /// <summary>
    /// Multi-tiered radial partition diagram (Sunburst) visualizing hierarchical tree proportions in WPF.
    /// Thin WPF rendering layer consuming ZeroUI.Core.Analytics.SunburstLayoutEngine.
    /// </summary>
    public class ZSunburstChart : FrameworkElement
    {
        private SunburstNode? _rootNode;
        private string _title = "Hierarchical Sunburst Breakdown";
        private double _innerHoleRatio = 0.25;

        private SunburstSector? _hoverSector;
        private IReadOnlyList<SunburstSector>? _lastSectors;
        private Point _center;

        public SunburstNode? RootNode
        {
            get => _rootNode;
            set { _rootNode = value; InvalidateVisual(); }
        }

        public string Title
        {
            get => _title;
            set { _title = value ?? string.Empty; InvalidateVisual(); }
        }

        public double InnerHoleRatio
        {
            get => _innerHoleRatio;
            set { _innerHoleRatio = Math.Max(0.05, Math.Min(0.7, value)); InvalidateVisual(); }
        }

        public ZSunburstChart()
        {
            ClipToBounds = true;
            MinHeight = 240;
            MinWidth = 280;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public void LoadSampleData()
        {
            var root = new SunburstNode("Total Enterprise", 0);

            var sales = new SunburstNode("Sales", 0, 0xFF3B82F6);
            sales.Children.Add(new SunburstNode("Domestic", 180));
            sales.Children.Add(new SunburstNode("Global", 240));

            var tech = new SunburstNode("Technology", 0, 0xFF10B981);
            tech.Children.Add(new SunburstNode("Software", 140));
            tech.Children.Add(new SunburstNode("Hardware", 95));
            tech.Children.Add(new SunburstNode("Cloud", 165));

            var ops = new SunburstNode("Operations", 0, 0xFFF97316);
            ops.Children.Add(new SunburstNode("Logistics", 110));
            ops.Children.Add(new SunburstNode("Quality", 80));

            root.Children.Add(sales);
            root.Children.Add(tech);
            root.Children.Add(ops);

            _rootNode = root;
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var pos = e.GetPosition(this);
            SunburstSector? newHover = null;

            if (_lastSectors != null)
            {
                for (int i = _lastSectors.Count - 1; i >= 0; i--)
                {
                    var s = _lastSectors[i];
                    if (s.Contains(pos.X, pos.Y, _center.X, _center.Y))
                    {
                        newHover = s;
                        break;
                    }
                }
            }

            if (newHover != _hoverSector)
            {
                _hoverSector = newHover;
                if (_hoverSector != null && _rootNode != null)
                {
                    double total = _rootNode.GetEffectiveValue();
                    double val = _hoverSector.Node.GetEffectiveValue();
                    double pct = total > 0 ? (val / total) : 0;
                    ToolTip = $"{_hoverSector.Node.Name}\nValue: {val:N0}\nShare: {pct:P1}\nDepth: L{_hoverSector.Depth}";
                }
                else
                {
                    ToolTip = null;
                }
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverSector != null)
            {
                _hoverSector = null;
                ToolTip = null;
                InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 10 || h <= 10) return;

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, 0, w, h));

            // Title
            double topMargin = 36.0;
            if (!string.IsNullOrEmpty(_title))
            {
                var titleText = new FormattedText(
                    _title,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.BoldTypeface,
                    13,
                    ZeroWpfTheme.TextPrimary,
                    1.0);
                dc.DrawText(titleText, new Point(14, 8));
            }

            var root = _rootNode ?? GetPreviewRoot();
            double availW = w - 30.0;
            double availH = h - topMargin - 20.0;
            double maxR = Math.Min(availW, availH) / 2.0;
            if (maxR < 20.0) return;

            _center = new Point(15.0 + (availW / 2.0), topMargin + (availH / 2.0));
            _lastSectors = SunburstLayoutEngine.ComputeLayout(root, maxR, _innerHoleRatio);
            if (_lastSectors.Count == 0) return;

            Color[] palette = {
                Color.FromRgb(59, 130, 246),
                Color.FromRgb(16, 185, 129),
                Color.FromRgb(249, 115, 22),
                Color.FromRgb(168, 85, 247),
                Color.FromRgb(236, 72, 153),
                Color.FromRgb(14, 165, 233),
                Color.FromRgb(234, 179, 8)
            };

            var strokeColor = ZeroWpfTheme.IsDark ? Color.FromRgb(15, 23, 42) : Colors.White;
            var strokePen = new Pen(new SolidColorBrush(strokeColor), 1.5);
            strokePen.Freeze();

            int sectorIdx = 0;
            foreach (var s in _lastSectors)
            {
                Color baseColor;
                if (s.Node.ColorRgba.HasValue)
                {
                    baseColor = s.Node.ColorRgba.ToColor(palette[sectorIdx % palette.Length]);
                }
                else
                {
                    baseColor = palette[sectorIdx % palette.Length];
                    if (s.Depth > 1)
                    {
                        int shift = (s.Depth - 1) * 20;
                        baseColor = Color.FromRgb(
                            (byte)Math.Min(255, baseColor.R + shift),
                            (byte)Math.Min(255, baseColor.G + shift),
                            (byte)Math.Min(255, baseColor.B + shift));
                    }
                }
                sectorIdx++;

                bool isHover = (_hoverSector == s);
                if (isHover)
                {
                    baseColor = Color.FromRgb(
                        (byte)Math.Min(255, baseColor.R + 35),
                        (byte)Math.Min(255, baseColor.G + 35),
                        (byte)Math.Min(255, baseColor.B + 35));
                }

                var fillBrush = new SolidColorBrush(baseColor);
                fillBrush.Freeze();

                var geometry = CreateSectorGeometry(_center, s.InnerRadius, s.OuterRadius, s.StartAngle, s.SweepAngle);
                dc.DrawGeometry(fillBrush, strokePen, geometry);

                // Sector text label if large enough
                if (s.SweepAngle >= 15.0 && (s.OuterRadius - s.InnerRadius) >= 16.0)
                {
                    double midRad = (s.MidAngle * Math.PI) / 180.0;
                    double midR = s.MidRadius;
                    double tx = _center.X + (midR * Math.Cos(midRad));
                    double ty = _center.Y + (midR * Math.Sin(midRad));

                    var labelText = new FormattedText(
                        s.Node.Name,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        ZeroWpfTheme.BoldTypeface,
                        9.0,
                        Brushes.White,
                        1.0);
                    dc.DrawText(labelText, new Point(tx - (labelText.Width / 2.0), ty - (labelText.Height / 2.0)));
                }
            }

            // Center Hole Text
            string centerLabel = _hoverSector != null ? _hoverSector.Node.Name : root.Name;
            double centerVal = _hoverSector != null ? _hoverSector.Node.GetEffectiveValue() : root.GetEffectiveValue();

            var centerTitle = new FormattedText(
                $"{centerLabel}\n{centerVal:N0}",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                ZeroWpfTheme.BoldTypeface,
                11.0,
                ZeroWpfTheme.TextPrimary,
                1.0)
            {
                TextAlignment = TextAlignment.Center
            };
            dc.DrawText(centerTitle, new Point(_center.X - (centerTitle.Width / 2.0), _center.Y - (centerTitle.Height / 2.0)));
        }

        private static StreamGeometry CreateSectorGeometry(Point center, double innerR, double outerR, double startAngle, double sweepAngle)
        {
            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                double startRad = (startAngle * Math.PI) / 180.0;
                double endRad = ((startAngle + sweepAngle) * Math.PI) / 180.0;

                Point outerStart = new Point(center.X + outerR * Math.Cos(startRad), center.Y + outerR * Math.Sin(startRad));
                Point outerEnd = new Point(center.X + outerR * Math.Cos(endRad), center.Y + outerR * Math.Sin(endRad));
                Point innerEnd = new Point(center.X + innerR * Math.Cos(endRad), center.Y + innerR * Math.Sin(endRad));
                Point innerStart = new Point(center.X + innerR * Math.Cos(startRad), center.Y + innerR * Math.Sin(startRad));

                bool isLargeArc = sweepAngle > 180.0;

                ctx.BeginFigure(outerStart, true, true);
                ctx.ArcTo(outerEnd, new Size(outerR, outerR), 0, isLargeArc, SweepDirection.Clockwise, true, true);

                if (innerR > 1.0)
                {
                    ctx.LineTo(innerEnd, true, true);
                    ctx.ArcTo(innerStart, new Size(innerR, innerR), 0, isLargeArc, SweepDirection.Counterclockwise, true, true);
                }
                else
                {
                    ctx.LineTo(center, true, true);
                }
            }
            geom.Freeze();
            return geom;
        }

        private static SunburstNode GetPreviewRoot()
        {
            var root = new SunburstNode("Overview", 0);
            var a = new SunburstNode("North", 120);
            var b = new SunburstNode("South", 90);
            var c = new SunburstNode("East", 150);
            root.Children.Add(a);
            root.Children.Add(b);
            root.Children.Add(c);
            return root;
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
    /// Legacy alias for <see cref="ZSunburstChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SunburstChart is deprecated and will be removed in 5 release cycles. Please migrate to ZSunburstChart instead.")]
    public class SunburstChart : ZSunburstChart { }

    /// <summary>
    /// Legacy alias for <see cref="ZSunburstChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroSunburstChart is deprecated and will be removed in 5 release cycles. Please migrate to ZSunburstChart instead.")]
    public class ZeroSunburstChart : ZSunburstChart { }

    #endregion

}
