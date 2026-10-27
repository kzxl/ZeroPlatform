using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using ZeroData.Core;
using ZeroData.Sql.Mapping;

namespace ZeroData.Sql.Tests
{
    [Table(Name = "DataFrameSourceItems")]
    public class DataFrameSourceItem
    {
        [Column(Name = "Id", IsPrimaryKey = true)]
        public long Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; }

        [Column(Name = "Price")]
        public double Price { get; set; }

        [Column(Name = "Quantity")]
        public long Quantity { get; set; }

        [Column(Name = "IsActive")]
        public bool IsActive { get; set; }
    }

    public class DataFrameSqlPipelineTests
    {
        private SqliteConnection CreateDatabase()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE DataFrameSourceItems (
                        Id INTEGER PRIMARY KEY,
                        Name TEXT NOT NULL,
                        Price REAL NOT NULL,
                        Quantity INTEGER NOT NULL,
                        IsActive INTEGER NOT NULL
                    );

                    CREATE TABLE DataFrameTargetItems (
                        Id INTEGER PRIMARY KEY,
                        Name TEXT NOT NULL,
                        Price REAL NOT NULL,
                        Quantity INTEGER NOT NULL,
                        IsActive INTEGER NOT NULL
                    );
                ";
                cmd.ExecuteNonQuery();
            }

            return conn;
        }

        private void SeedItems(SqlContext db)
        {
            var items = new List<DataFrameSourceItem>
            {
                new DataFrameSourceItem { Id = 1, Name = "Alpha", Price = 100.5, Quantity = 10, IsActive = true },
                new DataFrameSourceItem { Id = 2, Name = "Beta", Price = 250.0, Quantity = 5, IsActive = false },
                new DataFrameSourceItem { Id = 3, Name = "Gamma", Price = 15.75, Quantity = 100, IsActive = true },
                new DataFrameSourceItem { Id = 4, Name = "Delta", Price = 800.0, Quantity = 2, IsActive = true },
                new DataFrameSourceItem { Id = 5, Name = "Epsilon", Price = 45.0, Quantity = 50, IsActive = false }
            };

            db.BulkInsert(items);
        }

        [Fact]
        public void QueryDataFrame_ReadsDirectlyIntoColumnarVectors()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            SeedItems(db);

            var df = db.QueryDataFrame("SELECT Id, Name, Price, Quantity, IsActive FROM DataFrameSourceItems ORDER BY Id ASC");

            Assert.NotNull(df);
            Assert.Equal(5, df.RowCount);
            Assert.Equal(5, df.ColumnCount);

            // Verify strongly typed columns
            var idCol = df.Column<long>("Id");
            var nameCol = df.Column<string>("Name");
            var priceCol = df.Column<double>("Price");

            Assert.Equal(1L, idCol[0]);
            Assert.Equal(5L, idCol[4]);
            Assert.Equal("Alpha", nameCol[0]);
            Assert.Equal("Epsilon", nameCol[4]);
            Assert.Equal(100.5, priceCol[0]);
            Assert.Equal(45.0, priceCol[4]);
        }

        [Fact]
        public async Task QueryDataFrameAsync_ReadsDirectlyIntoColumnarVectors()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            SeedItems(db);

            var df = await db.QueryDataFrameAsync("SELECT Name, Price FROM DataFrameSourceItems WHERE Price > @minPrice",
                new { minPrice = 100.0 });

            Assert.NotNull(df);
            Assert.Equal(3, df.RowCount); // Alpha (100.5), Beta (250), Delta (800)
            Assert.Equal(2, df.ColumnCount);

            var nameCol = df.Column<string>("Name");
            var names = Enumerable.Range(0, df.RowCount).Select(i => nameCol[i]).ToList();
            Assert.Contains("Alpha", names);
            Assert.Contains("Beta", names);
            Assert.Contains("Delta", names);
        }

        [Fact]
        public void Table_ToDataFrame_BypassesPOCOAllocation()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            SeedItems(db);

            var df = db.GetTable<DataFrameSourceItem>()
                       .AndWhere(x => x.IsActive)
                       .OrderByDescending(x => x.Price)
                       .ToDataFrame();

            Assert.NotNull(df);
            Assert.Equal(3, df.RowCount); // Delta (800), Alpha (100.5), Gamma (15.75)

            var nameCol = df.Column<string>("Name");
            var priceCol = df.Column<double>("Price");

            Assert.Equal("Delta", nameCol[0]);
            Assert.Equal(800.0, priceCol[0]);
            Assert.Equal("Alpha", nameCol[1]);
            Assert.Equal(100.5, priceCol[1]);
            Assert.Equal("Gamma", nameCol[2]);
            Assert.Equal(15.75, priceCol[2]);
        }

        [Fact]
        public async Task Table_ToDataFrameAsync_BypassesPOCOAllocation()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            SeedItems(db);

            var df = await db.GetTable<DataFrameSourceItem>()
                             .AndWhere(x => x.Quantity >= 50)
                             .ToDataFrameAsync();

            Assert.NotNull(df);
            Assert.Equal(2, df.RowCount); // Gamma (100), Epsilon (50)
        }

        [Fact]
        public void BulkInsert_DataFrame_InsertsDirectlyIntoTable()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            SeedItems(db);

            // Read from Source into DataFrame
            var df = db.GetTable<DataFrameSourceItem>().ToDataFrame();
            Assert.Equal(5, df.RowCount);

            // Bulk Insert DataFrame directly into Target table
            var inserted = db.BulkInsert(df, "DataFrameTargetItems");
            Assert.Equal(5, inserted);

            // Verify target table contents
            var count = db.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM DataFrameTargetItems");
            Assert.Equal(5, count);

            var targetDf = db.QueryDataFrame("SELECT Name FROM DataFrameTargetItems ORDER BY Id");
            var targetNames = targetDf.Column<string>("Name");
            Assert.Equal("Alpha", targetNames[0]);
            Assert.Equal("Epsilon", targetNames[4]);
        }

        [Fact]
        public async Task BulkInsertAsync_DataFrame_InsertsDirectlyIntoTable()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            SeedItems(db);

            // Query only active items into DataFrame
            var df = await db.GetTable<DataFrameSourceItem>()
                             .AndWhere(x => x.IsActive)
                             .ToDataFrameAsync();

            Assert.Equal(3, df.RowCount);

            // Bulk insert async
            var inserted = await db.BulkInsertAsync(df, "DataFrameTargetItems");
            Assert.Equal(3, inserted);

            var count = await db.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DataFrameTargetItems");
            Assert.Equal(3, count);
        }
    }
}
