using System;
using System.Data;
using ZeroData.Sql.Dialects;
using ZeroData.Sql.Mapping;
using ZeroData.Sql.Providers;
using ZeroData.Sql.Sql;
using Xunit;

namespace ZeroData.Sql.Tests
{
    // Fake DbConnection implementations for testing factory and dialect resolution
    public class MockOracleConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 30;
        public string Database => "ORCL";
        public ConnectionState State => ConnectionState.Open;

        public IDbTransaction BeginTransaction() => null!;
        public IDbTransaction BeginTransaction(IsolationLevel il) => null!;
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => null!;
        public void Dispose() { }
        public void Open() { }
    }

    public class MockFirebirdConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 30;
        public string Database => "TEST.FDB";
        public ConnectionState State => ConnectionState.Open;

        public IDbTransaction BeginTransaction() => null!;
        public IDbTransaction BeginTransaction(IsolationLevel il) => null!;
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => null!;
        public void Dispose() { }
        public void Open() { }
    }

    public class MockCustomDuckDbConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 30;
        public string Database => "MEMORY";
        public ConnectionState State => ConnectionState.Open;

        public IDbTransaction BeginTransaction() => null!;
        public IDbTransaction BeginTransaction(IsolationLevel il) => null!;
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => null!;
        public void Dispose() { }
        public void Open() { }
    }

    [Table(Name = "Users")]
    public class MdcUser
    {
        [Column(Name = "Id", IsPrimaryKey = true)]
        public int Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [Column(Name = "IsActive")]
        public bool IsActive { get; set; }
    }

    public class MultiDatabaseConnectionTests
    {
        [Fact]
        public void SqlDialectFactory_ResolvesNewDialects()
        {
            using var oracleConn = new MockOracleConnection();
            var oracleDialect = SqlDialectFactory.GetDialect(oracleConn);
            Assert.IsType<OracleDialect>(oracleDialect);
            Assert.Equal("Oracle", oracleDialect.ProviderName);

            using var fbConn = new MockFirebirdConnection();
            var fbDialect = SqlDialectFactory.GetDialect(fbConn);
            Assert.IsType<FirebirdDialect>(fbDialect);
            Assert.Equal("Firebird", fbDialect.ProviderName);

            // Unknown connection falls back to AnsiSqlDialect
            using var unknownConn = new MockCustomDuckDbConnection();
            var ansiDialect = SqlDialectFactory.GetDialect(unknownConn);
            Assert.IsType<AnsiSqlDialect>(ansiDialect);
            Assert.Equal("ANSI SQL", ansiDialect.ProviderName);
        }

        [Fact]
        public void SqlDialectFactory_CanRegisterCustomDialect()
        {
            var customDialect = new AnsiSqlDialect();
            SqlDialectFactory.RegisterDialect<MockCustomDuckDbConnection>(customDialect);

            using var conn = new MockCustomDuckDbConnection();
            var resolved = SqlDialectFactory.GetDialect(conn);
            Assert.Same(customDialect, resolved);
        }

        [Fact]
        public void DbProviderRegistry_DetectsProviderHeuristically()
        {
            // SQL Server
            Assert.Equal(DbProviderRegistry.SqlServer, DbProviderRegistry.DetectProvider("Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;"));
            Assert.Equal(DbProviderRegistry.SqlServer, DbProviderRegistry.DetectProvider("Data Source=.\\sqlexpress;Initial Catalog=TestDb;Integrated Security=SSPI;"));

            // SQLite
            Assert.Equal(DbProviderRegistry.Sqlite, DbProviderRegistry.DetectProvider("Data Source=mydb.sqlite;Version=3;"));
            Assert.Equal(DbProviderRegistry.Sqlite, DbProviderRegistry.DetectProvider("Data Source=:memory:;"));

            // PostgreSQL
            Assert.Equal(DbProviderRegistry.PostgreSql, DbProviderRegistry.DetectProvider("Host=localhost;Database=test;Username=postgres;Password=secret;"));
            Assert.Equal(DbProviderRegistry.PostgreSql, DbProviderRegistry.DetectProvider("Server=127.0.0.1;Port=5432;Database=users;"));

            // MySQL
            Assert.Equal(DbProviderRegistry.MySql, DbProviderRegistry.DetectProvider("Server=myServerAddress;Database=myDataBase;Uid=myUsername;Pwd=myPassword;"));

            // Oracle
            Assert.Equal(DbProviderRegistry.Oracle, DbProviderRegistry.DetectProvider("Data Source=ORCL;User Id=hr;Password=welcome;"));

            // Firebird
            Assert.Equal(DbProviderRegistry.Firebird, DbProviderRegistry.DetectProvider("Database=localhost:C:\\db\\test.fdb;User=SYSDBA;Password=masterkey"));
        }

        [Fact]
        public void DbProviderRegistry_CanRegisterAndCreateConnection()
        {
            DbProviderRegistry.RegisterProvider("MockOracle", cs => new MockOracleConnection { ConnectionString = cs }, "ora", "oracle");

            var conn = DbProviderRegistry.CreateConnection("Data Source=ORCL;", "ora");
            Assert.NotNull(conn);
            Assert.IsType<MockOracleConnection>(conn);
            Assert.Equal("Data Source=ORCL;", conn.ConnectionString);
        }

        [Fact]
        public void SqlContext_CreateHelpersConfigureDialectCorrectly()
        {
            // Register providers for connection instantiation
            DbProviderRegistry.Register(DbProviderRegistry.SqlServer, cs => new MockOracleConnection { ConnectionString = cs });
            DbProviderRegistry.Register(DbProviderRegistry.Sqlite, cs => new MockOracleConnection { ConnectionString = cs });
            DbProviderRegistry.Register(DbProviderRegistry.PostgreSql, cs => new MockOracleConnection { ConnectionString = cs });
            DbProviderRegistry.Register(DbProviderRegistry.MySql, cs => new MockOracleConnection { ConnectionString = cs });
            DbProviderRegistry.Register(DbProviderRegistry.Oracle, cs => new MockOracleConnection { ConnectionString = cs });
            DbProviderRegistry.Register(DbProviderRegistry.Firebird, cs => new MockOracleConnection { ConnectionString = cs });
            DbProviderRegistry.Register(DbProviderRegistry.Ansi, cs => new MockOracleConnection { ConnectionString = cs });

            var sqlServerCtx = SqlContext.CreateSqlServer("Server=test;Database=db;");
            Assert.IsType<SqlServerDialect>(sqlServerCtx.Dialect);

            var sqliteCtx = SqlContext.CreateSqlite("Data Source=test.db;");
            Assert.IsType<SqliteDialect>(sqliteCtx.Dialect);

            var pgCtx = SqlContext.CreatePostgreSql("Host=localhost;Database=db;");
            Assert.IsType<PostgreSqlDialect>(pgCtx.Dialect);

            var myCtx = SqlContext.CreateMySql("Server=localhost;Database=db;");
            Assert.IsType<MySqlDialect>(myCtx.Dialect);

            var oraCtx = SqlContext.CreateOracle("Data Source=ORCL;");
            Assert.IsType<OracleDialect>(oraCtx.Dialect);

            var fbCtx = SqlContext.CreateFirebird("Database=test.fdb;");
            Assert.IsType<FirebirdDialect>(fbCtx.Dialect);

            var ansiCtx = SqlContext.Create("Data Source=unknown;", "ANSI");
            Assert.IsType<AnsiSqlDialect>(ansiCtx.Dialect);
        }

        [Fact]
        public void WhereBuilder_NormalizesBooleanPredicates_ForSqlServerAndPostgreSql()
        {
            var mapping = MappingCache.GetMapping<MdcUser>();

            // SQL Server: booleans must normalize to [Col] = 1 or NOT ([Col] = 1)
            var sqlServerWb = new WhereBuilder(mapping, new SqlServerDialect());

            var (sql1, _) = sqlServerWb.Build<MdcUser>(u => u.IsActive);
            Assert.Equal("[IsActive] = 1", sql1);

            var (sql2, _) = sqlServerWb.Build<MdcUser>(u => !u.IsActive);
            Assert.Equal("NOT ([IsActive] = 1)", sql2);

            var (sql3, _) = sqlServerWb.Build<MdcUser>(u => u.IsActive && u.Name == "Alice");
            Assert.Contains("[IsActive] = 1", sql3);
            Assert.Contains("AND", sql3);
            Assert.Contains("[Name] = @w", sql3);

            // PostgreSQL: booleans normalize to "Col" = TRUE or NOT ("Col" = TRUE)
            var pgWb = new WhereBuilder(mapping, new PostgreSqlDialect());

            var (pgSql1, _) = pgWb.Build<MdcUser>(u => u.IsActive);
            Assert.Equal("\"IsActive\" = TRUE", pgSql1);

            var (pgSql2, _) = pgWb.Build<MdcUser>(u => !u.IsActive);
            Assert.Equal("NOT (\"IsActive\" = TRUE)", pgSql2);
        }

        [Fact]
        public void WhereBuilder_TranslatesStringFunctionsCorrectly()
        {
            var mapping = MappingCache.GetMapping<MdcUser>();
            var sqlServerWb = new WhereBuilder(mapping, new SqlServerDialect());
            var pgWb = new WhereBuilder(mapping, new PostgreSqlDialect());

            // IsNullOrEmpty
            var (emptySql, _) = sqlServerWb.Build<MdcUser>(u => string.IsNullOrEmpty(u.Name));
            Assert.Equal("([Name] IS NULL OR [Name] = '')", emptySql);

            // IsNullOrWhiteSpace
            var (wsSql, _) = sqlServerWb.Build<MdcUser>(u => string.IsNullOrWhiteSpace(u.Name));
            Assert.Equal("([Name] IS NULL OR TRIM([Name]) = '')", wsSql);

            // ToLower, ToUpper, Trim
            var (lowerSql, _) = sqlServerWb.Build<MdcUser>(u => u.Name.ToLower() == "alice");
            Assert.Equal("LOWER([Name]) = @w0", lowerSql);

            var (upperSql, _) = sqlServerWb.Build<MdcUser>(u => u.Name.ToUpper() == "ALICE");
            Assert.Equal("UPPER([Name]) = @w0", upperSql);

            var (trimSql, _) = sqlServerWb.Build<MdcUser>(u => u.Name.Trim() == "alice");
            Assert.Equal("TRIM([Name]) = @w0", trimSql);

            // Length: LEN on SQL Server vs LENGTH on Postgres
            var (lenSql, _) = sqlServerWb.Build<MdcUser>(u => u.Name.Length > 5);
            Assert.Equal("LEN([Name]) > @w0", lenSql);

            var (pgLenSql, _) = pgWb.Build<MdcUser>(u => u.Name.Length > 5);
            Assert.Equal("LENGTH(\"Name\") > @w0", pgLenSql);
        }
    }
}
