using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Layout
{
    /// <summary>
    /// Represents a 2D integer bounding box for layout calculations.
    /// Used by Core layout engines to remain independent of System.Drawing.
    /// </summary>
    public readonly struct LayoutRect
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }

        public int Right => X + Width;
        public int Bottom => Y + Height;

        public LayoutRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = Math.Max(0, width);
            Height = Math.Max(0, height);
        }

        public bool Contains(int px, int py) =>
            px >= X && px <= Right && py >= Y && py <= Bottom;
    }

    /// <summary>
    /// Pure mathematical coordinate calculation engine for responsive flow layouts.
    /// Computes multi-row wrapping, slot coordinates, hit-testing, and item reordering.
    /// </summary>
    public static class FlowLayoutEngine
    {
        /// <summary>
        /// Calculates bounding rectangles for child items wrapping into multiple rows.
        /// </summary>
        /// <param name="itemSizes">Width and height of each child item.</param>
        /// <param name="availableWidth">Available container width in pixels.</param>
        /// <param name="hGap">Horizontal gap between adjacent items.</param>
        /// <param name="vGap">Vertical gap between adjacent rows.</param>
        /// <param name="paddingLeft">Left container padding.</param>
        /// <param name="paddingTop">Top container padding.</param>
        /// <param name="totalHeight">Output total required container height.</param>
        /// <returns>List of calculated child bounding rectangles.</returns>
        public static List<LayoutRect> CalculateLayout(
            IReadOnlyList<(int Width, int Height)> itemSizes,
            int availableWidth,
            int hGap,
            int vGap,
            int paddingLeft,
            int paddingTop,
            out int totalHeight)
        {
            var bounds = new List<LayoutRect>(itemSizes != null ? itemSizes.Count : 0);
            if (itemSizes == null || itemSizes.Count == 0)
            {
                totalHeight = paddingTop;
                return bounds;
            }

            int curX = paddingLeft;
            int curY = paddingTop;
            int rowHeight = 0;
            int maxRight = paddingLeft + Math.Max(100, availableWidth);

            for (int i = 0; i < itemSizes.Count; i++)
            {
                var (w, h) = itemSizes[i];

                // If adding this item exceeds available width and we already have items on this row, wrap
                if (curX > paddingLeft && curX + w > maxRight)
                {
                    curX = paddingLeft;
                    curY += rowHeight + vGap;
                    rowHeight = 0;
                }

                bounds.Add(new LayoutRect(curX, curY, w, h));
                curX += w + hGap;
                if (h > rowHeight) rowHeight = h;
            }

            totalHeight = curY + rowHeight;
            return bounds;
        }

        /// <summary>
        /// Determines the slot index under or closest to the given mouse coordinate.
        /// </summary>
        public static int HitTestSlot(int mouseX, int mouseY, IReadOnlyList<LayoutRect> slots)
        {
            if (slots == null || slots.Count == 0) return -1;

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Contains(mouseX, mouseY))
                {
                    return i;
                }
            }

            // Fallback: Find closest slot center
            int bestIdx = 0;
            long bestDistSq = long.MaxValue;

            for (int i = 0; i < slots.Count; i++)
            {
                int cx = slots[i].X + slots[i].Width / 2;
                int cy = slots[i].Y + slots[i].Height / 2;
                long dx = mouseX - cx;
                long dy = mouseY - cy;
                long distSq = dx * dx + dy * dy;

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestIdx = i;
                }
            }

            return bestIdx;
        }

        /// <summary>
        /// Moves an item from sourceIndex to targetIndex within a list.
        /// </summary>
        public static void Reorder<T>(IList<T> list, int sourceIndex, int targetIndex)
        {
            if (list == null || sourceIndex == targetIndex) return;
            if (sourceIndex < 0 || sourceIndex >= list.Count) return;
            if (targetIndex < 0 || targetIndex >= list.Count) return;

            T item = list[sourceIndex];
            list.RemoveAt(sourceIndex);
            list.Insert(targetIndex, item);
        }
    }
}
