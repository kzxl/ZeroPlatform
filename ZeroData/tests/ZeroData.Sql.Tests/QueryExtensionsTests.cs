using System;
using System.Collections.Generic;
using System.Linq;
using ZeroData.Sql;
using ZeroData.Sql.Mapping;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ZeroData.Sql.Tests
{
    /// <summary>
    /// Tests for QueryExtensions (WhereIf, OrderByIf, etc.)
    /// Inspired by SqlSugar and FreeSql patterns.
    /// </summary>
    public class QueryExtensionsTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly SqlContext _context;

        public QueryExtensionsTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _context = new SqlContext(_connection);

            // Create test table
            _connection.Execute(@"
                CREATE TABLE Orders (
                    Id INTEGER PRIMARY KEY,
                    CustomerId INTEGER NOT NULL,
                    Amount DECIMAL NOT NULL,
                    Status TEXT NOT NULL
                )
            ");

            // Insert test data
            _connection.Execute(@"
                INSERT INTO Orders (Id, CustomerId, Amount, Status) VALUES
                (1, 1, 100, 'Completed'),
                (2, 1, 200, 'Pending'),
                (3, 2, 150, 'Completed'),
                (4, 2, 50, 'Cancelled'),
                (5, 3, 300, 'Completed')
            ");
        }

        [Fact]
        public void WhereIf_WhenConditionTrue_AppliesFilter()
        {
            // Arrange
            var orders = _context.GetTable<Order>();
            string status = "Completed";

            // Act
            var result = orders
                .WhereIf(!string.IsNullOrEmpty(status), o => o.Status == status)
                .ToList();

            // Assert
            Assert.Equal(3, result.Count);
            Assert.All(result, o => Assert.Equal("Completed", o.Status));
        }

        [Fact]
        public void WhereIf_WhenConditionFalse_DoesNotApplyFilter()
        {
            // Arrange
            var orders = _context.GetTable<Order>();
            string status = null;

            // Act
            var result = orders
                .WhereIf(!string.IsNullOrEmpty(status), o => o.Status == status)
                .ToList();

            // Assert
            Assert.Equal(5, result.Count); // All orders
        }

        [Fact]
        public void WhereIf_MultipleConditions_AppliesOnlyTrueConditions()
        {
            // Arrange
            var orders = _context.GetTable<Order>();
            string status = "Completed";
            int? minAmount = null;
            int? maxAmount = 200;

            // Act
            var result = orders
                .WhereIf(!string.IsNullOrEmpty(status), o => o.Status == status)
                .WhereIf(minAmount.HasValue, o => o.Amount >= minAmount.Value)
                .WhereIf(maxAmount.HasValue, o => o.Amount <= maxAmount.Value)
                .ToList();

            // Assert
            Assert.Equal(2, result.Count); // Completed orders with Amount <= 200
            Assert.All(result, o => Assert.Equal("Completed", o.Status));
            Assert.All(result, o => Assert.True(o.Amount <= 200));
        }

        [Fact]
        public void WhereIf_ComplexScenario_BuildsDynamicQuery()
        {
            // Arrange - Simulate dynamic search form
            var orders = _context.GetTable<Order>();

            // Search parameters (some null, some filled)
            string status = "Completed";
            int? customerId = null;
            decimal? minAmount = 100;
            decimal? maxAmount = null;

            // Act - Build query dynamically
            var result = orders
                .WhereIf(!string.IsNullOrEmpty(status), o => o.Status == status)
                .WhereIf(customerId.HasValue, o => o.CustomerId == customerId.Value)
                .WhereIf(minAmount.HasValue, o => o.Amount >= minAmount.Value)
                .WhereIf(maxAmount.HasValue, o => o.Amount <= maxAmount.Value)
                .Where(o => true) // Execute query
                .ToList();

            // Assert
            Assert.Equal(3, result.Count); // Completed orders with Amount >= 100
            Assert.All(result, o => Assert.Equal("Completed", o.Status));
            Assert.All(result, o => Assert.True(o.Amount >= 100));
        }

        public void Dispose()
        {
            _context?.Dispose();
            _connection?.Dispose();
        }

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
        }

        public class CustomerOrderCount
        {
            public int CustomerId { get; set; }
            public long Count { get; set; }
        }
    }
}
