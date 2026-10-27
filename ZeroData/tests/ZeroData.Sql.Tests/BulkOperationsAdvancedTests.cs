using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using Xunit.Abstractions;
using ZeroData.Sql.Mapping;

namespace ZeroData.Sql.Tests
{
    [Table(Name = "AdvancedBulkItems")]
    public class AdvancedBulkItem
    {
        [Column(Name = "Id", IsPrimaryKey = true)]
        public int Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; }

        [Column(Name = "Score")]
        public double Score { get; set; }

        [Column(Name = "IsActive")]
        public bool IsActive { get; set; }
    }

    [Table(Name = "CompositeBulkItems")]
    public class CompositeBulkItem
    {
        [Column(Name = "TenantId", IsPrimaryKey = true)]
        public int TenantId { get; set; }

        [Column(Name = "ItemId", IsPrimaryKey = true)]
        public int ItemId { get; set; }

        [Column(Name = "Description")]
        public string Description { get; set; }
    }

    public class BulkOperationsAdvancedTests
    {
        private readonly ITestOutputHelper _output;

        public BulkOperationsAdvancedTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private SqliteConnection CreateDatabase()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE AdvancedBulkItems (
                        Id INTEGER PRIMARY KEY,
                        Name TEXT NOT NULL,
                        Score REAL NOT NULL,
                        IsActive INTEGER NOT NULL
                    );
                    CREATE TABLE CompositeBulkItems (
                        TenantId INTEGER NOT NULL,
                        ItemId INTEGER NOT NULL,
                        Description TEXT NOT NULL,
                        PRIMARY KEY (TenantId, ItemId)
                    );";
                cmd.ExecuteNonQuery();
            }

            return conn;
        }

        [Fact]
        public async Task BulkInsertAsync_InsertsLargeDatasetWithCompiledGetters()
        {
            using var conn = CreateDatabase();
            using var ctx = new SqlContext(conn);

            const int count = 2500;
            var items = Enumerable.Range(1, count).Select(i => new AdvancedBulkItem
            {
                Id = i,
                Name = $"Item_{i}",
                Score = i * 1.5,
                IsActive = i % 2 == 0
            }).ToList();

            var sw = Stopwatch.StartNew();
            int inserted = await ctx.BulkInsertAsync(items);
            sw.Stop();

            _output.WriteLine($"[BulkInsertAsync] Inserted {inserted} items in {sw.ElapsedMilliseconds} ms ({(inserted * 1000.0 / Math.Max(1, sw.ElapsedMilliseconds)):F0} rows/s)");

            Assert.Equal(count, inserted);

            var countResult = await ctx.GetTable<AdvancedBulkItem>().CountAsync();
            Assert.Equal(count, countResult);

            var first = await ctx.GetTable<AdvancedBulkItem>().FirstOrDefaultAsync(x => x.Id == 1);
            Assert.NotNull(first);
            Assert.Equal("Item_1", first.Name);
            Assert.Equal(1.5, first.Score);
        }

        [Fact]
        public async Task BulkUpdateAsync_UpdatesEntitiesByPrimaryKey()
        {
            using var conn = CreateDatabase();
            using var ctx = new SqlContext(conn);

            var items = Enumerable.Range(1, 100).Select(i => new AdvancedBulkItem
            {
                Id = i,
                Name = $"Original_{i}",
                Score = 0.0,
                IsActive = false
            }).ToList();

            await ctx.BulkInsertAsync(items);

            // Mutate items
            foreach (var item in items)
            {
                item.Name = $"Updated_{item.Id}";
                item.Score = item.Id * 10.0;
                item.IsActive = true;
            }

            int updated = await ctx.BulkUpdateAsync(items);
            Assert.Equal(100, updated);

            var sample = await ctx.GetTable<AdvancedBulkItem>().FirstOrDefaultAsync(x => x.Id == 50);
            Assert.NotNull(sample);
            Assert.Equal("Updated_50", sample.Name);
            Assert.Equal(500.0, sample.Score);
            Assert.True(sample.IsActive);
        }

        [Fact]
        public async Task BulkDeleteAsync_DeletesBySinglePrimaryKey()
        {
            using var conn = CreateDatabase();
            using var ctx = new SqlContext(conn);

            var items = Enumerable.Range(1, 200).Select(i => new AdvancedBulkItem
            {
                Id = i,
                Name = $"Item_{i}",
                Score = 1.0,
                IsActive = true
            }).ToList();

            await ctx.BulkInsertAsync(items);

            // Delete first 50 items
            var toDelete = items.Take(50).ToList();
            int deleted = await ctx.BulkDeleteAsync(toDelete);
            Assert.Equal(50, deleted);

            int remaining = await ctx.GetTable<AdvancedBulkItem>().CountAsync();
            Assert.Equal(150, remaining);

            var existsFirst = await ctx.GetTable<AdvancedBulkItem>().AnyAsync(x => x.Id == 1);
            Assert.False(existsFirst);

            var exists51 = await ctx.GetTable<AdvancedBulkItem>().AnyAsync(x => x.Id == 51);
            Assert.True(exists51);
        }

        [Fact]
        public async Task BulkDeleteAsync_CompositePrimaryKey_DeletesCorrectRows()
        {
            using var conn = CreateDatabase();
            using var ctx = new SqlContext(conn);

            var compositeItems = new List<CompositeBulkItem>
            {
                new CompositeBulkItem { TenantId = 1, ItemId = 100, Description = "T1-100" },
                new CompositeBulkItem { TenantId = 1, ItemId = 200, Description = "T1-200" },
                new CompositeBulkItem { TenantId = 2, ItemId = 100, Description = "T2-100" },
                new CompositeBulkItem { TenantId = 2, ItemId = 200, Description = "T2-200" }
            };

            await ctx.BulkInsertAsync(compositeItems);
            Assert.Equal(4, await ctx.GetTable<CompositeBulkItem>().CountAsync());

            // Delete only (Tenant 1, Item 100) and (Tenant 2, Item 200)
            var toDelete = new List<CompositeBulkItem>
            {
                new CompositeBulkItem { TenantId = 1, ItemId = 100 },
                new CompositeBulkItem { TenantId = 2, ItemId = 200 }
            };

            int deleted = await ctx.BulkDeleteAsync(toDelete);
            Assert.Equal(2, deleted);

            var remaining = await ctx.GetTable<CompositeBulkItem>().ToListAsync();
            Assert.Equal(2, remaining.Count);
            Assert.Contains(remaining, x => x.TenantId == 1 && x.ItemId == 200);
            Assert.Contains(remaining, x => x.TenantId == 2 && x.ItemId == 100);
        }

        [Fact]
        public async Task Table_BulkInsertAndBulkDelete_ErgonomicForwarding()
        {
            using var conn = CreateDatabase();
            using var ctx = new SqlContext(conn);
            var table = ctx.GetTable<AdvancedBulkItem>();

            var items = Enumerable.Range(1, 100).Select(i => new AdvancedBulkItem
            {
                Id = i,
                Name = $"TableItem_{i}",
                Score = i,
                IsActive = true
            }).ToList();

            int inserted = await table.BulkInsertAsync(items);
            Assert.Equal(100, inserted);

            int count = await table.CountAsync();
            Assert.Equal(100, count);

            int deleted = await table.BulkDeleteAsync(items.Take(25));
            Assert.Equal(25, deleted);
            Assert.Equal(75, await table.CountAsync());
        }
    }
}
