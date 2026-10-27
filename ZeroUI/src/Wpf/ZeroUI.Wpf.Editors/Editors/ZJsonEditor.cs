using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Professional WPF JSON editor with line numbering gutter, format/minify commands, real-time validation, and theme reactivity.
    /// </summary>
    public class ZJsonEditor : Control
    {
        private readonly TextBlock _gutterBlock;
        private readonly TextBox _innerTextBox;
        private readonly TextBlock _statusBlock;
        private readonly Border _rootBorder;

        #region Dependency Properties

        public static readonly DependencyProperty JsonTextProperty =
            DependencyProperty.Register(
                nameof(JsonText),
                typeof(string),
                typeof(ZJsonEditor),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnJsonTextChanged));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(
                nameof(ReadOnly),
                typeof(bool),
                typeof(ZJsonEditor),
                new PropertyMetadata(false, OnReadOnlyChanged));

        public static readonly DependencyProperty IndentSizeProperty =
            DependencyProperty.Register(
                nameof(IndentSize),
                typeof(int),
                typeof(ZJsonEditor),
                new PropertyMetadata(2));

        public static readonly DependencyProperty ShowLineNumbersProperty =
            DependencyProperty.Register(
                nameof(ShowLineNumbers),
                typeof(bool),
                typeof(ZJsonEditor),
                new PropertyMetadata(true, OnShowLineNumbersChanged));

        public static readonly DependencyProperty IsValidProperty =
            DependencyProperty.Register(
                nameof(IsValid),
                typeof(bool),
                typeof(ZJsonEditor),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ErrorMessageProperty =
            DependencyProperty.Register(
                nameof(ErrorMessage),
                typeof(string),
                typeof(ZJsonEditor),
                new PropertyMetadata(null));

        public string JsonText
        {
            get => (string)GetValue(JsonTextProperty);
            set => SetValue(JsonTextProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public int IndentSize
        {
            get => (int)GetValue(IndentSizeProperty);
            set => SetValue(IndentSizeProperty, Math.Max(1, Math.Min(8, value)));
        }

        public bool ShowLineNumbers
        {
            get => (bool)GetValue(ShowLineNumbersProperty);
            set => SetValue(ShowLineNumbersProperty, value);
        }

        public bool IsValid
        {
            get => (bool)GetValue(IsValidProperty);
            private set => SetValue(IsValidProperty, value);
        }

        public string? ErrorMessage
        {
            get => (string?)GetValue(ErrorMessageProperty);
            private set => SetValue(ErrorMessageProperty, value);
        }

        #endregion

        #region Events

        public event EventHandler? JsonChanged;
        public event EventHandler? ValidationChanged;

        #endregion

        static ZJsonEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZJsonEditor), new FrameworkPropertyMetadata(typeof(ZJsonEditor)));
        }

        public ZJsonEditor()
        {
            Width = 400;
            Height = 280;
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
                if (JsonText != _innerTextBox.Text)
                {
                    JsonText = _innerTextBox.Text;
                }
                UpdateGutter();
                ValidateJson();
                JsonChanged?.Invoke(this, EventArgs.Empty);
            };

            _statusBlock = new TextBlock
            {
                FontSize = 11.5,
                Padding = new Thickness(6, 2, 6, 2),
                Text = "✔ Valid JSON"
            };

            var statusBorder = new Border
            {
                Child = _statusBlock,
                BorderThickness = new Thickness(0, 1, 0, 0),
                BorderBrush = ZeroWpfTheme.BorderDefault
            };

            var editorGrid = new Grid();
            editorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            editorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(_gutterBlock, 0);
            Grid.SetColumn(_innerTextBox, 1);

            editorGrid.Children.Add(_gutterBlock);
            editorGrid.Children.Add(_innerTextBox);

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(editorGrid, 0);
            Grid.SetRow(statusBorder, 1);

            rootGrid.Children.Add(editorGrid);
            rootGrid.Children.Add(statusBorder);

            _rootBorder = new Border
            {
                Child = rootGrid,
                BorderThickness = new Thickness(1),
                BorderBrush = ZeroWpfTheme.BorderDefault
            };

            AddVisualChild(_rootBorder);
            AddLogicalChild(_rootBorder);

            Loaded += (s, e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            Unloaded += (s, e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;

            ApplyTheme();
            UpdateGutter();
        }

        private static void OnJsonTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZJsonEditor editor)
            {
                string newText = (string)e.NewValue ?? string.Empty;
                if (editor._innerTextBox.Text != newText)
                {
                    editor._innerTextBox.Text = newText;
                }
                editor.ValidateJson();
                editor.UpdateGutter();
            }
        }

        private static void OnReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZJsonEditor editor && e.NewValue is bool ro)
            {
                editor._innerTextBox.IsReadOnly = ro;
            }
        }

        private static void OnShowLineNumbersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZJsonEditor editor && e.NewValue is bool show)
            {
                editor._gutterBlock.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public void FormatJson()
        {
            string formatted = JsonHelper.Prettify(_innerTextBox.Text, IndentSize);
            JsonText = formatted;
        }

        public void MinifyJson()
        {
            string minified = JsonHelper.Minify(_innerTextBox.Text);
            JsonText = minified;
        }

        public bool ValidateJson()
        {
            var (valid, err) = JsonHelper.Validate(_innerTextBox.Text);
            bool changed = (IsValid != valid || ErrorMessage != err);
            IsValid = valid;
            ErrorMessage = err;

            UpdateStatusDisplay();

            if (changed)
            {
                ValidationChanged?.Invoke(this, EventArgs.Empty);
            }

            return IsValid;
        }

        private void UpdateStatusDisplay()
        {
            if (IsValid)
            {
                int lineCount = _innerTextBox.LineCount;
                _statusBlock.Text = $"✔ Valid JSON  |  Lines: {lineCount}";
                _statusBlock.Foreground = ZeroWpfTheme.SuccessAccent;
            }
            else
            {
                _statusBlock.Text = $"✖ Invalid JSON: {ErrorMessage}";
                _statusBlock.Foreground = ZeroWpfTheme.DangerAccent;
            }
        }

        private void UpdateGutter()
        {
            if (!ShowLineNumbers) return;

            int lines = Math.Max(1, _innerTextBox.LineCount);
            if (string.IsNullOrEmpty(_innerTextBox.Text)) lines = 1;

            _gutterBlock.Text = string.Join(Environment.NewLine, Enumerable.Range(1, lines));
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
            _rootBorder.Background = ZeroWpfTheme.BgInput;
            _rootBorder.BorderBrush = ZeroWpfTheme.BorderDefault;

            _innerTextBox.Background = ZeroWpfTheme.BgInput;
            _innerTextBox.Foreground = ZeroWpfTheme.TextPrimary;

            _gutterBlock.Background = ZeroWpfTheme.BgCard;
            _gutterBlock.Foreground = ZeroWpfTheme.TextMuted;

            UpdateStatusDisplay();
        }

        protected override int VisualChildrenCount => _rootBorder != null ? 1 : 0;

        protected override Visual GetVisualChild(int index)
        {
            if (index != 0 || _rootBorder == null) throw new ArgumentOutOfRangeException(nameof(index));
            return _rootBorder;
        }

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

        #region Internal JSON Helper

        internal static class JsonHelper
        {
            public static (bool isValid, string? error) Validate(string json)
            {
                if (string.IsNullOrWhiteSpace(json)) return (true, null);
                var stack = new Stack<char>();
                bool inString = false;
                bool isEscaped = false;
                var word = new StringBuilder();

                for (int i = 0; i < json.Length; i++)
                {
                    char c = json[i];
                    if (inString)
                    {
                        if (isEscaped) isEscaped = false;
                        else if (c == '\\') isEscaped = true;
                        else if (c == '"') inString = false;
                        continue;
                    }

                    if (char.IsLetter(c))
                    {
                        word.Append(c);
                        continue;
                    }

                    if (word.Length > 0)
                    {
                        string w = word.ToString();
                        word.Clear();
                        if (w != "true" && w != "false" && w != "null")
                        {
                            return (false, $"Invalid token '{w}' outside string");
                        }
                    }

                    if (c == '"')
                    {
                        inString = true;
                    }
                    else if (c == '{' || c == '[')
                    {
                        stack.Push(c);
                    }
                    else if (c == '}')
                    {
                        if (stack.Count == 0 || stack.Pop() != '{')
                            return (false, $"Mismatched '}}' at {i}");
                    }
                    else if (c == ']')
                    {
                        if (stack.Count == 0 || stack.Pop() != '[')
                            return (false, $"Mismatched ']' at {i}");
                    }
                }

                if (inString) return (false, "Unterminated string literal");
                if (word.Length > 0)
                {
                    string w = word.ToString();
                    if (w != "true" && w != "false" && w != "null")
                        return (false, $"Invalid token '{w}'");
                }
                if (stack.Count > 0) return (false, $"Unclosed '{stack.Peek()}'");
                return (true, null);
            }

            public static string Prettify(string json, int indentSize = 2)
            {
                if (string.IsNullOrWhiteSpace(json)) return json;
                var sb = new StringBuilder();
                int indent = 0;
                bool inString = false;
                bool isEscaped = false;
                string indentStr = new string(' ', Math.Max(1, indentSize));

                for (int i = 0; i < json.Length; i++)
                {
                    char c = json[i];
                    if (inString)
                    {
                        sb.Append(c);
                        if (isEscaped) isEscaped = false;
                        else if (c == '\\') isEscaped = true;
                        else if (c == '"') inString = false;
                        continue;
                    }

                    if (char.IsWhiteSpace(c)) continue;

                    if (c == '"')
                    {
                        inString = true;
                        sb.Append(c);
                    }
                    else if (c == '{' || c == '[')
                    {
                        sb.Append(c);
                        sb.AppendLine();
                        indent++;
                        for (int s = 0; s < indent; s++) sb.Append(indentStr);
                    }
                    else if (c == '}' || c == ']')
                    {
                        sb.AppendLine();
                        indent = Math.Max(0, indent - 1);
                        for (int s = 0; s < indent; s++) sb.Append(indentStr);
                        sb.Append(c);
                    }
                    else if (c == ',')
                    {
                        sb.Append(c);
                        sb.AppendLine();
                        for (int s = 0; s < indent; s++) sb.Append(indentStr);
                    }
                    else if (c == ':')
                    {
                        sb.Append(": ");
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }

                return sb.ToString();
            }

            public static string Minify(string json)
            {
                if (string.IsNullOrWhiteSpace(json)) return json;
                var sb = new StringBuilder();
                bool inString = false;
                bool isEscaped = false;

                for (int i = 0; i < json.Length; i++)
                {
                    char c = json[i];
                    if (inString)
                    {
                        sb.Append(c);
                        if (isEscaped) isEscaped = false;
                        else if (c == '\\') isEscaped = true;
                        else if (c == '"') inString = false;
                        continue;
                    }

                    if (char.IsWhiteSpace(c)) continue;

                    if (c == '"') inString = true;
                    sb.Append(c);
                }

                return sb.ToString();
            }
        }

        #endregion
    }

    #region Backward Compatibility Shims

    [Obsolete("JsonEditor is deprecated and will be removed in 5 release cycles. Please migrate to ZJsonEditor instead.")]
    public class JsonEditor : ZJsonEditor { }

    [Obsolete("ZeroJsonEditor is deprecated and will be removed in 5 release cycles. Please migrate to ZJsonEditor instead.")]
    public class ZeroJsonEditor : ZJsonEditor { }

    #endregion
}
