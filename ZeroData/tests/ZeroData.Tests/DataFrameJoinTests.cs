using System;
using Xunit;
using ZeroData.Core;

namespace ZeroData.Tests
{
    public class DataFrameJoinTests
    {
        [Fact]
        public void TestInnerJoin()
        {
            var left = new DataFrame(
                new DataColumn<int>("Id", new[] { 1, 2, 3 }),
                new DataColumn<string>("Name", new[] { "Alice", "Bob", "Charlie" })
            );

            var right = new DataFrame(
                new DataColumn<int>("Id", new[] { 2, 3, 4 }),
                new DataColumn<double>("Score", new[] { 85.5, 92.0, 78.0 })
            );

            var joined = left.Join(right, "Id", JoinType.Inner);

            Assert.Equal(2, joined.RowCount);
            Assert.Equal(3, joined.ColumnCount); // Id, Name, Score

            var idCol = (DataColumn<int>)joined["Id"];
            var nameCol = (DataColumn<string>)joined["Name"];
            var scoreCol = (DataColumn<double>)joined["Score"];

            Assert.Equal(2, idCol[0]);
            Assert.Equal("Bob", nameCol[0]);
            Assert.Equal(85.5, scoreCol[0]);

            Assert.Equal(3, idCol[1]);
            Assert.Equal("Charlie", nameCol[1]);
            Assert.Equal(92.0, scoreCol[1]);
        }

        [Fact]
        public void TestLeftJoin()
        {
            var left = new DataFrame(
                new DataColumn<int>("Id", new[] { 1, 2, 3 }),
                new DataColumn<string>("Name", new[] { "Alice", "Bob", "Charlie" })
            );

            var right = new DataFrame(
                new DataColumn<int>("Id", new[] { 2, 3, 4 }),
                new DataColumn<double>("Score", new[] { 85.5, 92.0, 78.0 })
            );

            var joined = left.Join(right, "Id", JoinType.Left);

            Assert.Equal(3, joined.RowCount);

            var idCol = (DataColumn<int>)joined["Id"];
            var nameCol = (DataColumn<string>)joined["Name"];
            var scoreCol = (DataColumn<double>)joined["Score"];

            // Row 0: Id 1 (Alice), Score = default(double) = 0.0
            Assert.Equal(1, idCol[0]);
            Assert.Equal("Alice", nameCol[0]);
            Assert.Equal(0.0, scoreCol[0]);

            // Row 1: Id 2 (Bob), Score = 85.5
            Assert.Equal(2, idCol[1]);
            Assert.Equal("Bob", nameCol[1]);
            Assert.Equal(85.5, scoreCol[1]);
        }

        [Fact]
        public void TestRightJoin()
        {
            var left = new DataFrame(
                new DataColumn<int>("Id", new[] { 1, 2 }),
                new DataColumn<string>("Name", new[] { "Alice", "Bob" })
            );

            var right = new DataFrame(
                new DataColumn<int>("Id", new[] { 2, 3 }),
                new DataColumn<double>("Score", new[] { 85.5, 95.0 })
            );

            var joined = left.Join(right, "Id", JoinType.Right);

            Assert.Equal(2, joined.RowCount);

            var nameCol = (DataColumn<string>)joined["Name"];
            var scoreCol = (DataColumn<double>)joined["Score"];

            // Matched row (Id 2)
            Assert.Equal("Bob", nameCol[0]);
            Assert.Equal(85.5, scoreCol[0]);

            // Unmatched right row (Id 3)
            Assert.Null(nameCol[1]);
            Assert.Equal(95.0, scoreCol[1]);
        }

        [Fact]
        public void TestFullOuterJoin()
        {
            var left = new DataFrame(
                new DataColumn<int>("Id", new[] { 1, 2 }),
                new DataColumn<string>("Name", new[] { "Alice", "Bob" })
            );

            var right = new DataFrame(
                new DataColumn<int>("Id", new[] { 2, 3 }),
                new DataColumn<double>("Score", new[] { 85.5, 95.0 })
            );

            var joined = left.Join(right, "Id", JoinType.FullOuter);

            Assert.Equal(3, joined.RowCount); // Id 1, Id 2, Id 3

            var idCol = (DataColumn<int>)joined["Id"];
            var nameCol = (DataColumn<string>)joined["Name"];
            var scoreCol = (DataColumn<double>)joined["Score"];

            // Row 0: Id 1 (Alice, 0.0)
            Assert.Equal(1, idCol[0]);
            Assert.Equal("Alice", nameCol[0]);
            Assert.Equal(0.0, scoreCol[0]);

            // Row 1: Id 2 (Bob, 85.5)
            Assert.Equal(2, idCol[1]);
            Assert.Equal("Bob", nameCol[1]);
            Assert.Equal(85.5, scoreCol[1]);

            // Row 2: Id 3 (null, 95.0)
            Assert.Null(nameCol[2]);
            Assert.Equal(95.0, scoreCol[2]);
        }

        [Fact]
        public void TestJoinWithDifferentKeyNames()
        {
            var left = new DataFrame(
                new DataColumn<int>("UserId", new[] { 10, 20 }),
                new DataColumn<string>("Role", new[] { "Admin", "Operator" })
            );

            var right = new DataFrame(
                new DataColumn<int>("EmpId", new[] { 20, 30 }),
                new DataColumn<string>("Dept", new[] { "Manufacturing", "Quality" })
            );

            var joined = left.Join(right, "UserId", "EmpId", JoinType.Inner);

            Assert.Equal(1, joined.RowCount);
            Assert.Equal(4, joined.ColumnCount); // UserId, Role, EmpId, Dept

            var uidCol = (DataColumn<int>)joined["UserId"];
            var empIdCol = (DataColumn<int>)joined["EmpId"];
            var deptCol = (DataColumn<string>)joined["Dept"];

            Assert.Equal(20, uidCol[0]);
            Assert.Equal(20, empIdCol[0]);
            Assert.Equal("Manufacturing", deptCol[0]);
        }

        [Fact]
        public void TestJoinAsOf_LongTimestamps_WithTolerance()
        {
            var left = new DataFrame(
                new DataColumn<long>("Time", new long[] { 50, 100, 150, 250, 400 }),
                new DataColumn<double>("TradePrice", new double[] { 10.0, 10.5, 10.8, 11.2, 11.5 })
            );

            var right = new DataFrame(
                new DataColumn<long>("Time", new long[] { 80, 100, 120, 240 }),
                new DataColumn<double>("QuoteBid", new double[] { 9.9, 10.4, 10.7, 11.1 })
            );

            // Max tolerance = 50 ticks:
            // t_L = 50  -> no t_R <= 50 -> null (0.0)
            // t_L = 100 -> t_R = 100 (diff 0 <= 50) -> 10.4
            // t_L = 150 -> t_R = 120 (diff 30 <= 50) -> 10.7
            // t_L = 250 -> t_R = 240 (diff 10 <= 50) -> 11.1
            // t_L = 400 -> t_R = 240 (diff 160 > 50, exceeds tolerance) -> null (0.0)
            var joined = left.JoinAsOf(right, "Time", maxToleranceTicks: 50);

            Assert.Equal(5, joined.RowCount);
            Assert.Equal(3, joined.ColumnCount); // Time, TradePrice, QuoteBid

            var bidCol = joined.Column<double>("QuoteBid");
            Assert.True(bidCol.IsNull(0));
            Assert.False(bidCol.IsNull(1));
            Assert.Equal(10.4, bidCol[1], 5);
            Assert.False(bidCol.IsNull(2));
            Assert.Equal(10.7, bidCol[2], 5);
            Assert.False(bidCol.IsNull(3));
            Assert.Equal(11.1, bidCol[3], 5);
            Assert.True(bidCol.IsNull(4));
        }

        [Fact]
        public void TestJoinAsOf_DateTimeAndByKeyColumn()
        {
            var t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            var trades = new DataFrame(
                new DataColumn<DateTime>("Timestamp", new[]
                {
                    t0.AddSeconds(1),
                    t0.AddSeconds(3),
                    t0.AddSeconds(4),
                    t0.AddSeconds(10)
                }),
                new DataColumn<string>("Symbol", new[] { "AAPL", "MSFT", "AAPL", "MSFT" }),
                new DataColumn<int>("Qty", new[] { 100, 200, 150, 300 })
            );

            var quotes = new DataFrame(
                new DataColumn<DateTime>("Timestamp", new[]
                {
                    t0.AddSeconds(0),
                    t0.AddSeconds(2),
                    t0.AddSeconds(3)
                }),
                new DataColumn<string>("Symbol", new[] { "AAPL", "MSFT", "AAPL" }),
                new DataColumn<double>("Ask", new[] { 180.0, 410.0, 181.5 })
            );

            var joined = trades.JoinAsOf(quotes, "Timestamp", TimeSpan.FromSeconds(5).Ticks, byKeyColumn: "Symbol");

            Assert.Equal(4, joined.RowCount);
            Assert.Equal(4, joined.ColumnCount); // Timestamp, Symbol, Qty, Ask

            var askCol = joined.Column<double>("Ask");
            // Row 0: AAPL at t0+1s -> matches AAPL at t0+0s (180.0)
            Assert.Equal(180.0, askCol[0], 5);
            // Row 1: MSFT at t0+3s -> matches MSFT at t0+2s (410.0)
            Assert.Equal(410.0, askCol[1], 5);
            // Row 2: AAPL at t0+4s -> matches AAPL at t0+3s (181.5)
            Assert.Equal(181.5, askCol[2], 5);
            // Row 3: MSFT at t0+10s -> MSFT at t0+2s is 8s old (> 5s tolerance) -> null
            Assert.True(askCol.IsNull(3));
        }

        [Fact]
        public void TestJoinAsOf_DoubleTimestamps()
        {
            var left = new DataFrame(
                new DataColumn<double>("Sec", new[] { 1.0, 2.5, 4.0 }),
                new DataColumn<int>("SensorA", new[] { 10, 20, 30 })
            );

            var right = new DataFrame(
                new DataColumn<double>("Sec", new[] { 0.5, 2.0, 2.5 }),
                new DataColumn<int>("SensorB", new[] { 100, 200, 250 })
            );

            var joined = left.JoinAsOf(right, "Sec", maxToleranceTicks: 1);
            var bCol = joined.Column<int>("SensorB");

            Assert.Equal(100, bCol[0]); // 1.0 - 0.5 = 0.5 <= 1
            Assert.Equal(250, bCol[1]); // 2.5 - 2.5 = 0.0 <= 1
            Assert.True(bCol.IsNull(2)); // 4.0 - 2.5 = 1.5 > 1
        }
    }
}
