using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Xunit;
using ZeroUI.Core.Data;

namespace ZeroUI.Core.Tests
{
    public class GridDataExporterTests
    {
        private class MockDataSource : IZeroVirtualSource
        {
            private readonly string[,] _data;

            public MockDataSource(string[,] data)
            {
                _data = data;
            }

            public int TotalRowCount => _data.GetLength(0);
            public int TotalColumnCount => _data.GetLength(1);

            public void GetCellValue(int rowIndex, int columnIndex, ref CellValueBuffer buffer)
            {
                if (rowIndex >= 0 && rowIndex < _data.GetLength(0) &&
                    columnIndex >= 0 && columnIndex < _data.GetLength(1))
                {
                    buffer.Text = _data[rowIndex, columnIndex].AsSpan();
                }
            }
        }

        [Fact]
        public void GetCellReference_CalculatesExcelCoordinatesCorrectly()
        {
            Assert.Equal("A1", GridDataExporter.GetCellReference(0, 1));
            Assert.Equal("B2", GridDataExporter.GetCellReference(1, 2));
            Assert.Equal("Z10", GridDataExporter.GetCellReference(25, 10));
            Assert.Equal("AA1", GridDataExporter.GetCellReference(26, 1));
            Assert.Equal("AB5", GridDataExporter.GetCellReference(27, 5));
            Assert.Equal("AZ100", GridDataExporter.GetCellReference(51, 100));
            Assert.Equal("BA1", GridDataExporter.GetCellReference(52, 1));
        }

        [Fact]
        public async Task ExportToXlsxAsync_CreatesValidOpenXmlZipPackage()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid():N}.xlsx");
            try
            {
                var columns = new List<ZeroColumn>
                {
                    new ZeroColumn("col1", "Item Code", 100),
                    new ZeroColumn("col2", "Description", 200),
                    new ZeroColumn("col3", "Quantity", 80),
                    new ZeroColumn("col4", "Unit Price", 90),
                    new ZeroColumn("col5", "Hidden Col", 50) { IsVisible = false }
                };

                var data = new string[,]
                {
                    { "ITEM-001", "Bearing Assembly <Grade A>", "150", "45.50", "secret" },
                    { "ITEM-002", "Drive Shaft & Coupler", "25", "1200.00", "secret" },
                    { "ITEM-003", "Standard O-Ring \"Metric\"", "500", "0.75", "secret" }
                };

                var ds = new MockDataSource(data);
                int progressValue = 0;
                var progress = new Progress<int>(p => progressValue = p);

                int exportedRows = await GridDataExporter.ExportToXlsxAsync(
                    ds, columns, 3, tempFile, progress: progress);

                Assert.Equal(3, exportedRows);
                Assert.True(File.Exists(tempFile));
                Assert.True(new FileInfo(tempFile).Length > 0);

                // Verify ZIP structure
                using (var zip = ZipFile.OpenRead(tempFile))
                {
                    Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
                    Assert.NotNull(zip.GetEntry("_rels/.rels"));
                    Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
                    Assert.NotNull(zip.GetEntry("xl/_rels/workbook.xml.rels"));
                    Assert.NotNull(zip.GetEntry("xl/styles.xml"));

                    var sheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml");
                    Assert.NotNull(sheetEntry);

                    using var reader = new StreamReader(sheetEntry!.Open());
                    string sheetXml = await reader.ReadToEndAsync();

                    // Check headers
                    Assert.Contains("Item Code", sheetXml);
                    Assert.Contains("Description", sheetXml);
                    Assert.DoesNotContain("Hidden Col", sheetXml); // Hidden col excluded

                    // Check XML escaped content
                    Assert.Contains("Bearing Assembly &lt;Grade A&gt;", sheetXml);
                    Assert.Contains("Drive Shaft &amp; Coupler", sheetXml);
                    Assert.Contains("Standard O-Ring &quot;Metric&quot;", sheetXml);

                    // Check numeric representation (<v> tag)
                    Assert.Contains("<v>150</v>", sheetXml);
                    Assert.Contains("<v>45.5</v>", sheetXml);
                    Assert.Contains("<v>1200</v>", sheetXml);
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        [Fact]
        public async Task ExportToCsvAsync_GeneratesRfc4180EscapedCsv()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid():N}.csv");
            try
            {
                var columns = new List<ZeroColumn>
                {
                    new ZeroColumn("col1", "Item Code"),
                    new ZeroColumn("col2", "Description"),
                    new ZeroColumn("col3", "Notes")
                };

                var data = new string[,]
                {
                    { "A1", "Simple Item", "No issues" },
                    { "A2", "Item with, comma", "Quoted \"word\" inside" },
                    { "A3", "Line\nBreak", "Regular note" }
                };

                var ds = new MockDataSource(data);
                int exported = await GridDataExporter.ExportToCsvAsync(ds, columns, 3, tempFile);

                Assert.Equal(3, exported);
                string csvContent = File.ReadAllText(tempFile);

                // Verify CSV escaping
                Assert.Contains("\"Item with, comma\"", csvContent);
                Assert.Contains("\"Quoted \"\"word\"\" inside\"", csvContent);
                Assert.Contains("\"Line\nBreak\"", csvContent);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }
    }
}
