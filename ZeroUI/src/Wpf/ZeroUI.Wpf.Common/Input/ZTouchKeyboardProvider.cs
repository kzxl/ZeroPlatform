using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZeroUI.Core.Input;

namespace ZeroUI.Wpf.Input
{
    /// <summary>
    /// Attached behavior providing automatic touch-screen virtual keyboard popups
    /// when user clicks or focuses input elements in WPF.
    /// </summary>
    public static class ZTouchKeyboardProvider
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(ZTouchKeyboardProvider),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static readonly DependencyProperty LayoutProperty =
            DependencyProperty.RegisterAttached(
                "Layout",
                typeof(VirtualKeyboardLayout),
                typeof(ZTouchKeyboardProvider),
                new PropertyMetadata(VirtualKeyboardLayout.AlphaNumeric));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        public static VirtualKeyboardLayout GetLayout(DependencyObject obj) => (VirtualKeyboardLayout)obj.GetValue(LayoutProperty);
        public static void SetLayout(DependencyObject obj, VirtualKeyboardLayout value) => obj.SetValue(LayoutProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                if ((bool)e.NewValue)
                {
                    element.PreviewMouseDown += OnElementMouseDown;
                    element.GotFocus += OnElementGotFocus;
                }
                else
                {
                    element.PreviewMouseDown -= OnElementMouseDown;
                    element.GotFocus -= OnElementGotFocus;
                }
            }
        }

        private static void OnElementMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement element && GetIsEnabled(element))
            {
                TriggerPopup(element);
            }
        }

        private static void OnElementGotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is UIElement element && GetIsEnabled(element))
            {
                TriggerPopup(element);
            }
        }

        private static void TriggerPopup(UIElement element)
        {
            var layout = GetLayout(element);

            // Auto-detect numpad for password or numeric named fields if default
            if (layout == VirtualKeyboardLayout.AlphaNumeric && element is FrameworkElement fe)
            {
                string name = (fe.Name ?? "").ToLowerInvariant();
                if (name.Contains("pin") || name.Contains("qty") || name.Contains("price") || name.Contains("numeric"))
                {
                    layout = VirtualKeyboardLayout.Numpad;
                }
            }

            ZVirtualKeyboard.ShowFloatingPopup(element, layout);
        }
    }
}
