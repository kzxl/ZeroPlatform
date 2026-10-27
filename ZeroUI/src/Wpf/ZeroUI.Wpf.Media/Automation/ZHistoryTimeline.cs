using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    /// Represents an entry in the <see cref="ZHistoryTimeline"/>.
    /// </summary>
    public class HistoryTimelineItemModel : INotifyPropertyChanged
    {
        private int _index;
        private string _title = "";
        private string _timeText = "";
        private bool _isActive;
        private bool _isFuture;
        private ImageSource? _thumbnail;
        private object? _tag;

        public int Index
        {
            get => _index;
            set { _index = value; OnPropertyChanged(nameof(Index)); }
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(nameof(Title)); }
        }

        public string TimeText
        {
            get => _timeText;
            set { _timeText = value; OnPropertyChanged(nameof(TimeText)); }
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
                    OnPropertyChanged(nameof(MarkerColor));
                    OnPropertyChanged(nameof(TextColor));
                }
            }
        }

        public bool IsFuture
        {
            get => _isFuture;
            set
            {
                if (_isFuture != value)
                {
                    _isFuture = value;
                    OnPropertyChanged(nameof(IsFuture));
                    OnPropertyChanged(nameof(MarkerColor));
                    OnPropertyChanged(nameof(TextColor));
                }
            }
        }

        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set { _thumbnail = value; OnPropertyChanged(nameof(Thumbnail)); }
        }

        public object? Tag
        {
            get => _tag;
            set { _tag = value; OnPropertyChanged(nameof(Tag)); }
        }

        public Brush MarkerColor => IsActive
            ? ZeroWpfTheme.PrimaryAccent
            : (IsFuture ? ZeroWpfTheme.TextMuted : ZeroWpfTheme.TextSecondary);

        public Brush TextColor => IsActive
            ? ZeroWpfTheme.TextPrimary
            : (IsFuture ? ZeroWpfTheme.TextMuted : ZeroWpfTheme.TextSecondary);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public override string ToString() => $"[{Index}] {Title} ({TimeText})";
    }

    /// <summary>
    /// Represents a saved named snapshot state in <see cref="ZHistoryTimeline"/>.
    /// </summary>
    public class HistorySnapshotItemModel : INotifyPropertyChanged
    {
        private string _name = "";
        private string _timeText = "";
        private object? _tag;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public string TimeText
        {
            get => _timeText;
            set { _timeText = value; OnPropertyChanged(nameof(TimeText)); }
        }

        public object? Tag
        {
            get => _tag;
            set { _tag = value; OnPropertyChanged(nameof(Tag)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public override string ToString() => $"{Name} ({TimeText})";
    }

    /// <summary>
    /// Visual Undo/Redo Timeline and Snapshot history control for ZeroUI in WPF.
    /// Provides linear step history with thumbnails, active marker indicators,
    /// named snapshot bookmarks, and instant time-travel state rollback.
    /// </summary>
    public class ZHistoryTimeline : Control, IZeroEditor
    {
        private readonly ObservableCollection<HistoryTimelineItemModel> _steps = new ObservableCollection<HistoryTimelineItemModel>();
        private readonly ObservableCollection<HistorySnapshotItemModel> _snapshots = new ObservableCollection<HistorySnapshotItemModel>();

        private ListBox? _stepsListBox;
        private ListBox? _snapshotsListBox;
        private TextBlock? _txtEmpty;
        private Border? _headerBorder;
        private Border? _snapshotsBorder;
        private bool _isModified;

        #region Dependency Properties

        public static readonly DependencyProperty HeaderTitleProperty =
            DependencyProperty.Register(nameof(HeaderTitle), typeof(string), typeof(ZHistoryTimeline),
                new PropertyMetadata("HISTORY"));

        public static readonly DependencyProperty EmptyMessageProperty =
            DependencyProperty.Register(nameof(EmptyMessage), typeof(string), typeof(ZHistoryTimeline),
                new PropertyMetadata("No history entries yet."));

        public static readonly DependencyProperty CurrentIndexProperty =
            DependencyProperty.Register(nameof(CurrentIndex), typeof(int), typeof(ZHistoryTimeline),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCurrentIndexChanged));

        public static readonly DependencyProperty ShowSnapshotsProperty =
            DependencyProperty.Register(nameof(ShowSnapshots), typeof(bool), typeof(ZHistoryTimeline),
                new PropertyMetadata(true, OnShowSnapshotsChanged));

        public static readonly DependencyProperty ShowThumbnailsProperty =
            DependencyProperty.Register(nameof(ShowThumbnails), typeof(bool), typeof(ZHistoryTimeline),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZHistoryTimeline),
                new PropertyMetadata(false));

        #endregion

        #region Properties & Events

        public string HeaderTitle
        {
            get => (string)GetValue(HeaderTitleProperty);
            set => SetValue(HeaderTitleProperty, value);
        }

        public string EmptyMessage
        {
            get => (string)GetValue(EmptyMessageProperty);
            set => SetValue(EmptyMessageProperty, value);
        }

        public int CurrentIndex
        {
            get => (int)GetValue(CurrentIndexProperty);
            set => SetValue(CurrentIndexProperty, value);
        }

        public bool ShowSnapshots
        {
            get => (bool)GetValue(ShowSnapshotsProperty);
            set => SetValue(ShowSnapshotsProperty, value);
        }

        public bool ShowThumbnails
        {
            get => (bool)GetValue(ShowThumbnailsProperty);
            set => SetValue(ShowThumbnailsProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public ObservableCollection<HistoryTimelineItemModel> Steps => _steps;
        public ObservableCollection<HistorySnapshotItemModel> Snapshots => _snapshots;

        public event EventHandler<int>? StepSelected;
        public event EventHandler? UndoRequested;
        public event EventHandler? RedoRequested;
        public event EventHandler? ClearRequested;
        public event EventHandler? SnapshotSaved;
        public event EventHandler<string>? SnapshotSelected;
        public event EventHandler<string>? SnapshotDeleted;

        #endregion

        #region IZeroEditor Implementation

        public object? EditValue
        {
            get => CurrentIndex;
            set
            {
                if (value is int idx) CurrentIndex = idx;
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
            CurrentIndex = 0;
            _isModified = false;
        }

        public void Clear()
        {
            _steps.Clear();
            _snapshots.Clear();
            Reset();
        }

        #endregion

        static ZHistoryTimeline()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZHistoryTimeline), new FrameworkPropertyMetadata(typeof(ZHistoryTimeline)));
        }

        public ZHistoryTimeline()
        {
            Focusable = true;
            _steps.CollectionChanged += (_, _) => UpdateEmptyState();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            BuildVisualTree();
        }

        private static void OnCurrentIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZHistoryTimeline ctrl)
            {
                int newIdx = (int)e.NewValue;
                foreach (var step in ctrl._steps)
                {
                    step.IsActive = step.Index == newIdx;
                    step.IsFuture = step.Index > newIdx;
                }
                ctrl.EditValueChanged?.Invoke(ctrl, EventArgs.Empty);
            }
        }

        private static void OnShowSnapshotsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZHistoryTimeline ctrl && ctrl._snapshotsBorder != null)
            {
                ctrl._snapshotsBorder.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void BuildVisualTree()
        {
            var dock = new DockPanel { LastChildFill = true };

            // 1. Top Header (Title + Undo/Redo/Clear)
            _headerBorder = new Border
            {
                Padding = new Thickness(10, 8, 10, 8),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            DockPanel.SetDock(_headerBorder, Dock.Top);

            var headerDock = new DockPanel { LastChildFill = true };
            var txtTitle = new TextBlock
            {
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            txtTitle.SetBinding(TextBlock.TextProperty, new Binding(nameof(HeaderTitle)) { Source = this });
            DockPanel.SetDock(txtTitle, Dock.Left);
            headerDock.Children.Add(txtTitle);

            var btnStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(btnStack, Dock.Right);

            var btnUndo = CreateHeaderButton("↶", "Undo (Ctrl+Z)", (_, _) => UndoRequested?.Invoke(this, EventArgs.Empty));
            var btnRedo = CreateHeaderButton("↷", "Redo (Ctrl+Y)", (_, _) => RedoRequested?.Invoke(this, EventArgs.Empty));
            var btnClear = CreateHeaderButton("✕", "Clear all history", (_, _) => ClearRequested?.Invoke(this, EventArgs.Empty));

            btnStack.Children.Add(btnUndo);
            btnStack.Children.Add(btnRedo);
            btnStack.Children.Add(btnClear);
            headerDock.Children.Add(btnStack);

            _headerBorder.Child = headerDock;
            dock.Children.Add(_headerBorder);

            // 2. Bottom Snapshots Section
            _snapshotsBorder = new Border
            {
                MaxHeight = 180,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            DockPanel.SetDock(_snapshotsBorder, Dock.Bottom);

            var snapDock = new DockPanel { LastChildFill = true };
            var snapHeaderBorder = new Border
            {
                Padding = new Thickness(10, 6, 10, 6)
            };
            DockPanel.SetDock(snapHeaderBorder, Dock.Top);

            var snapHeaderDock = new DockPanel { LastChildFill = true };
            var txtSnapTitle = new TextBlock
            {
                Text = "SNAPSHOTS",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(txtSnapTitle, Dock.Left);
            snapHeaderDock.Children.Add(txtSnapTitle);

            var btnAddSnapshot = new Button
            {
                Content = "+ Save",
                Height = 22,
                Padding = new Thickness(8, 2, 8, 2),
                FontSize = 10,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            btnAddSnapshot.Click += (_, _) => SnapshotSaved?.Invoke(this, EventArgs.Empty);
            DockPanel.SetDock(btnAddSnapshot, Dock.Right);
            snapHeaderDock.Children.Add(btnAddSnapshot);

            snapHeaderBorder.Child = snapHeaderDock;
            snapDock.Children.Add(snapHeaderBorder);

            _snapshotsListBox = new ListBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ItemsSource = _snapshots
            };
            _snapshotsListBox.ItemTemplate = CreateSnapshotItemTemplate();
            snapDock.Children.Add(_snapshotsListBox);

            _snapshotsBorder.Child = snapDock;
            dock.Children.Add(_snapshotsBorder);

            // 3. Middle Steps Timeline List
            var centerGrid = new Grid();

            _stepsListBox = new ListBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ItemsSource = _steps
            };
            _stepsListBox.ItemTemplate = CreateStepItemTemplate();
            _stepsListBox.SelectionChanged += (s, e) =>
            {
                if (_stepsListBox.SelectedItem is HistoryTimelineItemModel selected)
                {
                    CurrentIndex = selected.Index;
                    _isModified = true;
                    StepSelected?.Invoke(this, selected.Index);
                }
            };
            centerGrid.Children.Add(_stepsListBox);

            _txtEmpty = new TextBlock
            {
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(12, 24, 12, 0),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Visible
            };
            _txtEmpty.SetBinding(TextBlock.TextProperty, new Binding(nameof(EmptyMessage)) { Source = this });
            centerGrid.Children.Add(_txtEmpty);

            dock.Children.Add(centerGrid);

            AttachVisualChild(dock);
            ApplyTheme();
        }

        private static Button CreateHeaderButton(string text, string toolTip, RoutedEventHandler handler)
        {
            var btn = new Button
            {
                Content = text,
                Width = 22,
                Height = 22,
                Margin = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0),
                ToolTip = toolTip
            };
            btn.Click += handler;
            return btn;
        }

        private DataTemplate CreateStepItemTemplate()
        {
            var dt = new DataTemplate();
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(8, 4, 8, 4));
            borderFactory.SetValue(Border.MarginProperty, new Thickness(0, 1, 0, 1));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            borderFactory.SetValue(FrameworkElement.CursorProperty, Cursors.Hand);

            var dockFactory = new FrameworkElementFactory(typeof(DockPanel));
            dockFactory.SetValue(DockPanel.LastChildFillProperty, true);

            // Left marker indicator
            var markerFactory = new FrameworkElementFactory(typeof(TextBlock));
            markerFactory.SetValue(DockPanel.DockProperty, Dock.Left);
            markerFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            markerFactory.SetValue(FrameworkElement.WidthProperty, 16.0);
            markerFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            markerFactory.SetBinding(TextBlock.TextProperty, new Binding("Index")
            {
                Converter = new StepMarkerConverter()
            });
            markerFactory.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(HistoryTimelineItemModel.MarkerColor)));
            dockFactory.AppendChild(markerFactory);

            // Thumbnail
            var thumbBorderFactory = new FrameworkElementFactory(typeof(Border));
            thumbBorderFactory.SetValue(DockPanel.DockProperty, Dock.Left);
            thumbBorderFactory.SetValue(FrameworkElement.WidthProperty, 44.0);
            thumbBorderFactory.SetValue(FrameworkElement.HeightProperty, 33.0);
            thumbBorderFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
            thumbBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
            thumbBorderFactory.SetValue(Border.BackgroundProperty, ZeroWpfTheme.BgInput);

            var imgFactory = new FrameworkElementFactory(typeof(Image));
            imgFactory.SetValue(Image.StretchProperty, Stretch.Uniform);
            imgFactory.SetBinding(Image.SourceProperty, new Binding(nameof(HistoryTimelineItemModel.Thumbnail)));
            thumbBorderFactory.AppendChild(imgFactory);
            dockFactory.AppendChild(thumbBorderFactory);

            // Right timestamp
            var timeFactory = new FrameworkElementFactory(typeof(TextBlock));
            timeFactory.SetValue(DockPanel.DockProperty, Dock.Right);
            timeFactory.SetValue(TextBlock.FontSizeProperty, 9.0);
            timeFactory.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.TextMuted);
            timeFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            timeFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(6, 0, 0, 0));
            timeFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(HistoryTimelineItemModel.TimeText)));
            dockFactory.AppendChild(timeFactory);

            // Title text
            var titleFactory = new FrameworkElementFactory(typeof(TextBlock));
            titleFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            titleFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            titleFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            titleFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(HistoryTimelineItemModel.Title)));
            titleFactory.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(HistoryTimelineItemModel.TextColor)));
            dockFactory.AppendChild(titleFactory);

            borderFactory.AppendChild(dockFactory);
            dt.VisualTree = borderFactory;
            return dt;
        }

        private DataTemplate CreateSnapshotItemTemplate()
        {
            var dt = new DataTemplate();
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(8, 4, 8, 4));
            borderFactory.SetValue(Border.MarginProperty, new Thickness(0, 1, 0, 1));
            borderFactory.SetValue(FrameworkElement.CursorProperty, Cursors.Hand);

            var dockFactory = new FrameworkElementFactory(typeof(DockPanel));
            dockFactory.SetValue(DockPanel.LastChildFillProperty, true);

            // Delete button
            var btnDelFactory = new FrameworkElementFactory(typeof(Button));
            btnDelFactory.SetValue(DockPanel.DockProperty, Dock.Right);
            btnDelFactory.SetValue(ContentControl.ContentProperty, "✕");
            btnDelFactory.SetValue(FrameworkElement.WidthProperty, 18.0);
            btnDelFactory.SetValue(FrameworkElement.HeightProperty, 18.0);
            btnDelFactory.SetValue(Control.BackgroundProperty, Brushes.Transparent);
            btnDelFactory.SetValue(Control.ForegroundProperty, ZeroWpfTheme.TextMuted);
            btnDelFactory.SetValue(Control.BorderThicknessProperty, new Thickness(0));
            btnDelFactory.SetValue(FrameworkElement.CursorProperty, Cursors.Hand);
            btnDelFactory.SetValue(FrameworkElement.ToolTipProperty, "Delete snapshot");
            btnDelFactory.SetBinding(FrameworkElement.TagProperty, new Binding(nameof(HistorySnapshotItemModel.Name)));
            btnDelFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, _) =>
            {
                if (s is Button btn && btn.Tag is string snapName)
                {
                    SnapshotDeleted?.Invoke(this, snapName);
                }
            }));
            dockFactory.AppendChild(btnDelFactory);

            // Timestamp
            var timeFactory = new FrameworkElementFactory(typeof(TextBlock));
            timeFactory.SetValue(DockPanel.DockProperty, Dock.Right);
            timeFactory.SetValue(TextBlock.FontSizeProperty, 9.0);
            timeFactory.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.TextMuted);
            timeFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            timeFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 6, 0));
            timeFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(HistorySnapshotItemModel.TimeText)));
            dockFactory.AppendChild(timeFactory);

            // Snapshot Name
            var nameFactory = new FrameworkElementFactory(typeof(TextBlock));
            nameFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            nameFactory.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.TextPrimary);
            nameFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            nameFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            nameFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(HistorySnapshotItemModel.Name)));
            dockFactory.AppendChild(nameFactory);

            borderFactory.AppendChild(dockFactory);
            borderFactory.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler((s, e) =>
            {
                if (s is FrameworkElement fe && fe.DataContext is HistorySnapshotItemModel item)
                {
                    SnapshotSelected?.Invoke(this, item.Name);
                }
            }));

            dt.VisualTree = borderFactory;
            return dt;
        }

        private void UpdateEmptyState()
        {
            if (_txtEmpty != null)
            {
                _txtEmpty.Visibility = _steps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ApplyTheme()
        {
            Background = ZeroWpfTheme.BgCard;
            Foreground = ZeroWpfTheme.TextPrimary;

            if (_headerBorder != null)
            {
                _headerBorder.Background = ZeroWpfTheme.BgInput;
                _headerBorder.BorderBrush = ZeroWpfTheme.BorderSubtle;
            }
            if (_snapshotsBorder != null)
            {
                _snapshotsBorder.Background = ZeroWpfTheme.BgCard;
                _snapshotsBorder.BorderBrush = ZeroWpfTheme.BorderSubtle;
            }
            if (_txtEmpty != null)
            {
                _txtEmpty.Foreground = ZeroWpfTheme.TextMuted;
            }
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

        private class StepMarkerConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                if (value is int idx)
                {
                    return idx == 0 ? "○" : "●";
                }
                return "·";
            }

            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
                throw new NotImplementedException();
        }
    
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
    /// Legacy alias for <see cref="ZHistoryTimeline"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("HistoryTimelineControl is deprecated. Use ZHistoryTimeline instead.")]
    public class HistoryTimelineControl : ZHistoryTimeline
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZHistoryTimeline"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroHistoryTimeline is deprecated. Use ZHistoryTimeline instead.")]
    public class ZeroHistoryTimeline : ZHistoryTimeline
    {
    }
    #endregion
}
