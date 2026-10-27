using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml;
using ZeroDocuments.Common;
using ZeroDocuments.Excel.Models;

namespace ZeroDocuments.Excel.Internal
{
    /// <summary>
    /// Describes a worksheet to be serialized. Rows are consumed lazily (forward-only) during packaging,
    /// so streaming sources (iterators, DataReaders) never need to be materialized in memory.
    /// </summary>
    internal sealed class WorksheetSpec
    {
        public string Name { get; set; } = "Sheet1";
        public IReadOnlyList<string>? Headers { get; set; }
        public IEnumerable<IReadOnlyList<object?>> Rows { get; set; } = Array.Empty<IReadOnlyList<object?>>();
        public bool HeaderBold { get; set; }
        public string? AutoFilterRange { get; set; }
        public bool AutoFilterEnabled { get; set; }
        public IReadOnlyList<ExcelImage> Images { get; set; } = Array.Empty<ExcelImage>();
        public IReadOnlyList<ExcelConditionalFormatRule> ConditionalFormatting { get; set; } = Array.Empty<ExcelConditionalFormatRule>();
        public string? ResolvedAutoFilterRange { get; set; }
    }

    /// <summary>
    /// Single OPC/SpreadsheetML package generator shared by <see cref="ExcelWriter"/> and <see cref="ExcelWorkbookBuilder"/>.
    /// </summary>
    internal static class XlsxPackageWriter
    {
        // cellXfs indices (must match WriteStyles ordering)
        private const int StyleDefault = 0;
        private const int StyleHeaderBold = 1;
        private const int StyleDate = 2;
        private const int StyleDateTime = 3;
        private const int StyleQuotePrefix = 4;
        private const int StyleHeaderBoldQuotePrefix = 5;

        private const int NumFmtDate = 164;
        private const int NumFmtDateTime = 165;
        private const string NumFmtDateCode = "yyyy-mm-dd";
        private const string NumFmtDateTimeCode = "yyyy-mm-dd hh:mm:ss";

        // Excel's 1900 date system incorrectly treats 1900 as a leap year; serials before 1900-03-01
        // are off by one day. Such dates are written as ISO text to avoid silent corruption.
        private static readonly DateTime MinNativeDate = new DateTime(1900, 3, 1);

        private const long EmuPerPixel = 9525L;

        public static void Write(Stream stream, IReadOnlyList<WorksheetSpec> sheets, FormulaInjectionMode formulaInjection)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (sheets == null || sheets.Count == 0) throw new ArgumentException("At least one worksheet is required.", nameof(sheets));

            using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);

            var dxfs = CollectDxfStyles(sheets);

            WriteContentTypes(zip, sheets);
            WriteGlobalRels(zip);
            WriteWorkbookRels(zip, sheets.Count);
            WriteStyles(zip, dxfs);

            int globalImageIndex = 1;
            for (int i = 0; i < sheets.Count; i++)
            {
                int sheetIndex = i + 1;
                var sheet = sheets[i];

                WriteWorksheet(zip, sheetIndex, sheet, dxfs, formulaInjection);

                if (sheet.Images.Count > 0)
                {
                    WriteSheetRels(zip, sheetIndex);
                    WriteDrawing(zip, sheetIndex, sheet.Images);
                    WriteDrawingRelsAndMedia(zip, sheetIndex, sheet.Images, ref globalImageIndex);
                }
            }

            // Write workbook after worksheets so resolved auto-filter ranges are populated
            WriteWorkbook(zip, sheets);
        }

        public static void Write(Stream stream, IReadOnlyList<WorksheetSpec> sheets, bool formulaInjectionProtection) =>
            Write(stream, sheets, formulaInjectionProtection ? FormulaInjectionMode.PrefixQuote : FormulaInjectionMode.Disabled);

        #region OPC Parts

        private static void WriteContentTypes(ZipArchive zip, IReadOnlyList<WorksheetSpec> sheets)
        {
            var entry = zip.CreateEntry("[Content_Types].xml", CompressionLevel.Fastest);
            using var stream = entry.Open();
            using var writer = SpreadsheetXml.CreateWriter(stream);

            writer.WriteStartDocument(true);
            writer.WriteStartElement("Types", SpreadsheetXml.NsContentTypes);

            WriteDefault(writer, "rels", "application/vnd.openxmlformats-package.relationships+xml");
            WriteDefault(writer, "xml", "application/xml");
            foreach (var ext in SpreadsheetXml.SupportedImageExtensions)
            {
                WriteDefault(writer, ext, SpreadsheetXml.GetImageContentType(ext)!);
            }

            WriteOverride(writer, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
            WriteOverride(writer, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");

            for (int i = 1; i <= sheets.Count; i++)
            {
                WriteOverride(writer, $"/xl/worksheets/sheet{i}.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
                if (sheets[i - 1].Images.Count > 0)
                {
                    WriteOverride(writer, $"/xl/drawings/drawing{i}.xml", "application/vnd.openxmlformats-officedocument.drawing+xml");
                }
            }

            writer.WriteEndElement(); // Types
            writer.WriteEndDocument();
        }

        private static void WriteDefault(XmlWriter writer, string extension, string contentType)
        {
            writer.WriteStartElement("Default");
            writer.WriteAttributeString("Extension", extension);
            writer.WriteAttributeString("ContentType", contentType);
            writer.WriteEndElement();
        }

        private static void WriteOverride(XmlWriter writer, string partName, string contentType)
        {
            writer.WriteStartElement("Override");
            writer.WriteAttributeString("PartName", partName);
            writer.WriteAttributeString("ContentType", contentType);
            writer.WriteEndElement();
        }

        private static void WriteGlobalRels(ZipArchive zip)
        {
            var entry = zip.CreateEntry("_rels/.rels", CompressionLevel.Fastest);
            using var stream = entry.Open();
            using var writer = SpreadsheetXml.CreateWriter(stream);

            writer.WriteStartDocument(true);
            writer.WriteStartElement("Relationships", SpreadsheetXml.NsPackageRelationships);
            WriteRelationship(writer, "rId1", SpreadsheetXml.RelTypeOfficeDocument, "xl/workbook.xml");
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        private static void WriteRelationship(XmlWriter writer, string id, string type, string target)
        {
            writer.WriteStartElement("Relationship");
            writer.WriteAttributeString("Id", id);
            writer.WriteAttributeString("Type", type);
            writer.WriteAttributeString("Target", target);
            writer.WriteEndElement();
        }

        private static void WriteWorkbook(ZipArchive zip, IReadOnlyList<WorksheetSpec> sheets)
        {
            var entry = zip.CreateEntry("xl/workbook.xml", CompressionLevel.Fastest);
            using var stream = entry.Open();
            using var writer = SpreadsheetXml.CreateWriter(stream);

            writer.WriteStartDocument(true);
            writer.WriteStartElement("workbook", SpreadsheetXml.NsMain);
            writer.WriteAttributeString("xmlns", "r", null, SpreadsheetXml.NsRelationships);

            writer.WriteStartElement("sheets");
            for (int i = 0; i < sheets.Count; i++)
            {
                writer.WriteStartElement("sheet");
                writer.WriteAttributeString("name", sheets[i].Name);
                writer.WriteAttributeString("sheetId", (i + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("id", SpreadsheetXml.NsRelationships, $"rId{i + 1}");
                writer.WriteEndElement();
            }
            writer.WriteEndElement(); // sheets

            bool hasAutoFilter = false;
            for (int i = 0; i < sheets.Count; i++)
            {
                if (!string.IsNullOrEmpty(sheets[i].ResolvedAutoFilterRange))
                {
                    hasAutoFilter = true;
                    break;
                }
            }

            if (hasAutoFilter)
            {
                writer.WriteStartElement("definedNames");
                for (int i = 0; i < sheets.Count; i++)
                {
                    string? filterRef = sheets[i].ResolvedAutoFilterRange;
                    if (!string.IsNullOrEmpty(filterRef))
                    {
                        ExcelCellAddress.ParseCellRange(filterRef, out var c1, out var r1, out var c2, out var r2);
                        writer.WriteStartElement("definedName");
                        writer.WriteAttributeString("name", "_xlnm._FilterDatabase");
                        writer.WriteAttributeString("localSheetId", i.ToString(CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("hidden", "1");
                        writer.WriteString($"'{sheets[i].Name.Replace("'", "''")}'!${c1}${r1}:${c2}${r2}");
                        writer.WriteEndElement();
                    }
                }
                writer.WriteEndElement(); // definedNames
            }

            writer.WriteEndElement(); // workbook
            writer.WriteEndDocument();
        }

        private static void WriteWorkbookRels(ZipArchive zip, int sheetCount)
        {
            var entry = zip.CreateEntry("xl/_rels/workbook.xml.rels", CompressionLevel.Fastest);
            using var stream = entry.Open();
            using var writer = SpreadsheetXml.CreateWriter(stream);

            writer.WriteStartDocument(true);
            writer.WriteStartElement("Relationships", SpreadsheetXml.NsPackageRelationships);

            for (int i = 1; i <= sheetCount; i++)
            {
                WriteRelationship(writer, $"rId{i}", SpreadsheetXml.RelTypeWorksheet, $"worksheets/sheet{i}.xml");
            }
            WriteRelationship(writer, $"rId{sheetCount + 1}", SpreadsheetXml.RelTypeStyles, "styles.xml");

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        private static void WriteStyles(ZipArchive zip, List<DxfStyle> dxfs)
        {
            var entry = zip.CreateEntry("xl/styles.xml", CompressionLevel.Fastest);
            using var stream = entry.Open();
            using var writer = SpreadsheetXml.CreateWriter(stream);

            writer.WriteStartDocument(true);
            writer.WriteStartElement("styleSheet", SpreadsheetXml.NsMain);

            // numFmts (custom ISO date formats, ids >= 164)
            writer.WriteStartElement("numFmts");
            writer.WriteAttributeString("count", "2");
            WriteNumFmt(writer, NumFmtDate, NumFmtDateCode);
            WriteNumFmt(writer, NumFmtDateTime, NumFmtDateTimeCode);
            writer.WriteEndElement();

            // fonts: 0 = regular, 1 = bold
            writer.WriteStartElement("fonts");
            writer.WriteAttributeString("count", "2");
            WriteFont(writer, bold: false);
            WriteFont(writer, bold: true);
            writer.WriteEndElement();

            // fills (0 and 1 are reserved by the spec)
            writer.WriteStartElement("fills");
            writer.WriteAttributeString("count", "2");
            WritePatternFill(writer, "none");
            WritePatternFill(writer, "gray125");
            writer.WriteEndElement();

            // borders
            writer.WriteStartElement("borders");
            writer.WriteAttributeString("count", "1");
            writer.WriteStartElement("border");
            writer.WriteElementString("left", SpreadsheetXml.NsMain, "");
            writer.WriteElementString("right", SpreadsheetXml.NsMain, "");
            writer.WriteElementString("top", SpreadsheetXml.NsMain, "");
            writer.WriteElementString("bottom", SpreadsheetXml.NsMain, "");
            writer.WriteElementString("diagonal", SpreadsheetXml.NsMain, "");
            writer.WriteEndElement();
            writer.WriteEndElement();

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

            // cellXfs (indices must match Style* constants)
            writer.WriteStartElement("cellXfs");
            writer.WriteAttributeString("count", "6");
            WriteCellXf(writer, numFmtId: 0, fontId: 0);
            WriteCellXf(writer, numFmtId: 0, fontId: 1);
            WriteCellXf(writer, numFmtId: NumFmtDate, fontId: 0);
            WriteCellXf(writer, numFmtId: NumFmtDateTime, fontId: 0);
            WriteCellXf(writer, numFmtId: 0, fontId: 0, quotePrefix: true);
            WriteCellXf(writer, numFmtId: 0, fontId: 1, quotePrefix: true);
            writer.WriteEndElement();

            // cellStyles
            writer.WriteStartElement("cellStyles");
            writer.WriteAttributeString("count", "1");
            writer.WriteStartElement("cellStyle");
            writer.WriteAttributeString("name", "Normal");
            writer.WriteAttributeString("xfId", "0");
            writer.WriteAttributeString("builtinId", "0");
            writer.WriteEndElement();
            writer.WriteEndElement();

            WriteDxfs(writer, dxfs);

            writer.WriteEndElement(); // styleSheet
            writer.WriteEndDocument();
        }

        private static void WriteNumFmt(XmlWriter writer, int id, string code)
        {
            writer.WriteStartElement("numFmt");
            writer.WriteAttributeString("numFmtId", id.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("formatCode", code);
            writer.WriteEndElement();
        }

        private static void WriteFont(XmlWriter writer, bool bold)
        {
            writer.WriteStartElement("font");
            if (bold)
            {
                writer.WriteStartElement("b");
                writer.WriteEndElement();
            }
            writer.WriteStartElement("sz");
            writer.WriteAttributeString("val", "11");
            writer.WriteEndElement();
            writer.WriteStartElement("name");
            writer.WriteAttributeString("val", "Calibri");
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WritePatternFill(XmlWriter writer, string patternType)
        {
            writer.WriteStartElement("fill");
            writer.WriteStartElement("patternFill");
            writer.WriteAttributeString("patternType", patternType);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteCellXf(XmlWriter writer, int numFmtId, int fontId, bool quotePrefix = false)
        {
            writer.WriteStartElement("xf");
            writer.WriteAttributeString("numFmtId", numFmtId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("fontId", fontId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("fillId", "0");
            writer.WriteAttributeString("borderId", "0");
            writer.WriteAttributeString("xfId", "0");
            if (fontId != 0) writer.WriteAttributeString("applyFont", "1");
            if (numFmtId != 0) writer.WriteAttributeString("applyNumberFormat", "1");
            if (quotePrefix) writer.WriteAttributeString("quotePrefix", "1");
            writer.WriteEndElement();
        }

        private static void WriteDxfs(XmlWriter writer, List<DxfStyle> dxfs)
        {
            if (dxfs.Count == 0) return;

            writer.WriteStartElement("dxfs");
            writer.WriteAttributeString("count", dxfs.Count.ToString(CultureInfo.InvariantCulture));

            foreach (var dxf in dxfs)
            {
                writer.WriteStartElement("dxf");

                if (!string.IsNullOrEmpty(dxf.FontColorHex) || dxf.Bold)
                {
                    writer.WriteStartElement("font");
                    if (dxf.Bold)
                    {
                        writer.WriteStartElement("b");
                        writer.WriteEndElement();
                    }
                    if (!string.IsNullOrEmpty(dxf.FontColorHex))
                    {
                        writer.WriteStartElement("color");
                        writer.WriteAttributeString("rgb", SpreadsheetXml.NormalizeColor(dxf.FontColorHex));
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement(); // font
                }

                if (!string.IsNullOrEmpty(dxf.FillColorHex))
                {
                    writer.WriteStartElement("fill");
                    writer.WriteStartElement("patternFill");
                    writer.WriteStartElement("bgColor");
                    writer.WriteAttributeString("rgb", SpreadsheetXml.NormalizeColor(dxf.FillColorHex));
                    writer.WriteEndElement(); // bgColor
                    writer.WriteEndElement(); // patternFill
                    writer.WriteEndElement(); // fill
                }

                writer.WriteEndElement(); // dxf
            }

            writer.WriteEndElement(); // dxfs
        }

        #endregion

        #region Worksheet

        private static void WriteWorksheet(ZipArchive zip, int sheetIndex, WorksheetSpec sheet, List<DxfStyle> dxfs, FormulaInjectionMode mode)
        {
            var entry = zip.CreateEntry($"xl/worksheets/sheet{sheetIndex}.xml", CompressionLevel.Fastest);
            using var stream = entry.Open();
            using var writer = SpreadsheetXml.CreateWriter(stream);

            writer.WriteStartDocument(true);
            writer.WriteStartElement("worksheet", SpreadsheetXml.NsMain);
            writer.WriteAttributeString("xmlns", "r", null, SpreadsheetXml.NsRelationships);

            writer.WriteStartElement("sheetData");

            int currentRowIndex = 1;

            // 1. Header row
            if (sheet.Headers != null && sheet.Headers.Count > 0)
            {
                EnsureColumnLimit(sheet.Headers.Count, sheet.Name);

                string rowText = currentRowIndex.ToString(CultureInfo.InvariantCulture);
                writer.WriteStartElement("row");
                writer.WriteAttributeString("r", rowText);

                int headerStyle = sheet.HeaderBold ? StyleHeaderBold : StyleDefault;
                for (int col = 0; col < sheet.Headers.Count; col++)
                {
                    string cellRef = SpreadsheetXml.GetColumnName(col + 1) + rowText;
                    WriteInlineString(writer, cellRef, sheet.Headers[col], mode, headerStyle);
                }

                writer.WriteEndElement(); // row
                currentRowIndex++;
            }

            // 2. Data rows (consumed lazily)
            foreach (var rowValues in sheet.Rows)
            {
                if (rowValues == null)
                {
                    currentRowIndex++;
                    continue;
                }

                if (currentRowIndex > MaxRows)
                {
                    throw new InvalidOperationException(
                        $"Worksheet '{sheet.Name}' exceeds Excel's limit of {MaxRows:N0} rows. Split the data across multiple sheets.");
                }
                EnsureColumnLimit(rowValues.Count, sheet.Name);

                string rowText = currentRowIndex.ToString(CultureInfo.InvariantCulture);
                writer.WriteStartElement("row");
                writer.WriteAttributeString("r", rowText);

                for (int col = 0; col < rowValues.Count; col++)
                {
                    var val = rowValues[col];
                    if (val == null || val is DBNull) continue;

                    string cellRef = SpreadsheetXml.GetColumnName(col + 1) + rowText;
                    WriteCellValue(writer, cellRef, val, mode);
                }

                writer.WriteEndElement(); // row
                currentRowIndex++;
            }

            int lastRowIndex = Math.Max(1, currentRowIndex - 1);
            writer.WriteEndElement(); // sheetData

            // 3. AutoFilter
            if (!string.IsNullOrEmpty(sheet.AutoFilterRange))
            {
                sheet.ResolvedAutoFilterRange = sheet.AutoFilterRange;
                writer.WriteStartElement("autoFilter");
                writer.WriteAttributeString("ref", sheet.AutoFilterRange);
                writer.WriteEndElement();
            }
            else if (sheet.AutoFilterEnabled && sheet.Headers != null && sheet.Headers.Count > 0)
            {
                string lastCol = SpreadsheetXml.GetColumnName(sheet.Headers.Count);
                sheet.ResolvedAutoFilterRange = $"A1:{lastCol}{lastRowIndex.ToString(CultureInfo.InvariantCulture)}";
                writer.WriteStartElement("autoFilter");
                writer.WriteAttributeString("ref", sheet.ResolvedAutoFilterRange);
                writer.WriteEndElement();
            }

            // 4. Conditional formatting
            WriteConditionalFormatting(writer, sheet.ConditionalFormatting, dxfs);

            // 5. Drawing reference
            if (sheet.Images.Count > 0)
            {
                writer.WriteStartElement("drawing");
                writer.WriteAttributeString("id", SpreadsheetXml.NsRelationships, "rIdDrawing");
                writer.WriteEndElement();
            }

            writer.WriteEndElement(); // worksheet
            writer.WriteEndDocument();
        }

        private const int MaxRows = 1048576;

        private static void EnsureColumnLimit(int columnCount, string sheetName)
        {
            if (columnCount > SpreadsheetXml.MaxColumns)
            {
                throw new InvalidOperationException(
                    $"Worksheet '{sheetName}' has {columnCount:N0} columns; Excel supports at most {SpreadsheetXml.MaxColumns:N0} (XFD).");
            }
        }

        /// <summary>
        /// ST_Sqref is a space-separated list; accept the comma-separated form users commonly type.
        /// </summary>
        private static string NormalizeSqref(string range) =>
            string.Join(" ", range.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries));

        private static void WriteConditionalFormatting(XmlWriter writer, IReadOnlyList<ExcelConditionalFormatRule> rules, List<DxfStyle> dxfs)
        {
            int priority = 1;
            foreach (var rule in rules)
            {
                writer.WriteStartElement("conditionalFormatting");
                writer.WriteAttributeString("sqref", NormalizeSqref(rule.Range));

                if (rule is ExcelHighlightRule hr)
                {
                    writer.WriteStartElement("cfRule");
                    writer.WriteAttributeString("type", "cellIs");
                    writer.WriteAttributeString("operator", GetOperatorAttribute(hr.Operator));
                    writer.WriteAttributeString("priority", priority.ToString(CultureInfo.InvariantCulture));

                    int dxfId = DxfStyle.TryCreate(hr, out var key) ? dxfs.IndexOf(key!) : -1;
                    if (dxfId >= 0)
                    {
                        writer.WriteAttributeString("dxfId", dxfId.ToString(CultureInfo.InvariantCulture));
                    }

                    writer.WriteElementString("formula", SpreadsheetXml.NsMain, SpreadsheetXml.SanitizeXmlText(hr.Formula1));
                    if (!string.IsNullOrEmpty(hr.Formula2))
                    {
                        writer.WriteElementString("formula", SpreadsheetXml.NsMain, SpreadsheetXml.SanitizeXmlText(hr.Formula2));
                    }

                    writer.WriteEndElement(); // cfRule
                }
                else if (rule is ExcelColorScaleRule csr)
                {
                    bool hasMid = !string.IsNullOrEmpty(csr.MidColorHex);

                    writer.WriteStartElement("cfRule");
                    writer.WriteAttributeString("type", "colorScale");
                    writer.WriteAttributeString("priority", priority.ToString(CultureInfo.InvariantCulture));
                    writer.WriteStartElement("colorScale");

                    WriteCfvo(writer, "min", null);
                    if (hasMid) WriteCfvo(writer, "percentile", "50");
                    WriteCfvo(writer, "max", null);

                    WriteColor(writer, csr.MinColorHex);
                    if (hasMid) WriteColor(writer, csr.MidColorHex);
                    WriteColor(writer, csr.MaxColorHex);

                    writer.WriteEndElement(); // colorScale
                    writer.WriteEndElement(); // cfRule
                }
                else if (rule is ExcelDataBarRule dbr)
                {
                    writer.WriteStartElement("cfRule");
                    writer.WriteAttributeString("type", "dataBar");
                    writer.WriteAttributeString("priority", priority.ToString(CultureInfo.InvariantCulture));
                    writer.WriteStartElement("dataBar");

                    WriteCfvo(writer, "min", null);
                    WriteCfvo(writer, "max", null);
                    WriteColor(writer, dbr.ColorHex);

                    writer.WriteEndElement(); // dataBar
                    writer.WriteEndElement(); // cfRule
                }

                writer.WriteEndElement(); // conditionalFormatting
                priority++;
            }
        }

        private static void WriteCfvo(XmlWriter writer, string type, string? val)
        {
            writer.WriteStartElement("cfvo");
            writer.WriteAttributeString("type", type);
            if (val != null) writer.WriteAttributeString("val", val);
            writer.WriteEndElement();
        }

        private static void WriteColor(XmlWriter writer, string? hex)
        {
            writer.WriteStartElement("color");
            writer.WriteAttributeString("rgb", SpreadsheetXml.NormalizeColor(hex));
            writer.WriteEndElement();
        }

        private static string GetOperatorAttribute(CellRuleOperator op) => op switch
        {
            CellRuleOperator.GreaterThan => "greaterThan",
            CellRuleOperator.LessThan => "lessThan",
            CellRuleOperator.Equal => "equal",
            CellRuleOperator.NotEqual => "notEqual",
            CellRuleOperator.GreaterThanOrEqual => "greaterThanOrEqual",
            CellRuleOperator.LessThanOrEqual => "lessThanOrEqual",
            CellRuleOperator.Between => "between",
            CellRuleOperator.NotBetween => "notBetween",
            _ => "greaterThan"
        };

        #endregion

        #region Cell Serialization

        /// <summary>
        /// Writes a typed cell. Numbers and booleans are written natively; DateTime is written as an
        /// OLE Automation serial with an ISO date/datetime number format so Excel treats it as a real date.
        /// </summary>
        internal static void WriteCellValue(XmlWriter writer, string cellRef, object val, FormulaInjectionMode mode)
        {
            switch (val)
            {
                case string s:
                    WriteInlineString(writer, cellRef, s, mode, StyleDefault);
                    break;

                case bool b:
                    WriteRawCell(writer, cellRef, "b", StyleDefault, b ? "1" : "0");
                    break;

                case sbyte or byte or short or ushort or int or uint or long or ulong:
                    WriteRawCell(writer, cellRef, null, StyleDefault, Convert.ToString(val, CultureInfo.InvariantCulture)!);
                    break;

                case float f:
                    if (float.IsNaN(f) || float.IsInfinity(f))
                        WriteInlineString(writer, cellRef, f.ToString("R", CultureInfo.InvariantCulture), FormulaInjectionMode.Disabled, StyleDefault);
                    else
                        WriteRawCell(writer, cellRef, null, StyleDefault, f.ToString("R", CultureInfo.InvariantCulture));
                    break;

                case double d:
                    // NaN/Infinity are not valid xsd:double cell values in SpreadsheetML and trigger Excel's repair dialog.
                    if (double.IsNaN(d) || double.IsInfinity(d))
                        WriteInlineString(writer, cellRef, d.ToString("R", CultureInfo.InvariantCulture), FormulaInjectionMode.Disabled, StyleDefault);
                    else
                        WriteRawCell(writer, cellRef, null, StyleDefault, d.ToString("R", CultureInfo.InvariantCulture));
                    break;

                case decimal dec:
                    WriteRawCell(writer, cellRef, null, StyleDefault, dec.ToString(CultureInfo.InvariantCulture));
                    break;

                case DateTime dt:
                    if (dt >= MinNativeDate)
                    {
                        int style = dt.TimeOfDay == TimeSpan.Zero ? StyleDate : StyleDateTime;
                        WriteRawCell(writer, cellRef, null, style, dt.ToOADate().ToString("R", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        WriteInlineString(writer, cellRef, OADateFormatter.FormatIso(dt), mode, StyleDefault);
                    }
                    break;

                case DateTimeOffset dto:
                    WriteInlineString(writer, cellRef, dto.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture), mode, StyleDefault);
                    break;

                case IFormattable formattable:
                    WriteInlineString(writer, cellRef, formattable.ToString(null, CultureInfo.InvariantCulture), mode, StyleDefault);
                    break;

                default:
                    WriteInlineString(writer, cellRef, val.ToString() ?? string.Empty, mode, StyleDefault);
                    break;
            }
        }

        private static void WriteRawCell(XmlWriter writer, string cellRef, string? type, int styleIndex, string value)
        {
            writer.WriteStartElement("c");
            writer.WriteAttributeString("r", cellRef);
            if (styleIndex > 0) writer.WriteAttributeString("s", styleIndex.ToString(CultureInfo.InvariantCulture));
            if (type != null) writer.WriteAttributeString("t", type);
            writer.WriteElementString("v", SpreadsheetXml.NsMain, value);
            writer.WriteEndElement();
        }

        internal static void WriteInlineString(XmlWriter writer, string cellRef, string? rawText, FormulaInjectionMode mode, int baseStyle)
        {
            string? text = rawText;
            int styleIndex = baseStyle;

            if (mode == FormulaInjectionMode.PrefixQuote)
            {
                text = FormulaInjectionGuard.Sanitize(rawText);
            }
            else if (mode == FormulaInjectionMode.QuotePrefixStyle)
            {
                if (!string.IsNullOrEmpty(rawText) && FormulaInjectionGuard.IsTrigger(rawText![0]))
                {
                    styleIndex = baseStyle == StyleHeaderBold ? StyleHeaderBoldQuotePrefix : StyleQuotePrefix;
                }
            }

            string cleanText = SpreadsheetXml.SanitizeXmlText(text);

            writer.WriteStartElement("c");
            writer.WriteAttributeString("r", cellRef);
            if (styleIndex > 0) writer.WriteAttributeString("s", styleIndex.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("t", "inlineStr");

            writer.WriteStartElement("is");
            writer.WriteStartElement("t");
            if (cleanText.Length > 0 && (char.IsWhiteSpace(cleanText[0]) || char.IsWhiteSpace(cleanText[cleanText.Length - 1])))
            {
                writer.WriteAttributeString("xml", "space", null, "preserve");
            }
            writer.WriteString(cleanText);
            writer.WriteEndElement(); // t
            writer.WriteEndElement(); // is

            writer.WriteEndElement(); // c
        }

        #endregion

        #region DrawingML

        private static void WriteSheetRels(ZipArchive zip, int sheetIndex)
        {
            var entry = zip.CreateEntry($"xl/worksheets/_rels/sheet{sheetIndex}.xml.rels", CompressionLevel.Fastest);
            using var stream = entry.Open();
            using var writer = SpreadsheetXml.CreateWriter(stream);

            writer.WriteStartDocument(true);
            writer.WriteStartElement("Relationships", SpreadsheetXml.NsPackageRelationships);
            WriteRelationship(writer, "rIdDrawing", SpreadsheetXml.RelTypeDrawing, $"../drawings/drawing{sheetIndex}.xml");
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        private static void WriteDrawing(ZipArchive zip, int sheetIndex, IReadOnlyList<ExcelImage> images)
        {
            var entry = zip.CreateEntry($"xl/drawings/drawing{sheetIndex}.xml", CompressionLevel.Fastest);
            using var stream = entry.Open();
            using var writer = SpreadsheetXml.CreateWriter(stream);

            const string xdr = SpreadsheetXml.NsDrawingSheet;
            const string a = SpreadsheetXml.NsDrawingMain;

            writer.WriteStartDocument(true);
            writer.WriteStartElement("xdr", "wsDr", xdr);
            writer.WriteAttributeString("xmlns", "a", null, a);
            writer.WriteAttributeString("xmlns", "r", null, SpreadsheetXml.NsRelationships);

            for (int j = 0; j < images.Count; j++)
            {
                var img = images[j];
                int col = Math.Max(0, img.ColumnIndex - 1);
                int row = Math.Max(0, img.RowIndex - 1);
                string colOff = ((long)img.ColOffsetPx * EmuPerPixel).ToString(CultureInfo.InvariantCulture);
                string rowOff = ((long)img.RowOffsetPx * EmuPerPixel).ToString(CultureInfo.InvariantCulture);
                string cx = ((long)img.WidthPx * EmuPerPixel).ToString(CultureInfo.InvariantCulture);
                string cy = ((long)img.HeightPx * EmuPerPixel).ToString(CultureInfo.InvariantCulture);

                writer.WriteStartElement("xdr", "oneCellAnchor", xdr);

                writer.WriteStartElement("xdr", "from", xdr);
                writer.WriteElementString("xdr", "col", xdr, col.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("xdr", "colOff", xdr, colOff);
                writer.WriteElementString("xdr", "row", xdr, row.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("xdr", "rowOff", xdr, rowOff);
                writer.WriteEndElement(); // from

                writer.WriteStartElement("xdr", "ext", xdr);
                writer.WriteAttributeString("cx", cx);
                writer.WriteAttributeString("cy", cy);
                writer.WriteEndElement(); // ext

                writer.WriteStartElement("xdr", "pic", xdr);

                writer.WriteStartElement("xdr", "nvPicPr", xdr);
                writer.WriteStartElement("xdr", "cNvPr", xdr);
                writer.WriteAttributeString("id", (j + 2).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("name", string.IsNullOrWhiteSpace(img.Name) ? $"Picture {j + 1}" : SpreadsheetXml.SanitizeXmlText(img.Name));
                writer.WriteEndElement(); // cNvPr
                writer.WriteStartElement("xdr", "cNvPicPr", xdr);
                writer.WriteStartElement("a", "picLocks", a);
                writer.WriteAttributeString("noChangeAspect", "1");
                writer.WriteEndElement(); // picLocks
                writer.WriteEndElement(); // cNvPicPr
                writer.WriteEndElement(); // nvPicPr

                writer.WriteStartElement("xdr", "blipFill", xdr);
                writer.WriteStartElement("a", "blip", a);
                writer.WriteAttributeString("embed", SpreadsheetXml.NsRelationships, $"rId{j + 1}");
                writer.WriteEndElement(); // blip
                writer.WriteStartElement("a", "stretch", a);
                writer.WriteStartElement("a", "fillRect", a);
                writer.WriteEndElement(); // fillRect
                writer.WriteEndElement(); // stretch
                writer.WriteEndElement(); // blipFill

                writer.WriteStartElement("xdr", "spPr", xdr);
                writer.WriteStartElement("a", "xfrm", a);
                writer.WriteStartElement("a", "off", a);
                writer.WriteAttributeString("x", "0");
                writer.WriteAttributeString("y", "0");
                writer.WriteEndElement(); // off
                writer.WriteStartElement("a", "ext", a);
                writer.WriteAttributeString("cx", cx);
                writer.WriteAttributeString("cy", cy);
                writer.WriteEndElement(); // ext
                writer.WriteEndElement(); // xfrm
                writer.WriteStartElement("a", "prstGeom", a);
                writer.WriteAttributeString("prst", "rect");
                writer.WriteStartElement("a", "avLst", a);
                writer.WriteEndElement(); // avLst
                writer.WriteEndElement(); // prstGeom
                writer.WriteEndElement(); // spPr

                writer.WriteEndElement(); // pic

                writer.WriteStartElement("xdr", "clientData", xdr);
                writer.WriteEndElement(); // clientData

                writer.WriteEndElement(); // oneCellAnchor
            }

            writer.WriteEndElement(); // wsDr
            writer.WriteEndDocument();
        }

        private static void WriteDrawingRelsAndMedia(ZipArchive zip, int sheetIndex, IReadOnlyList<ExcelImage> images, ref int globalImageIndex)
        {
            int startGlobalIndex = globalImageIndex;

            var relsEntry = zip.CreateEntry($"xl/drawings/_rels/drawing{sheetIndex}.xml.rels", CompressionLevel.Fastest);
            using (var relsStream = relsEntry.Open())
            using (var writer = SpreadsheetXml.CreateWriter(relsStream))
            {
                writer.WriteStartDocument(true);
                writer.WriteStartElement("Relationships", SpreadsheetXml.NsPackageRelationships);

                for (int j = 0; j < images.Count; j++)
                {
                    WriteRelationship(writer, $"rId{j + 1}", SpreadsheetXml.RelTypeImage, $"../media/image{startGlobalIndex + j}.{images[j].Format}");
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            // Media binaries are written after the rels stream is closed (ZipArchive allows one open entry at a time).
            for (int j = 0; j < images.Count; j++)
            {
                var img = images[j];
                var mediaEntry = zip.CreateEntry($"xl/media/image{startGlobalIndex + j}.{img.Format}", CompressionLevel.Optimal);
                using var mediaStream = mediaEntry.Open();
                mediaStream.Write(img.Data, 0, img.Data.Length);
            }

            globalImageIndex = startGlobalIndex + images.Count;
        }

        #endregion

        #region DXF Styles

        private static List<DxfStyle> CollectDxfStyles(IReadOnlyList<WorksheetSpec> sheets)
        {
            var dxfs = new List<DxfStyle>();
            foreach (var sheet in sheets)
            {
                foreach (var rule in sheet.ConditionalFormatting)
                {
                    if (rule is ExcelHighlightRule hr && DxfStyle.TryCreate(hr, out var def) && !dxfs.Contains(def!))
                    {
                        dxfs.Add(def!);
                    }
                }
            }
            return dxfs;
        }

        private sealed class DxfStyle : IEquatable<DxfStyle>
        {
            public string? FillColorHex { get; private set; }
            public string? FontColorHex { get; private set; }
            public bool Bold { get; private set; }

            public static bool TryCreate(ExcelHighlightRule hr, out DxfStyle? style)
            {
                if (string.IsNullOrEmpty(hr.FillColorHex) && string.IsNullOrEmpty(hr.FontColorHex) && !hr.Bold)
                {
                    style = null;
                    return false;
                }

                style = new DxfStyle { FillColorHex = hr.FillColorHex, FontColorHex = hr.FontColorHex, Bold = hr.Bold };
                return true;
            }

            public bool Equals(DxfStyle? other) =>
                other != null &&
                string.Equals(FillColorHex, other.FillColorHex, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(FontColorHex, other.FontColorHex, StringComparison.OrdinalIgnoreCase) &&
                Bold == other.Bold;

            public override bool Equals(object? obj) => Equals(obj as DxfStyle);

            public override int GetHashCode()
            {
                int hash = 17;
                hash = hash * 31 + (FillColorHex != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(FillColorHex) : 0);
                hash = hash * 31 + (FontColorHex != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(FontColorHex) : 0);
                hash = hash * 31 + Bold.GetHashCode();
                return hash;
            }
        }

        #endregion
    }
}
