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
    public class JoinTests : IDisposable
    {
        private readonly IDbConnection _connection;
        private readonly SqlContext _db;

        public JoinTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            // Create test tables
            _connection.Execute(@"
                CREATE TABLE Customers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    City TEXT
                )");

            _connection.Execute(@"
                CREATE TABLE Orders (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerId INTEGER NOT NULL,
                    Amount REAL NOT NULL,
                    OrderDate TEXT
                )");

            // Insert test data
            _connection.Execute(@"
                INSERT INTO Customers (Name, City) VALUES
                ('Alice', 'New York'),
                ('Bob', 'London'),
                ('Charlie', 'Paris')");

            _connection.Execute(@"
                INSERT INTO Orders (CustomerId, Amount, OrderDate) VALUES
                (1, 100.0, '2024-01-01'),
                (1, 200.0, '2024-01-02'),
                (2, 150.0, '2024-01-03'),
                (3, 300.0, '2024-01-04')");

            _db = new SqlContext(_connection);
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }

        [Fact]
        public void Join_InnerJoin_WithDTO_ReturnsResults()
        {
            // Arrange & Act
            var results = _db.GetTable<Order>()
                .Join(_db.GetTable<Customer>(),
                    o => o.CustomerId,
                    c => c.Id,
                    (o, c) => new OrderCustomerDto
                    {
                        OrderId = o.Id,
                        Amount = o.Amount,
                        CustomerName = c.Name,
                        City = c.City
                    })
                .ToList();

            // Assert
            Assert.Equal(4, results.Count);
            var firstOrder = results.First(r => r.OrderId == 1);
            Assert.Equal("Alice", firstOrder.CustomerName);
            Assert.Equal("New York", firstOrder.City);
            Assert.Equal(100.0m, firstOrder.Amount);
        }

        [Fact]
        public void Join_MultipleColumns_WithDTO_ReturnsCorrectResults()
        {
            // Arrange & Act
            var results = _db.GetTable<Order>()
                .Join(_db.GetTable<Customer>(),
                    o => o.CustomerId,
                    c => c.Id,
                    (o, c) => new OrderCustomerDetailDto
                    {
                        OrderId = o.Id,
                        OrderAmount = o.Amount,
                        OrderDate = o.OrderDate,
                        CustomerId = c.Id,
                        CustomerName = c.Name,
                        CustomerCity = c.City
                    })
                .ToList();

            // Assert
            Assert.Equal(4, results.Count);
            Assert.All(results, r =>
            {
                Assert.True(r.OrderId > 0);
                Assert.True(r.OrderAmount > 0);
                Assert.NotNull(r.CustomerName);
            });
        }

        [Fact]
        public void Join_ToSql_ReturnsValidSql()
        {
            // Arrange & Act
            var sql = _db.GetTable<Order>()
                .Join(_db.GetTable<Customer>(),
                    o => o.CustomerId,
                    c => c.Id,
                    (o, c) => new OrderCustomerDto { OrderId = o.Id, CustomerName = c.Name, Amount = o.Amount, City = c.City })
                .ToSql();

            // Assert
            Assert.Contains("SELECT", sql);
            Assert.Contains("FROM \"Orders\" AS t1", sql);
            Assert.Contains("INNER JOIN \"Customers\" AS t2", sql);
            Assert.Contains("ON t1.\"CustomerId\" = t2.\"Id\"", sql);
        }

        [Fact]
        public void Join_First_ReturnsFirstResult()
        {
            // Arrange & Act
            var result = _db.GetTable<Order>()
                .Join(_db.GetTable<Customer>(),
                    o => o.CustomerId,
                    c => c.Id,
                    (o, c) => new OrderCustomerDto
                    {
                        OrderId = o.Id,
                        Amount = o.Amount,
                        CustomerName = c.Name,
                        City = c.City
                    })
                .First();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Amount > 0);
            Assert.NotNull(result.CustomerName);
        }

        [Fact]
        public void Join_FirstOrDefault_ReturnsNull_WhenNoResults()
        {
            // Arrange - Delete all orders
            _connection.Execute("DELETE FROM Orders");

            // Act
            var result = _db.GetTable<Order>()
                .Join(_db.GetTable<Customer>(),
                    o => o.CustomerId,
                    c => c.Id,
                    (o, c) => new OrderCustomerDto
                    {
                        OrderId = o.Id,
                        Amount = o.Amount,
                        CustomerName = c.Name,
                        City = c.City
                    })
                .FirstOrDefault();

            // Assert
            Assert.Null(result);
        }

        [Table(Name = "Customers")]
        private class Customer
        {
            [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
            public int Id { get; set; }

            [Column(Name = "Name")]
            public string Name { get; set; }

            [Column(Name = "City")]
            public string City { get; set; }
        }

        [Table(Name = "Orders")]
        private class Order
        {
            [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
            public int Id { get; set; }

            [Column(Name = "CustomerId")]
            public int CustomerId { get; set; }

            [Column(Name = "Amount")]
            public decimal Amount { get; set; }

            [Column(Name = "OrderDate")]
            public string OrderDate { get; set; }
        }

        private class OrderCustomerDto
        {
            public int OrderId { get; set; }
            public decimal Amount { get; set; }
            public string CustomerName { get; set; }
            public string City { get; set; }
        }

        private class OrderCustomerDetailDto
        {
            public int OrderId { get; set; }
            public decimal OrderAmount { get; set; }
            public string OrderDate { get; set; }
            public int CustomerId { get; set; }
            public string CustomerName { get; set; }
            public string CustomerCity { get; set; }
        }
    }
}
