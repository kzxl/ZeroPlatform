using Microsoft.Data.Sqlite;
using System.Linq;
using Xunit;

namespace ZeroData.Sql.Tests
{
    public class Sprint2Tests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public Sprint2Tests()
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

        #region Phase 9 — ExecuteInTransaction

        [Fact]
        public void ExecuteInTransaction_CommitsOnSuccess()
        {
            using var ctx = new SqlContext(_connection);

            ctx.ExecuteInTransaction(db =>
            {
                db.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "A", Quantity = 1 });
                db.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "B", Quantity = 2 });
                db.SubmitChanges();
            });

            // Verify data committed
            using var ctx2 = new SqlContext(_connection);
            Assert.Equal(2, ctx2.GetTable<TestItem>().ToList().Count);
        }

        [Fact]
        public void ExecuteInTransaction_RollsBackOnException()
        {
            using var ctx = new SqlContext(_connection);

            // Insert initial data
            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "Initial", Quantity = 0 });
            ctx.SubmitChanges();

            // Transaction that throws
            Assert.Throws<InvalidOperationException>(() =>
            {
                ctx.ExecuteInTransaction(db =>
                {
                    db.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "ShouldRollBack", Quantity = 99 });
                    db.SubmitChanges();
                    throw new InvalidOperationException("Simulated failure");
                });
            });

            // Verify only initial data exists (rollback should have removed "ShouldRollBack")
            using var ctx2 = new SqlContext(_connection);
            var items = ctx2.GetTable<TestItem>().ToList();
            Assert.Single(items);
            Assert.Equal("Initial", items[0].Name);
        }

        [Fact]
        public async Task ExecuteInTransactionAsync_CommitsOnSuccess()
        {
            using var ctx = new SqlContext(_connection);

            await ctx.ExecuteInTransactionAsync(async db =>
            {
                db.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "AsyncA", Quantity = 10 });
                await db.SubmitChangesAsync();
            });

            using var ctx2 = new SqlContext(_connection);
            var items = ctx2.GetTable<TestItem>().ToList();
            Assert.Single(items);
            Assert.Equal("AsyncA", items[0].Name);
        }

        #endregion

        #region Phase 8.2 — InsertAndGetId

        [Fact]
        public void InsertAndGetId_ReturnsGeneratedId()
        {
            using var ctx = new SqlContext(_connection);
            var entity = new TestItem { Name = "Widget", Quantity = 5, Price = 1.0m };

            var id = ctx.InsertAndGetId(entity);

            Assert.True(id > 0);
            Assert.Equal(id, entity.Id);

            // Verify persisted
            using var ctx2 = new SqlContext(_connection);
            var item = ctx2.GetTable<TestItem>().Find(id);
            Assert.NotNull(item);
            Assert.Equal("Widget", item.Name);
        }

        [Fact]
        public void InsertAndGetId_MultipleInserts_IncrementingIds()
        {
            using var ctx = new SqlContext(_connection);
            var id1 = ctx.InsertAndGetId(new TestItem { Name = "First", Quantity = 1 });
            var id2 = ctx.InsertAndGetId(new TestItem { Name = "Second", Quantity = 2 });
            var id3 = ctx.InsertAndGetId(new TestItem { Name = "Third", Quantity = 3 });

            Assert.Equal(1, id1);
            Assert.Equal(2, id2);
            Assert.Equal(3, id3);
        }

        [Fact]
        public async Task InsertAndGetIdAsync_ReturnsGeneratedId()
        {
            using var ctx = new SqlContext(_connection);
            var entity = new TestItem { Name = "AsyncWidget", Quantity = 10 };

            var id = await ctx.InsertAndGetIdAsync(entity);

            Assert.True(id > 0);
            Assert.Equal(id, entity.Id);
        }

        #endregion

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
