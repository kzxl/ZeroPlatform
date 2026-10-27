using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using Xunit;
using ZeroDocuments.Csv;
using ZeroDocuments.Excel;
using ZeroDocuments.Excel.Models;

namespace ZeroDocuments.Tests
{
    /// <summary>
    /// Regression suite for the v1.4.0 reliability and performance overhaul.
    /// Shares the collection with other tests that mutate static FormulaInjectionProtection flags.
    /// </summary>
    [Collection(GlobalWriterSettingsCollection.Name)]
    public class ReaderWriterHardeningTests
    {
        private const string NsMain = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string NsRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string NsPkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

        public struct PointDto
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        public class NumericDto
        {
            public int Quantity { get; set; }
            public decimal Rate { get; set; }
            public DateTime When { get; set; }
            public TimeSpan Duration { get; set; }
        }

        #region Helpers

        private static MemoryStream BuildPackage(IDictionary<string, string> parts)
        {
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var part in parts)
                {
                    var entry = zip.CreateEntry(part.Key);
                    using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                    writer.Write(part.Value);
                }
            }
            ms.Position = 0;
            return ms;
        }

        private static string ReadPart(byte[] package, string path)
        {
            using var zip = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry(path)!.Open());
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Package that mimics third-party producers: absolute targets, tab order != file order,
        /// implicit row/cell references, rich text, phonetic runs, custom date format, shared strings.
        /// </summary>
        private static MemoryStream BuildThirdPartyPackage(bool date1904 = false)
        {
            return BuildPackage(new Dictionary<string, string>
            {
                ["_rels/.rels"] =
                    $"<Relationships xmlns=\"{NsPkgRel}\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"/xl/workbook.xml\"/></Relationships>",
                ["xl/workbook.xml"] =
                    $"<workbook xmlns=\"{NsMain}\" xmlns:r=\"{NsRel}\">" +
                    (date1904 ? "<workbookPr date1904=\"1\"/>" : "") +
                    "<sheets><sheet name=\"Summary\" sheetId=\"7\" r:id=\"rId2\"/><sheet name=\"Data\" sheetId=\"3\" r:id=\"rId1\"/></sheets></workbook>",
                ["xl/_rels/workbook.xml.rels"] =
                    $"<Relationships xmlns=\"{NsPkgRel}\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"/xl/worksheets/sheet1.xml\"/>" +
                    "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/../worksheets/sheet2.xml\"/>" +
                    "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"/xl/sharedStrings.xml\"/>" +
                    "<Relationship Id=\"rId4\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                    "</Relationships>",
                ["xl/sharedStrings.xml"] =
                    $"<sst xmlns=\"{NsMain}\" count=\"2\" uniqueCount=\"2\">" +
                    "<si><t>Plain</t></si>" +
                    "<si><r><t>Kan</t></r><r><t>ji</t></r><rPh sb=\"0\" eb=\"1\"><t>PHONETIC</t></rPh></si>" +
                    "</sst>",
                ["xl/styles.xml"] =
                    $"<styleSheet xmlns=\"{NsMain}\">" +
                    "<numFmts count=\"1\"><numFmt numFmtId=\"170\" formatCode=\"dd/mm/yyyy\\ hh:mm\"/></numFmts>" +
                    "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\"/></cellStyleXfs>" +
                    "<cellXfs count=\"4\"><xf numFmtId=\"0\"/><xf numFmtId=\"14\"/><xf numFmtId=\"170\"/><xf numFmtId=\"4\"/></cellXfs>" +
                    "<dxfs count=\"1\"><dxf><numFmt numFmtId=\"171\" formatCode=\"yyyy\"/></dxf></dxfs>" +
                    "</styleSheet>",
                // sheet1.xml is "Data" (second tab)
                ["xl/worksheets/sheet1.xml"] =
                    $"<worksheet xmlns=\"{NsMain}\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>DataSheet</t></is></c></row></sheetData></worksheet>",
                // sheet2.xml is "Summary" (first tab): implicit refs, rich inline text, dates, shared strings
                ["xl/worksheets/sheet2.xml"] =
                    $"<worksheet xmlns=\"{NsMain}\"><sheetData>" +
                    "<row><c t=\"s\"><v>0</v></c><c t=\"s\"><v>1</v></c><c t=\"inlineStr\"><is><r><t>Rich </t></r><r><t>Text</t></r></is></c></row>" +
                    "<row><c s=\"1\"><v>46162</v></c><c s=\"2\"><v>46162.6041666667</v></c><c s=\"3\"><v>1234.5</v></c><c s=\"1\"><v>0.5</v></c></row>" +
                    "<row r=\"5\"><c r=\"C5\"><f>SUM(1,2)</f><v>3</v></c></row>" +
                    "</sheetData></worksheet>"
            });
        }

        #endregion

        #region Reader: package resolution & cell semantics

        [Fact]
        public void Reader_DefaultSheet_IsFirstTabInWorkbookOrder_NotSheet1Xml()
        {
            using var ms = BuildThirdPartyPackage();
            var rows = ExcelReader.ReadRows(ms);
            Assert.Equal("Plain", rows[0][1]);
        }

        [Fact]
        public void Reader_ResolvesAbsoluteAndDotSegmentRelationshipTargets()
        {
            using var ms = BuildThirdPartyPackage();
            var data = ExcelReader.ReadRows(ms, sheetName: "Data");
            Assert.Single(data);
            Assert.Equal("DataSheet", data[0][1]);

            ms.Position = 0;
            var summary = ExcelReader.ReadRows(ms, sheetName: "Summary");
            Assert.Equal(3, summary.Count);
        }

        [Fact]
        public void Reader_ImplicitRowAndCellReferences_AreAssignedSequentially()
        {
            using var ms = BuildThirdPartyPackage();
            var rows = ExcelReader.ReadRows(ms, sheetName: "Summary");

            Assert.Equal(1, rows[0].RowNumber);
            Assert.Equal(2, rows[1].RowNumber);
            Assert.Equal(5, rows[2].RowNumber);
            Assert.Equal("Rich Text", rows[0][3]);
            Assert.Equal("3", rows[2]["C"]);
        }

        [Fact]
        public void Reader_SharedStrings_ConcatenatesRunsAndExcludesPhonetic()
        {
            using var ms = BuildThirdPartyPackage();
            var rows = ExcelReader.ReadRows(ms, sheetName: "Summary");
            Assert.Equal("Kanji", rows[0][2]);
        }

        [Fact]
        public void Reader_DateStyledCells_AreReturnedAsIsoText()
        {
            using var ms = BuildThirdPartyPackage();
            var rows = ExcelReader.ReadRows(ms, sheetName: "Summary");

            Assert.Equal("2026-05-20", rows[1][1]);            // built-in numFmt 14
            Assert.Equal("2026-05-20 14:30:00", rows[1][2]);   // custom "dd/mm/yyyy\ hh:mm"
            Assert.Equal("1234.5", rows[1][3]);                // numFmt 4 (#,##0.00) stays numeric
            Assert.Equal("12:00:00", rows[1][4]);              // time-of-day serial
        }

        [Fact]
        public void Reader_Date1904System_IsHonored()
        {
            using var ms = BuildThirdPartyPackage(date1904: true);
            var rows = ExcelReader.ReadRows(ms, sheetName: "Summary");
            Assert.Equal("2030-05-21", rows[1][1]);
        }

        [Fact]
        public void Reader_DoctypeInWorksheet_IsRejected()
        {
            using var ms = BuildPackage(new Dictionary<string, string>
            {
                ["xl/worksheets/sheet1.xml"] =
                    "<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY e SYSTEM \"file:///c:/windows/win.ini\">]>" +
                    $"<worksheet xmlns=\"{NsMain}\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>&e;</t></is></c></row></sheetData></worksheet>"
            });

            Assert.Throws<XmlException>(() => ExcelReader.ReadRows(ms));
        }

        [Fact]
        public void StreamRows_FromFilePath_ReleasesFileHandle()
        {
            string path = Path.Combine(Path.GetTempPath(), $"zd_stream_{Guid.NewGuid():N}.xlsx");
            try
            {
                ExcelWriter.WriteToFile(path, new[] { new PointDto { X = 1, Y = 2 } });
                int count = ExcelReader.StreamRows(path).Count();
                Assert.Equal(2, count);

                File.Delete(path); // would throw if the handle leaked
                Assert.False(File.Exists(path));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        #endregion

        #region Reader: typed hydration

        [Fact]
        public void Read_ValueTypeT_PopulatesProperties()
        {
            using var ms = new MemoryStream();
            ExcelWriter.WriteToStream(ms, new[] { new PointDto { X = 3, Y = 4 } });
            ms.Position = 0;

            var result = ExcelReader.Read<PointDto>(ms);
            Assert.Single(result);
            Assert.Equal(3, result[0].X);
            Assert.Equal(4, result[0].Y);
        }

        [Fact]
        public void Read_TolerantNumericAndDateConversion()
        {
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { "15.0", "1E-3", "46162.5", "0.25" },
                new object?[] { "1E3", "2.5", "2026-05-20 12:00:00", "06:00:00" }
            };

            using var ms = new MemoryStream();
            ExcelWriter.WriteRowsToStream(ms, rows, new[] { "Quantity", "Rate", "When", "Duration" });
            ms.Position = 0;

            var result = ExcelReader.Read<NumericDto>(ms);
            Assert.Equal(2, result.Count);
            Assert.Equal(15, result[0].Quantity);
            Assert.Equal(0.001m, result[0].Rate);
            Assert.Equal(new DateTime(2026, 5, 20, 12, 0, 0), result[0].When);
            Assert.Equal(TimeSpan.FromHours(6), result[0].Duration);
            Assert.Equal(1000, result[1].Quantity);
        }

        [Fact]
        public void ReadWithHeaders_NamesColumnsFromHeaderRow()
        {
            var table = new DataTable();
            table.Columns.Add("A");
            table.Columns.Add("B");
            table.Columns.Add("C");
            table.Columns.Add("D");
            table.Rows.Add("ignored", "ignored", "ignored", "ignored");
            table.Rows.Add("x", "Item Code", "Qty", "Qty");
            table.Rows.Add("x", "SKU-1", "10", "11");
            table.Rows.Add("x", "SKU-2", "20", "21");

            using var ms = new MemoryStream();
            ExcelWriter.WriteToStream(ms, table, includeHeaders: false);
            ms.Position = 0;

            var result = ExcelReader.ReadWithHeaders(ms, "B2:E2", maxRows: 100);

            Assert.Equal(new[] { "Item Code", "Qty", "Qty_2", "Column_E" }, result.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
            Assert.Equal(2, result.Rows.Count);
            Assert.Equal("SKU-2", result.Rows[1]["Item Code"]);
            Assert.Equal("21", result.Rows[1]["Qty_2"]);
        }

        #endregion

        #region Writer: native dates & XML safety

        [Fact]
        public void Writer_DateTime_IsNativeDateCell_AndRoundTripsAsIso()
        {
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { new DateTime(2026, 5, 20), new DateTime(2026, 5, 20, 14, 30, 45), new DateTime(2026, 5, 20, 14, 30, 45, 123) }
            };

            using var ms = new MemoryStream();
            ExcelWriter.WriteRowsToStream(ms, rows);
            byte[] bytes = ms.ToArray();

            string sheet = ReadPart(bytes, "xl/worksheets/sheet1.xml");
            Assert.Contains("<c r=\"A1\" s=\"2\"><v>46162</v></c>", sheet);
            Assert.Contains("r=\"B1\" s=\"3\"", sheet);
            Assert.DoesNotContain("inlineStr", sheet);

            string styles = ReadPart(bytes, "xl/styles.xml");
            Assert.Contains("formatCode=\"yyyy-mm-dd\"", styles);

            var read = ExcelReader.ReadRows(new MemoryStream(bytes));
            Assert.Equal("2026-05-20", read[0][1]);
            Assert.Equal("2026-05-20 14:30:45", read[0][2]);
            Assert.Equal("2026-05-20 14:30:45.123", read[0][3]);
        }

        [Fact]
        public void Writer_NaNAndInfinity_AreWrittenAsTextNotInvalidNumbers()
        {
            var rows = new List<IReadOnlyList<object?>> { new object?[] { double.NaN, double.NegativeInfinity, float.PositiveInfinity } };

            using var ms = new MemoryStream();
            ExcelWriter.WriteRowsToStream(ms, rows);
            byte[] bytes = ms.ToArray();

            string sheet = ReadPart(bytes, "xl/worksheets/sheet1.xml");
            Assert.DoesNotContain("<v>NaN</v>", sheet);

            var read = ExcelReader.ReadRows(new MemoryStream(bytes));
            Assert.Equal("NaN", read[0][1]);
            Assert.Equal("-Infinity", read[0][2]);
            Assert.Equal("Infinity", read[0][3]);
        }

        [Fact]
        public void Writer_CarriageReturns_SurviveRoundTrip()
        {
            var rows = new List<IReadOnlyList<object?>> { new object?[] { "line1\r\nline2", "a\rb", "trailing\n" } };

            using var ms = new MemoryStream();
            ExcelWriter.WriteRowsToStream(ms, rows);
            ms.Position = 0;

            var read = ExcelReader.ReadRows(ms);
            Assert.Equal("line1\r\nline2", read[0][1]);
            Assert.Equal("a\rb", read[0][2]);
            Assert.Equal("trailing\n", read[0][3]);
        }

        [Theory]
        [InlineData("'Quarter'", "Quarter")]
        [InlineData("Data\u0007Bell", "DataBell")]
        [InlineData("History", "History_")]
        [InlineData("a/b:c", "a_b_c")]
        public void Writer_SheetNames_AreSanitizedToExcelRules(string input, string expected)
        {
            using var ms = new MemoryStream();
            ExcelWriter.WriteRowsToStream(ms, new List<IReadOnlyList<object?>> { new object?[] { 1 } }, sheetName: input);

            string workbook = ReadPart(ms.ToArray(), "xl/workbook.xml");
            Assert.Contains($"name=\"{expected}\"", workbook);
        }

        [Fact]
        public void Writer_SheetNameTruncation_DoesNotSplitSurrogatePairs()
        {
            string name = new string('x', 30) + "\U0001F600"; // 32 UTF-16 units, emoji straddles the 31 limit

            using var builder = ZeroExcel.Create();
            builder.AddSheet(name, new List<IReadOnlyList<object?>> { new object?[] { 1 } });
            var bytes = builder.ToArray(); // would throw on a lone surrogate

            string workbook = ReadPart(bytes, "xl/workbook.xml");
            Assert.Contains($"name=\"{new string('x', 30)}\"", workbook);
        }

        [Fact]
        public void Writer_DeletedDataRows_AreSkipped()
        {
            var table = new DataTable();
            table.Columns.Add("Name");
            table.Rows.Add("Keep");
            table.Rows.Add("Drop");
            table.AcceptChanges();
            table.Rows[1].Delete();

            using var ms = new MemoryStream();
            ExcelWriter.WriteToStream(ms, table);
            ms.Position = 0;

            var rows = ExcelReader.ReadRows(ms);
            Assert.Equal(2, rows.Count);
            Assert.Equal("Keep", rows[1][1]);
        }

        #endregion

        #region Builder validation

        [Fact]
        public void Builder_AddImageBeforeAddSheet_ClaimsPlaceholderInsteadOfDuplicating()
        {
            byte[] png = { 0x89, 0x50, 0x4E, 0x47 };

            using var builder = ZeroExcel.Create();
            builder.AddImage("Report", png, "png", 1, 1, 10, 10)
                   .AddSheet("Report", new List<IReadOnlyList<object?>> { new object?[] { "value" } }, new[] { "H" });

            var bytes = builder.ToArray();
            string workbook = ReadPart(bytes, "xl/workbook.xml");

            Assert.Contains("name=\"Report\"", workbook);
            Assert.DoesNotContain("Report_1", workbook);
            Assert.Contains("<drawing", ReadPart(bytes, "xl/worksheets/sheet1.xml"));
        }

        [Fact]
        public void Builder_InvalidRulesAndColors_FailFast()
        {
            using var builder = ZeroExcel.Create();

            Assert.Throws<ArgumentException>(() => builder.AddHighlightRule("S", "A1:A5", CellRuleOperator.Between, "1"));
            Assert.Throws<ArgumentException>(() => builder.AddHighlightRule("S", "A1:A5", CellRuleOperator.Equal, ""));
            Assert.Throws<ArgumentException>(() => builder.AddHighlightRule("S", "A1:A5", CellRuleOperator.Equal, "1", fillColorHex: "ZZZZZZ"));
            Assert.Throws<ArgumentException>(() => builder.AddDataBar("S", "A1:A5", "12345"));
        }

        [Fact]
        public void Builder_ShortHexColorAndCommaSqref_AreNormalized()
        {
            using var builder = ZeroExcel.Create();
            builder.AddSheet("S", new List<IReadOnlyList<object?>> { new object?[] { 1 } })
                   .AddHighlightRule("S", "A1:A5,C1:C5", CellRuleOperator.GreaterThan, "0", fillColorHex: "#F00");

            var bytes = builder.ToArray();
            Assert.Contains("sqref=\"A1:A5 C1:C5\"", ReadPart(bytes, "xl/worksheets/sheet1.xml"));
            Assert.Contains("rgb=\"FFFF0000\"", ReadPart(bytes, "xl/styles.xml"));
        }

        [Fact]
        public void ExcelImage_UnsupportedFormat_Throws()
        {
            Assert.Throws<NotSupportedException>(() => new ExcelImage(new byte[] { 1 }, "webp", 1, 1, 10, 10));
            var gif = new ExcelImage(new byte[] { 1 }, ".GIF", 1, 1, 10, 10);
            Assert.Equal("gif", gif.Format);
        }

        #endregion

        #region CSV

        [Fact]
        public void Csv_TypedNegativeNumbers_AreNotPrefixed_ButTextPayloadsAre()
        {
            var rows = new List<IReadOnlyList<object?>> { new object?[] { -5, -2.5m, TimeSpan.FromHours(-1), "-5", "=1+1" } };

            using var ms = new MemoryStream();
            CsvWriter.WriteRowsToStream(ms, rows);
            ms.Position = 0;

            var row = CsvReader.ReadRows(new StreamReader(ms)).First();
            Assert.Equal("-5", row[0]);
            Assert.Equal("-2.5", row[1].Replace(',', '.'));
            Assert.Equal("-01:00:00", row[2]);
            Assert.Equal("'-5", row[3]);
            Assert.Equal("'=1+1", row[4]);
        }

        [Fact]
        public void Csv_QuoteInsideUnquotedField_IsLiteral()
        {
            const string csv = "Size,Qty\r\n5\" pipe,10\r\nA,B\r\n";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));

            var table = CsvReader.ReadToDataTable(ms);
            Assert.Equal(2, table.Rows.Count);
            Assert.Equal("5\" pipe", table.Rows[0]["Size"]);
            Assert.Equal("A", table.Rows[1]["Size"]);
        }

        [Fact]
        public void Csv_OverflowColumnNameCollision_DoesNotThrow()
        {
            const string csv = "A,Column_3\r\n1,2,3\r\n";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));

            var table = CsvReader.ReadToDataTable(ms);
            Assert.Equal(3, table.Columns.Count);
            Assert.Equal("3", table.Rows[0][2]);
        }

        [Fact]
        public void Csv_LargeQuotedFieldsAcrossBufferBoundaries_ParseCorrectly()
        {
            string big = new string('x', 40000) + "\"\"" + new string('y', 40000);
            string csv = "H1,H2\r\n\"" + big + "\",tail\r\n";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));

            var table = CsvReader.ReadToDataTable(ms);
            Assert.Single(table.Rows);
            Assert.Equal(new string('x', 40000) + "\"" + new string('y', 40000), table.Rows[0]["H1"]);
            Assert.Equal("tail", table.Rows[0]["H2"]);
        }

        #endregion
    }
}
