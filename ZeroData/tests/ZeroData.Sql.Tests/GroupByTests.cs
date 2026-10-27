using ZeroData.Sql;
using ZeroData.Sql.Mapping;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ZeroData.Sql.Tests
{
    /// <summary>
    /// Tests for Phase 28: GroupBy Support
    /// Covers single/multi-column grouping, aggregates, HAVING, ORDER BY
    /// </summary>
    public class GroupByTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly SqlContext _context;

        public GroupByTests()
        {
            // Setup in-memory SQLite database
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            // Create test tables
            _connection.Execute(@"
                CREATE TABLE Orders (
                    Id INTEGER PRIMARY KEY,
                    CustomerId INTEGER NOT NULL,
                    Amount REAL NOT NULL,
                    Status TEXT NOT NULL,
                    Year INTEGER NOT NULL
                )
            ");

            // Seed test data
            _connection.Execute(@"
                INSERT INTO Orders (Id, CustomerId, Amount, Status, Year) VALUES
                (1, 1, 100.0, 'Completed', 2026),
                (2, 1, 200.0, 'Completed', 2026),
                (3, 1, 150.0, 'Pending', 2026),
                (4, 2, 300.0, 'Completed', 2026),
                (5, 2, 50.0, 'Completed', 2026),
                (6, 2, 75.0, 'Cancelled', 2026),
                (7, 3, 500.0, 'Completed', 2025),
                (8, 3, 250.0, 'Completed', 2025),
                (9, 4, 100.0, 'Completed', 2026),
                (10, 5, 1000.0, 'Completed', 2026)
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
            public double Amount { get; set; }

            [Column(Name = "Status")]
            public string Status { get; set; }

            [Column(Name = "Year")]
            public int Year { get; set; }
        }

        // DTO classes for GroupBy results - Dapper can handle type conversion for these
        public class CustomerSummary
        {
            public int CustomerId { get; set; }
            public long Count { get; set; }
        }

        public class CustomerAggregates
        {
            public int CustomerId { get; set; }
            public long OrderCount { get; set; }
            public double TotalAmount { get; set; }
            public double AvgAmount { get; set; }
            public double MinAmount { get; set; }
            public double MaxAmount { get; set; }
        }

        #endregion

        #region Basic GroupBy with Count

        [Fact]
        public void GroupBy_SingleColumn_Count()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act - Use DTO to avoid SQLite type mismatch with anonymous types
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Select(g => new CustomerSummary
                {
                    CustomerId = g.Key,
                    Count = g.Count()
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

            // Act - Use DTO to avoid SQLite type mismatch with anonymous types
            var result = await orders
                .GroupBy(o => o.CustomerId)
                .SelectAsync(g => new CustomerSummary
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
            Assert.Contains(result, r => r.CustomerId == 1 && r.TotalAmount == 450.0);
            Assert.Contains(result, r => r.CustomerId == 2 && r.TotalAmount == 425.0);
            Assert.Contains(result, r => r.CustomerId == 3 && r.TotalAmount == 750.0);
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
            Assert.Contains(result, r => r.CustomerId == 1 && Math.Abs(r.AvgAmount - 150.0) < 0.01);
            Assert.Contains(result, r => r.CustomerId == 3 && Math.Abs(r.AvgAmount - 375.0) < 0.01);
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
            Assert.Equal(100.0, customer1.MinAmount);
            Assert.Equal(200.0, customer1.MaxAmount);
        }

        [Fact]
        public void GroupBy_MultipleAggregates()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act - Use DTO to avoid SQLite type mismatch
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Select(g => new CustomerAggregates
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
            Assert.Equal(450.0, customer1.TotalAmount);
            Assert.Equal(150.0, customer1.AvgAmount);
            Assert.Equal(100.0, customer1.MinAmount);
            Assert.Equal(200.0, customer1.MaxAmount);
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
                    Count = (long)g.Count()
                })
                .ToList();

            // Assert
            Assert.Equal(5, result.Count); // 5 distinct (CustomerId, Year) combinations in the seed data
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
                    Count = (long)g.Count(),
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
                    Count = (long)g.Count()
                })
                .ToList();

            // Assert
            Assert.Equal(3, result.Count); // Only customers 1, 2, 3 have more than 1 order
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
            Assert.Equal(4, result.Count); // Customers 1 (450), 2 (425), 3 (750), 5 (1000)
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
                    Count = (long)g.Count(),
                    TotalAmount = g.Sum(x => x.Amount)
                })
                .ToList();

            // Assert
            Assert.Equal(3, result.Count); // Customers 1, 2, 3
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
                    Count = (long)g.Count()
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
            // Customer 5 has highest total (1000)
            Assert.Equal(5, result[0].CustomerId);
        }

        #endregion

        #region Complete Queries

        [Fact]
        public void GroupBy_CompleteQuery_WithWhereAndHaving()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act - This is the target API from the roadmap
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Where(g => g.Count() > 1)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    OrderCount = (long)g.Count(),
                    TotalAmount = g.Sum(x => x.Amount),
                    AvgAmount = g.Average(x => x.Amount)
                })
                .ToList();

            // Assert
            Assert.Equal(3, result.Count);
            var customer1 = result.First(r => r.CustomerId == 1);
            Assert.Equal(3, customer1.OrderCount);
            Assert.Equal(450.0, customer1.TotalAmount);
            Assert.Equal(150.0, customer1.AvgAmount);
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
                .Where(g => g.Sum(x => x.Amount) > 10000) // No customer has this much
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
                    Count = (long)g.Count()
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
