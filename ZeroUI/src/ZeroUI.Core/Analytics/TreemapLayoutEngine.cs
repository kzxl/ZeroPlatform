using System;
using System.Collections.Generic;
using System.Linq;
using ZeroUI.Core.Layout;

namespace ZeroUI.Core.Analytics
{
    /// <summary>
    /// Platform-agnostic hierarchical tile item for Treemap visualizations.
    /// </summary>
    public class TreemapItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Category { get; set; } = string.Empty;
        public uint? ColorRgba { get; set; }
        public object? Tag { get; set; }

        public TreemapItem() { }

        public TreemapItem(string label, double value, string category = "", uint? colorRgba = null)
        {
            Label = label ?? string.Empty;
            Value = value;
            Category = category ?? string.Empty;
            ColorRgba = colorRgba;
        }
    }

    /// <summary>
    /// Core layout engine wrapping Bruls-Huizing-van Wijk squarified treemap partition algorithm.
    /// </summary>
    public static class TreemapEngine
    {
        public static IReadOnlyList<TreeMapRect<TreemapItem>> ComputeLayout(
            IEnumerable<TreemapItem> items,
            double x, double y, double width, double height,
            double padding = 1.0)
        {
            var list = items?.Where(i => i != null && i.Value > 0).ToList() ?? new List<TreemapItem>();
            if (list.Count == 0 || width <= 0 || height <= 0)
            {
                return Array.Empty<TreeMapRect<TreemapItem>>();
            }

            var rawLayout = SquarifiedTreeMap.Layout(list, itm => itm.Value, width, height, padding);
            if (x == 0 && y == 0) return rawLayout;

            var shifted = new List<TreeMapRect<TreemapItem>>(rawLayout.Count);
            foreach (var r in rawLayout)
            {
                shifted.Add(new TreeMapRect<TreemapItem>(r.Item, r.X + x, r.Y + y, r.Width, r.Height, r.NormalizedWeight));
            }
            return shifted;
        }
    }
}
