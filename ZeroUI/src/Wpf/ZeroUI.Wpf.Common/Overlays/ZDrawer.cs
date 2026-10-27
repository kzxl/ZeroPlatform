using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using ZeroUI.Core.Theme;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Overlays
{
    public enum DrawerEdge
    {
        Right,
        Left,
        Top,
        Bottom
    }

    /// <summary>
    /// High-performance sliding detail drawer overlay for ZeroUI WPF.
    /// Uses hardware-accelerated TranslateTransform with cubic easing (220ms)
    /// to guarantee fluid 60-120 FPS sliding without triggering layout storms on sibling controls.
    /// </summary>
    [ContentProperty(nameof(ContentElement))]
    public class ZDrawer : ZeroWpfControlBase
    {
        private Grid? _rootGrid;
        private Border? _scrimBorder;
        private Border? _drawerPanel;
        private ContentPresenter? _contentPresenter;
        private TextBlock? _titleBlock;
        private TextBlock? _subtitleBlock;
        private TranslateTransform? _translateTransform;
        private bool _isAnimating;

        #region Dependency Properties

        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(
                nameof(IsOpen),
                typeof(bool),
                typeof(ZDrawer),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title),
                typeof(string),
                typeof(ZDrawer),
                new PropertyMetadata("Detail Inspector", OnTitleChanged));

        public static readonly DependencyProperty SubtitleProperty =
            DependencyProperty.Register(
                nameof(Subtitle),
                typeof(string),
                typeof(ZDrawer),
                new PropertyMetadata(null, OnSubtitleChanged));

        public static readonly DependencyProperty DrawerWidthProperty =
            DependencyProperty.Register(
                nameof(DrawerWidth),
                typeof(double),
                typeof(ZDrawer),
                new PropertyMetadata(400.0, OnDrawerDimensionChanged));

        public static readonly DependencyProperty DrawerHeightProperty =
            DependencyProperty.Register(
                nameof(DrawerHeight),
                typeof(double),
                typeof(ZDrawer),
                new PropertyMetadata(350.0, OnDrawerDimensionChanged));

        public static readonly DependencyProperty EdgeProperty =
            DependencyProperty.Register(
                nameof(Edge),
                typeof(DrawerEdge),
                typeof(ZDrawer),
                new PropertyMetadata(DrawerEdge.Right, OnDrawerDimensionChanged));

        public static readonly DependencyProperty CloseOnDimClickProperty =
            DependencyProperty.Register(
                nameof(CloseOnDimClick),
                typeof(bool),
                typeof(ZDrawer),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ContentElementProperty =
            DependencyProperty.Register(
                nameof(ContentElement),
                typeof(object),
                typeof(ZDrawer),
                new PropertyMetadata(null, OnContentElementChanged));

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string? Subtitle
        {
            get => (string?)GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public double DrawerWidth
        {
            get => (double)GetValue(DrawerWidthProperty);
            set => SetValue(DrawerWidthProperty, value);
        }

        public double DrawerHeight
        {
            get => (double)GetValue(DrawerHeightProperty);
            set => SetValue(DrawerHeightProperty, value);
        }

        public DrawerEdge Edge
        {
            get => (DrawerEdge)GetValue(EdgeProperty);
            set => SetValue(EdgeProperty, value);
        }

        public bool CloseOnDimClick
        {
            get => (bool)GetValue(CloseOnDimClickProperty);
            set => SetValue(CloseOnDimClickProperty, value);
        }

        public object? ContentElement
        {
            get => GetValue(ContentElementProperty);
            set => SetValue(ContentElementProperty, value);
        }

        #endregion

        #region Public Methods

        public void Open() => IsOpen = true;
        public void Close() => IsOpen = false;
        public void Toggle() => IsOpen = !IsOpen;

        #endregion

        #region Events

        public event EventHandler? Opened;
        public event EventHandler? Closed;

        #endregion

        static ZDrawer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZDrawer), new FrameworkPropertyMetadata(typeof(ZDrawer)));
        }

        public ZDrawer()
        {
            Visibility = Visibility.Collapsed;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
            Focusable = true;

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape && IsOpen && !_isAnimating)
                {
                    IsOpen = false;
                    e.Handled = true;
                }
            };

            BuildVisualTemplate();
        }

        private void BuildVisualTemplate()
        {
            _rootGrid = new Grid();

            // 1. Dimmed Scrim Background
            _scrimBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)),
                Opacity = 0.0,
                IsHitTestVisible = true
            };
            _scrimBorder.MouseDown += (s, e) =>
            {
                if (CloseOnDimClick && !_isAnimating)
                {
                    IsOpen = false;
                }
            };
            _rootGrid.Children.Add(_scrimBorder);

            // 2. Sliding Drawer Panel
            _translateTransform = new TranslateTransform();
            _drawerPanel = new Border
            {
                Background = ZeroWpfTheme.BgCard,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                RenderTransform = _translateTransform,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 24,
                    ShadowDepth = 0,
                    Opacity = 0.4
                }
            };

            UpdatePanelLayout();

            // Inner Content Grid (Header + Body)
            var innerGrid = new Grid();
            innerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48, GridUnitType.Pixel) });
            innerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Header Border
            var headerBorder = new Border
            {
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 0, 12, 0)
            };

            var headerDock = new DockPanel { LastChildFill = true };

            // Close button ('✕')
            var closeBtn = new Button
            {
                Content = "✕",
                Width = 28,
                Height = 28,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = ZeroWpfTheme.TextSecondary,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            closeBtn.Click += (s, e) => IsOpen = false;
            DockPanel.SetDock(closeBtn, Dock.Right);
            headerDock.Children.Add(closeBtn);

            // Title & Subtitle in StackPanel
            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _titleBlock = new TextBlock
            {
                Text = Title,
                Foreground = ZeroWpfTheme.TextPrimary,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14
            };
            titleStack.Children.Add(_titleBlock);

            _subtitleBlock = new TextBlock
            {
                Text = Subtitle,
                Foreground = ZeroWpfTheme.TextSecondary,
                FontSize = 11.5,
                Margin = new Thickness(0, 2, 0, 0),
                Visibility = string.IsNullOrEmpty(Subtitle) ? Visibility.Collapsed : Visibility.Visible
            };
            titleStack.Children.Add(_subtitleBlock);
            headerDock.Children.Add(titleStack);

            headerBorder.Child = headerDock;
            Grid.SetRow(headerBorder, 0);
            innerGrid.Children.Add(headerBorder);

            // Body Presenter
            _contentPresenter = new ContentPresenter
            {
                Margin = new Thickness(16),
                Content = ContentElement
            };
            Grid.SetRow(_contentPresenter, 1);
            innerGrid.Children.Add(_contentPresenter);

            _drawerPanel.Child = innerGrid;
            _rootGrid.Children.Add(_drawerPanel);

            AddVisualChild(_rootGrid);
        }

        private void UpdatePanelLayout()
        {
            if (_drawerPanel == null) return;

            switch (Edge)
            {
                case DrawerEdge.Right:
                    _drawerPanel.HorizontalAlignment = HorizontalAlignment.Right;
                    _drawerPanel.VerticalAlignment = VerticalAlignment.Stretch;
                    _drawerPanel.Width = DrawerWidth;
                    _drawerPanel.Height = double.NaN;
                    _drawerPanel.BorderThickness = new Thickness(1, 0, 0, 0);
                    break;

                case DrawerEdge.Left:
                    _drawerPanel.HorizontalAlignment = HorizontalAlignment.Left;
                    _drawerPanel.VerticalAlignment = VerticalAlignment.Stretch;
                    _drawerPanel.Width = DrawerWidth;
                    _drawerPanel.Height = double.NaN;
                    _drawerPanel.BorderThickness = new Thickness(0, 0, 1, 0);
                    break;

                case DrawerEdge.Top:
                    _drawerPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                    _drawerPanel.VerticalAlignment = VerticalAlignment.Top;
                    _drawerPanel.Width = double.NaN;
                    _drawerPanel.Height = DrawerHeight;
                    _drawerPanel.BorderThickness = new Thickness(0, 0, 0, 1);
                    break;

                case DrawerEdge.Bottom:
                    _drawerPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                    _drawerPanel.VerticalAlignment = VerticalAlignment.Bottom;
                    _drawerPanel.Width = double.NaN;
                    _drawerPanel.Height = DrawerHeight;
                    _drawerPanel.BorderThickness = new Thickness(0, 1, 0, 0);
                    break;
            }
        }

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZDrawer drawer)
            {
                drawer.AnimateDrawer((bool)e.NewValue);
            }
        }

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZDrawer drawer && drawer._titleBlock != null)
            {
                drawer._titleBlock.Text = e.NewValue as string ?? string.Empty;
            }
        }

        private static void OnSubtitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZDrawer drawer && drawer._subtitleBlock != null)
            {
                var text = e.NewValue as string;
                drawer._subtitleBlock.Text = text ?? string.Empty;
                drawer._subtitleBlock.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private static void OnDrawerDimensionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZDrawer drawer)
            {
                drawer.UpdatePanelLayout();
            }
        }

        private static void OnContentElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZDrawer drawer && drawer._contentPresenter != null)
            {
                drawer._contentPresenter.Content = e.NewValue;
            }
        }

        private void AnimateDrawer(bool open)
        {
            if (_drawerPanel == null || _translateTransform == null || _scrimBorder == null) return;

            _isAnimating = true;
            var duration = TimeSpan.FromMilliseconds(220);
            var ease = new QuarticEase { EasingMode = EasingMode.EaseOut };

            // Transient GPU bitmap cache during sliding to accelerate DropShadowEffect at 120 FPS
            _drawerPanel.CacheMode = new BitmapCache { RenderAtScale = 1.0, EnableClearType = false };

            if (open)
            {
                Visibility = Visibility.Visible;
                double initialOffset = GetClosedOffset();
                SetTransformOffset(initialOffset);

                // Scrim opacity 0 -> 1
                var scrimAnim = new DoubleAnimation(0.0, 1.0, duration) { EasingFunction = ease };
                Timeline.SetDesiredFrameRate(scrimAnim, 120);
                _scrimBorder.BeginAnimation(UIElement.OpacityProperty, scrimAnim);

                // Drawer translation -> 0
                var prop = (Edge == DrawerEdge.Right || Edge == DrawerEdge.Left)
                    ? TranslateTransform.XProperty
                    : TranslateTransform.YProperty;

                var slideAnim = new DoubleAnimation(initialOffset, 0.0, duration) { EasingFunction = ease };
                Timeline.SetDesiredFrameRate(slideAnim, 120);
                slideAnim.Completed += (s, e) =>
                {
                    _isAnimating = false;
                    _drawerPanel.CacheMode = null; // Restore crisp ClearType rendering when stationary
                    Focus();
                    Opened?.Invoke(this, EventArgs.Empty);
                };
                _translateTransform.BeginAnimation(prop, slideAnim);
            }
            else
            {
                double targetOffset = GetClosedOffset();

                // Scrim opacity 1 -> 0
                var scrimAnim = new DoubleAnimation(1.0, 0.0, duration) { EasingFunction = ease };
                Timeline.SetDesiredFrameRate(scrimAnim, 120);
                _scrimBorder.BeginAnimation(UIElement.OpacityProperty, scrimAnim);

                // Drawer translation -> targetOffset
                var prop = (Edge == DrawerEdge.Right || Edge == DrawerEdge.Left)
                    ? TranslateTransform.XProperty
                    : TranslateTransform.YProperty;

                var slideAnim = new DoubleAnimation(targetOffset, duration) { EasingFunction = ease };
                Timeline.SetDesiredFrameRate(slideAnim, 120);
                slideAnim.Completed += (s, e) =>
                {
                    _isAnimating = false;
                    _drawerPanel.CacheMode = null; // Restore ClearType
                    Visibility = Visibility.Collapsed;
                    Closed?.Invoke(this, EventArgs.Empty);
                };
                _translateTransform.BeginAnimation(prop, slideAnim);
            }
        }

        private double GetClosedOffset()
        {
            return Edge switch
            {
                DrawerEdge.Right => Math.Max(100.0, DrawerWidth),
                DrawerEdge.Left => -Math.Max(100.0, DrawerWidth),
                DrawerEdge.Bottom => Math.Max(100.0, DrawerHeight),
                DrawerEdge.Top => -Math.Max(100.0, DrawerHeight),
                _ => DrawerWidth
            };
        }

        private void SetTransformOffset(double offset)
        {
            if (_translateTransform == null) return;
            if (Edge == DrawerEdge.Right || Edge == DrawerEdge.Left)
            {
                _translateTransform.X = offset;
                _translateTransform.Y = 0;
            }
            else
            {
                _translateTransform.X = 0;
                _translateTransform.Y = offset;
            }
        }

        protected override void OnThemeChanged()
        {
            base.OnThemeChanged();
            if (_drawerPanel != null)
            {
                _drawerPanel.Background = ZeroWpfTheme.BgCard;
                _drawerPanel.BorderBrush = ZeroWpfTheme.BorderDefault;
            }
            if (_titleBlock != null)
            {
                _titleBlock.Foreground = ZeroWpfTheme.TextPrimary;
            }
            if (_subtitleBlock != null)
            {
                _subtitleBlock.Foreground = ZeroWpfTheme.TextSecondary;
            }
        }

        #region Visual Children Overrides

        protected override int VisualChildrenCount => _rootGrid != null ? 1 : 0;

        protected override Visual GetVisualChild(int index)
        {
            if (_rootGrid == null || index != 0) throw new ArgumentOutOfRangeException(nameof(index));
            return _rootGrid;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (_rootGrid != null)
            {
                _rootGrid.Measure(constraint);
                return _rootGrid.DesiredSize;
            }
            return base.MeasureOverride(constraint);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            if (_rootGrid != null)
            {
                _rootGrid.Arrange(new Rect(arrangeBounds));
            }
            return arrangeBounds;
        }

        #endregion

        #region Public Static Helpers

        /// <summary>
        /// Instantly displays a drawer overlay over the specified container panel.
        /// </summary>
        public static ZDrawer Show(Panel container, object content, string title = "Detail Inspector", double width = 420, DrawerEdge edge = DrawerEdge.Right)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));

            var drawer = new ZDrawer
            {
                Title = title,
                DrawerWidth = width,
                Edge = edge,
                ContentElement = content
            };

            container.Children.Add(drawer);
            drawer.Closed += (s, e) =>
            {
                container.Children.Remove(drawer);
            };

            drawer.IsOpen = true;
            return drawer;
        }

        #endregion
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZDrawer"/>.
    /// </summary>
    [Obsolete("DrawerControl is deprecated and will be removed in 5 release cycles. Please migrate to ZDrawer instead.")]
    public class DrawerControl : ZDrawer
    {
        static DrawerControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DrawerControl), new FrameworkPropertyMetadata(typeof(ZDrawer)));
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZDrawer"/>.
    /// </summary>
    [Obsolete("Drawer is deprecated and will be removed in 5 release cycles. Please migrate to ZDrawer instead.")]
    public class Drawer : ZDrawer
    {
        static Drawer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Drawer), new FrameworkPropertyMetadata(typeof(ZDrawer)));
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZDrawer"/>.
    /// </summary>
    [Obsolete("ZeroDrawer is deprecated and will be removed in 5 release cycles. Please migrate to ZDrawer instead.")]
    public class ZeroDrawer : ZDrawer
    {
        static ZeroDrawer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZeroDrawer), new FrameworkPropertyMetadata(typeof(ZDrawer)));
        }
    }
}
