using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Xunit;
using ZeroDocuments.Csv;
using ZeroDocuments.Excel;

namespace ZeroDocuments.Tests
{
    [Collection(GlobalWriterSettingsCollection.Name)]
    public class AdvancedFeaturesTests
    {
        public class SampleProduct
        {
            public int Id { get; set; }
            public string Sku { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public bool InStock { get; set; }
        }

        [Fact]
        public void MultiSheetWorkbook_ShouldWriteAndReadIndependentSheets()
        {
            using var wb = ZeroExcel.Create();

            // Sheet 1: DataTable
            var table1 = new DataTable("Summary");
            table1.Columns.Add("Metric", typeof(string));
            table1.Columns.Add("Value", typeof(int));
            table1.Rows.Add("TotalSales", 1500);
            table1.Rows.Add("ActiveUsers", 320);
            wb.AddSheet("SummarySheet", table1);

            // Sheet 2: POCO Collection
            var products = new List<SampleProduct>
            {
                new SampleProduct { Id = 101, Sku = "SKU-A", Title = "Sensor Node Pro", Price = 45.99m, InStock = true },
                new SampleProduct { Id = 102, Sku = "SKU-B", Title = "Gateway 4G", Price = 120.50m, InStock = false }
            };
            wb.AddSheet("ProductCatalog", products);

            // Sheet 3: Raw 2D Grid
            var rawRows = new List<IReadOnlyList<object?>>
            {
                new object?[] { "Log 1", "2026-09-20", "OK" },
                new object?[] { "Log 2", "2026-09-21", "WARNING" }
            };
            wb.AddSheet("AuditLogs", rawRows, new[] { "Event", "Date", "Status" });

            // Save to memory
            byte[] packageBytes = wb.ToArray();
            Assert.NotEmpty(packageBytes);

            using var ms = new MemoryStream(packageBytes);

            // Read Sheet 1
            var dtSummary = ExcelReader.ReadToDataTable(ms, "A2:B3", "SummarySheet");
            Assert.Equal(2, dtSummary.Rows.Count);
            Assert.Equal("TotalSales", dtSummary.Rows[0][0]?.ToString());
            Assert.Equal("1500", dtSummary.Rows[0][1]?.ToString());

            // Read Sheet 2 via POCO Mapper
            ms.Position = 0;
            var readProducts = ExcelReader.Read<SampleProduct>(ms, "A1:E3", "ProductCatalog");
            Assert.Equal(2, readProducts.Count);
            Assert.Equal(101, readProducts[0].Id);
            Assert.Equal("SKU-A", readProducts[0].Sku);
            Assert.Equal("Sensor Node Pro", readProducts[0].Title);
            Assert.Equal(45.99m, readProducts[0].Price);
            Assert.True(readProducts[0].InStock);
            Assert.Equal(102, readProducts[1].Id);
            Assert.False(readProducts[1].InStock);

            // Read Sheet 3
            ms.Position = 0;
            var dtLogs = ExcelReader.ReadToDataTable(ms, "A2:C3", "AuditLogs");
            Assert.Equal(2, dtLogs.Rows.Count);
            Assert.Equal("Log 1", dtLogs.Rows[0][0]?.ToString());
            Assert.Equal("WARNING", dtLogs.Rows[1][2]?.ToString());
        }

        [Fact]
        public void StreamingXmlReader_ShouldStreamLargeDatasetAccurately()
        {
            // Prepare 1,000 rows
            const int count = 1000;
            var rows = new List<IReadOnlyList<object?>>(count);
            for (int i = 1; i <= count; i++)
            {
                rows.Add(new object?[] { i, $"Item_{i}", i * 1.5 });
            }

            using var ms = new MemoryStream();
            ExcelWriter.WriteRowsToStream(ms, rows, new[] { "ID", "Name", "Cost" }, "StreamSheet");
            ms.Position = 0;

            // Stream rows lazily
            var streamed = ExcelReader.StreamRows(ms, "A2:C1001", "StreamSheet").ToList();
            Assert.Equal(count, streamed.Count);
            Assert.Equal("1", streamed[0][1]);
            Assert.Equal("Item_1", streamed[0][2]);
            Assert.Equal(count.ToString(), streamed[count - 1][1]);
            Assert.Equal($"Item_{count}", streamed[count - 1][2]);
        }

        [Fact]
        public void FormulaInjectionProtection_ShouldSanitizeUnsafeInputs()
        {
            // 1. Excel Formula Injection Test
            var unsafeRows = new List<IReadOnlyList<object?>>
            {
                new object?[] { "=cmd|' /C calc'!A0", "@SUM(A1:A10)", "-20", "+40", "Normal Text" }
            };

            using var excelMs = new MemoryStream();
            ExcelWriter.FormulaInjectionProtection = true;
            ExcelWriter.WriteRowsToStream(excelMs, unsafeRows, new[] { "C1", "C2", "C3", "C4", "C5" }, "SafeSheet");
            excelMs.Position = 0;

            var readTable = ExcelReader.ReadToDataTable(excelMs, "A2:E2", "SafeSheet");
            Assert.Equal("'=cmd|' /C calc'!A0", readTable.Rows[0][0]?.ToString());
            Assert.Equal("'@SUM(A1:A10)", readTable.Rows[0][1]?.ToString());
            Assert.Equal("'-20", readTable.Rows[0][2]?.ToString());
            Assert.Equal("'+40", readTable.Rows[0][3]?.ToString());
            Assert.Equal("Normal Text", readTable.Rows[0][4]?.ToString());

            // 2. CSV Formula Injection Test
            using var csvMs = new MemoryStream();
            CsvWriter.FormulaInjectionProtection = true;
            CsvWriter.WriteRowsToStream(csvMs, unsafeRows, new[] { "C1", "C2", "C3", "C4", "C5" });
            csvMs.Position = 0;

            var csvTable = CsvReader.ReadToDataTable(csvMs, ',');
            Assert.Equal("'=cmd|' /C calc'!A0", csvTable.Rows[0][0]?.ToString());
            Assert.Equal("'@SUM(A1:A10)", csvTable.Rows[0][1]?.ToString());
        }
    }
}
