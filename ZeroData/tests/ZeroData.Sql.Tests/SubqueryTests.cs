using System;
using System.Linq;
using ZeroData.Sql;
using ZeroData.Sql.Mapping;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ZeroData.Sql.Tests
{
    /// <summary>
    /// Tests for correlated subqueries: WhereExists / WhereNotExists / WhereIn / WhereNotIn.
    /// </summary>
    public class SubqueryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly SqlContext _db;

        public SubqueryTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _connection.Execute(@"
                CREATE TABLE Customers (Id INTEGER PRIMARY KEY, Name TEXT);
                CREATE TABLE Orders (Id INTEGER PRIMARY KEY, CustomerId INTEGER, Status TEXT, Amount REAL);
                INSERT INTO Customers (Id, Name) VALUES (1,'Alice'),(2,'Bob'),(3,'Carol');
                INSERT INTO Orders (Id, CustomerId, Status, Amount) VALUES
                    (1,1,'Active',100),(2,1,'Closed',50),(3,2,'Active',200);");
            _db = new SqlContext(_connection);
        }

        public void Dispose() { _db?.Dispose(); _connection?.Dispose(); }

        [Table(Name = "Customers")]
        public class Customer
        {
            [Column(Name = "Id", IsPrimaryKey = true)] public int Id { get; set; }
            [Column(Name = "Name")] public string Name { get; set; }
        }

        [Table(Name = "Orders")]
        public class Order
        {
            [Column(Name = "Id", IsPrimaryKey = true)] public int Id { get; set; }
            [Column(Name = "CustomerId")] public int CustomerId { get; set; }
            [Column(Name = "Status")] public string Status { get; set; }
            [Column(Name = "Amount")] public double Amount { get; set; }
        }

        [Fact]
        public void WhereExists_ReturnsCustomersWithOrders()
        {
            var result = _db.GetTable<Customer>()
                .WhereExists(_db.GetTable<Order>(), (c, o) => o.CustomerId == c.Id)
                .ToList();

            Assert.Equal(2, result.Count); // Alice, Bob
            Assert.DoesNotContain(result, c => c.Id == 3); // Carol has none
        }

        [Fact]
        public void WhereNotExists_ReturnsCustomersWithoutOrders()
        {
            var result = _db.GetTable<Customer>()
                .WhereNotExists(_db.GetTable<Order>(), (c, o) => o.CustomerId == c.Id)
                .ToList();

            Assert.Single(result);
            Assert.Equal(3, result[0].Id); // Carol
        }

        [Fact]
        public void WhereExists_WithCorrelatedFilter()
        {
            // Customers having an Active order
            var result = _db.GetTable<Customer>()
                .WhereExists(_db.GetTable<Order>(), (c, o) => o.CustomerId == c.Id && o.Status == "Active")
                .ToList();

            Assert.Equal(2, result.Count); // Alice (active), Bob (active)
        }

        [Fact]
        public void WhereIn_WithInnerFilter()
        {
            // Customers whose Id is in the set of CustomerId from Active orders
            var result = _db.GetTable<Customer>()
                .WhereIn(c => c.Id, _db.GetTable<Order>(), o => o.CustomerId, o => o.Status == "Active")
                .ToList();

            Assert.Equal(2, result.Count);
            Assert.Contains(result, c => c.Id == 1);
            Assert.Contains(result, c => c.Id == 2);
        }

        [Fact]
        public void WhereNotIn_ExcludesMatching()
        {
            var result = _db.GetTable<Customer>()
                .WhereNotIn(c => c.Id, _db.GetTable<Order>(), o => o.CustomerId)
                .ToList();

            Assert.Single(result);
            Assert.Equal(3, result[0].Id);
        }

        [Fact]
        public void WhereExists_CombinesWithAndWhere()
        {
            var result = _db.GetTable<Customer>()
                .AndWhere(c => c.Name == "Alice")
                .WhereExists(_db.GetTable<Order>(), (c, o) => o.CustomerId == c.Id)
                .ToList();

            Assert.Single(result);
            Assert.Equal("Alice", result[0].Name);
        }
    }
}
