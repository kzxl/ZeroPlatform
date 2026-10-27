using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroUI.Core.Analytics
{
    /// <summary>
    /// Orientation mode for rendering Lollipop comparison charts.
    /// </summary>
    public enum LollipopOrientation
    {
        /// <summary>
        /// Categories on vertical Y-axis, values extending horizontally along X-axis. Best for long labels.
        /// </summary>
        Horizontal,

        /// <summary>
        /// Categories on horizontal X-axis, values extending vertically along Y-axis.
        /// </summary>
        Vertical
    }

    /// <summary>
    /// Discrete data item for high-density Lollipop comparison charts.
    /// </summary>
    public class LollipopItem
    {
        public string Category { get; set; } = string.Empty;
        public double Value { get; set; }
        public uint? ColorRgba { get; set; }
        public object? Tag { get; set; }

        public LollipopItem() { }

        public LollipopItem(string category, double value, uint? colorRgba = null)
        {
            Category = category ?? string.Empty;
            Value = value;
            ColorRgba = colorRgba;
        }

        public override string ToString() => $"{Category}: {Value:F2}";
    }

    /// <summary>
    /// Layout element with pixel-exact stem and marker coordinates computed by LollipopEngine.
    /// </summary>
    public class LollipopRenderItem
    {
        public LollipopItem SourceItem { get; }
        public int Index { get; }
        public double StemStartX { get; }
        public double StemStartY { get; }
        public double StemEndX { get; }
        public double StemEndY { get; }
        public double DotCenterX { get; }
        public double DotCenterY { get; }
        public double DotRadius { get; }

        public LollipopRenderItem(
            LollipopItem sourceItem,
            int index,
            double stemStartX,
            double stemStartY,
            double stemEndX,
            double stemEndY,
            double dotCenterX,
            double dotCenterY,
            double dotRadius)
        {
            SourceItem = sourceItem;
            Index = index;
            StemStartX = stemStartX;
            StemStartY = stemStartY;
            StemEndX = stemEndX;
            StemEndY = stemEndY;
            DotCenterX = dotCenterX;
            DotCenterY = dotCenterY;
            DotRadius = dotRadius;
        }

        /// <summary>
        /// Tests if a screen coordinate hits the lollipop head dot or stem line.
        /// </summary>
        public bool Contains(double px, double py, double hitSlop = 6.0)
        {
            // Check dot head
            double dx = px - DotCenterX;
            double dy = py - DotCenterY;
            double r = DotRadius + hitSlop;
            if ((dx * dx) + (dy * dy) <= (r * r)) return true;

            // Check stem bounding box with slop
            double minX = Math.Min(StemStartX, StemEndX) - hitSlop;
            double maxX = Math.Max(StemStartX, StemEndX) + hitSlop;
            double minY = Math.Min(StemStartY, StemEndY) - hitSlop;
            double maxY = Math.Max(StemStartY, StemEndY) + hitSlop;

            return px >= minX && px <= maxX && py >= minY && py <= maxY;
        }
    }

    /// <summary>
    /// Encapsulates the computed lollipop layout coordinate collection and range bounds.
    /// </summary>
    public class LollipopLayoutResult
    {
        public IReadOnlyList<LollipopRenderItem> RenderItems { get; }
        public double MinValue { get; }
        public double MaxValue { get; }
        public double BaselineValue { get; }

        public LollipopLayoutResult(IReadOnlyList<LollipopRenderItem> items, double minValue, double maxValue, double baselineValue)
        {
            RenderItems = items ?? Array.Empty<LollipopRenderItem>();
            MinValue = minValue;
            MaxValue = maxValue;
            BaselineValue = baselineValue;
        }
    }

    /// <summary>
    /// Core layout calculation engine determining stem vectors and dot positions for lollipop charts.
    /// </summary>
    public static class LollipopEngine
    {
        /// <summary>
        /// Computes pixel coordinates for each lollipop item within given viewport dimensions.
        /// </summary>
        public static LollipopLayoutResult ComputeLayout(
            IReadOnlyList<LollipopItem> items,
            double plotX,
            double plotY,
            double plotWidth,
            double plotHeight,
            LollipopOrientation orientation = LollipopOrientation.Horizontal,
            double dotRadius = 6.0,
            double? customBaseline = null)
        {
            if (items == null || items.Count == 0 || plotWidth <= 0 || plotHeight <= 0)
            {
                return new LollipopLayoutResult(Array.Empty<LollipopRenderItem>(), 0, 0, 0);
            }

            int count = items.Count;
            double minVal = items.Min(i => i.Value);
            double maxVal = items.Max(i => i.Value);

            // Establish standard baseline (0.0 if range spans zero or positive, else minVal)
            double baseline = customBaseline ?? (minVal >= 0 ? 0.0 : minVal);
            if (baseline < minVal) minVal = baseline;
            if (baseline > maxVal) maxVal = baseline;

            double range = maxVal - minVal;
            if (range < 1e-12)
            {
                range = 1.0;
                maxVal = minVal + 1.0;
            }

            var renderItems = new List<LollipopRenderItem>(count);

            if (orientation == LollipopOrientation.Horizontal)
            {
                // Horizontal: Y-axis divides categories, X-axis represents values
                double rowHeight = plotHeight / count;
                double baseX = plotX + ((baseline - minVal) / range) * plotWidth;

                for (int i = 0; i < count; i++)
                {
                    var item = items[i];
                    double centerY = plotY + (i + 0.5) * rowHeight;
                    double dotX = plotX + ((item.Value - minVal) / range) * plotWidth;

                    renderItems.Add(new LollipopRenderItem(
                        item,
                        i,
                        stemStartX: baseX,
                        stemStartY: centerY,
                        stemEndX: dotX,
                        stemEndY: centerY,
                        dotCenterX: dotX,
                        dotCenterY: centerY,
                        dotRadius: dotRadius
                    ));
                }
            }
            else
            {
                // Vertical: X-axis divides categories, Y-axis represents values (inverted: higher value is near top)
                double colWidth = plotWidth / count;
                double baseY = plotY + plotHeight - ((baseline - minVal) / range) * plotHeight;

                for (int i = 0; i < count; i++)
                {
                    var item = items[i];
                    double centerX = plotX + (i + 0.5) * colWidth;
                    double dotY = plotY + plotHeight - ((item.Value - minVal) / range) * plotHeight;

                    renderItems.Add(new LollipopRenderItem(
                        item,
                        i,
                        stemStartX: centerX,
                        stemStartY: baseY,
                        stemEndX: centerX,
                        stemEndY: dotY,
                        dotCenterX: centerX,
                        dotCenterY: dotY,
                        dotRadius: dotRadius
                    ));
                }
            }

            return new LollipopLayoutResult(renderItems, minVal, maxVal, baseline);
        }
    }
}
