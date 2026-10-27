using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    public enum PasswordStrength
    {
        None,
        Weak,
        Fair,
        Good,
        Strong
    }

    /// <summary>
    /// Modern password input control for WPF with eye visibility toggle and visual strength meter.
    /// </summary>
    public class ZPasswordBox : Control
    {
        public static readonly DependencyProperty PasswordProperty =
            DependencyProperty.Register(nameof(Password), typeof(string), typeof(ZPasswordBox),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPasswordPropertyChanged));

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(ZPasswordBox),
                new PropertyMetadata("Enter password..."));

        public static readonly DependencyProperty ShowStrengthMeterProperty =
            DependencyProperty.Register(nameof(ShowStrengthMeter), typeof(bool), typeof(ZPasswordBox),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ShowToggleVisibilityProperty =
            DependencyProperty.Register(nameof(ShowToggleVisibility), typeof(bool), typeof(ZPasswordBox),
                new PropertyMetadata(true));

        private readonly PasswordBox _passwordBox;
        private readonly TextBox _textBox;
        private readonly Button _toggleButton;
        private readonly Rectangle _strengthBar;
        private readonly Border _rootBorder;
        private bool _isPasswordVisible;

        public event RoutedEventHandler? PasswordChanged;

        public string Password
        {
            get => (string)GetValue(PasswordProperty);
            set => SetValue(PasswordProperty, value);
        }

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public bool ShowStrengthMeter
        {
            get => (bool)GetValue(ShowStrengthMeterProperty);
            set => SetValue(ShowStrengthMeterProperty, value);
        }

        public bool ShowToggleVisibility
        {
            get => (bool)GetValue(ShowToggleVisibilityProperty);
            set => SetValue(ShowToggleVisibilityProperty, value);
        }

        private PasswordStrength _strength = PasswordStrength.None;
        public PasswordStrength Strength => _strength;

        public ZPasswordBox()
        {
            Height = 36;
            Background = ZeroWpfTheme.BgInput;
            BorderBrush = ZeroWpfTheme.BorderDefault;
            BorderThickness = new Thickness(1);

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var inputGrid = new Grid();
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _passwordBox = new PasswordBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = ZeroWpfTheme.TextPrimary,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 4, 0)
            };
            _passwordBox.PasswordChanged += (s, e) =>
            {
                if (!_isPasswordVisible)
                {
                    Password = _passwordBox.Password;
                    UpdateStrength();
                    PasswordChanged?.Invoke(this, e);
                }
            };

            _textBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = ZeroWpfTheme.TextPrimary,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 4, 0),
                Visibility = Visibility.Collapsed
            };
            _textBox.TextChanged += (s, e) =>
            {
                if (_isPasswordVisible)
                {
                    Password = _textBox.Text;
                    UpdateStrength();
                }
            };

            _toggleButton = new Button
            {
                Content = "👁",
                Width = 28,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = ZeroWpfTheme.TextMuted,
                Cursor = Cursors.Hand
            };
            _toggleButton.Click += (s, e) => ToggleVisibility();

            Grid.SetColumn(_passwordBox, 0);
            Grid.SetColumn(_textBox, 0);
            Grid.SetColumn(_toggleButton, 1);

            inputGrid.Children.Add(_passwordBox);
            inputGrid.Children.Add(_textBox);
            inputGrid.Children.Add(_toggleButton);

            _strengthBar = new Rectangle
            {
                Height = 3,
                HorizontalAlignment = HorizontalAlignment.Left,
                Fill = Brushes.Transparent
            };

            Grid.SetRow(inputGrid, 0);
            Grid.SetRow(_strengthBar, 1);

            rootGrid.Children.Add(inputGrid);
            rootGrid.Children.Add(_strengthBar);

            var border = new Border
            {
                Background = Background,
                BorderBrush = BorderBrush,
                BorderThickness = BorderThickness,
                CornerRadius = new CornerRadius(4),
                Child = rootGrid
            };

            _rootBorder = border;
            AddVisualChild(border);
            AddLogicalChild(border);

            Loaded += (s, e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            Unloaded += (s, e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            if (Dispatcher.CheckAccess())
            {
                Background = ZeroWpfTheme.BgInput;
                BorderBrush = ZeroWpfTheme.BorderDefault;
                _passwordBox.Foreground = ZeroWpfTheme.TextPrimary;
                _textBox.Foreground = ZeroWpfTheme.TextPrimary;
            }
            else
            {
                Dispatcher.BeginInvoke((Action)OnThemeChanged);
            }
        }

        private void ToggleVisibility()
        {
            _isPasswordVisible = !_isPasswordVisible;
            if (_isPasswordVisible)
            {
                _textBox.Text = _passwordBox.Password;
                _passwordBox.Visibility = Visibility.Collapsed;
                _textBox.Visibility = Visibility.Visible;
                _toggleButton.Foreground = ZeroWpfTheme.PrimaryAccent;
            }
            else
            {
                _passwordBox.Password = _textBox.Text;
                _textBox.Visibility = Visibility.Collapsed;
                _passwordBox.Visibility = Visibility.Visible;
                _toggleButton.Foreground = ZeroWpfTheme.TextMuted;
            }
        }

        private void UpdateStrength()
        {
            string pwd = _isPasswordVisible ? _textBox.Text : _passwordBox.Password;
            if (string.IsNullOrEmpty(pwd))
            {
                _strength = PasswordStrength.None;
                _strengthBar.Fill = Brushes.Transparent;
                _strengthBar.Width = 0;
                return;
            }

            int score = 0;
            if (pwd.Length >= 8) score++;
            if (pwd.Length >= 12) score++;
            if (Regex.IsMatch(pwd, @"[A-Z]")) score++;
            if (Regex.IsMatch(pwd, @"[0-9]")) score++;
            if (Regex.IsMatch(pwd, @"[^A-Za-z0-9]")) score++;

            _strength = score switch
            {
                <= 1 => PasswordStrength.Weak,
                <= 2 => PasswordStrength.Fair,
                <= 3 => PasswordStrength.Good,
                _ => PasswordStrength.Strong
            };

            if (!ShowStrengthMeter)
            {
                _strengthBar.Fill = Brushes.Transparent;
                _strengthBar.Width = 0;
                return;
            }

            Color barColor = _strength switch
            {
                PasswordStrength.Weak => Color.FromRgb(231, 76, 60),  // Red
                PasswordStrength.Fair => Color.FromRgb(243, 156, 18),    // Orange
                PasswordStrength.Good => Color.FromRgb(241, 196, 15),    // Yellow
                _ => Color.FromRgb(46, 204, 113)     // Green
            };

            double fraction = Math.Min(1.0, score / 5.0);
            _strengthBar.Fill = new SolidColorBrush(barColor);
            _strengthBar.Width = Math.Max(10, ActualWidth * fraction);
        }

        private static void OnPasswordPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZPasswordBox ctrl)
            {
                string newVal = (string)e.NewValue ?? string.Empty;
                if (ctrl._isPasswordVisible && ctrl._textBox.Text != newVal) ctrl._textBox.Text = newVal;
                else if (!ctrl._isPasswordVisible && ctrl._passwordBox.Password != newVal) ctrl._passwordBox.Password = newVal;
                ctrl.UpdateStrength();
            }
        }

        protected override int VisualChildrenCount => _rootBorder != null ? 1 : 0;
        protected override Visual GetVisualChild(int index)
        {
            if (index != 0 || _rootBorder == null) throw new ArgumentOutOfRangeException(nameof(index));
            return _rootBorder;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            _rootBorder?.Measure(constraint);
            return _rootBorder?.DesiredSize ?? base.MeasureOverride(constraint);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _rootBorder?.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }
    }

    [Obsolete("ZeroPasswordBox is deprecated. Use ZPasswordBox instead.")]
    public class ZeroPasswordBox : ZPasswordBox { }
}
