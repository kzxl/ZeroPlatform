using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;
using ZeroUI.Core.Scheduler;

namespace ZeroUI.Wpf.Scheduler
{
    public class ZMaintenancePlanner : FrameworkElement
    {
        public List<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
        public WorkOrderViewMode ViewMode { get; set; }

        public event EventHandler? WorkOrderClicked;

        public ZMaintenancePlanner()
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

    [Obsolete("MaintenancePlanner is deprecated and will be removed in 5 release cycles. Please migrate to ZMaintenancePlanner instead.")]
    public class MaintenancePlanner : ZMaintenancePlanner { }

    [Obsolete("ZeroMaintenancePlanner is deprecated. Please use ZMaintenancePlanner instead.")]
    public class ZeroMaintenancePlanner : ZMaintenancePlanner { }
}
