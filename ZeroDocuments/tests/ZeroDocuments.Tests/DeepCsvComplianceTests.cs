using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using ZeroDocuments.Csv;

namespace ZeroDocuments.Tests
{
    public class DeepCsvComplianceTests
    {
        public class CsvProductDto
        {
            public int Id { get; set; }
            public string Sku { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public decimal UnitPrice { get; set; }
        }

        [Fact]
        public void Csv_EscapedQuotesInVariousPositions_ShouldRoundtripCorrectly()
        {
            var table = new DataTable();
            table.Columns.Add("Col1", typeof(string));
            table.Columns.Add("Col2", typeof(string));
            table.Columns.Add("Col3", typeof(string));

            // Escaped quotes: leading quote, internal quote, trailing quote
            table.Rows.Add("\"Leading quote", "Middle \"quoted\" word", "Trailing quote\"");

            using var ms = new MemoryStream();
            CsvWriter.WriteToStream(ms, table);

            ms.Position = 0;
            var readBack = CsvReader.ReadToDataTable(ms);

            Assert.Single(readBack.Rows);
            Assert.Equal("\"Leading quote", readBack.Rows[0]["Col1"]);
            Assert.Equal("Middle \"quoted\" word", readBack.Rows[0]["Col2"]);
            Assert.Equal("Trailing quote\"", readBack.Rows[0]["Col3"]);
        }

        [Fact]
        public void Csv_MultiLineFields_WithCRLFAndLF_ShouldPreserveLineBreaks()
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(string));
            table.Columns.Add("MultiLine", typeof(string));

            string multiLineWindows = "Line 1\r\nLine 2\r\nLine 3";
            string multiLineUnix = "Alpha\nBeta\nGamma";

            table.Rows.Add("1", multiLineWindows);
            table.Rows.Add("2", multiLineUnix);

            using var ms = new MemoryStream();
            CsvWriter.WriteToStream(ms, table);

            ms.Position = 0;
            var readBack = CsvReader.ReadToDataTable(ms);

            Assert.Equal(2, readBack.Rows.Count);
            Assert.Equal(multiLineWindows, readBack.Rows[0]["MultiLine"]);
            Assert.Equal(multiLineUnix, readBack.Rows[1]["MultiLine"]);
        }

        [Theory]
        [InlineData('\t')]
        [InlineData('|')]
        [InlineData(';')]
        public void Csv_CustomDelimiters_TabPipeSemicolon_ShouldParseCorrectly(char delimiter)
        {
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { 101, "Sensor-Alpha", 98.6 },
                new object?[] { 102, "Sensor-Beta", 102.4 }
            };

            var headers = new[] { "SensorId", "SensorName", "Reading" };

            using var ms = new MemoryStream();
            CsvWriter.WriteRowsToStream(ms, rows, headers, delimiter: delimiter);

            ms.Position = 0;
            var table = CsvReader.ReadToDataTable(ms, delimiter: delimiter);

            Assert.Equal(2, table.Rows.Count);
            Assert.Equal(3, table.Columns.Count);
            Assert.Equal("Sensor-Alpha", table.Rows[0]["SensorName"]);
            Assert.Equal("102.4", table.Rows[1]["Reading"]);
        }

        [Fact]
        public void Csv_ConsecutiveDelimitersAndEmptyFields_ShouldPreserveNulls()
        {
            // CSV with empty fields: A,,C,,,F
            string rawCsv = "Col1,Col2,Col3,Col4,Col5,Col6\r\nVal1,,Val3,,,Val6\r\n";
            using var reader = new StringReader(rawCsv);

            var rows = CsvReader.ReadRows(reader, ',').ToList();

            Assert.Equal(2, rows.Count); // Header + 1 data row
            var dataRow = rows[1];
            Assert.Equal(6, dataRow.Count);
            Assert.Equal("Val1", dataRow[0]);
            Assert.Equal("", dataRow[1]);
            Assert.Equal("Val3", dataRow[2]);
            Assert.Equal("", dataRow[3]);
            Assert.Equal("", dataRow[4]);
            Assert.Equal("Val6", dataRow[5]);
        }

        [Fact]
        public void Csv_Utf8WithBom_ShouldProperlyStripBomFromFirstHeader()
        {
            var utf8Bom = new UTF8Encoding(true);
            byte[] bomBytes = utf8Bom.GetPreamble();
            byte[] csvBytes = utf8Bom.GetBytes("ProductId,Title,Price\r\n1,Widget,19.99\r\n");

            using var ms = new MemoryStream();
            ms.Write(bomBytes, 0, bomBytes.Length);
            ms.Write(csvBytes, 0, csvBytes.Length);

            ms.Position = 0;
            var table = CsvReader.ReadToDataTable(ms);

            Assert.Equal(3, table.Columns.Count);
            // Header should NOT start with \uFEFF
            Assert.Equal("ProductId", table.Columns[0].ColumnName);
            Assert.False(table.Columns[0].ColumnName.StartsWith("\uFEFF", StringComparison.Ordinal));
            Assert.NotEqual('\uFEFF', table.Columns[0].ColumnName[0]);
            Assert.Single(table.Rows);
            Assert.Equal("Widget", table.Rows[0]["Title"]);
        }

        [Fact]
        public void Csv_PocoCollection_WriteAndReadRoundtrip()
        {
            var list = new List<CsvProductDto>
            {
                new CsvProductDto { Id = 1, Sku = "SKU-AAA", Description = "Premium, Gold Edition", UnitPrice = 999.99m },
                new CsvProductDto { Id = 2, Sku = "SKU-BBB", Description = "Standard \"Silver\" Edition", UnitPrice = 499.50m }
            };

            using var ms = new MemoryStream();
            CsvWriter.WriteToStream(ms, list);

            ms.Position = 0;
            var table = CsvReader.ReadToDataTable(ms);

            Assert.Equal(2, table.Rows.Count);
            Assert.Equal("Premium, Gold Edition", table.Rows[0]["Description"]);
            Assert.Equal("Standard \"Silver\" Edition", table.Rows[1]["Description"]);
        }
    }
}
