using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Xunit;
using ZeroData.Sql.Dialects;
using ZeroData.Sql.Mapping;
using ZeroData.Sql.Sql;

namespace ZeroData.Sql.Tests
{
    [Table(Name = "LinqItems")]
    public class LinqItem
    {
        [Column(Name = "Id", IsPrimaryKey = true)]
        public int Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; }

        [Column(Name = "Description")]
        public string Description { get; set; }

        [Column(Name = "Price")]
        public decimal Price { get; set; }

        [Column(Name = "Rating")]
        public double Rating { get; set; }

        [Column(Name = "CreatedAt")]
        public DateTime CreatedAt { get; set; }

        [Column(Name = "ModifiedAt")]
        public DateTime? ModifiedAt { get; set; }
    }

    public class LinqExpressionExtensionsTests
    {
        private readonly EntityMapping _mapping;

        public LinqExpressionExtensionsTests()
        {
            _mapping = MappingCache.GetMapping<LinqItem>();
        }

        #region DateTime Translation Tests

        [Fact]
        public void DateTime_SqlServer_GeneratesDateFunctions()
        {
            var builder = new WhereBuilder(_mapping, new SqlServerDialect());

            var (sqlYear, _) = builder.Build<LinqItem>(p => p.CreatedAt.Year == 2024);
            Assert.Equal("YEAR([CreatedAt]) = @w0", sqlYear);

            var (sqlMonth, _) = builder.Build<LinqItem>(p => p.CreatedAt.Month == 12);
            Assert.Equal("MONTH([CreatedAt]) = @w0", sqlMonth);

            var (sqlDay, _) = builder.Build<LinqItem>(p => p.CreatedAt.Day == 25);
            Assert.Equal("DAY([CreatedAt]) = @w0", sqlDay);

            var (sqlDate, _) = builder.Build<LinqItem>(p => p.CreatedAt.Date == DateTime.Today);
            Assert.Equal("CAST([CreatedAt] AS DATE) = @w0", sqlDate);
        }

        [Fact]
        public void DateTime_Sqlite_GeneratesStrftimeAndDate()
        {
            var builder = new WhereBuilder(_mapping, new SqliteDialect());

            var (sqlYear, _) = builder.Build<LinqItem>(p => p.CreatedAt.Year == 2024);
            Assert.Equal("CAST(strftime('%Y', \"CreatedAt\") AS INTEGER) = @w0", sqlYear);

            var (sqlMonth, _) = builder.Build<LinqItem>(p => p.CreatedAt.Month == 6);
            Assert.Equal("CAST(strftime('%m', \"CreatedAt\") AS INTEGER) = @w0", sqlMonth);

            var (sqlDay, _) = builder.Build<LinqItem>(p => p.CreatedAt.Day == 15);
            Assert.Equal("CAST(strftime('%d', \"CreatedAt\") AS INTEGER) = @w0", sqlDay);

            var (sqlDate, _) = builder.Build<LinqItem>(p => p.CreatedAt.Date == DateTime.Today);
            Assert.Equal("date(\"CreatedAt\") = @w0", sqlDate);
        }

        [Fact]
        public void DateTime_PostgreSql_GeneratesExtractAndCast()
        {
            var builder = new WhereBuilder(_mapping, new PostgreSqlDialect());

            var (sqlYear, _) = builder.Build<LinqItem>(p => p.CreatedAt.Year == 2025);
            Assert.Equal("EXTRACT(YEAR FROM \"CreatedAt\") = @w0", sqlYear);

            var (sqlDate, _) = builder.Build<LinqItem>(p => p.CreatedAt.Date == DateTime.Today);
            Assert.Equal("CAST(\"CreatedAt\" AS DATE) = @w0", sqlDate);
        }

        [Fact]
        public void DateTime_MySql_GeneratesNativeFunctions()
        {
            var builder = new WhereBuilder(_mapping, new MySqlDialect());

            var (sqlYear, _) = builder.Build<LinqItem>(p => p.CreatedAt.Year == 2026);
            Assert.Equal("YEAR(`CreatedAt`) = @w0", sqlYear);

            var (sqlDate, _) = builder.Build<LinqItem>(p => p.CreatedAt.Date == DateTime.Today);
            Assert.Equal("DATE(`CreatedAt`) = @w0", sqlDate);
        }

        [Fact]
        public void DateTime_NullableProperty_TranslatesCorrectly()
        {
            var builder = new WhereBuilder(_mapping, new SqlServerDialect());

            var (sql, _) = builder.Build<LinqItem>(p => p.ModifiedAt.Value.Year == 2024);
            Assert.Equal("YEAR([ModifiedAt]) = @w0", sql);
        }

        #endregion

        #region Coalesce (??) Tests

        [Fact]
        public void Coalesce_GeneratesSqlCoalesceFunction()
        {
            var builder = new WhereBuilder(_mapping, new SqlServerDialect());

            var (sql, parameters) = builder.Build<LinqItem>(p => (p.Description ?? "No description") == "Archived");

            Assert.Equal("COALESCE([Description], @w0) = @w1", sql);
            Assert.Equal("No description", parameters["@w0"]);
            Assert.Equal("Archived", parameters["@w1"]);
        }

        #endregion

        #region Math Functions Tests

        [Fact]
        public void Math_Abs_GeneratesAbsSql()
        {
            var builder = new WhereBuilder(_mapping, new SqlServerDialect());

            var (sql, _) = builder.Build<LinqItem>(p => Math.Abs(p.Rating) > 4.0);
            Assert.Equal("ABS([Rating]) > @w0", sql);
        }

        [Fact]
        public void Math_Round_OneArg_GeneratesRoundSql()
        {
            var builder = new WhereBuilder(_mapping, new SqlServerDialect());

            var (sql, _) = builder.Build<LinqItem>(p => Math.Round(p.Rating) == 5.0);
            Assert.Equal("ROUND([Rating]) = @w0", sql);
        }

        [Fact]
        public void Math_Round_TwoArgs_GeneratesRoundWithDecimals()
        {
            var builder = new WhereBuilder(_mapping, new SqlServerDialect());

            var (sql, parameters) = builder.Build<LinqItem>(p => Math.Round(p.Price, 2) == 19.99m);
            Assert.Equal("ROUND([Price], @w0) = @w1", sql);
            Assert.Equal(2, parameters["@w0"]);
            Assert.Equal(19.99m, parameters["@w1"]);
        }

        [Fact]
        public void Math_Floor_GeneratesFloorSql()
        {
            var builder = new WhereBuilder(_mapping, new SqlServerDialect());

            var (sql, _) = builder.Build<LinqItem>(p => Math.Floor(p.Price) == 100m);
            Assert.Equal("FLOOR([Price]) = @w0", sql);
        }

        [Fact]
        public void Math_Ceiling_GeneratesCeilingSql()
        {
            var builder = new WhereBuilder(_mapping, new SqlServerDialect());

            var (sql, _) = builder.Build<LinqItem>(p => Math.Ceiling(p.Price) == 101m);
            Assert.Equal("CEILING([Price]) = @w0", sql);
        }

        #endregion

        #region Sqlite In-Memory End-to-End Integration Tests

        [Fact]
        public void EndToEnd_Sqlite_DateTimeAndCoalesceQueries()
        {
            using var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE LinqItems (
                        Id INTEGER PRIMARY KEY,
                        Name TEXT NOT NULL,
                        Description TEXT,
                        Price NUMERIC NOT NULL,
                        Rating REAL NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        ModifiedAt TEXT
                    );
                ";
                cmd.ExecuteNonQuery();
            }

            using var db = new SqlContext(conn);

            var items = new List<LinqItem>
            {
                new LinqItem { Id = 1, Name = "Item1", Description = null, Price = 19.45m, Rating = -4.5, CreatedAt = new DateTime(2023, 5, 10) },
                new LinqItem { Id = 2, Name = "Item2", Description = "CustomDesc", Price = 99.8m, Rating = 3.2, CreatedAt = new DateTime(2024, 8, 20) },
                new LinqItem { Id = 3, Name = "Item3", Description = null, Price = 100.1m, Rating = 4.8, CreatedAt = new DateTime(2024, 12, 25) }
            };
            db.BulkInsert(items);

            // Query by Year
            var items2024 = db.GetTable<LinqItem>().Where(p => p.CreatedAt.Year == 2024).ToList();
            Assert.Equal(2, items2024.Count);

            // Query by Coalesce
            var defaultDescItems = db.GetTable<LinqItem>()
                .Where(p => (p.Description ?? "DefaultVal") == "DefaultVal")
                .ToList();
            Assert.Equal(2, defaultDescItems.Count);

            // Query by Math.Abs
            var absRating = db.GetTable<LinqItem>()
                .Where(p => Math.Abs(p.Rating) > 4.0)
                .ToList();
            Assert.Equal(2, absRating.Count);

            // Query by Math.Round
            var roundRating = db.GetTable<LinqItem>()
                .Where(p => Math.Round(p.Rating) == 3.0)
                .ToList();
            Assert.Single(roundRating);
            Assert.Equal("Item2", roundRating[0].Name);
        }

        #endregion
    }
}
