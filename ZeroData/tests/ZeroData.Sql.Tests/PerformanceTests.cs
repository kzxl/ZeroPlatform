using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ZeroData.Sql.Mapping;
using Xunit;
using Xunit.Abstractions;

namespace ZeroData.Sql.Tests
{
    #region Test Models with FK

    [Table(Name = "Categories")]
    public class PerfCategory
    {
        [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
        public long Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; }
    }

    [Table(Name = "Warehouses")]
    public class PerfWarehouse
    {
        [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
        public long Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; }
    }

    [Table(Name = "Orders")]
    public class PerfOrder
    {
        [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
        public long Id { get; set; }

        [Column(Name = "Code")]
        public string Code { get; set; }

        [Column(Name = "CategoryId")]
        public long CategoryId { get; set; }

        [Column(Name = "WarehouseId")]
        public long WarehouseId { get; set; }

        [Column(Name = "Amount", CanBeNull = true)]
        public decimal? Amount { get; set; }

        // FK navigation properties
        [Association(Name = "FK_Order_Category", ThisKey = "CategoryId", OtherKey = "Id", IsForeignKey = true)]
        public PerfCategory PerfCategory { get; set; }

        [Association(Name = "FK_Order_Warehouse", ThisKey = "WarehouseId", OtherKey = "Id", IsForeignKey = true)]
        public PerfWarehouse PerfWarehouse { get; set; }
    }

    #endregion

    public class PerformanceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ITestOutputHelper _output;

        private const int CategoryCount = 5;
        private const int WarehouseCount = 3;
        private const int OrderCount = 100;

        public PerformanceTests(ITestOutputHelper output)
        {
            _output = output;
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            CreateSchema();
            SeedData();
        }

        private void CreateSchema()
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE Categories (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL
                    );
                    CREATE TABLE Warehouses (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL
                    );
                    CREATE TABLE Orders (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Code TEXT NOT NULL,
                        CategoryId INTEGER NOT NULL,
                        WarehouseId INTEGER NOT NULL,
                        Amount REAL,
                        FOREIGN KEY (CategoryId) REFERENCES Categories(Id),
                        FOREIGN KEY (WarehouseId) REFERENCES Warehouses(Id)
                    );
                ";
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedData()
        {
            using (var ctx = new SqlContext(_connection))
            {
                // Seed categories
                for (int i = 1; i <= CategoryCount; i++)
                    ctx.GetTable<PerfCategory>().InsertOnSubmit(new PerfCategory { Name = $"Category_{i}" });
                ctx.SubmitChanges();

                // Seed warehouses
                for (int i = 1; i <= WarehouseCount; i++)
                    ctx.GetTable<PerfWarehouse>().InsertOnSubmit(new PerfWarehouse { Name = $"Warehouse_{i}" });
                ctx.SubmitChanges();

                // Seed orders (distributed across categories & warehouses)
                for (int i = 1; i <= OrderCount; i++)
                {
                    ctx.GetTable<PerfOrder>().InsertOnSubmit(new PerfOrder
                    {
                        Code = $"ORD-{i:D4}",
                        CategoryId = (i % CategoryCount) + 1,
                        WarehouseId = (i % WarehouseCount) + 1,
                        Amount = 100m + i
                    });
                }
                ctx.SubmitChanges();
            }
        }

        // ─── FK Auto-Load Tests ───────────────────────────────────

        [Fact]
        public void AutoLoad_AllFKs_PopulatesNavProperties()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var order = ctx.GetTable<PerfOrder>().FirstOrDefault(o => o.Id == 1);

                Assert.NotNull(order);
                Assert.NotNull(order.PerfCategory);
                Assert.NotNull(order.PerfWarehouse);
                Assert.StartsWith("Category_", order.PerfCategory.Name);
                Assert.StartsWith("Warehouse_", order.PerfWarehouse.Name);

                _output.WriteLine($"Order: {order.Code}");
                _output.WriteLine($"  Category: {order.PerfCategory.Name}");
                _output.WriteLine($"  Warehouse: {order.PerfWarehouse.Name}");
            }
        }

        [Fact]
        public void AutoLoad_BatchWhere_AllFKsPopulated()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var orders = ctx.GetTable<PerfOrder>().Where(o => o.Amount > 150);

                int orderCount = orders.Count();
                Assert.True(orderCount > 0);
                foreach (var order in orders)
                {
                    Assert.NotNull(order.PerfCategory);
                    Assert.NotNull(order.PerfWarehouse);
                }

                _output.WriteLine($"Loaded {orderCount} orders — all FK populated");
            }
        }

        // ─── Include() Selective Tests ─────────────────────────────

        [Fact]
        public void Include_OnlyCategory_WarehouseIsNull()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var order = ctx.GetTable<PerfOrder>()
                    .Include(o => o.PerfCategory)
                    .FirstOrDefault(o => o.Id == 1);

                Assert.NotNull(order);
                Assert.NotNull(order.PerfCategory);
                Assert.Null(order.PerfWarehouse); // NOT included → null

                _output.WriteLine($"Category: {order.PerfCategory.Name} ✓");
                _output.WriteLine($"Warehouse: null (not included) ✓");
            }
        }

        [Fact]
        public void Include_BothFKs_AllPopulated()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var orders = ctx.GetTable<PerfOrder>()
                    .Include(o => o.PerfCategory)
                    .Include(o => o.PerfWarehouse)
                    .Where(o => o.Amount > 150);

                Assert.True(orders.Count > 0);
                foreach (var order in orders)
                {
                    Assert.NotNull(order.PerfCategory);
                    Assert.NotNull(order.PerfWarehouse);
                }

                _output.WriteLine($"Loaded {orders.Count} orders with both FKs via Include()");
            }
        }

        // ─── Async Include Tests ──────────────────────────────────

        [Fact]
        public async Task IncludeAsync_FirstOrDefault_Works()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var order = await ctx.GetTable<PerfOrder>()
                    .Include(o => o.PerfCategory)
                    .FirstOrDefaultAsync(o => o.Id == 5);

                Assert.NotNull(order);
                Assert.NotNull(order.PerfCategory);
                Assert.Null(order.PerfWarehouse);
            }
        }

        [Fact]
        public async Task IncludeAsync_Where_Works()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var orders = await ctx.GetTable<PerfOrder>()
                    .Include(o => o.PerfWarehouse)
                    .WhereAsync(o => o.Amount > 180);

                Assert.True(orders.Count > 0);
                foreach (var order in orders)
                {
                    Assert.Null(order.PerfCategory);     // not included
                    Assert.NotNull(order.PerfWarehouse);  // included
                }
            }
        }

        // ─── Performance Benchmarks ───────────────────────────────

        [Fact]
        public void Benchmark_DefaultAutoLoad_100Orders()
        {
            var sw = Stopwatch.StartNew();
            const int iterations = 10;

            for (int i = 0; i < iterations; i++)
            {
                using (var ctx = new SqlContext(_connection))
                {
                    var orders = ctx.GetTable<PerfOrder>().Where(o => o.Amount > 0);
                    Assert.All(orders, o => Assert.NotNull(o.PerfCategory));
                }
            }

            sw.Stop();
            var avg = sw.ElapsedMilliseconds / iterations;
            _output.WriteLine($"Default auto-load {OrderCount} orders × 2 FK: avg {avg}ms per query");
            _output.WriteLine($"  Queries per iteration: 1 (orders) + 2 (batch FK) = 3 total");
            _output.WriteLine($"  L2S equivalent: 1 + {OrderCount}×2 = {1 + OrderCount * 2} queries (N+1)");
        }

        [Fact]
        public void Benchmark_IncludeSelective_100Orders()
        {
            var sw = Stopwatch.StartNew();
            const int iterations = 10;

            for (int i = 0; i < iterations; i++)
            {
                using (var ctx = new SqlContext(_connection))
                {
                    var orders = ctx.GetTable<PerfOrder>()
                        .Include(o => o.PerfCategory)
                        .Where(o => o.Amount > 0);
                    Assert.All(orders, o => Assert.NotNull(o.PerfCategory));
                }
            }

            sw.Stop();
            var avg = sw.ElapsedMilliseconds / iterations;
            _output.WriteLine($"Include(Category) {OrderCount} orders × 1 FK: avg {avg}ms per query");
            _output.WriteLine($"  Queries per iteration: 1 (orders) + 1 (batch Category) = 2 total");
            _output.WriteLine($"  L2S equivalent: 1 + {OrderCount}×1 = {1 + OrderCount} queries (N+1)");
        }

        [Fact]
        public void Benchmark_NoFK_100Orders()
        {
            MappingCache.Clear();
            var sw = Stopwatch.StartNew();
            const int iterations = 10;

            for (int i = 0; i < iterations; i++)
            {
                using (var ctx = new SqlContext(_connection))
                {
                    // Use DataLoadOptions with empty rules to disable auto-load
                    ctx.LoadOptions = new DataLoadOptions();
                    var orders = ctx.GetTable<PerfOrder>().Where(o => o.Amount > 0);
                    int cnt = orders.Count();
                    Assert.Equal(OrderCount, cnt);
                }
            }

            sw.Stop();
            var avg = sw.ElapsedMilliseconds / iterations;
            _output.WriteLine($"No FK load {OrderCount} orders: avg {avg}ms per query");
            _output.WriteLine($"  Queries per iteration: 1 total");
        }

        [Fact]
        public async Task Benchmark_Async_100Orders()
        {
            var sw = Stopwatch.StartNew();
            const int iterations = 10;

            for (int i = 0; i < iterations; i++)
            {
                using (var ctx = new SqlContext(_connection))
                {
                    var orders = await ctx.GetTable<PerfOrder>()
                        .Include(o => o.PerfCategory)
                        .WhereAsync(o => o.Amount > 0);
                    Assert.All(orders, o => Assert.NotNull(o.PerfCategory));
                }
            }

            sw.Stop();
            var avg = sw.ElapsedMilliseconds / iterations;
            _output.WriteLine($"Async Include {OrderCount} orders × 1 FK: avg {avg}ms per query");
        }

        // ─── DataLoadOptions Tests ──────────────────────────────────

        [Fact]
        public void DataLoadOptions_Selective_OnlyLoadsRegistered()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var opts = new DataLoadOptions();
                opts.LoadWith<PerfOrder>(o => o.PerfWarehouse);
                ctx.LoadOptions = opts;

                var order = ctx.GetTable<PerfOrder>().FirstOrDefault(o => o.Id == 1);

                Assert.NotNull(order);
                Assert.Null(order.PerfCategory);      // not registered → null
                Assert.NotNull(order.PerfWarehouse);   // registered → loaded

                _output.WriteLine($"LoadWith(Warehouse): {order.PerfWarehouse.Name} ✓");
                _output.WriteLine($"Category: null (not registered) ✓");
            }
        }

        [Fact]
        public void EmptyLoadOptions_NoFKLoaded()
        {
            using (var ctx = new SqlContext(_connection))
            {
                ctx.LoadOptions = new DataLoadOptions(); // empty = load nothing
                var order = ctx.GetTable<PerfOrder>().FirstOrDefault(o => o.Id == 1);

                Assert.NotNull(order);
                Assert.Null(order.PerfCategory);
                Assert.Null(order.PerfWarehouse);

                _output.WriteLine("Empty LoadOptions: no FK loaded ✓");
            }
        }

        public void Dispose()
        {
            MappingCache.Clear();
            _connection?.Dispose();
        }
    }
}
