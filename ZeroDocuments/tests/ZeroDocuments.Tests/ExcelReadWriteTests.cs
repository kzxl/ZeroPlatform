using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Xunit;
using ZeroDocuments.Excel;
using ZeroDocuments.Excel.Models;

namespace ZeroDocuments.Tests
{
    public class ExcelReadWriteTests
    {
        public class SampleItem
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public bool IsActive { get; set; }
        }

        [Fact]
        public void WriteToStream_And_ReadToDataTable_ShouldRoundTripAccurately()
        {
            // 1. Prepare DataTable with Vietnamese text, special characters, numbers, booleans
            var originalTable = new DataTable("TestTable");
            originalTable.Columns.Add("Code", typeof(string));
            originalTable.Columns.Add("Description", typeof(string));
            originalTable.Columns.Add("Quantity", typeof(int));
            originalTable.Columns.Add("UnitPrice", typeof(double));
            originalTable.Columns.Add("InStock", typeof(bool));

            originalTable.Rows.Add("VT001", "Thép cuộn mạ kẽm & nhôm <loại 1>", 150, 1250000.50, true);
            originalTable.Rows.Add("VT002", "Bu-lông inox 304 \"tiêu chuẩn\"", 2000, 450.75, false);
            originalTable.Rows.Add("VT003", "Đệm cao su chịu nhiệt (Ø50)", 50, 85000.0, true);

            // 2. Write to MemoryStream
            using var ms = new MemoryStream();
            ExcelWriter.WriteToStream(ms, originalTable, "MaterialList", includeHeaders: true);
            ms.Position = 0;

            // 3. Read back using ExcelReader
            // Header is row 1 (A1:E1), data starts from row 2 (A2:E4)
            var readTable = ExcelReader.ReadToDataTable(ms, "A2:E4", "MaterialList");

            // 4. Assert
            Assert.Equal(3, readTable.Rows.Count);
            Assert.Equal(5, readTable.Columns.Count);

            // Row 1
            Assert.Equal("VT001", readTable.Rows[0][0]?.ToString());
            Assert.Equal("Thép cuộn mạ kẽm & nhôm <loại 1>", readTable.Rows[0][1]?.ToString());
            Assert.Equal("150", readTable.Rows[0][2]?.ToString());
            Assert.Contains("1250000.5", readTable.Rows[0][3]?.ToString() ?? "");
            Assert.Equal("TRUE", readTable.Rows[0][4]?.ToString());

            // Row 2
            Assert.Equal("VT002", readTable.Rows[1][0]?.ToString());
            Assert.Equal("Bu-lông inox 304 \"tiêu chuẩn\"", readTable.Rows[1][1]?.ToString());
            Assert.Equal("2000", readTable.Rows[1][2]?.ToString());
            Assert.Equal("FALSE", readTable.Rows[1][4]?.ToString());
        }

        [Fact]
        public void WriteCollection_And_ReadRows_ShouldSucceed()
        {
            var items = new List<SampleItem>
            {
                new() { Id = 1, Name = "Vật tư A", Price = 100.5m, IsActive = true },
                new() { Id = 2, Name = "Vật tư B", Price = 250.0m, IsActive = false }
            };

            using var ms = new MemoryStream();
            ExcelWriter.WriteToStream(ms, items, "Items", includeHeaders: true);
            ms.Position = 0;

            // Read rows
            var rows = ExcelReader.ReadRows(ms, "A1:D3", "Items");

            // 3 rows: 1 header + 2 data
            Assert.Equal(3, rows.Count);

            // Header row
            Assert.Equal("Id", rows[0][1]);
            Assert.Equal("Name", rows[0][2]);
            Assert.Equal("Price", rows[0][3]);
            Assert.Equal("IsActive", rows[0][4]);

            // Data row 1
            Assert.Equal("1", rows[1][1]);
            Assert.Equal("Vật tư A", rows[1][2]);
            Assert.Equal("100.5", rows[1][3]);
            Assert.Equal("TRUE", rows[1][4]);
        }

        [Fact]
        public void ReadByHeaderRange_ShouldReadBelowHeader()
        {
            var table = new DataTable();
            table.Columns.Add("ColA", typeof(string));
            table.Columns.Add("ColB", typeof(string));
            table.Rows.Add("HeaderA", "HeaderB"); // Row 1: Header
            table.Rows.Add("ValA1", "ValB1");     // Row 2: Data 1
            table.Rows.Add("ValA2", "ValB2");     // Row 3: Data 2

            string tempFile = Path.Combine(Path.GetTempPath(), $"zero_doc_test_{Guid.NewGuid():N}.xlsx");
            try
            {
                ExcelWriter.WriteToFile(tempFile, table, "Sheet1", includeHeaders: false);

                // Use header range A1:B1 to read data downward
                var result = ExcelReader.ReadByHeaderRange(tempFile, "A1:B1", maxRows: 10, sheetName: "Sheet1");

                Assert.Equal(2, result.Rows.Count);
                Assert.Equal("ValA1", result.Rows[0][0]?.ToString());
                Assert.Equal("ValB1", result.Rows[0][1]?.ToString());
                Assert.Equal("ValA2", result.Rows[1][0]?.ToString());
                Assert.Equal("ValB2", result.Rows[1][1]?.ToString());
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}
