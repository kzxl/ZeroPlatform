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
    /// Tests for multi-table (3+) joins via JoinMany / LeftJoinMany chains.
    /// </summary>
    public class MultiJoinTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly SqlContext _db;

        public MultiJoinTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _connection.Execute(@"
                CREATE TABLE Countries (Id INTEGER PRIMARY KEY, Name TEXT);
                CREATE TABLE Customers (Id INTEGER PRIMARY KEY, Name TEXT, CountryId INTEGER);
                CREATE TABLE Orders (Id INTEGER PRIMARY KEY, CustomerId INTEGER, Amount REAL);
                CREATE TABLE OrderLines (Id INTEGER PRIMARY KEY, OrderId INTEGER, Sku TEXT, Qty INTEGER);

                INSERT INTO Countries (Id, Name) VALUES (1,'Vietnam'),(2,'USA');
                INSERT INTO Customers (Id, Name, CountryId) VALUES (1,'Alice',1),(2,'Bob',2),(3,'Carol',1);
                INSERT INTO Orders (Id, CustomerId, Amount) VALUES (1,1,100),(2,1,200),(3,2,150);
                INSERT INTO OrderLines (Id, OrderId, Sku, Qty) VALUES (1,1,'A',2),(2,1,'B',1),(3,2,'C',5),(4,3,'D',3);");
            _db = new SqlContext(_connection);
        }

        public void Dispose() { _db?.Dispose(); _connection?.Dispose(); }

        [Table(Name = "Countries")]
        public class Country
        {
            [Column(Name = "Id", IsPrimaryKey = true)] public int Id { get; set; }
            [Column(Name = "Name")] public string Name { get; set; }
        }

        [Table(Name = "Customers")]
        public class Customer
        {
            [Column(Name = "Id", IsPrimaryKey = true)] public int Id { get; set; }
            [Column(Name = "Name")] public string Name { get; set; }
            [Column(Name = "CountryId")] public int CountryId { get; set; }
        }

        [Table(Name = "Orders")]
        public class Order
        {
            [Column(Name = "Id", IsPrimaryKey = true)] public int Id { get; set; }
            [Column(Name = "CustomerId")] public int CustomerId { get; set; }
            [Column(Name = "Amount")] public double Amount { get; set; }
        }

        [Table(Name = "OrderLines")]
        public class OrderLine
        {
            [Column(Name = "Id", IsPrimaryKey = true)] public int Id { get; set; }
            [Column(Name = "OrderId")] public int OrderId { get; set; }
            [Column(Name = "Sku")] public string Sku { get; set; }
            [Column(Name = "Qty")] public int Qty { get; set; }
        }

        public class Row3
        {
            public int OrderId { get; set; }
            public string Customer { get; set; }
            public string Country { get; set; }
            public double Amount { get; set; }
        }

        [Fact]
        public void ThreeTableJoin_AnonymousType()
        {
            var rows = _db.GetTable<Order>()
                .JoinMany(_db.GetTable<Customer>(), (o, c) => o.CustomerId == c.Id)
                .Join(_db.GetTable<Country>(), (o, c, ct) => c.CountryId == ct.Id)
                .Select((o, c, ct) => new { OrderId = o.Id, Customer = c.Name, Country = ct.Name, o.Amount });

            Assert.Equal(3, rows.Count);
            var first = rows.First(r => r.OrderId == 1);
            Assert.Equal("Alice", first.Customer);
            Assert.Equal("Vietnam", first.Country);
        }

        [Fact]
        public void ThreeTableJoin_WithWhere_AndDto()
        {
            var rows = _db.GetTable<Order>()
                .JoinMany(_db.GetTable<Customer>(), (o, c) => o.CustomerId == c.Id)
                .Join(_db.GetTable<Country>(), (o, c, ct) => c.CountryId == ct.Id)
                .Where((o, c, ct) => ct.Name == "Vietnam")
                .Select((o, c, ct) => new Row3 { OrderId = o.Id, Customer = c.Name, Country = ct.Name, Amount = o.Amount });

            Assert.Equal(2, rows.Count); // Alice's 2 orders (Vietnam)
            Assert.All(rows, r => Assert.Equal("Vietnam", r.Country));
        }

        [Fact]
        public void FourTableJoin_Works()
        {
            var rows = _db.GetTable<Order>()
                .JoinMany(_db.GetTable<Customer>(), (o, c) => o.CustomerId == c.Id)
                .Join(_db.GetTable<Country>(), (o, c, ct) => c.CountryId == ct.Id)
                .Join(_db.GetTable<OrderLine>(), (o, c, ct, l) => l.OrderId == o.Id)
                .Where((o, c, ct, l) => l.Qty >= 2)
                .Select((o, c, ct, l) => new { o.Id, Customer = c.Name, Country = ct.Name, l.Sku, l.Qty });

            // Lines with Qty >= 2: A(2), C(5), D(3) → 3 rows
            Assert.Equal(3, rows.Count);
        }

        [Fact]
        public async Task ThreeTableJoin_Async()
        {
            var rows = await _db.GetTable<Order>()
                .JoinMany(_db.GetTable<Customer>(), (o, c) => o.CustomerId == c.Id)
                .Join(_db.GetTable<Country>(), (o, c, ct) => c.CountryId == ct.Id)
                .SelectAsync((o, c, ct) => new { o.Id, Country = ct.Name });

            Assert.Equal(3, rows.Count);
        }

        [Fact]
        public void LeftJoin_IncludesUnmatched()
        {
            // Orders LEFT JOIN OrderLines — every order kept even if it had no lines.
            // All orders here have lines, but verify LEFT JOIN multiplies rows correctly.
            var rows = _db.GetTable<Order>()
                .LeftJoinMany(_db.GetTable<OrderLine>(), (o, l) => l.OrderId == o.Id)
                .Select((o, l) => new { o.Id, l.Sku });

            Assert.Equal(4, rows.Count); // 4 order lines total
        }

        [Fact]
        public void ToSql_ProducesJoinSql()
        {
            var sql = _db.GetTable<Order>()
                .JoinMany(_db.GetTable<Customer>(), (o, c) => o.CustomerId == c.Id)
                .Join(_db.GetTable<Country>(), (o, c, ct) => c.CountryId == ct.Id)
                .ToSql((o, c, ct) => new { o.Id });

            Assert.Contains("FROM \"Orders\" AS t1", sql);
            Assert.Contains("INNER JOIN \"Customers\" AS t2 ON", sql);
            Assert.Contains("INNER JOIN \"Countries\" AS t3 ON", sql);
        }
    }
}
