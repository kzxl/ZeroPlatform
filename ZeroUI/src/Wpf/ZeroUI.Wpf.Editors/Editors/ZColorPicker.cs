using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern anti-aliased Color Picker editor for ZeroUI WPF.
    /// Provides live swatch preview, standard enterprise palette matrix, and HEX input.
    /// Implements <see cref="IZeroEditor"/>.
    /// </summary>
    public class ZColorPicker : ZeroWpfControlBase, IZeroEditor
    {
        private static readonly Color[] Palette = new[]
        {
            Color.FromRgb(79, 70, 229),   // Indigo (Primary)
            Color.FromRgb(37, 99, 235),   // Blue
            Color.FromRgb(14, 165, 233),  // Sky
            Color.FromRgb(20, 184, 166),  // Teal
            Color.FromRgb(34, 197, 94),   // Green (Success)
            Color.FromRgb(234, 179, 8),   // Yellow (Warning)
            Color.FromRgb(249, 115, 22),  // Orange
            Color.FromRgb(239, 68, 68),   // Red (Danger)
            Color.FromRgb(236, 72, 153),  // Pink
            Color.FromRgb(168, 85, 247),  // Purple
            Color.FromRgb(15, 23, 42),    // Slate Dark
            Color.FromRgb(100, 116, 139), // Slate Muted
            Color.FromRgb(148, 163, 184), // Slate Light
            Color.FromRgb(226, 232, 240), // Slate Border
            Color.FromRgb(255, 255, 255), // Pure White
            Color.FromRgb(248, 250, 252), // Slate 50 (Pastel)
            Color.FromRgb(238, 242, 255), // Indigo 50 (Pastel)
            Color.FromRgb(236, 253, 245), // Emerald 50 (Pastel)
            Color.FromRgb(255, 251, 235), // Amber 50 (Pastel)
            Color.FromRgb(255, 241, 242)  // Rose 50 (Pastel)
        };

        private Border? _border;
        private Border? _swatchBorder;
        private TextBlock? _hexTextBlock;
        private TextBlock? _arrow;
        private Border? _popupBorder;
        private Popup? _popup;
        private TextBox? _hexInputBox;
        private Border? _previewBorder;

        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register(
                nameof(SelectedColor),
                typeof(Color),
                typeof(ZColorPicker),
                new FrameworkPropertyMetadata(Color.FromRgb(79, 70, 229), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));

        public static readonly DependencyProperty IsDropDownOpenProperty =
            DependencyProperty.Register(
                nameof(IsDropDownOpen),
                typeof(bool),
                typeof(ZColorPicker),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsDropDownOpenChanged));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(
                nameof(ReadOnly),
                typeof(bool),
                typeof(ZColorPicker),
                new PropertyMetadata(false));

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        public bool IsDropDownOpen
        {
            get => (bool)GetValue(IsDropDownOpenProperty);
            set => SetValue(IsDropDownOpenProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public object? EditValue
        {
            get => SelectedColor;
            set
            {
                if (value is Color c)
                {
                    SelectedColor = c;
                }
                else if (value is string s && !string.IsNullOrWhiteSpace(s))
                {
                    TryApplyHex(s);
                }
                else if (value == null)
                {
                    SelectedColor = Colors.Transparent;
                }
                IsModified = true;
                EditValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool IsModified { get; set; }

        public event EventHandler<Color>? ColorChanged;
        public event EventHandler? EditValueChanged;

        static ZColorPicker()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZColorPicker), new FrameworkPropertyMetadata(typeof(ZColorPicker)));
        }

        public ZColorPicker()
        {
            Background = ZeroWpfTheme.BgInput;
            BorderBrush = ZeroWpfTheme.BorderDefault;
            BorderThickness = new Thickness(1);
            Height = 36;
            Width = 150;
            FontSize = 13.0;
            Cursor = Cursors.Hand;

            BuildVisualTemplate();
        }

        protected override void OnThemeChanged()
        {
            base.OnThemeChanged();
            Background = ZeroWpfTheme.BgInput;
            BorderBrush = ZeroWpfTheme.BorderDefault;
            if (_border != null)
            {
                _border.Background = ZeroWpfTheme.BgInput;
                _border.BorderBrush = ZeroWpfTheme.BorderDefault;
            }
            if (_swatchBorder != null) _swatchBorder.BorderBrush = ZeroWpfTheme.BorderDefault;
            if (_hexTextBlock != null) _hexTextBlock.Foreground = ZeroWpfTheme.TextPrimary;
            if (_arrow != null) _arrow.Foreground = ZeroWpfTheme.TextSecondary;
            if (_popupBorder != null)
            {
                _popupBorder.Background = ZeroWpfTheme.BgCard;
                _popupBorder.BorderBrush = ZeroWpfTheme.BorderDefault;
            }
            if (_hexInputBox != null)
            {
                _hexInputBox.Background = ZeroWpfTheme.BgInput;
                _hexInputBox.Foreground = ZeroWpfTheme.TextPrimary;
                _hexInputBox.BorderBrush = ZeroWpfTheme.BorderDefault;
            }
            if (_previewBorder != null) _previewBorder.BorderBrush = ZeroWpfTheme.BorderDefault;
        }

        private Grid? _rootGrid;

        private void BuildVisualTemplate()
        {
            var rootGrid = new Grid();
            _rootGrid = rootGrid;

            _border = new Border
            {
                Background = Background,
                BorderBrush = BorderBrush,
                BorderThickness = BorderThickness,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 0, 8, 0)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24, GridUnitType.Pixel) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16, GridUnitType.Pixel) });

            _swatchBorder = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(SelectedColor),
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_swatchBorder, 0);
            headerGrid.Children.Add(_swatchBorder);

            _hexTextBlock = new TextBlock
            {
                Text = GetHex(SelectedColor),
                Foreground = ZeroWpfTheme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(_hexTextBlock, 1);
            headerGrid.Children.Add(_hexTextBlock);

            _arrow = new TextBlock
            {
                Text = "▼",
                FontSize = 9.0,
                Foreground = ZeroWpfTheme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(_arrow, 2);
            headerGrid.Children.Add(_arrow);

            _border.Child = headerGrid;
            rootGrid.Children.Add(_border);

            // Popup construction
            _popup = new Popup
            {
                PlacementTarget = this,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true
            };

            _popupBorder = new Border
            {
                Background = ZeroWpfTheme.BgCard,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Width = 200
            };

            var popupStack = new StackPanel();

            // Swatch Grid (4 rows x 5 cols)
            var swatchGrid = new UniformGrid { Columns = 5, Rows = 4, Margin = new Thickness(0, 0, 0, 10) };
            for (int i = 0; i < Palette.Length; i++)
            {
                var color = Palette[i];
                var btn = new Border
                {
                    Width = 26,
                    Height = 26,
                    Margin = new Thickness(3),
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(color),
                    BorderBrush = ZeroWpfTheme.BorderDefault,
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand
                };
                btn.MouseDown += (s, e) =>
                {
                    if (ReadOnly) return;
                    SelectedColor = color;
                    IsDropDownOpen = false;
                };
                swatchGrid.Children.Add(btn);
            }
            popupStack.Children.Add(swatchGrid);

            // HEX input and preview
            var bottomGrid = new Grid();
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40, GridUnitType.Pixel) });

            _hexInputBox = new TextBox
            {
                Height = 28,
                Text = GetHex(SelectedColor),
                Background = ZeroWpfTheme.BgInput,
                Foreground = ZeroWpfTheme.TextPrimary,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 0, 4, 0)
            };
            _hexInputBox.TextChanged += (s, e) =>
            {
                if (ReadOnly) return;
                string t = _hexInputBox.Text.Trim();
                if (TryParseHex(t, out Color parsed))
                {
                    if (_previewBorder != null) _previewBorder.Background = new SolidColorBrush(parsed);
                }
            };
            _hexInputBox.KeyDown += (s, e) =>
            {
                if (ReadOnly) return;
                if (e.Key == Key.Enter)
                {
                    TryApplyHex(_hexInputBox.Text);
                    IsDropDownOpen = false;
                }
            };
            Grid.SetColumn(_hexInputBox, 0);
            bottomGrid.Children.Add(_hexInputBox);

            _previewBorder = new Border
            {
                Width = 32,
                Height = 28,
                Margin = new Thickness(8, 0, 0, 0),
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(SelectedColor),
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = "Click to select this preview color"
            };
            _previewBorder.MouseDown += (s, e) =>
            {
                if (ReadOnly) return;
                if (TryParseHex(_hexInputBox?.Text, out Color parsed))
                {
                    SelectedColor = parsed;
                    IsDropDownOpen = false;
                }
            };
            Grid.SetColumn(_previewBorder, 1);
            bottomGrid.Children.Add(_previewBorder);

            popupStack.Children.Add(bottomGrid);
            _popupBorder.Child = popupStack;
            _popup.Child = _popupBorder;

            rootGrid.Children.Add(_popup);
            AddVisualChild(rootGrid);
            AddLogicalChild(rootGrid);
        }

        protected override int VisualChildrenCount => _rootGrid != null ? 1 : 0;
        protected override Visual GetVisualChild(int index) => _rootGrid ?? throw new ArgumentOutOfRangeException(nameof(index));

        protected override Size MeasureOverride(Size constraint)
        {
            if (_rootGrid != null)
            {
                _rootGrid.Measure(constraint);
                return _rootGrid.DesiredSize;
            }
            return base.MeasureOverride(constraint);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _rootGrid?.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);
            if (ReadOnly) return;

            if (!IsDropDownOpen)
            {
                IsDropDownOpen = true;
                if (_hexInputBox != null)
                {
                    _hexInputBox.Text = GetHex(SelectedColor);
                    _hexInputBox.Focus();
                    _hexInputBox.SelectAll();
                }
            }
        }

        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZColorPicker cp)
            {
                Color color = (Color)e.NewValue;
                if (cp._swatchBorder != null) cp._swatchBorder.Background = new SolidColorBrush(color);
                if (cp._previewBorder != null) cp._previewBorder.Background = new SolidColorBrush(color);
                if (cp._hexTextBlock != null) cp._hexTextBlock.Text = GetHex(color);
                if (cp._hexInputBox != null) cp._hexInputBox.Text = GetHex(color);
                cp.IsModified = true;
                cp.ColorChanged?.Invoke(cp, color);
                cp.EditValueChanged?.Invoke(cp, EventArgs.Empty);
            }
        }

        private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZColorPicker cp && cp._popup != null)
            {
                cp._popup.IsOpen = (bool)e.NewValue;
            }
        }

        public static bool TryParseHex(string? hex, out Color color)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                color = Colors.Transparent;
                return false;
            }
            string clean = hex!.Trim();
            if (!clean.StartsWith("#") && (clean.Length == 6 || clean.Length == 8))
            {
                clean = "#" + clean;
            }
            if (ZeroColor.TryParseHex(clean, out var zc))
            {
                color = Color.FromArgb(zc.A, zc.R, zc.G, zc.B);
                return true;
            }
            color = Colors.Transparent;
            return false;
        }

        private void TryApplyHex(string hex)
        {
            if (TryParseHex(hex, out Color parsed))
            {
                SelectedColor = parsed;
            }
        }

        private static string GetHex(Color c) => new ZeroColor(c.R, c.G, c.B, c.A).ToHex();

        public Color Color
        {
            get => SelectedColor;
            set => SelectedColor = value;
        }

        public string HexCode
        {
            get => GetHex(SelectedColor);
            set => TryApplyHex(value);
        }

        public void Reset()
        {
            SelectedColor = Color.FromRgb(79, 70, 229);
            IsModified = false;
        }

        public void Clear()
        {
            SelectedColor = Colors.Transparent;
            IsModified = false;
        }

        /// <summary>
        /// Displays a modern ZeroUI modal dialog for color selection.
        /// </summary>
        public static Color? PickColor(Window? owner, Color initialColor, string title = "Select Color")
        {
            var win = new Window
            {
                Title = title,
                Width = 280,
                Height = 200,
                WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize,
                Background = ZeroWpfTheme.BgCard
            };

            var stack = new StackPanel { Margin = new Thickness(16) };
            var lbl = new TextBlock
            {
                Text = "Choose Color:",
                Foreground = ZeroWpfTheme.TextPrimary,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stack.Children.Add(lbl);

            var picker = new ColorPickEdit
            {
                SelectedColor = initialColor,
                Height = 34,
                Margin = new Thickness(0, 0, 0, 16)
            };
            stack.Children.Add(picker);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnOk = new SimpleButton
            {
                Content = "OK",
                Variant = ButtonVariant.Primary,
                Width = 75,
                Height = 28,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true
            };
            var btnCancel = new SimpleButton
            {
                Content = "Cancel",
                Variant = ButtonVariant.Secondary,
                Width = 75,
                Height = 28,
                IsCancel = true
            };

            Color? result = null;
            btnOk.Click += (s, e) =>
            {
                result = picker.SelectedColor;
                win.DialogResult = true;
                win.Close();
            };

            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);

            win.Content = stack;
            if (win.ShowDialog() == true)
            {
                return result;
            }
            return null;
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ColorPickEdit"/>.
    /// </summary>
    [Obsolete("ZeroColorPicker is deprecated. Use ColorPickEdit instead.")]
    public class ZeroColorPicker : ColorPickEdit
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Convenience alias for <see cref="ZColorPicker"/>.
    /// </summary>
    public class ZColorPickEdit : ZColorPicker { }

    /// <summary>
    /// Legacy alias for <see cref="ZColorPicker"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ColorPickEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZColorPicker instead.")]
    public class ColorPickEdit : ZColorPicker { }

    /// <summary>
    /// Legacy alias for <see cref="ZColorPicker"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ColorPickerEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZColorPicker instead.")]
    public class ColorPickerEdit : ZColorPicker { }

    /// <summary>
    /// Legacy alias for <see cref="ZColorPicker"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroColorPickEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZColorPicker instead.")]
    public class ZeroColorPickEdit : ZColorPicker { }

    #endregion

}
