using System;
using System.Collections.Generic;
using ZeroData.Sql.Dialects;
using Xunit;

namespace ZeroData.Sql.Tests
{
    public class DialectTests
    {
        [Fact]
        public void SqlServerDialect_FeaturesAndSqlGeneration()
        {
            var dialect = new SqlServerDialect();
            Assert.Equal("SQL Server", dialect.ProviderName);
            Assert.Equal("@", dialect.ParameterPrefix);
            Assert.True(dialect.SupportsOutputClause);
            Assert.False(dialect.SupportsReturningClause);
            Assert.True(dialect.SupportsMultiRowValues);

            // Quoting
            Assert.Equal("[Users]", dialect.QuoteIdentifier("Users"));
            Assert.Equal("[User]]Name]", dialect.QuoteIdentifier("User]Name"));

            // Paging
            Assert.Equal("OFFSET 10 ROWS FETCH NEXT 20 ROWS ONLY", dialect.GetLimitClause(10, 20));
            Assert.Equal("OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY", dialect.GetLimitClause(null, 10));
            Assert.Equal("OFFSET 5 ROWS", dialect.GetLimitClause(5, null));
            Assert.Equal(string.Empty, dialect.GetLimitClause(null, null));

            // Types
            Assert.Equal("BIT", dialect.GetDbType(typeof(bool)));
            Assert.Equal("INT", dialect.GetDbType(typeof(int)));
            Assert.Equal("BIGINT", dialect.GetDbType(typeof(long)));
            Assert.Equal("NVARCHAR(MAX)", dialect.GetDbType(typeof(string)));
            Assert.Equal("UNIQUEIDENTIFIER", dialect.GetDbType(typeof(Guid)));

            // Bulk Insert
            var sql = dialect.GenerateBulkInsertSql("Users", new[] { "Name", "Age" }, new[]
            {
                new[] { "@p0", "@p1" },
                new[] { "@p2", "@p3" }
            });
            Assert.Equal("INSERT INTO Users (Name, Age) VALUES (@p0, @p1), (@p2, @p3)", sql);
        }

        [Fact]
        public void SqliteDialect_FeaturesAndSqlGeneration()
        {
            var dialect = new SqliteDialect();
            Assert.Equal("SQLite", dialect.ProviderName);
            Assert.Equal("@", dialect.ParameterPrefix);
            Assert.False(dialect.SupportsOutputClause);
            Assert.True(dialect.SupportsReturningClause);
            Assert.True(dialect.SupportsMultiRowValues);

            // Quoting
            Assert.Equal("\"Users\"", dialect.QuoteIdentifier("Users"));

            // Paging
            Assert.Equal("LIMIT 20 OFFSET 10", dialect.GetLimitClause(10, 20));
            Assert.Equal("LIMIT 10", dialect.GetLimitClause(null, 10));
            Assert.Equal("OFFSET 5", dialect.GetLimitClause(5, null));

            // Types
            Assert.Equal("INTEGER", dialect.GetDbType(typeof(int)));
            Assert.Equal("INTEGER", dialect.GetDbType(typeof(bool)));
            Assert.Equal("TEXT", dialect.GetDbType(typeof(string)));

            // Bulk Insert
            var sql = dialect.GenerateBulkInsertSql("Users", new[] { "Name" }, new[]
            {
                new[] { "@p0" },
                new[] { "@p1" }
            });
            Assert.Equal("INSERT INTO Users (Name) VALUES (@p0), (@p1)", sql);
        }

        [Fact]
        public void PostgreSqlDialect_FeaturesAndSqlGeneration()
        {
            var dialect = new PostgreSqlDialect();
            Assert.Equal("PostgreSQL", dialect.ProviderName);
            Assert.True(dialect.SupportsReturningClause);
            Assert.True(dialect.SupportsMultiRowValues);

            // Quoting
            Assert.Equal("\"Users\"", dialect.QuoteIdentifier("Users"));
            Assert.Equal("\"User\"\"Name\"", dialect.QuoteIdentifier("User\"Name"));

            // Paging
            Assert.Equal("LIMIT 20 OFFSET 10", dialect.GetLimitClause(10, 20));

            // Types
            Assert.Equal("BOOLEAN", dialect.GetDbType(typeof(bool)));
            Assert.Equal("UUID", dialect.GetDbType(typeof(Guid)));
            Assert.Equal("BYTEA", dialect.GetDbType(typeof(byte[])));

            // Bulk Insert
            var sql = dialect.GenerateBulkInsertSql("\"Users\"", new[] { "\"Name\"" }, new[]
            {
                new[] { "@p0" },
                new[] { "@p1" }
            });
            Assert.Equal("INSERT INTO \"Users\" (\"Name\") VALUES (@p0), (@p1)", sql);
        }

        [Fact]
        public void MySqlDialect_FeaturesAndSqlGeneration()
        {
            var dialect = new MySqlDialect();
            Assert.Equal("MySQL", dialect.ProviderName);
            Assert.True(dialect.SupportsMultiRowValues);

            // Quoting
            Assert.Equal("`Users`", dialect.QuoteIdentifier("Users"));
            Assert.Equal("`User``Name`", dialect.QuoteIdentifier("User`Name"));

            // Paging
            Assert.Equal("LIMIT 20 OFFSET 10", dialect.GetLimitClause(10, 20));
            Assert.Equal("LIMIT 20", dialect.GetLimitClause(null, 20));

            // Types
            Assert.Equal("TINYINT(1)", dialect.GetDbType(typeof(bool)));
            Assert.Equal("TEXT", dialect.GetDbType(typeof(string)));

            // Bulk Insert
            var sql = dialect.GenerateBulkInsertSql("`Users`", new[] { "`Name`" }, new[]
            {
                new[] { "@p0" },
                new[] { "@p1" }
            });
            Assert.Equal("INSERT INTO `Users` (`Name`) VALUES (@p0), (@p1)", sql);
        }

        [Fact]
        public void OracleDialect_FeaturesAndSqlGeneration()
        {
            var dialect = new OracleDialect();
            Assert.Equal("Oracle", dialect.ProviderName);
            Assert.Equal(":", dialect.ParameterPrefix);
            Assert.True(dialect.SupportsReturningClause);
            Assert.False(dialect.SupportsOutputClause);
            Assert.False(dialect.SupportsMultiRowValues);

            // Quoting
            Assert.Equal("\"USERS\"", dialect.QuoteIdentifier("USERS"));
            Assert.Equal("\"USER\"\"NAME\"", dialect.QuoteIdentifier("USER\"NAME"));

            // Paging (Oracle 12c+ OFFSET / FETCH)
            Assert.Equal("OFFSET 10 ROWS FETCH NEXT 20 ROWS ONLY", dialect.GetLimitClause(10, 20));
            Assert.Equal("OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY", dialect.GetLimitClause(null, 10));
            Assert.Equal("OFFSET 5 ROWS", dialect.GetLimitClause(5, null));

            // Types (Oracle specific)
            Assert.Equal("NUMBER(1)", dialect.GetDbType(typeof(bool)));
            Assert.Equal("NUMBER(10)", dialect.GetDbType(typeof(int)));
            Assert.Equal("NUMBER(19)", dialect.GetDbType(typeof(long)));
            Assert.Equal("VARCHAR2(4000)", dialect.GetDbType(typeof(string)));
            Assert.Equal("RAW(16)", dialect.GetDbType(typeof(Guid)));
            Assert.Equal("BLOB", dialect.GetDbType(typeof(byte[])));

            // Identity
            Assert.Equal("GENERATED ALWAYS AS IDENTITY", dialect.GetAutoIncrementSql());
            Assert.Equal("RETURNING \"ID\" INTO :new_id", dialect.GetLastInsertIdSql("USERS", "ID"));

            // Oracle Multi-Row Bulk Insert Syntax: INSERT ALL INTO ... SELECT * FROM dual
            var sql = dialect.GenerateBulkInsertSql("\"USERS\"", new[] { "\"NAME\"", "\"AGE\"" }, new[]
            {
                new[] { ":p0", ":p1" },
                new[] { ":p2", ":p3" }
            });
            Assert.Equal("INSERT ALL INTO \"USERS\" (\"NAME\", \"AGE\") VALUES (:p0, :p1) INTO \"USERS\" (\"NAME\", \"AGE\") VALUES (:p2, :p3) SELECT * FROM dual", sql);
        }

        [Fact]
        public void FirebirdDialect_FeaturesAndSqlGeneration()
        {
            var dialect = new FirebirdDialect();
            Assert.Equal("Firebird", dialect.ProviderName);
            Assert.Equal("@", dialect.ParameterPrefix);
            Assert.True(dialect.SupportsReturningClause);
            Assert.False(dialect.SupportsOutputClause);
            Assert.True(dialect.SupportsMultiRowValues);

            // Quoting
            Assert.Equal("\"Users\"", dialect.QuoteIdentifier("Users"));

            // Paging
            Assert.Equal("OFFSET 10 ROWS FETCH NEXT 20 ROWS ONLY", dialect.GetLimitClause(10, 20));
            Assert.Equal("OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY", dialect.GetLimitClause(null, 10));
            Assert.Equal("OFFSET 5 ROWS", dialect.GetLimitClause(5, null));

            // Types
            Assert.Equal("BOOLEAN", dialect.GetDbType(typeof(bool)));
            Assert.Equal("CHAR(16) CHARACTER SET OCTETS", dialect.GetDbType(typeof(Guid)));
            Assert.Equal("BLOB", dialect.GetDbType(typeof(byte[])));

            // Identity
            Assert.Equal("GENERATED BY DEFAULT AS IDENTITY", dialect.GetAutoIncrementSql());
            Assert.Equal("RETURNING \"Id\"", dialect.GetLastInsertIdSql("Users", "Id"));

            // Bulk Insert
            var sql = dialect.GenerateBulkInsertSql("\"Users\"", new[] { "\"Name\"" }, new[]
            {
                new[] { "@p0" },
                new[] { "@p1" }
            });
            Assert.Equal("INSERT INTO \"Users\" (\"Name\") VALUES (@p0), (@p1)", sql);
        }

        [Fact]
        public void AnsiSqlDialect_FeaturesAndSqlGeneration()
        {
            var dialect = new AnsiSqlDialect();
            Assert.Equal("ANSI SQL", dialect.ProviderName);
            Assert.Equal("@", dialect.ParameterPrefix);
            Assert.False(dialect.SupportsReturningClause);
            Assert.False(dialect.SupportsOutputClause);
            Assert.True(dialect.SupportsMultiRowValues);

            // Quoting
            Assert.Equal("\"Users\"", dialect.QuoteIdentifier("Users"));

            // Paging (Standard ANSI SQL:2008)
            Assert.Equal("OFFSET 10 ROWS FETCH NEXT 20 ROWS ONLY", dialect.GetLimitClause(10, 20));
            Assert.Equal("OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY", dialect.GetLimitClause(null, 10));

            // Types
            Assert.Equal("BOOLEAN", dialect.GetDbType(typeof(bool)));
            Assert.Equal("INTEGER", dialect.GetDbType(typeof(int)));

            // Bulk Insert
            var sql = dialect.GenerateBulkInsertSql("\"Users\"", new[] { "\"Name\"" }, new[]
            {
                new[] { "@p0" },
                new[] { "@p1" }
            });
            Assert.Equal("INSERT INTO \"Users\" (\"Name\") VALUES (@p0), (@p1)", sql);
        }
    }
}
