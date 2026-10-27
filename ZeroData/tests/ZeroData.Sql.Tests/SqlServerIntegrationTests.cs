using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroData.Sql;
using ZeroData.Sql.Dialects;
using ZeroData.Sql.Mapping;
using ZeroData.Sql.Migrations;
using Microsoft.Data.SqlClient;
using Xunit;

namespace ZeroData.Sql.Tests
{
    /// <summary>
    /// Integration tests against a real SQL Server for the newer features:
    /// subqueries (EXISTS/IN), graph insert, and schema migrations.
    /// Uses dedicated table names (prefix SqInt_) to avoid colliding with other SQL Server suites.
    /// </summary>
    [Collection("SqlServer")]
    public class SqlServerIntegrationTests : IDisposable
    {
        private const string ConnectionString =
            "Server=.\\SQLEXPRESS;Database=LiteSqlTest;User Id=testing;Password=268479#Kzx;TrustServerCertificate=true";

        private readonly SqlConnection _connection;
        private readonly SqlContext _db;

        public SqlServerIntegrationTests()
        {
            using (var master = new SqlConnection(
                "Server=.\\SQLEXPRESS;Database=master;User Id=testing;Password=268479#Kzx;TrustServerCertificate=true"))
            {
                master.Open();
                master.Execute(@"IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'LiteSqlTest')
                                 BEGIN CREATE DATABASE LiteSqlTest END");
            }

            _connection = new SqlConnection(ConnectionString);
            _connection.Open();

            _connection.Execute(@"
                IF OBJECT_ID('SqInt_OrderLine','U') IS NOT NULL DROP TABLE SqInt_OrderLine;
                IF OBJECT_ID('SqInt_Order','U') IS NOT NULL DROP TABLE SqInt_Order;
                IF OBJECT_ID('SqInt_Customer','U') IS NOT NULL DROP TABLE SqInt_Customer;
                CREATE TABLE SqInt_Customer (Id INT PRIMARY KEY IDENTITY, Name NVARCHAR(100) NOT NULL);
                CREATE TABLE SqInt_Order (Id INT PRIMARY KEY IDENTITY, CustomerId INT NOT NULL, Code NVARCHAR(50), Status NVARCHAR(20));
                CREATE TABLE SqInt_OrderLine (Id INT PRIMARY KEY IDENTITY, OrderId INT NOT NULL, Sku NVARCHAR(50) NOT NULL, Qty INT NOT NULL);
            ");
            _connection.Execute(@"
                SET IDENTITY_INSERT SqInt_Customer ON;
                INSERT INTO SqInt_Customer (Id, Name) VALUES (1,'Alice'),(2,'Bob'),(3,'Carol');
                SET IDENTITY_INSERT SqInt_Customer OFF;
                INSERT INTO SqInt_Order (CustomerId, Code, Status) VALUES (1,'O1','Active'),(2,'O2','Closed');
            ");

            _db = new SqlContext(_connection);
        }

        public void Dispose()
        {
            try
            {
                _connection.Execute(@"
                    IF OBJECT_ID('SqInt_OrderLine','U') IS NOT NULL DROP TABLE SqInt_OrderLine;
                    IF OBJECT_ID('SqInt_Order','U') IS NOT NULL DROP TABLE SqInt_Order;
                    IF OBJECT_ID('SqInt_Customer','U') IS NOT NULL DROP TABLE SqInt_Customer;
                    IF OBJECT_ID('SqInt_GraphParent','U') IS NOT NULL DROP TABLE SqInt_GraphParent;
                    IF OBJECT_ID('SqInt_GraphChild','U') IS NOT NULL DROP TABLE SqInt_GraphChild;
                    IF OBJECT_ID('SqInt_MigUsers','U') IS NOT NULL DROP TABLE SqInt_MigUsers;
                    IF OBJECT_ID('__SqIntMig','U') IS NOT NULL DROP TABLE __SqIntMig;");
            }
            catch { /* best effort cleanup */ }
            _db?.Dispose();
            _connection?.Dispose();
        }

        [Table(Name = "SqInt_Customer")]
        public class Customer
        {
            [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)] public int Id { get; set; }
            [Column(Name = "Name")] public string Name { get; set; }
        }

        [Table(Name = "SqInt_Order")]
        public class Order
        {
            [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)] public int Id { get; set; }
            [Column(Name = "CustomerId")] public int CustomerId { get; set; }
            [Column(Name = "Code")] public string Code { get; set; }
            [Column(Name = "Status")] public string Status { get; set; }
        }

        [Table(Name = "SqInt_OrderLine")]
        public class OrderLine
        {
            [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)] public int Id { get; set; }
            [Column(Name = "OrderId")] public int OrderId { get; set; }
            [Column(Name = "Sku")] public string Sku { get; set; }
            [Column(Name = "Qty")] public int Qty { get; set; }
        }

        // ---------- Subqueries ----------

        [Fact]
        public void WhereExists_OnSqlServer()
        {
            var result = _db.GetTable<Customer>()
                .WhereExists(_db.GetTable<Order>(), (c, o) => o.CustomerId == c.Id)
                .ToList();
            Assert.Equal(2, result.Count);
            Assert.DoesNotContain(result, c => c.Name == "Carol");
        }

        [Fact]
        public void WhereIn_WithFilter_OnSqlServer()
        {
            var result = _db.GetTable<Customer>()
                .WhereIn(c => c.Id, _db.GetTable<Order>(), o => o.CustomerId, o => o.Status == "Active")
                .ToList();
            Assert.Single(result);
            Assert.Equal("Alice", result[0].Name);
        }

        // ---------- Multi-table join (3 tables) ----------

        [Fact]
        public void MultiJoin_ThreeTables_OnSqlServer()
        {
            _connection.Execute(@"
                IF OBJECT_ID('SqInt_OrderLine','U') IS NOT NULL DROP TABLE SqInt_OrderLine;
                CREATE TABLE SqInt_OrderLine (Id INT PRIMARY KEY IDENTITY, OrderId INT NOT NULL, Sku NVARCHAR(50) NOT NULL, Qty INT NOT NULL);
                INSERT INTO SqInt_OrderLine (OrderId, Sku, Qty) VALUES (1,'A',2),(1,'B',1);");

            var rows = _db.GetTable<Order>()
                .JoinMany(_db.GetTable<Customer>(), (o, c) => o.CustomerId == c.Id)
                .Join(_db.GetTable<OrderLine>(), (o, c, l) => l.OrderId == o.Id)
                .Where((o, c, l) => o.Status == "Active")
                .Select((o, c, l) => new { o.Id, Customer = c.Name, l.Sku, l.Qty });

            Assert.Equal(2, rows.Count); // O1 (Active, Alice) has 2 lines
            Assert.All(rows, r => Assert.Equal("Alice", r.Customer));
        }

        // ---------- Graph insert ----------

        [Table(Name = "SqInt_GraphParent")]
        public class GParent
        {
            [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)] public long Id { get; set; }
            [Column(Name = "Title")] public string Title { get; set; }
            [Association(ThisKey = "Id", OtherKey = "ParentId")]
            public List<GChild> Children { get; set; } = new List<GChild>();
        }

        [Table(Name = "SqInt_GraphChild")]
        public class GChild
        {
            [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)] public long Id { get; set; }
            [Column(Name = "ParentId")] public long ParentId { get; set; }
            [Column(Name = "Note")] public string Note { get; set; }
        }

        [Fact]
        public void InsertGraph_OnSqlServer()
        {
            _connection.Execute(@"
                IF OBJECT_ID('SqInt_GraphChild','U') IS NOT NULL DROP TABLE SqInt_GraphChild;
                IF OBJECT_ID('SqInt_GraphParent','U') IS NOT NULL DROP TABLE SqInt_GraphParent;
                CREATE TABLE SqInt_GraphParent (Id BIGINT PRIMARY KEY IDENTITY, Title NVARCHAR(100));
                CREATE TABLE SqInt_GraphChild (Id BIGINT PRIMARY KEY IDENTITY, ParentId BIGINT NOT NULL, Note NVARCHAR(100));");

            var parent = new GParent
            {
                Title = "P1",
                Children = { new GChild { Note = "c1" }, new GChild { Note = "c2" } }
            };

            _db.InsertGraph(parent);

            Assert.True(parent.Id > 0);
            Assert.All(parent.Children, c => Assert.Equal(parent.Id, c.ParentId));
            var childCount = _connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM SqInt_GraphChild WHERE ParentId = @id", new { id = parent.Id });
            Assert.Equal(2, childCount);
        }

        // ---------- Migration ----------

        private class CreateMigUsers : Migration
        {
            public override string Id => "0001_SqIntCreateUsers";
            public override void Up(SchemaBuilder s) => s.CreateTable("SqInt_MigUsers", t =>
            {
                t.Int("Id").Identity().PrimaryKey();
                t.String("Username", 100).NotNull();
            });
            public override void Down(SchemaBuilder s) => s.DropTable("SqInt_MigUsers");
        }

        [Fact]
        public void Migration_OnSqlServer_CreatesTableAndTracksHistory()
        {
            _connection.Execute(@"IF OBJECT_ID('SqInt_MigUsers','U') IS NOT NULL DROP TABLE SqInt_MigUsers;
                                  IF OBJECT_ID('__SqIntMig','U') IS NOT NULL DROP TABLE __SqIntMig;");

            var runner = new MigrationRunner(_connection, new SqlServerDialect(), "__SqIntMig");
            var applied = runner.MigrateUp(new Migration[] { new CreateMigUsers() });
            Assert.Single(applied);

            _connection.Execute("INSERT INTO SqInt_MigUsers (Username) VALUES ('alice')");
            var cnt = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM SqInt_MigUsers");
            Assert.Equal(1, cnt);

            // Idempotent re-run
            var second = runner.MigrateUp(new Migration[] { new CreateMigUsers() });
            Assert.Empty(second);

            // Rollback
            Assert.True(runner.MigrateDown(new CreateMigUsers()));
            Assert.Null(_connection.ExecuteScalar<int?>("SELECT OBJECT_ID('SqInt_MigUsers','U')"));
        }
    }
}
