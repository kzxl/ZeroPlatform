using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;
using ZeroUI.Core.Scheduler;

namespace ZeroUI.Wpf.Scheduler
{
    public class ZShiftPlanner : FrameworkElement
    {
        public List<Employee> Employees { get; set; } = new List<Employee>();
        public List<Shift> Shifts { get; set; } = new List<Shift>();
        public TimeSpan DateRange { get; set; }

        public event EventHandler? ShiftChanged;

        public ZShiftPlanner()
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

    [Obsolete("ShiftPlanner is deprecated and will be removed in 5 release cycles. Please migrate to ZShiftPlanner instead.")]
    public class ShiftPlanner : ZShiftPlanner { }

    [Obsolete("ZeroShiftPlanner is deprecated. Please use ZShiftPlanner instead.")]
    public class ZeroShiftPlanner : ZShiftPlanner { }
}
