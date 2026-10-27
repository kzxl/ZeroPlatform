using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Xunit;
using ZeroDocuments.Common;
using ZeroDocuments.Csv;
using ZeroDocuments.Excel;
using ZeroDocuments.Excel.Internal;
using ZeroDocuments.Excel.Models;

namespace ZeroDocuments.Tests
{
    [Collection(GlobalWriterSettingsCollection.Name)]
    public class V15OptimizationsTests
    {
        private static string ReadZipPart(byte[] package, string partPath)
        {
            using var ms = new MemoryStream(package);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry(partPath);
            Assert.NotNull(entry);
            using var reader = new StreamReader(entry!.Open(), Encoding.UTF8);
            return reader.ReadToEnd();
        }

        [Fact]
        public void FormulaInjection_QuotePrefixStyle_ShouldSetQuotePrefixInStylesAndPreserveText()
        {
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { 1, "=SUM(A1:A10)" },
                new object?[] { 2, "+1+1" }
            };

            var options = new ExcelWriterOptions
            {
                FormulaInjection = FormulaInjectionMode.QuotePrefixStyle
            };

            using var ms = new MemoryStream();
            ExcelWriter.WriteRowsToStream(ms, rows, options, new[] { "Id", "Formula" });
            byte[] package = ms.ToArray();

            // 1. styles.xml must contain quotePrefix="1"
            string stylesXml = ReadZipPart(package, "xl/styles.xml");
            Assert.Contains("quotePrefix=\"1\"", stylesXml);

            // 2. sheet1.xml must contain the raw formula without leading apostrophe
            string sheetXml = ReadZipPart(package, "xl/worksheets/sheet1.xml");
            Assert.Contains("<t>=SUM(A1:A10)</t>", sheetXml);
            Assert.Contains("<t>+1+1</t>", sheetXml);
            Assert.DoesNotContain("<t>'=SUM", sheetXml);

            // 3. Read back returns the original text intact
            using var readMs = new MemoryStream(package);
            var readRows = ExcelReader.StreamRows(readMs).ToList();
            Assert.Equal("=SUM(A1:A10)", readRows[1][2]);
            Assert.Equal("+1+1", readRows[2][2]);
        }

        [Fact]
        public void ExcelWriterOptions_PerCall_ShouldNotMutateGlobalState()
        {
            bool previous = ExcelWriter.FormulaInjectionProtection;
            try
            {
                ExcelWriter.FormulaInjectionProtection = true;

                var options = new ExcelWriterOptions
                {
                    FormulaInjection = FormulaInjectionMode.Disabled
                };

                using var ms = new MemoryStream();
                ExcelWriter.WriteRowsToStream(ms, new[] { new object?[] { "=RAW" } }, options);

                // Global state should still be true
                Assert.True(ExcelWriter.FormulaInjectionProtection);

                // Content should be raw
                string sheetXml = ReadZipPart(ms.ToArray(), "xl/worksheets/sheet1.xml");
                Assert.Contains("<t>=RAW</t>", sheetXml);
            }
            finally
            {
                ExcelWriter.FormulaInjectionProtection = previous;
            }
        }

        [Fact]
        public void CsvWriter_InvariantCulture_NumbersUnderEuropeanCulture()
        {
            var oldCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

                var table = new DataTable();
                table.Columns.Add("Id", typeof(int));
                table.Columns.Add("Amount", typeof(decimal));
                table.Columns.Add("Ratio", typeof(double));
                table.Rows.Add(1, 1234.56m, 0.789);

                using var ms = new MemoryStream();
                CsvWriter.WriteToStream(ms, table);

                ms.Position = 0;
                using var reader = new StreamReader(ms, Encoding.UTF8);
                string csv = reader.ReadToEnd();

                // Numbers must use '.' instead of German ','
                Assert.Contains("1234.56", csv);
                Assert.Contains("0.789", csv);
                Assert.DoesNotContain("1234,56", csv);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = oldCulture;
            }
        }

        [Fact]
        public void ExcelReader_DefaultRange_TrimsUnusedColumns()
        {
            var table = new DataTable();
            table.Columns.Add("A", typeof(string));
            table.Columns.Add("B", typeof(string));
            table.Columns.Add("C", typeof(string));
            table.Rows.Add("1", "2", "3");
            table.Rows.Add("4", "5", "6");

            using var ms = new MemoryStream();
            ExcelWriter.WriteToStream(ms, table);

            ms.Position = 0;
            // Read with default range (no explicit range argument)
            var readTable = ExcelReader.ReadToDataTable(ms);

            // Must NOT have 702 or 16384 columns! Only 3 columns for A, B, C!
            Assert.Equal(3, readTable.Columns.Count);
            Assert.Equal("Column_A", readTable.Columns[0].ColumnName);
            Assert.Equal("Column_B", readTable.Columns[1].ColumnName);
            Assert.Equal("Column_C", readTable.Columns[2].ColumnName);
            Assert.Equal(3, readTable.Rows.Count); // Header row + 2 data rows
        }

        [Fact]
        public void ExcelCellAddress_AdvancedRanges()
        {
            // Absolute coordinates
            ExcelCellAddress.ParseCellRange("$A$1:$D$50", out var col1, out var row1, out var col2, out var row2);
            Assert.Equal("A", col1);
            Assert.Equal(1, row1);
            Assert.Equal("D", col2);
            Assert.Equal(50, row2);

            // Whole column range A:C
            ExcelCellAddress.ParseCellRange("A:C", out col1, out row1, out col2, out row2);
            Assert.Equal("A", col1);
            Assert.Equal(1, row1);
            Assert.Equal("C", col2);
            Assert.Equal(1048576, row2);

            // Whole row range 1:5
            ExcelCellAddress.ParseCellRange("1:5", out col1, out row1, out col2, out row2);
            Assert.Equal("A", col1);
            Assert.Equal(1, row1);
            Assert.Equal("XFD", col2);
            Assert.Equal(5, row2);

            // Single absolute cell $B$2
            Assert.True(ExcelCellAddress.TryParseCellReference("$B$2", out var singleCol, out var singleRow));
            Assert.Equal("B", singleCol);
            Assert.Equal(2, singleRow);
        }

        [Fact]
        public void SpreadsheetXml_DecodeXString_HandlesControlEscapes()
        {
            Assert.Equal("Line1\r\nLine2", SpreadsheetXml.DecodeXString("Line1_x000D_\nLine2"));
            Assert.Equal("Tab\tSeparated", SpreadsheetXml.DecodeXString("Tab_x0009_Separated"));
            Assert.Equal("Literal_x000D_Escape", SpreadsheetXml.DecodeXString("Literal_x005F_x000D_Escape"));
            Assert.Equal("Normal Text", SpreadsheetXml.DecodeXString("Normal Text"));
            Assert.Equal("", SpreadsheetXml.DecodeXString(""));
            Assert.Equal("", SpreadsheetXml.DecodeXString(null));
        }

        [Fact]
        public void XlsxPackageWriter_AutoFilter_GeneratesFilterDatabaseDefinedName()
        {
            using var builder = ZeroExcel.Create();
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { "Alpha", 10 },
                new object?[] { "Beta", 20 }
            };

            builder.AddSheet("FilteredSheet", rows, new[] { "Name", "Score" });
            builder.SetAutoFilter("FilteredSheet");

            byte[] package = builder.ToArray();
            string workbookXml = ReadZipPart(package, "xl/workbook.xml");

            Assert.Contains("<definedNames>", workbookXml);
            Assert.Contains("name=\"_xlnm._FilterDatabase\"", workbookXml);
            Assert.Contains("localSheetId=\"0\"", workbookXml);
            Assert.Contains("'FilteredSheet'!$A$1:$B$3", workbookXml);
        }

        [Fact]
        public void AtomicFileWriter_SuccessfulWrite_ReplacesFile()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ZD_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string targetFile = Path.Combine(tempDir, "test.xlsx");
                var table = new DataTable();
                table.Columns.Add("Col1", typeof(int));
                table.Rows.Add(42);

                ExcelWriter.WriteToFile(targetFile, table);

                Assert.True(File.Exists(targetFile));
                var read = ExcelReader.ReadToDataTable(targetFile);
                Assert.Equal(2, read.Rows.Count); // Header + data
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public void AtomicFileWriter_FailedWrite_DeletesTempAndPreservesOriginal()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ZD_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string targetFile = Path.Combine(tempDir, "atomic_test.txt");
                File.WriteAllText(targetFile, "Original Content", Encoding.UTF8);

                Assert.Throws<InvalidOperationException>(() =>
                {
                    AtomicFileWriter.Write(targetFile, stream =>
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes("Partial Content");
                        stream.Write(bytes, 0, bytes.Length);
                        throw new InvalidOperationException("Simulated mid-write crash!");
                    });
                });

                // Original file must be completely untouched
                Assert.Equal("Original Content", File.ReadAllText(targetFile, Encoding.UTF8));

                // No temp files left
                var files = Directory.GetFiles(tempDir, "*.tmp");
                Assert.Empty(files);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public void ZipBomb_ExceedingLimit_ThrowsInvalidDataException()
        {
            var table = new DataTable();
            table.Columns.Add("Test", typeof(string));
            table.Rows.Add(new string('X', 500));

            using var ms = new MemoryStream();
            ExcelWriter.WriteToStream(ms, table);

            ms.Position = 0;
            var options = new ExcelReaderOptions
            {
                MaxUncompressedEntryBytes = 50 // artificially low limit to trigger protection
            };

            Assert.Throws<InvalidDataException>(() =>
            {
                ExcelReader.ReadToDataTable(ms, options);
            });
        }

        [Fact]
        public void ExcelReader_GetSheetNames_ReturnsTabOrder()
        {
            using var builder = ZeroExcel.Create();
            builder.AddSheet("FirstSheet", new[] { new object?[] { 1 } });
            builder.AddSheet("SecondSheet", new[] { new object?[] { 2 } });
            builder.AddSheet("ThirdSheet", new[] { new object?[] { 3 } });

            using var ms = new MemoryStream(builder.ToArray());
            var names = ExcelReader.GetSheetNames(ms);

            Assert.Equal(3, names.Count);
            Assert.Equal("FirstSheet", names[0]);
            Assert.Equal("SecondSheet", names[1]);
            Assert.Equal("ThirdSheet", names[2]);
        }
    }
}
