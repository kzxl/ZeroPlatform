using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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
    /// Status states for a task in <see cref="ZBatchTaskQueue"/>.
    /// </summary>
    public enum BatchTaskStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Canceled,
        Paused
    }

    /// <summary>
    /// Represents an individual task item in <see cref="ZBatchTaskQueue"/>.
    /// </summary>
    public class BatchTaskItemModel : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString("N");
        private string _displayName = "";
        private int _progress;
        private BatchTaskStatus _status = BatchTaskStatus.Pending;
        private string _statusText = "";
        private string _statusGlyph = "○";
        private Brush _statusBrush = Brushes.Gray;
        private string? _error;
        private object? _tag;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(nameof(DisplayName)); }
        }

        public int Progress
        {
            get => _progress;
            set
            {
                int clamped = value < 0 ? 0 : (value > 100 ? 100 : value);
                if (_progress != clamped)
                {
                    _progress = clamped;
                    OnPropertyChanged(nameof(Progress));
                }
            }
        }

        public BatchTaskStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    UpdateStatusVisuals();
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
        }

        public string StatusGlyph
        {
            get => _statusGlyph;
            private set { _statusGlyph = value; OnPropertyChanged(nameof(StatusGlyph)); }
        }

        public Brush StatusBrush
        {
            get => _statusBrush;
            private set { _statusBrush = value; OnPropertyChanged(nameof(StatusBrush)); }
        }

        public string? Error
        {
            get => _error;
            set { _error = value; OnPropertyChanged(nameof(Error)); }
        }

        public object? Tag
        {
            get => _tag;
            set { _tag = value; OnPropertyChanged(nameof(Tag)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public BatchTaskItemModel()
        {
            UpdateStatusVisuals();
        }

        public BatchTaskItemModel(string id, string displayName)
        {
            _id = id;
            _displayName = displayName;
            UpdateStatusVisuals();
        }

        public void Update(string displayName, int progress, BatchTaskStatus status, string? error = null)
        {
            DisplayName = displayName;
            Progress = progress;
            Error = error;
            Status = status;

            StatusText = status switch
            {
                BatchTaskStatus.Pending => "queued",
                BatchTaskStatus.Running => $"{Progress}%",
                BatchTaskStatus.Completed => "done",
                BatchTaskStatus.Failed => !string.IsNullOrEmpty(error) ? error! : "failed",
                BatchTaskStatus.Canceled => "canceled",
                BatchTaskStatus.Paused => "paused",
                _ => ""
            };
        }

        private void UpdateStatusVisuals()
        {
            switch (_status)
            {
                case BatchTaskStatus.Running:
                    StatusGlyph = "●";
                    StatusBrush = Brushes.DodgerBlue;
                    break;
                case BatchTaskStatus.Completed:
                    StatusGlyph = "✓";
                    StatusBrush = Brushes.LimeGreen;
                    break;
                case BatchTaskStatus.Failed:
                    StatusGlyph = "✗";
                    StatusBrush = Brushes.IndianRed;
                    break;
                case BatchTaskStatus.Canceled:
                    StatusGlyph = "✕";
                    StatusBrush = ZeroWpfTheme.TextMuted;
                    break;
                case BatchTaskStatus.Pending:
                    StatusGlyph = "○";
                    StatusBrush = ZeroWpfTheme.TextMuted;
                    break;
                case BatchTaskStatus.Paused:
                    StatusGlyph = "⏸";
                    StatusBrush = Brushes.Goldenrod;
                    break;
                default:
                    StatusGlyph = "·";
                    StatusBrush = ZeroWpfTheme.TextMuted;
                    break;
            }
        }
    }

    /// <summary>
    /// Production-grade Batch Task Queue control for ZeroUI in WPF.
    /// Manages queued, running, completed, and failed batch tasks with parallel concurrency selection,
    /// pause/resume orchestration, retry/cancel item actions, and live visual progress bars.
    /// </summary>
    public class ZBatchTaskQueue : Control, IZeroEditor
    {
        private readonly ObservableCollection<BatchTaskItemModel> _tasks = new ObservableCollection<BatchTaskItemModel>();

        private UIElement? _child;
        private Border? _headerBorder;
        private TextBlock? _txtHeaderTitle;
        private TextBlock? _lblParallel;
        private ComboBox? _cmbParallel;
        private Button? _btnPause;
        private Button? _btnClear;
        private ListBox? _taskListBox;
        private TextBlock? _txtEmpty;
        private bool _isModified;
        private bool _suppressSelectionEvent;

        #region Dependency Properties

        public static readonly DependencyProperty HeaderTitleProperty =
            DependencyProperty.Register(nameof(HeaderTitle), typeof(string), typeof(ZBatchTaskQueue),
                new PropertyMetadata("BATCH QUEUE", OnHeaderTitleChanged));

        public static readonly DependencyProperty MaxParallelProperty =
            DependencyProperty.Register(nameof(MaxParallel), typeof(int), typeof(ZBatchTaskQueue),
                new PropertyMetadata(1, OnMaxParallelChanged));

        public static readonly DependencyProperty IsPausedProperty =
            DependencyProperty.Register(nameof(IsPaused), typeof(bool), typeof(ZBatchTaskQueue),
                new PropertyMetadata(false, OnIsPausedChanged));

        public static readonly DependencyProperty EmptyMessageProperty =
            DependencyProperty.Register(nameof(EmptyMessage), typeof(string), typeof(ZBatchTaskQueue),
                new PropertyMetadata("Queue is empty.", OnEmptyMessageChanged));

        public static readonly DependencyProperty ParallelLabelProperty =
            DependencyProperty.Register(nameof(ParallelLabel), typeof(string), typeof(ZBatchTaskQueue),
                new PropertyMetadata("Parallel:", OnParallelLabelChanged));

        public static readonly DependencyProperty ConcurrencyOptionsProperty =
            DependencyProperty.Register(nameof(ConcurrencyOptions), typeof(int[]), typeof(ZBatchTaskQueue),
                new PropertyMetadata(new[] { 1, 2, 4, 8 }, OnConcurrencyOptionsChanged));

        public static readonly DependencyProperty ShowParallelSelectorProperty =
            DependencyProperty.Register(nameof(ShowParallelSelector), typeof(bool), typeof(ZBatchTaskQueue),
                new PropertyMetadata(true, OnShowParallelSelectorChanged));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZBatchTaskQueue),
                new PropertyMetadata(false));

        public string HeaderTitle
        {
            get => (string)GetValue(HeaderTitleProperty);
            set => SetValue(HeaderTitleProperty, value);
        }

        public int MaxParallel
        {
            get => (int)GetValue(MaxParallelProperty);
            set => SetValue(MaxParallelProperty, value);
        }

        public bool IsPaused
        {
            get => (bool)GetValue(IsPausedProperty);
            set => SetValue(IsPausedProperty, value);
        }

        public string EmptyMessage
        {
            get => (string)GetValue(EmptyMessageProperty);
            set => SetValue(EmptyMessageProperty, value);
        }

        public string ParallelLabel
        {
            get => (string)GetValue(ParallelLabelProperty);
            set => SetValue(ParallelLabelProperty, value);
        }

        public int[] ConcurrencyOptions
        {
            get => (int[])GetValue(ConcurrencyOptionsProperty);
            set => SetValue(ConcurrencyOptionsProperty, value);
        }

        public bool ShowParallelSelector
        {
            get => (bool)GetValue(ShowParallelSelectorProperty);
            set => SetValue(ShowParallelSelectorProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public ObservableCollection<BatchTaskItemModel> Tasks => _tasks;

        #endregion

        #region Events

        public event EventHandler<bool>? PauseResumeClicked;
        public event EventHandler? ClearCompletedClicked;
        public event EventHandler<BatchTaskItemModel>? RetryTaskClicked;
        public event EventHandler<BatchTaskItemModel>? RemoveTaskClicked;
        public event EventHandler<BatchTaskItemModel>? TaskDoubleClicked;
        public event EventHandler<int>? MaxParallelChanged;

        #endregion

        #region IZeroEditor

        public object? EditValue
        {
            get => _tasks.ToList();
            set
            {
                if (value is IEnumerable<BatchTaskItemModel> items)
                {
                    _tasks.Clear();
                    foreach (var item in items) _tasks.Add(item);
                    UpdateEmptyState();
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
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
            _tasks.Clear();
            _isModified = false;
            UpdateEmptyState();
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            _tasks.Clear();
            _isModified = false;
            UpdateEmptyState();
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        public ZBatchTaskQueue()
        {
            Focusable = true;
            _tasks.CollectionChanged += (_, _) => UpdateEmptyState();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            BuildVisualTree();
        }

        private static void OnHeaderTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZBatchTaskQueue ctrl && ctrl._txtHeaderTitle != null)
            {
                ctrl._txtHeaderTitle.Text = e.NewValue as string ?? "";
            }
        }

        private static void OnMaxParallelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZBatchTaskQueue ctrl && ctrl._cmbParallel != null)
            {
                int val = (int)e.NewValue;
                ctrl.SelectParallelComboValue(val);
                ctrl.MaxParallelChanged?.Invoke(ctrl, val);
            }
        }

        private static void OnIsPausedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZBatchTaskQueue ctrl && ctrl._btnPause != null)
            {
                bool isPaused = (bool)e.NewValue;
                ctrl._btnPause.Content = isPaused ? "▶" : "⏸";
                ctrl._btnPause.ToolTip = isPaused ? "Resume queue" : "Pause queue";
            }
        }

        private static void OnEmptyMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZBatchTaskQueue ctrl && ctrl._txtEmpty != null)
            {
                ctrl._txtEmpty.Text = e.NewValue as string ?? "";
            }
        }

        private static void OnParallelLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZBatchTaskQueue ctrl && ctrl._lblParallel != null)
            {
                ctrl._lblParallel.Text = e.NewValue as string ?? "";
            }
        }

        private static void OnConcurrencyOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZBatchTaskQueue ctrl)
            {
                ctrl.RebuildConcurrencyCombo();
            }
        }

        private static void OnShowParallelSelectorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZBatchTaskQueue ctrl)
            {
                bool show = (bool)e.NewValue;
                var vis = show ? Visibility.Visible : Visibility.Collapsed;
                if (ctrl._lblParallel != null) ctrl._lblParallel.Visibility = vis;
                if (ctrl._cmbParallel != null) ctrl._cmbParallel.Visibility = vis;
            }
        }

        private void BuildVisualTree()
        {
            var rootDock = new DockPanel { LastChildFill = true };

            // 1. Top Header Bar
            _headerBorder = new Border
            {
                Padding = new Thickness(10, 8, 10, 8),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            DockPanel.SetDock(_headerBorder, Dock.Top);

            var headerDock = new DockPanel { LastChildFill = true };
            _txtHeaderTitle = new TextBlock
            {
                Text = HeaderTitle,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_txtHeaderTitle, Dock.Left);
            headerDock.Children.Add(_txtHeaderTitle);

            var rightPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(rightPanel, Dock.Right);

            _lblParallel = new TextBlock
            {
                Text = ParallelLabel,
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            rightPanel.Children.Add(_lblParallel);

            _cmbParallel = new ComboBox
            {
                Width = 44,
                Height = 22,
                Margin = new Thickness(0, 0, 4, 0)
            };
            RebuildConcurrencyCombo();
            _cmbParallel.SelectionChanged += CmbParallel_SelectionChanged;
            rightPanel.Children.Add(_cmbParallel);

            _btnPause = new Button
            {
                Content = IsPaused ? "▶" : "⏸",
                Width = 22,
                Height = 22,
                Margin = new Thickness(2, 0, 2, 0),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = IsPaused ? "Resume queue" : "Pause queue"
            };
            _btnPause.Click += (_, _) =>
            {
                IsPaused = !IsPaused;
                PauseResumeClicked?.Invoke(this, IsPaused);
            };
            rightPanel.Children.Add(_btnPause);

            _btnClear = new Button
            {
                Content = "✕",
                Width = 22,
                Height = 22,
                Margin = new Thickness(2, 0, 0, 0),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "Clear completed tasks"
            };
            _btnClear.Click += (_, _) => ClearCompletedClicked?.Invoke(this, EventArgs.Empty);
            rightPanel.Children.Add(_btnClear);

            headerDock.Children.Add(rightPanel);
            _headerBorder.Child = headerDock;
            rootDock.Children.Add(_headerBorder);

            // 2. Main List Body + Empty State
            var centerGrid = new Grid();

            _taskListBox = new ListBox
            {
                ItemsSource = _tasks,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(_taskListBox, ScrollBarVisibility.Disabled);
            _taskListBox.ItemContainerStyle = CreateItemContainerStyle();
            _taskListBox.ItemTemplate = CreateTaskDataTemplate();
            _taskListBox.MouseDoubleClick += (s, e) =>
            {
                if (_taskListBox.SelectedItem is BatchTaskItemModel item)
                {
                    TaskDoubleClicked?.Invoke(this, item);
                }
            };
            centerGrid.Children.Add(_taskListBox);

            _txtEmpty = new TextBlock
            {
                Text = EmptyMessage,
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(12, 28, 12, 0),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Visible
            };
            centerGrid.Children.Add(_txtEmpty);

            rootDock.Children.Add(centerGrid);

            _child = rootDock;
            AddVisualChild(rootDock);
            AddLogicalChild(rootDock);

            ApplyTheme();
            UpdateEmptyState();
        }

        private void RebuildConcurrencyCombo()
        {
            if (_cmbParallel == null) return;
            _suppressSelectionEvent = true;
            try
            {
                _cmbParallel.Items.Clear();
                int[] options = ConcurrencyOptions ?? new[] { 1, 2, 4, 8 };
                foreach (var val in options)
                {
                    _cmbParallel.Items.Add(new ComboBoxItem { Content = val.ToString(CultureInfo.InvariantCulture), Tag = val });
                }
                SelectParallelComboValue(MaxParallel);
            }
            finally
            {
                _suppressSelectionEvent = false;
            }
        }

        private void CmbParallel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionEvent) return;
            if (_cmbParallel?.SelectedItem is ComboBoxItem item && item.Tag is int val)
            {
                MaxParallel = val;
            }
        }

        private void SelectParallelComboValue(int val)
        {
            if (_cmbParallel == null) return;
            _suppressSelectionEvent = true;
            try
            {
                foreach (ComboBoxItem item in _cmbParallel.Items)
                {
                    if (item.Tag is int n && n == val)
                    {
                        _cmbParallel.SelectedItem = item;
                        return;
                    }
                }
                if (_cmbParallel.Items.Count > 0 && _cmbParallel.SelectedIndex < 0)
                {
                    _cmbParallel.SelectedIndex = 0;
                }
            }
            finally
            {
                _suppressSelectionEvent = false;
            }
        }

        private void UpdateEmptyState()
        {
            if (_txtEmpty != null)
            {
                _txtEmpty.Visibility = _tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private Style CreateItemContainerStyle()
        {
            var style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            style.Setters.Add(new Setter(PaddingProperty, new Thickness(0)));
            style.Setters.Add(new Setter(MarginProperty, new Thickness(0, 0, 0, 1)));
            style.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(BackgroundProperty, Brushes.Transparent));
            return style;
        }

        private DataTemplate CreateTaskDataTemplate()
        {
            var template = new DataTemplate(typeof(BatchTaskItemModel));

            var factoryBorder = new FrameworkElementFactory(typeof(Border));
            factoryBorder.SetValue(Border.PaddingProperty, new Thickness(8, 6, 8, 6));
            factoryBorder.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 1));
            factoryBorder.SetResourceReference(Border.BackgroundProperty, "BgPanelBrush");
            factoryBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush_");

            var factoryGrid = new FrameworkElementFactory(typeof(Grid));

            var row0 = new FrameworkElementFactory(typeof(RowDefinition));
            row0.SetValue(RowDefinition.HeightProperty, GridLength.Auto);
            var row1 = new FrameworkElementFactory(typeof(RowDefinition));
            row1.SetValue(RowDefinition.HeightProperty, GridLength.Auto);

            factoryGrid.AppendChild(row0);
            factoryGrid.AppendChild(row1);

            // Row 0: Top DockPanel (Glyph + Title + Action buttons)
            var factoryTopDock = new FrameworkElementFactory(typeof(DockPanel));
            factoryTopDock.SetValue(Grid.RowProperty, 0);

            // Status Glyph
            var factoryGlyph = new FrameworkElementFactory(typeof(TextBlock));
            factoryGlyph.SetValue(DockPanel.DockProperty, Dock.Left);
            factoryGlyph.SetValue(TextBlock.WidthProperty, 16.0);
            factoryGlyph.SetValue(TextBlock.FontSizeProperty, 11.0);
            factoryGlyph.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            factoryGlyph.SetBinding(TextBlock.TextProperty, new Binding(nameof(BatchTaskItemModel.StatusGlyph)));
            factoryGlyph.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(BatchTaskItemModel.StatusBrush)));
            factoryTopDock.AppendChild(factoryGlyph);

            // Action Buttons
            var factoryActionStack = new FrameworkElementFactory(typeof(StackPanel));
            factoryActionStack.SetValue(DockPanel.DockProperty, Dock.Right);
            factoryActionStack.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            // Retry Button
            var factoryBtnRetry = new FrameworkElementFactory(typeof(Button));
            factoryBtnRetry.SetValue(Button.ContentProperty, "↺");
            factoryBtnRetry.SetValue(Button.WidthProperty, 18.0);
            factoryBtnRetry.SetValue(Button.HeightProperty, 18.0);
            factoryBtnRetry.SetValue(Button.MarginProperty, new Thickness(2, 0, 2, 0));
            factoryBtnRetry.SetValue(Button.BackgroundProperty, Brushes.Transparent);
            factoryBtnRetry.SetValue(Button.BorderThicknessProperty, new Thickness(0));
            factoryBtnRetry.SetValue(Button.CursorProperty, Cursors.Hand);
            factoryBtnRetry.SetValue(Button.ToolTipProperty, "Retry task");
            factoryBtnRetry.SetResourceReference(Button.ForegroundProperty, "TextSecondaryBrush");
            factoryBtnRetry.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, e) =>
            {
                if (s is FrameworkElement fe && fe.DataContext is BatchTaskItemModel model)
                {
                    RetryTaskClicked?.Invoke(this, model);
                }
            }));
            factoryActionStack.AppendChild(factoryBtnRetry);

            // Cancel / Remove Button
            var factoryBtnRemove = new FrameworkElementFactory(typeof(Button));
            factoryBtnRemove.SetValue(Button.ContentProperty, "✕");
            factoryBtnRemove.SetValue(Button.WidthProperty, 18.0);
            factoryBtnRemove.SetValue(Button.HeightProperty, 18.0);
            factoryBtnRemove.SetValue(Button.MarginProperty, new Thickness(2, 0, 0, 0));
            factoryBtnRemove.SetValue(Button.BackgroundProperty, Brushes.Transparent);
            factoryBtnRemove.SetValue(Button.BorderThicknessProperty, new Thickness(0));
            factoryBtnRemove.SetValue(Button.CursorProperty, Cursors.Hand);
            factoryBtnRemove.SetValue(Button.ToolTipProperty, "Cancel / Remove task");
            factoryBtnRemove.SetResourceReference(Button.ForegroundProperty, "TextSecondaryBrush");
            factoryBtnRemove.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, e) =>
            {
                if (s is FrameworkElement fe && fe.DataContext is BatchTaskItemModel model)
                {
                    RemoveTaskClicked?.Invoke(this, model);
                }
            }));
            factoryActionStack.AppendChild(factoryBtnRemove);

            factoryTopDock.AppendChild(factoryActionStack);

            // Display Name
            var factoryTitle = new FrameworkElementFactory(typeof(TextBlock));
            factoryTitle.SetValue(TextBlock.FontSizeProperty, 11.0);
            factoryTitle.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            factoryTitle.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            factoryTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            factoryTitle.SetBinding(TextBlock.TextProperty, new Binding(nameof(BatchTaskItemModel.DisplayName)));
            factoryTopDock.AppendChild(factoryTitle);

            factoryGrid.AppendChild(factoryTopDock);

            // Row 1: Progress bar + Status description text
            var factoryProgressGrid = new FrameworkElementFactory(typeof(Grid));
            factoryProgressGrid.SetValue(Grid.RowProperty, 1);
            factoryProgressGrid.SetValue(Grid.MarginProperty, new Thickness(16, 4, 0, 0));

            var factoryProgressBar = new FrameworkElementFactory(typeof(ProgressBar));
            factoryProgressBar.SetValue(ProgressBar.HeightProperty, 6.0);
            factoryProgressBar.SetValue(ProgressBar.MaximumProperty, 100.0);
            factoryProgressBar.SetValue(ProgressBar.VerticalAlignmentProperty, VerticalAlignment.Center);
            factoryProgressBar.SetBinding(ProgressBar.ValueProperty, new Binding(nameof(BatchTaskItemModel.Progress)));
            factoryProgressGrid.AppendChild(factoryProgressBar);

            var factoryStatusText = new FrameworkElementFactory(typeof(TextBlock));
            factoryStatusText.SetValue(TextBlock.FontSizeProperty, 9.0);
            factoryStatusText.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            factoryStatusText.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Top);
            factoryStatusText.SetValue(TextBlock.MarginProperty, new Thickness(0, 8, 0, 0));
            factoryStatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextDimBrush");
            factoryStatusText.SetBinding(TextBlock.TextProperty, new Binding(nameof(BatchTaskItemModel.StatusText)));
            factoryProgressGrid.AppendChild(factoryStatusText);

            factoryGrid.AppendChild(factoryProgressGrid);
            factoryBorder.AppendChild(factoryGrid);

            template.VisualTree = factoryBorder;
            return template;
        }

        public void ApplyTheme()
        {
            Background = ZeroWpfTheme.BgCard;
            Foreground = ZeroWpfTheme.TextPrimary;

            if (_headerBorder != null)
            {
                _headerBorder.Background = ZeroWpfTheme.BgInput;
                _headerBorder.BorderBrush = ZeroWpfTheme.BorderSubtle;
            }

            if (_txtHeaderTitle != null) _txtHeaderTitle.Foreground = ZeroWpfTheme.TextSecondary;
            if (_lblParallel != null) _lblParallel.Foreground = ZeroWpfTheme.TextSecondary;
            if (_btnPause != null)
            {
                _btnPause.Background = ZeroWpfTheme.BgHover;
                _btnPause.Foreground = ZeroWpfTheme.TextPrimary;
            }
            if (_btnClear != null)
            {
                _btnClear.Background = ZeroWpfTheme.BgHover;
                _btnClear.Foreground = ZeroWpfTheme.TextPrimary;
            }
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
    /// Legacy alias for <see cref="ZBatchTaskQueue"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("BatchTaskQueueControl is deprecated. Use ZBatchTaskQueue instead.")]
    public class BatchTaskQueueControl : ZBatchTaskQueue
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZBatchTaskQueue"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroBatchTaskQueue is deprecated. Use ZBatchTaskQueue instead.")]
    public class ZeroBatchTaskQueue : ZBatchTaskQueue
    {
    }
    #endregion
}
