using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// High-throughput streaming data exporter for ZeroUI.
    /// Exports tabular datasets directly to OpenXML (.xlsx) and RFC 4180 (.csv)
    /// with zero third-party library dependencies and zero heap accumulation.
    /// </summary>
    public static class GridDataExporter
    {
        private const int BufferSize = 65536;

        #region Public API

        /// <summary>
        /// Asynchronously exports data to an OpenXML Spreadsheet (.xlsx) file.
        /// </summary>
        public static Task<int> ExportToXlsxAsync(
            IZeroVirtualSource dataSource,
            IReadOnlyList<ZeroColumn> columns,
            int totalRows,
            string filePath,
            Func<int, int>? rowModelIndexMap = null,
            string sheetName = "Sheet1",
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (dataSource == null) throw new ArgumentNullException(nameof(dataSource));
            if (columns == null) throw new ArgumentNullException(nameof(columns));
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be empty", nameof(filePath));

            return Task.Run(() =>
            {
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize))
                using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false))
                {
                    // 1. [Content_Types].xml
                    WriteContentTypes(zip);

                    // 2. _rels/.rels
                    WriteRootRels(zip);

                    // 3. xl/workbook.xml
                    WriteWorkbook(zip, sheetName);

                    // 4. xl/_rels/workbook.xml.rels
                    WriteWorkbookRels(zip);

                    // 5. xl/styles.xml
                    WriteStyles(zip);

                    // 6. xl/worksheets/sheet1.xml (Main streaming data part)
                    int rowsExported = WriteWorksheet(zip, dataSource, columns, totalRows, rowModelIndexMap, progress, cancellationToken);
                    return rowsExported;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Asynchronously exports data to an RFC 4180 compliant CSV file.
        /// </summary>
        public static Task<int> ExportToCsvAsync(
            IZeroVirtualSource dataSource,
            IReadOnlyList<ZeroColumn> columns,
            int totalRows,
            string filePath,
            Func<int, int>? rowModelIndexMap = null,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (dataSource == null) throw new ArgumentNullException(nameof(dataSource));
            if (columns == null) throw new ArgumentNullException(nameof(columns));
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be empty", nameof(filePath));

            return Task.Run(() =>
            {
                int colCount = columns.Count;
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize))
                using (var writer = new StreamWriter(fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), BufferSize))
                {
                    // Header Row
                    bool firstCol = true;
                    for (int c = 0; c < colCount; c++)
                    {
                        if (!columns[c].IsVisible) continue;
                        if (!firstCol) writer.Write(',');
                        firstCol = false;
                        WriteCsvEscapedString(writer, columns[c].HeaderText);
                    }
                    writer.WriteLine();

                    // Data Rows
                    CellValueBuffer cellBuf = new CellValueBuffer();
                    int rowsExported = 0;

                    for (int r = 0; r < totalRows; r++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int modelRow = rowModelIndexMap != null ? rowModelIndexMap(r) : r;

                        firstCol = true;
                        for (int c = 0; c < colCount; c++)
                        {
                            if (!columns[c].IsVisible) continue;
                            if (!firstCol) writer.Write(',');
                            firstCol = false;

                            cellBuf.Reset();
                            dataSource.GetCellValue(modelRow, c, ref cellBuf);
                            WriteCsvEscapedSpan(writer, cellBuf.Text);
                        }
                        writer.WriteLine();

                        rowsExported++;
                        if (progress != null && (rowsExported % 10000 == 0 || rowsExported == totalRows))
                        {
                            progress.Report((int)((long)rowsExported * 100 / totalRows));
                        }
                    }

                    writer.Flush();
                    return rowsExported;
                }
            }, cancellationToken);
        }

        #endregion

        #region OpenXML Package Construction

        private static void WriteContentTypes(ZipArchive zip)
        {
            var entry = zip.CreateEntry("[Content_Types].xml", CompressionLevel.Fastest);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">\n" +
                    "  <Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>\n" +
                    "  <Default Extension=\"xml\" ContentType=\"application/xml\"/>\n" +
                    "  <Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>\n" +
                    "  <Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>\n" +
                    "  <Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>\n" +
                    "</Types>");
            }
        }

        private static void WriteRootRels(ZipArchive zip)
        {
            var entry = zip.CreateEntry("_rels/.rels", CompressionLevel.Fastest);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\n" +
                    "  <Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>\n" +
                    "</Relationships>");
            }
        }

        private static void WriteWorkbook(ZipArchive zip, string sheetName)
        {
            string safeName = string.IsNullOrEmpty(sheetName) ? "Sheet1" : XmlEscape(sheetName);
            var entry = zip.CreateEntry("xl/workbook.xml", CompressionLevel.Fastest);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
                    "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">\n" +
                    "  <sheets>\n" +
                    "    <sheet name=\"" + safeName + "\" sheetId=\"1\" r:id=\"rId1\"/>\n" +
                    "  </sheets>\n" +
                    "</workbook>");
            }
        }

        private static void WriteWorkbookRels(ZipArchive zip)
        {
            var entry = zip.CreateEntry("xl/_rels/workbook.xml.rels", CompressionLevel.Fastest);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\n" +
                    "  <Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>\n" +
                    "  <Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>\n" +
                    "</Relationships>");
            }
        }

        private static void WriteStyles(ZipArchive zip)
        {
            var entry = zip.CreateEntry("xl/styles.xml", CompressionLevel.Fastest);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
                    "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">\n" +
                    "  <fonts count=\"2\">\n" +
                    "    <font><sz val=\"10\"/><name val=\"Segoe UI\"/></font>\n" +
                    "    <font><b/><sz val=\"10\"/><name val=\"Segoe UI\"/><color rgb=\"FF1E293B\"/></font>\n" +
                    "  </fonts>\n" +
                    "  <fills count=\"3\">\n" +
                    "    <fill><patternFill patternType=\"none\"/></fill>\n" +
                    "    <fill><patternFill patternType=\"gray125\"/></fill>\n" +
                    "    <fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF1F5F9\"/></patternFill></fill>\n" +
                    "  </fills>\n" +
                    "  <borders count=\"2\">\n" +
                    "    <border><left/><right/><top/><bottom/><diagonal/></border>\n" +
                    "    <border>\n" +
                    "      <left style=\"thin\"><color rgb=\"FFE2E8F0\"/></left>\n" +
                    "      <right style=\"thin\"><color rgb=\"FFE2E8F0\"/></right>\n" +
                    "      <top style=\"thin\"><color rgb=\"FFE2E8F0\"/></top>\n" +
                    "      <bottom style=\"medium\"><color rgb=\"FFCBD5E1\"/></bottom>\n" +
                    "    </border>\n" +
                    "  </borders>\n" +
                    "  <cellStyleXfs count=\"1\">\n" +
                    "    <xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/>\n" +
                    "  </cellStyleXfs>\n" +
                    "  <cellXfs count=\"3\">\n" +
                    "    <xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>\n" +
                    "    <xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>\n" +
                    "    <xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyAlignment=\"1\"><alignment horizontal=\"right\"/></xf>\n" +
                    "  </cellXfs>\n" +
                    "</styleSheet>");
            }
        }

        private static int WriteWorksheet(
            ZipArchive zip,
            IZeroVirtualSource dataSource,
            IReadOnlyList<ZeroColumn> columns,
            int totalRows,
            Func<int, int>? rowModelIndexMap,
            IProgress<int>? progress,
            CancellationToken cancellationToken)
        {
            var entry = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Fastest);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, Encoding.UTF8, BufferSize))
            {
                writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
                    "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">\n");

                // Columns width specification
                writer.Write("  <cols>\n");
                int visibleColIdx = 0;
                for (int c = 0; c < columns.Count; c++)
                {
                    if (!columns[c].IsVisible) continue;
                    visibleColIdx++;
                    double charWidth = Math.Max(8.0, columns[c].Width / 7.5);
                    writer.Write(string.Format(CultureInfo.InvariantCulture,
                        "    <col min=\"{0}\" max=\"{0}\" width=\"{1:F1}\" customWidth=\"1\"/>\n",
                        visibleColIdx, charWidth));
                }
                writer.Write("  </cols>\n");

                writer.Write("  <sheetData>\n");

                // 1. Header Row (Row 1, style 1 = Header font + fill + border)
                writer.Write("    <row r=\"1\">\n");
                visibleColIdx = 0;
                for (int c = 0; c < columns.Count; c++)
                {
                    if (!columns[c].IsVisible) continue;
                    string cellRef = GetCellReference(visibleColIdx, 1);
                    visibleColIdx++;
                    writer.Write("      <c r=\"");
                    writer.Write(cellRef);
                    writer.Write("\" s=\"1\" t=\"inlineStr\"><is><t>");
                    WriteXmlEscaped(writer, columns[c].HeaderText);
                    writer.Write("</t></is></c>\n");
                }
                writer.Write("    </row>\n");

                // 2. Data Rows
                CellValueBuffer cellBuf = new CellValueBuffer();
                int rowsExported = 0;

                for (int r = 0; r < totalRows; r++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int modelRow = rowModelIndexMap != null ? rowModelIndexMap(r) : r;
                    int excelRow = r + 2; // 1-based, after header

                    writer.Write("    <row r=\"");
                    writer.Write(excelRow);
                    writer.Write("\">\n");

                    visibleColIdx = 0;
                    for (int c = 0; c < columns.Count; c++)
                    {
                        if (!columns[c].IsVisible) continue;
                        string cellRef = GetCellReference(visibleColIdx, excelRow);
                        visibleColIdx++;

                        cellBuf.Reset();
                        dataSource.GetCellValue(modelRow, c, ref cellBuf);
                        var textSpan = cellBuf.Text;

                        if (textSpan.IsEmpty)
                        {
                            continue;
                        }

                        // Check if pure numeric to write <v> tag for native Excel calculations
                        if (IsNumeric(textSpan, out double numVal))
                        {
                            writer.Write("      <c r=\"");
                            writer.Write(cellRef);
                            writer.Write("\" s=\"2\"><v>");
                            writer.Write(numVal.ToString("G", CultureInfo.InvariantCulture));
                            writer.Write("</v></c>\n");
                        }
                        else
                        {
                            writer.Write("      <c r=\"");
                            writer.Write(cellRef);
                            writer.Write("\" t=\"inlineStr\"><is><t>");
                            WriteXmlEscapedSpan(writer, textSpan);
                            writer.Write("</t></is></c>\n");
                        }
                    }

                    writer.Write("    </row>\n");

                    rowsExported++;
                    if (progress != null && (rowsExported % 10000 == 0 || rowsExported == totalRows))
                    {
                        progress.Report((int)((long)rowsExported * 100 / totalRows));
                    }
                }

                writer.Write("  </sheetData>\n" +
                    "</worksheet>");
                writer.Flush();
                return rowsExported;
            }
        }

        #endregion

        #region Helpers

        public static string GetCellReference(int colIndex, int rowNumber)
        {
            var sb = new StringBuilder(8);
            int dividend = colIndex + 1;
            while (dividend > 0)
            {
                int modulo = (dividend - 1) % 26;
                sb.Insert(0, (char)('A' + modulo));
                dividend = (dividend - modulo) / 26;
            }
            sb.Append(rowNumber);
            return sb.ToString();
        }

        private static bool IsNumeric(ReadOnlySpan<char> span, out double result)
        {
            // Avoid allocating string in net8.0+
#if NET8_0_OR_GREATER
            return double.TryParse(span, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result);
#else
            return double.TryParse(span.ToString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result);
#endif
        }

        private static void WriteXmlEscaped(TextWriter writer, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                switch (ch)
                {
                    case '<': writer.Write("&lt;"); break;
                    case '>': writer.Write("&gt;"); break;
                    case '&': writer.Write("&amp;"); break;
                    case '"': writer.Write("&quot;"); break;
                    case '\'': writer.Write("&apos;"); break;
                    default:
                        if (ch >= 0x20 || ch == '\t' || ch == '\n' || ch == '\r')
                            writer.Write(ch);
                        break;
                }
            }
        }

        private static void WriteXmlEscapedSpan(TextWriter writer, ReadOnlySpan<char> span)
        {
            for (int i = 0; i < span.Length; i++)
            {
                char ch = span[i];
                switch (ch)
                {
                    case '<': writer.Write("&lt;"); break;
                    case '>': writer.Write("&gt;"); break;
                    case '&': writer.Write("&amp;"); break;
                    case '"': writer.Write("&quot;"); break;
                    case '\'': writer.Write("&apos;"); break;
                    default:
                        if (ch >= 0x20 || ch == '\t' || ch == '\n' || ch == '\r')
                            writer.Write(ch);
                        break;
                }
            }
        }

        private static string XmlEscape(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("&", "&amp;")
                       .Replace("<", "&lt;")
                       .Replace(">", "&gt;")
                       .Replace("\"", "&quot;")
                       .Replace("'", "&apos;");
        }

        private static void WriteCsvEscapedString(TextWriter writer, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            bool needsQuotes = text.Contains(",") || text.Contains("\"") || text.Contains("\n") || text.Contains("\r");
            if (!needsQuotes)
            {
                writer.Write(text);
            }
            else
            {
                writer.Write('"');
                writer.Write(text.Replace("\"", "\"\""));
                writer.Write('"');
            }
        }

        private static void WriteCsvEscapedSpan(TextWriter writer, ReadOnlySpan<char> span)
        {
            if (span.IsEmpty) return;
            bool needsQuotes = false;
            for (int i = 0; i < span.Length; i++)
            {
                char ch = span[i];
                if (ch == ',' || ch == '"' || ch == '\n' || ch == '\r')
                {
                    needsQuotes = true;
                    break;
                }
            }

            if (!needsQuotes)
            {
#if NET8_0_OR_GREATER
                writer.Write(span);
#else
                writer.Write(span.ToString());
#endif
            }
            else
            {
                writer.Write('"');
                string raw = span.ToString();
                writer.Write(raw.Replace("\"", "\"\""));
                writer.Write('"');
            }
        }

        #endregion
    }
}
