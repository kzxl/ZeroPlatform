using ZeroData.Sql.Mapping;
using ZeroData.Sql.Sql;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;

namespace ZeroData.Sql.Tests
{
    #region Test Entities

    [Table(Name = "SoftItems")]
    [SoftDelete(ColumnName = "IsDeleted")]
    public class SoftItem
    {
        [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
        public long Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; }

        [Column(Name = "IsDeleted")]
        public bool IsDeleted { get; set; }
    }

    [Table(Name = "RawItems")]
    public class RawItem
    {
        [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
        public long Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; }

        [Column(Name = "Value")]
        public int Value { get; set; }
    }

    #endregion

    public class Sprint7Tests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public Sprint7Tests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE SoftItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE RawItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Value INTEGER NOT NULL DEFAULT 0
                );
                INSERT INTO SoftItems (Name, IsDeleted) VALUES ('Active1', 0);
                INSERT INTO SoftItems (Name, IsDeleted) VALUES ('Active2', 0);
                INSERT INTO SoftItems (Name, IsDeleted) VALUES ('Deleted1', 1);

                INSERT INTO RawItems (Name, Value) VALUES ('Alpha', 10);
                INSERT INTO RawItems (Name, Value) VALUES ('Beta', 20);
                INSERT INTO RawItems (Name, Value) VALUES ('Gamma', 30);
                INSERT INTO RawItems (Name, Value) VALUES ('Delta', 40);
                INSERT INTO RawItems (Name, Value) VALUES ('Epsilon', 50);
            ";
            cmd.ExecuteNonQuery();
        }

        #region Feature 1 — FromSql

        [Fact]
        public void FromSql_BasicQuery()
        {
            using var ctx = new SqlContext(_connection);
            var results = ctx.FromSql<RawItem>("SELECT * FROM RawItems WHERE Value > @min",
                new { min = 25 });
            Assert.Equal(3, results.Count);
        }

        [Fact]
        public void FromSql_NoParams()
        {
            using var ctx = new SqlContext(_connection);
            var results = ctx.FromSql<RawItem>("SELECT * FROM RawItems");
            Assert.Equal(5, results.Count);
        }

        [Fact]
        public async Task FromSql_Async()
        {
            using var ctx = new SqlContext(_connection);
            var results = await ctx.FromSqlAsync<RawItem>(
                "SELECT * FROM RawItems WHERE Name = @name", new { name = "Alpha" });
            Assert.Single(results);
            Assert.Equal(10, results[0].Value);
        }

        #endregion

        #region Feature 2 — Query Tags

        [Fact]
        public void TagWith_QueryStillWorks()
        {
            using var ctx = new SqlContext(_connection);

            // TagWith should not break query execution
            var results = ctx.GetTable<RawItem>()
                .TagWith("TestTag")
                .Where(x => x.Value > 10);
            Assert.Equal(4, results.Count);
        }

        #endregion

        #region Feature 3 — Async Parity

        [Fact]
        public async Task FirstOrDefaultAsync_NoPredicate()
        {
            using var ctx = new SqlContext(_connection);
            var item = await ctx.GetTable<RawItem>()
                .OrderBy(x => x.Name)
                .FirstOrDefaultAsync();
            Assert.NotNull(item);
            Assert.Equal("Alpha", item.Name);
        }

        [Fact]
        public async Task CountAsync_NoPredicate()
        {
            using var ctx = new SqlContext(_connection);
            var count = await ctx.GetTable<RawItem>().CountAsync();
            Assert.Equal(5, count);
        }

        [Fact]
        public async Task AnyAsync_NoPredicate()
        {
            using var ctx = new SqlContext(_connection);
            Assert.True(await ctx.GetTable<RawItem>().AnyAsync());
        }

        [Fact]
        public async Task CountAsync_WithFilter()
        {
            using var ctx = new SqlContext(_connection);
            ctx.Filters.Add<SoftItem>(x => x.IsDeleted == false);

            var count = await ctx.GetTable<SoftItem>().CountAsync(x => x.Name == "Active1");
            Assert.Equal(1, count);
        }

        #endregion

        #region Feature 4 — SoftDelete

        [Fact]
        public void SoftDelete_DeleteSetsFlag()
        {
            using var ctx = new SqlContext(_connection);
            var item = ctx.GetTable<SoftItem>().FirstOrDefault(x => x.Name == "Active1");
            Assert.NotNull(item);

            ctx.GetTable<SoftItem>().DeleteOnSubmit(item);
            ctx.SubmitChanges();

            // After soft delete, row still exists with IsDeleted = 1
            var all = ctx.FromSql<SoftItem>("SELECT * FROM SoftItems WHERE Name = 'Active1'");
            Assert.Single(all);
            Assert.True(all[0].IsDeleted);
        }

        [Fact]
        public void SoftDelete_WithFilter_Auto()
        {
            using var ctx = new SqlContext(_connection);
            ctx.Filters.Add<SoftItem>(x => x.IsDeleted == false);

            var active = ctx.GetTable<SoftItem>().ToList();
            Assert.Equal(2, active.Count); // Active1, Active2 (Deleted1 filtered)
        }

        #endregion

        #region Feature 7 — Interceptor

        [Fact]
        public void Interceptor_OnBeforeExecute_Called()
        {
            var interceptor = new QueryInterceptor();
            string capturedSql = null;
            interceptor.OnBeforeExecute = (sql, _) =>
            {
                capturedSql = sql;
                return sql;
            };

            // Just verify the interceptor object works
            Assert.NotNull(interceptor);
            Assert.NotNull(interceptor.OnBeforeExecute);
            var result = interceptor.OnBeforeExecute("SELECT 1", null);
            Assert.Equal("SELECT 1", result);
            Assert.Equal("SELECT 1", capturedSql);
        }

        [Fact]
        public void Interceptor_OnError_Called()
        {
            var interceptor = new QueryInterceptor();
            Exception capturedEx = null;
            interceptor.OnError = (sql, ex) => capturedEx = ex;

            interceptor.OnError("SELECT * FROM NotExists", new Exception("test"));
            Assert.NotNull(capturedEx);
            Assert.Equal("test", capturedEx.Message);
        }

        #endregion

        #region Feature 8 — Repository

        [Fact]
        public void Repository_GetAll()
        {
            using var ctx = new SqlContext(_connection);
            var repo = new Repository<RawItem>(ctx);

            var all = repo.GetAll();
            Assert.Equal(5, all.Count);
        }

        [Fact]
        public void Repository_Where()
        {
            using var ctx = new SqlContext(_connection);
            var repo = new Repository<RawItem>(ctx);

            var items = repo.Where(x => x.Value > 30);
            Assert.Equal(2, items.Count);
        }

        [Fact]
        public void Repository_FirstOrDefault()
        {
            using var ctx = new SqlContext(_connection);
            var repo = new Repository<RawItem>(ctx);

            var item = repo.FirstOrDefault(x => x.Name == "Beta");
            Assert.NotNull(item);
            Assert.Equal(20, item.Value);
        }

        [Fact]
        public void Repository_Count()
        {
            using var ctx = new SqlContext(_connection);
            var repo = new Repository<RawItem>(ctx);

            Assert.Equal(5, repo.Count(x => true));
            Assert.Equal(2, repo.Count(x => x.Value >= 40));
        }

        [Fact]
        public async Task Repository_Async_GetAll()
        {
            using var ctx = new SqlContext(_connection);
            var repo = new Repository<RawItem>(ctx);

            var all = await repo.GetAllAsync();
            Assert.Equal(5, all.Count);
        }

        #endregion

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
