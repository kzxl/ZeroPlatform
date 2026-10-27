using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;
using ZeroUI.Core.Workflow;

namespace ZeroUI.Wpf.Workflow
{
    public class ZCalendarView : FrameworkElement
    {
        public List<CalendarEvent> Events { get; set; } = new List<CalendarEvent>();
        public DateTime CurrentMonth { get; set; }
        public bool ShowWeekNumbers { get; set; }

        public event EventHandler? DateClicked;
        public event EventHandler? EventClicked;

        public ZCalendarView()
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

    [Obsolete("CalendarView is deprecated and will be removed in 5 release cycles. Please migrate to ZCalendarView instead.")]
    public class CalendarView : ZCalendarView { }

    [Obsolete("ZeroCalendarView is deprecated. Please use ZCalendarView instead.")]
    public class ZeroCalendarView : ZCalendarView { }
}
