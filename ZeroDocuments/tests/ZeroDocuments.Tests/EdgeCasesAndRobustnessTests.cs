using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Xunit;
using ZeroDocuments.Excel;
using ZeroDocuments.Excel.Models;

namespace ZeroDocuments.Tests
{
    public class EdgeCasesAndRobustnessTests
    {
        [Fact]
        public void EmptySheet_ShouldCreateValidExcelWithoutExceptions()
        {
            using var ms = new MemoryStream();
            var emptyRows = new List<IReadOnlyList<object?>>();

            ExcelWriter.WriteRowsToStream(ms, emptyRows, headers: null, sheetName: "EmptySheet");
            Assert.True(ms.Length > 0);

            ms.Position = 0;
            var readRows = ExcelReader.ReadRows(ms, sheetName: "EmptySheet");
            Assert.Empty(readRows);
        }

        [Fact]
        public void SheetWithHeadersOnly_ShouldReadZeroDataRows()
        {
            using var ms = new MemoryStream();
            var emptyRows = new List<IReadOnlyList<object?>>();
            var headers = new[] { "ColA", "ColB", "ColC" };

            ExcelWriter.WriteRowsToStream(ms, emptyRows, headers: headers, sheetName: "HeaderOnly");
            ms.Position = 0;

            var table = ExcelReader.ReadToDataTable(ms, "A2:C2", sheetName: "HeaderOnly");
            Assert.Empty(table.Rows);
        }

        [Fact]
        public void UnicodeAndSpecialXmlCharacters_ShouldPreserveExactContent()
        {
            var headers = new[] { "Language", "SpecialCharacters", "Emojis", "XmlEscapeTest" };
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[]
                {
                    "Tiếng Việt: Hệ thống Quản Lý Kho & Xưởng Sản Xuất Tự Động (v1.1.0)",
                    "Test \"quotes\", 'apostrophes', & ampersands, <tags>, /slashes\\",
                    "🚀 ⚡ 📦 🤖 🏭 🛡️ 🎨",
                    "<script>alert(\"XSS & Injection\");</script>"
                },
                new object?[]
                {
                    "日本語: 自動化マテリアルハンドリングシステム",
                    "Special: \t tabs \n newlines \r carriage",
                    "🎌 🏯 ⛩️ 🍣",
                    "<![CDATA[Raw Unescaped Block & Test]]>"
                },
                new object?[]
                {
                    "中文: 智能工业自动化立体仓储与视觉检测系统",
                    "Symbols: © ® ™ § ¶ € ¥ £ ₫",
                    "🇨🇳 🏭 🧪",
                    "<xml attr=\"val\">inner & value</xml>"
                }
            };

            using var ms = new MemoryStream();
            ExcelWriter.WriteRowsToStream(ms, rows, headers, sheetName: "UnicodeTest");
            ms.Position = 0;

            var readTable = ExcelReader.ReadToDataTable(ms, "A2:D4", sheetName: "UnicodeTest");
            Assert.Equal(3, readTable.Rows.Count);

            // Row 1 Assertions
            Assert.Equal(rows[0][0]?.ToString(), readTable.Rows[0][0]?.ToString());
            Assert.Equal(rows[0][1]?.ToString(), readTable.Rows[0][1]?.ToString());
            Assert.Equal(rows[0][2]?.ToString(), readTable.Rows[0][2]?.ToString());
            Assert.Equal(rows[0][3]?.ToString(), readTable.Rows[0][3]?.ToString());

            // Row 2 Assertions (Japanese)
            Assert.Equal(rows[1][0]?.ToString(), readTable.Rows[1][0]?.ToString());

            // Row 3 Assertions (Chinese & Symbols)
            Assert.Equal(rows[2][0]?.ToString(), readTable.Rows[2][0]?.ToString());
            Assert.Equal(rows[2][1]?.ToString(), readTable.Rows[2][1]?.ToString());
        }

        [Fact]
        public void ExtremelyLongText_ShouldWriteAndReadAccurately()
        {
            string longText = new string('Z', 8192); // 8KB string in a single cell

            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { 1, longText }
            };

            using var ms = new MemoryStream();
            ExcelWriter.WriteRowsToStream(ms, rows, new[] { "ID", "Payload" }, "LargeCell");
            ms.Position = 0;

            var readRows = ExcelReader.ReadRows(ms, "A2:B2", "LargeCell");
            Assert.Single(readRows);
            Assert.Equal(longText, readRows[0][2]);
        }

        [Fact]
        public void NumericBoundaryValues_ShouldMaintainPrecision()
        {
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[]
                {
                    int.MaxValue,
                    int.MinValue,
                    long.MaxValue,
                    long.MinValue,
                    123456789.987654m,
                    0.00000123456
                }
            };

            using var ms = new MemoryStream();
            ExcelWriter.WriteRowsToStream(ms, rows, new[] { "IntMax", "IntMin", "LongMax", "LongMin", "DecimalVal", "DoubleVal" }, "Numbers");
            ms.Position = 0;

            var readRows = ExcelReader.ReadRows(ms, "A2:F2", "Numbers");
            Assert.Single(readRows);

            Assert.Equal(int.MaxValue.ToString(), readRows[0][1]);
            Assert.Equal(int.MinValue.ToString(), readRows[0][2]);
            Assert.Equal(long.MaxValue.ToString(), readRows[0][3]);
            Assert.Equal(long.MinValue.ToString(), readRows[0][4]);
            Assert.Contains("123456789.987654", readRows[0][5] ?? "");
        }

        [Fact]
        public void SheetNameSanitization_ShouldHandleForbiddenCharsAndLength()
        {
            using var wb = ZeroExcel.Create();

            // Name with illegal characters: \ / ? * : [ ] and > 31 characters
            string forbiddenName = "Very/Long:Sheet*Name[With]?Forbidden\\CharactersAndExtraLengthExceeding31";
            var table = new DataTable("Test");
            table.Columns.Add("TestCol");
            table.Rows.Add("Val1");

            wb.AddSheet(forbiddenName, table);

            byte[] bytes = wb.ToArray();
            Assert.NotEmpty(bytes);

            using var ms = new MemoryStream(bytes);
            // Must be readable without corruption
            var rows = ExcelReader.ReadRows(ms);
            Assert.NotEmpty(rows);
        }

        [Fact]
        public void SparseRowsWithGaps_ShouldReadCorrectCoordinates()
        {
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { "ColA_Val", null, null, null, "ColE_Val" } // Col 1 and Col 5 populated
            };

            using var ms = new MemoryStream();
            ExcelWriter.WriteRowsToStream(ms, rows, new[] { "A", "B", "C", "D", "E" }, "Sparse");
            ms.Position = 0;

            var readRows = ExcelReader.ReadRows(ms, "A2:E2", "Sparse");
            Assert.Single(readRows);
            Assert.Equal("ColA_Val", readRows[0][1]);
            Assert.Null(readRows[0][2]);
            Assert.Null(readRows[0][3]);
            Assert.Null(readRows[0][4]);
            Assert.Equal("ColE_Val", readRows[0][5]);
        }
    }
}
