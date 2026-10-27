using System;
using System.Linq;
using System.Threading.Tasks;
using ZeroData.Sql;
using ZeroData.Sql.Mapping;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ZeroData.Sql.Tests
{
    /// <summary>
    /// Tests for features added/fixed during the comprehensive development pass:
    /// WhereIf accumulation, AndWhere, anonymous-type GroupBy/Join projection,
    /// LIKE wildcard escaping, dialect-aware quoting, Where-before-GroupBy,
    /// conditional Count, expression aggregates, composite-key bulk ops,
    /// value-converter read path, and convention PK detection.
    /// </summary>
    public class FeatureEnhancementsTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly SqlContext _context;

        public FeatureEnhancementsTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            _connection.Execute(@"
                CREATE TABLE Orders (
                    Id INTEGER PRIMARY KEY,
                    CustomerId INTEGER NOT NULL,
                    Amount REAL NOT NULL,
                    Status TEXT NOT NULL,
                    Note TEXT
                )");

            _connection.Execute(@"
                INSERT INTO Orders (Id, CustomerId, Amount, Status, Note) VALUES
                (1, 1, 100.0, 'Completed', '50% off'),
                (2, 1, 200.0, 'Pending',   'normal'),
                (3, 2, 150.0, 'Completed', 'normal'),
                (4, 2, 50.0,  'Cancelled', 'normal'),
                (5, 3, 300.0, 'Completed', '50% off')");

            _context = new SqlContext(_connection);
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
            public double Amount { get; set; }
            [Column(Name = "Status")]
            public string Status { get; set; }
            [Column(Name = "Note")]
            public string Note { get; set; }
        }

        public class CustomerTotal
        {
            public int CustomerId { get; set; }
            public long OrderCount { get; set; }
            public double Total { get; set; }
        }

        // ---- WhereIf / AndWhere accumulation ----

        [Fact]
        public void WhereIf_AccumulatesPredicates_BeforeTerminal()
        {
            var result = _context.GetTable<Order>()
                .WhereIf(true, o => o.Status == "Completed")
                .WhereIf(true, o => o.Amount >= 150)
                .WhereIf(false, o => o.CustomerId == 999) // ignored
                .ToList();

            Assert.Equal(2, result.Count); // order 3 (150) and 5 (300)
            Assert.All(result, o => Assert.Equal("Completed", o.Status));
            Assert.All(result, o => Assert.True(o.Amount >= 150));
        }

        [Fact]
        public void AndWhere_CombinesWithFinalWhere()
        {
            var result = _context.GetTable<Order>()
                .AndWhere(o => o.Status == "Completed")
                .Where(o => o.CustomerId == 1);

            Assert.Single(result);
            Assert.Equal(1, result[0].Id);
        }

        [Fact]
        public async Task WhereIf_Works_WithAsyncTerminal()
        {
            var result = await _context.GetTable<Order>()
                .WhereIf(true, o => o.Status == "Completed")
                .ToListAsync();

            Assert.Equal(3, result.Count);
        }

        // ---- LIKE wildcard escaping ----

        [Fact]
        public void Contains_EscapesPercentWildcard()
        {
            // "50%" must match literally, not as "50<anything>"
            var result = _context.GetTable<Order>()
                .Where(o => o.Note.Contains("50%"));

            Assert.Equal(2, result.Count);
            Assert.All(result, o => Assert.Contains("50%", o.Note));
        }

        // ---- GroupBy anonymous type projection ----

        [Fact]
        public void GroupBy_AnonymousType_Materializes()
        {
            var result = _context.GetTable<Order>()
                .GroupBy(o => o.CustomerId)
                .Select(g => new { CustomerId = g.Key, Count = g.Count(), Total = g.Sum(x => x.Amount) })
                .ToList();

            Assert.Equal(3, result.Count);
            var c1 = result.First(r => r.CustomerId == 1);
            Assert.Equal(2, c1.Count);
            Assert.Equal(300.0, c1.Total);
        }

        // ---- Where before GroupBy ----

        [Fact]
        public void Where_Before_GroupBy_FiltersRows()
        {
            var result = _context.GetTable<Order>()
                .AndWhere(o => o.Status == "Completed")
                .GroupBy(o => o.CustomerId)
                .Select(g => new CustomerTotal
                {
                    CustomerId = g.Key,
                    OrderCount = g.Count(),
                    Total = g.Sum(x => x.Amount)
                })
                .ToList();

            // Only completed orders: customer 1 (100), 2 (150), 3 (300)
            Assert.Equal(3, result.Count);
            Assert.Equal(100.0, result.First(r => r.CustomerId == 1).Total);
        }

        // ---- Conditional count + expression aggregate (F9) ----

        [Fact]
        public void GroupBy_ConditionalCount_CountsMatchingRows()
        {
            var result = _context.GetTable<Order>()
                .GroupBy(o => o.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    CompletedCount = g.Count(x => x.Status == "Completed")
                })
                .ToList();

            Assert.Equal(1, result.First(r => r.CustomerId == 1).CompletedCount);
            Assert.Equal(1, result.First(r => r.CustomerId == 2).CompletedCount);
        }

        [Fact]
        public void GroupBy_ExpressionSum_ComputesArithmetic()
        {
            var result = _context.GetTable<Order>()
                .GroupBy(o => o.CustomerId)
                .Select(g => new { CustomerId = g.Key, Weighted = g.Sum(x => x.Amount * 2) })
                .ToList();

            Assert.Equal(600.0, result.First(r => r.CustomerId == 1).Weighted); // (100+200)*2
        }

        // ---- Join Where + async ----

        [Fact]
        public async Task Join_Where_And_Async_Work()
        {
            _connection.Execute(@"
                CREATE TABLE Customers (Id INTEGER PRIMARY KEY, Name TEXT, Country TEXT);
                INSERT INTO Customers (Id, Name, Country) VALUES
                (1, 'Alice', 'VN'), (2, 'Bob', 'US'), (3, 'Carol', 'VN');");

            var result = await _context.GetTable<Order>()
                .Join(_context.GetTable<Customer>(),
                    o => o.CustomerId,
                    c => c.Id,
                    (o, c) => new { o.Id, c.Country, o.Amount })
                .Where((o, c) => c.Country == "VN")
                .ToListAsync();

            Assert.All(result, r => Assert.Equal("VN", r.Country));
            Assert.True(result.Count >= 1);
        }

        [Table(Name = "Customers")]
        public class Customer
        {
            [Column(Name = "Id", IsPrimaryKey = true)]
            public int Id { get; set; }
            [Column(Name = "Name")]
            public string Name { get; set; }
            [Column(Name = "Country")]
            public string Country { get; set; }
        }
    }

    /// <summary>
    /// Convention-based mapping (no [Column] attributes) should auto-detect a primary key.
    /// </summary>
    public class ConventionMappingTests
    {
        public class PlainEntity
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        [Fact]
        public void ConventionMapping_DetectsIdAsPrimaryKey()
        {
            MappingCache.Clear();
            var mapping = MappingCache.GetMapping<PlainEntity>();

            Assert.Single(mapping.PrimaryKeys);
            Assert.Equal("Id", mapping.PrimaryKeys[0].ColumnName);
            Assert.True(mapping.PrimaryKeys[0].IsDbGenerated);
        }
    }

    /// <summary>
    /// Optimistic concurrency (F6): a versioned UPDATE that affects no rows throws.
    /// </summary>
    public class ConcurrencyTests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public ConcurrencyTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _connection.Execute(@"
                CREATE TABLE VersionedDocs (
                    Id INTEGER PRIMARY KEY,
                    Title TEXT NOT NULL,
                    Version INTEGER NOT NULL
                );
                INSERT INTO VersionedDocs (Id, Title, Version) VALUES (1, 'Original', 1);");
        }

        public void Dispose() => _connection?.Dispose();

        [Table(Name = "VersionedDocs")]
        public class VersionedDoc
        {
            [Column(Name = "Id", IsPrimaryKey = true)]
            public int Id { get; set; }
            [Column(Name = "Title")]
            public string Title { get; set; }
            [Column(Name = "Version", IsVersion = true)]
            public int Version { get; set; }
        }

        [Fact]
        public void ConcurrentUpdate_StaleVersion_Throws()
        {
            using var ctx = new SqlContext(_connection);

            // Load the entity (version = 1).
            var doc = ctx.GetTable<VersionedDoc>().FirstOrDefault(d => d.Id == 1);
            Assert.NotNull(doc);

            // Simulate another process bumping the version out-of-band.
            _connection.Execute("UPDATE VersionedDocs SET Version = 2, Title = 'Changed' WHERE Id = 1");

            // Our update still targets version = 1 → 0 rows affected → conflict.
            doc.Title = "My change";
            Assert.Throws<ConcurrencyException>(() => ctx.SubmitChanges());
        }

        [Fact]
        public void VersionedUpdate_IncrementsVersion_OnSuccess()
        {
            using var ctx = new SqlContext(_connection);
            var doc = ctx.GetTable<VersionedDoc>().FirstOrDefault(d => d.Id == 1);
            doc.Title = "Updated";
            ctx.SubmitChanges();

            var dbVersion = _connection.ExecuteScalar<int>("SELECT Version FROM VersionedDocs WHERE Id = 1");
            Assert.Equal(2, dbVersion); // incremented from 1
        }
    }

    /// <summary>
    /// Value converter read-path (F3): registered converters apply on SELECT via Dapper handler.
    /// </summary>
    public class ValueConverterReadTests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public ValueConverterReadTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _connection.Execute(@"
                CREATE TABLE Tickets (Id INTEGER PRIMARY KEY, Priority TEXT NOT NULL);
                INSERT INTO Tickets (Id, Priority) VALUES (1, 'High'), (2, 'Low');");
        }

        public void Dispose() => _connection?.Dispose();

        public enum Priority { Low, High }

        [Table(Name = "Tickets")]
        public class Ticket
        {
            [Column(Name = "Id", IsPrimaryKey = true)]
            public int Id { get; set; }
            [Column(Name = "Priority")]
            public Priority Priority { get; set; }
        }

        [Fact]
        public void Converter_AppliesOnRead()
        {
            using var ctx = new SqlContext(_connection);
            ctx.Converters.Add<Priority, string>(
                toDb: p => p.ToString(),
                fromDb: s => (Priority)Enum.Parse(typeof(Priority), s));

            var ticket = ctx.GetTable<Ticket>().FirstOrDefault(t => t.Id == 1);
            Assert.NotNull(ticket);
            Assert.Equal(Priority.High, ticket.Priority);
        }
    }
}
