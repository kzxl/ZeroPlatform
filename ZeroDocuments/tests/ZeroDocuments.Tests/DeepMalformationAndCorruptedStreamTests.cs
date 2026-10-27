using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Xunit;
using ZeroDocuments.Csv;
using ZeroDocuments.Excel;

namespace ZeroDocuments.Tests
{
    public class DeepMalformationAndCorruptedStreamTests
    {
        public class StubEntity
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        [Fact]
        public void CorruptOpenXml_ZeroLengthStream_ThrowsInvalidDataException()
        {
            using var emptyStream = new MemoryStream(Array.Empty<byte>());

            Assert.Throws<InvalidDataException>(() =>
            {
                ExcelReader.ReadRows(emptyStream);
            });
        }

        [Fact]
        public void CorruptOpenXml_TruncatedZipFile_ThrowsInvalidDataException()
        {
            // Valid ZIP local header signature (PK\x03\x04), but truncated
            byte[] truncatedZip = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x0A, 0x00, 0x00, 0x00 };
            using var ms = new MemoryStream(truncatedZip);

            Assert.Throws<InvalidDataException>(() =>
            {
                ExcelReader.ReadRows(ms);
            });
        }

        [Fact]
        public void CorruptOpenXml_NonExistentSheetName_ReturnsEmptyAndDoesNotReturnSheet1()
        {
            using var ms = new MemoryStream();
            var items = new List<StubEntity>
            {
                new StubEntity { Id = 1, Name = "ExistingItem1" },
                new StubEntity { Id = 2, Name = "ExistingItem2" }
            };

            ExcelWriter.WriteToStream(ms, items, sheetName: "RealSheet");
            ms.Position = 0;

            // 1. Reading existing sheet should return items
            var realRead = ExcelReader.Read<StubEntity>(ms, sheetName: "RealSheet");
            Assert.Equal(2, realRead.Count);

            // 2. Reading a non-existent sheet must return EMPTY and NOT leak RealSheet's data
            ms.Position = 0;
            var fakeRead = ExcelReader.Read<StubEntity>(ms, sheetName: "NonExistentSheet_9999");
            Assert.Empty(fakeRead);

            // 3. ReadRows on non-existent sheet must also be empty
            ms.Position = 0;
            var fakeRows = ExcelReader.ReadRows(ms, sheetName: "NonExistentSheet_9999");
            Assert.Empty(fakeRows);
        }

        [Fact]
        public void CorruptWorksheetXml_MalformedXmlSyntax_ThrowsXmlException()
        {
            // Construct a zip file containing broken sheet XML
            using var zipMs = new MemoryStream();
            using (var zip = new ZipArchive(zipMs, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = zip.CreateEntry("xl/worksheets/sheet1.xml");
                using var entryStream = entry.Open();
                byte[] malformedXml = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><worksheet><sheetData><row r=\"1\"><c><v>broken unclosed tags");
                entryStream.Write(malformedXml, 0, malformedXml.Length);
            }

            zipMs.Position = 0;

            Assert.Throws<XmlException>(() =>
            {
                ExcelReader.ReadRows(zipMs);
            });
        }

        [Fact]
        public void CsvMalformation_UnclosedQuotesAtEof_ShouldHandleGracefullyWithoutHanging()
        {
            string brokenCsv = "Id,Title,Description\r\n101,ValidTitle,\"Unclosed quote that reaches end of file";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(brokenCsv));

            var table = CsvReader.ReadToDataTable(ms);
            Assert.NotNull(table);
            Assert.Single(table.Rows);
            Assert.Equal("101", table.Rows[0]["Id"]?.ToString());
            Assert.Equal("ValidTitle", table.Rows[0]["Title"]?.ToString());
            Assert.Contains("Unclosed quote", table.Rows[0]["Description"]?.ToString());
        }

        [Fact]
        public void CsvMalformation_RaggedRows_VaryingColumnLengths_ShouldNotThrowIndexOutOfRange()
        {
            // Row 0 (header): 3 cols
            // Row 1: 5 cols (extra cols)
            // Row 2: 1 col (fewer cols)
            // Row 3: 0 cols (empty line)
            // Row 4: 3 cols (normal)
            string raggedCsv = "Col1,Col2,Col3\r\nVal1,Val2,Val3,Extra4,Extra5\r\nSingleVal\r\n\r\nFinal1,Final2,Final3";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(raggedCsv));

            var table = CsvReader.ReadToDataTable(ms);
            Assert.NotNull(table);
            Assert.True(table.Rows.Count >= 3);

            // Row 1: Extra cols are either ignored or fitted into row schema
            Assert.Equal("Val1", table.Rows[0][0]?.ToString());
            Assert.Equal("Val2", table.Rows[0][1]?.ToString());
            Assert.Equal("Val3", table.Rows[0][2]?.ToString());

            // Row 2: Single col with missing remaining cols
            Assert.Equal("SingleVal", table.Rows[1][0]?.ToString());
        }

        [Fact]
        public void CsvMalformation_OnlyWhitespaceAndEmptyLines_ReturnsEmptyTable()
        {
            string blankCsv = "   \r\n\t\r\n\r\n   \n   \r\n";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(blankCsv));

            var table = CsvReader.ReadToDataTable(ms);
            Assert.NotNull(table);
            Assert.Empty(table.Rows);
        }

        [Fact]
        public void CsvMalformation_NullBytesAndControlCharacters_ShouldPreserveContent()
        {
            string csvWithControlChars = "ColA,ColB\r\n\"Line\0WithNull\",\"Control\u0001\u0002Chars\"";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csvWithControlChars));

            var table = CsvReader.ReadToDataTable(ms);
            Assert.Single(table.Rows);
            Assert.Contains("\0", table.Rows[0]["ColA"]?.ToString());
            Assert.Contains("\u0001\u0002", table.Rows[0]["ColB"]?.ToString());
        }

        [Fact]
        public void CorruptOpenXml_ZipSlipPathTraversal_ThrowsInvalidDataException()
        {
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = zip.CreateEntry("../../malicious.xml");
                using var writer = new StreamWriter(entry.Open());
                writer.WriteLine("<root>payload</root>");
            }
            ms.Position = 0;

            Assert.Throws<InvalidDataException>(() =>
            {
                ExcelReader.ReadRows(ms);
            });
        }
    }
}
