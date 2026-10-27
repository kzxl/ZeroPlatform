using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroData.Sql;
using ZeroData.Sql.Mapping;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ZeroData.Sql.Tests
{
    /// <summary>
    /// Tests for InsertGraph / InsertGraphAsync — inserting a parent with its child collections,
    /// propagating the generated parent key to each child's foreign key.
    /// </summary>
    public class GraphInsertTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly SqlContext _db;

        public GraphInsertTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _connection.Execute(@"
                CREATE TABLE Orders (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Code TEXT NOT NULL);
                CREATE TABLE OrderLines (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    OrderId INTEGER NOT NULL,
                    Sku TEXT NOT NULL,
                    Qty INTEGER NOT NULL);");
            _db = new SqlContext(_connection);
        }

        public void Dispose() { _db?.Dispose(); _connection?.Dispose(); }

        [Table(Name = "Orders")]
        public class Order
        {
            [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
            public long Id { get; set; }
            [Column(Name = "Code")]
            public string Code { get; set; }

            [Association(ThisKey = "Id", OtherKey = "OrderId")]
            public List<OrderLine> Lines { get; set; } = new List<OrderLine>();
        }

        [Table(Name = "OrderLines")]
        public class OrderLine
        {
            [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
            public long Id { get; set; }
            [Column(Name = "OrderId")]
            public long OrderId { get; set; }
            [Column(Name = "Sku")]
            public string Sku { get; set; }
            [Column(Name = "Qty")]
            public int Qty { get; set; }
        }

        [Fact]
        public void InsertGraph_InsertsParentAndChildren_WithFkPropagated()
        {
            var order = new Order
            {
                Code = "ORD-1",
                Lines =
                {
                    new OrderLine { Sku = "A", Qty = 2 },
                    new OrderLine { Sku = "B", Qty = 5 }
                }
            };

            _db.InsertGraph(order);

            Assert.True(order.Id > 0);                 // parent key generated
            Assert.All(order.Lines, l => Assert.Equal(order.Id, l.OrderId)); // FK propagated
            Assert.All(order.Lines, l => Assert.True(l.Id > 0));             // child keys generated

            var orderCount = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Orders");
            var lineCount = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM OrderLines WHERE OrderId = @id", new { id = order.Id });
            Assert.Equal(1, orderCount);
            Assert.Equal(2, lineCount);
        }

        [Fact]
        public async Task InsertGraphAsync_Works()
        {
            var order = new Order
            {
                Code = "ORD-2",
                Lines = { new OrderLine { Sku = "X", Qty = 1 } }
            };

            await _db.InsertGraphAsync(order);

            Assert.True(order.Id > 0);
            Assert.Equal(order.Id, order.Lines[0].OrderId);
            var lineCount = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM OrderLines WHERE OrderId = @id", new { id = order.Id });
            Assert.Equal(1, lineCount);
        }

        [Fact]
        public void InsertGraph_RollsBackOnError()
        {
            // Second line violates NOT NULL (Sku = null) → whole graph should roll back.
            var order = new Order
            {
                Code = "ORD-3",
                Lines =
                {
                    new OrderLine { Sku = "ok", Qty = 1 },
                    new OrderLine { Sku = null, Qty = 1 }
                }
            };

            Assert.ThrowsAny<Exception>(() => _db.InsertGraph(order));

            var orderCount = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Orders WHERE Code = 'ORD-3'");
            Assert.Equal(0, orderCount); // rolled back
        }
    }
}
