using ZeroData.Sql.Mapping;
using ZeroData.Sql.Sql;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ZeroData.Sql.Tests
{
    #region Test Entities & Enums

    public enum ItemStatus
    {
        Active,
        Inactive,
        Archived
    }

    [Table(Name = "ConvertItems")]
    public class ConvertItem
    {
        [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
        public long Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; }

        [Column(Name = "Status")]
        public string Status { get; set; }

        [Column(Name = "Value")]
        public int Value { get; set; }
    }

    [Table(Name = "InTestItems")]
    public class InTestItem
    {
        [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
        public long Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; }

        [Column(Name = "Category")]
        public string Category { get; set; }

        [Column(Name = "Value")]
        public int Value { get; set; }
    }

    #endregion

    public class Sprint8Tests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public Sprint8Tests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE ConvertItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Status TEXT NOT NULL DEFAULT 'Active',
                    Value INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE InTestItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Category TEXT NOT NULL,
                    Value INTEGER NOT NULL DEFAULT 0
                );
                INSERT INTO InTestItems (Name, Category, Value) VALUES ('A', 'Electronics', 10);
                INSERT INTO InTestItems (Name, Category, Value) VALUES ('B', 'Electronics', 20);
                INSERT INTO InTestItems (Name, Category, Value) VALUES ('C', 'Clothing', 30);
                INSERT INTO InTestItems (Name, Category, Value) VALUES ('D', 'Food', 40);
                INSERT INTO InTestItems (Name, Category, Value) VALUES ('E', 'Clothing', 50);
                INSERT INTO InTestItems (Name, Category, Value) VALUES ('F', 'Food', 60);
            ";
            cmd.ExecuteNonQuery();
        }

        #region Feature 1 — Value Converters

        [Fact]
        public void ValueConverter_RegistrationAndRetrieval()
        {
            var converters = new ValueConverterCollection();
            converters.Add<ItemStatus, string>(
                toDb: v => v.ToString(),
                fromDb: v => (ItemStatus)Enum.Parse(typeof(ItemStatus), v));

            Assert.True(converters.HasAny);
            Assert.True(converters.HasConverter(typeof(ItemStatus)));
            Assert.False(converters.HasConverter(typeof(int)));
        }

        [Fact]
        public void ValueConverter_ConvertToDb()
        {
            var converters = new ValueConverterCollection();
            converters.Add<ItemStatus, string>(
                toDb: v => v.ToString(),
                fromDb: v => (ItemStatus)Enum.Parse(typeof(ItemStatus), v));

            var result = converters.ConvertToDb(ItemStatus.Active, typeof(ItemStatus));
            Assert.Equal("Active", result);
        }

        [Fact]
        public void ValueConverter_ConvertFromDb()
        {
            var converters = new ValueConverterCollection();
            converters.Add<ItemStatus, string>(
                toDb: v => v.ToString(),
                fromDb: v => (ItemStatus)Enum.Parse(typeof(ItemStatus), v));

            var converter = converters.GetConverter(typeof(ItemStatus));
            var result = converter.FromDb("Archived");
            Assert.Equal(ItemStatus.Archived, result);
        }

        [Fact]
        public void ValueConverter_NullSafe()
        {
            var converters = new ValueConverterCollection();
            converters.Add<ItemStatus, string>(
                toDb: v => v.ToString(),
                fromDb: v => (ItemStatus)Enum.Parse(typeof(ItemStatus), v));

            var result = converters.ConvertToDb(null, typeof(ItemStatus));
            Assert.Null(result);
        }

        [Fact]
        public void ValueConverter_NoConverterReturnOriginal()
        {
            var converters = new ValueConverterCollection();
            var result = converters.ConvertToDb(42, typeof(int));
            Assert.Equal(42, result);
        }

        [Fact]
        public void ValueConverter_Remove()
        {
            var converters = new ValueConverterCollection();
            converters.Add<ItemStatus, string>(
                toDb: v => v.ToString(),
                fromDb: v => (ItemStatus)Enum.Parse(typeof(ItemStatus), v));

            Assert.True(converters.HasConverter(typeof(ItemStatus)));
            converters.Remove<ItemStatus>();
            Assert.False(converters.HasConverter(typeof(ItemStatus)));
        }

        [Fact]
        public void ValueConverter_Clear()
        {
            var converters = new ValueConverterCollection();
            converters.Add<ItemStatus, string>(
                toDb: v => v.ToString(),
                fromDb: v => (ItemStatus)Enum.Parse(typeof(ItemStatus), v));

            converters.Clear();
            Assert.False(converters.HasAny);
        }

        [Fact]
        public void ValueConverter_ExposedOnContext()
        {
            using var ctx = new SqlContext(_connection);
            Assert.NotNull(ctx.Converters);
            Assert.False(ctx.Converters.HasAny);

            ctx.Converters.Add<ItemStatus, string>(
                toDb: v => v.ToString(),
                fromDb: v => (ItemStatus)Enum.Parse(typeof(ItemStatus), v));

            Assert.True(ctx.Converters.HasAny);
        }

        #endregion

        #region Feature 2 — Contains / IN Clause

        [Fact]
        public void Contains_ListOfStrings_GeneratesIN()
        {
            using var ctx = new SqlContext(_connection);
            var categories = new List<string> { "Electronics", "Food" };

            var items = ctx.GetTable<InTestItem>().Where(x => categories.Contains(x.Category));
            Assert.Equal(4, items.Count); // A, B (Electronics) + D, F (Food)
        }

        [Fact]
        public void Contains_ArrayOfInts_GeneratesIN()
        {
            using var ctx = new SqlContext(_connection);
            var values = new List<int> { 10, 30, 50 };

            var items = ctx.GetTable<InTestItem>().Where(x => values.Contains(x.Value));
            Assert.Equal(3, items.Count); // A(10), C(30), E(50)
        }

        [Fact]
        public void Contains_EmptyList_ReturnsNothing()
        {
            using var ctx = new SqlContext(_connection);
            var empty = new List<string>();

            var items = ctx.GetTable<InTestItem>().Where(x => empty.Contains(x.Category));
            Assert.Empty(items);
        }

        [Fact]
        public void Contains_SingleItem()
        {
            using var ctx = new SqlContext(_connection);
            var single = new List<string> { "Clothing" };

            var items = ctx.GetTable<InTestItem>().Where(x => single.Contains(x.Category));
            Assert.Equal(2, items.Count); // C, E
        }

        [Fact]
        public void Contains_CombinedWithOtherConditions()
        {
            using var ctx = new SqlContext(_connection);
            var cats = new List<string> { "Electronics", "Clothing" };

            var items = ctx.GetTable<InTestItem>()
                .Where(x => cats.Contains(x.Category) && x.Value > 20);
            Assert.Equal(2, items.Count); // C(30,Clothing), E(50,Clothing)
        }

        [Fact]
        public void StringContains_LIKE()
        {
            using var ctx = new SqlContext(_connection);
            var items = ctx.GetTable<InTestItem>()
                .Where(x => x.Category.Contains("ectro"));
            Assert.Equal(2, items.Count); // Electronics items
        }

        [Fact]
        public async Task Contains_Async()
        {
            using var ctx = new SqlContext(_connection);
            var values = new List<int> { 40, 60 };

            var items = await ctx.GetTable<InTestItem>()
                .WhereAsync(x => values.Contains(x.Value));
            Assert.Equal(2, items.Count); // D(40), F(60)
        }

        #endregion

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
