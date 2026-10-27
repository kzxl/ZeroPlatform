using Xunit;
using ZeroUI.Core.Data;

namespace ZeroUI.Core.Tests
{
    public class GridCardLayoutManagerTests
    {
        [Fact]
        public void UpdateLayout_CalculatesCorrectGeometry()
        {
            var manager = new GridCardLayoutManager
            {
                CardWidth = 200,
                CardHeight = 100,
                SpacingX = 10,
                SpacingY = 10,
                PaddingLeft = 10,
                PaddingTop = 10
            };

            // Available width = 640. Usable width = 640 - 20 = 620.
            // (620 + 10) / 210 = 3 columns per row
            manager.UpdateLayout(640, 10);

            Assert.Equal(3, manager.ColumnsPerRow);
            Assert.Equal(4, manager.TotalRows); // 10 cards / 3 cols = 4 rows (3 + 3 + 3 + 1)
        }

        [Fact]
        public void GetVisibleRange_IdentifiesCorrectVisibleCards()
        {
            var manager = new GridCardLayoutManager
            {
                CardWidth = 200,
                CardHeight = 100,
                SpacingX = 10,
                SpacingY = 10,
                PaddingLeft = 10,
                PaddingTop = 10
            };

            manager.UpdateLayout(640, 100);

            // Scroll at 0, viewport height 300
            manager.GetVisibleRange(0, 300, 100, out int startIdx, out int endIdx);
            Assert.Equal(0, startIdx);
            Assert.True(endIdx >= 9); // At least 3–4 rows visible (9–12 cards)

            // Scroll down 220px (skipped 2 rows = 6 cards)
            manager.GetVisibleRange(220, 300, 100, out startIdx, out endIdx);
            Assert.True(startIdx >= 3);
        }

        [Fact]
        public void HitTest_ReturnsCorrectCardOrMinusOneForGaps()
        {
            var manager = new GridCardLayoutManager
            {
                CardWidth = 200,
                CardHeight = 100,
                SpacingX = 10,
                SpacingY = 10,
                PaddingLeft = 10,
                PaddingTop = 10
            };

            manager.UpdateLayout(640, 10);

            // Card 0: X in [10, 210], Y in [10, 110]
            int hit0 = manager.HitTest(50, 50, 0, 10);
            Assert.Equal(0, hit0);

            // Card 1: X in [220, 420], Y in [10, 110]
            int hit1 = manager.HitTest(250, 50, 0, 10);
            Assert.Equal(1, hit1);

            // Gap between Card 0 and Card 1: X in (210, 220)
            int hitGap = manager.HitTest(215, 50, 0, 10);
            Assert.Equal(-1, hitGap);
        }
    }
}
