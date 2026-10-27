using System;
using System.Collections.Generic;
using System.Drawing;
using ZeroCharts.Axis;

namespace ZeroCharts.Interaction
{
    public enum LegendPlacement
    {
        None,
        Top,
        Bottom,
        Left,
        Right
    }

    public class LegendEntry
    {
        public string Title { get; set; }
        public Color Color { get; set; }
        public bool IsVisible { get; set; }
        public Action<bool>? OnVisibilityToggled { get; set; }
        public RectangleF ClickBounds { get; set; }

        public LegendEntry(string title, Color color, bool isVisible, Action<bool>? onVisibilityToggled = null)
        {
            Title = title ?? string.Empty;
            Color = color;
            IsVisible = isVisible;
            OnVisibilityToggled = onVisibilityToggled;
        }
    }

    /// <summary>
    /// Interactive legend box supporting automatic item arrangement and mouse-click series toggling.
    /// </summary>
    public class ChartLegend
    {
        public LegendPlacement Placement { get; set; } = LegendPlacement.Top;
        public Color BackgroundColor { get; set; } = Color.FromArgb(200, 20, 24, 32);
        public Color BorderColor { get; set; } = ChartPalette.GridLine;
        public Color TextColor { get; set; } = ChartPalette.AxisText;
        public Color InactiveTextColor { get; set; } = Color.FromArgb(120, ChartPalette.AxisText);

        private readonly List<LegendEntry> _entries = new List<LegendEntry>();
        public IReadOnlyList<LegendEntry> Entries => _entries;

        public void Clear()
        {
            _entries.Clear();
        }

        public void AddEntry(string title, Color color, bool isVisible, Action<bool>? onVisibilityToggled = null)
        {
            _entries.Add(new LegendEntry(title, color, isVisible, onVisibilityToggled));
        }

        public bool HandleClick(Point mousePoint)
        {
            if (Placement == LegendPlacement.None) return false;

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.ClickBounds.Contains(mousePoint))
                {
                    entry.IsVisible = !entry.IsVisible;
                    entry.OnVisibilityToggled?.Invoke(entry.IsVisible);
                    return true;
                }
            }
            return false;
        }

        public RectangleF MeasureAndLayout(Graphics g, Font font, RectangleF availableArea)
        {
            if (Placement == LegendPlacement.None || _entries.Count == 0)
                return RectangleF.Empty;

            float badgeSize = 10f;
            float itemSpacing = 16f;
            float padding = 6f;

            if (Placement == LegendPlacement.Top || Placement == LegendPlacement.Bottom)
            {
                // Horizontal layout
                float totalWidth = padding * 2;
                float maxHeight = 0;

                for (int i = 0; i < _entries.Count; i++)
                {
                    var sz = g.MeasureString(_entries[i].Title, font);
                    totalWidth += badgeSize + 6f + sz.Width + itemSpacing;
                    if (sz.Height > maxHeight) maxHeight = sz.Height;
                }

                float boxHeight = Math.Max(badgeSize, maxHeight) + padding * 2;
                float boxWidth = Math.Min(totalWidth, availableArea.Width);
                float boxX = availableArea.Left + (availableArea.Width - boxWidth) * 0.5f;
                float boxY = Placement == LegendPlacement.Top ? availableArea.Top : availableArea.Bottom - boxHeight;

                float currX = boxX + padding;
                float currY = boxY + padding;

                for (int i = 0; i < _entries.Count; i++)
                {
                    var sz = g.MeasureString(_entries[i].Title, font);
                    float itemW = badgeSize + 6f + sz.Width;
                    _entries[i].ClickBounds = new RectangleF(currX, currY, itemW, Math.Max(badgeSize, sz.Height));
                    currX += itemW + itemSpacing;
                }

                return new RectangleF(boxX, boxY, boxWidth, boxHeight);
            }
            else
            {
                // Vertical layout (Left / Right)
                float maxWidth = 0;
                float totalHeight = padding * 2;

                for (int i = 0; i < _entries.Count; i++)
                {
                    var sz = g.MeasureString(_entries[i].Title, font);
                    float w = badgeSize + 6f + sz.Width;
                    if (w > maxWidth) maxWidth = w;
                    totalHeight += Math.Max(badgeSize, sz.Height) + 4f;
                }

                float boxWidth = maxWidth + padding * 2;
                float boxHeight = Math.Min(totalHeight, availableArea.Height);
                float boxX = Placement == LegendPlacement.Left ? availableArea.Left : availableArea.Right - boxWidth;
                float boxY = availableArea.Top + (availableArea.Height - boxHeight) * 0.5f;

                float currY = boxY + padding;
                for (int i = 0; i < _entries.Count; i++)
                {
                    var sz = g.MeasureString(_entries[i].Title, font);
                    float itemH = Math.Max(badgeSize, sz.Height);
                    _entries[i].ClickBounds = new RectangleF(boxX + padding, currY, boxWidth - padding * 2, itemH);
                    currY += itemH + 4f;
                }

                return new RectangleF(boxX, boxY, boxWidth, boxHeight);
            }
        }

        public void Render(Graphics g, Font font, RectangleF bounds)
        {
            if (Placement == LegendPlacement.None || _entries.Count == 0 || bounds.Width <= 0 || bounds.Height <= 0)
                return;

            using (var bgBrush = new SolidBrush(BackgroundColor))
            using (var borderPen = new Pen(BorderColor, 1f))
            {
                g.FillRectangle(bgBrush, bounds);
                g.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }

            float badgeSize = 10f;
            using (var activeTextBrush = new SolidBrush(TextColor))
            using (var inactiveTextBrush = new SolidBrush(InactiveTextColor))
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    var entry = _entries[i];
                    var rect = entry.ClickBounds;
                    if (rect.Width <= 0) continue;

                    float badgeY = rect.Y + (rect.Height - badgeSize) * 0.5f;

                    // Draw Color badge
                    Color badgeColor = entry.IsVisible ? entry.Color : Color.FromArgb(80, entry.Color);
                    using (var badgeBrush = new SolidBrush(badgeColor))
                    using (var badgeBorder = new Pen(Color.FromArgb(180, Color.White), 1f))
                    {
                        g.FillRectangle(badgeBrush, rect.X, badgeY, badgeSize, badgeSize);
                        if (!entry.IsVisible)
                        {
                            // Draw an X or strikethrough if inactive
                            g.DrawLine(badgeBorder, rect.X, badgeY, rect.X + badgeSize, badgeY + badgeSize);
                        }
                    }

                    // Draw Text
                    var textBrush = entry.IsVisible ? activeTextBrush : inactiveTextBrush;
                    g.DrawString(entry.Title, font, textBrush, rect.X + badgeSize + 6f, rect.Y);
                }
            }
        }
    }
}
