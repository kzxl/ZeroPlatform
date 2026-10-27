using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Feedback
{
    public enum SkeletonShape
    {
        Rectangle,
        RoundedRectangle,
        Circle
    }

    [Obsolete("WpfSkeletonShape is deprecated. Use SkeletonShape instead.")]
    public enum WpfSkeletonShape
    {
        Rectangle = SkeletonShape.Rectangle,
        RoundedRectangle = SkeletonShape.RoundedRectangle,
        Circle = SkeletonShape.Circle
    }

    /// <summary>
    /// Modern hardware-accelerated animated Skeleton/Shimmer loading placeholder for ZeroUI WPF.
    /// Provides smooth gradient shimmer waves imitating UI cards, text, and avatars
    /// while asynchronous data requests resolve.
    /// </summary>
    public class ZSkeleton : Control
    {
        private Rectangle? _rectShape;
        private Ellipse? _circleShape;

        public static readonly DependencyProperty ShapeProperty =
            DependencyProperty.Register(nameof(Shape), typeof(SkeletonShape), typeof(ZSkeleton), new PropertyMetadata(SkeletonShape.RoundedRectangle, OnShapeChanged));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(ZSkeleton), new PropertyMetadata(new CornerRadius(4), OnCornerRadiusChanged));

        public SkeletonShape Shape
        {
            get => (SkeletonShape)GetValue(ShapeProperty);
            set => SetValue(ShapeProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        static ZSkeleton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZSkeleton), new FrameworkPropertyMetadata(typeof(ZSkeleton)));
        }

        public ZSkeleton()
        {
            Height = 24;
            Width = 160;
            BuildShimmerVisual();
        }

        private Grid? _grid;

        private void BuildShimmerVisual()
        {
            var grid = new Grid();
            _grid = grid;

            // Setup hardware-accelerated LinearGradientBrush shimmer
            var lgb = new LinearGradientBrush
            {
                StartPoint = new Point(-1, 0),
                EndPoint = new Point(2, 0)
            };

            Color baseColor = Color.FromArgb(40, 128, 128, 128);
            Color shimmer = Color.FromArgb(90, 200, 200, 200);

            lgb.GradientStops.Add(new GradientStop(baseColor, 0.0));
            lgb.GradientStops.Add(new GradientStop(shimmer, 0.5));
            lgb.GradientStops.Add(new GradientStop(baseColor, 1.0));

            // Start shimmer translation animation
            var animStart = new PointAnimation
            {
                From = new Point(-1.5, 0),
                To = new Point(1.5, 0),
                Duration = TimeSpan.FromSeconds(1.6),
                RepeatBehavior = RepeatBehavior.Forever
            };
            var animEnd = new PointAnimation
            {
                From = new Point(0, 0),
                To = new Point(3.0, 0),
                Duration = TimeSpan.FromSeconds(1.6),
                RepeatBehavior = RepeatBehavior.Forever
            };

            lgb.BeginAnimation(LinearGradientBrush.StartPointProperty, animStart);
            lgb.BeginAnimation(LinearGradientBrush.EndPointProperty, animEnd);

            _rectShape = new Rectangle
            {
                Fill = lgb,
                RadiusX = CornerRadius.TopLeft,
                RadiusY = CornerRadius.TopLeft,
                Visibility = (Shape == SkeletonShape.Circle) ? Visibility.Collapsed : Visibility.Visible
            };
            grid.Children.Add(_rectShape);

            _circleShape = new Ellipse
            {
                Fill = lgb,
                Visibility = (Shape == SkeletonShape.Circle) ? Visibility.Visible : Visibility.Collapsed
            };
            grid.Children.Add(_circleShape);

            AddVisualChild(grid);
            AddLogicalChild(grid);
        }

        protected override int VisualChildrenCount => _grid != null ? 1 : 0;
        protected override Visual GetVisualChild(int index) => _grid ?? throw new ArgumentOutOfRangeException(nameof(index));

        protected override Size MeasureOverride(Size constraint)
        {
            if (_grid != null)
            {
                _grid.Measure(constraint);
                return _grid.DesiredSize;
            }
            return base.MeasureOverride(constraint);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _grid?.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        private static void OnShapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZSkeleton s)
            {
                var shape = (SkeletonShape)e.NewValue;
                if (s._rectShape != null) s._rectShape.Visibility = (shape == SkeletonShape.Circle) ? Visibility.Collapsed : Visibility.Visible;
                if (s._circleShape != null) s._circleShape.Visibility = (shape == SkeletonShape.Circle) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static void OnCornerRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZSkeleton s && s._rectShape != null)
            {
                var cr = (CornerRadius)e.NewValue;
                s._rectShape.RadiusX = cr.TopLeft;
                s._rectShape.RadiusY = cr.TopLeft;
            }
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZSkeleton"/>.
    /// </summary>
    [Obsolete("SkeletonControl is deprecated and will be removed in 5 release cycles. Please migrate to ZSkeleton instead.")]
    public class SkeletonControl : ZSkeleton
    {
        static SkeletonControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SkeletonControl), new FrameworkPropertyMetadata(typeof(ZSkeleton)));
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZSkeleton"/>.
    /// </summary>
    [Obsolete("ZeroSkeleton is deprecated and will be removed in 5 release cycles. Please migrate to ZSkeleton instead.")]
    public class ZeroSkeleton : ZSkeleton
    {
        static ZeroSkeleton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZeroSkeleton), new FrameworkPropertyMetadata(typeof(ZSkeleton)));
        }
    }
}
