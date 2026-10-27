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
    [Collection(GlobalWriterSettingsCollection.Name)]
    public class DeepFormulaInjectionTests
    {
        public static readonly string[] DangerousPayloads = new[]
        {
            "=cmd|' /C calc'!A0",
            "+1+1",
            "-2+3",
            "@SUM(1+1)*cmd|' /C calc'!A0",
            "\t@calc",
            "\r-999"
        };

        [Fact]
        public void FormulaInjection_ExcelWriter_ShouldPrefixDangerousInputs()
        {
            var rows = DangerousPayloads.Select((p, idx) => (IReadOnlyList<object?>)new object?[] { idx + 1, p }).ToList();
            var headers = new[] { "Id", "Payload" };

            using var ms = new MemoryStream();
            ExcelWriter.WriteRowsToStream(ms, rows, headers);

            ms.Position = 0;
            var readBack = ExcelReader.StreamRows(ms).ToList();

            // Header is row 0; data starts at row 1
            for (int i = 0; i < DangerousPayloads.Length; i++)
            {
                string? cellVal = readBack[i + 1][2]; // Payload is col 2
                Assert.NotNull(cellVal);
                Assert.StartsWith("'", cellVal!);
            }
        }

        [Fact]
        public void FormulaInjection_CsvWriter_ShouldPrefixDangerousInputs()
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Payload", typeof(string));

            for (int i = 0; i < DangerousPayloads.Length; i++)
            {
                table.Rows.Add(i + 1, DangerousPayloads[i]);
            }

            using var ms = new MemoryStream();
            CsvWriter.WriteToStream(ms, table);

            ms.Position = 0;
            var readBack = CsvReader.ReadToDataTable(ms);

            for (int i = 0; i < DangerousPayloads.Length; i++)
            {
                string? cellVal = readBack.Rows[i]["Payload"]?.ToString();
                Assert.NotNull(cellVal);
                Assert.StartsWith("'", cellVal!);
            }
        }

        [Fact]
        public void FormulaInjection_ExcelWorkbookBuilder_ShouldPrefixDangerousInputs()
        {
            var builder = ZeroExcel.Create();
            var rows = DangerousPayloads.Select((p, idx) => (IReadOnlyList<object?>)new object?[] { idx + 1, p }).ToList();

            builder.AddSheet("SecurityTest", rows, new[] { "Id", "Payload" });

            using var ms = new MemoryStream();
            builder.Save(ms);

            ms.Position = 0;
            var readBack = ExcelReader.StreamRows(ms, sheetName: "SecurityTest").ToList();

            for (int i = 0; i < DangerousPayloads.Length; i++)
            {
                string? cellVal = readBack[i + 1][2];
                Assert.NotNull(cellVal);
                Assert.StartsWith("'", cellVal!);
            }
        }

        [Fact]
        public void FormulaInjection_Disabled_ShouldPreserveRawFormulasWhenExplicitlyRequested()
        {
            try
            {
                ExcelWriter.FormulaInjectionProtection = false;
                CsvWriter.FormulaInjectionProtection = false;

                const string formula = "=SUM(A1:A10)";
                var rows = new List<IReadOnlyList<object?>>
                {
                    new object?[] { 1, formula }
                };

                // Test Excel
                using var excelMs = new MemoryStream();
                ExcelWriter.WriteRowsToStream(excelMs, rows, new[] { "Id", "Formula" });

                excelMs.Position = 0;
                var excelRows = ExcelReader.StreamRows(excelMs).ToList();
                Assert.Equal(formula, excelRows[1][2]); // Preserves raw =SUM(A1:A10)

                // Test CSV
                using var csvMs = new MemoryStream();
                CsvWriter.WriteRowsToStream(csvMs, rows, new[] { "Id", "Formula" });

                csvMs.Position = 0;
                var csvTable = CsvReader.ReadToDataTable(csvMs);
                Assert.Equal(formula, csvTable.Rows[0]["Formula"]);
            }
            finally
            {
                // Restore defaults
                ExcelWriter.FormulaInjectionProtection = true;
                CsvWriter.FormulaInjectionProtection = true;
            }
        }
    }
}
