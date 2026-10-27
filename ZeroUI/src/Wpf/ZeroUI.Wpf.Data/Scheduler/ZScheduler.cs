using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Globalization;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Data
{
    /// <summary>
    /// Custom rendered scheduler control for WPF.
    /// </summary>
    public class ZScheduler : FrameworkElement
    {
        public static readonly DependencyProperty EventSourceProperty = DependencyProperty.Register(
            nameof(EventSource), typeof(IEnumerable<SchedulerEvent>), typeof(ZScheduler),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ActiveViewProperty = DependencyProperty.Register(
            nameof(ActiveView), typeof(SchedulerView), typeof(ZScheduler),
            new FrameworkPropertyMetadata(SchedulerView.Month, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CurrentDateProperty = DependencyProperty.Register(
            nameof(CurrentDate), typeof(DateTime), typeof(ZScheduler),
            new FrameworkPropertyMetadata(DateTime.Today, FrameworkPropertyMetadataOptions.AffectsRender, OnCurrentDateChanged));

        public static readonly DependencyProperty FirstDayOfWeekProperty = DependencyProperty.Register(
            nameof(FirstDayOfWeek), typeof(DayOfWeek), typeof(ZScheduler),
            new FrameworkPropertyMetadata(DayOfWeek.Sunday, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty WorkDayStartProperty = DependencyProperty.Register(
            nameof(WorkDayStart), typeof(TimeSpan), typeof(ZScheduler),
            new FrameworkPropertyMetadata(TimeSpan.FromHours(8), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty WorkDayEndProperty = DependencyProperty.Register(
            nameof(WorkDayEnd), typeof(TimeSpan), typeof(ZScheduler),
            new FrameworkPropertyMetadata(TimeSpan.FromHours(17), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AllowCreateProperty = DependencyProperty.Register(
            nameof(AllowCreate), typeof(bool), typeof(ZScheduler), new PropertyMetadata(true));

        public static readonly DependencyProperty AllowEditProperty = DependencyProperty.Register(
            nameof(AllowEdit), typeof(bool), typeof(ZScheduler), new PropertyMetadata(true));

        public static readonly DependencyProperty ShowNavigationBarProperty = DependencyProperty.Register(
            nameof(ShowNavigationBar), typeof(bool), typeof(ZScheduler),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public IEnumerable<SchedulerEvent>? EventSource
        {
            get => (IEnumerable<SchedulerEvent>?)GetValue(EventSourceProperty);
            set => SetValue(EventSourceProperty, value);
        }

        public SchedulerView ActiveView
        {
            get => (SchedulerView)GetValue(ActiveViewProperty);
            set => SetValue(ActiveViewProperty, value);
        }

        public DateTime CurrentDate
        {
            get => (DateTime)GetValue(CurrentDateProperty);
            set => SetValue(CurrentDateProperty, value);
        }

        public DayOfWeek FirstDayOfWeek
        {
            get => (DayOfWeek)GetValue(FirstDayOfWeekProperty);
            set => SetValue(FirstDayOfWeekProperty, value);
        }

        public TimeSpan WorkDayStart
        {
            get => (TimeSpan)GetValue(WorkDayStartProperty);
            set => SetValue(WorkDayStartProperty, value);
        }

        public TimeSpan WorkDayEnd
        {
            get => (TimeSpan)GetValue(WorkDayEndProperty);
            set => SetValue(WorkDayEndProperty, value);
        }

        public bool AllowCreate
        {
            get => (bool)GetValue(AllowCreateProperty);
            set => SetValue(AllowCreateProperty, value);
        }

        public bool AllowEdit
        {
            get => (bool)GetValue(AllowEditProperty);
            set => SetValue(AllowEditProperty, value);
        }

        public bool ShowNavigationBar
        {
            get => (bool)GetValue(ShowNavigationBarProperty);
            set => SetValue(ShowNavigationBarProperty, value);
        }

        public Func<SchedulerEvent, Color>? EventColorResolver { get; set; }

#pragma warning disable CS0067
        public event EventHandler<SchedulerEventArgs>? EventCreated;
        public event EventHandler<SchedulerEventArgs>? EventModified;
        public event EventHandler<SchedulerEventArgs>? EventDeleted;
        public event EventHandler<SchedulerEventArgs>? EventDoubleClicked;
#pragma warning restore CS0067
        public event EventHandler<DateChangedEventArgs>? DateChanged;

        private static void OnCurrentDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZScheduler scheduler && e.OldValue is DateTime oldDate && e.NewValue is DateTime newDate)
            {
                scheduler.DateChanged?.Invoke(scheduler, new DateChangedEventArgs(oldDate, newDate));
            }
        }

        public ZScheduler()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            ClipToBounds = true;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged += OnThemeChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            InvalidateVisual();
        }

        public void NavigateToday() => CurrentDate = DateTime.Today;
        public void NavigateNext() => CurrentDate = ActiveView == SchedulerView.Month ? CurrentDate.AddMonths(1) : CurrentDate.AddDays(ActiveView == SchedulerView.Week ? 7 : 1);
        public void NavigatePrevious() => CurrentDate = ActiveView == SchedulerView.Month ? CurrentDate.AddMonths(-1) : CurrentDate.AddDays(ActiveView == SchedulerView.Week ? -7 : -1);

#if NETFRAMEWORK
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
        }
#else
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush, pixelsPerDip);
        }
#endif

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, bounds);

#if NETFRAMEWORK
            double dpi = 1.0;
#else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
#endif

            double yOffset = 0;
            if (ShowNavigationBar)
            {
                yOffset = 40;
                DrawNavigationBar(dc, new Rect(0, 0, ActualWidth, yOffset), dpi);
            }

            var viewRect = new Rect(0, yOffset, ActualWidth, Math.Max(0, ActualHeight - yOffset));
            
            switch (ActiveView)
            {
                case SchedulerView.Month:
                    DrawMonthView(dc, viewRect);
                    break;
                case SchedulerView.Day:
                case SchedulerView.Week:
                case SchedulerView.WorkWeek:
                case SchedulerView.Agenda:
                    break;
            }
        }

        private void DrawNavigationBar(DrawingContext dc, Rect rect, double dpi)
        {
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, rect);
            dc.DrawLine(ZeroWpfTheme.BorderPen, new Point(0, rect.Bottom - 1), new Point(ActualWidth, rect.Bottom - 1));
            
            var formattedText = CreateFormattedText(
                CurrentDate.ToString("MMMM yyyy"),
                ZeroWpfTheme.BoldTypeface,
                14.0,
                ZeroWpfTheme.TextPrimary,
                dpi);
                
            dc.DrawText(formattedText, new Point(10, 10));
        }

        private void DrawMonthView(DrawingContext dc, Rect rect)
        {
            double cellW = rect.Width / 7.0;
            double cellH = rect.Height / 6.0;

            for (int r = 0; r < 6; r++)
            {
                for (int c = 0; c < 7; c++)
                {
                    double x = rect.X + c * cellW;
                    double y = rect.Y + r * cellH;
                    dc.DrawRectangle(null, ZeroWpfTheme.BorderPen, new Rect(x, y, cellW, cellH));
                }
            }
        }
    }

    [Obsolete("Use ZScheduler instead")]
    public class ZeroSchedulerControl : ZScheduler { }
}
