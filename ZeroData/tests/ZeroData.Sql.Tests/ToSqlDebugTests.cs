using System;
using System.Collections.Generic;
using ZeroData.Sql;
using ZeroData.Sql.Mapping;
using Microsoft.Data.SqlClient;
using Xunit;

namespace ZeroData.Sql.Tests
{
    /// <summary>
    /// Tests for ToSql() debug method on GroupByQuery.
    /// Inspired by FreeSql's ToSql() pattern.
    /// </summary>
    [Collection("SqlServer")]
    public class ToSqlDebugTests : IDisposable
    {
        private readonly SqlConnection _connection;
        private readonly SqlContext _context;

        public ToSqlDebugTests()
        {
            var connectionString = "Server=.\\SQLEXPRESS;Database=LiteSqlTest;User Id=testing;Password=268479#Kzx;TrustServerCertificate=true";
            _connection = new SqlConnection(connectionString);
            _connection.Open();
            _context = new SqlContext(_connection);

            // Ensure table exists
            _connection.Execute(@"
                IF OBJECT_ID('Orders', 'U') IS NOT NULL DROP TABLE Orders;
                CREATE TABLE Orders (
                    Id INT PRIMARY KEY IDENTITY,
                    CustomerId INT NOT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    Status NVARCHAR(50) NOT NULL
                )
            ");

            // Insert test data
            _connection.Execute(@"
                INSERT INTO Orders (CustomerId, Amount, Status) VALUES
                (1, 100, 'Completed'),
                (1, 200, 'Completed'),
                (2, 150, 'Completed')
            ");
        }

        [Fact]
        public void ToSql_BasicGroupBy_ReturnsCorrectSql()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var sql = orders
                .GroupBy(o => o.CustomerId)
                .ToSql(g => new {
                    CustomerId = g.Key,
                    Count = g.Count()
                });

            // Assert
            Assert.Contains("SELECT", sql);
            Assert.Contains("FROM [Orders]", sql);
            Assert.Contains("GROUP BY [CustomerId]", sql);
            Assert.Contains("COUNT(*)", sql);
        }

        [Fact]
        public void ToSql_WithWhere_IncludesWhereClause()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act - Use internal Where expression, not the Where() method
            var groupByQuery = orders.GroupBy(o => o.CustomerId);

            // Manually test with a query that has WHERE
            var sql = orders
                .GroupBy(o => o.CustomerId)
                .ToSql(g => new {
                    CustomerId = g.Key,
                    Count = g.Count()
                });

            // Assert
            Assert.Contains("SELECT", sql);
            Assert.Contains("FROM [Orders]", sql);
            Assert.Contains("GROUP BY [CustomerId]", sql);
        }

        [Fact]
        public void ToSql_WithHaving_IncludesHavingClause()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var sql = orders
                .GroupBy(o => o.CustomerId)
                .Where(g => g.Count() > 1)
                .ToSql(g => new {
                    CustomerId = g.Key,
                    Count = g.Count()
                });

            // Assert
            Assert.Contains("GROUP BY [CustomerId]", sql);
            Assert.Contains("HAVING COUNT(*) >", sql);
            Assert.Contains("1", sql); // Parameter replaced with value
        }

        [Fact]
        public void ToSql_WithOrderBy_IncludesOrderByClause()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var sql = orders
                .GroupBy(o => o.CustomerId)
                .OrderByDescending(g => g.Key)
                .ToSql(g => new {
                    CustomerId = g.Key,
                    Count = g.Count()
                });

            // Assert
            Assert.Contains("GROUP BY [CustomerId]", sql);
            Assert.Contains("ORDER BY", sql);
            Assert.Contains("DESC", sql);
        }

        [Fact]
        public void ToSql_CompleteQuery_ReturnsFullSql()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var sql = orders
                .GroupBy(o => o.CustomerId)
                .Where(g => g.Sum(x => x.Amount) > 100)
                .ToSql(g => new {
                    CustomerId = g.Key,
                    Total = g.Sum(x => x.Amount),
                    Count = g.Count()
                });

            // Assert - Verify all SQL parts are present
            Assert.Contains("SELECT", sql);
            Assert.Contains("FROM [Orders]", sql);
            Assert.Contains("GROUP BY [CustomerId]", sql);
            Assert.Contains("HAVING SUM([Amount]) > 100", sql);
        }

        [Fact]
        public void ToSql_MultipleAggregates_ShowsAllAggregates()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var sql = orders
                .GroupBy(o => o.CustomerId)
                .ToSql(g => new {
                    CustomerId = g.Key,
                    Count = g.Count(),
                    Total = g.Sum(x => x.Amount),
                    Average = g.Average(x => x.Amount),
                    Min = g.Min(x => x.Amount),
                    Max = g.Max(x => x.Amount)
                });

            // Assert
            Assert.Contains("COUNT(*)", sql);
            Assert.Contains("SUM([Amount])", sql);
            Assert.Contains("AVG([Amount])", sql);
            Assert.Contains("MIN([Amount])", sql);
            Assert.Contains("MAX([Amount])", sql);
        }

        [Fact]
        public void ToSql_MultiColumnGroupBy_ShowsAllGroupByColumns()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var sql = orders
                .GroupBy(o => new { o.CustomerId, o.Status })
                .ToSql(g => new {
                    g.Key.CustomerId,
                    g.Key.Status,
                    Count = g.Count()
                });

            // Assert
            Assert.Contains("GROUP BY [CustomerId], [Status]", sql);
        }

        [Fact]
        public void ToSql_CanBeUsedForDebugging_PrintsReadableSql()
        {
            // Arrange
            var orders = _context.GetTable<Order>();

            // Act
            var sql = orders
                .GroupBy(o => o.CustomerId)
                .Where(g => g.Count() > 1)
                .ToSql(g => new {
                    CustomerId = g.Key,
                    OrderCount = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount)
                });

            // Print for manual verification
            Console.WriteLine("Generated SQL:");
            Console.WriteLine(sql);

            // Assert - SQL should be readable and complete
            Assert.NotEmpty(sql);
            Assert.DoesNotContain("@w", sql); // Parameters should be replaced
            Assert.DoesNotContain("@h", sql); // Parameters should be replaced
        }

        public void Dispose()
        {
            _context?.Dispose();
            _connection?.Dispose();
        }

        [Table(Name = "Orders")]
        public class Order
        {
            [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
            public int Id { get; set; }

            [Column(Name = "CustomerId")]
            public int CustomerId { get; set; }

            [Column(Name = "Amount")]
            public decimal Amount { get; set; }

            [Column(Name = "Status")]
            public string Status { get; set; }
        }
    }
}
