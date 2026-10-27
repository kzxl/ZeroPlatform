using Microsoft.Data.Sqlite;
using System;
using System.Linq;
using ZeroData.Sql.Mapping;
using Xunit;

namespace ZeroData.Sql.Tests
{
    public class Phase3Tests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public Phase3Tests()
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

            // Seed data
            using (var ctx = new SqlContext(_connection))
            {
                var table = ctx.GetTable<TestItem>();
                table.InsertOnSubmit(new TestItem { Name = "Alpha", Quantity = 10, Price = 1.99m });
                table.InsertOnSubmit(new TestItem { Name = "Beta", Quantity = 20, Price = 2.99m });
                table.InsertOnSubmit(new TestItem { Name = "Gamma", Quantity = 30, Price = 3.99m });
                ctx.SubmitChanges();
            }
        }

        [Fact]
        public void FirstOrDefault_WithPredicate_ReturnsMatch()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var item = ctx.GetTable<TestItem>().FirstOrDefault(x => x.Name == "Beta");
                Assert.NotNull(item);
                Assert.Equal("Beta", item.Name);
                Assert.Equal(20, item.Quantity);
            }
        }

        [Fact]
        public void FirstOrDefault_NoMatch_ReturnsNull()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var item = ctx.GetTable<TestItem>().FirstOrDefault(x => x.Name == "NonExistent");
                Assert.Null(item);
            }
        }

        [Fact]
        public void Count_WithPredicate_ReturnsCorrect()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var count = ctx.GetTable<TestItem>().Count(x => x.Quantity > 15);
                Assert.Equal(2, count);
            }
        }

        [Fact]
        public void Any_WithMatch_ReturnsTrue()
        {
            using (var ctx = new SqlContext(_connection))
            {
                Assert.True(ctx.GetTable<TestItem>().Any(x => x.Name == "Alpha"));
                Assert.False(ctx.GetTable<TestItem>().Any(x => x.Name == "Missing"));
            }
        }

        [Fact]
        public void Where_ServerSide_FiltersCorrectly()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var items = ctx.GetTable<TestItem>()
                    .Where(x => x.Price > 2.0m && x.Quantity > 15)
                    .ToList();
                Assert.Equal(2, items.Count);
                Assert.Contains(items, i => i.Name == "Beta");
                Assert.Contains(items, i => i.Name == "Gamma");
            }
        }

        [Fact]
        public void Attach_AsModified_UpdatesOnSubmit()
        {
            using (var ctx = new SqlContext(_connection))
            {
                // Create entity manually (not loaded from DB)
                var detached = new TestItem { Id = 1, Name = "Updated", Quantity = 999 };
                ctx.GetTable<TestItem>().Attach(detached, asModified: true);
                ctx.SubmitChanges();
            }

            // Verify update persisted
            using (var ctx = new SqlContext(_connection))
            {
                var item = ctx.GetTable<TestItem>().FirstOrDefault(x => x.Id == 1);
                Assert.NotNull(item);
                Assert.Equal("Updated", item.Name);
                Assert.Equal(999, item.Quantity);
            }
        }

        [Fact]
        public void UpdateTracking_DetectsModifiedEntity()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var item = ctx.GetTable<TestItem>().FirstOrDefault(x => x.Name == "Alpha");
                Assert.NotNull(item);

                // Modify the loaded entity
                item.Quantity = 42;
                item.Price = 9.99m;
                ctx.SubmitChanges();
            }

            // Verify update persisted
            using (var ctx = new SqlContext(_connection))
            {
                var item = ctx.GetTable<TestItem>().FirstOrDefault(x => x.Name == "Alpha");
                Assert.NotNull(item);
                Assert.Equal(42, item.Quantity);
                Assert.Equal(9.99m, item.Price);
            }
        }

        [Fact]
        public void Where_StringContains_Works()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var items = ctx.GetTable<TestItem>()
                    .Where(x => x.Name.Contains("eta"))
                    .ToList();
                Assert.Single(items);
                Assert.Equal("Beta", items[0].Name);
            }
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
