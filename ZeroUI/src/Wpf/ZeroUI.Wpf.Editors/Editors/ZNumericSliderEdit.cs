using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Event arguments for <see cref="NumericSliderEdit.ValueChanged"/> events.
    /// Provides the new value, old value, and whether modifier keys (Alt, Shift, Ctrl) were held during modification.
    /// </summary>
    public class NumericSliderValueChangedEventArgs : EventArgs
    {
        public double OldValue { get; }
        public double NewValue { get; }
        public bool IsAltDown { get; }
        public bool IsShiftDown { get; }
        public bool IsControlDown { get; }

        public NumericSliderValueChangedEventArgs(double oldValue, double newValue, bool alt, bool shift, bool ctrl)
        {
            OldValue = oldValue;
            NewValue = newValue;
            IsAltDown = alt;
            IsShiftDown = shift;
            IsControlDown = ctrl;
        }
    }

    /// <summary>
    /// Composite numeric parameter editor for ZeroUI in WPF.
    /// Integrates a caption label, fine-tuned trackbar slider, and direct numeric input box.
    /// Supports double-click label to reset to default value, mouse-wheel micro-stepping,
    /// Alt-key live clipping signaling, and automatic theme synchronization.
    /// </summary>
    public class ZNumericSliderEdit : Control, IZeroEditor
    {
        private TextBlock? _captionLabel;
        private Slider? _slider;
        private TextBox? _inputBox;
        private bool _isInternalUpdating;
        private bool _isModified;

        #region Dependency Properties

        public static readonly DependencyProperty CaptionProperty =
            DependencyProperty.Register(nameof(Caption), typeof(string), typeof(ZNumericSliderEdit),
                new PropertyMetadata("", OnCaptionChanged));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(ZNumericSliderEdit),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(ZNumericSliderEdit),
                new PropertyMetadata(0.0, OnRangeChanged));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(ZNumericSliderEdit),
                new PropertyMetadata(100.0, OnRangeChanged));

        public static readonly DependencyProperty DefaultValueProperty =
            DependencyProperty.Register(nameof(DefaultValue), typeof(double), typeof(ZNumericSliderEdit),
                new PropertyMetadata(0.0));

        public static readonly DependencyProperty StepProperty =
            DependencyProperty.Register(nameof(Step), typeof(double), typeof(ZNumericSliderEdit),
                new PropertyMetadata(1.0, OnStepChanged));

        public static readonly DependencyProperty FormatProperty =
            DependencyProperty.Register(nameof(Format), typeof(string), typeof(ZNumericSliderEdit),
                new PropertyMetadata("0.00", OnFormatChanged));

        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(nameof(Unit), typeof(string), typeof(ZNumericSliderEdit),
                new PropertyMetadata(""));

        public static readonly DependencyProperty CaptionWidthProperty =
            DependencyProperty.Register(nameof(CaptionWidth), typeof(GridLength), typeof(ZNumericSliderEdit),
                new PropertyMetadata(new GridLength(92)));

        public static readonly DependencyProperty InputWidthProperty =
            DependencyProperty.Register(nameof(InputWidth), typeof(GridLength), typeof(ZNumericSliderEdit),
                new PropertyMetadata(new GridLength(48)));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZNumericSliderEdit),
                new PropertyMetadata(false, OnReadOnlyChanged));

        public string Caption
        {
            get => (string)GetValue(CaptionProperty);
            set => SetValue(CaptionProperty, value);
        }

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public double DefaultValue
        {
            get => (double)GetValue(DefaultValueProperty);
            set => SetValue(DefaultValueProperty, value);
        }

        public double Step
        {
            get => (double)GetValue(StepProperty);
            set => SetValue(StepProperty, value);
        }

        public string Format
        {
            get => (string)GetValue(FormatProperty);
            set => SetValue(FormatProperty, value);
        }

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        public GridLength CaptionWidth
        {
            get => (GridLength)GetValue(CaptionWidthProperty);
            set => SetValue(CaptionWidthProperty, value);
        }

        public GridLength InputWidth
        {
            get => (GridLength)GetValue(InputWidthProperty);
            set => SetValue(InputWidthProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        #endregion

        #region Events

        public event EventHandler<NumericSliderValueChangedEventArgs>? ValueChanged;
        public event EventHandler? EditValueChanged;

        #endregion

        #region IZeroEditor Implementation

        public object? EditValue
        {
            get => Value;
            set
            {
                if (value == null)
                {
                    Reset();
                }
                else if (value is double d)
                {
                    Value = d;
                }
                else if (value is float f)
                {
                    Value = f;
                }
                else if (value is int i)
                {
                    Value = i;
                }
                else if (double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    Value = parsed;
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
            Value = DefaultValue;
            _isModified = false;
        }

        public void Clear()
        {
            Reset();
        }

        #endregion

        static ZNumericSliderEdit()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZNumericSliderEdit),
                new FrameworkPropertyMetadata(typeof(ZNumericSliderEdit)));
        }

        public ZNumericSliderEdit()
        {
            Loaded += (s, e) => BuildVisualTree();
        }

        public ZNumericSliderEdit(string caption, double min, double max, double defVal, string fmt = "0.00")
            : this()
        {
            Caption = caption;
            Minimum = min;
            Maximum = max;
            DefaultValue = defVal;
            Value = defVal;
            Format = fmt;
            Step = (max - min) > 10 ? 1.0 : 0.01;
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
            var grid = new Grid
            {
                Margin = new Thickness(0, 2, 0, 2)
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = CaptionWidth });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = InputWidth });

            // 1. Caption Label
            _captionLabel = new TextBlock
            {
                Text = Caption,
                FontSize = 11,
                Foreground = ZeroWpfTheme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                ToolTip = "Double-click to reset to default (" + DefaultValue.ToString(Format, CultureInfo.InvariantCulture) + ")"
            };
            _captionLabel.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount >= 2 && !ReadOnly)
                {
                    Value = DefaultValue;
                    e.Handled = true;
                }
            };
            Grid.SetColumn(_captionLabel, 0);
            grid.Children.Add(_captionLabel);

            // 2. Trackbar Slider
            _slider = new Slider
            {
                Minimum = Minimum,
                Maximum = Maximum,
                Value = Value,
                SmallChange = Step,
                LargeChange = Step * 10,
                IsMoveToPointEnabled = true,
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = !ReadOnly
            };
            _slider.ValueChanged += OnSliderValueChanged;
            Grid.SetColumn(_slider, 1);
            grid.Children.Add(_slider);

            // 3. Numeric Input Box
            _inputBox = new TextBox
            {
                Text = FormatValue(Value),
                FontSize = 10,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
                Background = ZeroWpfTheme.BgInput,
                Foreground = ZeroWpfTheme.TextPrimary,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(2, 1, 2, 1),
                IsEnabled = !ReadOnly
            };
            _inputBox.KeyDown += OnInputBoxKeyDown;
            _inputBox.LostFocus += (s, e) => CommitInputText();
            _inputBox.MouseDoubleClick += (s, e) =>
            {
                if (!ReadOnly)
                {
                    Value = DefaultValue;
                    e.Handled = true;
                }
            };
            Grid.SetColumn(_inputBox, 2);
            grid.Children.Add(_inputBox);

            // Mouse wheel stepping over control
            grid.PreviewMouseWheel += (s, e) =>
            {
                if (ReadOnly) return;
                double delta = e.Delta > 0 ? Step : -Step;
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) delta *= 10;
                Value = Clamp(Value + delta, Minimum, Maximum);
                e.Handled = true;
            };

            AttachVisualChild(grid);
        }

        private static double Clamp(double val, double min, double max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }

        #endregion

        #region Event Handlers & Data Synchronization

        private void OnSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInternalUpdating) return;
            _isInternalUpdating = true;
            try
            {
                Value = e.NewValue;
                if (_inputBox != null && !_inputBox.IsKeyboardFocused)
                {
                    _inputBox.Text = FormatValue(e.NewValue);
                }
            }
            finally
            {
                _isInternalUpdating = false;
            }

            NotifyValueChanged(e.OldValue, e.NewValue);
        }

        private void OnInputBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitInputText();
                e.Handled = true;
            }
            else if (e.Key == Key.Up && !ReadOnly)
            {
                Value = Clamp(Value + Step, Minimum, Maximum);
                e.Handled = true;
            }
            else if (e.Key == Key.Down && !ReadOnly)
            {
                Value = Clamp(Value - Step, Minimum, Maximum);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (_inputBox != null) _inputBox.Text = FormatValue(Value);
                e.Handled = true;
            }
        }

        private void CommitInputText()
        {
            if (_inputBox == null || ReadOnly) return;
            var text = _inputBox.Text?.Trim() ?? "";

            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                Value = Clamp(parsed, Minimum, Maximum);
            }
            else if (double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var parsedLocale))
            {
                Value = Clamp(parsedLocale, Minimum, Maximum);
            }
            else
            {
                _inputBox.Text = FormatValue(Value);
            }
        }

        private void NotifyValueChanged(double oldVal, double newVal)
        {
            _isModified = Math.Abs(newVal - DefaultValue) > 1e-6;
            bool isAlt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
            bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            var args = new NumericSliderValueChangedEventArgs(oldVal, newVal, isAlt, isShift, isCtrl);
            ValueChanged?.Invoke(this, args);
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        private string FormatValue(double val)
        {
            return val.ToString(Format, CultureInfo.InvariantCulture);
        }

        #endregion

        #region DP Callbacks

        private static void OnCaptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZNumericSliderEdit ctrl && ctrl._captionLabel != null)
            {
                ctrl._captionLabel.Text = (string)e.NewValue;
            }
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZNumericSliderEdit ctrl)
            {
                var newVal = (double)e.NewValue;
                if (!ctrl._isInternalUpdating)
                {
                    ctrl._isInternalUpdating = true;
                    try
                    {
                        if (ctrl._slider != null) ctrl._slider.Value = newVal;
                        if (ctrl._inputBox != null && !ctrl._inputBox.IsKeyboardFocused)
                        {
                            ctrl._inputBox.Text = ctrl.FormatValue(newVal);
                        }
                    }
                    finally
                    {
                        ctrl._isInternalUpdating = false;
                    }

                    ctrl.NotifyValueChanged((double)e.OldValue, newVal);
                }
            }
        }

        private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZNumericSliderEdit ctrl && ctrl._slider != null)
            {
                ctrl._slider.Minimum = ctrl.Minimum;
                ctrl._slider.Maximum = ctrl.Maximum;
            }
        }

        private static void OnStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZNumericSliderEdit ctrl && ctrl._slider != null)
            {
                ctrl._slider.SmallChange = ctrl.Step;
                ctrl._slider.LargeChange = ctrl.Step * 10;
            }
        }

        private static void OnFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZNumericSliderEdit ctrl && ctrl._inputBox != null)
            {
                ctrl._inputBox.Text = ctrl.FormatValue(ctrl.Value);
            }
        }

        private static void OnReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZNumericSliderEdit ctrl)
            {
                bool ro = (bool)e.NewValue;
                if (ctrl._slider != null) ctrl._slider.IsEnabled = !ro;
                if (ctrl._inputBox != null) ctrl._inputBox.IsEnabled = !ro;
            }
        }

        #endregion
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZNumericSliderEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("NumericSliderEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZNumericSliderEdit instead.")]
    public class NumericSliderEdit : ZNumericSliderEdit { }

    /// <summary>
    /// Legacy alias for <see cref="ZNumericSliderEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroNumericSliderEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZNumericSliderEdit instead.")]
    public class ZeroNumericSliderEdit : ZNumericSliderEdit { }

    #endregion

}
