using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using ZeroData.Sql.Mapping;

namespace ZeroData.Sql.Tests
{
    [Table(Name = "BatchUpdateProducts")]
    public class BatchUpdateProduct
    {
        [Column(Name = "Id", IsPrimaryKey = true)]
        public int Id { get; set; }

        [Column(Name = "Title")]
        public string Title { get; set; }

        [Column(Name = "Price")]
        public decimal Price { get; set; }

        [Column(Name = "Status")]
        public string Status { get; set; }

        [Column(Name = "Stock")]
        public int Stock { get; set; }
    }

    public class BatchUpdateTests
    {
        private SqliteConnection CreateDatabase()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE BatchUpdateProducts (
                        Id INTEGER PRIMARY KEY,
                        Title TEXT NOT NULL,
                        Price NUMERIC NOT NULL,
                        Status TEXT NOT NULL,
                        Stock INTEGER NOT NULL
                    );
                ";
                cmd.ExecuteNonQuery();
            }

            return conn;
        }

        private void SeedProducts(SqlContext db)
        {
            var items = new List<BatchUpdateProduct>
            {
                new BatchUpdateProduct { Id = 1, Title = "Laptop", Price = 1000m, Status = "InStock", Stock = 10 },
                new BatchUpdateProduct { Id = 2, Title = "Mouse", Price = 25m, Status = "InStock", Stock = 50 },
                new BatchUpdateProduct { Id = 3, Title = "Keyboard", Price = 75m, Status = "OutOfStock", Stock = 0 },
                new BatchUpdateProduct { Id = 4, Title = "Monitor", Price = 300m, Status = "InStock", Stock = 5 },
                new BatchUpdateProduct { Id = 5, Title = "Cable", Price = 15m, Status = "OutOfStock", Stock = 0 }
            };

            db.BulkInsert(items);
        }

        [Fact]
        public void UpdateWhere_AnonymousObject_UpdatesMatchingRows()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            SeedProducts(db);

            // Set all OutOfStock products to Status = 'Discontinued', Price = 0
            var affected = db.UpdateWhere<BatchUpdateProduct>(
                x => x.Status == "OutOfStock",
                new { Status = "Discontinued", Price = 0m });

            Assert.Equal(2, affected);

            var discontinued = db.GetTable<BatchUpdateProduct>().Where(x => x.Status == "Discontinued");
            Assert.Equal(2, discontinued.Count);
            Assert.All(discontinued, p => Assert.Equal(0m, p.Price));

            var inStock = db.GetTable<BatchUpdateProduct>().Where(x => x.Status == "InStock");
            Assert.Equal(3, inStock.Count);
        }

        [Fact]
        public async Task UpdateWhereAsync_AnonymousObject_UpdatesMatchingRows()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            SeedProducts(db);

            var table = db.GetTable<BatchUpdateProduct>();
            var affected = await table.UpdateWhereAsync(
                x => x.Price > 200m,
                new { Status = "Premium" });

            Assert.Equal(2, affected); // Laptop (1000), Monitor (300)

            var premium = table.Where(x => x.Status == "Premium");
            Assert.Equal(2, premium.Count);
            Assert.Contains(premium, p => p.Title == "Laptop");
            Assert.Contains(premium, p => p.Title == "Monitor");
        }

        [Fact]
        public async Task UpdateWhereAsync_MemberInitExpression_UpdatesDirectly()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            SeedProducts(db);

            var targetStatus = "Sale";
            var discountPrice = 20m;

            var affected = await db.UpdateWhereAsync<BatchUpdateProduct>(
                x => x.Id == 2,
                x => new BatchUpdateProduct { Status = targetStatus, Price = discountPrice });

            Assert.Equal(1, affected);

            var mouse = db.GetTable<BatchUpdateProduct>().Find(2);
            Assert.NotNull(mouse);
            Assert.Equal("Sale", mouse.Status);
            Assert.Equal(20m, mouse.Price);
        }

        [Fact]
        public void UpdateWhere_Dictionary_UpdatesMatchingRows()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            SeedProducts(db);

            var values = new Dictionary<string, object>
            {
                ["Title"] = "Mechanical Keyboard",
                ["Stock"] = 15
            };

            var affected = db.UpdateWhere<BatchUpdateProduct>(x => x.Id == 3, values);
            Assert.Equal(1, affected);

            var item = db.GetTable<BatchUpdateProduct>().Find(3);
            Assert.Equal("Mechanical Keyboard", item.Title);
            Assert.Equal(15, item.Stock);
        }

        [Fact]
        public void UpdateWhere_DetachesTrackedEntitiesToPreventStaleState()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            SeedProducts(db);

            // Load entity into change tracker
            var product = db.GetTable<BatchUpdateProduct>().Find(1);
            Assert.Equal(1000m, product.Price);

            // Server-side direct update
            db.UpdateWhere<BatchUpdateProduct>(x => x.Id == 1, new { Price = 888m });

            // Re-fetch should get the fresh DB value
            var refreshed = db.GetTable<BatchUpdateProduct>().Find(1);
            Assert.Equal(888m, refreshed.Price);
        }
    }
}
