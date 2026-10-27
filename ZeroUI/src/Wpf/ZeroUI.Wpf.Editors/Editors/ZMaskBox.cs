using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZeroUI.Core.Input.Masking;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern anti-aliased masked text box for ZeroUI in WPF.
    /// Powered by the headless <see cref="ZeroMaskEngine"/> and <see cref="MaskDefinition"/> engine.
    /// Supports formatted input for IP addresses, MAC addresses, serial numbers, phone numbers, and lot codes.
    /// </summary>
    public class ZMaskBox : TextBox
    {
        private ZeroMaskEngine? _engine;
        private bool _isInternalUpdate = false;

        #region Dependency Properties

        public static readonly DependencyProperty MaskProperty =
            DependencyProperty.Register(nameof(Mask), typeof(string), typeof(ZMaskBox),
                new FrameworkPropertyMetadata(string.Empty, OnMaskChanged));

        public static readonly DependencyProperty PromptCharProperty =
            DependencyProperty.Register(nameof(PromptChar), typeof(char), typeof(ZMaskBox),
                new FrameworkPropertyMetadata('_', OnPromptCharChanged));

        public static readonly DependencyProperty RawTextProperty =
            DependencyProperty.Register(nameof(RawText), typeof(string), typeof(ZMaskBox),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRawTextChanged));

        public static readonly DependencyProperty IsMaskCompletedProperty =
            DependencyProperty.Register(nameof(IsMaskCompleted), typeof(bool), typeof(ZMaskBox),
                new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(ZMaskBox),
                new FrameworkPropertyMetadata(new CornerRadius(5)));

        #endregion

        #region Properties

        public string Mask
        {
            get => (string)GetValue(MaskProperty);
            set => SetValue(MaskProperty, value);
        }

        public char PromptChar
        {
            get => (char)GetValue(PromptCharProperty);
            set => SetValue(PromptCharProperty, value);
        }

        public string RawText
        {
            get => (string)GetValue(RawTextProperty);
            set => SetValue(RawTextProperty, value);
        }

        public bool IsMaskCompleted
        {
            get => (bool)GetValue(IsMaskCompletedProperty);
            private set => SetValue(IsMaskCompletedProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public ZeroMaskEngine? Engine => _engine;

        #endregion

        static ZMaskBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZMaskBox),
                new FrameworkPropertyMetadata(typeof(TextEdit)));
        }

        public ZMaskBox()
        {
            Height = 32;
            VerticalContentAlignment = VerticalAlignment.Center;
            Padding = new Thickness(10, 0, 10, 0);
            SnapsToDevicePixels = true;
        }

        #region DP Callbacks

        private static void OnMaskChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZMaskBox box)
            {
                box.InitializeMask();
            }
        }

        private static void OnPromptCharChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZMaskBox box && box._engine != null)
            {
                box._engine.PromptChar = (char)e.NewValue;
                box.SyncFromEngine(box.CaretIndex);
            }
        }

        private static void OnRawTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZMaskBox box && !box._isInternalUpdate && box._engine != null)
            {
                string raw = (string)e.NewValue ?? string.Empty;
                box._engine.SetRawText(raw.AsSpan());
                box.SyncFromEngine(0);
            }
        }

        #endregion

        private void InitializeMask()
        {
            if (string.IsNullOrEmpty(Mask))
            {
                _engine = null;
                Text = string.Empty;
                RawText = string.Empty;
                IsMaskCompleted = false;
                return;
            }

            var def = new MaskDefinition(Mask);
            _engine = new ZeroMaskEngine(def, PromptChar);
            SyncFromEngine(0);
        }

        private void SyncFromEngine(int desiredCaret)
        {
            if (_engine == null) return;

            _isInternalUpdate = true;
            try
            {
                Text = _engine.GetFormattedText();
                RawText = _engine.GetRawText();
                IsMaskCompleted = _engine.IsComplete;
                CaretIndex = Math.Max(0, Math.Min(Text.Length, desiredCaret));
            }
            finally
            {
                _isInternalUpdate = false;
            }
        }

        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            if (_engine != null && !string.IsNullOrEmpty(e.Text))
            {
                int caret = CaretIndex;
                bool changed = false;

                for (int i = 0; i < e.Text.Length; i++)
                {
                    if (_engine.Insert(e.Text[i], ref caret))
                    {
                        changed = true;
                    }
                }

                if (changed)
                {
                    SyncFromEngine(caret);
                }

                e.Handled = true;
                return;
            }

            base.OnPreviewTextInput(e);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (_engine != null)
            {
                if (e.Key == Key.Back)
                {
                    int caret = CaretIndex;
                    if (_engine.DeleteBackwards(ref caret))
                    {
                        SyncFromEngine(caret);
                    }
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Delete)
                {
                    int caret = CaretIndex;
                    if (_engine.DeleteForward(ref caret))
                    {
                        SyncFromEngine(caret);
                    }
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Space)
                {
                    int caret = CaretIndex;
                    if (_engine.Insert(' ', ref caret))
                    {
                        SyncFromEngine(caret);
                    }
                    e.Handled = true;
                    return;
                }
            }

            base.OnPreviewKeyDown(e);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Convenience alias for <see cref="ZMaskBox"/>.
    /// </summary>
    public class ZMaskedTextBox : ZMaskBox { }

    /// <summary>
    /// Legacy alias for <see cref="ZMaskBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("MaskedTextBox is deprecated and will be removed in 5 release cycles. Please migrate to ZMaskBox instead.")]
    public class MaskedTextBox : ZMaskBox { }

    /// <summary>
    /// Legacy alias for <see cref="ZMaskBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroMaskedTextBox is deprecated and will be removed in 5 release cycles. Please migrate to ZMaskBox instead.")]
    public class ZeroMaskedTextBox : ZMaskBox { }

    #endregion

}
