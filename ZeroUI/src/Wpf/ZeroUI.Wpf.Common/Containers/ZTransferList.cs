using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Containers
{
    /// <summary>
    /// Dual-list transfer control for WPF for moving items between Available and Selected collections,
    /// complete with batch transfer buttons (&gt;, &lt;, &gt;&gt;, &lt;&lt;) and dynamic theme integration.
    /// </summary>
    public class ZTransferList : Control
    {
        private readonly ObservableCollection<object> _availableItems = new ObservableCollection<object>();
        private readonly ObservableCollection<object> _selectedItems = new ObservableCollection<object>();

        private readonly ListBox _leftList;
        private readonly ListBox _rightList;
        private readonly TextBlock _leftHeader;
        private readonly TextBlock _rightHeader;
        private readonly Button _btnRight;
        private readonly Button _btnAllRight;
        private readonly Button _btnLeft;
        private readonly Button _btnAllLeft;
        private readonly Border _rootBorder;

        public event EventHandler? ItemsTransferred;

        public object AvailableItems
        {
            get => _availableItems;
            set
            {
                _availableItems.Clear();
                if (value is IEnumerable enumerable and not string)
                {
                    foreach (var item in enumerable)
                    {
                        if (item != null) _availableItems.Add(item);
                    }
                }
            }
        }

        public object SelectedItems
        {
            get => _selectedItems;
            set
            {
                _selectedItems.Clear();
                if (value is IEnumerable enumerable and not string)
                {
                    foreach (var item in enumerable)
                    {
                        if (item != null) _selectedItems.Add(item);
                    }
                }
            }
        }

        public string AvailableTitle
        {
            get => _leftHeader.Text;
            set => _leftHeader.Text = value;
        }

        public string SelectedTitle
        {
            get => _rightHeader.Text;
            set => _rightHeader.Text = value;
        }

        static ZTransferList()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZTransferList), new FrameworkPropertyMetadata(typeof(ZTransferList)));
        }

        public ZTransferList()
        {
            Width = 420;
            Height = 240;

            _leftHeader = new TextBlock
            {
                Text = "Available",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };

            _rightHeader = new TextBlock
            {
                Text = "Selected",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };

            _leftList = new ListBox
            {
                ItemsSource = _availableItems,
                SelectionMode = SelectionMode.Extended
            };

            _rightList = new ListBox
            {
                ItemsSource = _selectedItems,
                SelectionMode = SelectionMode.Extended
            };

            _btnRight = CreateButton(">");
            _btnAllRight = CreateButton(">>");
            _btnLeft = CreateButton("<");
            _btnAllLeft = CreateButton("<<");

            _btnRight.Click += (s, e) => TransferRight();
            _btnAllRight.Click += (s, e) => TransferAllRight();
            _btnLeft.Click += (s, e) => TransferLeft();
            _btnAllLeft.Click += (s, e) => TransferAllLeft();

            var btnStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0)
            };
            btnStack.Children.Add(_btnRight);
            btnStack.Children.Add(_btnAllRight);
            btnStack.Children.Add(_btnLeft);
            btnStack.Children.Add(_btnAllLeft);

            var leftDock = new DockPanel();
            DockPanel.SetDock(_leftHeader, Dock.Top);
            leftDock.Children.Add(_leftHeader);
            leftDock.Children.Add(_leftList);

            var rightDock = new DockPanel();
            DockPanel.SetDock(_rightHeader, Dock.Top);
            rightDock.Children.Add(_rightHeader);
            rightDock.Children.Add(_rightList);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(leftDock, 0);
            Grid.SetColumn(btnStack, 1);
            Grid.SetColumn(rightDock, 2);

            grid.Children.Add(leftDock);
            grid.Children.Add(btnStack);
            grid.Children.Add(rightDock);

            _rootBorder = new Border
            {
                Child = grid,
                Padding = new Thickness(4)
            };

            AddVisualChild(_rootBorder);
            AddLogicalChild(_rootBorder);

            Loaded += (s, e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            Unloaded += (s, e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;

            ApplyTheme();
        }

        private Button CreateButton(string text)
        {
            return new Button
            {
                Content = text,
                Width = 36,
                Height = 28,
                Margin = new Thickness(0, 3, 0, 3),
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
        }

        public void TransferRight()
        {
            var selected = _leftList.SelectedItems.Cast<object>().ToList();
            if (selected.Count == 0) return;

            foreach (var item in selected)
            {
                _availableItems.Remove(item);
                _selectedItems.Add(item);
            }
            ItemsTransferred?.Invoke(this, EventArgs.Empty);
        }

        public void TransferAllRight()
        {
            if (_availableItems.Count == 0) return;

            var items = _availableItems.ToList();
            _availableItems.Clear();
            foreach (var item in items)
            {
                _selectedItems.Add(item);
            }
            ItemsTransferred?.Invoke(this, EventArgs.Empty);
        }

        public void TransferLeft()
        {
            var selected = _rightList.SelectedItems.Cast<object>().ToList();
            if (selected.Count == 0) return;

            foreach (var item in selected)
            {
                _selectedItems.Remove(item);
                _availableItems.Add(item);
            }
            ItemsTransferred?.Invoke(this, EventArgs.Empty);
        }

        public void TransferAllLeft()
        {
            if (_selectedItems.Count == 0) return;

            var items = _selectedItems.ToList();
            _selectedItems.Clear();
            foreach (var item in items)
            {
                _availableItems.Add(item);
            }
            ItemsTransferred?.Invoke(this, EventArgs.Empty);
        }

        private void OnThemeChanged()
        {
            if (!Dispatcher.CheckAccess())
            {
                if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                    Dispatcher.BeginInvoke((Action)OnThemeChanged);
                return;
            }
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            _leftHeader.Foreground = ZeroWpfTheme.TextPrimary;
            _rightHeader.Foreground = ZeroWpfTheme.TextPrimary;
            _leftList.Background = ZeroWpfTheme.BgInput;
            _leftList.Foreground = ZeroWpfTheme.TextPrimary;
            _leftList.BorderBrush = ZeroWpfTheme.BorderPen.Brush;

            _rightList.Background = ZeroWpfTheme.BgInput;
            _rightList.Foreground = ZeroWpfTheme.TextPrimary;
            _rightList.BorderBrush = ZeroWpfTheme.BorderPen.Brush;
        }

        protected override int VisualChildrenCount => _rootBorder != null ? 1 : 0;
        protected override Visual GetVisualChild(int index) => _rootBorder ?? throw new ArgumentOutOfRangeException(nameof(index));

        protected override Size MeasureOverride(Size constraint)
        {
            _rootBorder.Measure(constraint);
            return _rootBorder.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _rootBorder.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }
    }

    [Obsolete("ZeroTransferList is deprecated and will be removed in 5 release cycles. Please migrate to ZTransferList instead.")]
    public class ZeroTransferList : ZTransferList { }
}
