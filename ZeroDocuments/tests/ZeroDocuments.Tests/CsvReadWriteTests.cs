using System;
using System.Data;
using System.IO;
using Xunit;
using ZeroDocuments.Csv;

namespace ZeroDocuments.Tests
{
    public class CsvReadWriteTests
    {
        [Fact]
        public void WriteToStream_And_ReadToDataTable_ShouldHandleQuotesAndCommas()
        {
            var originalTable = new DataTable("CsvTest");
            originalTable.Columns.Add("SKU", typeof(string));
            originalTable.Columns.Add("Title", typeof(string));
            originalTable.Columns.Add("Notes", typeof(string));

            originalTable.Rows.Add("SKU-01", "Dây cáp, mạng Cat6", "Ghi chú: \"Hàng đặc biệt\"");
            originalTable.Rows.Add("SKU-02", "Jack RJ45", "Dòng 1\r\nDòng 2");

            using var ms = new MemoryStream();
            CsvWriter.WriteToStream(ms, originalTable, delimiter: ',', includeHeaders: true);
            ms.Position = 0;

            var readTable = CsvReader.ReadToDataTable(ms, delimiter: ',', hasHeader: true);

            Assert.Equal(2, readTable.Rows.Count);
            Assert.Equal(3, readTable.Columns.Count);

            // Row 1
            Assert.Equal("SKU-01", readTable.Rows[0]["SKU"]?.ToString());
            Assert.Equal("Dây cáp, mạng Cat6", readTable.Rows[0]["Title"]?.ToString());
            Assert.Equal("Ghi chú: \"Hàng đặc biệt\"", readTable.Rows[0]["Notes"]?.ToString());

            // Row 2
            Assert.Equal("SKU-02", readTable.Rows[1]["SKU"]?.ToString());
            Assert.Equal("Jack RJ45", readTable.Rows[1]["Title"]?.ToString());
            Assert.Equal("Dòng 1\r\nDòng 2", readTable.Rows[1]["Notes"]?.ToString());
        }

        [Fact]
        public void ReadRows_WithSemicolonDelimiter_ShouldParseCorrectly()
        {
            string csvContent = "ID;Name;Price\r\n1;Sản phẩm A;1200\r\n2;Sản phẩm B;4500";
            using var reader = new StringReader(csvContent);

            var rows = new System.Collections.Generic.List<System.Collections.Generic.IReadOnlyList<string>>(
                CsvReader.ReadRows(reader, delimiter: ';'));

            Assert.Equal(3, rows.Count);
            Assert.Equal("Sản phẩm A", rows[1][1]);
            Assert.Equal("4500", rows[2][2]);
        }
    }
}
