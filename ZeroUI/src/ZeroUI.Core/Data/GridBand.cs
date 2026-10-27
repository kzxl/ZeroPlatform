using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Represents a computed bounding layout rectangle for a column band header.
    /// </summary>
    public readonly struct GridBandLayoutEntry
    {
        public readonly GridBand Band;
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;

        public GridBandLayoutEntry(GridBand band, int x, int y, int width, int height)
        {
            Band = band;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Hierarchical column band descriptor for multi-tier banded DataGrid headers.
    /// Supports nested sub-bands and parent-child column grouping.
    /// </summary>
    public sealed class GridBand
    {
        public string Title { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = true;
        public GridBand? ParentBand { get; set; }
        public List<GridBand> ChildBands { get; } = new List<GridBand>();
        public List<ZeroColumn> Columns { get; } = new List<ZeroColumn>();

        public GridBand() { }

        public GridBand(string title)
        {
            Title = title;
        }

        public void AddColumn(ZeroColumn column)
        {
            if (column != null && !Columns.Contains(column))
            {
                column.BandTitle = Title;
                Columns.Add(column);
            }
        }

        public void AddChildBand(GridBand childBand)
        {
            if (childBand != null && !ChildBands.Contains(childBand))
            {
                childBand.ParentBand = this;
                ChildBands.Add(childBand);
            }
        }

        /// <summary>
        /// Calculates the total visual pixel width of this band by summing all visible children.
        /// </summary>
        public int CalculateWidth()
        {
            if (!IsVisible) return 0;

            int totalW = 0;
            if (ChildBands.Count > 0)
            {
                for (int i = 0; i < ChildBands.Count; i++)
                {
                    totalW += ChildBands[i].CalculateWidth();
                }
            }
            else
            {
                for (int i = 0; i < Columns.Count; i++)
                {
                    if (Columns[i].IsVisible)
                    {
                        totalW += Columns[i].Width;
                    }
                }
            }
            return totalW;
        }

        /// <summary>
        /// Computes the maximum hierarchy depth of this band.
        /// </summary>
        public int GetMaxDepth()
        {
            if (!IsVisible) return 0;
            int maxChildDepth = 0;
            for (int i = 0; i < ChildBands.Count; i++)
            {
                int d = ChildBands[i].GetMaxDepth();
                if (d > maxChildDepth) maxChildDepth = d;
            }
            return 1 + maxChildDepth;
        }

        /// <summary>
        /// Computes 2D header layout rectangles for a root collection of bands.
        /// </summary>
        public static List<GridBandLayoutEntry> ComputeLayout(
            IReadOnlyList<GridBand> rootBands,
            int startX,
            int startY,
            int singleTierHeight,
            int totalMaxDepth)
        {
            var results = new List<GridBandLayoutEntry>();
            if (rootBands == null || rootBands.Count == 0) return results;

            int currentX = startX;
            for (int i = 0; i < rootBands.Count; i++)
            {
                var band = rootBands[i];
                if (!band.IsVisible) continue;

                int bandW = band.CalculateWidth();
                if (bandW <= 0) continue;

                ComputeBandRecursive(band, currentX, startY, bandW, singleTierHeight, totalMaxDepth, 1, results);
                currentX += bandW;
            }

            return results;
        }

        private static void ComputeBandRecursive(
            GridBand band,
            int x,
            int y,
            int width,
            int singleTierHeight,
            int totalMaxDepth,
            int currentDepth,
            List<GridBandLayoutEntry> results)
        {
            int height = (band.ChildBands.Count == 0)
                ? (totalMaxDepth - currentDepth + 1) * singleTierHeight
                : singleTierHeight;

            results.Add(new GridBandLayoutEntry(band, x, y, width, height));

            if (band.ChildBands.Count > 0)
            {
                int childY = y + singleTierHeight;
                int childX = x;
                for (int i = 0; i < band.ChildBands.Count; i++)
                {
                    var child = band.ChildBands[i];
                    if (!child.IsVisible) continue;
                    int cWidth = child.CalculateWidth();
                    if (cWidth <= 0) continue;

                    ComputeBandRecursive(child, childX, childY, cWidth, singleTierHeight, totalMaxDepth, currentDepth + 1, results);
                    childX += cWidth;
                }
            }
        }
    }
}
