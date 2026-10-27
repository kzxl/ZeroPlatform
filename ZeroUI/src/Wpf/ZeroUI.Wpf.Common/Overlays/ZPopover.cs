using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Overlays
{
    public class ZPopover : Control
    {
        public static void Show(object content, object placement) { }
        
        public ZPopover()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }
        private void OnLoaded(object sender, RoutedEventArgs e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
        private void OnUnloaded(object sender, RoutedEventArgs e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        private void OnThemeChanged() { }
    }

    [Obsolete("ZeroPopover is deprecated and will be removed in 5 release cycles. Please migrate to ZPopover instead.")]
    public class ZeroPopover : ZPopover
    {
    }
}
