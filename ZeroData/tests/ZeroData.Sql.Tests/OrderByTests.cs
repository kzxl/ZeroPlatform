using Microsoft.Data.Sqlite;
using System.Linq;
using ZeroData.Sql.Mapping;
using Xunit;

namespace ZeroData.Sql.Tests
{
    public class OrderByTests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public OrderByTests()
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

            // Seed test data
            using var ctx = new SqlContext(_connection);
            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "Cherry", Quantity = 30, Price = 3.00m });
            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "Apple", Quantity = 10, Price = 1.50m });
            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "Banana", Quantity = 20, Price = 2.00m });
            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "Apple", Quantity = 5, Price = 1.00m });
            ctx.SubmitChanges();
        }

        [Fact]
        public void OrderBy_Ascending()
        {
            using var ctx = new SqlContext(_connection);
            var items = ctx.GetTable<TestItem>()
                .OrderBy(x => x.Name)
                .ToList();

            Assert.Equal(4, items.Count);
            Assert.Equal("Apple", items[0].Name);
            Assert.Equal("Apple", items[1].Name);
            Assert.Equal("Banana", items[2].Name);
            Assert.Equal("Cherry", items[3].Name);
        }

        [Fact]
        public void OrderByDescending_Descending()
        {
            using var ctx = new SqlContext(_connection);
            var items = ctx.GetTable<TestItem>()
                .OrderByDescending(x => x.Quantity)
                .ToList();

            Assert.Equal(4, items.Count);
            Assert.Equal(30, items[0].Quantity);
            Assert.Equal(20, items[1].Quantity);
            Assert.Equal(10, items[2].Quantity);
            Assert.Equal(5, items[3].Quantity);
        }

        [Fact]
        public void ThenBy_MultiColumn()
        {
            using var ctx = new SqlContext(_connection);
            var items = ctx.GetTable<TestItem>()
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Quantity)
                .ToList();

            Assert.Equal(4, items.Count);
            Assert.Equal("Apple", items[0].Name);
            Assert.Equal(5, items[0].Quantity);   // Apple qty=5 first
            Assert.Equal("Apple", items[1].Name);
            Assert.Equal(10, items[1].Quantity);  // Apple qty=10 second
            Assert.Equal("Banana", items[2].Name);
        }

        [Fact]
        public void OrderBy_WithWhere()
        {
            using var ctx = new SqlContext(_connection);
            var items = ctx.GetTable<TestItem>()
                .OrderByDescending(x => x.Quantity)
                .Where(x => x.Quantity > 5);

            Assert.Equal(3, items.Count);
            Assert.Equal(30, items[0].Quantity);
            Assert.Equal(20, items[1].Quantity);
            Assert.Equal(10, items[2].Quantity);
        }

        [Fact]
        public void OrderBy_WithFirstOrDefault()
        {
            using var ctx = new SqlContext(_connection);
            var item = ctx.GetTable<TestItem>()
                .OrderByDescending(x => x.Quantity)
                .FirstOrDefault(x => x.Quantity > 0);

            Assert.NotNull(item);
            Assert.Equal("Cherry", item.Name);
            Assert.Equal(30, item.Quantity);
        }

        [Fact]
        public async Task OrderByAsync_Works()
        {
            using var ctx = new SqlContext(_connection);
            var items = await ctx.GetTable<TestItem>()
                .OrderBy(x => x.Name)
                .ThenByDescending(x => x.Quantity)
                .WhereAsync(x => x.Quantity > 0);

            Assert.Equal(4, items.Count);
            Assert.Equal("Apple", items[0].Name);
            Assert.Equal(10, items[0].Quantity);  // Apple qty=10 first (desc)
            Assert.Equal("Apple", items[1].Name);
            Assert.Equal(5, items[1].Quantity);   // Apple qty=5 second
        }

        [Fact]
        public async Task OrderByAsync_FirstOrDefaultAsync()
        {
            using var ctx = new SqlContext(_connection);
            var item = await ctx.GetTable<TestItem>()
                .OrderBy(x => x.Price)
                .FirstOrDefaultAsync(x => x.Price > 0);

            Assert.NotNull(item);
            Assert.Equal(1.00m, item.Price);
        }

        [Fact]
        public void OrderBy_ResetAfterExecution()
        {
            using var ctx = new SqlContext(_connection);
            var table = ctx.GetTable<TestItem>();

            // First query with OrderBy
            var items1 = table.OrderByDescending(x => x.Quantity).Where(x => x.Quantity > 0);
            Assert.Equal(30, items1[0].Quantity);

            // Second query WITHOUT OrderBy - should not retain previous order
            var items2 = table.Where(x => x.Name == "Apple");
            Assert.Equal(2, items2.Count);
            // No ordering guarantee — just verify results are correct
        }

        [Fact]
        public void ThenBy_WithoutOrderBy_Throws()
        {
            using var ctx = new SqlContext(_connection);
            Assert.Throws<InvalidOperationException>(() =>
                ctx.GetTable<TestItem>().ThenBy(x => x.Name));
        }

        [Fact]
        public async Task OrderBy_ToListAsync()
        {
            using var ctx = new SqlContext(_connection);
            var items = await ctx.GetTable<TestItem>()
                .OrderBy(x => x.Name)
                .ToListAsync();

            Assert.Equal(4, items.Count);
            Assert.Equal("Apple", items[0].Name);
            Assert.Equal("Cherry", items[3].Name);
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
