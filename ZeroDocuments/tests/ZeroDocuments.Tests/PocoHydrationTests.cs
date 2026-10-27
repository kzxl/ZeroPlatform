using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using ZeroDocuments.Excel;

namespace ZeroDocuments.Tests
{
    public class PocoHydrationTests
    {
        public class ComprehensivePoco
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public int? NullableCount { get; set; }
            public decimal? NullablePrice { get; set; }
            public bool? NullableFlag { get; set; }
            public DateTime? NullableDate { get; set; }
            public double Score { get; set; }
        }

        [Fact]
        public void Read_NullableTypesAndValues_ShouldHydrateCorrectly()
        {
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { 1, "Alice", 10, 99.50m, true, new DateTime(2026, 5, 20), 4.75 },
                new object?[] { 2, "Bob", null, null, null, null, 3.20 }
            };

            var headers = new[] { "Id", "Name", "NullableCount", "NullablePrice", "NullableFlag", "NullableDate", "Score" };

            using var ms = new MemoryStream();
            ExcelWriter.WriteRowsToStream(ms, rows, headers);

            ms.Position = 0;
            var result = ExcelReader.Read<ComprehensivePoco>(ms);

            Assert.Equal(2, result.Count);

            // Row 1: all values populated
            Assert.Equal(1, result[0].Id);
            Assert.Equal("Alice", result[0].Name);
            Assert.Equal(10, result[0].NullableCount);
            Assert.Equal(99.50m, result[0].NullablePrice);
            Assert.True(result[0].NullableFlag);
            Assert.Equal(new DateTime(2026, 5, 20), result[0].NullableDate);
            Assert.Equal(4.75, result[0].Score);

            // Row 2: nullables are null
            Assert.Equal(2, result[1].Id);
            Assert.Equal("Bob", result[1].Name);
            Assert.Null(result[1].NullableCount);
            Assert.Null(result[1].NullablePrice);
            Assert.Null(result[1].NullableFlag);
            Assert.Null(result[1].NullableDate);
            Assert.Equal(3.20, result[1].Score);
        }

        [Fact]
        public void Read_ScrambledHeaderOrderAndCaseInsensitivity_ShouldMapCorrectly()
        {
            // Scrambled order: Score, FULLNAME, id
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { 9.8, "Quantum Unit", 42 }
            };

            var headers = new[] { "SCORE", "name", "ID" };

            using var ms = new MemoryStream();
            ExcelWriter.WriteRowsToStream(ms, rows, headers);

            ms.Position = 0;
            var result = ExcelReader.Read<ComprehensivePoco>(ms);

            Assert.Single(result);
            Assert.Equal(42, result[0].Id);
            Assert.Equal("Quantum Unit", result[0].Name);
            Assert.Equal(9.8, result[0].Score);
        }

        [Fact]
        public void Read_ExtraColumnsAndMissingColumns_ShouldHandleGracefully()
        {
            // Contains ExtraCol1, ExtraCol2 not in POCO, and lacks NullablePrice, Score
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { 101, "Test POCO", "Unused 1", "Unused 2" }
            };

            var headers = new[] { "Id", "Name", "ExtraCol1", "ExtraCol2" };

            using var ms = new MemoryStream();
            ExcelWriter.WriteRowsToStream(ms, rows, headers);

            ms.Position = 0;
            var result = ExcelReader.Read<ComprehensivePoco>(ms);

            Assert.Single(result);
            Assert.Equal(101, result[0].Id);
            Assert.Equal("Test POCO", result[0].Name);
            Assert.Null(result[0].NullableCount);
            Assert.Equal(0.0, result[0].Score);
        }

        [Fact]
        public void Read_TypeConversionFallback_InvalidDataDoesNotCrashReader()
        {
            // Row has invalid integer and invalid date
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { "NOT_AN_INT", "Corrupt Row", "INVALID_DATE" },
                new object?[] { 202, "Valid Row", "2026-09-20" }
            };

            var headers = new[] { "Id", "Name", "NullableDate" };

            using var ms = new MemoryStream();
            ExcelWriter.WriteRowsToStream(ms, rows, headers);

            ms.Position = 0;
            var result = ExcelReader.Read<ComprehensivePoco>(ms);

            // Both rows are processed; row 1 retains default Id (0) and null NullableDate
            Assert.Equal(2, result.Count);
            Assert.Equal(0, result[0].Id);
            Assert.Equal("Corrupt Row", result[0].Name);
            Assert.Null(result[0].NullableDate);

            Assert.Equal(202, result[1].Id);
            Assert.Equal("Valid Row", result[1].Name);
            Assert.Equal(new DateTime(2026, 9, 20), result[1].NullableDate);
        }
    }
}
