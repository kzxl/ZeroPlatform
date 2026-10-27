using Microsoft.Data.Sqlite;
using System;
using System.Linq;
using System.Threading.Tasks;
using ZeroData.Sql.Mapping;
using Xunit;

namespace ZeroData.Sql.Tests
{
    public class Phase4Tests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public Phase4Tests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            using (var cmd = _connection.CreateCommand())
            {
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

            // Seed
            using (var ctx = new SqlContext(_connection))
            {
                var t = ctx.GetTable<TestItem>();
                t.InsertOnSubmit(new TestItem { Name = "Async1", Quantity = 10, Price = 1.99m });
                t.InsertOnSubmit(new TestItem { Name = "Async2", Quantity = 20, Price = 2.99m });
                t.InsertOnSubmit(new TestItem { Name = "Async3", Quantity = 30, Price = 3.99m });
                ctx.SubmitChanges();
            }
        }

        // --- Async API Tests ---

        [Fact]
        public async Task SubmitChangesAsync_InsertsRow()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var item = new TestItem { Name = "AsyncInsert", Quantity = 99 };
                ctx.GetTable<TestItem>().InsertOnSubmit(item);
                await ctx.SubmitChangesAsync();
                Assert.True(item.Id > 0);
            }

            using (var ctx = new SqlContext(_connection))
            {
                Assert.NotNull(ctx.GetTable<TestItem>().FirstOrDefault(x => x.Name == "AsyncInsert"));
            }
        }

        [Fact]
        public async Task SubmitChangesAsync_UpdatesRow()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var item = ctx.GetTable<TestItem>().FirstOrDefault(x => x.Name == "Async1");
                Assert.NotNull(item);
                item.Quantity = 777;
                await ctx.SubmitChangesAsync();
            }

            using (var ctx = new SqlContext(_connection))
            {
                var item = ctx.GetTable<TestItem>().FirstOrDefault(x => x.Name == "Async1");
                Assert.Equal(777, item.Quantity);
            }
        }

        [Fact]
        public async Task WhereAsync_ReturnsFiltered()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var items = await ctx.GetTable<TestItem>().WhereAsync(x => x.Quantity > 15);
                Assert.Equal(2, items.Count);
            }
        }

        [Fact]
        public async Task FirstOrDefaultAsync_ReturnsMatch()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var item = await ctx.GetTable<TestItem>().FirstOrDefaultAsync(x => x.Name == "Async2");
                Assert.NotNull(item);
                Assert.Equal(20, item.Quantity);
            }
        }

        [Fact]
        public async Task CountAsync_ReturnsCorrect()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var count = await ctx.GetTable<TestItem>().CountAsync(x => x.Quantity >= 20);
                Assert.Equal(2, count);
            }
        }

        [Fact]
        public async Task AnyAsync_Works()
        {
            using (var ctx = new SqlContext(_connection))
            {
                Assert.True(await ctx.GetTable<TestItem>().AnyAsync(x => x.Name == "Async1"));
                Assert.False(await ctx.GetTable<TestItem>().AnyAsync(x => x.Name == "NotExist"));
            }
        }

        [Fact]
        public async Task ToListAsync_LoadsAll()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var all = await ctx.GetTable<TestItem>().ToListAsync();
                Assert.Equal(3, all.Count);
            }
        }

        [Fact]
        public async Task ExecuteQueryAsync_Works()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var results = await ctx.ExecuteQueryAsync<TestItem>("SELECT * FROM Items WHERE Quantity > {0}", 15);
                Assert.Equal(2, results.Count());
            }
        }

        [Fact]
        public async Task ExecuteCommandAsync_Works()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var affected = await ctx.ExecuteCommandAsync(
                    "UPDATE Items SET Quantity = {0} WHERE Name = {1}", 0, "Async3");
                Assert.Equal(1, affected);
            }
        }

        // --- Find / FindAsync Tests ---

        [Fact]
        public void Find_ByPrimaryKey_ReturnsEntity()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var item = ctx.GetTable<TestItem>().Find(1L);
                Assert.NotNull(item);
                Assert.Equal("Async1", item.Name);
            }
        }

        [Fact]
        public void Find_NonExistent_ReturnsNull()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var item = ctx.GetTable<TestItem>().Find(999L);
                Assert.Null(item);
            }
        }

        [Fact]
        public async Task FindAsync_ByPrimaryKey_ReturnsEntity()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var item = await ctx.GetTable<TestItem>().FindAsync(2L);
                Assert.NotNull(item);
                Assert.Equal("Async2", item.Name);
            }
        }

        // --- AsNoTracking Tests ---

        [Fact]
        public void AsNoTracking_DoesNotTrackChanges()
        {
            using (var ctx = new SqlContext(_connection))
            {
                // Load with no tracking
                var item = ctx.GetTable<TestItem>().AsNoTracking()
                    .FirstOrDefault(x => x.Name == "Async1");
                Assert.NotNull(item);

                // Modify should NOT be detected
                item.Quantity = 12345;
                ctx.SubmitChanges(); // Should be no-op
            }

            // Verify NOT updated
            using (var ctx = new SqlContext(_connection))
            {
                var item = ctx.GetTable<TestItem>().FirstOrDefault(x => x.Name == "Async1");
                Assert.NotEqual(12345, item.Quantity); // Should still be original value
            }
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
