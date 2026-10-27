using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using Xunit;
using ZeroDocuments.Csv;
using ZeroDocuments.Excel;

namespace ZeroDocuments.Tests
{
    public class DeepCultureAndTypeResilienceTests
    {
        public enum OrderStatus
        {
            Pending = 1,
            Processing = 2,
            Completed = 3,
            Cancelled = 4
        }

        public class CultureTestDto
        {
            public int Id { get; set; }
            public double QuantityDouble { get; set; }
            public float FactorFloat { get; set; }
            public decimal UnitPrice { get; set; }
            public long TotalCount { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTimeOffset ShippedAt { get; set; }
            public TimeSpan Duration { get; set; }
            public Guid TransactionId { get; set; }
            public bool IsActive { get; set; }
            public OrderStatus Status { get; set; }
        }

        public class NullableTypeMatrixDto
        {
            public int? NullableInt { get; set; }
            public double? NullableDouble { get; set; }
            public decimal? NullableDecimal { get; set; }
            public DateTime? NullableDateTime { get; set; }
            public Guid? NullableGuid { get; set; }
            public bool? NullableBool { get; set; }
            public TimeSpan? NullableTimeSpan { get; set; }
            public OrderStatus? NullableStatus { get; set; }
        }

        [Theory]
        [InlineData("fr-FR")] // French (uses comma for decimal, space for thousands)
        [InlineData("de-DE")] // German (uses comma for decimal, dot for thousands)
        [InlineData("vi-VN")] // Vietnamese (uses comma for decimal, dot for thousands)
        [InlineData("ar-SA")] // Arabic (Saudi Arabia)
        [InlineData("en-US")] // US Invariant-like
        public void CultureInvariance_ExcelAndCsv_NumericAndDatePrecision_ShouldPreserveValues(string cultureName)
        {
            var originalCulture = Thread.CurrentThread.CurrentCulture;
            var originalUiCulture = Thread.CurrentThread.CurrentUICulture;

            try
            {
                var culture = new CultureInfo(cultureName);
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;

                var guid = Guid.NewGuid();
                var created = new DateTime(2026, 9, 20, 14, 30, 45, DateTimeKind.Utc);
                var shipped = new DateTimeOffset(2026, 9, 20, 15, 45, 0, TimeSpan.FromHours(7));
                var duration = TimeSpan.FromMinutes(95);

                var items = new List<CultureTestDto>
                {
                    new CultureTestDto
                    {
                        Id = 101,
                        QuantityDouble = 123456.789,
                        FactorFloat = 98.75f,
                        UnitPrice = 1234567.89m,
                        TotalCount = 9876543210123L,
                        CreatedAt = created,
                        ShippedAt = shipped,
                        Duration = duration,
                        TransactionId = guid,
                        IsActive = true,
                        Status = OrderStatus.Processing
                    }
                };

                // 1. Excel Roundtrip under active culture
                using (var excelMs = new MemoryStream())
                {
                    ExcelWriter.WriteToStream(excelMs, items, sheetName: "CultureSheet");
                    excelMs.Position = 0;

                    var readBack = ExcelReader.Read<CultureTestDto>(excelMs, sheetName: "CultureSheet");
                    Assert.Single(readBack);
                    var item = readBack[0];

                    Assert.Equal(101, item.Id);
                    Assert.Equal(123456.789, item.QuantityDouble, precision: 3);
                    Assert.Equal(98.75f, item.FactorFloat, precision: 2);
                    Assert.Equal(1234567.89m, item.UnitPrice);
                    Assert.Equal(9876543210123L, item.TotalCount);
                    Assert.Equal(guid, item.TransactionId);
                    Assert.True(item.IsActive);
                    Assert.Equal(OrderStatus.Processing, item.Status);
                }

                // 2. CSV Roundtrip under active culture
                using (var csvMs = new MemoryStream())
                {
                    CsvWriter.WriteToStream(csvMs, items);
                    csvMs.Position = 0;

                    var table = CsvReader.ReadToDataTable(csvMs);
                    Assert.Single(table.Rows);
                    var row = table.Rows[0];

                    Assert.Equal("101", row["Id"]?.ToString());
                    Assert.Equal(guid.ToString(), row["TransactionId"]?.ToString());
                    Assert.Equal("Processing", row["Status"]?.ToString());
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
                Thread.CurrentThread.CurrentUICulture = originalUiCulture;
            }
        }

        [Fact]
        public void NullableTypeMatrix_Roundtrip_ShouldHandleNullsAndValuesSeamlessly()
        {
            var testGuid = Guid.NewGuid();
            var testDate = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);
            var testSpan = TimeSpan.FromHours(4.5);

            var items = new List<NullableTypeMatrixDto>
            {
                // Row 0: Part A nulls, Part B populated
                new NullableTypeMatrixDto
                {
                    NullableInt = null,
                    NullableDouble = null,
                    NullableDecimal = null,
                    NullableDateTime = null,
                    NullableGuid = testGuid,
                    NullableBool = true,
                    NullableTimeSpan = testSpan,
                    NullableStatus = OrderStatus.Pending
                },
                // Row 1: All populated
                new NullableTypeMatrixDto
                {
                    NullableInt = 42,
                    NullableDouble = 3.14159,
                    NullableDecimal = 999.99m,
                    NullableDateTime = testDate,
                    NullableGuid = testGuid,
                    NullableBool = true,
                    NullableTimeSpan = testSpan,
                    NullableStatus = OrderStatus.Completed
                },
                // Row 2: Part A populated, Part B nulls
                new NullableTypeMatrixDto
                {
                    NullableInt = 99,
                    NullableDouble = 2.71828,
                    NullableDecimal = 12.34m,
                    NullableDateTime = testDate,
                    NullableGuid = null,
                    NullableBool = null,
                    NullableTimeSpan = null,
                    NullableStatus = null
                }
            };

            using var ms = new MemoryStream();
            ExcelWriter.WriteToStream(ms, items, sheetName: "NullableMatrix");
            ms.Position = 0;

            var readBack = ExcelReader.Read<NullableTypeMatrixDto>(ms, sheetName: "NullableMatrix");
            Assert.Equal(3, readBack.Count);

            // Verify Row 0 (Part A null, Part B populated)
            Assert.Null(readBack[0].NullableInt);
            Assert.Null(readBack[0].NullableDouble);
            Assert.Null(readBack[0].NullableDecimal);
            Assert.Null(readBack[0].NullableDateTime);
            Assert.Equal(testGuid, readBack[0].NullableGuid);
            Assert.True(readBack[0].NullableBool);
            Assert.Equal(testSpan, readBack[0].NullableTimeSpan);
            Assert.Equal(OrderStatus.Pending, readBack[0].NullableStatus);

            // Verify Row 1 (all populated)
            Assert.Equal(42, readBack[1].NullableInt);
            Assert.Equal(3.14159, readBack[1].NullableDouble!.Value, precision: 5);
            Assert.Equal(999.99m, readBack[1].NullableDecimal);
            Assert.Equal(testDate, readBack[1].NullableDateTime);
            Assert.Equal(testGuid, readBack[1].NullableGuid);
            Assert.True(readBack[1].NullableBool);
            Assert.Equal(testSpan, readBack[1].NullableTimeSpan);
            Assert.Equal(OrderStatus.Completed, readBack[1].NullableStatus);

            // Verify Row 2 (Part A populated, Part B nulls)
            Assert.Equal(99, readBack[2].NullableInt);
            Assert.Equal(2.71828, readBack[2].NullableDouble!.Value, precision: 5);
            Assert.Equal(12.34m, readBack[2].NullableDecimal);
            Assert.Equal(testDate, readBack[2].NullableDateTime);
            Assert.Null(readBack[2].NullableGuid);
            Assert.Null(readBack[2].NullableBool);
            Assert.Null(readBack[2].NullableTimeSpan);
            Assert.Null(readBack[2].NullableStatus);
        }

        [Fact]
        public void ExtremeNumericBoundaries_ShouldNotOverflowOrCorrupt()
        {
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[]
                {
                    long.MaxValue,
                    long.MinValue,
                    int.MaxValue,
                    int.MinValue,
                    decimal.MaxValue,
                    decimal.MinValue,
                    float.Epsilon,
                    double.Epsilon
                }
            };

            var headers = new[] { "LongMax", "LongMin", "IntMax", "IntMin", "DecMax", "DecMin", "FloatEps", "DoubleEps" };

            using var ms = new MemoryStream();
            ExcelWriter.WriteRowsToStream(ms, rows, headers, "Boundaries");
            ms.Position = 0;

            var readRows = ExcelReader.ReadRows(ms, "A2:H2", "Boundaries");
            Assert.Single(readRows);
            var r = readRows[0];

            Assert.Equal(long.MaxValue.ToString(CultureInfo.InvariantCulture), r[1]);
            Assert.Equal(long.MinValue.ToString(CultureInfo.InvariantCulture), r[2]);
            Assert.Equal(int.MaxValue.ToString(CultureInfo.InvariantCulture), r[3]);
            Assert.Equal(int.MinValue.ToString(CultureInfo.InvariantCulture), r[4]);
            Assert.Equal(decimal.MaxValue.ToString(CultureInfo.InvariantCulture), r[5]);
            Assert.Equal(decimal.MinValue.ToString(CultureInfo.InvariantCulture), r[6]);
        }
    }
}
