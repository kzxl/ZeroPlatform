using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using ZeroDocuments.Common;
using ZeroDocuments.Excel.Internal;
using ZeroDocuments.Excel.Models;

namespace ZeroDocuments.Excel
{
    /// <summary>
    /// Pure C# Zero-Dependency OpenXML Excel (.xlsx) Reader.
    /// High-performance, streaming-first architecture using a single forward-only XmlReader.
    /// Operates with &lt; 15MB RAM regardless of sheet row count (excluding the shared string table).
    /// </summary>
    public static class ExcelReader
    {
        static ExcelReader()
        {
            RuntimeAssemblyResolver.EnsureInitialized();
        }

        #region Public DataTable & POCO APIs

        /// <summary>
        /// Reads Excel sheet into a DataTable bounded by header range (e.g. "D24:T24" or "A1:C1").
        /// The header row itself is skipped; columns are named positionally ("Column_D", "Column_E", ...).
        /// Use <see cref="ReadWithHeaders(string, string, int, string?)"/> to name columns from the header row.
        /// </summary>
        public static DataTable ReadByHeaderRange(string filePath, string headerRange, int maxRows = 5000, string? sheetName = null)
        {
            string fullDataRange = ExcelCellAddress.ConvertHeaderRangeToDataRange(headerRange, maxRows);
            return ReadToDataTable(filePath, fullDataRange, sheetName);
        }

        /// <summary>
        /// Reads a header-bounded table (e.g. "D24:T24") into a DataTable whose column names are taken from the header row.
        /// Blank headers fall back to "Column_{Letter}"; duplicate headers are suffixed ("Qty", "Qty_2").
        /// Data rows are read below the header up to <paramref name="maxRows"/> rows.
        /// </summary>
        public static DataTable ReadWithHeaders(string filePath, string headerRange, int maxRows = 5000, string? sheetName = null)
        {
            EnsureFileExists(filePath);
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return ReadWithHeaders(stream, headerRange, maxRows, sheetName);
        }

        /// <summary>
        /// Reads a header-bounded table from a stream into a DataTable whose column names are taken from the header row.
        /// </summary>
        public static DataTable ReadWithHeaders(Stream stream, string headerRange, int maxRows = 5000, string? sheetName = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (maxRows < 0) throw new ArgumentOutOfRangeException(nameof(maxRows));

            ExcelCellAddress.ParseCellRange(headerRange, out var startCol, out var headerRow, out var endCol, out _);
            int startColIdx = ExcelCellAddress.ColumnNameToIndex(startCol);
            int endColIdx = ExcelCellAddress.ColumnNameToIndex(endCol);
            string range = $"{startCol}{headerRow}:{endCol}{(long)headerRow + maxRows}";

            var table = new DataTable();
            bool headersResolved = false;

            foreach (var row in StreamRows(stream, range, sheetName))
            {
                if (!headersResolved)
                {
                    headersResolved = true;
                    var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    bool isHeaderRow = row.RowNumber == headerRow;

                    for (int c = startColIdx; c <= endColIdx; c++)
                    {
                        string? text = isHeaderRow ? row[c]?.Trim() : null;
                        string baseName = string.IsNullOrEmpty(text) ? "Column_" + ExcelCellAddress.IndexToColumnName(c) : text!;
                        string unique = baseName;
                        for (int n = 2; !used.Add(unique); n++) unique = baseName + "_" + n.ToString(CultureInfo.InvariantCulture);
                        table.Columns.Add(unique, typeof(string));
                    }

                    if (isHeaderRow) continue;
                }

                AddDataRow(table, row, startColIdx, endColIdx);
            }

            if (!headersResolved)
            {
                for (int c = startColIdx; c <= endColIdx; c++)
                {
                    table.Columns.Add("Column_" + ExcelCellAddress.IndexToColumnName(c), typeof(string));
                }
            }

            return table;
        }

        /// <summary>
        /// Reads Excel file from file path into a DataTable.
        /// </summary>
        public static DataTable ReadToDataTable(string filePath, string cellRange = ExcelCellAddress.DefaultRange, string? sheetName = null) =>
            ReadToDataTable(filePath, null, cellRange, sheetName);

        /// <summary>
        /// Reads Excel file from file path into a DataTable with custom reader options.
        /// </summary>
        public static DataTable ReadToDataTable(string filePath, ExcelReaderOptions? options, string cellRange = ExcelCellAddress.DefaultRange, string? sheetName = null)
        {
            EnsureFileExists(filePath);
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return ReadToDataTable(stream, options, cellRange, sheetName);
        }

        /// <summary>
        /// Reads Excel stream into a DataTable. Rows are streamed directly into the table (no intermediate row list).
        /// When using default range, only columns present in the data are created.
        /// </summary>
        public static DataTable ReadToDataTable(Stream stream, string cellRange = ExcelCellAddress.DefaultRange, string? sheetName = null) =>
            ReadToDataTable(stream, null, cellRange, sheetName);

        /// <summary>
        /// Reads Excel stream into a DataTable with custom reader options.
        /// </summary>
        public static DataTable ReadToDataTable(Stream stream, ExcelReaderOptions? options, string cellRange = ExcelCellAddress.DefaultRange, string? sheetName = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var table = new DataTable();
            bool isDefaultRange = string.IsNullOrWhiteSpace(cellRange) ||
                                  string.Equals(cellRange, ExcelCellAddress.DefaultRange, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(cellRange, "A1:ZZ1048576", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(cellRange, "A1:XFD1048576", StringComparison.OrdinalIgnoreCase);

            if (!isDefaultRange)
            {
                ExcelCellAddress.ParseCellRange(cellRange, out var startCol, out _, out var endCol, out _);
                int startColIdx = ExcelCellAddress.ColumnNameToIndex(startCol);
                int endColIdx = ExcelCellAddress.ColumnNameToIndex(endCol);

                for (int c = startColIdx; c <= endColIdx; c++)
                {
                    table.Columns.Add("Column_" + ExcelCellAddress.IndexToColumnName(c), typeof(string));
                }

                foreach (var row in StreamRows(stream, cellRange, sheetName, options))
                {
                    AddDataRow(table, row, startColIdx, endColIdx);
                }
            }
            else
            {
                foreach (var row in StreamRows(stream, cellRange, sheetName, options))
                {
                    if (row.Cells.Count == 0) continue;

                    int maxColInRow = 0;
                    foreach (var col in row.Cells.Keys)
                    {
                        if (col > maxColInRow) maxColInRow = col;
                    }

                    while (table.Columns.Count < maxColInRow)
                    {
                        int nextCol = table.Columns.Count + 1;
                        table.Columns.Add("Column_" + ExcelCellAddress.IndexToColumnName(nextCol), typeof(string));
                    }

                    var dataRow = table.NewRow();
                    bool hasData = false;
                    foreach (var kvp in row.Cells)
                    {
                        if (!string.IsNullOrEmpty(kvp.Value))
                        {
                            hasData = true;
                            dataRow[kvp.Key - 1] = kvp.Value;
                        }
                    }

                    if (hasData)
                    {
                        table.Rows.Add(dataRow);
                    }
                }
            }

            return table;
        }

        private static void AddDataRow(DataTable table, ExcelRow row, int startColIdx, int endColIdx)
        {
            var rowVals = new object?[endColIdx - startColIdx + 1];
            bool hasData = false;

            for (int c = startColIdx; c <= endColIdx; c++)
            {
                var val = row[c];
                if (!string.IsNullOrEmpty(val)) hasData = true;
                rowVals[c - startColIdx] = val;
            }

            if (hasData)
            {
                table.Rows.Add(rowVals);
            }
        }

        /// <summary>
        /// Reads an Excel file directly into strongly-typed POCO objects using compiled PropertyAccessorCache.
        /// </summary>
        public static List<T> Read<T>(string filePath, string cellRange = ExcelCellAddress.DefaultRange, string? sheetName = null) where T : new()
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Read<T>(stream, cellRange, sheetName);
        }

        /// <summary>
        /// Reads an Excel stream directly into strongly-typed POCO objects using compiled PropertyAccessorCache.
        /// The first non-empty row in the range is treated as the header row (matched case-insensitively to property names).
        /// Values that cannot be converted to the property type are skipped.
        /// </summary>
        public static List<T> Read<T>(Stream stream, string cellRange = ExcelCellAddress.DefaultRange, string? sheetName = null) where T : new()
        {
            var list = new List<T>();

            var accessors = PropertyAccessorCache.GetAccessors(typeof(T));
            var accessorMap = new Dictionary<string, PropertyAccessorCache.PropertyAccessorInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var acc in accessors)
            {
                accessorMap[acc.Name] = acc;
            }

            var columnBindings = new List<KeyValuePair<int, PropertyAccessorCache.PropertyAccessorInfo>>();
            bool isFirst = true;

            foreach (var row in StreamRows(stream, cellRange, sheetName))
            {
                if (isFirst)
                {
                    isFirst = false;
                    foreach (var colIdx in row.PopulatedColumns)
                    {
                        string? header = row[colIdx];
                        if (!string.IsNullOrWhiteSpace(header) &&
                            accessorMap.TryGetValue(header!.Trim(), out var acc) &&
                            acc.Setter != null)
                        {
                            columnBindings.Add(new KeyValuePair<int, PropertyAccessorCache.PropertyAccessorInfo>(colIdx, acc));
                        }
                    }
                    continue;
                }

                // Box once so setters mutate the same instance even when T is a value type.
                object item = new T();
                bool assigned = false;

                foreach (var binding in columnBindings)
                {
                    string? rawVal = row[binding.Key];
                    if (rawVal == null) continue;

                    try
                    {
                        binding.Value.Setter!(item, ValueConverter.Convert(rawVal, binding.Value.PropertyType));
                        assigned = true;
                    }
                    catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is InvalidCastException || ex is ArgumentException)
                    {
                        // Unconvertible values are skipped (lenient hydration).
                    }
                }

                if (assigned)
                {
                    list.Add((T)item);
                }
            }

            return list;
        }

        #endregion

        #region Streaming Row Reading APIs

        /// <summary>
        /// Reads Excel rows from file as a list of ExcelRow objects.
        /// </summary>
        public static List<ExcelRow> ReadRows(string filePath, string cellRange = ExcelCellAddress.DefaultRange, string? sheetName = null)
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return ReadRows(stream, cellRange, sheetName);
        }

        /// <summary>
        /// Reads Excel stream rows as a list of ExcelRow objects.
        /// </summary>
        public static List<ExcelRow> ReadRows(Stream stream, string cellRange = ExcelCellAddress.DefaultRange, string? sheetName = null)
        {
            return new List<ExcelRow>(StreamRows(stream, cellRange, sheetName));
        }

        /// <summary>
        /// Streams Excel rows lazily from a file. The file handle is released when enumeration completes or is disposed.
        /// </summary>
        public static IEnumerable<ExcelRow> StreamRows(string filePath, string cellRange = ExcelCellAddress.DefaultRange, string? sheetName = null) =>
            StreamRows(filePath, null, cellRange, sheetName);

        /// <summary>
        /// Streams Excel rows lazily from a file with custom reader options.
        /// </summary>
        public static IEnumerable<ExcelRow> StreamRows(string filePath, ExcelReaderOptions? options, string cellRange = ExcelCellAddress.DefaultRange, string? sheetName = null)
        {
            EnsureFileExists(filePath);
            return StreamRowsFromFile(filePath, cellRange, sheetName, options);
        }

        private static IEnumerable<ExcelRow> StreamRowsFromFile(string filePath, string cellRange, string? sheetName, ExcelReaderOptions? options)
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            foreach (var row in StreamRows(stream, cellRange, sheetName, options))
            {
                yield return row;
            }
        }

        /// <summary>
        /// Streams Excel rows lazily using a single forward-only XmlReader.
        /// Date-formatted numeric cells are returned as ISO text ("yyyy-MM-dd" or "yyyy-MM-dd HH:mm:ss").
        /// Minimal RAM footprint (&lt; 15MB) for maximum scalability.
        /// </summary>
        public static IEnumerable<ExcelRow> StreamRows(Stream stream, string cellRange = ExcelCellAddress.DefaultRange, string? sheetName = null) =>
            StreamRows(stream, cellRange, sheetName, null);

        /// <summary>
        /// Streams Excel rows lazily using a single forward-only XmlReader with custom reader options.
        /// </summary>
        public static IEnumerable<ExcelRow> StreamRows(Stream stream, string cellRange, string? sheetName, ExcelReaderOptions? options)
        {
            ExcelCellAddress.ParseCellRange(cellRange, out var startCol, out var startRow, out var endCol, out var endRow);
            int startColIdx = ExcelCellAddress.ColumnNameToIndex(startCol);
            int endColIdx = ExcelCellAddress.ColumnNameToIndex(endCol);

            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var package = XlsxPackageReader.Open(zip, options);

            var sheetEntry = package.FindSheetEntry(sheetName);
            if (sheetEntry == null) yield break;

            var context = new SheetReadContext(package.LoadSharedStrings(), package.LoadDateStyleMap(), package.Date1904);

            using var sheetStream = package.OpenEntryStream(sheetEntry);
            using var reader = SpreadsheetXml.CreateReader(sheetStream);

            int lastRowNum = 0;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "sheetData") yield break;
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "row") continue;

                // Row "r" is optional in SpreadsheetML; implicit rows follow the previous row.
                int rowNum = TryParsePositiveInt(reader.GetAttribute("r"), out int r) ? r : lastRowNum + 1;
                lastRowNum = rowNum;

                if (rowNum > endRow) yield break;
                if (reader.IsEmptyElement) continue;

                if (rowNum < startRow)
                {
                    SkipToEndElement(reader);
                    continue;
                }

                var excelRow = ReadRowCells(reader, rowNum, startColIdx, endColIdx, context, out bool hasAnyCell);
                if (hasAnyCell)
                {
                    yield return excelRow;
                }
            }
        }

        private sealed class SheetReadContext
        {
            public SheetReadContext(List<string> sharedStrings, bool[]? dateStyles, bool date1904)
            {
                SharedStrings = sharedStrings;
                DateStyles = dateStyles;
                Date1904 = date1904;
            }

            public List<string> SharedStrings { get; }
            public bool[]? DateStyles { get; }
            public bool Date1904 { get; }
            public StringBuilder TextBuffer { get; } = new StringBuilder();
        }

        /// <summary>
        /// Reads all &lt;c&gt; children of the current &lt;row&gt;. Leaves the reader on the row's end element.
        /// </summary>
        private static ExcelRow ReadRowCells(XmlReader reader, int rowNum, int startColIdx, int endColIdx, SheetReadContext ctx, out bool hasAnyCell)
        {
            var excelRow = new ExcelRow { RowNumber = rowNum };
            hasAnyCell = false;

            int rowDepth = reader.Depth;
            int lastColIdx = 0;

            reader.Read();
            while (!reader.EOF && !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == rowDepth))
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                if (reader.LocalName != "c")
                {
                    reader.Skip();
                    continue;
                }

                // Cell "r" is optional; implicit cells follow the previous cell in the row.
                string? cellRef = reader.GetAttribute("r");
                int colIdx = cellRef != null && TryParseColumnIndex(cellRef, out int parsedCol) ? parsedCol : lastColIdx + 1;
                lastColIdx = colIdx;

                if (colIdx < startColIdx || colIdx > endColIdx)
                {
                    reader.Skip();
                    continue;
                }

                string? cellVal = ReadCellValue(reader, ctx);
                excelRow[colIdx] = cellVal;
                if (!string.IsNullOrEmpty(cellVal)) hasAnyCell = true;

                // ReadCellValue leaves the reader on </c> (or on an empty <c/>); advance past it.
                reader.Read();
            }

            return excelRow;
        }

        /// <summary>
        /// Resolves the display value of the current &lt;c&gt; element. Leaves the reader on &lt;/c&gt; or the empty &lt;c/&gt;.
        /// </summary>
        private static string? ReadCellValue(XmlReader reader, SheetReadContext ctx)
        {
            string? cellType = reader.GetAttribute("t");
            string? styleAttr = reader.GetAttribute("s");

            if (reader.IsEmptyElement) return null;

            string? rawValue = null;
            bool hasInline = false;
            var inline = ctx.TextBuffer;
            inline.Clear();

            int cellDepth = reader.Depth;
            reader.Read();
            while (!reader.EOF && !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == cellDepth))
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.LocalName)
                    {
                        case "v":
                            rawValue = reader.ReadElementContentAsString();
                            continue;
                        case "is":
                            hasInline = true;
                            if (reader.IsEmptyElement)
                            {
                                reader.Read();
                                continue;
                            }
                            XlsxPackageReader.ReadTextRuns(reader, inline);
                            reader.Read();
                            continue;
                        case "t":
                            // Non-standard: bare <t> directly under <c>.
                            hasInline = true;
                            inline.Append(reader.ReadElementContentAsString());
                            continue;
                        default:
                            // <f> formulas, <extLst>, etc.
                            reader.Skip();
                            continue;
                    }
                }
                reader.Read();
            }

            switch (cellType)
            {
                case "s":
                    if (rawValue != null && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sIdx) &&
                        sIdx >= 0 && sIdx < ctx.SharedStrings.Count)
                    {
                        return ctx.SharedStrings[sIdx];
                    }
                    return rawValue;

                case "b":
                    return rawValue == null ? null : (rawValue == "1" ? "TRUE" : "FALSE");

                case "inlineStr":
                    return hasInline ? inline.ToString() : rawValue;

                case null:
                case "n":
                    if (rawValue == null) return hasInline ? inline.ToString() : null;
                    if (ctx.DateStyles != null && TryParsePositiveOrZeroInt(styleAttr, out int styleIdx) &&
                        styleIdx < ctx.DateStyles.Length && ctx.DateStyles[styleIdx] &&
                        OADateFormatter.TryFormatSerial(rawValue, ctx.Date1904, out var iso))
                    {
                        return iso;
                    }
                    return rawValue;

                default:
                    // "str" (formula string), "e" (error), "d" (ISO 8601 date)
                    return rawValue ?? (hasInline ? inline.ToString() : null);
            }
        }

        private static void SkipToEndElement(XmlReader reader)
        {
            int depth = reader.Depth;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth) return;
            }
        }

        /// <summary>
        /// Extracts the 1-based column index from a cell reference ("BC12" -> 55) without allocating substrings.
        /// </summary>
        private static bool TryParseColumnIndex(string cellRef, out int columnIndex)
        {
            columnIndex = 0;
            int i = 0;
            for (; i < cellRef.Length; i++)
            {
                char ch = cellRef[i];
                if (ch >= 'A' && ch <= 'Z') columnIndex = columnIndex * 26 + (ch - 'A' + 1);
                else if (ch >= 'a' && ch <= 'z') columnIndex = columnIndex * 26 + (ch - 'a' + 1);
                else break;

                if (columnIndex > SpreadsheetXml.MaxColumns) return false;
            }

            return i > 0 && i < cellRef.Length && columnIndex > 0;
        }

        private static bool TryParsePositiveInt(string? text, out int value) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;

        private static bool TryParsePositiveOrZeroInt(string? text, out int value) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

        private static void EnsureFileExists(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException($"Excel file not found: {filePath}");
            }
        }

        #endregion

        #region Embedded Media APIs

        /// <summary>
        /// Extracts all embedded images from an Excel (.xlsx) file on disk.
        /// </summary>
        public static List<ExcelEmbeddedImage> ExtractImages(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return ExtractImages(stream);
        }

        /// <summary>
        /// Extracts all embedded images from an Excel (.xlsx) stream.
        /// </summary>
        public static List<ExcelEmbeddedImage> ExtractImages(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var images = new List<ExcelEmbeddedImage>();
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.StartsWith("xl/media/", StringComparison.OrdinalIgnoreCase))
                {
                    using var entryStream = entry.Open();
                    using var ms = new MemoryStream();
                    entryStream.CopyTo(ms);

                    string ext = Path.GetExtension(entry.Name).TrimStart('.').ToLowerInvariant();
                    images.Add(new ExcelEmbeddedImage
                    {
                        Name = Path.GetFileNameWithoutExtension(entry.Name),
                        Format = ext,
                        Data = ms.ToArray()
                    });
                }
            }

            return images;
        }

        /// <summary>
        /// Returns the names of all worksheets in workbook (tab) order.
        /// </summary>
        public static List<string> GetSheetNames(string filePath)
        {
            EnsureFileExists(filePath);
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return GetSheetNames(stream);
        }

        /// <summary>
        /// Returns the names of all worksheets in workbook (tab) order from a stream.
        /// </summary>
        public static List<string> GetSheetNames(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var package = XlsxPackageReader.Open(zip);
            var list = new List<string>(package.Sheets.Count);
            foreach (var s in package.Sheets)
            {
                list.Add(s.Name);
            }
            return list;
        }

        #endregion
    }
}
