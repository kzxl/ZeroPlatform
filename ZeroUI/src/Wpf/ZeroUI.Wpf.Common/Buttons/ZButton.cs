using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    public enum ButtonVariant
    {
        Primary,
        Secondary,
        Success,
        Danger,
        Ghost
    }

    [Obsolete("ZeroButtonVariant is deprecated. Use ButtonVariant instead.")]
    public enum ZeroButtonVariant
    {
        Primary = ButtonVariant.Primary,
        Secondary = ButtonVariant.Secondary,
        Success = ButtonVariant.Success,
        Danger = ButtonVariant.Danger,
        Ghost = ButtonVariant.Ghost
    }

    /// <summary>
    /// Modern styled button adhering to AgentOption WPF UI standards.
    /// </summary>
    public class ZButton : Button
    {
        public static readonly DependencyProperty VariantProperty =
            DependencyProperty.Register(
                nameof(Variant),
                typeof(ButtonVariant),
                typeof(ZButton),
                new FrameworkPropertyMetadata(ButtonVariant.Primary, OnVariantChanged));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(ZButton),
                new FrameworkPropertyMetadata(new CornerRadius(6)));

        public ButtonVariant Variant
        {
            get => (ButtonVariant)GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public ZButton()
        {
            Height = 32;
            Padding = new Thickness(14, 6, 14, 6);
            FontSize = 12.5;
            FontWeight = FontWeights.SemiBold;
            Cursor = Cursors.Hand;
            Style = ZeroWpfStyles.ButtonStyle;
            ApplyStyle();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static void OnVariantChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZButton btn) btn.ApplyStyle();
        }

        private void ApplyStyle()
        {
            switch (Variant)
            {
                case ButtonVariant.Primary:
                    Background = ZeroWpfTheme.PrimaryAccent;
                    Foreground = Brushes.White;
                    BorderBrush = ZeroWpfTheme.PrimaryAccentDark;
                    BorderThickness = new Thickness(1);
                    break;
                case ButtonVariant.Secondary:
                    Background = ZeroWpfTheme.BgInput;
                    Foreground = ZeroWpfTheme.TextPrimary;
                    BorderBrush = ZeroWpfTheme.BorderDefault;
                    BorderThickness = new Thickness(1);
                    break;
                case ButtonVariant.Success:
                    Background = ZeroWpfTheme.SuccessAccent;
                    Foreground = ZeroWpfTheme.IsDark ? Brushes.Black : Brushes.White;
                    BorderBrush = Brushes.Transparent;
                    BorderThickness = new Thickness(0);
                    break;
                case ButtonVariant.Danger:
                    Background = ZeroWpfTheme.DangerAccent;
                    Foreground = Brushes.White;
                    BorderBrush = Brushes.Transparent;
                    BorderThickness = new Thickness(0);
                    break;
                case ButtonVariant.Ghost:
                    Background = Brushes.Transparent;
                    Foreground = ZeroWpfTheme.TextPrimary;
                    BorderBrush = Brushes.Transparent;
                    BorderThickness = new Thickness(0);
                    break;
            }
        }
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged += ApplyStyle;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= ApplyStyle;
    }
}

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZButton"/>.
    /// </summary>
    [Obsolete("SimpleButton is deprecated and will be removed in 5 release cycles. Please migrate to ZButton instead.")]
    public class SimpleButton : ZButton
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZButton"/>.
    /// Preserved for 100% backward compatibility.
    /// </summary>
    [Obsolete("ZeroButton is deprecated and will be removed in 5 release cycles. Please migrate to ZButton instead.")]
    public class ZeroButton : ZButton
    {
    }
}
