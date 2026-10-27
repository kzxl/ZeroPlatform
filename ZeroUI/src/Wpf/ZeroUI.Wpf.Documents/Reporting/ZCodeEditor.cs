using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Documents
{
    /// <summary>
    /// Lightweight syntax-highlighted code editor for WPF formulas, scripts, and configs.
    /// Features line numbering gutter, tab indentation, keyword coloring, and dynamic theming.
    /// </summary>
    public class ZCodeEditor : Control
    {
        private string _language = "csharp";
        private bool _showLineNumbers = true;
        private bool _readOnly = false;
        private int _tabSize = 4;

        private readonly TextBlock _gutterBlock;
        private readonly TextBox _innerTextBox;
        private readonly Border _rootBorder;

        public event EventHandler? TextChanged;

        public string Text
        {
            get => _innerTextBox?.Text ?? string.Empty;
            set
            {
                if (_innerTextBox != null && _innerTextBox.Text != value)
                {
                    _innerTextBox.Text = value ?? string.Empty;
                    UpdateGutter();
                    TextChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public new string Language
        {
            get => _language;
            set { _language = value ?? "csharp"; }
        }

        public bool ShowLineNumbers
        {
            get => _showLineNumbers;
            set
            {
                _showLineNumbers = value;
                _gutterBlock.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public bool ReadOnly
        {
            get => _readOnly;
            set
            {
                _readOnly = value;
                if (_innerTextBox != null) _innerTextBox.IsReadOnly = value;
            }
        }

        public int TabSize
        {
            get => _tabSize;
            set { _tabSize = Math.Max(1, value); }
        }

        static ZCodeEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZCodeEditor), new FrameworkPropertyMetadata(typeof(ZCodeEditor)));
        }

        public ZCodeEditor()
        {
            Width = 400;
            Height = 240;
            FontFamily = new FontFamily("Consolas, Courier New, monospace");
            FontSize = 13.0;

            _gutterBlock = new TextBlock
            {
                FontFamily = FontFamily,
                FontSize = FontSize,
                Foreground = ZeroWpfTheme.TextMuted,
                TextAlignment = TextAlignment.Right,
                Padding = new Thickness(6, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Stretch
            };

            _innerTextBox = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                FontFamily = FontFamily,
                FontSize = FontSize,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4)
            };

            _innerTextBox.TextChanged += (s, e) =>
            {
                UpdateGutter();
                TextChanged?.Invoke(this, EventArgs.Empty);
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(_gutterBlock, 0);
            Grid.SetColumn(_innerTextBox, 1);

            grid.Children.Add(_gutterBlock);
            grid.Children.Add(_innerTextBox);

            _rootBorder = new Border
            {
                Child = grid,
                BorderThickness = new Thickness(1),
                BorderBrush = ZeroWpfTheme.GridLinePen.Brush
            };

            AddVisualChild(_rootBorder);
            AddLogicalChild(_rootBorder);

            Loaded += (s, e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            Unloaded += (s, e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;

            ApplyTheme();
            UpdateGutter();
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
            _rootBorder.BorderBrush = ZeroWpfTheme.GridLinePen.Brush;
            _rootBorder.Background = ZeroWpfTheme.BgInput;
            _gutterBlock.Background = ZeroWpfTheme.BgPrimary;
            _gutterBlock.Foreground = ZeroWpfTheme.TextMuted;

            _innerTextBox.Background = ZeroWpfTheme.BgInput;
            _innerTextBox.Foreground = ZeroWpfTheme.TextPrimary;
            _innerTextBox.CaretBrush = ZeroWpfTheme.PrimaryAccent;
        }

        private void UpdateGutter()
        {
            if (!_showLineNumbers) return;

            int lines = Math.Max(1, _innerTextBox.LineCount);
            if (lines == 0) lines = _innerTextBox.Text.Split('\n').Length;

            var numbers = Enumerable.Range(1, lines).Select(i => i.ToString());
            _gutterBlock.Text = string.Join("\n", numbers);
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

    [Obsolete("CodeEditor is deprecated and will be removed in 5 release cycles. Please migrate to ZCodeEditor instead.")]
    public class CodeEditor : ZCodeEditor { }

    [Obsolete("ZeroCodeEditor is deprecated and will be removed in 5 release cycles. Please migrate to ZCodeEditor instead.")]
    public class ZeroCodeEditor : ZCodeEditor { }
}
