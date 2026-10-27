using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Precision rating editor for WPF supporting half-star increments (0.5), custom vector glyphs,
    /// smooth hover previews, and bidirectional IZeroEditor data binding.
    /// </summary>
    public class ZRating : FrameworkElement, IZeroEditor
    {
        private readonly RatingModel _model = new RatingModel();
        private decimal? _hoverValue;
        private bool _isModified;

        public RatingModel Model => _model;

        #region Dependency Properties

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(decimal), typeof(ZRating),
                new FrameworkPropertyMetadata(0m, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnValueChanged));

        public static readonly DependencyProperty MaxRatingProperty =
            DependencyProperty.Register(nameof(MaxRating), typeof(int), typeof(ZRating),
                new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnMaxRatingChanged));

        public static readonly DependencyProperty AllowHalfProperty =
            DependencyProperty.Register(nameof(AllowHalf), typeof(bool), typeof(ZRating),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender, OnAllowHalfChanged));

        public static readonly DependencyProperty ShapeProperty =
            DependencyProperty.Register(nameof(Shape), typeof(RatingShape), typeof(ZRating),
                new FrameworkPropertyMetadata(RatingShape.Star, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ItemSizeProperty =
            DependencyProperty.Register(nameof(ItemSize), typeof(double), typeof(ZRating),
                new FrameworkPropertyMetadata(22.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ItemSpacingProperty =
            DependencyProperty.Register(nameof(ItemSpacing), typeof(double), typeof(ZRating),
                new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZRating),
                new PropertyMetadata(false));

        #endregion

        #region Properties & Events

        public decimal Value
        {
            get => (decimal)GetValue(ValueProperty);
            set => SetValue(ValueProperty, ClampValue(value));
        }

        public int MaxRating
        {
            get => (int)GetValue(MaxRatingProperty);
            set => SetValue(MaxRatingProperty, Math.Max(1, value));
        }

        public bool AllowHalf
        {
            get => (bool)GetValue(AllowHalfProperty);
            set => SetValue(AllowHalfProperty, value);
        }

        public RatingShape Shape
        {
            get => (RatingShape)GetValue(ShapeProperty);
            set => SetValue(ShapeProperty, value);
        }

        public double ItemSize
        {
            get => (double)GetValue(ItemSizeProperty);
            set => SetValue(ItemSizeProperty, value);
        }

        public double ItemSpacing
        {
            get => (double)GetValue(ItemSpacingProperty);
            set => SetValue(ItemSpacingProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public bool IsModified
        {
            get => _isModified;
            set => _isModified = value;
        }

        public object? EditValue
        {
            get => Value;
            set
            {
                if (value is decimal d) Value = d;
                else if (value is double dbl) Value = (decimal)dbl;
                else if (value is float flt) Value = (decimal)flt;
                else if (value is int i) Value = i;
                else if (value != null && decimal.TryParse(value.ToString(), out var parsed)) Value = parsed;
                else Value = 0m;
            }
        }

        public void Reset()
        {
            Value = 0m;
            _isModified = false;
        }

        public void Clear()
        {
            Value = 0m;
            _isModified = false;
        }

        public event EventHandler<decimal>? ValueChanged;
        public event EventHandler? EditValueChanged;

        #endregion

        public ZRating()
        {
            Cursor = Cursors.Hand;
            Focusable = true;
            Height = 32;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZRating rc)
            {
                var val = (decimal)e.NewValue;
                rc._model.Value = val;
                rc.ValueChanged?.Invoke(rc, val);
                rc.EditValueChanged?.Invoke(rc, EventArgs.Empty);
            }
        }

        private static void OnMaxRatingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZRating rc)
            {
                rc._model.MaxRating = (int)e.NewValue;
            }
        }

        private static void OnAllowHalfChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZRating rc)
            {
                rc._model.AllowHalf = (bool)e.NewValue;
            }
        }

        private decimal ClampValue(decimal val)
        {
            return _model.ClampValue(val);
        }

        #region Measure & Input

        protected override Size MeasureOverride(Size availableSize)
        {
            int count = Math.Max(1, MaxRating);
            double w = (count * ItemSize) + ((count - 1) * ItemSpacing) + 8;
            double h = Math.Max(ItemSize + 8, Height);
            return new Size(w, h);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (ReadOnly || !IsEnabled) return;

            Point pt = e.GetPosition(this);
            decimal hovered = CalculateValueFromPoint(pt.X);
            if (_hoverValue != hovered)
            {
                _hoverValue = hovered;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverValue != null)
            {
                _hoverValue = null;
                InvalidateVisual();
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (ReadOnly || !IsEnabled) return;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Focus();
                decimal clicked = CalculateValueFromPoint(e.GetPosition(this).X);
                Value = clicked;
                IsModified = true;
                e.Handled = true;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (ReadOnly || !IsEnabled) return;

            if (e.Key == Key.Left || e.Key == Key.Down)
            {
                _model.StepDown();
                Value = _model.Value;
                IsModified = true;
                e.Handled = true;
            }
            else if (e.Key == Key.Right || e.Key == Key.Up)
            {
                _model.StepUp();
                Value = _model.Value;
                IsModified = true;
                e.Handled = true;
            }
        }

        private decimal CalculateValueFromPoint(double x)
        {
            return _model.CalculateScoreFromPosition(x, 4, ItemSize, ItemSpacing);
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double h = ActualHeight;
            int count = Math.Max(1, MaxRating);
            double startX = 4;
            double cy = h / 2.0;

            decimal effectiveScore = _hoverValue ?? Value;
            bool isHovering = _hoverValue != null;

            Brush ratedBrush = isHovering
                ? new SolidColorBrush(Color.FromRgb(251, 191, 36))  // Amber 400
                : new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber 500
            ratedBrush.Freeze();

            Brush unratedBrush = new SolidColorBrush(ZeroWpfTheme.IsDark
                ? Color.FromRgb(51, 65, 85)     // Slate 700
                : Color.FromRgb(203, 213, 225)); // Slate 300
            unratedBrush.Freeze();

            for (int i = 0; i < count; i++)
            {
                double itemLeft = startX + (i * (ItemSize + ItemSpacing));
                Rect itemRect = new Rect(itemLeft, cy - (ItemSize / 2.0), ItemSize, ItemSize);

                decimal itemScore = effectiveScore - i;
                Geometry shapeGeom = CreateShapeGeometry(Shape, itemRect);

                if (itemScore >= 1.0m)
                {
                    // Fully rated
                    dc.DrawGeometry(ratedBrush, null, shapeGeom);
                }
                else if (itemScore >= 0.5m)
                {
                    // Half rated: draw unrated full, then clip left half rated
                    dc.DrawGeometry(unratedBrush, null, shapeGeom);

                    dc.PushClip(new RectangleGeometry(new Rect(itemRect.X, itemRect.Y, itemRect.Width / 2.0, itemRect.Height)));
                    dc.DrawGeometry(ratedBrush, null, shapeGeom);
                    dc.Pop();
                }
                else
                {
                    // Unrated
                    dc.DrawGeometry(unratedBrush, null, shapeGeom);
                }
            }
        }

        private static Geometry CreateShapeGeometry(RatingShape shape, Rect bounds)
        {
            double cx = bounds.X + bounds.Width / 2.0;
            double cy = bounds.Y + bounds.Height / 2.0;
            double r = Math.Min(bounds.Width, bounds.Height) / 2.0;

            StreamGeometry geom = new StreamGeometry();
            using (StreamGeometryContext ctx = geom.Open())
            {
                switch (shape)
                {
                    case RatingShape.Star:
                        DrawStar(ctx, cx, cy, r, r * 0.42);
                        break;
                    case RatingShape.Diamond:
                        ctx.BeginFigure(new Point(cx, bounds.Top), true, true);
                        ctx.LineTo(new Point(bounds.Right, cy), true, false);
                        ctx.LineTo(new Point(cx, bounds.Bottom), true, false);
                        ctx.LineTo(new Point(bounds.Left, cy), true, false);
                        break;
                    case RatingShape.Heart:
                        DrawHeart(ctx, bounds);
                        break;
                    case RatingShape.Shield:
                        DrawShield(ctx, bounds);
                        break;
                }
            }
            geom.Freeze();
            return geom;
        }

        private static void DrawStar(StreamGeometryContext ctx, double cx, double cy, double outerR, double innerR)
        {
            int points = 5;
            double step = Math.PI / points;
            double startAngle = -Math.PI / 2.0;

            Point first = new Point(cx + outerR * Math.Cos(startAngle), cy + outerR * Math.Sin(startAngle));
            ctx.BeginFigure(first, true, true);

            for (int i = 1; i < points * 2; i++)
            {
                double angle = startAngle + (i * step);
                double r = (i % 2 == 1) ? innerR : outerR;
                ctx.LineTo(new Point(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle)), true, false);
            }
        }

        private static void DrawHeart(StreamGeometryContext ctx, Rect r)
        {
            Point topMid = new Point(r.X + r.Width / 2.0, r.Y + r.Height * 0.25);
            ctx.BeginFigure(topMid, true, true);
            ctx.BezierTo(
                new Point(r.X + r.Width * 0.1, r.Y),
                new Point(r.X, r.Y + r.Height * 0.4),
                new Point(r.X + r.Width / 2.0, r.Bottom),
                true, false);
            ctx.BezierTo(
                new Point(r.Right, r.Y + r.Height * 0.4),
                new Point(r.X + r.Width * 0.9, r.Y),
                topMid,
                true, false);
        }

        private static void DrawShield(StreamGeometryContext ctx, Rect r)
        {
            ctx.BeginFigure(new Point(r.X + r.Width / 2.0, r.Y), true, true);
            ctx.LineTo(new Point(r.Right, r.Y + r.Height * 0.15), true, false);
            ctx.BezierTo(
                new Point(r.Right, r.Y + r.Height * 0.65),
                new Point(r.X + r.Width * 0.7, r.Bottom),
                new Point(r.X + r.Width / 2.0, r.Bottom),
                true, false);
            ctx.BezierTo(
                new Point(r.X + r.Width * 0.3, r.Bottom),
                new Point(r.X, r.Y + r.Height * 0.65),
                new Point(r.X, r.Y + r.Height * 0.15),
                true, false);
        }

        #endregion
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        InvalidateMeasure();
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
    /// Convenience alias for <see cref="ZRating"/>.
    /// </summary>
    public class ZRatingControl : ZRating { }

    /// <summary>
    /// Legacy alias for <see cref="ZRating"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("RatingControl is deprecated and will be removed in 5 release cycles. Please migrate to ZRating instead.")]
    public class RatingControl : ZRating { }

    /// <summary>
    /// Legacy alias for <see cref="ZRating"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroRatingControl is deprecated and will be removed in 5 release cycles. Please migrate to ZRating instead.")]
    public class ZeroRatingControl : ZRating { }

    #endregion

}
