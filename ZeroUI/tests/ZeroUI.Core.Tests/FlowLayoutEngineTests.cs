using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Layout;

namespace ZeroUI.Core.Tests
{
    public class FlowLayoutEngineTests
    {
        [Fact]
        public void CalculateLayout_WrapsRowsWhenExceedingAvailableWidth()
        {
            // 4 items: each 100x50. Available width = 250. HGap = 10, VGap = 10, Padding = 10, 10
            // Row 1: Item 0 (10, 10), Item 1 (120, 10). 120 + 100 = 220 <= 260 (padding + availW)
            // Item 2: 230 + 100 = 330 > 260 -> wraps to Row 2 (10, 70), Item 3 (120, 70)
            var sizes = new List<(int Width, int Height)>
            {
                (100, 50),
                (100, 50),
                (100, 50),
                (100, 50)
            };

            var bounds = FlowLayoutEngine.CalculateLayout(sizes, 250, 10, 10, 10, 10, out int totalHeight);

            Assert.Equal(4, bounds.Count);

            // Row 1
            Assert.Equal(10, bounds[0].X);
            Assert.Equal(10, bounds[0].Y);
            Assert.Equal(120, bounds[1].X);
            Assert.Equal(10, bounds[1].Y);

            // Row 2
            Assert.Equal(10, bounds[2].X);
            Assert.Equal(70, bounds[2].Y);
            Assert.Equal(120, bounds[3].X);
            Assert.Equal(70, bounds[3].Y);

            Assert.Equal(120, totalHeight); // 70 + 50 = 120
        }

        [Fact]
        public void HitTestSlot_FindsExactOrClosestSlot()
        {
            var slots = new List<LayoutRect>
            {
                new LayoutRect(10, 10, 100, 50),
                new LayoutRect(120, 10, 100, 50)
            };

            int idx1 = FlowLayoutEngine.HitTestSlot(50, 25, slots);
            Assert.Equal(0, idx1);

            int idx2 = FlowLayoutEngine.HitTestSlot(150, 25, slots);
            Assert.Equal(1, idx2);
        }

        [Fact]
        public void Reorder_MovesItemAccurately()
        {
            var list = new List<string> { "CardA", "CardB", "CardC", "CardD" };

            // Move CardA (0) to index 2 -> [CardB, CardC, CardA, CardD]
            FlowLayoutEngine.Reorder(list, 0, 2);

            Assert.Equal("CardB", list[0]);
            Assert.Equal("CardC", list[1]);
            Assert.Equal("CardA", list[2]);
            Assert.Equal("CardD", list[3]);
        }
    }
}
