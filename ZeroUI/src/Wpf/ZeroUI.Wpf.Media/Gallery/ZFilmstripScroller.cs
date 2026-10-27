using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Media
{
    /// <summary>
    /// Represents an item in the <see cref="ZFilmstripScroller"/>.
    /// </summary>
    public class FilmstripItemModel : INotifyPropertyChanged
    {
        private string _id = "";
        private string _title = "";
        private ImageSource? _thumbnail;
        private bool _isSelected;
        private bool _isActive;
        private bool _isEdited;
        private int _rating;
        private Brush? _labelBrush;
        private string _badgeText = "";
        private object? _tag;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(nameof(Title)); }
        }

        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set { _thumbnail = value; OnPropertyChanged(nameof(Thumbnail)); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); OnPropertyChanged(nameof(BorderBrush)); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(nameof(IsActive)); OnPropertyChanged(nameof(BorderBrush)); }
        }

        public bool IsEdited
        {
            get => _isEdited;
            set { _isEdited = value; OnPropertyChanged(nameof(IsEdited)); }
        }

        public int Rating
        {
            get => _rating;
            set { _rating = value; OnPropertyChanged(nameof(Rating)); OnPropertyChanged(nameof(RatingDisplay)); }
        }

        public string RatingDisplay => Rating > 0 ? new string('★', Rating) : "";

        public Brush? LabelBrush
        {
            get => _labelBrush;
            set { _labelBrush = value; OnPropertyChanged(nameof(LabelBrush)); }
        }

        public string BadgeText
        {
            get => _badgeText;
            set { _badgeText = value; OnPropertyChanged(nameof(BadgeText)); }
        }

        public object? Tag
        {
            get => _tag;
            set { _tag = value; OnPropertyChanged(nameof(Tag)); }
        }

        public Brush BorderBrush
        {
            get
            {
                if (IsActive) return ZeroWpfTheme.PrimaryAccent;
                if (IsSelected) return ZeroWpfTheme.SecondaryAccent;
                return ZeroWpfTheme.BorderSubtle;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public override string ToString() => $"{Title} ({Id})";
    }

    /// <summary>
    /// Event arguments for <see cref="ZFilmstripScroller.ItemClicked"/> events.
    /// </summary>
    public class FilmstripItemClickEventArgs : EventArgs
    {
        public FilmstripItemModel Item { get; }
        public bool IsControlDown { get; }
        public bool IsShiftDown { get; }

        public FilmstripItemClickEventArgs(FilmstripItemModel item, bool ctrl, bool shift)
        {
            Item = item;
            IsControlDown = ctrl;
            IsShiftDown = shift;
        }
    }

    /// <summary>
    /// Horizontal Thumbnail Filmstrip Scroller control for ZeroUI in WPF.
    /// Provides horizontal smooth scrolling, rating stars, color label flags,
    /// edit badges, selection coordination, and theme integration.
    /// </summary>
    public class ZFilmstripScroller : Control, IZeroEditor
    {
        private readonly ObservableCollection<FilmstripItemModel> _items = new ObservableCollection<FilmstripItemModel>();
        private ScrollViewer? _scroller;
        private ItemsControl? _itemsControl;
        private bool _isModified;

        #region Dependency Properties

        public static readonly DependencyProperty ItemWidthProperty =
            DependencyProperty.Register(nameof(ItemWidth), typeof(double), typeof(ZFilmstripScroller),
                new PropertyMetadata(100.0));

        public static readonly DependencyProperty ItemHeightProperty =
            DependencyProperty.Register(nameof(ItemHeight), typeof(double), typeof(ZFilmstripScroller),
                new PropertyMetadata(112.0));

        public static readonly DependencyProperty AllowMultiSelectProperty =
            DependencyProperty.Register(nameof(AllowMultiSelect), typeof(bool), typeof(ZFilmstripScroller),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZFilmstripScroller),
                new PropertyMetadata(false));

        #endregion

        #region Properties & Events

        public double ItemWidth
        {
            get => (double)GetValue(ItemWidthProperty);
            set => SetValue(ItemWidthProperty, value);
        }

        public double ItemHeight
        {
            get => (double)GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }

        public bool AllowMultiSelect
        {
            get => (bool)GetValue(AllowMultiSelectProperty);
            set => SetValue(AllowMultiSelectProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public ObservableCollection<FilmstripItemModel> Items => _items;

        public event EventHandler<FilmstripItemClickEventArgs>? ItemClicked;
        public event EventHandler<FilmstripItemModel>? ItemRightClicked;
        public event EventHandler<FilmstripItemModel?>? ActiveItemChanged;
        public event EventHandler? SelectionChanged;

        #endregion

        #region IZeroEditor Implementation

        public object? EditValue
        {
            get => _items.FirstOrDefault(i => i.IsActive)?.Id;
            set
            {
                if (value is string id)
                {
                    foreach (var itm in _items)
                    {
                        itm.IsActive = string.Equals(itm.Id, id, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }

        public event EventHandler? EditValueChanged;

        public bool IsModified
        {
            get => _isModified;
            set => _isModified = value;
        }

        public void Reset()
        {
            foreach (var itm in _items)
            {
                itm.IsSelected = false;
                itm.IsActive = false;
            }
            _isModified = false;
        }

        public void Clear()
        {
            _items.Clear();
            Reset();
        }

        #endregion

        static ZFilmstripScroller()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZFilmstripScroller), new FrameworkPropertyMetadata(typeof(ZFilmstripScroller)));
        }

        public ZFilmstripScroller()
        {
            Height = 128;
            Focusable = true;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            BuildVisualTree();
        }

        private void BuildVisualTree()
        {
            _scroller = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false
            };

            // Support mouse wheel horizontal scrolling
            _scroller.PreviewMouseWheel += (s, e) =>
            {
                if (e.Delta != 0)
                {
                    _scroller.ScrollToHorizontalOffset(_scroller.HorizontalOffset - e.Delta);
                    e.Handled = true;
                }
            };

            _itemsControl = new ItemsControl
            {
                ItemsSource = _items,
                ItemsPanel = CreateItemsPanelTemplate(),
                ItemTemplate = CreateItemTemplate()
            };

            _scroller.Content = _itemsControl;

            AttachVisualChild(_scroller);
            ApplyTheme();
        }

        private static ItemsPanelTemplate CreateItemsPanelTemplate()
        {
            var panelFactory = new FrameworkElementFactory(typeof(StackPanel));
            panelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            panelFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 4, 0));
            return new ItemsPanelTemplate(panelFactory);
        }

        private DataTemplate CreateItemTemplate()
        {
            var dt = new DataTemplate();

            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(FrameworkElement.WidthProperty, ItemWidth);
            borderFactory.SetValue(FrameworkElement.HeightProperty, ItemHeight);
            borderFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(3));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(2));
            borderFactory.SetValue(FrameworkElement.CursorProperty, Cursors.Hand);
            borderFactory.SetValue(Border.BackgroundProperty, ZeroWpfTheme.BgInput);
            borderFactory.SetBinding(Border.BorderBrushProperty, new Binding(nameof(FilmstripItemModel.BorderBrush)));

            // Left Click & Right Click handlers
            borderFactory.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler((s, e) =>
            {
                if (s is FrameworkElement fe && fe.DataContext is FilmstripItemModel item)
                {
                    bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                    bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                    _isModified = true;
                    ItemClicked?.Invoke(this, new FilmstripItemClickEventArgs(item, ctrl, shift));
                    ActiveItemChanged?.Invoke(this, item);
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }));

            borderFactory.AddHandler(UIElement.MouseRightButtonUpEvent, new MouseButtonEventHandler((s, e) =>
            {
                if (s is FrameworkElement fe && fe.DataContext is FilmstripItemModel item)
                {
                    ItemRightClicked?.Invoke(this, item);
                    e.Handled = true;
                }
            }));

            var rootGridFactory = new FrameworkElementFactory(typeof(Grid));
            var row0 = new FrameworkElementFactory(typeof(RowDefinition));
            row0.SetValue(RowDefinition.HeightProperty, new GridLength(1, GridUnitType.Star));
            var row1 = new FrameworkElementFactory(typeof(RowDefinition));
            row1.SetValue(RowDefinition.HeightProperty, GridLength.Auto);
            rootGridFactory.AppendChild(row0);
            rootGridFactory.AppendChild(row1);

            // Thumbnail Area (Row 0)
            var thumbGridFactory = new FrameworkElementFactory(typeof(Grid));
            thumbGridFactory.SetValue(Grid.RowProperty, 0);

            var imgFactory = new FrameworkElementFactory(typeof(Image));
            imgFactory.SetValue(Image.StretchProperty, Stretch.Uniform);
            imgFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
            imgFactory.SetBinding(Image.SourceProperty, new Binding(nameof(FilmstripItemModel.Thumbnail)));
            thumbGridFactory.AppendChild(imgFactory);

            // Color label indicator (top-left)
            var labelDotFactory = new FrameworkElementFactory(typeof(Border));
            labelDotFactory.SetValue(FrameworkElement.WidthProperty, 8.0);
            labelDotFactory.SetValue(FrameworkElement.HeightProperty, 8.0);
            labelDotFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            labelDotFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            labelDotFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
            labelDotFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4));
            labelDotFactory.SetBinding(Border.BackgroundProperty, new Binding(nameof(FilmstripItemModel.LabelBrush)));
            thumbGridFactory.AppendChild(labelDotFactory);

            // Edited indicator (top-right)
            var editBadgeFactory = new FrameworkElementFactory(typeof(Border));
            editBadgeFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            editBadgeFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
            editBadgeFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4));
            editBadgeFactory.SetValue(Border.BackgroundProperty, ZeroWpfTheme.PrimaryAccent);
            editBadgeFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
            editBadgeFactory.SetValue(Border.PaddingProperty, new Thickness(2, 0, 2, 0));
            editBadgeFactory.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(FilmstripItemModel.IsEdited))
            {
                Converter = new BooleanToVisibilityConverter()
            });

            var editTxtFactory = new FrameworkElementFactory(typeof(TextBlock));
            editTxtFactory.SetValue(TextBlock.TextProperty, "✎");
            editTxtFactory.SetValue(TextBlock.FontSizeProperty, 8.0);
            editTxtFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            editTxtFactory.SetValue(TextBlock.ForegroundProperty, Brushes.Black);
            editBadgeFactory.AppendChild(editTxtFactory);
            thumbGridFactory.AppendChild(editBadgeFactory);

            // Rating display (bottom-right)
            var ratingTxtFactory = new FrameworkElementFactory(typeof(TextBlock));
            ratingTxtFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            ratingTxtFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Bottom);
            ratingTxtFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 2));
            ratingTxtFactory.SetValue(TextBlock.FontSizeProperty, 9.0);
            ratingTxtFactory.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.WarningAccent);
            ratingTxtFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(FilmstripItemModel.RatingDisplay)));
            thumbGridFactory.AppendChild(ratingTxtFactory);

            rootGridFactory.AppendChild(thumbGridFactory);

            // Bottom title panel (Row 1)
            var titleBorderFactory = new FrameworkElementFactory(typeof(Border));
            titleBorderFactory.SetValue(Grid.RowProperty, 1);
            titleBorderFactory.SetValue(Border.BackgroundProperty, ZeroWpfTheme.BgCard);
            titleBorderFactory.SetValue(Border.PaddingProperty, new Thickness(4, 2, 4, 2));

            var titleTxtFactory = new FrameworkElementFactory(typeof(TextBlock));
            titleTxtFactory.SetValue(TextBlock.FontSizeProperty, 9.0);
            titleTxtFactory.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.TextSecondary);
            titleTxtFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            titleTxtFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            titleTxtFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            titleTxtFactory.SetValue(FrameworkElement.MaxWidthProperty, 90.0);
            titleTxtFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(FilmstripItemModel.Title)));
            titleBorderFactory.AppendChild(titleTxtFactory);

            rootGridFactory.AppendChild(titleBorderFactory);

            borderFactory.AppendChild(rootGridFactory);
            dt.VisualTree = borderFactory;
            return dt;
        }

        public void ScrollIntoView(FilmstripItemModel item)
        {
            if (_scroller == null) return;
            int idx = _items.IndexOf(item);
            if (idx < 0) return;
            double targetOffset = Math.Max(0, idx * (ItemWidth + 6) - _scroller.ActualWidth / 2.0 + ItemWidth / 2.0);
            _scroller.ScrollToHorizontalOffset(targetOffset);
        }

        private void ApplyTheme()
        {
            Background = ZeroWpfTheme.BgPrimary;
            Foreground = ZeroWpfTheme.TextPrimary;
        }

        #region Visual Child Overrides

        private Visual? _child;
        protected override int VisualChildrenCount => _child != null ? 1 : 0;
        protected override Visual GetVisualChild(int index) => _child ?? throw new ArgumentOutOfRangeException();

        private void AttachVisualChild(Visual visual)
        {
            _child = visual;
            base.AddVisualChild(visual);
            base.AddLogicalChild(visual);
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (_child is UIElement el)
            {
                el.Measure(constraint);
                return el.DesiredSize;
            }
            return base.MeasureOverride(constraint);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            if (_child is UIElement el)
            {
                el.Arrange(new Rect(arrangeBounds));
            }
            return arrangeBounds;
        }

        #endregion
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged += ApplyTheme;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= ApplyTheme;
    }
}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZFilmstripScroller"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("FilmstripScrollerControl is deprecated. Use ZFilmstripScroller instead.")]
    public class FilmstripScrollerControl : ZFilmstripScroller
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZFilmstripScroller"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroFilmstripScroller is deprecated. Use ZFilmstripScroller instead.")]
    public class ZeroFilmstripScroller : ZFilmstripScroller
    {
    }
    #endregion
}
