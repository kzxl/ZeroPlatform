using Microsoft.Data.Sqlite;
using System.Linq;
using Xunit;

namespace ZeroData.Sql.Tests
{
    public class SelectTests : IDisposable
    {
        private readonly SqliteConnection _connection;

        // DTO for projection tests
        public class ItemDto
        {
            public string Name { get; set; }
            public int Quantity { get; set; }
        }

        public class IdNameDto
        {
            public long Id { get; set; }
            public string Name { get; set; }
        }

        public SelectTests()
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

            using var ctx = new SqlContext(_connection);
            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "Cherry", Quantity = 30, Price = 3.00m });
            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "Apple", Quantity = 10, Price = 1.50m });
            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "Banana", Quantity = 20, Price = 2.00m });
            ctx.SubmitChanges();
        }

        [Fact]
        public void Select_DTO_Projection()
        {
            using var ctx = new SqlContext(_connection);
            var items = ctx.GetTable<TestItem>()
                .Select(x => new ItemDto { Name = x.Name, Quantity = x.Quantity });

            Assert.Equal(3, items.Count);
            Assert.Contains(items, i => i.Name == "Cherry" && i.Quantity == 30);
            Assert.Contains(items, i => i.Name == "Apple" && i.Quantity == 10);
        }

        [Fact]
        public void Select_DTO_WithWhere()
        {
            using var ctx = new SqlContext(_connection);
            var items = ctx.GetTable<TestItem>()
                .Select(x => new ItemDto { Name = x.Name, Quantity = x.Quantity },
                        x => x.Quantity > 15);

            Assert.Equal(2, items.Count);
            Assert.Contains(items, i => i.Name == "Cherry");
            Assert.Contains(items, i => i.Name == "Banana");
        }

        [Fact]
        public void Select_WithOrderBy()
        {
            using var ctx = new SqlContext(_connection);
            var items = ctx.GetTable<TestItem>()
                .OrderBy(x => x.Name)
                .Select(x => new ItemDto { Name = x.Name, Quantity = x.Quantity });

            Assert.Equal(3, items.Count);
            Assert.Equal("Apple", items[0].Name);
            Assert.Equal("Banana", items[1].Name);
            Assert.Equal("Cherry", items[2].Name);
        }

        [Fact]
        public void Select_WithSkipTake()
        {
            using var ctx = new SqlContext(_connection);
            var items = ctx.GetTable<TestItem>()
                .OrderBy(x => x.Id)
                .Skip(1)
                .Take(1)
                .Select(x => new IdNameDto { Id = x.Id, Name = x.Name });

            Assert.Single(items);
            Assert.Equal("Apple", items[0].Name);
        }

        [Fact]
        public async Task SelectAsync_DTO()
        {
            using var ctx = new SqlContext(_connection);
            var items = await ctx.GetTable<TestItem>()
                .SelectAsync(x => new ItemDto { Name = x.Name, Quantity = x.Quantity });

            Assert.Equal(3, items.Count);
        }

        [Fact]
        public async Task SelectAsync_WithWhere()
        {
            using var ctx = new SqlContext(_connection);
            var items = await ctx.GetTable<TestItem>()
                .SelectAsync(x => new IdNameDto { Id = x.Id, Name = x.Name },
                             x => x.Name == "Apple");

            Assert.Single(items);
            Assert.Equal("Apple", items[0].Name);
        }

        [Fact]
        public void Select_WithOrderByAndWhere()
        {
            using var ctx = new SqlContext(_connection);
            var items = ctx.GetTable<TestItem>()
                .OrderByDescending(x => x.Quantity)
                .Select(x => new ItemDto { Name = x.Name, Quantity = x.Quantity },
                        x => x.Quantity >= 20);

            Assert.Equal(2, items.Count);
            Assert.Equal("Cherry", items[0].Name);
            Assert.Equal("Banana", items[1].Name);
        }

        [Fact]
        public async Task SelectAsync_WithOrderBy()
        {
            using var ctx = new SqlContext(_connection);
            var items = await ctx.GetTable<TestItem>()
                .OrderBy(x => x.Quantity)
                .SelectAsync(x => new ItemDto { Name = x.Name, Quantity = x.Quantity });

            Assert.Equal(3, items.Count);
            Assert.Equal("Apple", items[0].Name);
            Assert.Equal("Banana", items[1].Name);
            Assert.Equal("Cherry", items[2].Name);
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
