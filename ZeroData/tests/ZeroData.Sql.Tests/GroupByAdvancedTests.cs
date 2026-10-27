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
    /// Advanced GroupBy tests covering edge cases, NULL handling, and complex scenarios
    /// </summary>
    [Collection("SqlServer")]
    public class GroupByAdvancedTests : IDisposable
    {
        private readonly SqlConnection _connection;
        private readonly SqlContext _context;
        private const string ConnectionString = "Server=.\\SQLEXPRESS;Database=LiteSqlTest;User Id=testing;Password=268479#Kzx;TrustServerCertificate=true";

        public GroupByAdvancedTests()
        {
            _connection = new SqlConnection(ConnectionString);
            _connection.Open();

            // Create test table with nullable columns
            _connection.Execute(@"
                IF OBJECT_ID('AdvancedOrders', 'U') IS NOT NULL DROP TABLE AdvancedOrders;
                CREATE TABLE AdvancedOrders (
                    Id INT PRIMARY KEY IDENTITY,
                    CustomerId INT NOT NULL,
                    CategoryId INT NULL,
                    Amount DECIMAL(18,2) NULL,
                    Discount DECIMAL(18,2) NULL,
                    Status NVARCHAR(50) NOT NULL,
                    Year INT NOT NULL
                );
            ");

            // Seed test data with NULLs
            _connection.Execute(@"
                INSERT INTO AdvancedOrders (CustomerId, CategoryId, Amount, Discount, Status, Year) VALUES
                (1, 1, 100.0, 10.0, 'Completed', 2026),
                (1, 1, 200.0, NULL, 'Completed', 2026),
                (1, NULL, 150.0, 15.0, 'Pending', 2026),
                (2, 2, NULL, 20.0, 'Completed', 2026),
                (2, 2, 300.0, NULL, 'Completed', 2026),
                (3, NULL, NULL, NULL, 'Cancelled', 2026),
                (4, 3, 500.0, 50.0, 'Completed', 2025)
            ");

            _context = new SqlContext(_connection);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _connection?.Dispose();
        }

        #region Test Entity

        [Table(Name = "AdvancedOrders")]
        public class AdvancedOrder
        {
            [Column(Name = "Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column(Name = "CustomerId")]
            public int CustomerId { get; set; }

            [Column(Name = "CategoryId")]
            public int? CategoryId { get; set; }

            [Column(Name = "Amount")]
            public decimal? Amount { get; set; }

            [Column(Name = "Discount")]
            public decimal? Discount { get; set; }

            [Column(Name = "Status")]
            public string Status { get; set; }

            [Column(Name = "Year")]
            public int Year { get; set; }
        }

        #endregion

        #region NULL Handling Tests

        [Fact]
        public void GroupBy_WithNullableColumn_InGroupKey()
        {
            // Arrange
            var orders = _context.GetTable<AdvancedOrder>();

            // Act - GroupBy on nullable column
            var result = orders
                .GroupBy(o => o.CategoryId)
                .Select(g => new
                {
                    CategoryId = g.Key,
                    Count = g.Count()
                })
                .ToList();

            // Assert
            Assert.Equal(4, result.Count); // NULL, 1, 2, 3
            Assert.Contains(result, r => r.CategoryId == null && r.Count == 2);
            Assert.Contains(result, r => r.CategoryId == 1 && r.Count == 2);
            Assert.Contains(result, r => r.CategoryId == 2 && r.Count == 2);
            Assert.Contains(result, r => r.CategoryId == 3 && r.Count == 1);
        }

        [Fact]
        public void GroupBy_SumWithNullValues()
        {
            // Arrange
            var orders = _context.GetTable<AdvancedOrder>();

            // Act - SUM should ignore NULL values
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    TotalAmount = g.Sum(x => x.Amount),
                    TotalDiscount = g.Sum(x => x.Discount)
                })
                .ToList();

            // Assert
            var customer1 = result.First(r => r.CustomerId == 1);
            Assert.Equal(450.0m, customer1.TotalAmount); // 100 + 200 + 150, NULL ignored

            var customer2 = result.First(r => r.CustomerId == 2);
            Assert.Equal(300.0m, customer2.TotalAmount); // Only 300, NULL ignored
            Assert.Equal(20.0m, customer2.TotalDiscount); // Only 20, NULL ignored
        }

        [Fact]
        public void GroupBy_AverageWithNullValues()
        {
            // Arrange
            var orders = _context.GetTable<AdvancedOrder>();

            // Act - AVG should ignore NULL values
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    AvgAmount = g.Average(x => x.Amount)
                })
                .ToList();

            // Assert
            var customer1 = result.First(r => r.CustomerId == 1);
            Assert.Equal(150.0m, customer1.AvgAmount); // (100 + 200 + 150) / 3

            var customer2 = result.First(r => r.CustomerId == 2);
            Assert.Equal(300.0m, customer2.AvgAmount); // 300 / 1, NULL ignored
        }

        [Fact]
        public void GroupBy_CountWithNullValues()
        {
            // Arrange
            var orders = _context.GetTable<AdvancedOrder>();

            // Act - COUNT(*) counts all rows, COUNT(column) ignores NULLs
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    TotalRows = g.Count(),
                    // Note: COUNT(column) not directly supported, but Count() counts all rows
                })
                .ToList();

            // Assert
            var customer1 = result.First(r => r.CustomerId == 1);
            Assert.Equal(3, customer1.TotalRows);

            var customer3 = result.First(r => r.CustomerId == 3);
            Assert.Equal(1, customer3.TotalRows); // Even though all values are NULL
        }

        #endregion

        #region Empty Result Tests

        [Fact]
        public void GroupBy_OnEmptyTable()
        {
            // Arrange
            _connection.Execute("DELETE FROM AdvancedOrders");
            var orders = _context.GetTable<AdvancedOrder>();

            // Act
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Count = g.Count()
                })
                .ToList();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GroupBy_WithWhereReturningEmpty()
        {
            // Arrange
            var orders = _context.GetTable<AdvancedOrder>();

            // Act
            var result = orders
                .Where(o => o.Year == 2099) // No data for this year
                .GroupBy(o => o.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Count = g.Count()
                })
                .ToList();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GroupBy_WithHavingReturningEmpty()
        {
            // Arrange
            var orders = _context.GetTable<AdvancedOrder>();

            // Act
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Where(g => g.Sum(x => x.Amount) > 100000) // No customer has this much
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Total = g.Sum(x => x.Amount)
                })
                .ToList();

            // Assert
            Assert.Empty(result);
        }

        #endregion

        #region Complex Scenarios

        [Fact]
        public void GroupBy_MultiColumnWithNullable()
        {
            // Arrange
            var orders = _context.GetTable<AdvancedOrder>();

            // Act
            var result = orders
                .GroupBy(o => new { o.CustomerId, o.CategoryId })
                .Select(g => new
                {
                    CustomerId = g.Key.CustomerId,
                    CategoryId = g.Key.CategoryId,
                    Count = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount)
                })
                .ToList();

            // Assert
            Assert.True(result.Count >= 5);

            // Check NULL category group
            var customer1NullCategory = result.FirstOrDefault(r =>
                r.CustomerId == 1 && r.CategoryId == null);
            Assert.NotNull(customer1NullCategory);
            Assert.Equal(1, customer1NullCategory.Count);
        }

        [Fact]
        public void GroupBy_WithMinMaxOnNullableColumns()
        {
            // Arrange
            var orders = _context.GetTable<AdvancedOrder>();

            // Act
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    MinAmount = g.Min(x => x.Amount),
                    MaxAmount = g.Max(x => x.Amount),
                    MinDiscount = g.Min(x => x.Discount),
                    MaxDiscount = g.Max(x => x.Discount)
                })
                .ToList();

            // Assert
            var customer1 = result.First(r => r.CustomerId == 1);
            Assert.Equal(100.0m, customer1.MinAmount);
            Assert.Equal(200.0m, customer1.MaxAmount);
        }

        [Fact]
        public void GroupBy_HavingWithNullableColumn()
        {
            // Arrange
            var orders = _context.GetTable<AdvancedOrder>();

            // Act - HAVING on nullable column aggregate
            var result = orders
                .GroupBy(o => o.CustomerId)
                .Where(g => g.Sum(x => x.Amount) > 200)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    TotalAmount = g.Sum(x => x.Amount)
                })
                .ToList();

            // Assert
            Assert.True(result.Count >= 2);
            Assert.All(result, r => Assert.True(r.TotalAmount > 200));
        }

        [Fact]
        public void GroupBy_OrderByNullableAggregate()
        {
            // Arrange
            var orders = _context.GetTable<AdvancedOrder>();

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
            Assert.True(result.Count > 0);
            // Verify descending order (NULLs may be first or last depending on DB)
            for (int i = 0; i < result.Count - 1; i++)
            {
                if (result[i].TotalAmount.HasValue && result[i + 1].TotalAmount.HasValue)
                {
                    Assert.True(result[i].TotalAmount >= result[i + 1].TotalAmount);
                }
            }
        }

        #endregion

        #region Performance & Edge Cases

        [Fact]
        public void GroupBy_SingleGroup()
        {
            // Arrange
            var orders = _context.GetTable<AdvancedOrder>();

            // Act - All rows in one group
            var result = orders
                .GroupBy(o => 1) // Constant key
                .Select(g => new
                {
                    Key = g.Key,
                    TotalOrders = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount)
                })
                .ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal(7, result[0].TotalOrders);
        }

        [Fact]
        public void GroupBy_ManyGroups()
        {
            // Arrange - Each row is its own group
            var orders = _context.GetTable<AdvancedOrder>();

            // Act
            var result = orders
                .GroupBy(o => o.Id) // Primary key - unique per row
                .Select(g => new
                {
                    Id = g.Key,
                    Count = g.Count()
                })
                .ToList();

            // Assert
            Assert.Equal(7, result.Count);
            Assert.All(result, r => Assert.Equal(1, r.Count));
        }

        [Fact]
        public async Task GroupBy_AsyncWithNullValues()
        {
            // Arrange
            var orders = _context.GetTable<AdvancedOrder>();

            // Act
            var result = await orders
                .GroupBy(o => o.CustomerId)
                .SelectAsync(g => new
                {
                    CustomerId = g.Key,
                    TotalAmount = g.Sum(x => x.Amount)
                });

            // Assert
            Assert.True(result.Count > 0);
        }

        #endregion
    }
}
