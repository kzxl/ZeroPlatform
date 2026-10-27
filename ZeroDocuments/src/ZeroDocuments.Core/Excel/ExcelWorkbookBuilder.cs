using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using ZeroDocuments.Common;
using ZeroDocuments.Excel.Internal;
using ZeroDocuments.Excel.Models;

namespace ZeroDocuments.Excel
{
    /// <summary>
    /// Fluent multi-sheet OpenXML Excel (.xlsx) workbook builder.
    /// Pure C# BCL implementation with zero external dependencies.
    /// Supports rich formatting, embedded DrawingML images, and conditional formatting.
    /// </summary>
    public sealed class ExcelWorkbookBuilder : IDisposable
    {
        private readonly List<WorksheetDefinition> _sheets = new List<WorksheetDefinition>();

        /// <summary>
        /// Gets or sets the formula injection (CWE-1236) mitigation mode.
        /// Default is <see cref="FormulaInjectionMode.PrefixQuote"/>.
        /// </summary>
        public FormulaInjectionMode FormulaInjection { get; set; } = FormulaInjectionMode.PrefixQuote;

        /// <summary>
        /// Gets or sets whether formula injection protection (CWE-1236) is enabled.
        /// Default is true.
        /// </summary>
        public bool FormulaInjectionProtection
        {
            get => FormulaInjection != FormulaInjectionMode.Disabled;
            set => FormulaInjection = value ? FormulaInjectionMode.PrefixQuote : FormulaInjectionMode.Disabled;
        }

        private sealed class WorksheetDefinition
        {
            public string Name { get; set; } = string.Empty;
            public IReadOnlyList<string>? Headers { get; set; }
            public List<IReadOnlyList<object?>> Rows { get; } = new List<IReadOnlyList<object?>>();
            public bool HeaderBold { get; set; } = true;
            public string? AutoFilterRange { get; set; }
            public bool AutoFilterEnabled { get; set; }
            public List<ExcelImage> Images { get; } = new List<ExcelImage>();
            public List<ExcelConditionalFormatRule> ConditionalFormatting { get; } = new List<ExcelConditionalFormatRule>();

            /// <summary>
            /// True when the sheet was implicitly created by AddImage / AddHighlightRule / SetAutoFilter
            /// before any AddSheet call; a subsequent AddSheet with the same name populates it instead of duplicating.
            /// </summary>
            public bool IsPlaceholder { get; set; }

            public WorksheetSpec ToSpec() => new WorksheetSpec
            {
                Name = Name,
                Headers = Headers,
                Rows = Rows,
                HeaderBold = HeaderBold,
                AutoFilterRange = AutoFilterRange,
                AutoFilterEnabled = AutoFilterEnabled,
                Images = Images,
                ConditionalFormatting = ConditionalFormatting
            };
        }

        static ExcelWorkbookBuilder()
        {
            RuntimeAssemblyResolver.EnsureInitialized();
        }

        #region Fluent AddSheet APIs

        /// <summary>
        /// Adds a new worksheet populated from a System.Data.DataTable.
        /// Rows are snapshotted at call time.
        /// </summary>
        public ExcelWorkbookBuilder AddSheet(string sheetName, DataTable table, bool includeHeaders = true, bool headerBold = true)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var sheet = CreateOrClaimSheet(sheetName, includeHeaders ? TabularSource.GetHeaders(table) : null, headerBold);
            sheet.Rows.AddRange(TabularSource.FromDataTable(table));
            return this;
        }

        /// <summary>
        /// Adds a new worksheet populated from an IEnumerable of strongly-typed POCO objects.
        /// Items are snapshotted at call time.
        /// </summary>
        public ExcelWorkbookBuilder AddSheet<T>(string sheetName, IEnumerable<T> data, bool includeHeaders = true, bool headerBold = true)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var accessors = PropertyAccessorCache.GetAccessors(typeof(T));
            var sheet = CreateOrClaimSheet(sheetName, includeHeaders ? TabularSource.GetHeaders(accessors) : null, headerBold);
            sheet.Rows.AddRange(TabularSource.FromObjects(data, accessors));
            return this;
        }

        /// <summary>
        /// Adds a new worksheet populated from raw 2D grid rows.
        /// </summary>
        public ExcelWorkbookBuilder AddSheet(string sheetName, IEnumerable<IReadOnlyList<object?>> rows, IReadOnlyList<string>? headers = null, bool headerBold = true)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            var sheet = CreateOrClaimSheet(sheetName, headers, headerBold);
            sheet.Rows.AddRange(rows);
            return this;
        }

        private WorksheetDefinition CreateOrClaimSheet(string sheetName, IReadOnlyList<string>? headers, bool headerBold)
        {
            var placeholder = FindSheet(sheetName);
            if (placeholder != null && placeholder.IsPlaceholder)
            {
                placeholder.IsPlaceholder = false;
                placeholder.Headers = headers;
                placeholder.HeaderBold = headerBold;
                return placeholder;
            }

            var sheet = new WorksheetDefinition
            {
                Name = GetUniqueSheetName(sheetName),
                Headers = headers,
                HeaderBold = headerBold
            };
            _sheets.Add(sheet);
            return sheet;
        }

        #endregion

        #region Fluent Image & Formatting APIs

        /// <summary>
        /// Embeds an image into the specified worksheet using OpenXML DrawingML.
        /// </summary>
        /// <param name="sheetName">Target worksheet name.</param>
        /// <param name="imageData">Raw binary bytes of the image.</param>
        /// <param name="format">Format extension: "png", "jpg", "jpeg", "gif", "bmp", "tif", "tiff", "emf" or "wmf".</param>
        /// <param name="column">1-based column index where top-left corner is anchored (e.g., 1 for Col A).</param>
        /// <param name="row">1-based row index where top-left corner is anchored (e.g., 1 for Row 1).</param>
        /// <param name="widthPx">Display width in pixels.</param>
        /// <param name="heightPx">Display height in pixels.</param>
        /// <param name="name">Optional descriptive name for the image element.</param>
        public ExcelWorkbookBuilder AddImage(string sheetName, byte[] imageData, string format, int column, int row, int widthPx, int heightPx, string? name = null)
        {
            var image = new ExcelImage(imageData, format, column, row, widthPx, heightPx, name);
            GetOrAddSheet(sheetName).Images.Add(image);
            return this;
        }

        /// <summary>
        /// Embeds a configured <see cref="ExcelImage"/> into the specified worksheet.
        /// </summary>
        public ExcelWorkbookBuilder AddImage(string sheetName, ExcelImage image)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            GetOrAddSheet(sheetName).Images.Add(image);
            return this;
        }

        /// <summary>
        /// Adds a conditional highlight rule (e.g. GreaterThan, LessThan, Equal, Between) to a cell range.
        /// Text comparisons must be quoted, e.g. <c>"\"Closed\""</c>.
        /// </summary>
        public ExcelWorkbookBuilder AddHighlightRule(string sheetName, string range, CellRuleOperator op, string value1, string? value2 = null, string? fillColorHex = null, string? fontColorHex = null, bool bold = false)
        {
            if (string.IsNullOrWhiteSpace(range))
                throw new ArgumentException("Range cannot be null or empty.", nameof(range));
            if (string.IsNullOrWhiteSpace(value1))
                throw new ArgumentException("A comparison value is required.", nameof(value1));
            if ((op == CellRuleOperator.Between || op == CellRuleOperator.NotBetween) && string.IsNullOrWhiteSpace(value2))
                throw new ArgumentException($"Operator {op} requires a second comparison value.", nameof(value2));

            // Validate colors eagerly so errors surface at the call site rather than at Save().
            if (!string.IsNullOrEmpty(fillColorHex)) SpreadsheetXml.NormalizeColor(fillColorHex);
            if (!string.IsNullOrEmpty(fontColorHex)) SpreadsheetXml.NormalizeColor(fontColorHex);

            GetOrAddSheet(sheetName).ConditionalFormatting.Add(new ExcelHighlightRule(range, op, value1, value2, fillColorHex, fontColorHex, bold));
            return this;
        }

        /// <summary>
        /// Adds a 2-color or 3-color gradient heatmap conditional formatting rule to a cell range.
        /// </summary>
        public ExcelWorkbookBuilder AddColorScale(string sheetName, string range, string minColorHex = "FFF8696B", string maxColorHex = "FF63BE7B", string? midColorHex = null)
        {
            SpreadsheetXml.NormalizeColor(minColorHex);
            SpreadsheetXml.NormalizeColor(maxColorHex);
            if (!string.IsNullOrEmpty(midColorHex)) SpreadsheetXml.NormalizeColor(midColorHex);

            GetOrAddSheet(sheetName).ConditionalFormatting.Add(new ExcelColorScaleRule(range, minColorHex, maxColorHex, midColorHex));
            return this;
        }

        /// <summary>
        /// Adds an in-cell data bar conditional formatting rule to a cell range.
        /// </summary>
        public ExcelWorkbookBuilder AddDataBar(string sheetName, string range, string colorHex = "FF638EC6")
        {
            SpreadsheetXml.NormalizeColor(colorHex);
            GetOrAddSheet(sheetName).ConditionalFormatting.Add(new ExcelDataBarRule(range, colorHex));
            return this;
        }

        /// <summary>
        /// Enables Excel AutoFilter on the specified worksheet for a specific range or automatically over the populated table headers.
        /// </summary>
        public ExcelWorkbookBuilder SetAutoFilter(string sheetName, string? range = null)
        {
            var sheet = GetOrAddSheet(sheetName);
            if (!string.IsNullOrEmpty(range))
            {
                sheet.AutoFilterRange = range;
            }
            else
            {
                sheet.AutoFilterEnabled = true;
            }
            return this;
        }

        private WorksheetDefinition GetOrAddSheet(string sheetName)
        {
            var existing = FindSheet(sheetName);
            if (existing != null) return existing;

            var newSheet = new WorksheetDefinition
            {
                Name = GetUniqueSheetName(sheetName),
                IsPlaceholder = true
            };
            _sheets.Add(newSheet);
            return newSheet;
        }

        private WorksheetDefinition? FindSheet(string sheetName)
        {
            string raw = string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : sheetName;
            string sanitized = SpreadsheetXml.SanitizeSheetName(raw, "Sheet");

            foreach (var s in _sheets)
            {
                if (string.Equals(s.Name, raw, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.Name, sanitized, StringComparison.OrdinalIgnoreCase))
                {
                    return s;
                }
            }
            return null;
        }

        #endregion

        #region Save & Export APIs

        /// <summary>
        /// Saves the workbook to an Excel (.xlsx) file on disk atomically.
        /// </summary>
        public void Save(string filePath)
        {
            AtomicFileWriter.Write(filePath, Save);
        }

        /// <summary>
        /// Saves the workbook to a destination stream in OpenXML Excel (.xlsx) format.
        /// </summary>
        public void Save(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            if (_sheets.Count == 0)
            {
                // Ensure at least 1 worksheet exists
                AddSheet("Sheet1", Array.Empty<IReadOnlyList<object?>>());
            }

            var specs = new WorksheetSpec[_sheets.Count];
            for (int i = 0; i < specs.Length; i++)
            {
                specs[i] = _sheets[i].ToSpec();
            }

            XlsxPackageWriter.Write(stream, specs, FormulaInjection);
        }

        /// <summary>
        /// Returns the entire workbook package as a byte array.
        /// </summary>
        public byte[] ToArray()
        {
            using var ms = new MemoryStream();
            Save(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Releases buffered worksheet data.
        /// </summary>
        public void Dispose()
        {
            _sheets.Clear();
        }

        #endregion

        #region Sheet Naming

        private string GetUniqueSheetName(string rawName)
        {
            string baseName = SpreadsheetXml.SanitizeSheetName(rawName, "Sheet");
            string uniqueName = baseName;
            int counter = 1;

            while (ContainsSheetName(uniqueName))
            {
                string suffix = "_" + counter++;
                uniqueName = baseName.Length + suffix.Length > 31
                    ? SpreadsheetXml.TruncateSafe(baseName, 31 - suffix.Length) + suffix
                    : baseName + suffix;
            }

            return uniqueName;
        }

        private bool ContainsSheetName(string name)
        {
            foreach (var s in _sheets)
            {
                if (string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        #endregion
    }
}
