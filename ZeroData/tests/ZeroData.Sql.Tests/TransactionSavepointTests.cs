using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using ZeroData.Sql.Dialects;
using ZeroData.Sql.Mapping;

namespace ZeroData.Sql.Tests
{
    [Table(Name = "SavepointItems")]
    public class SavepointItem
    {
        [Column(Name = "Id", IsPrimaryKey = true)]
        public int Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; }
    }

    public class TransactionSavepointTests
    {
        [Fact]
        public void Dialects_SavepointSql_GeneratesExpectedSyntax()
        {
            var sqlServer = new SqlServerDialect();
            Assert.True(sqlServer.SupportsSavepoints);
            Assert.Equal("SAVE TRANSACTION [sp1];", sqlServer.GetCreateSavepointSql("sp1"));
            Assert.Equal("ROLLBACK TRANSACTION [sp1];", sqlServer.GetRollbackSavepointSql("sp1"));
            Assert.Null(sqlServer.GetReleaseSavepointSql("sp1"));

            var sqlite = new SqliteDialect();
            Assert.True(sqlite.SupportsSavepoints);
            Assert.Equal("SAVEPOINT \"sp1\";", sqlite.GetCreateSavepointSql("sp1"));
            Assert.Equal("ROLLBACK TO SAVEPOINT \"sp1\";", sqlite.GetRollbackSavepointSql("sp1"));
            Assert.Equal("RELEASE SAVEPOINT \"sp1\";", sqlite.GetReleaseSavepointSql("sp1"));

            var postgres = new PostgreSqlDialect();
            Assert.True(postgres.SupportsSavepoints);
            Assert.Equal("SAVEPOINT \"sp1\";", postgres.GetCreateSavepointSql("sp1"));
            Assert.Equal("ROLLBACK TO SAVEPOINT \"sp1\";", postgres.GetRollbackSavepointSql("sp1"));
            Assert.Equal("RELEASE SAVEPOINT \"sp1\";", postgres.GetReleaseSavepointSql("sp1"));

            var mysql = new MySqlDialect();
            Assert.True(mysql.SupportsSavepoints);
            Assert.Equal("SAVEPOINT `sp1`;", mysql.GetCreateSavepointSql("sp1"));
            Assert.Equal("ROLLBACK TO SAVEPOINT `sp1`;", mysql.GetRollbackSavepointSql("sp1"));
            Assert.Equal("RELEASE SAVEPOINT `sp1`;", mysql.GetReleaseSavepointSql("sp1"));
        }

        [Fact]
        public void SqlContext_Savepoint_WithoutActiveTransaction_ThrowsInvalidOperationException()
        {
            using var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            using var db = new SqlContext(conn);

            Assert.Throws<InvalidOperationException>(() => db.CreateSavepoint("sp1"));
            Assert.Throws<InvalidOperationException>(() => db.RollbackToSavepoint("sp1"));
            Assert.Throws<InvalidOperationException>(() => db.ReleaseSavepoint("sp1"));
        }

        [Fact]
        public void SqlContext_Savepoint_InvalidName_ThrowsArgumentException()
        {
            using var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            using var db = new SqlContext(conn);
            db.BeginTransaction();

            Assert.Throws<ArgumentException>(() => db.CreateSavepoint(""));
            Assert.Throws<ArgumentException>(() => db.RollbackToSavepoint("   "));
            Assert.Throws<ArgumentException>(() => db.ReleaseSavepoint(null));

            db.RollbackTransaction();
        }

        [Fact]
        public void SqlContext_Savepoint_SqliteEndToEnd_RollbackToSavepoint_PreservesPriorInserts()
        {
            using var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "CREATE TABLE SavepointItems (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);";
                cmd.ExecuteNonQuery();
            }

            using var db = new SqlContext(conn);

            db.BeginTransaction();

            // Insert 1st item
            db.Insert(new SavepointItem { Id = 1, Name = "Item1" });
            db.SubmitChanges();

            // Create Savepoint
            db.CreateSavepoint("checkpoint1");

            // Insert 2nd item that will be rolled back
            db.Insert(new SavepointItem { Id = 2, Name = "Item2" });
            db.SubmitChanges();

            // Rollback to checkpoint1
            db.RollbackToSavepoint("checkpoint1");

            // Insert 3rd item after rollback
            db.Insert(new SavepointItem { Id = 3, Name = "Item3" });
            db.SubmitChanges();

            // Release savepoint if supported
            db.ReleaseSavepoint("checkpoint1");

            // Commit outer transaction
            db.CommitTransaction();

            // Verify final database state
            var items = db.GetTable<SavepointItem>().OrderBy(x => x.Id).ToList();

            Assert.Equal(2, items.Count);
            Assert.Equal(1, items[0].Id);
            Assert.Equal("Item1", items[0].Name);
            Assert.Equal(3, items[1].Id);
            Assert.Equal("Item3", items[1].Name);
        }

        [Fact]
        public async Task SqlContext_SavepointAsync_SqliteEndToEnd_RollbackToSavepointAsync()
        {
            using var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "CREATE TABLE SavepointItems (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);";
                cmd.ExecuteNonQuery();
            }

            using var db = new SqlContext(conn);

            db.BeginTransaction();

            db.Insert(new SavepointItem { Id = 10, Name = "AsyncItem1" });
            await db.SubmitChangesAsync();

            await db.CreateSavepointAsync("async_sp1");

            db.Insert(new SavepointItem { Id = 20, Name = "AsyncItem2" });
            await db.SubmitChangesAsync();

            await db.RollbackToSavepointAsync("async_sp1");

            db.Insert(new SavepointItem { Id = 30, Name = "AsyncItem3" });
            await db.SubmitChangesAsync();

            await db.ReleaseSavepointAsync("async_sp1");

            db.CommitTransaction();

            var items = await db.GetTable<SavepointItem>().OrderBy(x => x.Id).ToListAsync();
            Assert.Equal(2, items.Count);
            Assert.Equal(10, items[0].Id);
            Assert.Equal(30, items[1].Id);
        }
    }
}
