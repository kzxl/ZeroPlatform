using System;
using System.Linq;
using ZeroData.Sql;
using ZeroData.Sql.Dialects;
using ZeroData.Sql.Migrations;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ZeroData.Sql.Tests
{
    /// <summary>
    /// Tests for the schema migration system (SchemaBuilder + Migration + MigrationRunner).
    /// Run against SQLite; the runner is dialect-aware and also targets SQL Server.
    /// </summary>
    public class MigrationTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ISqlDialect _dialect = new SqliteDialect();

        public MigrationTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
        }

        public void Dispose() => _connection?.Dispose();

        private class CreateUsers : Migration
        {
            public override string Id => "0001_CreateUsers";
            public override void Up(SchemaBuilder s) => s.CreateTable("Users", t =>
            {
                t.Int("Id").Identity().PrimaryKey();
                t.String("Username", 100).NotNull().Unique();
                t.String("FullName", 200);
            }).CreateIndex("Users", "IX_Users_Username", true, "Username");

            public override void Down(SchemaBuilder s) =>
                s.DropIndex("Users", "IX_Users_Username").DropTable("Users");
        }

        private class AddUserStatus : Migration
        {
            public override string Id => "0002_AddUserStatus";
            public override void Up(SchemaBuilder s) =>
                s.AddColumn("Users", c => c.Named("Status", "INTEGER").NotNull().Default("1"));
            public override void Down(SchemaBuilder s) =>
                s.DropColumn("Users", "Status");
        }

        [Fact]
        public void MigrateUp_CreatesTablesAndRecordsHistory()
        {
            var runner = new MigrationRunner(_connection, _dialect);
            var applied = runner.MigrateUp(new Migration[] { new AddUserStatus(), new CreateUsers() });

            // Ordered by Id ascending → CreateUsers first
            Assert.Equal(new[] { "0001_CreateUsers", "0002_AddUserStatus" }, applied);

            // Table + column exist
            _connection.Execute("INSERT INTO Users (Username, FullName, Status) VALUES ('alice','Alice', 1)");
            var count = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Users");
            Assert.Equal(1, count);

            var history = runner.GetAppliedIds();
            Assert.Equal(2, history.Count);
        }

        [Fact]
        public void MigrateUp_IsIdempotent()
        {
            var runner = new MigrationRunner(_connection, _dialect);
            runner.MigrateUp(new Migration[] { new CreateUsers() });

            // Running again should apply nothing (already recorded).
            var second = runner.MigrateUp(new Migration[] { new CreateUsers() });
            Assert.Empty(second);
        }

        [Fact]
        public void MigrateDown_RevertsMigration()
        {
            var runner = new MigrationRunner(_connection, _dialect);
            runner.MigrateUp(new Migration[] { new CreateUsers(), new AddUserStatus() });

            var reverted = runner.MigrateDown(new AddUserStatus());
            Assert.True(reverted);

            // Status column should be gone; inserting with it must fail.
            Assert.ThrowsAny<Exception>(() =>
                _connection.Execute("INSERT INTO Users (Username, Status) VALUES ('x', 1)"));

            Assert.DoesNotContain("0002_AddUserStatus", runner.GetAppliedIds());
        }

        [Fact]
        public void SchemaBuilder_GeneratesExpectedSql()
        {
            var s = new SchemaBuilder(new SqlServerDialect());
            s.CreateTable("tbUser", t =>
            {
                t.Int("id").Identity().PrimaryKey();
                t.String("Name", 100).NotNull();
            });

            var sql = s.Statements.Single();
            Assert.Contains("CREATE TABLE [tbUser]", sql);
            Assert.Contains("[id] INT IDENTITY(1,1) NOT NULL", sql);
            Assert.Contains("[Name] NVARCHAR(100) NOT NULL", sql);
            Assert.Contains("PRIMARY KEY ([id])", sql);
        }
    }
}
