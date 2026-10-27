using System;
using Xunit;
using ZeroData.Core;

namespace ZeroData.Tests
{
    public class JoinOptimizationTests
    {
        [Fact]
        public void TestTypedStringJoin()
        {
            var left = new DataFrame(
                new DataColumn<string>("Code", new[] { "A01", "B02", "C03" }),
                new DataColumn<int>("Qty", new[] { 10, 20, 30 })
            );

            var right = new DataFrame(
                new DataColumn<string>("Code", new[] { "B02", "C03", "D04" }),
                new DataColumn<string>("Desc", new[] { "Widget B", "Widget C", "Widget D" })
            );

            var joined = left.Join(right, "Code", JoinType.Inner);

            Assert.Equal(2, joined.RowCount);
            var codeCol = (DataColumn<string>)joined["Code"];
            var qtyCol = (DataColumn<int>)joined["Qty"];
            var descCol = (DataColumn<string>)joined["Desc"];

            Assert.Equal("B02", codeCol[0]);
            Assert.Equal(20, qtyCol[0]);
            Assert.Equal("Widget B", descCol[0]);

            Assert.Equal("C03", codeCol[1]);
            Assert.Equal(30, qtyCol[1]);
            Assert.Equal("Widget C", descCol[1]);
        }

        [Fact]
        public void TestTypedGuidJoin()
        {
            var g1 = Guid.NewGuid();
            var g2 = Guid.NewGuid();
            var g3 = Guid.NewGuid();

            var left = new DataFrame(
                new DataColumn<Guid>("Id", new[] { g1, g2 }),
                new DataColumn<string>("Name", new[] { "Alpha", "Beta" })
            );

            var right = new DataFrame(
                new DataColumn<Guid>("Id", new[] { g2, g3 }),
                new DataColumn<decimal>("Price", new[] { 100.5m, 200.75m })
            );

            var joined = left.Join(right, "Id", JoinType.Inner);

            Assert.Equal(1, joined.RowCount);
            var idCol = (DataColumn<Guid>)joined["Id"];
            var priceCol = (DataColumn<decimal>)joined["Price"];

            Assert.Equal(g2, idCol[0]);
            Assert.Equal(100.5m, priceCol[0]);
        }

        [Fact]
        public void TestTypedDecimalJoin()
        {
            var left = new DataFrame(
                new DataColumn<decimal>("Rate", new[] { 1.5m, 2.0m, 3.5m }),
                new DataColumn<string>("Tier", new[] { "Standard", "Silver", "Gold" })
            );

            var right = new DataFrame(
                new DataColumn<decimal>("Rate", new[] { 2.0m, 3.5m, 5.0m }),
                new DataColumn<int>("Bonus", new[] { 100, 250, 500 })
            );

            var joined = left.Join(right, "Rate", JoinType.Inner);

            Assert.Equal(2, joined.RowCount);
            var rateCol = (DataColumn<decimal>)joined["Rate"];
            var bonusCol = (DataColumn<int>)joined["Bonus"];

            Assert.Equal(2.0m, rateCol[0]);
            Assert.Equal(100, bonusCol[0]);

            Assert.Equal(3.5m, rateCol[1]);
            Assert.Equal(250, bonusCol[1]);
        }

        [Fact]
        public void TestLeftJoinPreservesNullValidityBitmap()
        {
            var left = new DataFrame(
                new DataColumn<int>("Id", new[] { 1, 2 }),
                new DataColumn<string>("Name", new[] { "Alice", "Bob" })
            );

            var right = new DataFrame(
                new DataColumn<int>("Id", new[] { 2, 3 }),
                new DataColumn<double>("Score", new[] { 99.5, 88.0 })
            );

            var joined = left.Join(right, "Id", JoinType.Left);

            Assert.Equal(2, joined.RowCount);
            var scoreCol = (DataColumn<double>)joined["Score"];

            // Row 0 has unmatched right side, must be marked as null in null bitmap
            Assert.True(scoreCol.HasNulls);
            Assert.True(scoreCol.IsNull(0));
            Assert.Null(scoreCol.GetValue(0));

            // Row 1 is matched, must not be null
            Assert.False(scoreCol.IsNull(1));
            Assert.Equal(99.5, scoreCol[1]);
        }
    }
}
