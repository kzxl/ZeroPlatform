using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Text.RegularExpressions;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern rich text editor for WPF.
    /// </summary>
    public class ZRichTextEditor : Control
    {
        private RichTextBox _richTextBox = null!;
        private StackPanel _toolbar = null!;
        private Grid _root = null!;
        private bool _isUpdatingHtml;

        public static readonly DependencyProperty HtmlContentProperty =
            DependencyProperty.Register(nameof(HtmlContent), typeof(string), typeof(ZRichTextEditor),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHtmlContentChanged));

        public static readonly DependencyProperty ShowToolbarProperty =
            DependencyProperty.Register(nameof(ShowToolbar), typeof(bool), typeof(ZRichTextEditor),
                new PropertyMetadata(true, OnShowToolbarChanged));

        public static readonly DependencyProperty EnabledFeaturesProperty =
            DependencyProperty.Register(nameof(EnabledFeatures), typeof(RichTextFeatures), typeof(ZRichTextEditor),
                new PropertyMetadata(RichTextFeatures.All));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(ZRichTextEditor),
                new PropertyMetadata(new CornerRadius(5)));

        public string HtmlContent
        {
            get => (string)GetValue(HtmlContentProperty);
            set => SetValue(HtmlContentProperty, value);
        }

        public bool ShowToolbar
        {
            get => (bool)GetValue(ShowToolbarProperty);
            set => SetValue(ShowToolbarProperty, value);
        }

        public RichTextFeatures EnabledFeatures
        {
            get => (RichTextFeatures)GetValue(EnabledFeaturesProperty);
            set => SetValue(EnabledFeaturesProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        private static void OnHtmlContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZRichTextEditor editor && !editor._isUpdatingHtml)
            {
                editor.ConvertHtmlToFlowDocument(e.NewValue as string);
            }
        }

        private static void OnShowToolbarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZRichTextEditor editor && editor._toolbar != null)
            {
                editor._toolbar.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZRichTextEditor"/> class.
        /// </summary>
        public ZRichTextEditor()
        {
            BuildVisualTree();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void BuildVisualTree()
        {
            _root = new Grid();
            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            _toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Height = 32,
                Visibility = ShowToolbar ? Visibility.Visible : Visibility.Collapsed
            };
            _root.Children.Add(_toolbar);

            _richTextBox = new RichTextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent
            };
            _richTextBox.TextChanged += RichTextBox_TextChanged;
            Grid.SetRow(_richTextBox, 1);
            _root.Children.Add(_richTextBox);

            AddVisualChild(_root);
            AddLogicalChild(_root);
            
            var btnBold = new System.Windows.Controls.Primitives.ToggleButton { Content = "B", Width = 24, Margin = new Thickness(2) };
            btnBold.Command = System.Windows.Documents.EditingCommands.ToggleBold;
            btnBold.CommandTarget = _richTextBox;
            _toolbar.Children.Add(btnBold);

            var btnItalic = new System.Windows.Controls.Primitives.ToggleButton { Content = "I", Width = 24, Margin = new Thickness(2) };
            btnItalic.Command = System.Windows.Documents.EditingCommands.ToggleItalic;
            btnItalic.CommandTarget = _richTextBox;
            _toolbar.Children.Add(btnItalic);
            
            var btnUnderline = new System.Windows.Controls.Primitives.ToggleButton { Content = "U", Width = 24, Margin = new Thickness(2) };
            btnUnderline.Command = System.Windows.Documents.EditingCommands.ToggleUnderline;
            btnUnderline.CommandTarget = _richTextBox;
            _toolbar.Children.Add(btnUnderline);
        }

        protected override int VisualChildrenCount => _root == null ? 0 : 1;

        protected override Visual GetVisualChild(int index)
        {
            if (index != 0 || _root == null) throw new ArgumentOutOfRangeException();
            return _root;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_root != null)
            {
                _root.Measure(availableSize);
                return _root.DesiredSize;
            }
            return base.MeasureOverride(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_root != null)
            {
                _root.Arrange(new Rect(finalSize));
            }
            return finalSize;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            OnThemeChanged();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            _richTextBox.Foreground = ZeroWpfTheme.TextPrimary;
            _toolbar.Background = Brushes.Transparent;
        }

        private void RichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingHtml) return;
            _isUpdatingHtml = true;
            HtmlContent = ConvertFlowDocumentToHtml();
            _isUpdatingHtml = false;
        }

        private void ConvertHtmlToFlowDocument(string? html)
        {
            if (_richTextBox == null || _richTextBox.Document == null) return;
            _isUpdatingHtml = true;
            _richTextBox.Document.Blocks.Clear();
            if (!string.IsNullOrEmpty(html))
            {
                var run = new Run(Regex.Replace(html, "<.*?>", string.Empty));
                var para = new Paragraph(run);
                _richTextBox.Document.Blocks.Add(para);
            }
            _isUpdatingHtml = false;
        }

        private string ConvertFlowDocumentToHtml()
        {
            if (_richTextBox == null || _richTextBox.Document == null) return string.Empty;
            var textRange = new TextRange(_richTextBox.Document.ContentStart, _richTextBox.Document.ContentEnd);
            string text = textRange.Text.Trim();
            return string.IsNullOrEmpty(text) ? string.Empty : $"<div>{System.Net.WebUtility.HtmlEncode(text)}</div>";
        }
    }

    /// <summary>
    /// Backward compatibility shim.
    /// </summary>
    [Obsolete("Use ZRichTextEditor instead.")]
    public class ZeroRichTextEditor : ZRichTextEditor { }
}
