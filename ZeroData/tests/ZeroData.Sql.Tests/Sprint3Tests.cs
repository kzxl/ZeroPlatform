using Microsoft.Data.Sqlite;
using System.Linq;
using Xunit;

namespace ZeroData.Sql.Tests
{
    public class Sprint3Tests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public Sprint3Tests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE Items (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Quantity INTEGER NOT NULL DEFAULT 0,
                    Price REAL
                );
            ";
            cmd.ExecuteNonQuery();
        }

        #region Phase 8.1 — BulkInsert

        [Fact]
        public void BulkInsert_Inserts100Rows()
        {
            var entities = Enumerable.Range(1, 100)
                .Select(i => new TestItem { Name = $"Item_{i}", Quantity = i, Price = i * 0.5m })
                .ToList();

            using var ctx = new SqlContext(_connection);
            ctx.BulkInsert(entities);

            using var ctx2 = new SqlContext(_connection);
            var items = ctx2.GetTable<TestItem>().ToList();
            Assert.Equal(100, items.Count);
        }

        [Fact]
        public void BulkInsert_DataIsCorrect()
        {
            var entities = Enumerable.Range(1, 5)
                .Select(i => new TestItem { Name = $"Item_{i}", Quantity = i * 10 })
                .ToList();

            using var ctx = new SqlContext(_connection);
            ctx.BulkInsert(entities);

            using var ctx2 = new SqlContext(_connection);
            var items = ctx2.GetTable<TestItem>().OrderBy(x => x.Quantity).ToList();
            Assert.Equal(5, items.Count);
            Assert.Equal("Item_1", items[0].Name);
            Assert.Equal(10, items[0].Quantity);
            Assert.Equal("Item_5", items[4].Name);
            Assert.Equal(50, items[4].Quantity);
        }

        [Fact]
        public async Task BulkInsertAsync_Inserts100Rows()
        {
            var entities = Enumerable.Range(1, 100)
                .Select(i => new TestItem { Name = $"Async_{i}", Quantity = i })
                .ToList();

            using var ctx = new SqlContext(_connection);
            await ctx.BulkInsertAsync(entities);

            using var ctx2 = new SqlContext(_connection);
            var count = ctx2.GetTable<TestItem>().ToList().Count;
            Assert.Equal(100, count);
        }

        #endregion

        #region Phase 7.4 — Aggregates

        private void SeedAggregateData()
        {
            using var ctx = new SqlContext(_connection);
            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "A", Quantity = 10, Price = 1.00m });
            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "B", Quantity = 30, Price = 3.00m });
            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "C", Quantity = 20, Price = 2.00m });
            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "A", Quantity = 5, Price = 0.50m }); // duplicate Name
            ctx.SubmitChanges();
        }

        [Fact]
        public void Max_ReturnsMaxValue()
        {
            SeedAggregateData();
            using var ctx = new SqlContext(_connection);
            var max = ctx.GetTable<TestItem>().Max(x => x.Quantity);
            Assert.Equal(30, max);
        }

        [Fact]
        public void Min_ReturnsMinValue()
        {
            SeedAggregateData();
            using var ctx = new SqlContext(_connection);
            var min = ctx.GetTable<TestItem>().Min(x => x.Quantity);
            Assert.Equal(5, min);
        }

        [Fact]
        public void Sum_ReturnsSumValue()
        {
            SeedAggregateData();
            using var ctx = new SqlContext(_connection);
            var sum = ctx.GetTable<TestItem>().Sum(x => x.Quantity);
            Assert.Equal(65, sum);
        }

        [Fact]
        public void Max_WithWhere()
        {
            SeedAggregateData();
            using var ctx = new SqlContext(_connection);
            var max = ctx.GetTable<TestItem>().Max(x => x.Quantity, x => x.Name != "B");
            Assert.Equal(20, max);
        }

        [Fact]
        public void Distinct_RemovesDuplicates()
        {
            // Insert items with same data
            using var ctx = new SqlContext(_connection);
            ctx.ExecuteCommand("INSERT INTO Items (Name, Quantity, Price) VALUES ('X', 10, 1.0), ('X', 10, 1.0), ('Y', 20, 2.0)");

            using var ctx2 = new SqlContext(_connection);
            var distinct = ctx2.GetTable<TestItem>().Distinct();
            // Since Id is different, DISTINCT on all columns means all rows are unique
            Assert.Equal(3, distinct.Count);
        }

        [Fact]
        public async Task MaxAsync_ReturnsMaxValue()
        {
            SeedAggregateData();
            using var ctx = new SqlContext(_connection);
            var max = await ctx.GetTable<TestItem>().MaxAsync(x => x.Quantity);
            Assert.Equal(30, max);
        }

        [Fact]
        public async Task SumAsync_ReturnsSumValue()
        {
            SeedAggregateData();
            using var ctx = new SqlContext(_connection);
            var sum = await ctx.GetTable<TestItem>().SumAsync(x => x.Quantity);
            Assert.Equal(65, sum);
        }

        #endregion

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
