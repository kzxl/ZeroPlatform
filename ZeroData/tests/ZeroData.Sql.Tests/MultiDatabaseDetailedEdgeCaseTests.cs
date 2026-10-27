using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using ZeroData.Sql.Dialects;
using ZeroData.Sql.Mapping;
using ZeroData.Sql.Migrations;
using ZeroData.Sql.Providers;
using ZeroData.Sql.Sql;
using Xunit;

namespace ZeroData.Sql.Tests
{
    [Table(Name = "ComplexEntities")]
    public class ComplexEntity
    {
        [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
        public int Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [Column(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Column(Name = "Price")]
        public decimal Price { get; set; }

        [Column(Name = "Score")]
        public int Score { get; set; }

        [Column(Name = "IsActive")]
        public bool IsActive { get; set; }

        [Column(Name = "IsApproved")]
        public bool? IsApproved { get; set; }

        [Column(Name = "OptionalDate")]
        public DateTime? OptionalDate { get; set; }
    }

    [Table(Name = "SoftDeletedEntities")]
    [SoftDelete(ColumnName = "IsDeleted")]
    public class SoftDeletedEntity
    {
        [Column(Name = "Id", IsPrimaryKey = true)]
        public int Id { get; set; }

        [Column(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        [Column(Name = "IsDeleted")]
        public bool IsDeleted { get; set; }
    }

    public class MultiDatabaseDetailedEdgeCaseTests
    {
        private readonly EntityMapping _mapping;
        private readonly SqlServerDialect _sqlServer;
        private readonly PostgreSqlDialect _postgres;
        private readonly SqliteDialect _sqlite;
        private readonly MySqlDialect _mysql;
        private readonly OracleDialect _oracle;
        private readonly FirebirdDialect _firebird;
        private readonly AnsiSqlDialect _ansi;

        public MultiDatabaseDetailedEdgeCaseTests()
        {
            _mapping = MappingCache.GetMapping<ComplexEntity>();
            _sqlServer = new SqlServerDialect();
            _postgres = new PostgreSqlDialect();
            _sqlite = new SqliteDialect();
            _mysql = new MySqlDialect();
            _oracle = new OracleDialect();
            _firebird = new FirebirdDialect();
            _ansi = new AnsiSqlDialect();
        }

        #region 1. WhereBuilder Boolean Edge Cases

        [Fact]
        public void WhereBuilder_BooleanPredicates_AcrossAllDialects()
        {
            var dialects = new ISqlDialect[] { _sqlServer, _sqlite, _mysql, _oracle, _firebird, _ansi };

            foreach (var dialect in dialects)
            {
                var wb = new WhereBuilder(_mapping, dialect);

                // Standalone boolean
                var (sql1, _) = wb.Build<ComplexEntity>(e => e.IsActive);
                Assert.Contains("= 1", sql1);

                // Negated standalone boolean
                var (sql2, _) = wb.Build<ComplexEntity>(e => !e.IsActive);
                Assert.Contains("NOT (", sql2);
                Assert.Contains("= 1)", sql2);

                // Explicit comparison with false
                var (sql3, p3) = wb.Build<ComplexEntity>(e => e.IsActive == false);
                Assert.Contains("= @w0", sql3);
                Assert.Equal(false, p3["@w0"]);

                // Explicit comparison with true
                var (sql4, p4) = wb.Build<ComplexEntity>(e => e.IsActive == true);
                Assert.Contains("= @w0", sql4);
                Assert.Equal(true, p4["@w0"]);
            }

            // PostgreSql: uses TRUE / FALSE
            var pgWb = new WhereBuilder(_mapping, _postgres);
            var (pgSql1, _) = pgWb.Build<ComplexEntity>(e => e.IsActive);
            Assert.Contains("= TRUE", pgSql1);

            var (pgSql2, _) = pgWb.Build<ComplexEntity>(e => !e.IsActive);
            Assert.Contains("NOT (", pgSql2);
            Assert.Contains("= TRUE)", pgSql2);
        }

        [Fact]
        public void WhereBuilder_NullableBooleanEdgeCases()
        {
            var wb = new WhereBuilder(_mapping, _sqlServer);

            // Nullable == null -> IS NULL
            var (sql1, _) = wb.Build<ComplexEntity>(e => e.IsApproved == null);
            Assert.Equal("[IsApproved] IS NULL", sql1);

            // Nullable != null -> IS NOT NULL
            var (sql2, _) = wb.Build<ComplexEntity>(e => e.IsApproved != null);
            Assert.Equal("[IsApproved] IS NOT NULL", sql2);

            // Nullable.HasValue -> IS NOT NULL
            var (sql3, _) = wb.Build<ComplexEntity>(e => e.IsApproved.HasValue);
            Assert.Equal("[IsApproved] IS NOT NULL", sql3);

            // !Nullable.HasValue -> NOT ([IsApproved] IS NOT NULL)
            var (sql4, _) = wb.Build<ComplexEntity>(e => !e.IsApproved.HasValue);
            Assert.Equal("NOT ([IsApproved] IS NOT NULL)", sql4);

            // Nullable.Value standalone condition -> = 1
            var (sql5, _) = wb.Build<ComplexEntity>(e => e.IsApproved.Value);
            Assert.Equal("[IsApproved] = 1", sql5);

            // !Nullable.Value standalone condition -> NOT ([IsApproved] = 1)
            var (sql6, _) = wb.Build<ComplexEntity>(e => !e.IsApproved.Value);
            Assert.Equal("NOT ([IsApproved] = 1)", sql6);

            // Nullable.Value == true -> = @w0
            var (sql7, p7) = wb.Build<ComplexEntity>(e => e.IsApproved.Value == true);
            Assert.Equal("[IsApproved] = @w0", sql7);
            Assert.Equal(true, p7["@w0"]);
        }

        [Fact]
        public void WhereBuilder_ComplexNestedLogicalTree()
        {
            var wb = new WhereBuilder(_mapping, _sqlServer);

            // (IsActive && Price > 100) || (!IsActive && Score <= 50)
            var (sql, p) = wb.Build<ComplexEntity>(e =>
                (e.IsActive && e.Price > 100m) || (!e.IsActive && e.Score <= 50));

            Assert.StartsWith("(", sql);
            Assert.EndsWith(")", sql);
            Assert.Contains("[IsActive] = 1 AND [Price] > @w0", sql);
            Assert.Contains("OR", sql);
            Assert.Contains("NOT ([IsActive] = 1) AND [Score] <= @w1", sql);
            Assert.Equal(100m, p["@w0"]);
            Assert.Equal(50, p["@w1"]);

            // Negated compound condition: !(IsActive && Price > 100)
            var (negSql, _) = wb.Build<ComplexEntity>(e => !(e.IsActive && e.Price > 100m));
            Assert.StartsWith("NOT (", negSql);
            Assert.Contains("[IsActive] = 1 AND [Price] > @w", negSql);
        }

        #endregion

        #region 2. WhereBuilder Arithmetic & String Edge Cases

        [Fact]
        public void WhereBuilder_ArithmeticOperatorsInWhere()
        {
            var wb = new WhereBuilder(_mapping, _sqlServer);

            // Addition: Price + 10 > 100
            var (sqlAdd, _) = wb.Build<ComplexEntity>(e => e.Price + 10m > 100m);
            Assert.Equal("[Price] + @w0 > @w1", sqlAdd);

            // Subtraction: Score - 5 <= 50
            var (sqlSub, _) = wb.Build<ComplexEntity>(e => e.Score - 5 <= 50);
            Assert.Equal("[Score] - @w0 <= @w1", sqlSub);

            // Multiplication: Price * 2 >= 200
            var (sqlMul, _) = wb.Build<ComplexEntity>(e => e.Price * 2m >= 200m);
            Assert.Equal("[Price] * @w0 >= @w1", sqlMul);

            // Division: Score / 10 == 5
            var (sqlDiv, _) = wb.Build<ComplexEntity>(e => e.Score / 10 == 5);
            Assert.Equal("[Score] / @w0 = @w1", sqlDiv);

            // Modulo: Score % 2 == 0
            var (sqlMod, _) = wb.Build<ComplexEntity>(e => e.Score % 2 == 0);
            Assert.Equal("[Score] % @w0 = @w1", sqlMod);
        }

        [Fact]
        public void WhereBuilder_ChainedStringFunctions()
        {
            var wb = new WhereBuilder(_mapping, _sqlServer);

            // ToLower().Trim() == "abc"
            var (sql1, p1) = wb.Build<ComplexEntity>(e => e.Name.ToLower().Trim() == "abc");
            Assert.Equal("TRIM(LOWER([Name])) = @w0", sql1);
            Assert.Equal("abc", p1["@w0"]);

            // ToLower().Contains("abc")
            var (sql2, p2) = wb.Build<ComplexEntity>(e => e.Name.ToLower().Contains("abc"));
            Assert.Contains("LOWER([Name]) LIKE @w0 ESCAPE '\\'", sql2);
            Assert.Equal("%abc%", p2["@w0"]);

            // ToUpper().StartsWith("XYZ")
            var (sql3, p3) = wb.Build<ComplexEntity>(e => e.Name.ToUpper().StartsWith("XYZ"));
            Assert.Contains("UPPER([Name]) LIKE @w0 ESCAPE '\\'", sql3);
            Assert.Equal("XYZ%", p3["@w0"]);

            // Trim().EndsWith(".COM")
            var (sql4, p4) = wb.Build<ComplexEntity>(e => e.Name.Trim().EndsWith(".COM"));
            Assert.Contains("TRIM([Name]) LIKE @w0 ESCAPE '\\'", sql4);
            Assert.Equal("%.COM", p4["@w0"]);

            // TrimStart and TrimEnd
            var (sql5, _) = wb.Build<ComplexEntity>(e => e.Name.TrimStart() == "A");
            Assert.Equal("LTRIM([Name]) = @w0", sql5);

            var (sql6, _) = wb.Build<ComplexEntity>(e => e.Name.TrimEnd() == "B");
            Assert.Equal("RTRIM([Name]) = @w0", sql6);

            // Replace
            var (sql7, p7) = wb.Build<ComplexEntity>(e => e.Name.Replace(" ", "-") == "A-B");
            Assert.Equal("REPLACE([Name], @w0, @w1) = @w2", sql7);
            Assert.Equal(" ", p7["@w0"]);
            Assert.Equal("-", p7["@w1"]);
            Assert.Equal("A-B", p7["@w2"]);
        }

        [Fact]
        public void WhereBuilder_StringLengthAcrossDialects_AndComparingTwoColumns()
        {
            // SQL Server: uses LEN
            var sqlServerWb = new WhereBuilder(_mapping, _sqlServer);
            var (sql1, _) = sqlServerWb.Build<ComplexEntity>(e => e.Name.Length > 5);
            Assert.Equal("LEN([Name]) > @w0", sql1);

            // Other dialects: use LENGTH
            var otherDialects = new ISqlDialect[] { _postgres, _sqlite, _mysql, _oracle, _firebird, _ansi };
            foreach (var d in otherDialects)
            {
                var wb = new WhereBuilder(_mapping, d);
                var (sql, _) = wb.Build<ComplexEntity>(e => e.Name.Length > 5);
                Assert.Contains("LENGTH(", sql);
            }

            // Comparing length of two entity columns: Name.Length <= Description.Length
            var (colLenSql, _) = sqlServerWb.Build<ComplexEntity>(e => e.Name.Length <= e.Description.Length);
            Assert.Equal("LEN([Name]) <= LEN([Description])", colLenSql);
        }

        [Fact]
        public void WhereBuilder_IsNullOrEmpty_And_IsNullOrWhiteSpace_Negations()
        {
            var wb = new WhereBuilder(_mapping, _sqlServer);

            // !string.IsNullOrEmpty
            var (sql1, _) = wb.Build<ComplexEntity>(e => !string.IsNullOrEmpty(e.Description));
            Assert.Equal("NOT (([Description] IS NULL OR [Description] = ''))", sql1);

            // !string.IsNullOrWhiteSpace
            var (sql2, _) = wb.Build<ComplexEntity>(e => !string.IsNullOrWhiteSpace(e.Description));
            Assert.Equal("NOT (([Description] IS NULL OR TRIM([Description]) = ''))", sql2);
        }

        [Fact]
        public void WhereBuilder_CollectionContainsEdgeCases()
        {
            var wb = new WhereBuilder(_mapping, _sqlServer);

            // Empty collection -> 1 = 0
            var emptyList = new List<int>();
            var (emptySql, _) = wb.Build<ComplexEntity>(e => emptyList.Contains(e.Id));
            Assert.Equal("1 = 0", emptySql);

            // Single item collection -> [Id] IN (@w0)
            var singleList = new List<int> { 42 };
            var (singleSql, pSingle) = wb.Build<ComplexEntity>(e => singleList.Contains(e.Id));
            Assert.Equal("[Id] IN (@w0)", singleSql);
            Assert.Equal(42, pSingle["@w0"]);

            // Large collection -> [Id] IN (@w0, @w1, ..., @w99)
            var largeList = Enumerable.Range(1, 100).ToList();
            var (largeSql, pLarge) = wb.Build<ComplexEntity>(e => largeList.Contains(e.Id));
            Assert.Contains("[Id] IN (", largeSql);
            Assert.Equal(100, pLarge.Count);
        }

        #endregion

        #region 3. Bulk Insert Across All 7 Dialects

        [Fact]
        public void BulkInsert_Oracle_SingleAndMultiRowGeneration()
        {
            // 1 row
            var sql1 = _oracle.GenerateBulkInsertSql("\"PRODUCTS\"", new[] { "\"ID\"", "\"NAME\"" }, new[]
            {
                new[] { ":p0", ":p1" }
            });
            Assert.Equal("INSERT ALL INTO \"PRODUCTS\" (\"ID\", \"NAME\") VALUES (:p0, :p1) SELECT * FROM dual", sql1);

            // 3 rows
            var sql3 = _oracle.GenerateBulkInsertSql("\"PRODUCTS\"", new[] { "\"ID\"", "\"NAME\"" }, new[]
            {
                new[] { ":p0", ":p1" },
                new[] { ":p2", ":p3" },
                new[] { ":p4", ":p5" }
            });
            Assert.Equal("INSERT ALL INTO \"PRODUCTS\" (\"ID\", \"NAME\") VALUES (:p0, :p1) INTO \"PRODUCTS\" (\"ID\", \"NAME\") VALUES (:p2, :p3) INTO \"PRODUCTS\" (\"ID\", \"NAME\") VALUES (:p4, :p5) SELECT * FROM dual", sql3);
        }

        [Fact]
        public void BulkInsert_StandardDialects_MultiRowValues()
        {
            var dialects = new ISqlDialect[] { _sqlServer, _sqlite, _postgres, _mysql, _firebird, _ansi };

            foreach (var dialect in dialects)
            {
                Assert.True(dialect.SupportsMultiRowValues);

                var sql = dialect.GenerateBulkInsertSql("T", new[] { "C1", "C2" }, new[]
                {
                    new[] { "@p0", "@p1" },
                    new[] { "@p2", "@p3" },
                    new[] { "@p4", "@p5" }
                });

                Assert.Equal("INSERT INTO T (C1, C2) VALUES (@p0, @p1), (@p2, @p3), (@p4, @p5)", sql);
            }
        }

        #endregion

        #region 4. DbProviderRegistry Heuristics & Thread-Safety

        [Theory]
        // SQL Server variations
        [InlineData("Server=tcp:sqlserver.database.windows.net,1433;Database=appdb;", "SqlServer")]
        [InlineData("Data Source=localhost\\SQLEXPRESS;Initial Catalog=MyDb;Integrated Security=True;Encrypt=False", "SqlServer")]
        [InlineData("server=127.0.0.1;database=test;trusted_connection=true;", "SqlServer")]
        // SQLite variations
        [InlineData("Data Source=C:\\data\\mydb.sqlite3;Cache=Shared", "Sqlite")]
        [InlineData("Filename=test.db;Mode=ReadWriteCreate", "Sqlite")]
        [InlineData("Data Source=:memory:", "Sqlite")]
        [InlineData("data source=app.db;mode=memory;", "Sqlite")]
        // PostgreSQL variations
        [InlineData("Host=db.example.com;Port=5432;Database=proddb;Username=app;Password=secret", "PostgreSql")]
        [InlineData("Server=10.0.0.5;Database=test;Search Path=myschema", "PostgreSql")]
        [InlineData("host=localhost;database=users;sslmode=require;", "PostgreSql")]
        // MySQL variations
        [InlineData("Server=mysql.local;Port=3306;Database=store;Uid=app;Pwd=secret", "MySql")]
        [InlineData("server=localhost;uid=root;pwd=root;allowuservariables=true;", "MySql")]
        // Oracle variations
        [InlineData("Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=oracle.local)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=pdb1)));User Id=app;Password=secret", "Oracle")]
        [InlineData("Data Source=PROD_ORCL;User Id=hr;Password=welcome;", "Oracle")]
        [InlineData("data source=test.world;user id=scott;password=tiger;", "Oracle")]
        // Firebird variations
        [InlineData("Database=localhost:C:\\fb\\data.fdb;User=SYSDBA;Password=masterkey;Dialect=3", "Firebird")]
        [InlineData("database=127.0.0.1:/db/firebird.fdb;user=sysdba;", "Firebird")]
        // Fallback
        [InlineData("", "SqlServer")]
        [InlineData("SomeRandomUnknownConnectionString=True;", "SqlServer")]
        public void DbProviderRegistry_DetectProvider_CoversAllVariations(string connectionString, string expectedProvider)
        {
            var detected = DbProviderRegistry.DetectProvider(connectionString);
            Assert.Equal(expectedProvider, detected);
        }

        [Fact]
        public void DbProviderRegistry_CaseInsensitiveProviderLookup()
        {
            DbProviderRegistry.Register("CaseInsensitiveDb", cs => new MockOracleConnection { ConnectionString = cs });

            var conn1 = DbProviderRegistry.CreateConnection("cs", "caseinsensitivedb");
            var conn2 = DbProviderRegistry.CreateConnection("cs", "CASEINSENSITIVEDB");
            var conn3 = DbProviderRegistry.CreateConnection("cs", "CaseInsensitiveDb");

            Assert.NotNull(conn1);
            Assert.NotNull(conn2);
            Assert.NotNull(conn3);
        }

        [Fact]
        public void DbProviderRegistry_UnregisteredProvider_ThrowsClearException()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                DbProviderRegistry.CreateConnection("cs", "UnregisteredNonExistentDb"));

            Assert.Contains("UnregisteredNonExistentDb", ex.Message);
            Assert.Contains("DbProviderRegistry.Register", ex.Message);
        }

        [Fact]
        public async Task DbProviderRegistry_ConcurrentRegistrationAndCreation_ThreadSafe()
        {
            var parallelTasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
            {
                var providerName = $"StressProvider_{i % 10}";
                DbProviderRegistry.Register(providerName, cs => new MockOracleConnection { ConnectionString = cs });

                var conn = DbProviderRegistry.CreateConnection($"Data Source=Stress_{i};", providerName);
                Assert.NotNull(conn);

                var detected = DbProviderRegistry.DetectProvider($"Data Source=mydb_{i}.sqlite;");
                Assert.Equal("Sqlite", detected);
            })).ToArray();

            await Task.WhenAll(parallelTasks);
        }

        #endregion

        #region 5. SqlDialectFactory Caching & Concurrency

        [Fact]
        public async Task SqlDialectFactory_ConcurrentResolution_ThreadSafe()
        {
            var dialectsResolved = new ConcurrentBag<ISqlDialect>();

            var parallelTasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
            {
                using var ora = new MockOracleConnection();
                using var fb = new MockFirebirdConnection();
                using var custom = new MockCustomDuckDbConnection();

                dialectsResolved.Add(SqlDialectFactory.GetDialect(ora));
                dialectsResolved.Add(SqlDialectFactory.GetDialect(fb));
                dialectsResolved.Add(SqlDialectFactory.GetDialect(custom));
            })).ToArray();

            await Task.WhenAll(parallelTasks);

            Assert.Equal(300, dialectsResolved.Count);
            Assert.Contains(dialectsResolved, d => d is OracleDialect);
            Assert.Contains(dialectsResolved, d => d is FirebirdDialect);
            Assert.Contains(dialectsResolved, d => d is AnsiSqlDialect);
        }

        #endregion

        #region 6. SqlGenerator & SchemaBuilder Multi-Dialect Tests

        [Fact]
        public void SqlGenerator_GenerateInsert_QuotingAndParameters_AcrossDialects()
        {
            var entity = new ComplexEntity { Name = "Alpha", Price = 12.5m };

            // Oracle
            var (oraSql, oraParams) = SqlGenerator.GenerateInsert(_mapping, entity, _oracle);
            Assert.Contains("INSERT INTO \"ComplexEntities\"", oraSql);
            Assert.Equal("Alpha", oraParams["@Name"]);

            // Firebird
            var (fbSql, _) = SqlGenerator.GenerateInsert(_mapping, entity, _firebird);
            Assert.Contains("INSERT INTO \"ComplexEntities\"", fbSql);

            // MySQL
            var (mySql, _) = SqlGenerator.GenerateInsert(_mapping, entity, _mysql);
            Assert.Contains("INSERT INTO `ComplexEntities`", mySql);

            // ANSI
            var (ansiSql, _) = SqlGenerator.GenerateInsert(_mapping, entity, _ansi);
            Assert.Contains("INSERT INTO \"ComplexEntities\"", ansiSql);
        }

        [Fact]
        public void SqlGenerator_SoftDelete_GeneratesCorrectLiterals()
        {
            var softMapping = MappingCache.GetMapping<SoftDeletedEntity>();
            var entity = new SoftDeletedEntity { Id = 10, Title = "Test" };

            // PostgreSQL uses TRUE
            var (pgSql, _) = SqlGenerator.GenerateDelete(softMapping, entity, _postgres);
            Assert.Contains("SET \"IsDeleted\" = TRUE", pgSql);

            // All others use 1
            var (sqlServerSql, _) = SqlGenerator.GenerateDelete(softMapping, entity, _sqlServer);
            Assert.Contains("SET [IsDeleted] = 1", sqlServerSql);

            var (oraSql, _) = SqlGenerator.GenerateDelete(softMapping, entity, _oracle);
            Assert.Contains("SET \"IsDeleted\" = 1", oraSql);

            var (fbSql, _) = SqlGenerator.GenerateDelete(softMapping, entity, _firebird);
            Assert.Contains("SET \"IsDeleted\" = 1", fbSql);
        }

        [Fact]
        public void SchemaBuilder_CreateTable_GeneratesDialectSpecificAutoIncrement()
        {
            // Oracle
            var oraSb = new SchemaBuilder(_oracle);
            oraSb.CreateTable("TestTable", t =>
            {
                t.Int("Id").Identity().PrimaryKey();
                t.String("Name", 100).NotNull();
            });
            var oraStmt = oraSb.Statements[0];
            Assert.Contains("GENERATED ALWAYS AS IDENTITY", oraStmt);
            Assert.Contains("\"TestTable\"", oraStmt);

            // Firebird
            var fbSb = new SchemaBuilder(_firebird);
            fbSb.CreateTable("TestTable", t =>
            {
                t.Int("Id").Identity().PrimaryKey();
                t.String("Name", 100).NotNull();
            });
            var fbStmt = fbSb.Statements[0];
            Assert.Contains("GENERATED BY DEFAULT AS IDENTITY", fbStmt);
            Assert.Contains("\"TestTable\"", fbStmt);

            // Postgres
            var pgSb = new SchemaBuilder(_postgres);
            pgSb.CreateTable("TestTable", t =>
            {
                t.Int("Id").Identity().PrimaryKey();
            });
            var pgStmt = pgSb.Statements[0];
            Assert.Contains("GENERATED ALWAYS AS IDENTITY", pgStmt);

            // SQL Server
            var msSb = new SchemaBuilder(_sqlServer);
            msSb.CreateTable("TestTable", t =>
            {
                t.Int("Id").Identity().PrimaryKey();
            });
            var msStmt = msSb.Statements[0];
            Assert.Contains("IDENTITY(1,1)", msStmt);
        }

        #endregion
    }
}
