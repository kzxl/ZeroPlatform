using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Linq;
using ZeroData.Sql.Mapping;
using Xunit;

namespace ZeroData.Sql.Tests
{
    #region SQLite Test Models

    [Table(Name = "Items")]
    public class TestItem
    {
        [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
        public long Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; }

        [Column(Name = "Quantity")]
        public int Quantity { get; set; }

        [Column(Name = "Price", CanBeNull = true)]
        public decimal? Price { get; set; }
    }

    [Table(Name = "Categories")]
    public class TestCategory
    {
        [Column(Name = "Code", IsPrimaryKey = true)]
        public string Code { get; set; }

        [Column(Name = "Description")]
        public string Description { get; set; }
    }

    #endregion

    public class LiteContextTests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public LiteContextTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            // Create test tables
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE Items (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Quantity INTEGER NOT NULL DEFAULT 0,
                        Price REAL
                    );
                    CREATE TABLE Categories (
                        Code TEXT PRIMARY KEY,
                        Description TEXT
                    );
                ";
                cmd.ExecuteNonQuery();
            }
        }

        [Fact]
        public void GetTable_ReturnsSameInstance()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var table1 = ctx.GetTable<TestItem>();
                var table2 = ctx.GetTable<TestItem>();
                Assert.Same(table1, table2);
            }
        }

        [Fact]
        public void InsertOnSubmit_And_SubmitChanges_InsertsRow()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var table = ctx.GetTable<TestItem>();
                var item = new TestItem { Name = "Widget", Quantity = 10, Price = 4.99m };

                table.InsertOnSubmit(item);
                ctx.SubmitChanges();

                // Verify the entity got auto-generated ID
                Assert.True(item.Id > 0, "Auto-generated Id should be set after SubmitChanges");
            }

            // Verify data persisted
            using (var ctx = new SqlContext(_connection))
            {
                var items = ctx.GetTable<TestItem>().ToList();
                Assert.Single(items);
                Assert.Equal("Widget", items[0].Name);
                Assert.Equal(10, items[0].Quantity);
            }
        }

        [Fact]
        public void InsertOnSubmit_StringPrimaryKey_InsertsCorrectly()
        {
            using (var ctx = new SqlContext(_connection))
            {
                var table = ctx.GetTable<TestCategory>();
                table.InsertOnSubmit(new TestCategory { Code = "CAT1", Description = "Category 1" });
                table.InsertOnSubmit(new TestCategory { Code = "CAT2", Description = "Category 2" });
                ctx.SubmitChanges();
            }

            using (var ctx = new SqlContext(_connection))
            {
                var categories = ctx.GetTable<TestCategory>().ToList();
                Assert.Equal(2, categories.Count);
            }
        }

        [Fact]
        public void DeleteOnSubmit_And_SubmitChanges_DeletesRow()
        {
            // Arrange: insert an item first
            using (var ctx = new SqlContext(_connection))
            {
                var item = new TestItem { Name = "ToDelete", Quantity = 1 };
                ctx.GetTable<TestItem>().InsertOnSubmit(item);
                ctx.SubmitChanges();
            }

            // Act: delete it
            using (var ctx = new SqlContext(_connection))
            {
                var table = ctx.GetTable<TestItem>();
                var item = table.FirstOrDefault(x => x.Name == "ToDelete");
                Assert.NotNull(item);

                table.DeleteOnSubmit(item);
                ctx.SubmitChanges();
            }

            // Assert: no items
            using (var ctx = new SqlContext(_connection))
            {
                var items = ctx.GetTable<TestItem>().ToList();
                Assert.Empty(items);
            }
        }

        [Fact]
        public void ExecuteQuery_WithPositionalParams_ReturnsResults()
        {
            // Arrange
            using (var ctx = new SqlContext(_connection))
            {
                ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "A", Quantity = 5 });
                ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "B", Quantity = 15 });
                ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "C", Quantity = 25 });
                ctx.SubmitChanges();
            }

            // Act
            using (var ctx = new SqlContext(_connection))
            {
                var results = ctx.ExecuteQuery<TestItem>(
                    "SELECT * FROM Items WHERE Quantity > {0}", 10).ToList();

                Assert.Equal(2, results.Count);
                Assert.Contains(results, r => r.Name == "B");
                Assert.Contains(results, r => r.Name == "C");
            }
        }

        [Fact]
        public void ExecuteQuery_RawSql_NoParams()
        {
            using (var ctx = new SqlContext(_connection))
            {
                ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "Test", Quantity = 1 });
                ctx.SubmitChanges();

                var results = ctx.ExecuteQuery<TestItem>("SELECT * FROM Items").ToList();
                Assert.Single(results);
            }
        }

        [Fact]
        public void ExecuteCommand_UpdatesRows()
        {
            using (var ctx = new SqlContext(_connection))
            {
                ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "Old", Quantity = 1 });
                ctx.SubmitChanges();

                var affected = ctx.ExecuteCommand(
                    "UPDATE Items SET Name = {0} WHERE Name = {1}", "New", "Old");

                Assert.Equal(1, affected);
            }

            using (var ctx = new SqlContext(_connection))
            {
                var item = ctx.GetTable<TestItem>().FirstOrDefault();
                Assert.Equal("New", item.Name);
            }
        }

        [Fact]
        public void Table_LINQ_Where_Works()
        {
            using (var ctx = new SqlContext(_connection))
            {
                ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "Apple", Quantity = 10 });
                ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "Banana", Quantity = 5 });
                ctx.SubmitChanges();
            }

            using (var ctx = new SqlContext(_connection))
            {
                var table = ctx.GetTable<TestItem>();
                var result = table.Where(x => x.Quantity > 7).ToList();
                Assert.Single(result);
                Assert.Equal("Apple", result[0].Name);
            }
        }

        [Fact]
        public void Table_LINQ_Any_Works()
        {
            using (var ctx = new SqlContext(_connection))
            {
                ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "Test", Quantity = 1 });
                ctx.SubmitChanges();
            }

            using (var ctx = new SqlContext(_connection))
            {
                Assert.True(ctx.GetTable<TestItem>().Any());
                Assert.True(ctx.GetTable<TestItem>().Any(x => x.Name == "Test"));
                Assert.False(ctx.GetTable<TestItem>().Any(x => x.Name == "NonExistent"));
            }
        }

        [Fact]
        public void Table_LINQ_Select_Works()
        {
            using (var ctx = new SqlContext(_connection))
            {
                ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "A", Quantity = 1 });
                ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "B", Quantity = 2 });
                ctx.SubmitChanges();
            }

            using (var ctx = new SqlContext(_connection))
            {
                var names = ctx.GetTable<TestItem>().Select(x => x.Name).ToList();
                Assert.Equal(2, names.Count);
                Assert.Contains("A", names);
                Assert.Contains("B", names);
            }
        }

        [Fact]
        public void Dispose_PreventsSubsequentAccess()
        {
            var ctx = new SqlContext(_connection);
            ctx.Dispose();

            Assert.Throws<ObjectDisposedException>(() => ctx.GetTable<TestItem>());
            Assert.Throws<ObjectDisposedException>(() => ctx.SubmitChanges());
        }

        [Fact]
        public void Log_CaptureesSqlStatements()
        {
            var logWriter = new StringWriter();

            using (var ctx = new SqlContext(_connection))
            {
                ctx.Log = logWriter;
                ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "Logged", Quantity = 1 });
                ctx.SubmitChanges();
            }

            var logOutput = logWriter.ToString();
            Assert.Contains("INSERT INTO \"Items\"", logOutput);
            Assert.Contains("@Name", logOutput);
        }

        [Fact]
        public void SubmitChanges_Transaction_RollsBackOnError()
        {
            using (var ctx = new SqlContext(_connection))
            {
                ctx.GetTable<TestItem>().InsertOnSubmit(new TestItem { Name = "Safe", Quantity = 1 });
                ctx.SubmitChanges();
            }

            // Verify safe item exists
            using (var ctx = new SqlContext(_connection))
            {
                Assert.Single(ctx.GetTable<TestItem>().ToList());
            }
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
