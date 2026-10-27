using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Media
{
    /// <summary>
    /// Represents a color swatch item in <see cref="ZDominantPalette"/>.
    /// </summary>
    public class PaletteSwatchItem : INotifyPropertyChanged
    {
        private string _hex = "#FFFFFF";
        private string _label = "";
        private string _role = "";
        private Brush _brush = Brushes.White;
        private Color _color = Colors.White;
        private double _percentage;
        private object? _tag;

        public PaletteSwatchItem() { }

        public PaletteSwatchItem(string hex, string label = "", string role = "", Brush? brush = null)
        {
            _hex = hex ?? "#000000";
            _label = label;
            _role = role;
            if (brush != null)
            {
                _brush = brush;
                if (brush is SolidColorBrush scb) _color = scb.Color;
            }
            else
            {
                ParseHex(_hex);
            }
        }

        public PaletteSwatchItem(byte r, byte g, byte b, string label = "", string role = "", double pct = 0)
        {
            _color = Color.FromRgb(r, g, b);
            _hex = $"#{r:X2}{g:X2}{b:X2}";
            _brush = new SolidColorBrush(_color);
            _label = label;
            _role = role;
            _percentage = pct;
        }

        public string Hex
        {
            get => _hex;
            set
            {
                if (_hex != value)
                {
                    _hex = value;
                    ParseHex(value);
                    OnPropertyChanged(nameof(Hex));
                }
            }
        }

        public string Label
        {
            get => _label;
            set { _label = value; OnPropertyChanged(nameof(Label)); }
        }

        public string Role
        {
            get => _role;
            set { _role = value; OnPropertyChanged(nameof(Role)); }
        }

        public Brush Brush
        {
            get => _brush;
            set { _brush = value; OnPropertyChanged(nameof(Brush)); }
        }

        public Color Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(nameof(Color)); }
        }

        public double Percentage
        {
            get => _percentage;
            set { _percentage = value; OnPropertyChanged(nameof(Percentage)); }
        }

        public object? Tag
        {
            get => _tag;
            set { _tag = value; OnPropertyChanged(nameof(Tag)); }
        }

        private void ParseHex(string hex)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hex)) return;
                var clean = hex.TrimStart('#');
                if (clean.Length == 6)
                {
                    byte r = Convert.ToByte(clean.Substring(0, 2), 16);
                    byte g = Convert.ToByte(clean.Substring(2, 2), 16);
                    byte b = Convert.ToByte(clean.Substring(4, 2), 16);
                    _color = Color.FromRgb(r, g, b);
                    _brush = new SolidColorBrush(_color);
                    _brush.Freeze();
                }
                else if (clean.Length == 8)
                {
                    byte a = Convert.ToByte(clean.Substring(0, 2), 16);
                    byte r = Convert.ToByte(clean.Substring(2, 2), 16);
                    byte g = Convert.ToByte(clean.Substring(4, 2), 16);
                    byte b = Convert.ToByte(clean.Substring(6, 2), 16);
                    _color = Color.FromArgb(a, r, g, b);
                    _brush = new SolidColorBrush(_color);
                    _brush.Freeze();
                }
            }
            catch { /* Ignore invalid hex input */ }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public override string ToString() => $"{Hex} ({Label})";
    }

    /// <summary>
    /// Event arguments for palette swatch clicks.
    /// </summary>
    public class PaletteSwatchClickEventArgs : EventArgs
    {
        public PaletteSwatchItem Item { get; }
        public bool Handled { get; set; }

        public PaletteSwatchClickEventArgs(PaletteSwatchItem item)
        {
            Item = item;
        }
    }

    /// <summary>
    /// Modern industrial Dominant Color Palette and Harmony inspector control for ZeroUI in WPF.
    /// Displays extracted image swatches (K-Means/Median-Cut), color percentage badges,
    /// color harmony recommendations (Complementary, Analogous, Triadic), and contrast assessment.
    /// </summary>
    public class ZDominantPalette : Control, IZeroEditor
    {
        private readonly ObservableCollection<PaletteSwatchItem> _swatches = new ObservableCollection<PaletteSwatchItem>();
        private readonly ObservableCollection<PaletteSwatchItem> _suggestions = new ObservableCollection<PaletteSwatchItem>();

        private WrapPanel? _swatchesWrap;
        private WrapPanel? _suggestionsWrap;
        private TextBlock? _contrastText;
        private FrameworkElement? _suggestionsSection;
        private TextBlock? _feedbackText;
        private bool _isModified;

        #region Dependency Properties

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ZDominantPalette),
                new PropertyMetadata("DOMINANT PALETTE", OnLayoutPropertyChanged));

        public static readonly DependencyProperty SuggestionsTitleProperty =
            DependencyProperty.Register(nameof(SuggestionsTitle), typeof(string), typeof(ZDominantPalette),
                new PropertyMetadata("COLOR HARMONY", OnLayoutPropertyChanged));

        public static readonly DependencyProperty ContrastAdviceProperty =
            DependencyProperty.Register(nameof(ContrastAdvice), typeof(string), typeof(ZDominantPalette),
                new PropertyMetadata("", OnContrastAdviceChanged));

        public static readonly DependencyProperty ShowSuggestionsProperty =
            DependencyProperty.Register(nameof(ShowSuggestions), typeof(bool), typeof(ZDominantPalette),
                new PropertyMetadata(true, OnLayoutPropertyChanged));

        public static readonly DependencyProperty ShowContrastAdviceProperty =
            DependencyProperty.Register(nameof(ShowContrastAdvice), typeof(bool), typeof(ZDominantPalette),
                new PropertyMetadata(true, OnLayoutPropertyChanged));

        public static readonly DependencyProperty AllowCopyOnClickProperty =
            DependencyProperty.Register(nameof(AllowCopyOnClick), typeof(bool), typeof(ZDominantPalette),
                new PropertyMetadata(true));

        public static readonly DependencyProperty SwatchWidthProperty =
            DependencyProperty.Register(nameof(SwatchWidth), typeof(double), typeof(ZDominantPalette),
                new PropertyMetadata(42.0, OnLayoutPropertyChanged));

        public static readonly DependencyProperty SwatchHeightProperty =
            DependencyProperty.Register(nameof(SwatchHeight), typeof(double), typeof(ZDominantPalette),
                new PropertyMetadata(30.0, OnLayoutPropertyChanged));

        public static readonly DependencyProperty SuggestionSwatchWidthProperty =
            DependencyProperty.Register(nameof(SuggestionSwatchWidth), typeof(double), typeof(ZDominantPalette),
                new PropertyMetadata(36.0, OnLayoutPropertyChanged));

        public static readonly DependencyProperty SuggestionSwatchHeightProperty =
            DependencyProperty.Register(nameof(SuggestionSwatchHeight), typeof(double), typeof(ZDominantPalette),
                new PropertyMetadata(22.0, OnLayoutPropertyChanged));

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(ZDominantPalette),
                new PropertyMetadata(false));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string SuggestionsTitle
        {
            get => (string)GetValue(SuggestionsTitleProperty);
            set => SetValue(SuggestionsTitleProperty, value);
        }

        public string ContrastAdvice
        {
            get => (string)GetValue(ContrastAdviceProperty);
            set => SetValue(ContrastAdviceProperty, value);
        }

        public bool ShowSuggestions
        {
            get => (bool)GetValue(ShowSuggestionsProperty);
            set => SetValue(ShowSuggestionsProperty, value);
        }

        public bool ShowContrastAdvice
        {
            get => (bool)GetValue(ShowContrastAdviceProperty);
            set => SetValue(ShowContrastAdviceProperty, value);
        }

        public bool AllowCopyOnClick
        {
            get => (bool)GetValue(AllowCopyOnClickProperty);
            set => SetValue(AllowCopyOnClickProperty, value);
        }

        public double SwatchWidth
        {
            get => (double)GetValue(SwatchWidthProperty);
            set => SetValue(SwatchWidthProperty, value);
        }

        public double SwatchHeight
        {
            get => (double)GetValue(SwatchHeightProperty);
            set => SetValue(SwatchHeightProperty, value);
        }

        public double SuggestionSwatchWidth
        {
            get => (double)GetValue(SuggestionSwatchWidthProperty);
            set => SetValue(SuggestionSwatchWidthProperty, value);
        }

        public double SuggestionSwatchHeight
        {
            get => (double)GetValue(SuggestionSwatchHeightProperty);
            set => SetValue(SuggestionSwatchHeightProperty, value);
        }

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public bool ReadOnly
        {
            get => IsReadOnly;
            set => IsReadOnly = value;
        }

        #endregion

        #region Events

        public event EventHandler<PaletteSwatchClickEventArgs>? SwatchClicked;
        public event EventHandler<string>? SwatchCopied;
        public event EventHandler? EditValueChanged;

        #endregion

        #region IZeroEditor Implementation

        public object? EditValue
        {
            get => _swatches.Select(s => s.Hex).ToList();
            set
            {
                if (value is IEnumerable<PaletteSwatchItem> items)
                {
                    SetSwatches(items);
                }
                else if (value is IEnumerable<string> hexList)
                {
                    SetHexColors(hexList);
                }
                else if (value == null)
                {
                    Clear();
                }
            }
        }

        public bool IsModified
        {
            get => _isModified;
            set => _isModified = value;
        }

        public void Reset()
        {
            Clear();
            _isModified = false;
        }

        #endregion

        public ObservableCollection<PaletteSwatchItem> Swatches => _swatches;
        public ObservableCollection<PaletteSwatchItem> Suggestions => _suggestions;

        static ZDominantPalette()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZDominantPalette),
                new FrameworkPropertyMetadata(typeof(ZDominantPalette)));
        }

        public ZDominantPalette()
        {
            Loaded += (s, e) => BuildVisualTree();
            _swatches.CollectionChanged += (s, e) => RebuildSwatches();
            _suggestions.CollectionChanged += (s, e) => RebuildSuggestions();
        }

        private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZDominantPalette ctrl)
            {
                ctrl.BuildVisualTree();
            }
        }

        private static void OnContrastAdviceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZDominantPalette ctrl && ctrl._contrastText != null)
            {
                var text = (string)e.NewValue;
                ctrl._contrastText.Text = text;
                ctrl._contrastText.Visibility = (!string.IsNullOrWhiteSpace(text) && ctrl.ShowContrastAdvice)
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Populates dominant swatches, suggestions, and contrast advice in a single call.
        /// </summary>
        public void SetPalette(IEnumerable<PaletteSwatchItem> swatches,
                               IEnumerable<PaletteSwatchItem>? suggestions = null,
                               string? contrastAdvice = null)
        {
            _swatches.Clear();
            if (swatches != null)
            {
                foreach (var s in swatches) _swatches.Add(s);
            }

            _suggestions.Clear();
            if (suggestions != null)
            {
                foreach (var s in suggestions) _suggestions.Add(s);
            }

            ContrastAdvice = contrastAdvice ?? "";
            _isModified = true;
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Populates swatches from an array of hex strings.
        /// </summary>
        public void SetHexColors(IEnumerable<string> hexStrings)
        {
            _swatches.Clear();
            if (hexStrings != null)
            {
                foreach (var hex in hexStrings)
                {
                    _swatches.Add(new PaletteSwatchItem(hex));
                }
            }
            _isModified = true;
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Clears all swatches and advice.
        /// </summary>
        public void Clear()
        {
            _swatches.Clear();
            _suggestions.Clear();
            ContrastAdvice = "";
        }

        private void SetSwatches(IEnumerable<PaletteSwatchItem> items)
        {
            _swatches.Clear();
            if (items != null)
            {
                foreach (var item in items) _swatches.Add(item);
            }
            _isModified = true;
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        #region Visual Tree Construction

        private Visual? _rootVisual;

        protected override int VisualChildrenCount => _rootVisual != null ? 1 : 0;

        protected override Visual GetVisualChild(int index)
        {
            if (_rootVisual == null || index != 0) throw new ArgumentOutOfRangeException(nameof(index));
            return _rootVisual;
        }

        private void AttachVisualChild(Visual visual)
        {
            if (_rootVisual != null)
            {
                RemoveVisualChild(_rootVisual);
                RemoveLogicalChild(_rootVisual);
            }
            _rootVisual = visual;
            if (visual != null)
            {
                AddVisualChild(visual);
                AddLogicalChild(visual);
            }
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (_rootVisual is UIElement elem)
            {
                elem.Measure(constraint);
                return elem.DesiredSize;
            }
            return base.MeasureOverride(constraint);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            if (_rootVisual is UIElement elem)
            {
                elem.Arrange(new Rect(arrangeBounds));
                return arrangeBounds;
            }
            return base.ArrangeOverride(arrangeBounds);
        }

        private void BuildVisualTree()
        {
            var root = new StackPanel();

            // Main Title Banner
            if (!string.IsNullOrWhiteSpace(Title))
            {
                var titleBorder = new Border
                {
                    Background = ZeroWpfTheme.BgCard,
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(0, 2, 0, 2)
                };
                titleBorder.Child = new TextBlock
                {
                    Text = Title,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = ZeroWpfTheme.TextSecondary
                };
                root.Children.Add(titleBorder);
            }

            // Swatches WrapPanel
            _swatchesWrap = new WrapPanel
            {
                Margin = new Thickness(6, 4, 6, 4)
            };
            root.Children.Add(_swatchesWrap);

            // Copy feedback toast
            _feedbackText = new TextBlock
            {
                FontSize = 9.5,
                Foreground = ZeroWpfTheme.PrimaryAccent,
                Margin = new Thickness(8, 0, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Visibility = Visibility.Collapsed
            };
            root.Children.Add(_feedbackText);

            // Contrast Advice Text
            _contrastText = new TextBlock
            {
                Text = ContrastAdvice,
                FontSize = 10,
                Foreground = ZeroWpfTheme.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10, 2, 10, 4),
                Visibility = (!string.IsNullOrWhiteSpace(ContrastAdvice) && ShowContrastAdvice)
                    ? Visibility.Visible : Visibility.Collapsed
            };
            root.Children.Add(_contrastText);

            // Suggestions Section (Color Harmony)
            if (ShowSuggestions)
            {
                var suggStack = new StackPanel();
                if (!string.IsNullOrWhiteSpace(SuggestionsTitle))
                {
                    var suggTitleBorder = new Border
                    {
                        Background = ZeroWpfTheme.BgCard,
                        Padding = new Thickness(10, 3, 10, 3),
                        Margin = new Thickness(0, 4, 0, 2)
                    };
                    suggTitleBorder.Child = new TextBlock
                    {
                        Text = SuggestionsTitle,
                        FontSize = 9.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = ZeroWpfTheme.TextSecondary
                    };
                    suggStack.Children.Add(suggTitleBorder);
                }

                _suggestionsWrap = new WrapPanel
                {
                    Margin = new Thickness(6, 2, 6, 4)
                };
                suggStack.Children.Add(_suggestionsWrap);

                _suggestionsSection = suggStack;
                root.Children.Add(suggStack);
            }

            AttachVisualChild(root);
            RebuildSwatches();
            RebuildSuggestions();
        }

        private void RebuildSwatches()
        {
            if (_swatchesWrap == null) return;
            _swatchesWrap.Children.Clear();

            foreach (var item in _swatches)
            {
                var card = CreateSwatchCard(item, SwatchWidth, SwatchHeight, isPrimary: true);
                _swatchesWrap.Children.Add(card);
            }
        }

        private void RebuildSuggestions()
        {
            if (_suggestionsWrap == null) return;
            _suggestionsWrap.Children.Clear();

            foreach (var item in _suggestions)
            {
                var card = CreateSwatchCard(item, SuggestionSwatchWidth, SuggestionSwatchHeight, isPrimary: false);
                _suggestionsWrap.Children.Add(card);
            }

            if (_suggestionsSection != null)
            {
                _suggestionsSection.Visibility = _suggestions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private FrameworkElement CreateSwatchCard(PaletteSwatchItem item, double width, double height, bool isPrimary)
        {
            var card = new Border
            {
                Margin = new Thickness(2),
                Padding = new Thickness(2),
                CornerRadius = new CornerRadius(3),
                Background = ZeroWpfTheme.BgCard,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = $"Color: {item.Hex}\nRole: {(!string.IsNullOrWhiteSpace(item.Role) ? item.Role : item.Label)}\nClick to copy HEX code"
            };

            var stack = new StackPanel();

            // Color patch
            var colorPatch = new Border
            {
                Width = width,
                Height = height,
                CornerRadius = new CornerRadius(3),
                Background = item.Brush,
                BorderBrush = ZeroWpfTheme.BorderSubtle,
                BorderThickness = new Thickness(0.5)
            };
            stack.Children.Add(colorPatch);

            // Hex text
            var hexBlock = new TextBlock
            {
                Text = item.Hex,
                FontSize = 8,
                Foreground = ZeroWpfTheme.TextMuted,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            stack.Children.Add(hexBlock);

            // Role / Label text (percent or role)
            var labelText = !string.IsNullOrWhiteSpace(item.Label) ? item.Label : item.Role;
            if (!string.IsNullOrWhiteSpace(labelText))
            {
                var labelBlock = new TextBlock
                {
                    Text = labelText,
                    FontSize = 8,
                    Foreground = isPrimary ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.TextSecondary,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                stack.Children.Add(labelBlock);
            }

            card.Child = stack;

            // Hover effects
            card.MouseEnter += (s, e) =>
            {
                card.BorderBrush = ZeroWpfTheme.PrimaryAccent;
                card.Background = ZeroWpfTheme.BgHover;
            };
            card.MouseLeave += (s, e) =>
            {
                card.BorderBrush = Brushes.Transparent;
                card.Background = ZeroWpfTheme.BgCard;
            };

            // Click handling
            card.MouseLeftButtonUp += (s, e) =>
            {
                if (AllowCopyOnClick && !string.IsNullOrWhiteSpace(item.Hex))
                {
                    try
                    {
                        Clipboard.SetText(item.Hex);
                        ShowCopyFeedback($"Copied {item.Hex} to clipboard!");
                        SwatchCopied?.Invoke(this, item.Hex);
                    }
                    catch { /* Clipboard access error in restricted environment */ }
                }

                var args = new PaletteSwatchClickEventArgs(item);
                SwatchClicked?.Invoke(this, args);
            };

            return card;
        }

        private void ShowCopyFeedback(string msg)
        {
            if (_feedbackText == null) return;
            _feedbackText.Text = msg;
            _feedbackText.Visibility = Visibility.Visible;

            // Auto-hide after 1.8 seconds
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1800)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                if (_feedbackText != null) _feedbackText.Visibility = Visibility.Collapsed;
            };
            timer.Start();
        }

        #endregion
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZDominantPalette"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DominantPaletteControl is deprecated. Use ZDominantPalette instead.")]
    public class DominantPaletteControl : ZDominantPalette
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZDominantPalette"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroDominantPalette is deprecated. Use ZDominantPalette instead.")]
    public class ZeroDominantPalette : ZDominantPalette
    {
    }
    #endregion
}
