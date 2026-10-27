using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// High-precision numeric stepper and spin box editor for industrial tolerances, setpoints,
    /// currencies, percentages, and quantities.
    /// Features unit prefixes/suffixes, acceleration on hold, decimal places, and mouse wheel support.
    /// </summary>
    [TemplatePart(Name = PartTextBox, Type = typeof(TextBox))]
    [TemplatePart(Name = PartUpButton, Type = typeof(RepeatButton))]
    [TemplatePart(Name = PartDownButton, Type = typeof(RepeatButton))]
    public class ZSpinEdit : Control, IZeroEditor
    {
        private const string PartTextBox = "PART_TextBox";
        private const string PartUpButton = "PART_UpButton";
        private const string PartDownButton = "PART_DownButton";

        private TextBox? _textBox;
        private RepeatButton? _upButton;
        private RepeatButton? _downButton;
        private bool _isInternalTextChange;

        public static readonly DependencyProperty EditValueProperty =
            DependencyProperty.Register(nameof(EditValue), typeof(object), typeof(ZSpinEdit),
                new FrameworkPropertyMetadata(0m, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnEditValueChanged));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(decimal), typeof(ZSpinEdit),
                new FrameworkPropertyMetadata(0m, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceValue));

        public static readonly DependencyProperty MinValueProperty =
            DependencyProperty.Register(nameof(MinValue), typeof(decimal), typeof(ZSpinEdit),
                new PropertyMetadata(-1000000000m, OnMinMaxChanged));

        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register(nameof(MaxValue), typeof(decimal), typeof(ZSpinEdit),
                new PropertyMetadata(1000000000m, OnMinMaxChanged));

        public static readonly DependencyProperty StepProperty =
            DependencyProperty.Register(nameof(Step), typeof(decimal), typeof(ZSpinEdit),
                new PropertyMetadata(1m));

        public static readonly DependencyProperty DecimalPlacesProperty =
            DependencyProperty.Register(nameof(DecimalPlaces), typeof(int), typeof(ZSpinEdit),
                new PropertyMetadata(0, OnFormattingChanged));

        public static readonly DependencyProperty PrefixProperty =
            DependencyProperty.Register(nameof(Prefix), typeof(string), typeof(ZSpinEdit),
                new PropertyMetadata(string.Empty, OnFormattingChanged));

        public static readonly DependencyProperty SuffixProperty =
            DependencyProperty.Register(nameof(Suffix), typeof(string), typeof(ZSpinEdit),
                new PropertyMetadata(string.Empty, OnFormattingChanged));

        public static readonly DependencyProperty ThousandsSeparatorProperty =
            DependencyProperty.Register(nameof(ThousandsSeparator), typeof(bool), typeof(ZSpinEdit),
                new PropertyMetadata(true, OnFormattingChanged));

        public static readonly DependencyProperty EnableMathEvaluationProperty =
            DependencyProperty.Register(nameof(EnableMathEvaluation), typeof(bool), typeof(ZSpinEdit),
                new PropertyMetadata(true));

        public static readonly DependencyProperty TextAlignmentProperty =
            DependencyProperty.Register(nameof(TextAlignment), typeof(TextAlignment), typeof(ZSpinEdit),
                new PropertyMetadata(TextAlignment.Right));

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(ZSpinEdit),
                new PropertyMetadata(false));

        public static readonly DependencyProperty IsModifiedProperty =
            DependencyProperty.Register(nameof(IsModified), typeof(bool), typeof(ZSpinEdit),
                new PropertyMetadata(false));

        public static readonly RoutedEvent ValueChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(ValueChanged), RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<decimal>), typeof(ZSpinEdit));

        public event EventHandler? EditValueChanged;

        public event RoutedPropertyChangedEventHandler<decimal> ValueChanged
        {
            add => AddHandler(ValueChangedEvent, value);
            remove => RemoveHandler(ValueChangedEvent, value);
        }

        public object? EditValue
        {
            get => GetValue(EditValueProperty);
            set => SetValue(EditValueProperty, value);
        }

        public bool IsModified
        {
            get => (bool)GetValue(IsModifiedProperty);
            set => SetValue(IsModifiedProperty, value);
        }

        public bool ReadOnly
        {
            get => IsReadOnly;
            set => IsReadOnly = value;
        }

        public bool EnableMathEvaluation
        {
            get => (bool)GetValue(EnableMathEvaluationProperty);
            set => SetValue(EnableMathEvaluationProperty, value);
        }

        public void Reset()
        {
            Value = MinValue;
            EditValue = MinValue;
            IsModified = false;
        }

        public void Clear() => Reset();

        private static void OnEditValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZSpinEdit edit)
            {
                if (e.NewValue is decimal dec)
                {
                    if (edit.Value != dec) edit.Value = dec;
                }
                else if (e.NewValue != null && decimal.TryParse(e.NewValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed))
                {
                    if (edit.Value != parsed) edit.Value = parsed;
                }
                edit.EditValueChanged?.Invoke(edit, EventArgs.Empty);
            }
        }

        public decimal Value
        {
            get => (decimal)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public decimal MinValue
        {
            get => (decimal)GetValue(MinValueProperty);
            set => SetValue(MinValueProperty, value);
        }

        public decimal MaxValue
        {
            get => (decimal)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        public decimal Step
        {
            get => (decimal)GetValue(StepProperty);
            set => SetValue(StepProperty, value);
        }

        public int DecimalPlaces
        {
            get => (int)GetValue(DecimalPlacesProperty);
            set => SetValue(DecimalPlacesProperty, value);
        }

        public string Prefix
        {
            get => (string)GetValue(PrefixProperty);
            set => SetValue(PrefixProperty, value);
        }

        public string Suffix
        {
            get => (string)GetValue(SuffixProperty);
            set => SetValue(SuffixProperty, value);
        }

        public bool ThousandsSeparator
        {
            get => (bool)GetValue(ThousandsSeparatorProperty);
            set => SetValue(ThousandsSeparatorProperty, value);
        }

        public TextAlignment TextAlignment
        {
            get => (TextAlignment)GetValue(TextAlignmentProperty);
            set => SetValue(TextAlignmentProperty, value);
        }

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        static ZSpinEdit()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZSpinEdit),
                new FrameworkPropertyMetadata(typeof(ZeroNumericBox)));
        }

        public ZSpinEdit()
        {
            Height = 32;
            MinWidth = 120;
            SnapsToDevicePixels = true;
            Focusable = true;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_textBox != null)
            {
                _textBox.TextChanged -= OnTextBoxTextChanged;
                _textBox.PreviewKeyDown -= OnTextBoxKeyDown;
                _textBox.LostFocus -= OnTextBoxLostFocus;
                _textBox.PreviewTextInput -= OnTextBoxPreviewTextInput;
            }
            if (_upButton != null) _upButton.Click -= OnUpButtonClick;
            if (_downButton != null) _downButton.Click -= OnDownButtonClick;

            _textBox = GetTemplateChild(PartTextBox) as TextBox;
            _upButton = GetTemplateChild(PartUpButton) as RepeatButton;
            _downButton = GetTemplateChild(PartDownButton) as RepeatButton;

            if (_textBox != null)
            {
                _textBox.TextChanged += OnTextBoxTextChanged;
                _textBox.PreviewKeyDown += OnTextBoxKeyDown;
                _textBox.LostFocus += OnTextBoxLostFocus;
                _textBox.PreviewTextInput += OnTextBoxPreviewTextInput;
                DataObject.AddPastingHandler(_textBox, OnTextBoxPasting);
            }
            if (_upButton != null) _upButton.Click += OnUpButtonClick;
            if (_downButton != null) _downButton.Click += OnDownButtonClick;

            UpdateFormattedText();
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (IsReadOnly || !IsEnabled) return;

            if (e.Delta > 0)
            {
                Increment();
            }
            else if (e.Delta < 0)
            {
                Decrement();
            }
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (IsReadOnly || !IsEnabled) return;

            if (e.Key == Key.Up)
            {
                Increment();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                Decrement();
                e.Handled = true;
            }
            else if (e.Key == Key.PageUp)
            {
                Increment(Step * 10);
                e.Handled = true;
            }
            else if (e.Key == Key.PageDown)
            {
                Decrement(Step * 10);
                e.Handled = true;
            }
        }

        public void Increment(decimal? customStep = null)
        {
            decimal delta = customStep ?? Step;
            decimal newValue = Math.Min(MaxValue, Value + delta);
            if (newValue != Value)
            {
                Value = newValue;
            }
        }

        public void Decrement(decimal? customStep = null)
        {
            decimal delta = customStep ?? Step;
            decimal newValue = Math.Max(MinValue, Value - delta);
            if (newValue != Value)
            {
                Value = newValue;
            }
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var box = (SpinEdit)d;
            if (!Equals(box.EditValue, e.NewValue))
            {
                box.EditValue = e.NewValue;
            }
            box.IsModified = true;
            box.UpdateFormattedText();
            box.RaiseEvent(new RoutedPropertyChangedEventArgs<decimal>((decimal)e.OldValue, (decimal)e.NewValue, ValueChangedEvent));
            box.EditValueChanged?.Invoke(box, EventArgs.Empty);
        }

        private static object CoerceValue(DependencyObject d, object baseValue)
        {
            var box = (SpinEdit)d;
            if (baseValue is decimal val)
            {
                return Math.Max(box.MinValue, Math.Min(box.MaxValue, val));
            }
            return baseValue;
        }

        private static void OnMinMaxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var box = (SpinEdit)d;
            box.CoerceValue(ValueProperty);
        }

        private static void OnFormattingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var box = (SpinEdit)d;
            box.UpdateFormattedText();
        }

        private void OnUpButtonClick(object sender, RoutedEventArgs e) => Increment();
        private void OnDownButtonClick(object sender, RoutedEventArgs e) => Decrement();

        private void OnTextBoxPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow digits, minus sign, decimal separator, and arithmetic operators if math evaluation is enabled
            string decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            foreach (char c in e.Text)
            {
                if (char.IsDigit(c) || c == '-' || c.ToString() == decimalSeparator) continue;

                if (EnableMathEvaluation && (c == '+' || c == '*' || c == '/' || c == '(' || c == ')' || c == '^' || c == '%' || char.IsWhiteSpace(c)))
                {
                    continue;
                }

                e.Handled = true;
                return;
            }
        }

        private void OnTextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!decimal.TryParse(CleanNumericString(text), NumberStyles.Any, CultureInfo.CurrentCulture, out _))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitTextValue();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                UpdateFormattedText();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                Increment();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                Decrement();
                e.Handled = true;
            }
        }

        private void OnTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            CommitTextValue();
            UpdateFormattedText();
        }

        private void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInternalTextChange) return;

            // Optional: live parsing if valid
            if (_textBox != null && _textBox.IsFocused)
            {
                string raw = CleanNumericString(_textBox.Text);
                if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal parsed))
                {
                    decimal clamped = Math.Max(MinValue, Math.Min(MaxValue, parsed));
                    if (clamped != Value)
                    {
                        Value = clamped;
                    }
                }
            }
        }

        private void CommitTextValue()
        {
            if (_textBox == null) return;
            string raw = CleanNumericString(_textBox.Text);

            if (EnableMathEvaluation && MathExpressionParser.TryEvaluate(raw, out decimal mathResult))
            {
                Value = Math.Max(MinValue, Math.Min(MaxValue, mathResult));
            }
            else if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal parsed))
            {
                Value = Math.Max(MinValue, Math.Min(MaxValue, parsed));
            }
            else
            {
                UpdateFormattedText();
            }
        }

        private string CleanNumericString(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "0";
            string clean = text.Trim();
            if (!string.IsNullOrEmpty(Prefix) && clean.StartsWith(Prefix.Trim(), StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(Prefix.Trim().Length).Trim();
            if (!string.IsNullOrEmpty(Suffix) && clean.EndsWith(Suffix.Trim(), StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(0, clean.Length - Suffix.Trim().Length).Trim();

            return clean;
        }

        private void UpdateFormattedText()
        {
            if (_textBox == null) return;

            _isInternalTextChange = true;
            try
            {
                string format;
                if (DecimalPlaces > 0)
                {
                    format = ThousandsSeparator ? $"#,##0.{new string('0', DecimalPlaces)}" : $"0.{new string('0', DecimalPlaces)}";
                }
                else
                {
                    format = ThousandsSeparator ? "#,##0" : "0";
                }

                string numberStr = Value.ToString(format, CultureInfo.CurrentCulture);
                string fullText = $"{Prefix}{numberStr}{Suffix}";
                _textBox.Text = fullText;
            }
            finally
            {
                _isInternalTextChange = false;
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSpinEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SpinEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZSpinEdit instead.")]
    public class SpinEdit : ZSpinEdit { }

    /// <summary>
    /// Legacy alias for <see cref="ZSpinEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroSpinEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZSpinEdit instead.")]
    public class ZeroSpinEdit : ZSpinEdit { }

    /// <summary>
    /// Legacy alias for <see cref="ZSpinEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroNumericBox is deprecated and will be removed in 5 release cycles. Please migrate to ZSpinEdit instead.")]
    public class ZeroNumericBox : ZSpinEdit { }

    #endregion

}
