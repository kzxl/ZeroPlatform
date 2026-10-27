using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Xunit;
using ZeroDocuments.Csv;
using ZeroDocuments.Excel;

namespace ZeroDocuments.Tests
{
    public class ErrorHandlingTests
    {
        [Fact]
        public void ExcelReader_NonExistentFile_ThrowsFileNotFoundException()
        {
            string fakePath = Path.Combine(Path.GetTempPath(), $"NonExistent_{Guid.NewGuid():N}.xlsx");

            Assert.Throws<FileNotFoundException>(() =>
            {
                ExcelReader.ReadToDataTable(fakePath);
            });

            Assert.Throws<FileNotFoundException>(() =>
            {
                ExcelReader.Read<ErrorHandlingTests>(fakePath);
            });

            Assert.Throws<FileNotFoundException>(() =>
            {
                ExcelReader.ReadRows(fakePath);
            });
        }

        [Fact]
        public void ExcelWriter_NullParameters_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ExcelWriter.WriteRowsToStream(null!, new List<IReadOnlyList<object?>>());
            });

            using var ms = new MemoryStream();
            Assert.Throws<ArgumentNullException>(() =>
            {
                ExcelWriter.WriteRowsToStream(ms, null!);
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                ExcelWriter.WriteToStream<ErrorHandlingTests>(null!, new List<ErrorHandlingTests>());
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                ExcelWriter.WriteToStream<ErrorHandlingTests>(ms, null!);
            });
        }

        [Fact]
        public void CsvWriter_NullParameters_ThrowsArgumentExceptions()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                CsvWriter.WriteToStream(null!, new DataTable());
            });

            using var ms = new MemoryStream();
            Assert.Throws<ArgumentNullException>(() =>
            {
                CsvWriter.WriteToStream(ms, (DataTable)null!);
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                CsvWriter.WriteRowsToStream(null!, new List<IReadOnlyList<object?>>());
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                CsvWriter.WriteRowsToStream(ms, null!);
            });

            Assert.Throws<ArgumentException>(() =>
            {
                CsvWriter.WriteToFile("", new DataTable());
            });
        }

        [Fact]
        public void ExcelReader_CorruptNonZipStream_ThrowsInvalidDataException()
        {
            byte[] corruptBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04 };
            using var ms = new MemoryStream(corruptBytes);

            Assert.Throws<InvalidDataException>(() =>
            {
                // Consuming the IEnumerable triggers ZipArchive loading
                ExcelReader.StreamRows(ms).ToList();
            });
        }

        [Fact]
        public void CsvReader_EmptyStream_ReturnsEmptyDataTable()
        {
            using var ms = new MemoryStream(Array.Empty<byte>());
            var table = CsvReader.ReadToDataTable(ms);

            Assert.NotNull(table);
            Assert.Empty(table.Rows);
        }

        [Fact]
        public void ExcelWorkbookBuilder_NullStream_ThrowsArgumentNullException()
        {
            var builder = ZeroExcel.Create();
            builder.AddSheet<ErrorHandlingTests>("Sheet1", new List<ErrorHandlingTests>());

            Assert.Throws<ArgumentNullException>(() =>
            {
                builder.Save((Stream)null!);
            });
        }
    }
}
