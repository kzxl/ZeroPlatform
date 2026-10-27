using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Xunit;
using ZeroDocuments.Excel;
using ZeroDocuments.Excel.Models;

namespace ZeroDocuments.Tests
{
    public class DrawingMLAndFormattingTests
    {
        private static readonly byte[] FakePngBytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG Header
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
            0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x10,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0xF3, 0xFF,
            0x61, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, // IEND chunk
            0x44, 0xAE, 0x42, 0x60, 0x82
        };

        private static readonly byte[] FakeJpegBytes = new byte[]
        {
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46,
            0x49, 0x46, 0x00, 0x01, 0x01, 0x01, 0x00, 0x48,
            0x00, 0x48, 0x00, 0x00, 0xFF, 0xD9
        };

        [Fact]
        public void ImageEmbedding_SingleSheet_GeneratesCompliantOpenXmlDrawingML()
        {
            // Arrange
            var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Rows.Add(1, "Alpha");
            table.Rows.Add(2, "Beta");

            using var builder = ZeroExcel.Create();
            builder.AddSheet("Summary", table)
                   .AddImage("Summary", FakePngBytes, "png", column: 4, row: 2, widthPx: 120, heightPx: 60, name: "CompanyLogo");

            var packageBytes = builder.ToArray();
            Assert.NotEmpty(packageBytes);

            // Assert OpenXML Packaging
            using var ms = new MemoryStream(packageBytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

            // 1. Content Types contains drawing override & image defaults
            var contentTypesEntry = zip.GetEntry("[Content_Types].xml");
            Assert.NotNull(contentTypesEntry);
            using (var reader = new StreamReader(contentTypesEntry.Open()))
            {
                var content = reader.ReadToEnd();
                Assert.Contains("Extension=\"png\"", content);
                Assert.Contains("PartName=\"/xl/drawings/drawing1.xml\"", content);
            }

            // 2. Sheet rels points to drawing1.xml
            var sheetRelsEntry = zip.GetEntry("xl/worksheets/_rels/sheet1.xml.rels");
            Assert.NotNull(sheetRelsEntry);
            using (var reader = new StreamReader(sheetRelsEntry.Open()))
            {
                var content = reader.ReadToEnd();
                Assert.Contains("Target=\"../drawings/drawing1.xml\"", content);
                Assert.Contains("Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing\"", content);
            }

            // 3. Drawing XML contains oneCellAnchor and valid EMUs
            var drawingEntry = zip.GetEntry("xl/drawings/drawing1.xml");
            Assert.NotNull(drawingEntry);
            using (var reader = new StreamReader(drawingEntry.Open()))
            {
                var content = reader.ReadToEnd();
                Assert.Contains("CompanyLogo", content);
                // Column 4 (1-based) is col 3 in 0-based DrawingML
                Assert.Contains("<xdr:col>3</xdr:col>", content);
                // Row 2 (1-based) is row 1 in 0-based DrawingML
                Assert.Contains("<xdr:row>1</xdr:row>", content);
                // 120px * 9525 = 1,143,000 EMUs
                Assert.Contains("cx=\"1143000\"", content);
                // 60px * 9525 = 571,500 EMUs
                Assert.Contains("cy=\"571500\"", content);
            }

            // 4. Media entry matches exact payload
            var mediaEntry = zip.GetEntry("xl/media/image1.png");
            Assert.NotNull(mediaEntry);
            using (var mediaStream = mediaEntry.Open())
            using (var mediaMs = new MemoryStream())
            {
                mediaStream.CopyTo(mediaMs);
                Assert.Equal(FakePngBytes, mediaMs.ToArray());
            }

            // 5. ExcelReader reads table rows cleanly without failing on drawing tags
            ms.Position = 0;
            var readTable = ExcelReader.ReadToDataTable(ms);
            Assert.Equal(3, readTable.Rows.Count); // 1 header row + 2 data rows
            Assert.Equal("Alpha", readTable.Rows[1]["Column_B"].ToString());
        }

        [Fact]
        public void ImageEmbedding_MultipleImagesAcrossMultipleSheets_ExtractsProperly()
        {
            using var builder = ZeroExcel.Create();
            var rows1 = new List<IReadOnlyList<object?>>
            {
                new object?[] { "Item A", 100 },
                new object?[] { "Item B", 200 }
            };
            var rows2 = new List<IReadOnlyList<object?>>
            {
                new object?[] { "Report X", 500 }
            };

            builder.AddSheet("SheetA", rows1, new[] { "Name", "Price" })
                   .AddImage("SheetA", FakePngBytes, "png", 3, 1, 80, 80, "IconA")
                   .AddImage("SheetA", FakeJpegBytes, "jpeg", 5, 1, 100, 50, "PhotoA")
                   .AddSheet("SheetB", rows2, new[] { "Title", "Score" })
                   .AddImage("SheetB", FakePngBytes, "png", 2, 3, 150, 75, "IconB");

            var packageBytes = builder.ToArray();

            // Extract images using ExcelReader.ExtractImages
            using var stream = new MemoryStream(packageBytes);
            var extractedImages = ExcelReader.ExtractImages(stream);

            Assert.Equal(3, extractedImages.Count);
            Assert.Equal("png", extractedImages[0].Format);
            Assert.Equal(FakePngBytes, extractedImages[0].Data);
            Assert.Equal("jpeg", extractedImages[1].Format);
            Assert.Equal(FakeJpegBytes, extractedImages[1].Data);
            Assert.Equal("png", extractedImages[2].Format);
            Assert.Equal(FakePngBytes, extractedImages[2].Data);
        }

        [Fact]
        public void ConditionalFormatting_HighlightRules_GeneratesDxfsAndCfRules()
        {
            using var builder = ZeroExcel.Create();
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { "Product 1", 150 },
                new object?[] { "Product 2", 40 },
                new object?[] { "Product 3", 85 }
            };

            builder.AddSheet("Inventory", rows, new[] { "Product", "Stock" })
                   .AddHighlightRule("Inventory", "B2:B4", CellRuleOperator.GreaterThan, "100",
                                     fillColorHex: "C6EFCE", fontColorHex: "006100", bold: true)
                   .AddHighlightRule("Inventory", "B2:B4", CellRuleOperator.LessThan, "50",
                                     fillColorHex: "FFC7CE", fontColorHex: "9C0006", bold: false)
                   .AddHighlightRule("Inventory", "B2:B4", CellRuleOperator.Between, "50", "100",
                                     fillColorHex: "FFEB9C", fontColorHex: "9C6500", bold: false);

            var packageBytes = builder.ToArray();

            using var ms = new MemoryStream(packageBytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

            // 1. Check styles.xml has dxfs count 3 with correct colors
            var stylesEntry = zip.GetEntry("xl/styles.xml");
            Assert.NotNull(stylesEntry);
            using (var reader = new StreamReader(stylesEntry.Open()))
            {
                var xml = reader.ReadToEnd();
                Assert.Contains("<dxfs count=\"3\">", xml);
                Assert.Contains("FFC6EFCE", xml); // Normalized fill
                Assert.Contains("FF006100", xml); // Normalized font
                Assert.Contains("FFFFC7CE", xml); // Normalized red fill
                Assert.Contains("FF9C0006", xml); // Normalized red font
                Assert.Contains("<b", xml);        // Bold tag
            }

            // 2. Check sheet1.xml has conditionalFormatting elements
            var sheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml");
            Assert.NotNull(sheetEntry);
            using (var reader = new StreamReader(sheetEntry.Open()))
            {
                var xml = reader.ReadToEnd();
                Assert.Contains("<conditionalFormatting sqref=\"B2:B4\">", xml);
                Assert.Contains("operator=\"greaterThan\"", xml);
                Assert.Contains("operator=\"lessThan\"", xml);
                Assert.Contains("operator=\"between\"", xml);
                Assert.Contains("<formula>100</formula>", xml);
                Assert.Contains("<formula>50</formula>", xml);
            }

            // 3. Ensure ExcelReader reads rows seamlessly
            ms.Position = 0;
            var dataTable = ExcelReader.ReadToDataTable(ms);
            Assert.Equal(4, dataTable.Rows.Count); // 1 header row + 3 data rows
        }

        [Fact]
        public void ConditionalFormatting_ColorScaleAndDataBar_GeneratesValidRules()
        {
            using var builder = ZeroExcel.Create();
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { 10 },
                new object?[] { 50 },
                new object?[] { 100 }
            };

            builder.AddSheet("Metrics", rows, new[] { "Score" })
                   .AddColorScale("Metrics", "A2:A4", minColorHex: "F8696B", maxColorHex: "63BE7B", midColorHex: "FFEB84")
                   .AddDataBar("Metrics", "A2:A4", colorHex: "638EC6");

            var packageBytes = builder.ToArray();

            using var ms = new MemoryStream(packageBytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

            var sheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml");
            Assert.NotNull(sheetEntry);
            using var reader = new StreamReader(sheetEntry.Open());
            var xml = reader.ReadToEnd();

            // Color scale
            Assert.Contains("type=\"colorScale\"", xml);
            Assert.Contains("<cfvo type=\"percentile\" val=\"50\" />", xml);
            Assert.Contains("FFF8696B", xml);
            Assert.Contains("FFFFEB84", xml);
            Assert.Contains("FF63BE7B", xml);

            // Data bar
            Assert.Contains("type=\"dataBar\"", xml);
            Assert.Contains("FF638EC6", xml);
        }

        [Fact]
        public void AutoFilter_ExplicitAndAutomaticRanges_EmitsValidXml()
        {
            using var builder = ZeroExcel.Create();
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { "Alice", 30 },
                new object?[] { "Bob", 25 }
            };

            builder.AddSheet("People", rows, new[] { "Name", "Age" })
                   .SetAutoFilter("People"); // Auto range

            var packageBytes = builder.ToArray();

            using var ms = new MemoryStream(packageBytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

            var sheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml");
            Assert.NotNull(sheetEntry);
            using var reader = new StreamReader(sheetEntry.Open());
            var xml = reader.ReadToEnd();

            // Auto range: 2 columns (A, B), 2 data rows + 1 header = 3 rows -> A1:B3
            Assert.Contains("<autoFilter ref=\"A1:B3\" />", xml);
        }

        [Fact]
        public void CombinedFeatures_ImageFormattingAndAutoFilter_WorksHarmoniously()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"zero_doc_combined_{Guid.NewGuid():N}.xlsx");
            try
            {
                using var builder = ZeroExcel.Create();
                var rows = new List<IReadOnlyList<object?>>
                {
                    new object?[] { "SKU-101", 1200.50, 45 },
                    new object?[] { "SKU-102", 450.00, 12 },
                    new object?[] { "SKU-103", 890.75, 8 }
                };

                builder.AddSheet("Dashboard", rows, new[] { "SKU", "Revenue", "Stock" })
                       .SetAutoFilter("Dashboard")
                       .AddHighlightRule("Dashboard", "C2:C4", CellRuleOperator.LessThan, "15",
                                         fillColorHex: "FFC7CE", fontColorHex: "9C0006", bold: true)
                       .AddColorScale("Dashboard", "B2:B4")
                       .AddImage("Dashboard", FakePngBytes, "png", column: 5, row: 1, widthPx: 100, heightPx: 50, name: "StatusBadge")
                       .Save(tempFile);

                Assert.True(File.Exists(tempFile));

                // Verify reading back data via POCO
                var items = ExcelReader.Read<DashboardPoco>(tempFile);
                Assert.Equal(3, items.Count);
                Assert.Equal("SKU-101", items[0].SKU);
                Assert.Equal(1200.50, items[0].Revenue);
                Assert.Equal(45, items[0].Stock);

                // Verify image extraction
                var extracted = ExcelReader.ExtractImages(tempFile);
                Assert.Single(extracted);
                Assert.Equal(FakePngBytes, extracted[0].Data);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        private class DashboardPoco
        {
            public string SKU { get; set; } = string.Empty;
            public double Revenue { get; set; }
            public int Stock { get; set; }
        }
    }
}
