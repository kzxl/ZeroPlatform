using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Workflow
{
    public enum TimelineStatus
    {
        Completed,
        InProgress,
        Pending,
        Error
    }

    public class TimelineItem
    {
        public string Title { get; set; } = "Process Node";
        public string Timestamp { get; set; } = "";
        public string? Description { get; set; }
        public TimelineStatus Status { get; set; } = TimelineStatus.Completed;
        public object? Tag { get; set; }

        public TimelineItem() { }

        public TimelineItem(string title, string timestamp, string? description = null, TimelineStatus status = TimelineStatus.Completed)
        {
            Title = title;
            Timestamp = timestamp;
            Description = description;
            Status = status;
        }
    }

    /// <summary>
    /// Modern vertical timeline control for lot tracking, manufacturing journals, and audit trails.
    /// </summary>
    public class ZTimeline : Control
    {
        private readonly List<TimelineItem> _items = new List<TimelineItem>();
        private const double NodeX = 24.0;

        public static readonly DependencyProperty ItemSpacingProperty =
            DependencyProperty.Register(
                nameof(ItemSpacing),
                typeof(int),
                typeof(ZTimeline),
                new FrameworkPropertyMetadata(52, FrameworkPropertyMetadataOptions.AffectsRender));

        public int ItemSpacing
        {
            get => (int)GetValue(ItemSpacingProperty);
            set => SetValue(ItemSpacingProperty, Math.Max(36, value));
        }

        public List<TimelineItem> Items => _items;

        public ZTimeline()
        {
            SnapsToDevicePixels = true;
            Loaded += (s, e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            Unloaded += (s, e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            InvalidateVisual();
        }

        public void Add(string title, string timestamp, string? description = null, TimelineStatus status = TimelineStatus.Completed)
        {
            _items.Add(new TimelineItem(title, timestamp, description, status));
            InvalidateVisual();
        }

        public void Clear()
        {
            _items.Clear();
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            int count = _items.Count;
            if (count == 0) return;

            double startY = 20.0;
            double spacing = ItemSpacing;
            var borderBrush = ZeroWpfTheme.BorderDefault;
            var textPrimaryBrush = ZeroWpfTheme.TextPrimary;
            var textSecondaryBrush = ZeroWpfTheme.TextSecondary;
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // 1. Draw connecting line
            if (count > 1)
            {
                double endY = startY + ((count - 1) * spacing);
                var linePen = new Pen(borderBrush, 2.0);
                linePen.Freeze();
                dc.DrawLine(linePen, new Point(NodeX, startY), new Point(NodeX, endY));
            }

            // 2. Draw nodes and text
            var titleTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            var regularTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

            for (int i = 0; i < count; i++)
            {
                var item = _items[i];
                double currentY = startY + (i * spacing);

                // Colors
                var (nodeColor, ringColor) = GetNodeColors(item.Status);
                var ringBrush = new SolidColorBrush(ringColor);
                ringBrush.Freeze();
                var nodeBrush = new SolidColorBrush(nodeColor);
                nodeBrush.Freeze();

                Point center = new Point(NodeX, currentY);

                // Outer Ring (radius 9)
                dc.DrawEllipse(ringBrush, null, center, 9.0, 9.0);
                // Inner Dot (radius 6)
                dc.DrawEllipse(nodeBrush, null, center, 6.0, 6.0);

                // Text: Title & Timestamp
                double textX = NodeX + 16.0;
                double maxWidth = Math.Max(50.0, ActualWidth - textX - 12.0);

#pragma warning disable CS0618
                var titleText = new FormattedText(
                    item.Title ?? string.Empty,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    titleTypeface,
                    13.0,
                    textPrimaryBrush,
                    dpi);

                dc.DrawText(titleText, new Point(textX, currentY - 8.0));

                if (!string.IsNullOrEmpty(item.Timestamp))
                {
                    var timeText = new FormattedText(
                        item.Timestamp,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        regularTypeface,
                        11.0,
                        textSecondaryBrush,
                        dpi);

                    dc.DrawText(timeText, new Point(textX + titleText.Width + 8.0, currentY - 7.0));
                }

                if (!string.IsNullOrEmpty(item.Description))
                {
                    var descText = new FormattedText(
                        item.Description,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        regularTypeface,
                        11.5,
                        textSecondaryBrush,
                        dpi);

                    dc.DrawText(descText, new Point(textX, currentY + 11.0));
                }
#pragma warning restore CS0618
            }
        }

        private static (Color node, Color ring) GetNodeColors(TimelineStatus status) => status switch
        {
            TimelineStatus.Completed => (Color.FromRgb(16, 185, 129), Color.FromArgb(70, 16, 185, 129)),
            TimelineStatus.InProgress => (Color.FromRgb(59, 130, 246), Color.FromArgb(70, 59, 130, 246)),
            TimelineStatus.Error => (Color.FromRgb(239, 68, 68), Color.FromArgb(70, 239, 68, 68)),
            _ => (Color.FromRgb(156, 163, 175), Color.FromArgb(70, 156, 163, 175))
        };
    }

    #region Backward Compatibility Shims

    [Obsolete("ZeroTimelineStatus is deprecated. Please use TimelineStatus instead.")]
    public enum ZeroTimelineStatus
    {
        Completed = TimelineStatus.Completed,
        InProgress = TimelineStatus.InProgress,
        Pending = TimelineStatus.Pending,
        Error = TimelineStatus.Error
    }

    [Obsolete("ZeroTimelineItem is deprecated. Please use TimelineItem instead.")]
    public class ZeroTimelineItem : TimelineItem
    {
        public ZeroTimelineItem() : base() { }
        public ZeroTimelineItem(string title, string timestamp, string? description = null, ZeroTimelineStatus status = ZeroTimelineStatus.Completed)
            : base(title, timestamp, description, (TimelineStatus)status) { }
    }

    [Obsolete("ZeroTimeline is deprecated and will be removed in 5 release cycles. Please migrate to ZTimeline instead.")]
    public class ZeroTimeline : ZTimeline
    {
        public void Add(string title, string timestamp, string? description = null, ZeroTimelineStatus status = ZeroTimelineStatus.Completed)
            => base.Add(title, timestamp, description, (TimelineStatus)status);
    }

    [Obsolete("Timeline is deprecated and will be removed in 5 release cycles. Please migrate to ZTimeline instead.")]
    public class Timeline : ZTimeline
    {
    }

    #endregion
}
