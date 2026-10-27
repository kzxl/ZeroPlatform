using System.Collections.Generic;
using System.Linq;
using ZeroData.Sql.Dialects;
using ZeroData.Sql.Mapping;
using ZeroData.Sql.Sql;
using Xunit;

namespace ZeroData.Sql.Tests
{
    /// <summary>
    /// Dialect-level SQL generation tests. These assert the generated SQL text per provider
    /// (SQL Server, SQLite, MySQL, PostgreSQL) without requiring a live database, closing the
    /// gap for providers that cannot be integration-tested locally.
    /// </summary>
    public class DialectSqlGenerationTests
    {
        [Table(Name = "Products")]
        public class Product
        {
            [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
            public int Id { get; set; }
            [Column(Name = "Code", IsPrimaryKey = false)]
            public string Code { get; set; }
            [Column(Name = "Name")]
            public string Name { get; set; }
            [Column(Name = "Price")]
            public decimal Price { get; set; }
        }

        [Table(Name = "Settings")]
        public class Setting
        {
            [Column(Name = "Code", IsPrimaryKey = true)]
            public string Code { get; set; }
            [Column(Name = "Value")]
            public string Value { get; set; }
        }

        private static EntityMapping Map<T>() where T : class
        {
            MappingCache.Clear();
            return MappingCache.GetMapping<T>();
        }

        // ---------- Identifier quoting per dialect ----------

        [Theory]
        [InlineData("SqlServer", "[Products]")]
        [InlineData("SQLite", "\"Products\"")]
        [InlineData("MySQL", "`Products`")]
        [InlineData("PostgreSQL", "\"Products\"")]
        public void Insert_UsesDialectQuoting(string provider, string expectedTable)
        {
            var mapping = Map<Product>();
            var dialect = GetDialect(provider);
            var entity = new Product { Code = "A", Name = "X", Price = 1m };

            var (sql, _) = SqlGenerator.GenerateInsert(mapping, entity, dialect);

            Assert.Contains($"INSERT INTO {expectedTable}", sql);
            Assert.DoesNotContain("Id", sql.Substring(0, sql.IndexOf("VALUES"))); // identity skipped
        }

        // ---------- UPSERT per dialect ----------

        [Fact]
        public void Upsert_Sqlite_UsesInsertOrReplace()
        {
            var mapping = Map<Setting>();
            var result = SqlGenerator.GenerateUpsert(mapping, new Setting { Code = "k", Value = "v" }, new SqliteDialect());
            Assert.NotNull(result);
            Assert.Contains("INSERT OR REPLACE INTO \"Settings\"", result.Value.sql);
        }

        [Fact]
        public void Upsert_SqlServer_UsesMerge()
        {
            var mapping = Map<Setting>();
            var result = SqlGenerator.GenerateUpsert(mapping, new Setting { Code = "k", Value = "v" }, new SqlServerDialect());
            Assert.NotNull(result);
            Assert.Contains("MERGE [Settings]", result.Value.sql);
            Assert.Contains("WHEN MATCHED THEN UPDATE", result.Value.sql);
            Assert.Contains("WHEN NOT MATCHED THEN INSERT", result.Value.sql);
        }

        [Fact]
        public void Upsert_MySql_UsesOnDuplicateKey()
        {
            var mapping = Map<Setting>();
            var result = SqlGenerator.GenerateUpsert(mapping, new Setting { Code = "k", Value = "v" }, new MySqlDialect());
            Assert.NotNull(result);
            Assert.Contains("INSERT INTO `Settings`", result.Value.sql);
            Assert.Contains("ON DUPLICATE KEY UPDATE", result.Value.sql);
            Assert.Contains("`Value` = VALUES(`Value`)", result.Value.sql);
        }

        [Fact]
        public void Upsert_PostgreSql_UsesOnConflict()
        {
            var mapping = Map<Setting>();
            var result = SqlGenerator.GenerateUpsert(mapping, new Setting { Code = "k", Value = "v" }, new PostgreSqlDialect());
            Assert.NotNull(result);
            Assert.Contains("INSERT INTO \"Settings\"", result.Value.sql);
            Assert.Contains("ON CONFLICT (\"Code\") DO UPDATE SET", result.Value.sql);
            Assert.Contains("\"Value\" = EXCLUDED.\"Value\"", result.Value.sql);
        }

        // ---------- Soft delete boolean literal per dialect ----------

        [Table(Name = "Docs")]
        [SoftDelete(ColumnName = "IsDeleted")]
        public class Doc
        {
            [Column(Name = "Id", IsPrimaryKey = true)]
            public int Id { get; set; }
            [Column(Name = "IsDeleted")]
            public bool IsDeleted { get; set; }
        }

        [Fact]
        public void SoftDelete_Postgres_UsesTrueLiteral()
        {
            var mapping = Map<Doc>();
            var (sql, _) = SqlGenerator.GenerateDelete(mapping, new Doc { Id = 1 }, new PostgreSqlDialect());
            Assert.Contains("UPDATE \"Docs\" SET \"IsDeleted\" = TRUE", sql);
        }

        [Fact]
        public void SoftDelete_SqlServer_UsesNumericLiteral()
        {
            var mapping = Map<Doc>();
            var (sql, _) = SqlGenerator.GenerateDelete(mapping, new Doc { Id = 1 }, new SqlServerDialect());
            Assert.Contains("UPDATE [Docs] SET [IsDeleted] = 1", sql);
        }

        // ---------- Optimistic concurrency in UPDATE ----------

        [Table(Name = "Versioned")]
        public class Versioned
        {
            [Column(Name = "Id", IsPrimaryKey = true)]
            public int Id { get; set; }
            [Column(Name = "Name")]
            public string Name { get; set; }
            [Column(Name = "Ver", IsVersion = true)]
            public int Ver { get; set; }
        }

        [Fact]
        public void Update_VersionColumn_AddsCheckAndIncrement()
        {
            var mapping = Map<Versioned>();
            var (sql, ps) = SqlGenerator.GenerateUpdate(mapping, new Versioned { Id = 1, Name = "x", Ver = 3 }, new SqlServerDialect());

            Assert.Contains("[Ver] = [Ver] + 1", sql);     // increment in SET
            Assert.Contains("[Ver] = @ver_Ver", sql);        // version check in WHERE
            Assert.Equal(3, ps["@ver_Ver"]);                 // uses loaded version value
        }

        private static ISqlDialect GetDialect(string provider)
        {
            switch (provider)
            {
                case "SQLite": return new SqliteDialect();
                case "MySQL": return new MySqlDialect();
                case "PostgreSQL": return new PostgreSqlDialect();
                default: return new SqlServerDialect();
            }
        }
    }
}
