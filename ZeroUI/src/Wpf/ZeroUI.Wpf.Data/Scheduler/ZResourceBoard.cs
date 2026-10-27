using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;
using ZeroUI.Core.Scheduler;

namespace ZeroUI.Wpf.Scheduler
{
    public class ZResourceBoard : FrameworkElement
    {
        public List<ResourceRow> Resources { get; set; } = new List<ResourceRow>();
        public int TimeSlotWidth { get; set; }
        public bool ShowConflicts { get; set; }

        public event EventHandler? SlotClicked;

        public ZResourceBoard()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged += OnThemeChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged() => InvalidateVisual();

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            // drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        }

        
    }

    [Obsolete("ResourceBoard is deprecated and will be removed in 5 release cycles. Please migrate to ZResourceBoard instead.")]
    public class ResourceBoard : ZResourceBoard { }

    [Obsolete("ZeroResourceBoard is deprecated. Please use ZResourceBoard instead.")]
    public class ZeroResourceBoard : ZResourceBoard { }
}
