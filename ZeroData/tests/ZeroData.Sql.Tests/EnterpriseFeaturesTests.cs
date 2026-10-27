using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using ZeroData.Sql.Dialects;
using ZeroData.Sql.Mapping;
using ZeroData.Sql.Sql;

namespace ZeroData.Sql.Tests
{
    [Table(Name = "EnterpriseProducts")]
    public class EnterpriseProduct
    {
        [Column(Name = "Id", IsPrimaryKey = true)]
        public int Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; }

        [Column(Name = "Price")]
        public decimal Price { get; set; }

        [Column(Name = "UpdatedAt")]
        public DateTime UpdatedAt { get; set; }
    }

    public class EnterpriseFeaturesTests
    {
        private SqliteConnection CreateDatabase()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE EnterpriseProducts (
                        Id INTEGER PRIMARY KEY,
                        Name TEXT NOT NULL,
                        Price NUMERIC NOT NULL,
                        UpdatedAt TEXT NOT NULL
                    );
                ";
                cmd.ExecuteNonQuery();
            }

            return conn;
        }

        #region QueryInterceptor & QueryProfiler Tests

        [Fact]
        public void Interceptor_And_Profiler_CaptureExecutionMetrics()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);

            db.Profiler.Enabled = true;

            string interceptedBefore = null;
            string interceptedAfter = null;
            TimeSpan duration = TimeSpan.Zero;
            int affected = -1;

            db.Interceptor = new QueryInterceptor
            {
                OnBeforeExecute = (sql, param) =>
                {
                    interceptedBefore = sql;
                    return sql;
                },
                OnAfterExecute = (sql, d, count) =>
                {
                    interceptedAfter = sql;
                    duration = d;
                    affected = count;
                }
            };

            var sw = db.Profiler.StartQuery();
            db.ExecuteCommand("INSERT INTO EnterpriseProducts (Id, Name, Price, UpdatedAt) VALUES (1, 'Test', 10.0, '2024-01-01');");
            db.Profiler.StopQuery(sw, "INSERT");

            Assert.NotNull(interceptedBefore);
            Assert.NotNull(interceptedAfter);
            Assert.True(duration >= TimeSpan.Zero);
            Assert.Equal(1, affected);
            Assert.Equal(1, db.Profiler.TotalQueries);
        }

        [Fact]
        public void Interceptor_OnError_CapturesExecutionFailure()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);

            string failedSql = null;
            Exception caughtEx = null;

            db.Interceptor = new QueryInterceptor
            {
                OnError = (sql, ex) =>
                {
                    failedSql = sql;
                    caughtEx = ex;
                }
            };

            Assert.ThrowsAny<Exception>(() =>
            {
                db.ExecuteCommand("SELECT * FROM NonExistentTableXYZ;");
            });

            Assert.NotNull(failedSql);
            Assert.NotNull(caughtEx);
        }

        #endregion

        #region Entity Lifecycle: GetOriginalValues & Reload Tests

        [Fact]
        public void EntityLifecycle_GetOriginalValues_And_Reload()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);

            db.Insert(new EnterpriseProduct { Id = 1, Name = "OriginalName", Price = 100m, UpdatedAt = new DateTime(2024, 1, 1) });
            db.SubmitChanges();

            // Fetch entity (so change tracker captures snapshot)
            var product = db.GetTable<EnterpriseProduct>().Find(1);
            Assert.NotNull(product);

            var origValues = db.GetOriginalValues(product);
            Assert.Equal("OriginalName", origValues["Name"]);
            Assert.Equal(100m, origValues["Price"]);

            // Modify in memory
            product.Name = "DirtyModifiedName";
            product.Price = 999m;

            // Reload from DB — should discard dirty changes
            db.Reload(product);

            Assert.Equal("OriginalName", product.Name);
            Assert.Equal(100m, product.Price);

            // Verified original snapshot is also synchronized
            var reloadedOrig = db.GetOriginalValues(product);
            Assert.Equal("OriginalName", reloadedOrig["Name"]);
        }

        [Fact]
        public async Task EntityLifecycle_ReloadAsync_DiscardsDirtyChanges()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);

            db.Insert(new EnterpriseProduct { Id = 2, Name = "AsyncProduct", Price = 50m, UpdatedAt = new DateTime(2024, 2, 2) });
            await db.SubmitChangesAsync();

            var product = await db.GetTable<EnterpriseProduct>().FindAsync(2);
            product.Name = "DirtyAsync";

            await db.ReloadAsync(product);

            Assert.Equal("AsyncProduct", product.Name);
        }

        #endregion

        #region BulkMerge (Upsert) Tests

        [Fact]
        public void Dialects_GenerateBulkMergeSql_GeneratesExpectedSyntax()
        {
            var columns = new[] { "Id", "Name", "Price" };
            var pks = new[] { "Id" };
            var rows = new List<IReadOnlyList<string>>
            {
                new List<string> { "@p0_Id", "@p0_Name", "@p0_Price" }
            };

            // SQLite
            var sqlite = new SqliteDialect();
            var sqliteSql = sqlite.GenerateBulkMergeSql("Products", columns, pks, rows);
            Assert.Contains("ON CONFLICT (Id) DO UPDATE SET", sqliteSql);
            Assert.Contains("excluded.Name", sqliteSql);

            // PostgreSQL
            var postgres = new PostgreSqlDialect();
            var pgSql = postgres.GenerateBulkMergeSql("\"Products\"", columns, pks, rows);
            Assert.Contains("ON CONFLICT (Id) DO UPDATE SET", pgSql);
            Assert.Contains("EXCLUDED.Name", pgSql);

            // MySQL
            var mysql = new MySqlDialect();
            var mysqlSql = mysql.GenerateBulkMergeSql("`Products`", columns, pks, rows);
            Assert.Contains("ON DUPLICATE KEY UPDATE", mysqlSql);
            Assert.Contains("VALUES(Name)", mysqlSql);

            // SQL Server
            var sqlServer = new SqlServerDialect();
            var ssSql = sqlServer.GenerateBulkMergeSql("[Products]", columns, pks, rows);
            Assert.Contains("MERGE INTO [Products]", ssSql);
            Assert.Contains("WHEN MATCHED THEN UPDATE SET", ssSql);
            Assert.Contains("WHEN NOT MATCHED THEN INSERT", ssSql);
        }

        [Fact]
        public void SqlContext_BulkMerge_InsertsNewAndUpdatesExistingRecords()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);

            // Seed initial records
            db.Insert(new EnterpriseProduct { Id = 1, Name = "Laptop", Price = 1000m, UpdatedAt = new DateTime(2024, 1, 1) });
            db.Insert(new EnterpriseProduct { Id = 2, Name = "Mouse", Price = 20m, UpdatedAt = new DateTime(2024, 1, 1) });
            db.SubmitChanges();

            // Prepare merge list: Id=2 updated (Price=25), Id=3 new record
            var mergeList = new List<EnterpriseProduct>
            {
                new EnterpriseProduct { Id = 2, Name = "Mouse RGB", Price = 25m, UpdatedAt = new DateTime(2024, 5, 1) },
                new EnterpriseProduct { Id = 3, Name = "Keyboard", Price = 80m, UpdatedAt = new DateTime(2024, 5, 1) }
            };

            int affected = db.BulkMerge(mergeList);

            var items = db.GetTable<EnterpriseProduct>().OrderBy(x => x.Id).ToList();

            Assert.Equal(3, items.Count);

            // Item 1 untouched
            Assert.Equal("Laptop", items[0].Name);
            Assert.Equal(1000m, items[0].Price);

            // Item 2 updated
            Assert.Equal("Mouse RGB", items[1].Name);
            Assert.Equal(25m, items[1].Price);

            // Item 3 inserted
            Assert.Equal("Keyboard", items[2].Name);
            Assert.Equal(80m, items[2].Price);
        }

        [Fact]
        public async Task SqlContext_BulkMergeAsync_InsertsNewAndUpdatesExistingRecords()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);

            db.Insert(new EnterpriseProduct { Id = 10, Name = "Initial", Price = 10m, UpdatedAt = new DateTime(2024, 1, 1) });
            await db.SubmitChangesAsync();

            var mergeList = new List<EnterpriseProduct>
            {
                new EnterpriseProduct { Id = 10, Name = "UpdatedInitial", Price = 15m, UpdatedAt = new DateTime(2024, 6, 1) },
                new EnterpriseProduct { Id = 20, Name = "BrandNew", Price = 200m, UpdatedAt = new DateTime(2024, 6, 1) }
            };

            await db.BulkMergeAsync(mergeList);

            var items = await db.GetTable<EnterpriseProduct>().OrderBy(x => x.Id).ToListAsync();
            Assert.Equal(2, items.Count);
            Assert.Equal("UpdatedInitial", items[0].Name);
            Assert.Equal(15m, items[0].Price);
            Assert.Equal("BrandNew", items[1].Name);
            Assert.Equal(200m, items[1].Price);
        }

        #endregion

        #region LINQ DateTime Hour, Minute, Second Tests

        [Fact]
        public void WhereBuilder_DateTime_HourMinuteSecond_GeneratesDialectSql()
        {
            var mapping = MappingCache.GetMapping<EnterpriseProduct>();

            // SQL Server
            var ssBuilder = new WhereBuilder(mapping, new SqlServerDialect());
            var (ssHour, _) = ssBuilder.Build<EnterpriseProduct>(p => p.UpdatedAt.Hour >= 8);
            Assert.Equal("DATEPART(hour, [UpdatedAt]) >= @w0", ssHour);
            var (ssMin, _) = ssBuilder.Build<EnterpriseProduct>(p => p.UpdatedAt.Minute == 30);
            Assert.Equal("DATEPART(minute, [UpdatedAt]) = @w0", ssMin);
            var (ssSec, _) = ssBuilder.Build<EnterpriseProduct>(p => p.UpdatedAt.Second == 0);
            Assert.Equal("DATEPART(second, [UpdatedAt]) = @w0", ssSec);

            // SQLite
            var sqliteBuilder = new WhereBuilder(mapping, new SqliteDialect());
            var (sqHour, _) = sqliteBuilder.Build<EnterpriseProduct>(p => p.UpdatedAt.Hour >= 8);
            Assert.Equal("CAST(strftime('%H', \"UpdatedAt\") AS INTEGER) >= @w0", sqHour);
            var (sqMin, _) = sqliteBuilder.Build<EnterpriseProduct>(p => p.UpdatedAt.Minute == 30);
            Assert.Equal("CAST(strftime('%M', \"UpdatedAt\") AS INTEGER) = @w0", sqMin);
            var (sqSec, _) = sqliteBuilder.Build<EnterpriseProduct>(p => p.UpdatedAt.Second == 0);
            Assert.Equal("CAST(strftime('%S', \"UpdatedAt\") AS INTEGER) = @w0", sqSec);

            // PostgreSQL
            var pgBuilder = new WhereBuilder(mapping, new PostgreSqlDialect());
            var (pgHour, _) = pgBuilder.Build<EnterpriseProduct>(p => p.UpdatedAt.Hour == 14);
            Assert.Equal("EXTRACT(HOUR FROM \"UpdatedAt\") = @w0", pgHour);

            // MySQL
            var myBuilder = new WhereBuilder(mapping, new MySqlDialect());
            var (myHour, _) = myBuilder.Build<EnterpriseProduct>(p => p.UpdatedAt.Hour == 14);
            Assert.Equal("HOUR(`UpdatedAt`) = @w0", myHour);
        }

        #endregion
    }
}
