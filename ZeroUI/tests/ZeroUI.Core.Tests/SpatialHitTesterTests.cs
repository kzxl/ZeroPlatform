using System;
using Xunit;
using ZeroUI.Core.Input;

namespace ZeroUI.Core.Tests
{
    public class SpatialHitTesterTests
    {
        [Fact]
        public void HitTest_WithPinnedOffset_ReturnsRowIndicatorInHeader()
        {
            int[] widths = new[] { 100, 150 };
            bool[] pinned = new[] { true, false };

            // When clicking within pinnedOffset (e.g. x = 15, y = 10, headerHeight = 28)
            var hit = SpatialHitTester.HitTest(
                clientX: 15,
                clientY: 10,
                headerHeight: 28,
                defaultRowHeight: 26,
                scrollX: 0,
                scrollY: 0,
                columnWidths: widths,
                isPinned: pinned,
                totalCols: 2,
                totalRows: 100,
                pinnedOffset: 34);

            Assert.Equal(HitRegion.RowIndicator, hit.Region);
            Assert.Equal(-1, hit.RowIndex);
        }

        [Fact]
        public void HitTest_WithPinnedOffset_ReturnsRowIndicatorInCell()
        {
            int[] widths = new[] { 100, 150 };
            bool[] pinned = new[] { true, false };

            // When clicking within pinnedOffset on row 2 (headerHeight = 28, rowHeight = 26, y = 28 + 26 * 2 + 5 = 85)
            var hit = SpatialHitTester.HitTest(
                clientX: 15,
                clientY: 85,
                headerHeight: 28,
                defaultRowHeight: 26,
                scrollX: 0,
                scrollY: 0,
                columnWidths: widths,
                isPinned: pinned,
                totalCols: 2,
                totalRows: 100,
                pinnedOffset: 34);

            Assert.Equal(HitRegion.RowIndicator, hit.Region);
            Assert.Equal(2, hit.RowIndex);
        }

        [Fact]
        public void HitTest_WithPinnedOffset_ShiftsColumnsCorrectly()
        {
            int[] widths = new[] { 100, 150 };
            bool[] pinned = new[] { true, false };

            // Pinned column 0 is located from x = 34 to 134.
            // Clicking at x = 50 should hit Column 0 in Header.
            var hit = SpatialHitTester.HitTest(
                clientX: 50,
                clientY: 10,
                headerHeight: 28,
                defaultRowHeight: 26,
                scrollX: 0,
                scrollY: 0,
                columnWidths: widths,
                isPinned: pinned,
                totalCols: 2,
                totalRows: 100,
                pinnedOffset: 34);

            Assert.Equal(HitRegion.Header, hit.Region);
            Assert.Equal(0, hit.ColumnIndex);
        }

        [Fact]
        public void HitTest_WithAutoFilterRow_ReturnsAutoFilterRow()
        {
            int[] widths = new[] { 100, 150 };
            bool[] pinned = new[] { false, false };

            // Header is y: 0..28, AutoFilterRow is y: 28..56.
            // Click at (x: 50, y: 35) should hit Column 0 of AutoFilterRow
            var hit = SpatialHitTester.HitTest(
                clientX: 50,
                clientY: 35,
                headerHeight: 28,
                defaultRowHeight: 26,
                scrollX: 0,
                scrollY: 0,
                columnWidths: widths,
                isPinned: pinned,
                totalCols: 2,
                totalRows: 100,
                autoFilterRowHeight: 28);

            Assert.Equal(HitRegion.AutoFilterRow, hit.Region);
            Assert.Equal(0, hit.ColumnIndex);
            Assert.Equal(28, hit.Bounds.Y);
            Assert.Equal(28, hit.Bounds.Height);
        }

        [Fact]
        public void HitTest_WithAutoFilterRow_ShiftsDataCellsDown()
        {
            int[] widths = new[] { 100, 150 };
            bool[] pinned = new[] { false, false };

            // Header = 28, AutoFilterRow = 28 -> Total top offset = 56.
            // Row 0 is at y: 56..82. Click at (x: 120, y: 65) should hit visual row 0, column 1.
            var hit = SpatialHitTester.HitTest(
                clientX: 120,
                clientY: 65,
                headerHeight: 28,
                defaultRowHeight: 26,
                scrollX: 0,
                scrollY: 0,
                columnWidths: widths,
                isPinned: pinned,
                totalCols: 2,
                totalRows: 100,
                autoFilterRowHeight: 28);

            Assert.Equal(HitRegion.Cell, hit.Region);
            Assert.Equal(0, hit.RowIndex);
            Assert.Equal(1, hit.ColumnIndex);
            Assert.Equal(56, hit.Bounds.Y);
        }
    }
}
