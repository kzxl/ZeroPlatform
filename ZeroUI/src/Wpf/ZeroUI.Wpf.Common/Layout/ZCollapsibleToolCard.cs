using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Theme;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Rendering.Optimizer;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Layout
{
    /// <summary>
    /// High-performance collapsible tool card container designed for studio workflows (ZStack, ZVision).
    /// Features GPU elevation, customizable icon/header, accordion expand/collapse toggling, and zero-allocation visual hierarchy.
    /// </summary>
    public class ZCollapsibleToolCard : HeaderedContentControl, IZeroSkinnable
    {
        private Border? _rootBorder;
        private Button? _headerButton;
        private TextBlock? _arrowBlock;
        private Border? _contentHost;

        #region Dependency Properties

        public static readonly DependencyProperty HeaderGlyphProperty =
            DependencyProperty.Register(nameof(HeaderGlyph), typeof(string), typeof(ZCollapsibleToolCard),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(ZCollapsibleToolCard),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsMeasure, OnIsExpandedChanged));

        public static readonly DependencyProperty ElevationProperty =
            DependencyProperty.Register(nameof(Elevation), typeof(int), typeof(ZCollapsibleToolCard),
                new PropertyMetadata(1, OnElevationChanged));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(ZCollapsibleToolCard),
                new PropertyMetadata(new CornerRadius(8), OnCornerRadiusChanged));

        public static readonly DependencyProperty HeaderBackgroundProperty =
            DependencyProperty.Register(nameof(HeaderBackground), typeof(Brush), typeof(ZCollapsibleToolCard),
                new PropertyMetadata(null));

        public static readonly DependencyProperty AccentBrushProperty =
            DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(ZCollapsibleToolCard),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ToggleCommandProperty =
            DependencyProperty.Register(nameof(ToggleCommand), typeof(ICommand), typeof(ZCollapsibleToolCard),
                new PropertyMetadata(null));

        #endregion

        #region Events

        public static readonly RoutedEvent ExpandedEvent =
            EventManager.RegisterRoutedEvent(nameof(Expanded), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ZCollapsibleToolCard));

        public static readonly RoutedEvent CollapsedEvent =
            EventManager.RegisterRoutedEvent(nameof(Collapsed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ZCollapsibleToolCard));

        public event RoutedEventHandler Expanded
        {
            add => AddHandler(ExpandedEvent, value);
            remove => RemoveHandler(ExpandedEvent, value);
        }

        public event RoutedEventHandler Collapsed
        {
            add => AddHandler(CollapsedEvent, value);
            remove => RemoveHandler(CollapsedEvent, value);
        }

        #endregion

        #region Properties

        public string HeaderGlyph
        {
            get => (string)GetValue(HeaderGlyphProperty);
            set => SetValue(HeaderGlyphProperty, value);
        }

        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        public int Elevation
        {
            get => (int)GetValue(ElevationProperty);
            set => SetValue(ElevationProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public Brush? HeaderBackground
        {
            get => (Brush?)GetValue(HeaderBackgroundProperty);
            set => SetValue(HeaderBackgroundProperty, value);
        }

        public Brush? AccentBrush
        {
            get => (Brush?)GetValue(AccentBrushProperty);
            set => SetValue(AccentBrushProperty, value);
        }

        public ICommand? ToggleCommand
        {
            get => (ICommand?)GetValue(ToggleCommandProperty);
            set => SetValue(ToggleCommandProperty, value);
        }

        public bool UseDefaultSkin { get; set; } = true;

        public ZeroSkin? CustomSkin { get; set; }

        public ZeroSkin EffectiveSkin => CustomSkin ?? ZeroSkinManager.CurrentSkin;

        #endregion

        static ZCollapsibleToolCard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZCollapsibleToolCard), new FrameworkPropertyMetadata(typeof(ZCollapsibleToolCard)));
        }

        public ZCollapsibleToolCard()
        {
            SetResourceReference(BackgroundProperty, "ZeroUI.BgCard");
            SetResourceReference(BorderBrushProperty, "ZeroUI.BorderDefault");
            BorderThickness = new Thickness(1);
            Loaded += (s, e) => BuildVisualTree();
        }

        private void BuildVisualTree()
        {
            if (_rootBorder != null) return;

            var defaultAccent = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
            defaultAccent.Freeze();

            var defaultHeaderBg = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x2B));
            defaultHeaderBg.Freeze();

            var hoverHeaderBg = new SolidColorBrush(Color.FromRgb(0x1F, 0x26, 0x3D));
            hoverHeaderBg.Freeze();

            _rootBorder = new Border
            {
                CornerRadius = CornerRadius,
                BorderThickness = BorderThickness,
                Background = Background ?? ZeroWpfTheme.BgCard,
                BorderBrush = BorderBrush ?? ZeroWpfTheme.BorderDefault,
                Margin = Margin
            };
            RenderOptimizer.SetElevation(_rootBorder, Elevation);

            var stack = new StackPanel();

            // Header Button
            _headerButton = new Button
            {
                Background = HeaderBackground ?? defaultHeaderBg,
                Foreground = AccentBrush ?? defaultAccent,
                BorderBrush = BorderBrush ?? ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(14, 10, 14, 10),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            // Custom ControlTemplate for Header Button
            var btnTemplate = new ControlTemplate(typeof(Button));
            var btnBorderFactory = new FrameworkElementFactory(typeof(Border), "btnBorder");
            btnBorderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
            btnBorderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(BorderBrushProperty));
            btnBorderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(BorderThicknessProperty));
            btnBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(Math.Max(0, CornerRadius.TopLeft - 1), Math.Max(0, CornerRadius.TopRight - 1), 0, 0));

            var cpFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            cpFactory.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(PaddingProperty));
            btnBorderFactory.AppendChild(cpFactory);
            btnTemplate.VisualTree = btnBorderFactory;

            var mouseOverTrigger = new Trigger { Property = IsMouseOverProperty, Value = true };
            mouseOverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverHeaderBg, "btnBorder"));
            btnTemplate.Triggers.Add(mouseOverTrigger);
            _headerButton.Template = btnTemplate;

            // Header Layout Grid
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (!string.IsNullOrEmpty(HeaderGlyph))
            {
                titleStack.Children.Add(new TextBlock
                {
                    Text = HeaderGlyph,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            var titleBlock = new TextBlock
            {
                Text = Header?.ToString() ?? string.Empty,
                FontWeight = FontWeights.Bold,
                Foreground = AccentBrush ?? defaultAccent,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(titleBlock);
            Grid.SetColumn(titleStack, 0);
            headerGrid.Children.Add(titleStack);

            _arrowBlock = new TextBlock
            {
                Text = IsExpanded ? "▼" : "▲",
                Foreground = ZeroWpfTheme.TextMuted,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_arrowBlock, 1);
            headerGrid.Children.Add(_arrowBlock);

            _headerButton.Content = headerGrid;
            _headerButton.Click += HeaderButton_Click;
            stack.Children.Add(_headerButton);

            // Content Container
            if (Content is UIElement contentElement)
            {
                RemoveLogicalChild(contentElement);
            }

            _contentHost = new Border
            {
                Visibility = IsExpanded ? Visibility.Visible : Visibility.Collapsed,
                Child = Content as UIElement
            };
            stack.Children.Add(_contentHost);

            _rootBorder.Child = stack;
            AddVisualChild(_rootBorder);
        }

        protected override int VisualChildrenCount => _rootBorder != null ? 1 : base.VisualChildrenCount;

        protected override Visual GetVisualChild(int index)
        {
            if (_rootBorder != null && index == 0) return _rootBorder;
            return base.GetVisualChild(index);
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (_rootBorder != null)
            {
                _rootBorder.Measure(constraint);
                return _rootBorder.DesiredSize;
            }
            return base.MeasureOverride(constraint);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _rootBorder?.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        protected override void OnContentChanged(object oldContent, object newContent)
        {
            base.OnContentChanged(oldContent, newContent);

            if (newContent is UIElement newElement)
            {
                RemoveLogicalChild(newElement);
            }

            if (_contentHost != null)
            {
                _contentHost.Child = newContent as UIElement;
            }
        }

        private void HeaderButton_Click(object sender, RoutedEventArgs e)
        {
            IsExpanded = !IsExpanded;
            if (ToggleCommand?.CanExecute(null) == true)
            {
                ToggleCommand.Execute(null);
            }
        }

        private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZCollapsibleToolCard card)
            {
                bool expanded = (bool)e.NewValue;
                if (card._arrowBlock != null)
                {
                    card._arrowBlock.Text = expanded ? "▼" : "▲";
                }
                if (card._contentHost != null)
                {
                    card._contentHost.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
                }

                card.RaiseEvent(new RoutedEventArgs(expanded ? ExpandedEvent : CollapsedEvent, card));
            }
        }

        private static void OnElevationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZCollapsibleToolCard card && card._rootBorder != null)
            {
                RenderOptimizer.SetElevation(card._rootBorder, (int)e.NewValue);
            }
        }

        private static void OnCornerRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZCollapsibleToolCard card && card._rootBorder != null)
            {
                card._rootBorder.CornerRadius = (CornerRadius)e.NewValue;
            }
        }

        public void ApplySkin(ZeroSkin skin)
        {
            if (skin == null) throw new ArgumentNullException(nameof(skin));
            CustomSkin = skin;
            UseDefaultSkin = false;
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZCollapsibleToolCard"/>.
    /// </summary>
    [Obsolete("CollapsibleToolCard is deprecated and will be removed in 5 release cycles. Please migrate to ZCollapsibleToolCard instead.")]
    public class CollapsibleToolCard : ZCollapsibleToolCard
    {
        static CollapsibleToolCard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CollapsibleToolCard), new FrameworkPropertyMetadata(typeof(ZCollapsibleToolCard)));
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZCollapsibleToolCard"/>.
    /// </summary>
    [Obsolete("ZeroCollapsibleToolCard is deprecated and will be removed in 5 release cycles. Please migrate to ZCollapsibleToolCard instead.")]
    public class ZeroCollapsibleToolCard : ZCollapsibleToolCard
    {
        static ZeroCollapsibleToolCard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZeroCollapsibleToolCard), new FrameworkPropertyMetadata(typeof(ZCollapsibleToolCard)));
        }
    }
}
