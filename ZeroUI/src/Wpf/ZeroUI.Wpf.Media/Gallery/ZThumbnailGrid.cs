using System;
using System.Collections;
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
    /// Represents an individual photo or asset card in <see cref="ZThumbnailGrid"/>.
    /// </summary>
    public class ThumbnailGridItemModel : INotifyPropertyChanged
    {
        private string _id = "";
        private string _fileName = "";
        private string _imagePath = "";
        private ImageSource? _thumbnail;
        private int _rating;
        private string _pickDisplay = "";
        private Brush? _labelBrush;
        private bool _isSelected;
        private bool _isActive;
        private bool _isEdited;
        private string _badgeText = "";
        private bool _isBadgeVisible;
        private object? _tag;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string FileName
        {
            get => _fileName;
            set { _fileName = value; OnPropertyChanged(nameof(FileName)); }
        }

        public string ImagePath
        {
            get => _imagePath;
            set { _imagePath = value; OnPropertyChanged(nameof(ImagePath)); }
        }

        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set { _thumbnail = value; OnPropertyChanged(nameof(Thumbnail)); OnPropertyChanged(nameof(Thumb)); }
        }

        public ImageSource? Thumb
        {
            get => _thumbnail;
            set => Thumbnail = value;
        }

        public int Rating
        {
            get => _rating;
            set
            {
                if (_rating != value)
                {
                    _rating = value;
                    OnPropertyChanged(nameof(Rating));
                    OnPropertyChanged(nameof(RatingDisplay));
                }
            }
        }

        public string RatingDisplay => _rating > 0 ? new string('★', _rating) : "";

        public string PickDisplay
        {
            get => _pickDisplay;
            set { _pickDisplay = value; OnPropertyChanged(nameof(PickDisplay)); }
        }

        public Brush? LabelBrush
        {
            get => _labelBrush;
            set { _labelBrush = value; OnPropertyChanged(nameof(LabelBrush)); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                    OnPropertyChanged(nameof(BorderBrush));
                }
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged(nameof(IsActive));
                    OnPropertyChanged(nameof(BorderBrush));
                }
            }
        }

        public bool IsEdited
        {
            get => _isEdited;
            set { _isEdited = value; OnPropertyChanged(nameof(IsEdited)); }
        }

        public string BadgeText
        {
            get => _badgeText;
            set
            {
                _badgeText = value;
                OnPropertyChanged(nameof(BadgeText));
                OnPropertyChanged(nameof(VirtualCopyBadge));
                IsBadgeVisible = !string.IsNullOrEmpty(value);
            }
        }

        public string VirtualCopyBadge => BadgeText;

        public bool IsBadgeVisible
        {
            get => _isBadgeVisible;
            set
            {
                _isBadgeVisible = value;
                OnPropertyChanged(nameof(IsBadgeVisible));
                OnPropertyChanged(nameof(IsVirtualCopy));
            }
        }

        public bool IsVirtualCopy => IsBadgeVisible;

        public object? Tag
        {
            get => _tag;
            set { _tag = value; OnPropertyChanged(nameof(Tag)); }
        }

        public Brush BorderBrush
        {
            get
            {
                if (IsActive) return ZeroWpfTheme.SecondaryAccent ?? Brushes.DarkOrange;
                if (IsSelected) return ZeroWpfTheme.PrimaryAccent ?? Brushes.DodgerBlue;
                return ZeroWpfTheme.BorderSubtle ?? Brushes.Transparent;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public ThumbnailGridItemModel() { }

        public ThumbnailGridItemModel(string id, string fileName, string path)
        {
            _id = id;
            _fileName = fileName;
            _imagePath = path;
        }

        public override string ToString() => $"{FileName} ({Id})";
    }

    /// <summary>
    /// Event arguments for click events on <see cref="ZThumbnailGrid"/>.
    /// </summary>
    public class ThumbnailGridItemClickEventArgs : EventArgs
    {
        public object Item { get; }
        public bool IsControlDown { get; }
        public bool IsShiftDown { get; }

        public ThumbnailGridItemClickEventArgs(object item, bool ctrl, bool shift)
        {
            Item = item;
            IsControlDown = ctrl;
            IsShiftDown = shift;
        }
    }

    /// <summary>
    /// Production-grade Lightroom-style Light Table Thumbnail Grid control for ZeroUI in WPF.
    /// Provides responsive wrap-grid layout, multi-selection (Ctrl/Shift range), rating stars,
    /// color label badges, pick flags, overlay badges, and theme-aware rendering.
    /// </summary>
    public class ZThumbnailGrid : Control, IZeroEditor
    {
        private readonly ObservableCollection<ThumbnailGridItemModel> _items = new ObservableCollection<ThumbnailGridItemModel>();
        private UIElement? _child;
        private ScrollViewer? _scroller;
        private ItemsControl? _itemsControl;
        private TextBlock? _txtEmpty;
        private bool _isModified;

        #region Dependency Properties

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ZThumbnailGrid),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty ThumbnailWidthProperty =
            DependencyProperty.Register(nameof(ThumbnailWidth), typeof(double), typeof(ZThumbnailGrid),
                new PropertyMetadata(130.0));

        public static readonly DependencyProperty ThumbnailHeightProperty =
            DependencyProperty.Register(nameof(ThumbnailHeight), typeof(double), typeof(ZThumbnailGrid),
                new PropertyMetadata(140.0));

        public static readonly DependencyProperty AllowMultiSelectProperty =
            DependencyProperty.Register(nameof(AllowMultiSelect), typeof(bool), typeof(ZThumbnailGrid),
                new PropertyMetadata(true));

        public static readonly DependencyProperty EmptyMessageProperty =
            DependencyProperty.Register(nameof(EmptyMessage), typeof(string), typeof(ZThumbnailGrid),
                new PropertyMetadata("No items to display.", OnEmptyMessageChanged));

        public static readonly DependencyProperty CardTemplateProperty =
            DependencyProperty.Register(nameof(CardTemplate), typeof(DataTemplate), typeof(ZThumbnailGrid),
                new PropertyMetadata(null, OnCardTemplateChanged));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZThumbnailGrid),
                new PropertyMetadata(false));

        public IEnumerable? ItemsSource
        {
            get => (IEnumerable?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public DataTemplate? CardTemplate
        {
            get => (DataTemplate?)GetValue(CardTemplateProperty);
            set => SetValue(CardTemplateProperty, value);
        }

        public double ThumbnailWidth
        {
            get => (double)GetValue(ThumbnailWidthProperty);
            set => SetValue(ThumbnailWidthProperty, value);
        }

        public double ThumbnailHeight
        {
            get => (double)GetValue(ThumbnailHeightProperty);
            set => SetValue(ThumbnailHeightProperty, value);
        }

        public bool AllowMultiSelect
        {
            get => (bool)GetValue(AllowMultiSelectProperty);
            set => SetValue(AllowMultiSelectProperty, value);
        }

        public string EmptyMessage
        {
            get => (string)GetValue(EmptyMessageProperty);
            set => SetValue(EmptyMessageProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public ObservableCollection<ThumbnailGridItemModel> Items => _items;

        public ThumbnailGridItemModel? ActiveItem => _items.FirstOrDefault(i => i.IsActive);

        public IEnumerable<ThumbnailGridItemModel> SelectedItems => _items.Where(i => i.IsSelected);

        #endregion

        #region Events

        public event EventHandler<ThumbnailGridItemClickEventArgs>? ItemClicked;
        public event EventHandler<object>? ItemRightClicked;
        public event EventHandler<object>? ItemDoubleClicked;
        public event EventHandler<ThumbnailGridItemModel?>? ActiveItemChanged;
        public event EventHandler? SelectionChanged;

        #endregion

        #region IZeroEditor Implementation

        public object? EditValue
        {
            get => ActiveItem?.Id;
            set
            {
                if (value is string id)
                {
                    SetActive(id);
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
            ClearSelection();
            _isModified = false;
        }

        public void Clear()
        {
            _items.Clear();
            _isModified = false;
            UpdateEmptyState();
        }

        #endregion

        public ZThumbnailGrid()
        {
            Focusable = true;
            _items.CollectionChanged += (_, _) => UpdateEmptyState();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            BuildVisualTree();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZThumbnailGrid ctrl && ctrl._itemsControl != null)
            {
                ctrl._itemsControl.ItemsSource = e.NewValue as IEnumerable ?? ctrl._items;
                ctrl.UpdateEmptyState();
            }
        }

        private static void OnEmptyMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZThumbnailGrid ctrl && ctrl._txtEmpty != null)
            {
                ctrl._txtEmpty.Text = e.NewValue as string ?? "";
            }
        }

        private static void OnCardTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZThumbnailGrid ctrl && ctrl._itemsControl != null)
            {
                ctrl._itemsControl.ItemTemplate = e.NewValue as DataTemplate ?? ctrl.CreateThumbnailTemplate();
            }
        }

        private void BuildVisualTree()
        {
            var grid = new Grid();

            _scroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                CanContentScroll = false
            };

            _itemsControl = new ItemsControl
            {
                ItemsSource = ItemsSource ?? _items,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };

            var panelFactory = new FrameworkElementFactory(typeof(WrapPanel));
            panelFactory.SetValue(WrapPanel.OrientationProperty, Orientation.Horizontal);
            panelFactory.SetValue(WrapPanel.IsItemsHostProperty, true);
            _itemsControl.ItemsPanel = new ItemsPanelTemplate(panelFactory);

            _itemsControl.ItemTemplate = CardTemplate ?? CreateThumbnailTemplate();

            _scroller.Content = _itemsControl;
            grid.Children.Add(_scroller);

            _txtEmpty = new TextBlock
            {
                Text = EmptyMessage,
                FontSize = 11,
                Foreground = ZeroWpfTheme.TextMuted,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(16),
                Visibility = Visibility.Visible
            };
            grid.Children.Add(_txtEmpty);

            _child = grid;
            AddVisualChild(grid);
            AddLogicalChild(grid);

            ApplyTheme();
            UpdateEmptyState();
        }

        private DataTemplate CreateThumbnailTemplate()
        {
            var template = new DataTemplate();

            var factoryBorder = new FrameworkElementFactory(typeof(Border));
            factoryBorder.SetValue(Border.WidthProperty, ThumbnailWidth);
            factoryBorder.SetValue(Border.HeightProperty, ThumbnailHeight);
            factoryBorder.SetValue(Border.MarginProperty, new Thickness(4));
            factoryBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            factoryBorder.SetValue(Border.BorderThicknessProperty, new Thickness(2));
            factoryBorder.SetValue(Border.CursorProperty, Cursors.Hand);
            factoryBorder.SetValue(Border.BackgroundProperty, ZeroWpfTheme.BgInput);

            var borderStyle = new Style(typeof(Border));
            borderStyle.Setters.Add(new Setter(Border.BorderBrushProperty, ZeroWpfTheme.BorderSubtle ?? Brushes.Transparent));

            var triggerSelected = new DataTrigger { Binding = new Binding("IsSelected"), Value = true };
            triggerSelected.Setters.Add(new Setter(Border.BorderBrushProperty, ZeroWpfTheme.PrimaryAccent ?? Brushes.DodgerBlue));
            borderStyle.Triggers.Add(triggerSelected);

            var triggerActive = new DataTrigger { Binding = new Binding("IsActive"), Value = true };
            triggerActive.Setters.Add(new Setter(Border.BorderBrushProperty, ZeroWpfTheme.SecondaryAccent ?? Brushes.DarkOrange));
            borderStyle.Triggers.Add(triggerActive);

            factoryBorder.SetValue(Border.StyleProperty, borderStyle);

            // Left Click
            factoryBorder.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler((s, e) =>
            {
                if (s is FrameworkElement fe && fe.DataContext != null)
                {
                    bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                    bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

                    if (ItemClicked != null)
                    {
                        ItemClicked.Invoke(this, new ThumbnailGridItemClickEventArgs(fe.DataContext, ctrl, shift));
                    }
                    else if (fe.DataContext is ThumbnailGridItemModel model)
                    {
                        HandleDefaultClick(model, ctrl, shift);
                    }
                }
            }));

            // Right Click
            factoryBorder.AddHandler(UIElement.MouseRightButtonUpEvent, new MouseButtonEventHandler((s, e) =>
            {
                if (s is FrameworkElement fe && fe.DataContext != null)
                {
                    ItemRightClicked?.Invoke(this, fe.DataContext);
                }
            }));

            // Double Click
            factoryBorder.AddHandler(Control.MouseDoubleClickEvent, new MouseButtonEventHandler((s, e) =>
            {
                if (s is FrameworkElement fe && fe.DataContext != null)
                {
                    ItemDoubleClicked?.Invoke(this, fe.DataContext);
                }
            }));

            var factoryGrid = new FrameworkElementFactory(typeof(Grid));

            // Image Thumbnail (bind Thumb)
            var factoryImg = new FrameworkElementFactory(typeof(Image));
            factoryImg.SetValue(Image.StretchProperty, Stretch.Uniform);
            factoryImg.SetValue(Image.MarginProperty, new Thickness(3));
            factoryImg.SetBinding(Image.SourceProperty, new Binding("Thumb"));
            factoryGrid.AppendChild(factoryImg);

            // Color Label Badge (top-left circle)
            var factoryColorDot = new FrameworkElementFactory(typeof(Border));
            factoryColorDot.SetValue(Border.WidthProperty, 10.0);
            factoryColorDot.SetValue(Border.HeightProperty, 10.0);
            factoryColorDot.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            factoryColorDot.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            factoryColorDot.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Top);
            factoryColorDot.SetValue(Border.MarginProperty, new Thickness(6));
            factoryColorDot.SetBinding(Border.BackgroundProperty, new Binding("LabelBrush"));
            factoryGrid.AppendChild(factoryColorDot);

            // Pick Flag (top-right text)
            var factoryPick = new FrameworkElementFactory(typeof(TextBlock));
            factoryPick.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            factoryPick.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            factoryPick.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Top);
            factoryPick.SetValue(TextBlock.MarginProperty, new Thickness(0, 4, 6, 0));
            factoryPick.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.SuccessAccent ?? Brushes.LimeGreen);
            factoryPick.SetBinding(TextBlock.TextProperty, new Binding("PickDisplay"));
            factoryGrid.AppendChild(factoryPick);

            // Virtual Copy / Edit Badge (bottom-left badge)
            var factoryBadgeBorder = new FrameworkElementFactory(typeof(Border));
            factoryBadgeBorder.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            factoryBadgeBorder.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Bottom);
            factoryBadgeBorder.SetValue(Border.MarginProperty, new Thickness(6, 0, 0, 24));
            factoryBadgeBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
            factoryBadgeBorder.SetValue(Border.PaddingProperty, new Thickness(3, 1, 3, 1));
            factoryBadgeBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 122, 204)));

            var badgeBinding = new Binding("IsVirtualCopy")
            {
                Converter = new BooleanToVisibilityConverter()
            };
            factoryBadgeBorder.SetBinding(Border.VisibilityProperty, badgeBinding);
            factoryBadgeBorder.SetBinding(Border.ToolTipProperty, new Binding("VirtualCopyBadge"));

            var factoryBadgeText = new FrameworkElementFactory(typeof(TextBlock));
            factoryBadgeText.SetValue(TextBlock.FontSizeProperty, 9.0);
            factoryBadgeText.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            factoryBadgeText.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            factoryBadgeText.SetBinding(TextBlock.TextProperty, new Binding("VirtualCopyBadge"));
            factoryBadgeBorder.AppendChild(factoryBadgeText);
            factoryGrid.AppendChild(factoryBadgeBorder);

            // Bottom Footer Overlay (Rating + FileName)
            var factoryFooter = new FrameworkElementFactory(typeof(Border));
            factoryFooter.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Bottom);
            factoryFooter.SetValue(Border.PaddingProperty, new Thickness(4, 2, 4, 2));
            factoryFooter.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)));

            var factoryFooterDock = new FrameworkElementFactory(typeof(DockPanel));

            // Rating Stars
            var factoryRating = new FrameworkElementFactory(typeof(TextBlock));
            factoryRating.SetValue(DockPanel.DockProperty, Dock.Right);
            factoryRating.SetValue(TextBlock.FontSizeProperty, 10.0);
            factoryRating.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.WarningAccent ?? Brushes.Gold);
            factoryRating.SetBinding(TextBlock.TextProperty, new Binding("RatingDisplay"));
            factoryFooterDock.AppendChild(factoryRating);

            // FileName
            var factoryFileName = new FrameworkElementFactory(typeof(TextBlock));
            factoryFileName.SetValue(TextBlock.FontSizeProperty, 10.0);
            factoryFileName.SetValue(TextBlock.ForegroundProperty, Brushes.WhiteSmoke);
            factoryFileName.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            factoryFileName.SetBinding(TextBlock.TextProperty, new Binding("FileName"));
            factoryFooterDock.AppendChild(factoryFileName);

            factoryFooter.AppendChild(factoryFooterDock);
            factoryGrid.AppendChild(factoryFooter);

            factoryBorder.AppendChild(factoryGrid);
            template.VisualTree = factoryBorder;
            return template;
        }

        public void HandleDefaultClick(ThumbnailGridItemModel item, bool ctrl, bool shift)
        {
            if (ctrl && AllowMultiSelect)
            {
                item.IsSelected = !item.IsSelected;
                if (item.IsSelected) SetActiveInternal(item);
            }
            else if (shift && AllowMultiSelect && ActiveItem != null)
            {
                int from = _items.IndexOf(ActiveItem);
                int to = _items.IndexOf(item);
                if (from >= 0 && to >= 0)
                {
                    int start = Math.Min(from, to);
                    int count = Math.Abs(from - to) + 1;
                    for (int i = 0; i < _items.Count; i++)
                    {
                        _items[i].IsSelected = i >= start && i < start + count;
                    }
                    SetActiveInternal(item);
                }
            }
            else
            {
                foreach (var other in _items) other.IsSelected = false;
                item.IsSelected = true;
                SetActiveInternal(item);
            }

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetActive(string id)
        {
            var item = _items.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
            if (item != null) SetActiveInternal(item);
        }

        private void SetActiveInternal(ThumbnailGridItemModel item)
        {
            foreach (var other in _items) other.IsActive = other == item;
            ActiveItemChanged?.Invoke(this, item);
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetSelection(IEnumerable<string> ids)
        {
            var set = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
            foreach (var item in _items)
            {
                item.IsSelected = set.Contains(item.Id);
            }
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearSelection()
        {
            foreach (var item in _items) item.IsSelected = false;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SelectAll()
        {
            if (!AllowMultiSelect) return;
            foreach (var item in _items) item.IsSelected = true;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateEmptyState()
        {
            if (_txtEmpty != null)
            {
                bool hasItems = false;
                if (ItemsSource != null)
                {
                    var enumerator = ItemsSource.GetEnumerator();
                    hasItems = enumerator != null && enumerator.MoveNext();
                }
                else
                {
                    hasItems = _items.Count > 0;
                }
                _txtEmpty.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public void ApplyTheme()
        {
            Background = ZeroWpfTheme.BgPrimary;
            Foreground = ZeroWpfTheme.TextPrimary;

            if (_txtEmpty != null) _txtEmpty.Foreground = ZeroWpfTheme.TextMuted;
        }

        #region Visual / Logical Tree Overrides

        protected override int VisualChildrenCount => _child != null ? 1 : 0;
        protected override Visual GetVisualChild(int index) => _child ?? throw new ArgumentOutOfRangeException(nameof(index));

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
    /// Legacy alias for <see cref="ZThumbnailGrid"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ThumbnailGridControl is deprecated. Use ZThumbnailGrid instead.")]
    public class ThumbnailGridControl : ZThumbnailGrid
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZThumbnailGrid"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroThumbnailGrid is deprecated. Use ZThumbnailGrid instead.")]
    public class ZeroThumbnailGrid : ZThumbnailGrid
    {
    }
    #endregion
}
