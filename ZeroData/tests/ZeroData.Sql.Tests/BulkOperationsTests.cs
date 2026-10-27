using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ZeroData.Sql;
using ZeroData.Sql.Mapping;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ZeroData.Sql.Tests
{
    public class BulkOperationsTests : IDisposable
    {
        private readonly IDbConnection _connection;

        public BulkOperationsTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            // Create test table
            _connection.Execute(@"
                CREATE TABLE Products (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Price REAL NOT NULL,
                    Stock INTEGER NOT NULL
                )");
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }

        [Fact]
        public void BulkInsert_MultipleEntities_InsertsAll()
        {
            // Arrange
            var products = new List<Product>
            {
                new Product { Name = "Product 1", Price = 10.5m, Stock = 100 },
                new Product { Name = "Product 2", Price = 20.0m, Stock = 50 },
                new Product { Name = "Product 3", Price = 15.75m, Stock = 75 }
            };

            // Act
            var rowsAffected = BulkOperations.BulkInsert(_connection, products);

            // Assert
            Assert.Equal(3, rowsAffected);

            var count = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Products");
            Assert.Equal(3, count);

            var inserted = _connection.Query<Product>("SELECT * FROM Products ORDER BY Name").ToList();
            Assert.Equal(3, inserted.Count);
            Assert.Equal("Product 1", inserted[0].Name);
            Assert.Equal(10.5m, inserted[0].Price);
            Assert.Equal(100, inserted[0].Stock);
        }

        [Fact]
        public void BulkInsert_EmptyList_ReturnsZero()
        {
            // Arrange
            var products = new List<Product>();

            // Act
            var rowsAffected = BulkOperations.BulkInsert(_connection, products);

            // Assert
            Assert.Equal(0, rowsAffected);
        }

        [Fact]
        public void BulkInsert_NullEntities_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                BulkOperations.BulkInsert<Product>(_connection, null));
        }

        [Fact]
        public void BulkInsert_LargeDataset_PerformsBetter()
        {
            // Arrange - Create 1000 products
            var products = Enumerable.Range(1, 1000)
                .Select(i => new Product
                {
                    Name = $"Product {i}",
                    Price = i * 1.5m,
                    Stock = i * 10
                })
                .ToList();

            // Act
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var rowsAffected = BulkOperations.BulkInsert(_connection, products);
            sw.Stop();

            // Assert
            Assert.Equal(1000, rowsAffected);
            Assert.True(sw.ElapsedMilliseconds < 1000,
                $"BulkInsert took {sw.ElapsedMilliseconds}ms for 1000 rows (should be < 1000ms)");

            var count = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Products");
            Assert.Equal(1000, count);
        }

        [Fact]
        public void BulkUpdate_MultipleEntities_UpdatesAll()
        {
            // Arrange - Insert test data
            _connection.Execute(@"
                INSERT INTO Products (Name, Price, Stock) VALUES
                ('Product 1', 10.0, 100),
                ('Product 2', 20.0, 200),
                ('Product 3', 30.0, 300)");

            var products = _connection.Query<Product>("SELECT * FROM Products").ToList();

            // Modify prices
            foreach (var p in products)
            {
                p.Price += 5.0m;
                p.Stock += 10;
            }

            // Act
            var rowsAffected = BulkOperations.BulkUpdate(_connection, products);

            // Assert
            Assert.Equal(3, rowsAffected);

            var updated = _connection.Query<Product>("SELECT * FROM Products ORDER BY Id").ToList();
            Assert.Equal(15.0m, updated[0].Price);
            Assert.Equal(110, updated[0].Stock);
            Assert.Equal(25.0m, updated[1].Price);
            Assert.Equal(210, updated[1].Stock);
        }

        [Fact]
        public void BulkUpdate_EmptyList_ReturnsZero()
        {
            // Arrange
            var products = new List<Product>();

            // Act
            var rowsAffected = BulkOperations.BulkUpdate(_connection, products);

            // Assert
            Assert.Equal(0, rowsAffected);
        }

        [Fact]
        public void BulkDelete_MultipleEntities_DeletesAll()
        {
            // Arrange - Insert test data
            _connection.Execute(@"
                INSERT INTO Products (Name, Price, Stock) VALUES
                ('Product 1', 10.0, 100),
                ('Product 2', 20.0, 200),
                ('Product 3', 30.0, 300),
                ('Product 4', 40.0, 400)");

            var products = _connection.Query<Product>("SELECT * FROM Products WHERE Id IN (1, 3)").ToList();

            // Act
            var rowsAffected = BulkOperations.BulkDelete(_connection, products);

            // Assert
            Assert.Equal(2, rowsAffected);

            var remaining = _connection.Query<Product>("SELECT * FROM Products ORDER BY Id").ToList();
            Assert.Equal(2, remaining.Count);
            Assert.Equal("Product 2", remaining[0].Name);
            Assert.Equal("Product 4", remaining[1].Name);
        }

        [Fact]
        public void BulkDelete_EmptyList_ReturnsZero()
        {
            // Arrange
            var products = new List<Product>();

            // Act
            var rowsAffected = BulkOperations.BulkDelete(_connection, products);

            // Assert
            Assert.Equal(0, rowsAffected);
        }

        [Fact]
        public void BulkInsert_WithTransaction_CommitsSuccessfully()
        {
            // Arrange
            var products = new List<Product>
            {
                new Product { Name = "Product 1", Price = 10.0m, Stock = 100 },
                new Product { Name = "Product 2", Price = 20.0m, Stock = 200 }
            };

            // Act
            using (var transaction = _connection.BeginTransaction())
            {
                BulkOperations.BulkInsert(_connection, products, transaction);
                transaction.Commit();
            }

            // Assert
            var count = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Products");
            Assert.Equal(2, count);
        }

        [Fact]
        public void BulkInsert_WithTransactionRollback_DoesNotInsert()
        {
            // Arrange
            var products = new List<Product>
            {
                new Product { Name = "Product 1", Price = 10.0m, Stock = 100 },
                new Product { Name = "Product 2", Price = 20.0m, Stock = 200 }
            };

            // Act
            using (var transaction = _connection.BeginTransaction())
            {
                BulkOperations.BulkInsert(_connection, products, transaction);
                transaction.Rollback();
            }

            // Assert
            var count = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Products");
            Assert.Equal(0, count);
        }

        [Table(Name = "Products")]
        private class Product
        {
            [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
            public int Id { get; set; }

            [Column(Name = "Name")]
            public string Name { get; set; }

            [Column(Name = "Price")]
            public decimal Price { get; set; }

            [Column(Name = "Stock")]
            public int Stock { get; set; }
        }
    }
}
