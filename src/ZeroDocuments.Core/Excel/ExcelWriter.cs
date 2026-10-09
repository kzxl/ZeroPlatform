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
    /// Pure C# Zero-Dependency OpenXML Excel (.xlsx) Writer.
    /// Operates without external DLLs (No EPPlus, ClosedXML, or DocumentFormat.OpenXml required).
    /// Rows are streamed directly into the package: sources are enumerated exactly once and never buffered.
    /// </summary>
    public static class ExcelWriter
    {
        /// <summary>
        /// Gets or sets whether formula injection protection (CWE-1236) is enabled.
        /// When true, strings beginning with =, +, -, @, \t, or \r are prefixed with a single quote.
        /// Default is true.
        /// </summary>
        /// <remarks>
        /// This is process-wide fallback state. Prefer passing <see cref="ExcelWriterOptions"/>
        /// or configuring <see cref="ExcelWorkbookBuilder.FormulaInjection"/> per-call.
        /// </remarks>
        public static bool FormulaInjectionProtection { get; set; } = true;

        static ExcelWriter()
        {
            RuntimeAssemblyResolver.EnsureInitialized();
        }

        #region Public Write APIs

        /// <summary>
        /// Writes a DataTable to an Excel (.xlsx) file on disk atomically.
        /// </summary>
        public static void WriteToFile(string filePath, DataTable table, string sheetName = "Sheet1", bool includeHeaders = true) =>
            WriteToFile(filePath, table, null, sheetName, includeHeaders);

        /// <summary>
        /// Writes a DataTable to an Excel (.xlsx) file on disk atomically with custom options.
        /// </summary>
        public static void WriteToFile(string filePath, DataTable table, ExcelWriterOptions? options, string sheetName = "Sheet1", bool includeHeaders = true)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            AtomicFileWriter.Write(filePath, stream => WriteToStream(stream, table, options, sheetName, includeHeaders));
        }

        /// <summary>
        /// Writes a DataTable to a stream in OpenXML Excel (.xlsx) format.
        /// </summary>
        public static void WriteToStream(Stream stream, DataTable table, string sheetName = "Sheet1", bool includeHeaders = true) =>
            WriteToStream(stream, table, null, sheetName, includeHeaders);

        /// <summary>
        /// Writes a DataTable to a stream in OpenXML Excel (.xlsx) format with custom options.
        /// </summary>
        public static void WriteToStream(Stream stream, DataTable table, ExcelWriterOptions? options, string sheetName = "Sheet1", bool includeHeaders = true)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (table == null) throw new ArgumentNullException(nameof(table));

            WriteRowsToStream(stream, TabularSource.FromDataTable(table), options, includeHeaders ? TabularSource.GetHeaders(table) : null, sheetName);
        }

        /// <summary>
        /// Writes an IDataReader directly to an Excel (.xlsx) file on disk atomically.
        /// Streams records forward-only without buffering the dataset in memory.
        /// </summary>
        public static void WriteToFile(string filePath, IDataReader reader, string sheetName = "Sheet1", bool includeHeaders = true) =>
            WriteToFile(filePath, reader, null, sheetName, includeHeaders);

        /// <summary>
        /// Writes an IDataReader directly to an Excel (.xlsx) file on disk atomically with custom options.
        /// </summary>
        public static void WriteToFile(string filePath, IDataReader reader, ExcelWriterOptions? options, string sheetName = "Sheet1", bool includeHeaders = true)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            AtomicFileWriter.Write(filePath, stream => WriteToStream(stream, reader, options, sheetName, includeHeaders));
        }

        /// <summary>
        /// Writes an IDataReader directly to a stream in OpenXML Excel (.xlsx) format.
        /// </summary>
        public static void WriteToStream(Stream stream, IDataReader reader, string sheetName = "Sheet1", bool includeHeaders = true) =>
            WriteToStream(stream, reader, null, sheetName, includeHeaders);

        /// <summary>
        /// Writes an IDataReader directly to a stream in OpenXML Excel (.xlsx) format with custom options.
        /// </summary>
        public static void WriteToStream(Stream stream, IDataReader reader, ExcelWriterOptions? options, string sheetName = "Sheet1", bool includeHeaders = true)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            WriteRowsToStream(stream, TabularSource.FromDataReader(reader), options, includeHeaders ? TabularSource.GetHeaders(reader) : null, sheetName);
        }

        /// <summary>
        /// Writes a collection of objects to an Excel (.xlsx) file on disk atomically.
        /// Public properties are mapped to columns.
        /// </summary>
        public static void WriteToFile<T>(string filePath, IEnumerable<T> data, string sheetName = "Sheet1", bool includeHeaders = true) =>
            WriteToFile(filePath, data, null, sheetName, includeHeaders);

        /// <summary>
        /// Writes a collection of objects to an Excel (.xlsx) file on disk atomically with custom options.
        /// </summary>
        public static void WriteToFile<T>(string filePath, IEnumerable<T> data, ExcelWriterOptions? options, string sheetName = "Sheet1", bool includeHeaders = true)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            AtomicFileWriter.Write(filePath, stream => WriteToStream(stream, data, options, sheetName, includeHeaders));
        }

        /// <summary>
        /// Writes a collection of objects to a stream in OpenXML Excel (.xlsx) format.
        /// </summary>
        public static void WriteToStream<T>(Stream stream, IEnumerable<T> data, string sheetName = "Sheet1", bool includeHeaders = true) =>
            WriteToStream(stream, data, null, sheetName, includeHeaders);

        /// <summary>
        /// Writes a collection of objects to a stream in OpenXML Excel (.xlsx) format with custom options.
        /// </summary>
        public static void WriteToStream<T>(Stream stream, IEnumerable<T> data, ExcelWriterOptions? options, string sheetName = "Sheet1", bool includeHeaders = true)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (data == null) throw new ArgumentNullException(nameof(data));

            var accessors = PropertyAccessorCache.GetAccessors(typeof(T));
            WriteRowsToStream(stream, TabularSource.FromObjects(data, accessors), options, includeHeaders ? TabularSource.GetHeaders(accessors) : null, sheetName);
        }

        /// <summary>
        /// Writes raw 2D grid rows to an Excel (.xlsx) file on disk atomically.
        /// </summary>
        public static void WriteToFile(string filePath, IEnumerable<IReadOnlyList<object?>> rows, IReadOnlyList<string>? headers = null, string sheetName = "Sheet1") =>
            WriteToFile(filePath, rows, null, headers, sheetName);

        /// <summary>
        /// Writes raw 2D grid rows to an Excel (.xlsx) file on disk atomically with custom options.
        /// </summary>
        public static void WriteToFile(string filePath, IEnumerable<IReadOnlyList<object?>> rows, ExcelWriterOptions? options, IReadOnlyList<string>? headers = null, string sheetName = "Sheet1")
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            AtomicFileWriter.Write(filePath, stream => WriteRowsToStream(stream, rows, options, headers, sheetName));
        }

        /// <summary>
        /// Writes raw 2D grid rows to a stream in OpenXML Excel (.xlsx) format.
        /// </summary>
        public static void WriteRowsToStream(Stream stream, IEnumerable<IReadOnlyList<object?>> rows, IReadOnlyList<string>? headers = null, string sheetName = "Sheet1") =>
            WriteRowsToStream(stream, rows, null, headers, sheetName);

        /// <summary>
        /// Writes raw 2D grid rows to a stream in OpenXML Excel (.xlsx) format with custom options.
        /// </summary>
        public static void WriteRowsToStream(Stream stream, IEnumerable<IReadOnlyList<object?>> rows, ExcelWriterOptions? options, IReadOnlyList<string>? headers = null, string sheetName = "Sheet1")
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            var sheet = new WorksheetSpec
            {
                Name = SpreadsheetXml.SanitizeSheetName(sheetName, "Sheet1"),
                Headers = headers,
                Rows = rows,
                HeaderBold = false
            };

            var mode = options?.FormulaInjection ?? (FormulaInjectionProtection ? FormulaInjectionMode.PrefixQuote : FormulaInjectionMode.Disabled);
            XlsxPackageWriter.Write(stream, new[] { sheet }, mode);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Sanitizes text to prevent formula injection attacks (CWE-1236).
        /// Prefixes a single quote if the first character is =, +, -, @, \t, or \r.
        /// </summary>
        public static string SanitizeFormulaInjection(string? text) => FormulaInjectionGuard.Sanitize(text);

        internal static FileStream CreateFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        }

        #endregion
    }
}
