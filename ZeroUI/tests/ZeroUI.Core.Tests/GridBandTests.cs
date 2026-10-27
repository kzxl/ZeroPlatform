using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Data;

namespace ZeroUI.Core.Tests
{
    public class GridBandTests
    {
        [Fact]
        public void GridBand_CalculatesWidthFromColumns()
        {
            var band = new GridBand("Dimensions");
            band.AddColumn(new ZeroColumn("Length", 100));
            band.AddColumn(new ZeroColumn("Width", 80));
            band.AddColumn(new ZeroColumn("Height", 120));

            Assert.Equal(300, band.CalculateWidth());
            Assert.Equal(1, band.GetMaxDepth());
        }

        [Fact]
        public void GridBand_NestedSubBands_CalculatesWidthAndDepth()
        {
            var rootBand = new GridBand("Root Band");

            var subBand1 = new GridBand("Sub 1");
            subBand1.AddColumn(new ZeroColumn("A", 100));
            subBand1.AddColumn(new ZeroColumn("B", 150));

            var subBand2 = new GridBand("Sub 2");
            subBand2.AddColumn(new ZeroColumn("C", 200));

            rootBand.AddChildBand(subBand1);
            rootBand.AddChildBand(subBand2);

            Assert.Equal(450, rootBand.CalculateWidth());
            Assert.Equal(2, rootBand.GetMaxDepth());
            Assert.Equal(rootBand, subBand1.ParentBand);
        }

        [Fact]
        public void GridBand_ComputeLayout_GeneratesValidRectangles()
        {
            var band1 = new GridBand("Financial");
            band1.AddColumn(new ZeroColumn("Price", 100));
            band1.AddColumn(new ZeroColumn("Tax", 50));

            var band2 = new GridBand("Specs");
            band2.AddColumn(new ZeroColumn("Weight", 80));

            var layout = GridBand.ComputeLayout(new[] { band1, band2 }, startX: 0, startY: 0, singleTierHeight: 28, totalMaxDepth: 1);

            Assert.Equal(2, layout.Count);

            Assert.Equal(0, layout[0].X);
            Assert.Equal(0, layout[0].Y);
            Assert.Equal(150, layout[0].Width);
            Assert.Equal(28, layout[0].Height);

            Assert.Equal(150, layout[1].X);
            Assert.Equal(0, layout[1].Y);
            Assert.Equal(80, layout[1].Width);
            Assert.Equal(28, layout[1].Height);
        }
    }
}
