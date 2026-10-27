using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Industrial 4-octet segmented IPv4 address editor for ZeroUI.Wpf.
    /// Provides automatic octet cursor advancement on period (.) or space, backspace navigation,
    /// clipboard address parsing, and bidirectional IPAddress/string binding.
    /// </summary>
    public class ZIPAddressEdit : Control, IZeroEditor
    {
        public static readonly DependencyProperty AddressProperty =
            DependencyProperty.Register(
                nameof(Address),
                typeof(IPAddress),
                typeof(ZIPAddressEdit),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnAddressChanged));

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(ZIPAddressEdit),
                new FrameworkPropertyMetadata("0.0.0.0", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(string),
                typeof(ZIPAddressEdit),
                new FrameworkPropertyMetadata("0.0.0.0", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public static readonly DependencyProperty EditValueProperty =
            DependencyProperty.Register(
                nameof(EditValue),
                typeof(object),
                typeof(ZIPAddressEdit),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnEditValueChanged));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(ZIPAddressEdit),
                new PropertyMetadata(new CornerRadius(5)));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(
                nameof(ReadOnly),
                typeof(bool),
                typeof(ZIPAddressEdit),
                new PropertyMetadata(false));

        public static readonly DependencyProperty IsModifiedProperty =
            DependencyProperty.Register(
                nameof(IsModified),
                typeof(bool),
                typeof(ZIPAddressEdit),
                new PropertyMetadata(false));

        public event EventHandler? EditValueChanged;

        public IPAddress? Address
        {
            get => (IPAddress?)GetValue(AddressProperty);
            set => SetValue(AddressProperty, value);
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public object? EditValue
        {
            get => GetValue(EditValueProperty);
            set => SetValue(EditValueProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public bool IsModified
        {
            get => (bool)GetValue(IsModifiedProperty);
            set => SetValue(IsModifiedProperty, value);
        }

        private readonly IPAddressModel _model = new IPAddressModel();
        private TextBox? _txtOctet1;
        private TextBox? _txtOctet2;
        private TextBox? _txtOctet3;
        private TextBox? _txtOctet4;
        private bool _isUpdating;

        public IPAddressModel Model => _model;

        static ZIPAddressEdit()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZIPAddressEdit), new FrameworkPropertyMetadata(typeof(ZIPAddressEdit)));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _txtOctet1 = GetTemplateChild("PART_Octet1") as TextBox;
            _txtOctet2 = GetTemplateChild("PART_Octet2") as TextBox;
            _txtOctet3 = GetTemplateChild("PART_Octet3") as TextBox;
            _txtOctet4 = GetTemplateChild("PART_Octet4") as TextBox;

            SetupOctetBox(_txtOctet1, null, _txtOctet2, 0);
            SetupOctetBox(_txtOctet2, _txtOctet1, _txtOctet3, 1);
            SetupOctetBox(_txtOctet3, _txtOctet2, _txtOctet4, 2);
            SetupOctetBox(_txtOctet4, _txtOctet3, null, 3);

            UpdateOctetBoxesFromText(Text);
        }

        private void SetupOctetBox(TextBox? box, TextBox? prev, TextBox? next, int octetIndex)
        {
            if (box == null) return;

            box.PreviewTextInput += (s, e) =>
            {
                if (ReadOnly) { e.Handled = true; return; }

                if (e.Text == "." || e.Text == " ")
                {
                    e.Handled = true;
                    if (next != null)
                    {
                        next.Focus();
                        next.SelectAll();
                    }
                    return;
                }

                foreach (char c in e.Text)
                {
                    if (!char.IsDigit(c))
                    {
                        e.Handled = true;
                        return;
                    }
                }
            };

            box.PreviewKeyDown += (s, e) =>
            {
                if (ReadOnly) return;

                if (e.Key == Key.Back && box.CaretIndex == 0 && box.SelectionLength == 0)
                {
                    if (prev != null)
                    {
                        prev.Focus();
                        prev.CaretIndex = prev.Text.Length;
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.Right && box.CaretIndex == box.Text.Length && next != null)
                {
                    next.Focus();
                    next.CaretIndex = 0;
                    e.Handled = true;
                }
                else if (e.Key == Key.Left && box.CaretIndex == 0 && prev != null)
                {
                    prev.Focus();
                    prev.CaretIndex = prev.Text.Length;
                    e.Handled = true;
                }
            };

            box.TextChanged += (s, e) =>
            {
                if (_isUpdating) return;

                if (int.TryParse(box.Text, out int val))
                {
                    _model.SetOctet(octetIndex, val);
                    if (val > 255)
                    {
                        box.Text = "255";
                        box.CaretIndex = box.Text.Length;
                    }
                    else if (val < 0)
                    {
                        box.Text = "0";
                    }

                    // Auto advance if 3 digits or if value > 25 and another digit would exceed 255
                    if (box.Text.Length == 3 || (val >= 26 && box.Text.Length >= 2))
                    {
                        if (next != null && box.IsFocused)
                        {
                            next.Focus();
                            next.SelectAll();
                        }
                    }
                }

                SyncTextFromOctets();
            };

            // Clipboard paste interceptor
            DataObject.AddPastingHandler(box, (s, e) =>
            {
                if (ReadOnly) { e.CancelCommand(); return; }

                if (e.DataObject.GetDataPresent(DataFormats.Text))
                {
                    string pasteText = (string)e.DataObject.GetData(DataFormats.Text);
                    if (_model.TrySetFromText(pasteText.Trim()))
                    {
                        e.CancelCommand();
                        UpdateBoxesFromModel();
                        IsModified = true;
                        EditValueChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            });
        }

        private void SyncTextFromOctets()
        {
            if (_isUpdating) return;

            _isUpdating = true;
            try
            {
                Text = _model.Text;
                Value = _model.Text;
                Address = _model.Address;
                EditValue = _model.Address;
                IsModified = true;
                EditValueChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void UpdateOctetBoxesFromText(string text)
        {
            _model.TrySetFromText(string.IsNullOrEmpty(text) ? "0.0.0.0" : text);
            UpdateBoxesFromModel();
        }

        private void UpdateBoxesFromModel()
        {
            _isUpdating = true;
            try
            {
                if (_txtOctet1 != null) _txtOctet1.Text = _model[0].ToString();
                if (_txtOctet2 != null) _txtOctet2.Text = _model[1].ToString();
                if (_txtOctet3 != null) _txtOctet3.Text = _model[2].ToString();
                if (_txtOctet4 != null) _txtOctet4.Text = _model[3].ToString();
                Text = _model.Text;
                Value = Text;
                Address = _model.Address;
                EditValue = Address;
            }
            finally
            {
                _isUpdating = false;
            }
        }

        public void SetIPAddress(IPAddress address)
        {
            _model.Address = address;
            UpdateBoxesFromModel();
        }

        private static void OnAddressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZIPAddressEdit edit && !edit._isUpdating && e.NewValue is IPAddress ip)
            {
                edit.SetIPAddress(ip);
            }
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZIPAddressEdit edit && !edit._isUpdating && e.NewValue is string s)
            {
                if (edit.Value != s) edit.Value = s;
                edit.UpdateOctetBoxesFromText(s);
            }
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZIPAddressEdit edit && !edit._isUpdating && e.NewValue is string s && edit.Text != s)
            {
                edit.Text = s;
            }
        }

        private static void OnEditValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not IPAddressEdit edit || edit._isUpdating) return;

            if (e.NewValue is IPAddress ip)
            {
                edit.SetIPAddress(ip);
            }
            else if (e.NewValue is string s && IPAddress.TryParse(s, out var parsed))
            {
                edit.SetIPAddress(parsed);
            }
        }

        public void Clear()
        {
            UpdateOctetBoxesFromText("0.0.0.0");
            SyncTextFromOctets();
            IsModified = false;
        }

        public void Reset()
        {
            Clear();
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZIPAddressEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("IPAddressEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZIPAddressEdit instead.")]
    public class IPAddressEdit : ZIPAddressEdit { }

    /// <summary>
    /// Legacy alias for <see cref="ZIPAddressEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroIPAddressEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZIPAddressEdit instead.")]
    public class ZeroIPAddressEdit : ZIPAddressEdit { }

    #endregion

}
