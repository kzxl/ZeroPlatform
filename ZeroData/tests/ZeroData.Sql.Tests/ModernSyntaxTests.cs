using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using ZeroData.Sql.Mapping;

namespace ZeroData.Sql.Tests
{
    public class ModernSyntaxTests : IDisposable
    {
        private readonly SqliteConnection _connection;

        [Table(Name = "ModernProducts")]
        public class ModernProduct
        {
            [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
            public long Id { get; set; }

            [Column(Name = "Name")]
            public string Name { get; set; }

            [Column(Name = "Price")]
            public decimal Price { get; set; }

            [Column(Name = "Stock")]
            public int Stock { get; set; }

            [Column(Name = "IsActive")]
            public bool IsActive { get; set; }
        }

        public ModernSyntaxTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE ModernProducts (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Price REAL NOT NULL,
                        Stock INTEGER NOT NULL DEFAULT 0,
                        IsActive INTEGER NOT NULL DEFAULT 1
                    );";
                cmd.ExecuteNonQuery();
            }
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }

        [Fact]
        public void ModernCrud_DirectInsertAndUpdateAndSave_Works()
        {
            using (var db = new SqlContext(_connection))
            {
                var p = new ModernProduct { Name = "Laptop", Price = 1500m, Stock = 10, IsActive = true };
                db.Insert(p);
                db.Save();

                Assert.True(p.Id > 0);

                // Fetch via fast Get
                var loaded = db.Get<ModernProduct>(p.Id);
                Assert.NotNull(loaded);
                Assert.Equal("Laptop", loaded.Name);

                // Update
                loaded.Price = 1400m;
                db.Update(loaded);
                db.SaveChanges();

                var reloaded = db.Get<ModernProduct>(p.Id);
                Assert.Equal(1400m, reloaded.Price);

                // Delete
                db.Delete(reloaded);
                db.Save();

                Assert.Null(db.Get<ModernProduct>(p.Id));
            }
        }

        [Fact]
        public async Task ModernAsyncCrud_Works()
        {
            using (var db = new SqlContext(_connection))
            {
                var p = new ModernProduct { Name = "Mouse", Price = 25m, Stock = 50, IsActive = true };
                db.Add(p);
                await db.SaveAsync();

                Assert.True(p.Id > 0);

                var loaded = await db.GetAsync<ModernProduct>(p.Id);
                Assert.NotNull(loaded);
                Assert.Equal("Mouse", loaded.Name);

                loaded.Stock = 45;
                db.Update(loaded);
                await db.SaveChangesAsync();

                var reloaded = await db.GetAsync<ModernProduct>(p.Id);
                Assert.Equal(45, reloaded.Stock);

                await db.DeleteByIdAsync<ModernProduct>(p.Id);
                Assert.Null(await db.GetAsync<ModernProduct>(p.Id));
            }
        }

        [Fact]
        public void DeleteById_DirectServerDelete_Works()
        {
            using (var db = new SqlContext(_connection))
            {
                var p = new ModernProduct { Name = "Keyboard", Price = 75m, Stock = 5, IsActive = true };
                db.Insert(p);
                db.Save();

                var rows = db.DeleteById<ModernProduct>(p.Id);
                Assert.Equal(1, rows);

                var loaded = db.Get<ModernProduct>(p.Id);
                Assert.Null(loaded);
            }
        }

        [Fact]
        public void DeleteWhere_ServerConditionDelete_Works()
        {
            using (var db = new SqlContext(_connection))
            {
                var list = new List<ModernProduct>
                {
                    new ModernProduct { Name = "Old 1", Price = 10m, Stock = 0, IsActive = false },
                    new ModernProduct { Name = "Old 2", Price = 20m, Stock = 0, IsActive = false },
                    new ModernProduct { Name = "Keep", Price = 30m, Stock = 10, IsActive = true }
                };
                db.InsertRange(list);
                db.Save();

                var deleted = db.GetTable<ModernProduct>().DeleteWhere(x => x.IsActive == false);
                Assert.Equal(2, deleted);

                var remaining = db.Query<ModernProduct>();
                Assert.Single(remaining);
                Assert.Equal("Keep", remaining[0].Name);
            }
        }

        [Fact]
        public async Task Query_ReadonlyNoTracking_Works()
        {
            using (var db = new SqlContext(_connection))
            {
                var list = new List<ModernProduct>
                {
                    new ModernProduct { Name = "Item A", Price = 100m, Stock = 20, IsActive = true },
                    new ModernProduct { Name = "Item B", Price = 200m, Stock = 5, IsActive = true },
                    new ModernProduct { Name = "Item C", Price = 50m, Stock = 0, IsActive = false }
                };
                db.InsertRange(list);
                await db.SaveAsync();

                var activeItems = await db.QueryAsync<ModernProduct>(x => x.IsActive);
                Assert.Equal(2, activeItems.Count);

                // Ensure no tracking entries were created for query
                Assert.Empty(db.ChangeTracker.GetPendingChanges());
            }
        }

        [Fact]
        public async Task QuerySqlAndExecuteSql_WithDapperParameters_Works()
        {
            using (var db = new SqlContext(_connection))
            {
                var p = new ModernProduct { Name = "Headphones", Price = 120m, Stock = 15, IsActive = true };
                db.Insert(p);
                db.Save();

                var items = await db.QuerySqlAsync<ModernProduct>(
                    "SELECT * FROM ModernProducts WHERE Price > @minPrice",
                    new { minPrice = 100m });

                Assert.Single(items);
                Assert.Equal("Headphones", items[0].Name);

                var affected = await db.ExecuteSqlAsync(
                    "UPDATE ModernProducts SET Stock = @newStock WHERE Id = @id",
                    new { newStock = 99, id = p.Id });

                Assert.Equal(1, affected);

                var updated = db.Get<ModernProduct>(p.Id);
                Assert.Equal(99, updated.Stock);
            }
        }

        [Fact]
        public void ChangeTracker_StateTransitions_InsertThenDelete_CancelsInsert()
        {
            using (var db = new SqlContext(_connection))
            {
                var p = new ModernProduct { Name = "Transient", Price = 50m, Stock = 1, IsActive = true };
                db.Insert(p);
                Assert.True(db.ChangeTracker.HasChanges);

                // Deleting an uncommitted inserted entity should cancel the insert
                db.Delete(p);
                Assert.False(db.ChangeTracker.HasChanges);
                Assert.Empty(db.ChangeTracker.GetPendingChanges());

                // Save does nothing
                db.Save();
                Assert.Null(db.Get<ModernProduct>(p.Id));
            }
        }

        [Fact]
        public void ChangeTracker_MultipleUpdates_DoesNotDuplicate()
        {
            using (var db = new SqlContext(_connection))
            {
                var p = new ModernProduct { Name = "SingleUpdate", Price = 10m, Stock = 5, IsActive = true };
                db.Insert(p);
                db.Save();

                p.Price = 12m;
                db.Update(p);
                p.Price = 15m;
                db.Update(p);

                var updates = db.ChangeTracker.GetPendingChanges()
                    .Where(e => ReferenceEquals(e.Entity, p) && e.State == ChangeTracking.EntityState.Update)
                    .ToList();

                Assert.Single(updates);
            }
        }

        [Fact]
        public void Detach_RemovesEntityFromTracking()
        {
            using (var db = new SqlContext(_connection))
            {
                var p = new ModernProduct { Name = "Detachable", Price = 90m, Stock = 3, IsActive = true };
                db.Insert(p);
                db.Save();

                var loaded = db.Get<ModernProduct>(p.Id);
                Assert.True(db.ChangeTracker.IsTracking(loaded));

                db.Detach(loaded);
                Assert.False(db.ChangeTracker.IsTracking(loaded));
            }
        }

        [Table(Name = "SoftDeleteItems")]
        [SoftDelete(ColumnName = "IsDeleted")]
        public class SoftDeleteItem
        {
            [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
            public long Id { get; set; }

            [Column(Name = "Title")]
            public string Title { get; set; }

            [Column(Name = "IsDeleted")]
            public bool IsDeleted { get; set; }
        }

        [Fact]
        public async Task SoftDelete_DeleteById_RespectsSoftDeleteAttribute()
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE SoftDeleteItems (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT NOT NULL,
                        IsDeleted INTEGER NOT NULL DEFAULT 0
                    );";
                cmd.ExecuteNonQuery();
            }

            using (var db = new SqlContext(_connection))
            {
                var item = new SoftDeleteItem { Title = "Task 1", IsDeleted = false };
                db.Insert(item);
                db.Save();

                // DeleteById without forceHardDelete should soft delete (update IsDeleted = 1)
                db.DeleteById<SoftDeleteItem>(item.Id);

                var reloaded = db.Get<SoftDeleteItem>(item.Id);
                Assert.NotNull(reloaded);
                Assert.True(reloaded.IsDeleted);

                // DeleteById with forceHardDelete should hard delete row
                await db.DeleteByIdAsync<SoftDeleteItem>(item.Id, forceHardDelete: true);
                var hardDeleted = db.Get<SoftDeleteItem>(item.Id);
                Assert.Null(hardDeleted);
            }
        }

        [Fact]
        public async Task AsyncSave_InvokesLifecycleHooks()
        {
            using (var db = new SqlContext(_connection))
            {
                bool beforeInvoked = false;
                bool afterInvoked = false;

                db.Hooks.OnBeforeSave<ModernProduct>((p, state) => { beforeInvoked = true; });
                db.Hooks.OnAfterSave<ModernProduct>((p, state) => { afterInvoked = true; });

                var p = new ModernProduct { Name = "HookTest", Price = 99m, Stock = 1, IsActive = true };
                db.Insert(p);
                await db.SaveAsync();

                Assert.True(beforeInvoked);
                Assert.True(afterInvoked);
            }
        }

        [Fact]
        public void SequentialSaves_TrackSubsequentModifications()
        {
            using (var db = new SqlContext(_connection))
            {
                var p = new ModernProduct { Name = "Initial", Price = 10m, Stock = 5, IsActive = true };
                db.Insert(p);
                db.Save();
                Assert.True(p.Id > 0);

                // Second save with modified property
                p.Name = "UpdatedInSameContext";
                p.Price = 25m;
                db.Save();

                var reloaded = db.Get<ModernProduct>(p.Id);
                Assert.NotNull(reloaded);
                Assert.Equal("UpdatedInSameContext", reloaded.Name);
                Assert.Equal(25m, reloaded.Price);
            }
        }

        [Fact]
        public void DeleteTrackedEntity_DoesNotEmitConflictingUpdate()
        {
            using (var db = new SqlContext(_connection))
            {
                var p = new ModernProduct { Name = "ToDelete", Price = 15m, Stock = 2, IsActive = true };
                db.Insert(p);
                db.Save();

                // Mutate property AND delete in same unit of work
                p.Name = "ModifiedBeforeDelete";
                db.Delete(p);

                // Should delete cleanly without trying to update
                db.Save();

                var reloaded = db.Get<ModernProduct>(p.Id);
                Assert.Null(reloaded);
            }
        }

        [Fact]
        public void DeleteWhere_DetachesTrackedEntitiesFromMemory()
        {
            using (var db = new SqlContext(_connection))
            {
                var p = new ModernProduct { Name = "DetachTest", Price = 30m, Stock = 1, IsActive = true };
                db.Insert(p);
                db.Save();

                Assert.True(db.ChangeTracker.IsTracking(p));

                // DeleteWhere directly on server
                db.DeleteWhere<ModernProduct>(x => x.Id == p.Id);

                // In-memory tracked entity should now be detached
                Assert.False(db.ChangeTracker.IsTracking(p));
            }
        }

        [Fact]
        public async Task ContextSetAndDirectDeleteWhere_Works()
        {
            using (var db = new SqlContext(_connection))
            {
                var p = new ModernProduct { Name = "SetTest", Price = 50m, Stock = 3, IsActive = true };
                db.Set<ModernProduct>().Insert(p);
                await db.SaveAsync();

                var found = await db.Set<ModernProduct>().FindAsync(p.Id);
                Assert.NotNull(found);

                var deletedRows = await db.DeleteWhereAsync<ModernProduct>(x => x.Id == p.Id);
                Assert.Equal(1, deletedRows);

                var afterDelete = await db.Set<ModernProduct>().FindAsync(p.Id);
                Assert.Null(afterDelete);
            }
        }

        [Fact]
        public void SelectProjection_RespectsGlobalQueryFilter()
        {
            using (var db = new SqlContext(_connection))
            {
                var p1 = new ModernProduct { Name = "ActiveOne", Price = 10m, Stock = 5, IsActive = true };
                var p2 = new ModernProduct { Name = "InactiveOne", Price = 20m, Stock = 0, IsActive = false };
                db.Insert(p1);
                db.Insert(p2);
                db.Save();

                // Add global filter for active products
                db.Filters.Add<ModernProduct>(x => x.IsActive);

                var names = db.GetTable<ModernProduct>()
                    .Select(x => x.Name);

                Assert.Contains("ActiveOne", names);
                Assert.DoesNotContain("InactiveOne", names);
            }
        }
    }
}
