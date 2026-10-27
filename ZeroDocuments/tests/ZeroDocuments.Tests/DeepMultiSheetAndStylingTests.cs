using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Xunit;
using ZeroDocuments.Excel;

namespace ZeroDocuments.Tests
{
    public class DeepMultiSheetAndStylingTests
    {
        public class SampleProduct
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal Price { get; set; }
        }

        [Fact]
        public void MultiSheet_10HeterogeneousSheets_ShouldWriteAndStreamReadIndependently()
        {
            var builder = ZeroExcel.Create();

            // Create 10 sheets with distinct data types
            for (int i = 1; i <= 10; i++)
            {
                if (i % 2 == 0)
                {
                    // POCO sheet
                    var pocoList = new List<SampleProduct>
                    {
                        new SampleProduct { Id = i, Name = $"Product_Sheet_{i}", Price = i * 10.5m }
                    };
                    builder.AddSheet($"PocoSheet_{i}", pocoList, headerBold: true);
                }
                else
                {
                    // 2D grid sheet
                    var rows = new List<IReadOnlyList<object?>>
                    {
                        new object?[] { i, $"Raw_Row_{i}", i * 100 }
                    };
                    builder.AddSheet($"GridSheet_{i}", rows, new[] { "Index", "Title", "Score" }, headerBold: false);
                }
            }

            using var ms = new MemoryStream();
            builder.Save(ms);

            Assert.True(ms.Length > 0);

            // Read back specific sheets by name
            ms.Position = 0;
            var sheet4Rows = ExcelReader.Read<SampleProduct>(ms, sheetName: "PocoSheet_4");
            Assert.Single(sheet4Rows);
            Assert.Equal(4, sheet4Rows[0].Id);
            Assert.Equal("Product_Sheet_4", sheet4Rows[0].Name);

            ms.Position = 0;
            var sheet7Rows = ExcelReader.StreamRows(ms, sheetName: "GridSheet_7").ToList();
            Assert.Equal(2, sheet7Rows.Count); // 1 header + 1 data row
            Assert.Equal("Raw_Row_7", sheet7Rows[1][2]);
        }

        [Fact]
        public void MultiSheet_DuplicateSheetNames_ShouldDeduplicateGracefully()
        {
            var builder = ZeroExcel.Create();

            // Add 3 sheets with the exact same name
            builder.AddSheet("Inventory", new[] { new object?[] { 1, "Item A" } }, new[] { "Id", "Name" });
            builder.AddSheet("Inventory", new[] { new object?[] { 2, "Item B" } }, new[] { "Id", "Name" });
            builder.AddSheet("Inventory", new[] { new object?[] { 3, "Item C" } }, new[] { "Id", "Name" });

            using var ms = new MemoryStream();
            builder.Save(ms);

            Assert.True(ms.Length > 0);

            // Read back the first sheet
            ms.Position = 0;
            var sheet1Rows = ExcelReader.StreamRows(ms, sheetName: "Inventory").ToList();
            Assert.Equal(2, sheet1Rows.Count);
            Assert.Equal("Item A", sheet1Rows[1][2]);

            // Read back the deduplicated second sheet
            ms.Position = 0;
            var sheet2Rows = ExcelReader.StreamRows(ms, sheetName: "Inventory_1").ToList();
            Assert.Equal(2, sheet2Rows.Count);
            Assert.Equal("Item B", sheet2Rows[1][2]);

            // Read back the deduplicated third sheet
            ms.Position = 0;
            var sheet3Rows = ExcelReader.StreamRows(ms, sheetName: "Inventory_2").ToList();
            Assert.Equal(2, sheet3Rows.Count);
            Assert.Equal("Item C", sheet3Rows[1][2]);
        }

        [Fact]
        public void MultiSheet_SpecialAndUnicodeSheetNames_ShouldSanitizeAndPreserve()
        {
            var builder = ZeroExcel.Create();

            builder.AddSheet("Báo cáo Kho 2026", new[] { new object?[] { 101, "Sản phẩm A" } }, new[] { "Mã", "Tên" });
            builder.AddSheet("Tokyo 東京工場", new[] { new object?[] { 202, "部品 X" } }, new[] { "ID", "名前" });
            builder.AddSheet("Invalid:Chars/Here?*", new[] { new object?[] { 303, "Cleaned" } }, new[] { "ID", "Status" });

            using var ms = new MemoryStream();
            builder.Save(ms);

            Assert.True(ms.Length > 0);

            ms.Position = 0;
            var vnRows = ExcelReader.StreamRows(ms, sheetName: "Báo cáo Kho 2026").ToList();
            Assert.Equal(2, vnRows.Count);
            Assert.Equal("Sản phẩm A", vnRows[1][2]);

            ms.Position = 0;
            var jpRows = ExcelReader.StreamRows(ms, sheetName: "Tokyo 東京工場").ToList();
            Assert.Equal(2, jpRows.Count);
            Assert.Equal("部品 X", jpRows[1][2]);

            // Forbidden characters [\/:*] sanitized to underscores
            ms.Position = 0;
            var cleanRows = ExcelReader.StreamRows(ms, sheetName: "Invalid_Chars_Here__").ToList();
            Assert.Equal(2, cleanRows.Count);
            Assert.Equal("Cleaned", cleanRows[1][2]);
        }
    }
}
