using System;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Supported presentation modes for GridControl.
    /// </summary>
    public enum GridViewType
    {
        /// <summary>
        /// Standard multi-column tabular rows with frozen columns and headers.
        /// </summary>
        Table,

        /// <summary>
        /// Multi-column responsive cards with field/value pairs and badges.
        /// </summary>
        CardView,

        /// <summary>
        /// Compact high-density visual tiles for catalog and status boards.
        /// </summary>
        TileView
    }

    /// <summary>
    /// High-performance zero-allocation geometric layout manager for virtualized CardView and TileView.
    /// Calculates multi-column card flow, binary-searched viewport culling, and spatial hit testing.
    /// </summary>
    public sealed class GridCardLayoutManager
    {
        public int CardWidth { get; set; } = 240;
        public int CardHeight { get; set; } = 120;
        public int SpacingX { get; set; } = 8;
        public int SpacingY { get; set; } = 8;
        public int PaddingLeft { get; set; } = 8;
        public int PaddingTop { get; set; } = 8;

        public int ColumnsPerRow { get; private set; } = 1;
        public int TotalRows { get; private set; } = 0;
        public int TotalHeight { get; private set; } = 0;

        /// <summary>
        /// Recalculates grid geometry based on available client width and total card count.
        /// </summary>
        public void UpdateLayout(int availableWidth, int totalCards)
        {
            if (availableWidth <= 0 || totalCards <= 0)
            {
                ColumnsPerRow = 1;
                TotalRows = 0;
                TotalHeight = 0;
                return;
            }

            int usableWidth = Math.Max(1, availableWidth - (PaddingLeft * 2));
            int cardStepX = CardWidth + SpacingX;
            ColumnsPerRow = Math.Max(1, (usableWidth + SpacingX) / cardStepX);

            TotalRows = (totalCards + ColumnsPerRow - 1) / ColumnsPerRow;
            int cardStepY = CardHeight + SpacingY;
            TotalHeight = PaddingTop + (TotalRows * cardStepY) + SpacingY;
        }

        /// <summary>
        /// Calculates the visible card range for the current scroll position and viewport height with zero allocations.
        /// </summary>
        public void GetVisibleRange(int scrollY, int viewportHeight, int totalCards, out int startCardIndex, out int endCardIndex)
        {
            if (totalCards <= 0 || ColumnsPerRow <= 0)
            {
                startCardIndex = 0;
                endCardIndex = -1;
                return;
            }

            int cardStepY = CardHeight + SpacingY;
            int effectiveScroll = Math.Max(0, scrollY - PaddingTop);

            int startRow = effectiveScroll / cardStepY;
            int visibleRows = (viewportHeight / cardStepY) + 2;
            int endRow = startRow + visibleRows;

            startCardIndex = Math.Max(0, startRow * ColumnsPerRow);
            endCardIndex = Math.Min(totalCards - 1, ((endRow + 1) * ColumnsPerRow) - 1);
        }

        /// <summary>
        /// Computes screen-relative coordinates for a card index.
        /// </summary>
        public void GetCardBounds(int cardIndex, int scrollY, out int x, out int y, out int width, out int height)
        {
            width = CardWidth;
            height = CardHeight;

            if (ColumnsPerRow <= 0)
            {
                x = PaddingLeft;
                y = PaddingTop - scrollY;
                return;
            }

            int col = cardIndex % ColumnsPerRow;
            int row = cardIndex / ColumnsPerRow;

            x = PaddingLeft + (col * (CardWidth + SpacingX));
            y = PaddingTop + (row * (CardHeight + SpacingY)) - scrollY;
        }

        /// <summary>
        /// Performs O(1) mathematical hit-testing to identify which card index was clicked.
        /// Returns -1 if clicking empty padding or spacing between cards.
        /// </summary>
        public int HitTest(int clientX, int clientY, int scrollY, int totalCards)
        {
            if (totalCards <= 0 || ColumnsPerRow <= 0) return -1;

            int relX = clientX - PaddingLeft;
            int relY = (clientY + scrollY) - PaddingTop;

            if (relX < 0 || relY < 0) return -1;

            int cardStepX = CardWidth + SpacingX;
            int cardStepY = CardHeight + SpacingY;

            int col = relX / cardStepX;
            int row = relY / cardStepY;

            if (col >= ColumnsPerRow) return -1;

            // Check if within card bounds (not in gap)
            int cardInnerX = relX % cardStepX;
            int cardInnerY = relY % cardStepY;

            if (cardInnerX > CardWidth || cardInnerY > CardHeight)
            {
                return -1; // Clicked in spacing gap
            }

            int index = (row * ColumnsPerRow) + col;
            return (index >= 0 && index < totalCards) ? index : -1;
        }
    }
}
