using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Enhanced Fluent TextBox with placeholder / watermark text, leading/trailing icons,
    /// 1-click clear button, and animated focus borders.
    /// </summary>
    public class ZTextBox : TextBox, IZeroEditor
    {
        public static readonly DependencyProperty EditValueProperty =
            DependencyProperty.Register(nameof(EditValue), typeof(object), typeof(ZTextBox),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnEditValueChanged));

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(ZTextBox),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty LeadingTextProperty =
            DependencyProperty.Register(nameof(LeadingText), typeof(string), typeof(ZTextBox),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ShowClearButtonProperty =
            DependencyProperty.Register(nameof(ShowClearButton), typeof(bool), typeof(ZTextBox),
                new PropertyMetadata(true));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(ZTextBox),
                new PropertyMetadata(new CornerRadius(5)));

        public static readonly DependencyProperty IsModifiedProperty =
            DependencyProperty.Register(nameof(IsModified), typeof(bool), typeof(ZTextBox),
                new PropertyMetadata(false));

        public event EventHandler? EditValueChanged;

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

        public void Reset()
        {
            Text = string.Empty;
            EditValue = null;
            IsModified = false;
        }

        public new void Clear()
        {
            base.Clear();
            EditValue = null;
            IsModified = false;
        }

        private static void OnEditValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZTextBox edit)
            {
                string text = e.NewValue?.ToString() ?? string.Empty;
                if (edit.Text != text)
                {
                    edit.Text = text;
                }
                edit.EditValueChanged?.Invoke(edit, EventArgs.Empty);
            }
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            base.OnTextChanged(e);
            if (EditValue?.ToString() != Text)
            {
                EditValue = Text;
            }
            IsModified = true;
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public string LeadingText
        {
            get => (string)GetValue(LeadingTextProperty);
            set => SetValue(LeadingTextProperty, value);
        }

        public bool ShowClearButton
        {
            get => (bool)GetValue(ShowClearButtonProperty);
            set => SetValue(ShowClearButtonProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        static ZTextBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZTextBox),
                new FrameworkPropertyMetadata(typeof(TextBox)));
        }

        public ZTextBox()
        {
            Height = 32;
            VerticalContentAlignment = VerticalAlignment.Center;
            Padding = new Thickness(10, 0, 10, 0);
            SnapsToDevicePixels = true;
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Convenience alias for <see cref="ZTextBox"/>.
    /// </summary>
    public class ZTextEdit : ZTextBox { }

    /// <summary>
    /// Legacy alias for <see cref="ZTextBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("TextEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZTextBox instead.")]
    public class TextEdit : ZTextBox { }

    /// <summary>
    /// Legacy alias for <see cref="ZTextBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroTextBox is deprecated and will be removed in 5 release cycles. Please migrate to ZTextBox instead.")]
    public class ZeroTextBox : ZTextBox { }

    /// <summary>
    /// Legacy alias for <see cref="ZTextBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroTextEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZTextBox instead.")]
    public class ZeroTextEdit : ZTextBox { }

    #endregion

}
