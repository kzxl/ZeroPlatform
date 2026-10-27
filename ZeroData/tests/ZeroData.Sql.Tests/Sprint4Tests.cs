using ZeroData.Sql.ChangeTracking;
using Microsoft.Data.Sqlite;
using System.Linq;
using Xunit;

namespace ZeroData.Sql.Tests
{
    public class Sprint4Tests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public Sprint4Tests()
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

        #region Phase 12 — ChangeTracker API

        [Fact]
        public void GetState_LoadedEntity_ReturnsUnchanged()
        {
            using var ctx = new SqlContext(_connection);
            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "A", Quantity = 1 });
            ctx.SubmitChanges();

            using var ctx2 = new SqlContext(_connection);
            var item = ctx2.GetTable<TestItem>().Where(x => x.Name == "A").First();
            var state = ctx2.ChangeTracker.GetState(item);
            Assert.Equal(EntityState.Unchanged, state);
        }

        [Fact]
        public void GetState_InsertedEntity_ReturnsInsert()
        {
            using var ctx = new SqlContext(_connection);
            var item = new TestItem { Name = "B", Quantity = 2 };
            ctx.GetTable<TestItem>().InsertOnSubmit(item);
            var state = ctx.ChangeTracker.GetState(item);
            Assert.Equal(EntityState.Insert, state);
        }

        [Fact]
        public void GetState_DeletedEntity_ReturnsDelete()
        {
            using var ctx = new SqlContext(_connection);
            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "C", Quantity = 3 });
            ctx.SubmitChanges();

            using var ctx2 = new SqlContext(_connection);
            var item = ctx2.GetTable<TestItem>().Where(x => x.Name == "C").First();
            ctx2.GetTable<TestItem>().DeleteOnSubmit(item);
            var state = ctx2.ChangeTracker.GetState(item);
            Assert.Equal(EntityState.Delete, state);
        }

        [Fact]
        public void Entries_ReturnsAllTracked()
        {
            using var ctx = new SqlContext(_connection);
            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "X", Quantity = 1 });
            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "Y", Quantity = 2 });
            ctx.SubmitChanges();

            using var ctx2 = new SqlContext(_connection);
            ctx2.GetTable<TestItem>().ToList(); // load all — tracked as Unchanged
            ctx2.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "Z", Quantity = 3 });

            var entries = ctx2.ChangeTracker.Entries<TestItem>();
            Assert.Equal(3, entries.Count);
            Assert.Equal(2, entries.Count(e => e.State == EntityState.Unchanged));
            Assert.Single(entries.Where(e => e.State == EntityState.Insert));
        }

        [Fact]
        public void HasChanges_TrueWhenPending()
        {
            using var ctx = new SqlContext(_connection);
            Assert.False(ctx.ChangeTracker.HasChanges);

            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "A", Quantity = 1 });
            Assert.True(ctx.ChangeTracker.HasChanges);
        }

        [Fact]
        public void IsTracking_ReturnsTrueForTrackedEntity()
        {
            using var ctx = new SqlContext(_connection);
            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "W", Quantity = 1 });
            ctx.SubmitChanges();

            using var ctx2 = new SqlContext(_connection);
            var item = ctx2.GetTable<TestItem>().Where(x => x.Name == "W").First();
            Assert.True(ctx2.ChangeTracker.IsTracking(item));

            var untracked = new TestItem { Name = "Not tracked" };
            Assert.False(ctx2.ChangeTracker.IsTracking(untracked));
        }

        #endregion

        #region Phase 13 — SaveHooks

        [Fact]
        public void OnBeforeSave_CalledOnInsert()
        {
            using var ctx = new SqlContext(_connection);
            var hookCalled = false;

            ctx.Hooks.OnBeforeSave<TestItem>((entity, state) =>
            {
                hookCalled = true;
                Assert.Equal(EntityState.Insert, state);
                // Auto-fill field
                if (entity.Price == null || entity.Price == 0) entity.Price = 9.99m;
            });

            var item = new TestItem { Name = "Hooked", Quantity = 1 };
            ctx.GetTable<TestItem>().InsertOnSubmit(item);
            ctx.SubmitChanges();

            Assert.True(hookCalled);
            // Verify the price was set by the hook
            using var ctx2 = new SqlContext(_connection);
            var saved = ctx2.GetTable<TestItem>().Where(x => x.Name == "Hooked").First();
            Assert.True(saved.Price > 9.0m, $"Expected ~9.99 but got {saved.Price}");
        }

        [Fact]
        public void OnAfterSave_CalledOnInsert()
        {
            using var ctx = new SqlContext(_connection);
            var afterCalled = false;

            ctx.Hooks.OnAfterSave<TestItem>((entity, state) =>
            {
                afterCalled = true;
                Assert.Equal(EntityState.Insert, state);
            });

            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "After", Quantity = 1 });
            ctx.SubmitChanges();

            Assert.True(afterCalled);
        }

        [Fact]
        public void OnBeforeSave_CalledOnDelete()
        {
            using var ctx = new SqlContext(_connection);
            ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "Del", Quantity = 1 });
            ctx.SubmitChanges();

            using var ctx2 = new SqlContext(_connection);
            var hookState = EntityState.Unchanged;
            ctx2.Hooks.OnBeforeSave<TestItem>((entity, state) =>
            {
                hookState = state;
            });

            var item = ctx2.GetTable<TestItem>().Where(x => x.Name == "Del").First();
            ctx2.GetTable<TestItem>().DeleteOnSubmit(item);
            ctx2.SubmitChanges();

            Assert.Equal(EntityState.Delete, hookState);
        }

        #endregion

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
