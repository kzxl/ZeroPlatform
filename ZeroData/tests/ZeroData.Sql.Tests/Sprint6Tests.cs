using ZeroData.Sql.Mapping;
using ZeroData.Sql.Sql;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;

namespace ZeroData.Sql.Tests
{
    #region Test Entities

    [Table(Name = "Products")]
    public class ProductWithVersion
    {
        [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
        public long Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; }

        [Column(Name = "Price")]
        public decimal Price { get; set; }

        [Column(Name = "IsDeleted")]
        public bool IsDeleted { get; set; }

        [Column(Name = "Category")]
        public string Category { get; set; }
    }

    [Table(Name = "UpsertItems")]
    public class UpsertItem
    {
        [Column(Name = "Code", IsPrimaryKey = true)]
        public string Code { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; }

        [Column(Name = "Value")]
        public int Value { get; set; }
    }

    #endregion

    public class Sprint6Tests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public Sprint6Tests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE Products (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Price REAL NOT NULL DEFAULT 0,
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    Category TEXT
                );
                CREATE TABLE UpsertItems (
                    Code TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Value INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE Items (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Quantity INTEGER NOT NULL DEFAULT 0,
                    Price REAL
                );
            ";
            cmd.ExecuteNonQuery();

            SeedProducts();
        }

        private void SeedProducts()
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Products (Name, Price, IsDeleted, Category) VALUES ('Phone', 999, 0, 'Electronics');
                INSERT INTO Products (Name, Price, IsDeleted, Category) VALUES ('Laptop', 1499, 0, 'Electronics');
                INSERT INTO Products (Name, Price, IsDeleted, Category) VALUES ('Shirt', 29, 0, 'Clothing');
                INSERT INTO Products (Name, Price, IsDeleted, Category) VALUES ('Pants', 49, 0, 'Clothing');
                INSERT INTO Products (Name, Price, IsDeleted, Category) VALUES ('OldPhone', 99, 1, 'Electronics');
                INSERT INTO Products (Name, Price, IsDeleted, Category) VALUES ('Cheap', 5, 0, 'Misc');
                INSERT INTO Products (Name, Price, IsDeleted, Category) VALUES ('Expensive', 9999, 0, 'Misc');
            ";
            cmd.ExecuteNonQuery();
        }

        #region Feature 1 — Pagination

        [Fact]
        public void Pagination_Page1_CorrectItems()
        {
            using var ctx = new SqlContext(_connection);
            var result = ctx.GetTable<ProductWithVersion>()
                .OrderBy(x => x.Name)
                .ToPagedResult(1, 3);

            Assert.Equal(3, result.Items.Count);
            Assert.Equal(7, result.TotalCount);
            Assert.Equal(3, result.TotalPages);
            Assert.Equal(1, result.CurrentPage);
            Assert.Equal(3, result.PageSize);
            Assert.True(result.HasNext);
            Assert.False(result.HasPrevious);
        }

        [Fact]
        public void Pagination_Page2_CorrectItems()
        {
            using var ctx = new SqlContext(_connection);
            var result = ctx.GetTable<ProductWithVersion>()
                .OrderBy(x => x.Name)
                .ToPagedResult(2, 3);

            Assert.Equal(3, result.Items.Count);
            Assert.Equal(7, result.TotalCount);
            Assert.Equal(2, result.CurrentPage);
            Assert.True(result.HasNext);
            Assert.True(result.HasPrevious);
        }

        [Fact]
        public void Pagination_LastPage_PartialItems()
        {
            using var ctx = new SqlContext(_connection);
            var result = ctx.GetTable<ProductWithVersion>()
                .OrderBy(x => x.Name)
                .ToPagedResult(3, 3);

            Assert.Equal(1, result.Items.Count); // 7 items, page 3 of size 3 = 1 item
            Assert.False(result.HasNext);
            Assert.True(result.HasPrevious);
        }

        [Fact]
        public void Pagination_WithPredicate_FiltersCorrectly()
        {
            using var ctx = new SqlContext(_connection);
            var result = ctx.GetTable<ProductWithVersion>()
                .OrderBy(x => x.Name)
                .ToPagedResult(1, 10, x => x.Category == "Electronics");

            Assert.Equal(3, result.TotalCount); // Phone, Laptop, OldPhone
            Assert.Equal(1, result.TotalPages);
        }

        [Fact]
        public async Task Pagination_Async_Works()
        {
            using var ctx = new SqlContext(_connection);
            var result = await ctx.GetTable<ProductWithVersion>()
                .OrderBy(x => x.Name)
                .ToPagedResultAsync(1, 2);

            Assert.Equal(2, result.Items.Count);
            Assert.Equal(7, result.TotalCount);
            Assert.Equal(4, result.TotalPages);
        }

        [Fact]
        public void Pagination_InvalidPage_ThrowsArgumentOutOfRange()
        {
            using var ctx = new SqlContext(_connection);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ctx.GetTable<ProductWithVersion>().OrderBy(x => x.Name).ToPagedResult(0, 10));
        }

        #endregion

        #region Feature 2 — Global Query Filters

        [Fact]
        public void GlobalFilter_SoftDelete_AutoApplied()
        {
            using var ctx = new SqlContext(_connection);
            ctx.Filters.Add<ProductWithVersion>(x => x.IsDeleted == false);

            var products = ctx.GetTable<ProductWithVersion>().ToList();
            Assert.Equal(6, products.Count); // 7 total - 1 deleted = 6
            Assert.DoesNotContain(products, p => p.Name == "OldPhone");
        }

        [Fact]
        public void GlobalFilter_CombinesWithUserWhere()
        {
            using var ctx = new SqlContext(_connection);
            ctx.Filters.Add<ProductWithVersion>(x => x.IsDeleted == false);

            var electronics = ctx.GetTable<ProductWithVersion>()
                .Where(x => x.Category == "Electronics");

            Assert.Equal(2, electronics.Count); // Phone, Laptop (OldPhone filtered)
        }

        [Fact]
        public void GlobalFilter_IgnoreFilters_BypassesFilter()
        {
            using var ctx = new SqlContext(_connection);
            ctx.Filters.Add<ProductWithVersion>(x => x.IsDeleted == false);

            var all = ctx.GetTable<ProductWithVersion>().IgnoreFilters().ToList();
            Assert.Equal(7, all.Count); // All including deleted
        }

        [Fact]
        public void GlobalFilter_MultipleFilters_ANDCombined()
        {
            using var ctx = new SqlContext(_connection);
            ctx.Filters.Add<ProductWithVersion>(x => x.IsDeleted == false);
            ctx.Filters.Add<ProductWithVersion>(x => x.Category == "Electronics");

            var products = ctx.GetTable<ProductWithVersion>().ToList();
            Assert.Equal(2, products.Count); // Phone, Laptop (not deleted + Electronics only)
        }

        [Fact]
        public void GlobalFilter_CountRespectsFilter()
        {
            using var ctx = new SqlContext(_connection);
            ctx.Filters.Add<ProductWithVersion>(x => x.IsDeleted == false);

            var count = ctx.GetTable<ProductWithVersion>().Count(x => x.Category == "Electronics");
            Assert.Equal(2, count);
        }

        #endregion

        #region Feature 3 — Batch Delete/Update

        [Fact]
        public void BatchDelete_DeletesByPredicate()
        {
            using var ctx = new SqlContext(_connection);
            var deleted = ctx.GetTable<ProductWithVersion>()
                .BatchDelete(x => x.Price < 10);

            Assert.Equal(1, deleted); // "Cheap" at $5

            var remaining = ctx.GetTable<ProductWithVersion>().ToList();
            Assert.Equal(6, remaining.Count);
            Assert.DoesNotContain(remaining, p => p.Name == "Cheap");
        }

        [Fact]
        public async Task BatchDelete_Async()
        {
            using var ctx = new SqlContext(_connection);
            var deleted = await ctx.GetTable<ProductWithVersion>()
                .BatchDeleteAsync(x => x.Category == "Clothing");

            Assert.Equal(2, deleted); // Shirt, Pants
        }

        [Fact]
        public void BatchUpdate_UpdatesByPredicate()
        {
            using var ctx = new SqlContext(_connection);
            var updated = ctx.GetTable<ProductWithVersion>()
                .BatchUpdate(
                    x => x.Category == "Electronics",
                    x => new ProductWithVersion { Price = 0 });

            Assert.Equal(3, updated); // Phone, Laptop, OldPhone

            var electronics = ctx.GetTable<ProductWithVersion>()
                .Where(x => x.Category == "Electronics");
            Assert.All(electronics, e => Assert.Equal(0, (double)e.Price));
        }

        [Fact]
        public async Task BatchUpdate_Async()
        {
            using var ctx = new SqlContext(_connection);
            var updated = await ctx.GetTable<ProductWithVersion>()
                .BatchUpdateAsync(
                    x => x.Name == "Cheap",
                    x => new ProductWithVersion { Name = "Budget", Price = 10 });

            Assert.Equal(1, updated);

            var item = ctx.GetTable<ProductWithVersion>()
                .Where(x => x.Name == "Budget");
            Assert.Single(item);
            Assert.Equal(10, (double)item[0].Price);
        }

        [Fact]
        public void BatchDelete_NoMatch_ReturnsZero()
        {
            using var ctx = new SqlContext(_connection);
            var deleted = ctx.GetTable<ProductWithVersion>()
                .BatchDelete(x => x.Name == "NONEXISTENT");
            Assert.Equal(0, deleted);
        }

        #endregion

        #region Feature 4 — Query Profiler

        [Fact]
        public void Profiler_TracksQueryCount()
        {
            using var ctx = new SqlContext(_connection);
            ctx.Profiler.Enabled = true;

            var sw1 = ctx.Profiler.StartQuery();
            ctx.GetTable<ProductWithVersion>().ToList();
            ctx.Profiler.StopQuery(sw1, "SELECT * FROM Products");

            var sw2 = ctx.Profiler.StartQuery();
            ctx.GetTable<ProductWithVersion>().Count(x => x.Category == "Electronics");
            ctx.Profiler.StopQuery(sw2, "SELECT COUNT(*)");

            Assert.Equal(2, ctx.Profiler.TotalQueries);
            Assert.True(ctx.Profiler.TotalDuration.TotalMilliseconds >= 0);
        }

        [Fact]
        public void Profiler_TracksSlowQuery()
        {
            var profiler = new QueryProfiler();
            profiler.Enabled = true;
            profiler.SlowQueryThreshold = TimeSpan.FromMilliseconds(1);

            string slowSql = null;
            profiler.OnSlowQuery += (sql, _) => slowSql = sql;

            var sw = profiler.StartQuery();
            System.Threading.Thread.Sleep(5); // Force >1ms
            profiler.StopQuery(sw, "SELECT * FROM BigTable");

            Assert.NotNull(slowSql);
            Assert.Equal("SELECT * FROM BigTable", slowSql);
        }

        [Fact]
        public void Profiler_Disabled_NoTracking()
        {
            var profiler = new QueryProfiler();
            // Enabled = false by default

            var sw = profiler.StartQuery();
            Assert.Null(sw);
            Assert.Equal(0, profiler.TotalQueries);
        }

        [Fact]
        public void Profiler_Reset_ClearsStats()
        {
            var profiler = new QueryProfiler();
            profiler.Enabled = true;

            var sw = profiler.StartQuery();
            profiler.StopQuery(sw, "test");
            Assert.Equal(1, profiler.TotalQueries);

            profiler.Reset();
            Assert.Equal(0, profiler.TotalQueries);
            Assert.Null(profiler.SlowestQuery);
        }

        #endregion

        #region Feature 5 — Upsert

        [Fact]
        public void Upsert_Insert_NewRecord()
        {
            using var ctx = new SqlContext(_connection);
            ctx.Upsert(new UpsertItem { Code = "A001", Name = "Widget", Value = 100 });

            var item = ctx.GetTable<UpsertItem>().Where(x => x.Code == "A001");
            Assert.Single(item);
            Assert.Equal("Widget", item[0].Name);
            Assert.Equal(100, item[0].Value);
        }

        [Fact]
        public void Upsert_Update_ExistingRecord()
        {
            using var ctx = new SqlContext(_connection);
            ctx.Upsert(new UpsertItem { Code = "B001", Name = "Original", Value = 50 });
            ctx.Upsert(new UpsertItem { Code = "B001", Name = "Updated", Value = 200 });

            var items = ctx.GetTable<UpsertItem>().Where(x => x.Code == "B001");
            Assert.Single(items);
            Assert.Equal("Updated", items[0].Name);
            Assert.Equal(200, items[0].Value);
        }

        [Fact]
        public async Task Upsert_Async_Works()
        {
            using var ctx = new SqlContext(_connection);
            await ctx.UpsertAsync(new UpsertItem { Code = "C001", Name = "Async", Value = 300 });

            var items = ctx.GetTable<UpsertItem>().Where(x => x.Code == "C001");
            Assert.Single(items);
            Assert.Equal("Async", items[0].Name);
        }

        [Fact]
        public void Upsert_MultipleItems_IndependentOperations()
        {
            using var ctx = new SqlContext(_connection);
            ctx.Upsert(new UpsertItem { Code = "D1", Name = "First", Value = 1 });
            ctx.Upsert(new UpsertItem { Code = "D2", Name = "Second", Value = 2 });
            ctx.Upsert(new UpsertItem { Code = "D1", Name = "First Updated", Value = 10 });

            var all = ctx.GetTable<UpsertItem>().ToList();
            Assert.Equal(2, all.Count);
            Assert.Equal("First Updated", all.First(x => x.Code == "D1").Name);
        }

        #endregion

        #region Feature 6 — Concurrency

        [Fact]
        public void ConcurrencyException_ContainsEntityInfo()
        {
            var entity = new ProductWithVersion { Id = 1, Name = "Test" };
            var ex = new ConcurrencyException(entity, typeof(ProductWithVersion));

            Assert.Contains("ProductWithVersion", ex.Message);
            Assert.Same(entity, ex.Entity);
            Assert.Equal(typeof(ProductWithVersion), ex.EntityType);
        }

        [Fact]
        public void IsVersion_DetectedInMapping()
        {
            var mapping = MappingCache.GetMapping<VersionedEntity>();
            var versionCol = mapping.Columns.FirstOrDefault(c => c.IsVersion);
            Assert.NotNull(versionCol);
            Assert.Equal("RowVersion", versionCol.ColumnName);
        }

        #endregion

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }

    [Table(Name = "VersionedEntities")]
    public class VersionedEntity
    {
        [Column(Name = "Id", IsPrimaryKey = true)]
        public int Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; }

        [Column(Name = "RowVersion", IsVersion = true)]
        public int RowVersion { get; set; }
    }
}
