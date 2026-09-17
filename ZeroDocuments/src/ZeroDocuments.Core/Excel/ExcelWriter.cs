using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Xml;
using ZeroDocuments.Common;
using ZeroDocuments.Excel.Models;

namespace ZeroDocuments.Excel
{
    /// <summary>
    /// Pure C# Zero-Dependency OpenXML Excel (.xlsx) Writer.
    /// Operates without external DLLs (No EPPlus, ClosedXML, or DocumentFormat.OpenXml required).
    /// </summary>
    public static class ExcelWriter
    {
        private const string NsSpreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string NsRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        static ExcelWriter()
        {
            RuntimeAssemblyResolver.EnsureInitialized();
        }

        #region Public Write APIs

        /// <summary>
        /// Writes a DataTable to an Excel (.xlsx) file on disk.
        /// </summary>
        public static void WriteToFile(string filePath, DataTable table, string sheetName = "Sheet1", bool includeHeaders = true)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            WriteToStream(stream, table, sheetName, includeHeaders);
        }

        /// <summary>
        /// Writes a DataTable to a stream in OpenXML Excel (.xlsx) format.
        /// </summary>
        public static void WriteToStream(Stream stream, DataTable table, string sheetName = "Sheet1", bool includeHeaders = true)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (table == null) throw new ArgumentNullException(nameof(table));

            var headers = new List<string>();
            foreach (DataColumn col in table.Columns)
            {
                headers.Add(col.ColumnName);
            }

            var rows = new List<IReadOnlyList<object?>>();
            foreach (DataRow row in table.Rows)
            {
                var values = new object?[table.Columns.Count];
                for (int i = 0; i < table.Columns.Count; i++)
                {
                    values[i] = row[i] == DBNull.Value ? null : row[i];
                }
                rows.Add(values);
            }

            WriteRowsToStream(stream, rows, includeHeaders ? headers : null, sheetName);
        }

        /// <summary>
        /// Writes a collection of objects to an Excel (.xlsx) file on disk.
        /// Public properties are mapped to columns.
        /// </summary>
        public static void WriteToFile<T>(string filePath, IEnumerable<T> data, string sheetName = "Sheet1", bool includeHeaders = true)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            WriteToStream(stream, data, sheetName, includeHeaders);
        }

        /// <summary>
        /// Writes a collection of objects to a stream in OpenXML Excel (.xlsx) format.
        /// </summary>
        public static void WriteToStream<T>(Stream stream, IEnumerable<T> data, string sheetName = "Sheet1", bool includeHeaders = true)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (data == null) throw new ArgumentNullException(nameof(data));

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var headers = new List<string>();
            foreach (var prop in properties)
            {
                headers.Add(prop.Name);
            }

            var rows = new List<IReadOnlyList<object?>>();
            foreach (var item in data)
            {
                if (item == null) continue;
                var values = new object?[properties.Length];
                for (int i = 0; i < properties.Length; i++)
                {
                    values[i] = properties[i].GetValue(item);
                }
                rows.Add(values);
            }

            WriteRowsToStream(stream, rows, includeHeaders ? headers : null, sheetName);
        }

        /// <summary>
        /// Writes raw 2D grid rows to an Excel (.xlsx) file on disk.
        /// </summary>
        public static void WriteToFile(string filePath, IEnumerable<IReadOnlyList<object?>> rows, IReadOnlyList<string>? headers = null, string sheetName = "Sheet1")
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            WriteRowsToStream(stream, rows, headers, sheetName);
        }

        /// <summary>
        /// Writes raw 2D grid rows to a stream in OpenXML Excel (.xlsx) format.
        /// </summary>
        public static void WriteRowsToStream(Stream stream, IEnumerable<IReadOnlyList<object?>> rows, IReadOnlyList<string>? headers = null, string sheetName = "Sheet1")
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            string safeSheetName = string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : SanitizeSheetName(sheetName);

            using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);

            // 1. [Content_Types].xml
            CreateContentTypesEntry(zip);

            // 2. _rels/.rels
            CreateGlobalRelsEntry(zip);

            // 3. xl/workbook.xml
            CreateWorkbookEntry(zip, safeSheetName);

            // 4. xl/_rels/workbook.xml.rels
            CreateWorkbookRelsEntry(zip);

            // 5. xl/styles.xml
            CreateStylesEntry(zip);

            // 6. xl/worksheets/sheet1.xml
            CreateWorksheetEntry(zip, rows, headers);
        }

        #endregion

        #region OPC Package Parts Generation

        private static void CreateContentTypesEntry(ZipArchive zip)
        {
            var entry = zip.CreateEntry("[Content_Types].xml", CompressionLevel.Fastest);
            using var stream = entry.Open();
            using var writer = CreateXmlWriter(stream);

            writer.WriteStartDocument(true);
            writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");

            writer.WriteStartElement("Default");
            writer.WriteAttributeString("Extension", "rels");
            writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-package.relationships+xml");
            writer.WriteEndElement();

            writer.WriteStartElement("Default");
            writer.WriteAttributeString("Extension", "xml");
            writer.WriteAttributeString("ContentType", "application/xml");
            writer.WriteEndElement();

            writer.WriteStartElement("Override");
            writer.WriteAttributeString("PartName", "/xl/workbook.xml");
            writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
            writer.WriteEndElement();

            writer.WriteStartElement("Override");
            writer.WriteAttributeString("PartName", "/xl/worksheets/sheet1.xml");
            writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
            writer.WriteEndElement();

            writer.WriteStartElement("Override");
            writer.WriteAttributeString("PartName", "/xl/styles.xml");
            writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
            writer.WriteEndElement();

            writer.WriteEndElement(); // Types
            writer.WriteEndDocument();
        }

        private static void CreateGlobalRelsEntry(ZipArchive zip)
        {
            var entry = zip.CreateEntry("_rels/.rels", CompressionLevel.Fastest);
            using var stream = entry.Open();
            using var writer = CreateXmlWriter(stream);

            writer.WriteStartDocument(true);
            writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");

            writer.WriteStartElement("Relationship");
            writer.WriteAttributeString("Id", "rId1");
            writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
            writer.WriteAttributeString("Target", "xl/workbook.xml");
            writer.WriteEndElement();

            writer.WriteEndElement(); // Relationships
            writer.WriteEndDocument();
        }

        private static void CreateWorkbookEntry(ZipArchive zip, string sheetName)
        {
            var entry = zip.CreateEntry("xl/workbook.xml", CompressionLevel.Fastest);
            using var stream = entry.Open();
            using var writer = CreateXmlWriter(stream);

            writer.WriteStartDocument(true);
            writer.WriteStartElement("workbook", NsSpreadsheet);
            writer.WriteAttributeString("xmlns", "r", null, NsRelationships);

            writer.WriteStartElement("sheets");
            writer.WriteStartElement("sheet");
            writer.WriteAttributeString("name", sheetName);
            writer.WriteAttributeString("sheetId", "1");
            writer.WriteAttributeString("id", NsRelationships, "rId1");
            writer.WriteEndElement(); // sheet
            writer.WriteEndElement(); // sheets

            writer.WriteEndElement(); // workbook
            writer.WriteEndDocument();
        }

        private static void CreateWorkbookRelsEntry(ZipArchive zip)
        {
            var entry = zip.CreateEntry("xl/_rels/workbook.xml.rels", CompressionLevel.Fastest);
            using var stream = entry.Open();
            using var writer = CreateXmlWriter(stream);

            writer.WriteStartDocument(true);
            writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");

            writer.WriteStartElement("Relationship");
            writer.WriteAttributeString("Id", "rId1");
            writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet");
            writer.WriteAttributeString("Target", "worksheets/sheet1.xml");
            writer.WriteEndElement();

            writer.WriteStartElement("Relationship");
            writer.WriteAttributeString("Id", "rId2");
            writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles");
            writer.WriteAttributeString("Target", "styles.xml");
            writer.WriteEndElement();

            writer.WriteEndElement(); // Relationships
            writer.WriteEndDocument();
        }

        private static void CreateStylesEntry(ZipArchive zip)
        {
            var entry = zip.CreateEntry("xl/styles.xml", CompressionLevel.Fastest);
            using var stream = entry.Open();
            using var writer = CreateXmlWriter(stream);

            writer.WriteStartDocument(true);
            writer.WriteStartElement("styleSheet", NsSpreadsheet);

            // fonts
            writer.WriteStartElement("fonts");
            writer.WriteAttributeString("count", "1");
            writer.WriteStartElement("font");
            writer.WriteStartElement("sz");
            writer.WriteAttributeString("val", "11");
            writer.WriteEndElement();
            writer.WriteStartElement("name");
            writer.WriteAttributeString("val", "Calibri");
            writer.WriteEndElement();
            writer.WriteEndElement(); // font
            writer.WriteEndElement(); // fonts

            // fills
            writer.WriteStartElement("fills");
            writer.WriteAttributeString("count", "2");
            writer.WriteStartElement("fill");
            writer.WriteStartElement("patternFill");
            writer.WriteAttributeString("patternType", "none");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteStartElement("fill");
            writer.WriteStartElement("patternFill");
            writer.WriteAttributeString("patternType", "gray125");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement(); // fills

            // borders
            writer.WriteStartElement("borders");
            writer.WriteAttributeString("count", "1");
            writer.WriteStartElement("border");
            writer.WriteElementString("left", NsSpreadsheet, "");
            writer.WriteElementString("right", NsSpreadsheet, "");
            writer.WriteElementString("top", NsSpreadsheet, "");
            writer.WriteElementString("bottom", NsSpreadsheet, "");
            writer.WriteElementString("diagonal", NsSpreadsheet, "");
            writer.WriteEndElement();
            writer.WriteEndElement(); // borders

            // cellStyleXfs
            writer.WriteStartElement("cellStyleXfs");
            writer.WriteAttributeString("count", "1");
            writer.WriteStartElement("xf");
            writer.WriteAttributeString("numFmtId", "0");
            writer.WriteAttributeString("fontId", "0");
            writer.WriteAttributeString("fillId", "0");
            writer.WriteAttributeString("borderId", "0");
            writer.WriteEndElement();
            writer.WriteEndElement();

            // cellXfs
            writer.WriteStartElement("cellXfs");
            writer.WriteAttributeString("count", "1");
            writer.WriteStartElement("xf");
            writer.WriteAttributeString("numFmtId", "0");
            writer.WriteAttributeString("fontId", "0");
            writer.WriteAttributeString("fillId", "0");
            writer.WriteAttributeString("borderId", "0");
            writer.WriteAttributeString("xfId", "0");
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteEndElement(); // styleSheet
            writer.WriteEndDocument();
        }

        private static void CreateWorksheetEntry(ZipArchive zip, IEnumerable<IReadOnlyList<object?>> rows, IReadOnlyList<string>? headers)
        {
            var entry = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Fastest);
            using var stream = entry.Open();
            using var writer = CreateXmlWriter(stream);

            writer.WriteStartDocument(true);
            writer.WriteStartElement("worksheet", NsSpreadsheet);

            writer.WriteStartElement("sheetData");

            int currentRowIndex = 1;

            // 1. Write Header Row if provided
            if (headers != null && headers.Count > 0)
            {
                writer.WriteStartElement("row");
                writer.WriteAttributeString("r", currentRowIndex.ToString(CultureInfo.InvariantCulture));

                for (int col = 0; col < headers.Count; col++)
                {
                    string colLetter = ExcelCellAddress.IndexToColumnName(col + 1);
                    string cellRef = $"{colLetter}{currentRowIndex}";
                    WriteCellString(writer, cellRef, headers[col]);
                }

                writer.WriteEndElement(); // row
                currentRowIndex++;
            }

            // 2. Write Data Rows
            foreach (var rowValues in rows)
            {
                if (rowValues == null)
                {
                    currentRowIndex++;
                    continue;
                }

                writer.WriteStartElement("row");
                writer.WriteAttributeString("r", currentRowIndex.ToString(CultureInfo.InvariantCulture));

                for (int col = 0; col < rowValues.Count; col++)
                {
                    var val = rowValues[col];
                    if (val == null) continue;

                    string colLetter = ExcelCellAddress.IndexToColumnName(col + 1);
                    string cellRef = $"{colLetter}{currentRowIndex}";

                    WriteCellValue(writer, cellRef, val);
                }

                writer.WriteEndElement(); // row
                currentRowIndex++;
            }

            writer.WriteEndElement(); // sheetData
            writer.WriteEndElement(); // worksheet
            writer.WriteEndDocument();
        }

        #endregion

        #region Cell Value Helpers

        private static void WriteCellValue(XmlWriter writer, string cellRef, object val)
        {
            switch (val)
            {
                case bool b:
                    writer.WriteStartElement("c");
                    writer.WriteAttributeString("r", cellRef);
                    writer.WriteAttributeString("t", "b");
                    writer.WriteElementString("v", NsSpreadsheet, b ? "1" : "0");
                    writer.WriteEndElement();
                    break;

                case sbyte or byte or short or ushort or int or uint or long or ulong:
                    writer.WriteStartElement("c");
                    writer.WriteAttributeString("r", cellRef);
                    writer.WriteElementString("v", NsSpreadsheet, Convert.ToString(val, CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                    break;

                case float f:
                    writer.WriteStartElement("c");
                    writer.WriteAttributeString("r", cellRef);
                    writer.WriteElementString("v", NsSpreadsheet, f.ToString("R", CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                    break;

                case double d:
                    writer.WriteStartElement("c");
                    writer.WriteAttributeString("r", cellRef);
                    writer.WriteElementString("v", NsSpreadsheet, d.ToString("R", CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                    break;

                case decimal dec:
                    writer.WriteStartElement("c");
                    writer.WriteAttributeString("r", cellRef);
                    writer.WriteElementString("v", NsSpreadsheet, dec.ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                    break;

                case DateTime dt:
                    string dateStr = dt.TimeOfDay == TimeSpan.Zero
                        ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    WriteCellString(writer, cellRef, dateStr);
                    break;

                case DateTimeOffset dto:
                    WriteCellString(writer, cellRef, dto.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture));
                    break;

                default:
                    WriteCellString(writer, cellRef, val.ToString() ?? string.Empty);
                    break;
            }
        }

        private static void WriteCellString(XmlWriter writer, string cellRef, string rawText)
        {
            string cleanText = SanitizeXmlText(rawText);

            writer.WriteStartElement("c");
            writer.WriteAttributeString("r", cellRef);
            writer.WriteAttributeString("t", "inlineStr");

            writer.WriteStartElement("is");
            writer.WriteStartElement("t");
            if (cleanText.StartsWith(" ") || cleanText.EndsWith(" "))
            {
                writer.WriteAttributeString("xml", "space", null, "preserve");
            }
            writer.WriteString(cleanText);
            writer.WriteEndElement(); // t
            writer.WriteEndElement(); // is

            writer.WriteEndElement(); // c
        }

        private static string SanitizeSheetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Sheet1";

            // Excel sheet names cannot contain: \ / ? * : [ ] and max 31 characters
            var sb = new StringBuilder(name.Length);
            foreach (char ch in name)
            {
                if (ch == '\\' || ch == '/' || ch == '?' || ch == '*' || ch == ':' || ch == '[' || ch == ']')
                    sb.Append('_');
                else
                    sb.Append(ch);
            }

            string result = sb.ToString().Trim();
            if (result.Length > 31)
            {
                result = result.Substring(0, 31);
            }

            return string.IsNullOrEmpty(result) ? "Sheet1" : result;
        }

        private static string SanitizeXmlText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var sb = new StringBuilder(text.Length);
            foreach (char ch in text)
            {
                // XML 1.0 valid characters:
                // #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD]
                if (ch == 0x9 || ch == 0xA || ch == 0xD ||
                    (ch >= 0x20 && ch <= 0xD7FF) ||
                    (ch >= 0xE000 && ch <= 0xFFFD))
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }

        private static XmlWriter CreateXmlWriter(Stream stream)
        {
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                OmitXmlDeclaration = false,
                Indent = false,
                CloseOutput = false
            };
            return XmlWriter.Create(stream, settings);
        }

        #endregion
    }
}
