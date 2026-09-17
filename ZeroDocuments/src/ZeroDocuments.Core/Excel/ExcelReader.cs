using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using ZeroDocuments.Common;
using ZeroDocuments.Excel.Models;

namespace ZeroDocuments.Excel
{
    /// <summary>
    /// Pure C# Zero-Dependency OpenXML Excel (.xlsx) Reader.
    /// Operates without external DLLs (No EPPlus, ClosedXML, or DevExpress required).
    /// </summary>
    public static class ExcelReader
    {
        private static readonly XNamespace NsMain = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace NsRels = "http://schemas.openxmlformats.org/package/2006/relationships";

        static ExcelReader()
        {
            RuntimeAssemblyResolver.EnsureInitialized();
        }

        /// <summary>
        /// Reads Excel sheet into a DataTable bounded by header range (e.g. "D24:T24" or "A1:C1").
        /// Automatically expands rows downward until data ends.
        /// </summary>
        public static DataTable ReadByHeaderRange(string filePath, string headerRange, int maxRows = 5000, string? sheetName = null)
        {
            string fullDataRange = ExcelCellAddress.ConvertHeaderRangeToDataRange(headerRange, maxRows);
            return ReadToDataTable(filePath, fullDataRange, sheetName);
        }

        /// <summary>
        /// Reads Excel file from file path into a DataTable.
        /// </summary>
        public static DataTable ReadToDataTable(string filePath, string cellRange = ExcelCellAddress.DefaultRange, string? sheetName = null)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException($"Excel file not found: {filePath}");
            }

            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return ReadToDataTable(stream, cellRange, sheetName);
        }

        /// <summary>
        /// Reads Excel stream into a DataTable.
        /// </summary>
        public static DataTable ReadToDataTable(Stream stream, string cellRange = ExcelCellAddress.DefaultRange, string? sheetName = null)
        {
            var table = new DataTable();
            ExcelCellAddress.ParseCellRange(cellRange, out var startCol, out var startRow, out var endCol, out var endRow);
            int startColIdx = ExcelCellAddress.ColumnNameToIndex(startCol);
            int endColIdx = ExcelCellAddress.ColumnNameToIndex(endCol);

            for (int c = startColIdx; c <= endColIdx; c++)
            {
                table.Columns.Add("Column_" + ExcelCellAddress.IndexToColumnName(c), typeof(string));
            }

            var rows = ReadRows(stream, cellRange, sheetName);
            foreach (var row in rows)
            {
                var rowVals = new object?[endColIdx - startColIdx + 1];
                bool hasData = false;

                for (int c = startColIdx; c <= endColIdx; c++)
                {
                    var val = row[c];
                    if (!string.IsNullOrEmpty(val))
                    {
                        hasData = true;
                    }
                    rowVals[c - startColIdx] = val;
                }

                if (hasData)
                {
                    table.Rows.Add(rowVals);
                }
            }

            return table;
        }

        /// <summary>
        /// Reads Excel rows as an enumerable sequence of ExcelRow objects.
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
            var result = new List<ExcelRow>();
            ExcelCellAddress.ParseCellRange(cellRange, out var startCol, out var startRow, out var endCol, out var endRow);
            int startColIdx = ExcelCellAddress.ColumnNameToIndex(startCol);
            int endColIdx = ExcelCellAddress.ColumnNameToIndex(endCol);

            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

            // 1. Load shared strings table if present
            var sharedStrings = LoadSharedStrings(zip);

            // 2. Locate target worksheet entry
            var sheetEntry = FindSheetEntry(zip, sheetName);
            if (sheetEntry == null) return result;

            using var sheetStream = sheetEntry.Open();
            var xdoc = XDocument.Load(sheetStream);

            var rowElements = xdoc.Descendants(NsMain + "row")
                .Select(r => new
                {
                    RowNum = int.TryParse((string?)r.Attribute("r"), out int rNum) ? rNum : 0,
                    Element = r
                })
                .Where(r => r.RowNum >= startRow && r.RowNum <= endRow)
                .OrderBy(r => r.RowNum);

            foreach (var r in rowElements)
            {
                var excelRow = new ExcelRow { RowNumber = r.RowNum };
                bool hasAnyCell = false;

                foreach (var c in r.Element.Elements(NsMain + "c"))
                {
                    string? cellRef = (string?)c.Attribute("r");
                    if (string.IsNullOrEmpty(cellRef)) continue;

                    if (!ExcelCellAddress.TryParseCellReference(cellRef!, out var colName, out _))
                        continue;

                    int colIdx = ExcelCellAddress.ColumnNameToIndex(colName);
                    if (colIdx < startColIdx || colIdx > endColIdx) continue;

                    string? val = ExtractCellValue(c, sharedStrings);
                    excelRow[colIdx] = val;
                    if (!string.IsNullOrEmpty(val))
                    {
                        hasAnyCell = true;
                    }
                }

                if (hasAnyCell)
                {
                    result.Add(excelRow);
                }
            }

            return result;
        }

        private static List<string> LoadSharedStrings(ZipArchive zip)
        {
            var list = new List<string>();
            var ssEntry = zip.Entries.FirstOrDefault(e => e.FullName.Equals("xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase));
            if (ssEntry == null) return list;

            using var stream = ssEntry.Open();
            var xdoc = XDocument.Load(stream);

            foreach (var si in xdoc.Descendants(NsMain + "si"))
            {
                string text = string.Concat(si.Descendants(NsMain + "t").Select(t => t.Value));
                list.Add(text);
            }
            return list;
        }

        private static ZipArchiveEntry? FindSheetEntry(ZipArchive zip, string? sheetName)
        {
            if (!string.IsNullOrEmpty(sheetName))
            {
                // Try to resolve sheet name via workbook.xml
                var wbEntry = zip.Entries.FirstOrDefault(e => e.FullName.Equals("xl/workbook.xml", StringComparison.OrdinalIgnoreCase));
                if (wbEntry != null)
                {
                    using var wbStream = wbEntry.Open();
                    var wbDoc = XDocument.Load(wbStream);
                    var sheetElem = wbDoc.Descendants(NsMain + "sheet")
                        .FirstOrDefault(s => string.Equals((string?)s.Attribute("name"), sheetName, StringComparison.OrdinalIgnoreCase));

                    if (sheetElem != null)
                    {
                        string? sheetId = (string?)sheetElem.Attribute("sheetId");
                        if (!string.IsNullOrEmpty(sheetId))
                        {
                            var entry = zip.Entries.FirstOrDefault(e => e.FullName.Equals($"xl/worksheets/sheet{sheetId}.xml", StringComparison.OrdinalIgnoreCase));
                            if (entry != null) return entry;
                        }
                    }
                }
            }

            // Fallback: first sheet in xl/worksheets/
            return zip.Entries.FirstOrDefault(e => e.FullName.Equals("xl/worksheets/sheet1.xml", StringComparison.OrdinalIgnoreCase))
                   ?? zip.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml"));
        }

        private static string? ExtractCellValue(XElement c, List<string> sharedStrings)
        {
            string? type = (string?)c.Attribute("t");
            string? val = c.Element(NsMain + "v")?.Value;

            if (type == "s") // Shared string lookup
            {
                if (int.TryParse(val, out int sIdx) && sIdx >= 0 && sIdx < sharedStrings.Count)
                {
                    return sharedStrings[sIdx];
                }
            }
            else if (type == "inlineStr") // Inline string
            {
                var isElem = c.Element(NsMain + "is");
                if (isElem != null)
                {
                    return string.Concat(isElem.Descendants(NsMain + "t").Select(t => t.Value));
                }
            }
            else if (type == "b") // Boolean
            {
                return val == "1" ? "TRUE" : "FALSE";
            }

            return val;
        }
    }
}
