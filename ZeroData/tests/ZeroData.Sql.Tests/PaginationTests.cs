using Microsoft.Data.Sqlite;
using System.Linq;
using ZeroData.Sql.Mapping;
using Xunit;

namespace ZeroData.Sql.Tests
{
    public class PaginationTests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public PaginationTests()
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

            // Seed 10 items
            using var ctx = new SqlContext(_connection);
            for (int i = 1; i <= 10; i++)
            {
                ctx.GetTable<TestItem>().InsertOnSubmit(
                    new TestItem { Name = $"Item{i:D2}", Quantity = i * 10, Price = i * 1.0m });
            }
            ctx.SubmitChanges();
        }

        [Fact]
        public void Take_LimitsResults()
        {
            using var ctx = new SqlContext(_connection);
            var items = ctx.GetTable<TestItem>()
                .OrderBy(x => x.Id)
                .Take(3)
                .ToList();

            Assert.Equal(3, items.Count);
        }

        [Fact]
        public void Skip_SkipsResults()
        {
            using var ctx = new SqlContext(_connection);
            var items = ctx.GetTable<TestItem>()
                .OrderBy(x => x.Id)
                .Skip(7)
                .ToList();

            Assert.Equal(3, items.Count);
            Assert.Equal("Item08", items[0].Name);
        }

        [Fact]
        public void SkipTake_Pagination()
        {
            using var ctx = new SqlContext(_connection);
            var page2 = ctx.GetTable<TestItem>()
                .OrderBy(x => x.Id)
                .Skip(3)
                .Take(3)
                .ToList();

            Assert.Equal(3, page2.Count);
            Assert.Equal("Item04", page2[0].Name);
            Assert.Equal("Item05", page2[1].Name);
            Assert.Equal("Item06", page2[2].Name);
        }

        [Fact]
        public void SkipTake_WithOrderByDescending()
        {
            using var ctx = new SqlContext(_connection);
            var items = ctx.GetTable<TestItem>()
                .OrderByDescending(x => x.Quantity)
                .Skip(2)
                .Take(3)
                .ToList();

            Assert.Equal(3, items.Count);
            Assert.Equal(80, items[0].Quantity); // 100, 90, [80, 70, 60]
            Assert.Equal(70, items[1].Quantity);
            Assert.Equal(60, items[2].Quantity);
        }

        [Fact]
        public async Task SkipTake_Async()
        {
            using var ctx = new SqlContext(_connection);
            var items = await ctx.GetTable<TestItem>()
                .OrderBy(x => x.Id)
                .Skip(5)
                .Take(2)
                .ToListAsync();

            Assert.Equal(2, items.Count);
            Assert.Equal("Item06", items[0].Name);
            Assert.Equal("Item07", items[1].Name);
        }

        [Fact]
        public void Take_WithWhere()
        {
            using var ctx = new SqlContext(_connection);
            var items = ctx.GetTable<TestItem>()
                .OrderBy(x => x.Id)
                .Take(2)
                .Where(x => x.Quantity >= 50);

            Assert.Equal(2, items.Count);
            Assert.Equal("Item05", items[0].Name);
            Assert.Equal("Item06", items[1].Name);
        }

        [Fact]
        public async Task Take_WithWhereAsync()
        {
            using var ctx = new SqlContext(_connection);
            var items = await ctx.GetTable<TestItem>()
                .OrderBy(x => x.Id)
                .Skip(1)
                .Take(2)
                .WhereAsync(x => x.Quantity >= 50);

            Assert.Equal(2, items.Count);
            Assert.Equal("Item06", items[0].Name);
            Assert.Equal("Item07", items[1].Name);
        }

        [Fact]
        public void Take_WithoutOrderBy_StillWorks()
        {
            using var ctx = new SqlContext(_connection);
            // SQLite doesn't require ORDER BY for LIMIT
            var items = ctx.GetTable<TestItem>()
                .Take(3)
                .ToList();

            Assert.Equal(3, items.Count);
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
