using ZeroData.Sql;
using ZeroData.Sql.Mapping;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ZeroData.Sql.Tests
{
    /// <summary>
    /// GroupBy tests for SQL Server - the primary target database.
    /// SQL Server returns Int32 for INT columns, so anonymous types work perfectly.
    /// </summary>
    [Collection("SqlServer")]
    public class SqlServerGroupByTests : IDisposable
    {
        private readonly SqlConnection _connection;
        private readonly SqlContext _context;
        private const string ConnectionString = "Server=.\\SQLEXPRESS;Database=LiteSqlTest;User Id=testing;Password=268479#Kzx;TrustServerCertificate=true";

        public SqlServerGroupByTests()
        {
            // Create database if not exists
            using (var masterConn = new SqlConnection("Server=.\\SQLEXPRESS;Database=master;User Id=testing;Password=268479#Kzx;TrustServerCertificate=true"))
            {
                masterConn.Open();
                masterConn.Execute(@"
                    IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'LiteSqlTest')
                    BEGIN
                        CREATE DATABASE LiteSqlTest
                    END
                ");
            }

            // Connect to test database
            _connection = new SqlConnection(ConnectionString);
            _connection.Open();

            // Create test table
            _connection.Execute(@"
                IF OBJECT_ID('Orders', 'U') IS NOT NULL DROP TABLE Orders;
                CREATE TABLE Orders (
                    Id INT PRIMARY KEY IDENTITY,
                    CustomerId INT NOT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    Status NVARCHAR(50) NOT NULL,
                    Year INT NOT NULL
                );
            ");

            // Seed test data
            _connection.Execute(@"
                INSERT INTO Orders (CustomerId, Amount, Status, Year) VALUES
                (1, 100.0, 'Completed', 2026),
                (1, 200.0, 'Completed', 2026),
                (1, 150.0, 'Pending', 2026),
                (2, 300.0, 'Completed', 2026),
                (2, 50.0, 'Completed', 2026),
                (2, 75.0, 'Cancelled', 2026),
                (3, 500.0, 'Completed', 2025),
                (3, 250.0, 'Completed', 2025),
                (4, 100.0, 'Completed', 2026),
                (5, 1000.0, 'Completed', 2026)
            ");

            _context = new SqlContext(_connection);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _connection?.Dispose();
        }

        #region Test Entity

        [Table(Name = "Orders")]
        public class Order
        {
            [Column(Name = "Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column(Name = "CustomerId")]
            public int CustomerId { get; set; }

            [Column(Name = "Amount")]
            public decimal Amount { get; set; }

            [Column(Name = "Status")]
            public string Status { get; set; }

            [Column(Name = "Year")]
            public int Year { get; set; }
        }

        #endregion

        #region Basic GroupBy with Count

        [Fact]
        public void GroupBy_SingleColumn_Count()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act - Anonymous types work perfectly on SQL Server!
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Count = g.Count()  // No cast needed!
                })
                .ToList();

            // Assert
            Assert.Equal(5, result.Count);
            Assert.Contains(result, r => r.CustomerId == 1 && r.Count == 3);
            Assert.Contains(result, r => r.CustomerId == 2 && r.Count == 3);
            Assert.Contains(result, r => r.CustomerId == 3 && r.Count == 2);
            Assert.Contains(result, r => r.CustomerId == 4 && r.Count == 1);
            Assert.Contains(result, r => r.CustomerId == 5 && r.Count == 1);
        }

        [Fact]
        public async Task GroupBy_SingleColumn_Count_Async()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var result = await orders
                .GroupBy(o => o.CustomerId)
                .SelectAsync(g => new
                {
                    CustomerId = g.Key,
                    Count = g.Count()
                });

            // Assert
            Assert.Equal(5, result.Count);
            Assert.Contains(result, r => r.CustomerId == 1 && r.Count == 3);
        }

        #endregion

        #region Aggregate Functions

        [Fact]
        public void GroupBy_Sum()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    TotalAmount = g.Sum(x => x.Amount)
                })
                .ToList();

            // Assert
            Assert.Equal(5, result.Count);
            Assert.Contains(result, r => r.CustomerId == 1 && r.TotalAmount == 450.0m);
            Assert.Contains(result, r => r.CustomerId == 2 && r.TotalAmount == 425.0m);
            Assert.Contains(result, r => r.CustomerId == 3 && r.TotalAmount == 750.0m);
        }

        [Fact]
        public void GroupBy_Average()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    AvgAmount = g.Average(x => x.Amount)
                })
                .ToList();

            // Assert
            Assert.Equal(5, result.Count);
            Assert.Contains(result, r => r.CustomerId == 1 && r.AvgAmount == 150.0m);
            Assert.Contains(result, r => r.CustomerId == 3 && r.AvgAmount == 375.0m);
        }

        [Fact]
        public void GroupBy_MinMax()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    MinAmount = g.Min(x => x.Amount),
                    MaxAmount = g.Max(x => x.Amount)
                })
                .ToList();

            // Assert
            Assert.Equal(5, result.Count);
            var customer1 = result.First(r => r.CustomerId == 1);
            Assert.Equal(100.0m, customer1.MinAmount);
            Assert.Equal(200.0m, customer1.MaxAmount);
        }

        [Fact]
        public void GroupBy_MultipleAggregates()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act - Anonymous types work perfectly!
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    OrderCount = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount),
                    AvgAmount = g.Average(x => x.Amount),
                    MinAmount = g.Min(x => x.Amount),
                    MaxAmount = g.Max(x => x.Amount)
                })
                .ToList();

            // Assert
            Assert.Equal(5, result.Count);
            var customer1 = result.First(r => r.CustomerId == 1);
            Assert.Equal(3, customer1.OrderCount);
            Assert.Equal(450.0m, customer1.TotalAmount);
            Assert.Equal(150.0m, customer1.AvgAmount);
            Assert.Equal(100.0m, customer1.MinAmount);
            Assert.Equal(200.0m, customer1.MaxAmount);
        }

        #endregion

        #region Multi-Column GroupBy

        [Fact]
        public void GroupBy_MultiColumn()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var result = orders
                .GroupBy(o => new { o.CustomerId, o.Year })
                .Select(g => new
                {
                    CustomerId = g.Key.CustomerId,
                    Year = g.Key.Year,
                    Count = g.Count()
                })
                .ToList();

            // Assert
            Assert.Equal(5, result.Count); // 5 unique (CustomerId, Year) combinations
            Assert.Contains(result, r => r.CustomerId == 1 && r.Year == 2026 && r.Count == 3);
            Assert.Contains(result, r => r.CustomerId == 3 && r.Year == 2025 && r.Count == 2);
        }

        [Fact]
        public void GroupBy_MultiColumn_WithAggregates()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var result = orders
                .GroupBy(o => new { o.Status, o.Year })
                .Select(g => new
                {
                    Status = g.Key.Status,
                    Year = g.Key.Year,
                    Count = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount)
                })
                .ToList();

            // Assert
            Assert.True(result.Count >= 3);
            var completed2026 = result.FirstOrDefault(r => r.Status == "Completed" && r.Year == 2026);
            Assert.NotNull(completed2026);
            Assert.Equal(6, completed2026.Count);
        }

        #endregion

        #region HAVING Clause

        [Fact]
        public void GroupBy_WithHaving_Count()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Where(g => g.Count() > 1)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Count = g.Count()
                })
                .ToList();

            // Assert
            Assert.Equal(3, result.Count);
            Assert.DoesNotContain(result, r => r.CustomerId == 4);
            Assert.DoesNotContain(result, r => r.CustomerId == 5);
        }

        [Fact]
        public void GroupBy_WithHaving_Sum()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Where(g => g.Sum(x => x.Amount) > 400)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    TotalAmount = g.Sum(x => x.Amount)
                })
                .ToList();

            // Assert
            // Customers with total > 400: 1 (450), 2 (425), 3 (750), 5 (1000)
            Assert.Equal(4, result.Count);
            Assert.Contains(result, r => r.CustomerId == 1);
            Assert.Contains(result, r => r.CustomerId == 2);
            Assert.Contains(result, r => r.CustomerId == 3);
            Assert.Contains(result, r => r.CustomerId == 5);
        }

        [Fact]
        public void GroupBy_WithHaving_ComplexPredicate()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Where(g => g.Count() > 1 && g.Sum(x => x.Amount) > 400)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount)
                })
                .ToList();

            // Assert
            Assert.Equal(3, result.Count);
            Assert.All(result, r => Assert.True(r.Count > 1 && r.TotalAmount > 400));
        }

        #endregion

        #region ORDER BY

        [Fact]
        public void GroupBy_OrderBy_Key()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var result = orders
                .GroupBy(o => o.CustomerId)
                .OrderBy(x => x.Key)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Count = g.Count()
                })
                .ToList();

            // Assert
            Assert.Equal(5, result.Count);
            Assert.Equal(1, result[0].CustomerId);
            Assert.Equal(2, result[1].CustomerId);
            Assert.Equal(3, result[2].CustomerId);
        }

        [Fact]
        public void GroupBy_OrderByDescending_Aggregate()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    TotalAmount = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.TotalAmount)
                .ToList();

            // Assert
            Assert.Equal(5, result.Count);
            Assert.Equal(5, result[0].CustomerId); // Customer 5 has highest total (1000)
        }

        #endregion

        #region Complete Queries

        [Fact]
        public void GroupBy_CompleteQuery_WithWhereAndHaving()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var result = orders
                .Where(o => o.Status == "Completed")
                .GroupBy(o => o.CustomerId)
                .Where(g => g.Count() > 1)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    OrderCount = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount),
                    AvgAmount = g.Average(x => x.Amount)
                })
                .ToList();

            // Assert
            // Completed orders: Customer 1 (2), Customer 2 (2), Customer 3 (2), Customer 4 (1), Customer 5 (1)
            // With Count > 1: Customer 1, 2, 3
            Assert.Equal(3, result.Count);
            var customer1 = result.FirstOrDefault(r => r.CustomerId == 1);
            Assert.NotNull(customer1);
            Assert.Equal(2, customer1.OrderCount);
            Assert.Equal(300.0m, customer1.TotalAmount);
            Assert.Equal(150.0m, customer1.AvgAmount);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void GroupBy_EmptyResult()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Where(g => g.Sum(x => x.Amount) > 10000)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    TotalAmount = g.Sum(x => x.Amount)
                })
                .ToList();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GroupBy_SingleGroup()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var result = orders
                .GroupBy(o => o.Year)
                .Where(g => g.Key == 2025)
                .Select(g => new
                {
                    Year = g.Key,
                    Count = g.Count()
                })
                .ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal(2025, result[0].Year);
            Assert.Equal(2, result[0].Count);
        }

        #endregion
    }
}
