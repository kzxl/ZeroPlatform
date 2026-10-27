using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using ZeroDocuments.Common;

namespace ZeroDocuments.Csv
{
    /// <summary>
    /// Pure C# Zero-Dependency RFC 4180 compliant CSV Writer.
    /// Supports streaming, proper character escaping, and DataTable/Collection exports.
    /// Rows are written as they are enumerated; sources are never buffered in memory.
    /// </summary>
    public static class CsvWriter
    {
        /// <summary>
        /// Gets or sets whether formula injection protection (CWE-1236) is enabled.
        /// When true, text fields beginning with =, +, -, @, \t, or \r are prefixed with a single quote.
        /// Typed numeric, boolean, date and time values are never prefixed (e.g. -5 stays "-5").
        /// Default is true.
        /// </summary>
        /// <remarks>
        /// This is process-wide fallback state. Prefer passing <see cref="CsvWriterOptions"/> per-call.
        /// </remarks>
        public static bool FormulaInjectionProtection { get; set; } = true;

        #region Public Write APIs

        /// <summary>
        /// Writes a DataTable to a CSV file atomically.
        /// </summary>
        public static void WriteToFile(string filePath, DataTable table, char delimiter = ',', bool includeHeaders = true, Encoding? encoding = null)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            AtomicFileWriter.Write(filePath, stream => WriteToStream(stream, table, delimiter, includeHeaders, encoding));
        }

        /// <summary>
        /// Writes a DataTable to a CSV file atomically with custom options.
        /// </summary>
        public static void WriteToFile(string filePath, DataTable table, CsvWriterOptions? options)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            AtomicFileWriter.Write(filePath, stream => WriteToStream(stream, table, options));
        }

        /// <summary>
        /// Writes a DataTable to a stream in CSV format.
        /// </summary>
        public static void WriteToStream(Stream stream, DataTable table, char delimiter = ',', bool includeHeaders = true, Encoding? encoding = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (table == null) throw new ArgumentNullException(nameof(table));

            WriteRowsToStream(stream, TabularSource.FromDataTable(table), includeHeaders ? TabularSource.GetHeaders(table) : null, delimiter, encoding);
        }

        /// <summary>
        /// Writes a DataTable to a stream in CSV format with custom options.
        /// </summary>
        public static void WriteToStream(Stream stream, DataTable table, CsvWriterOptions? options)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (table == null) throw new ArgumentNullException(nameof(table));

            var opt = options ?? new CsvWriterOptions();
            WriteRowsToStream(stream, TabularSource.FromDataTable(table), opt, opt.IncludeHeaders ? TabularSource.GetHeaders(table) : null);
        }

        /// <summary>
        /// Writes a collection of objects to a CSV file atomically.
        /// </summary>
        public static void WriteToFile<T>(string filePath, IEnumerable<T> data, char delimiter = ',', bool includeHeaders = true, Encoding? encoding = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            AtomicFileWriter.Write(filePath, stream => WriteToStream(stream, data, delimiter, includeHeaders, encoding));
        }

        /// <summary>
        /// Writes a collection of objects to a CSV file atomically with custom options.
        /// </summary>
        public static void WriteToFile<T>(string filePath, IEnumerable<T> data, CsvWriterOptions? options)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            AtomicFileWriter.Write(filePath, stream => WriteToStream(stream, data, options));
        }

        /// <summary>
        /// Writes a collection of objects to a stream in CSV format.
        /// </summary>
        public static void WriteToStream<T>(Stream stream, IEnumerable<T> data, char delimiter = ',', bool includeHeaders = true, Encoding? encoding = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (data == null) throw new ArgumentNullException(nameof(data));

            var accessors = PropertyAccessorCache.GetAccessors(typeof(T));
            WriteRowsToStream(stream, TabularSource.FromObjects(data, accessors), includeHeaders ? TabularSource.GetHeaders(accessors) : null, delimiter, encoding);
        }

        /// <summary>
        /// Writes a collection of objects to a stream in CSV format with custom options.
        /// </summary>
        public static void WriteToStream<T>(Stream stream, IEnumerable<T> data, CsvWriterOptions? options)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (data == null) throw new ArgumentNullException(nameof(data));

            var opt = options ?? new CsvWriterOptions();
            var accessors = PropertyAccessorCache.GetAccessors(typeof(T));
            WriteRowsToStream(stream, TabularSource.FromObjects(data, accessors), opt, opt.IncludeHeaders ? TabularSource.GetHeaders(accessors) : null);
        }

        /// <summary>
        /// Writes raw 2D grid rows to a CSV file atomically with custom options.
        /// </summary>
        public static void WriteToFile(string filePath, IEnumerable<IReadOnlyList<object?>> rows, CsvWriterOptions? options, IReadOnlyList<string>? headers = null)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            AtomicFileWriter.Write(filePath, stream => WriteRowsToStream(stream, rows, options, headers));
        }

        /// <summary>
        /// Writes raw 2D grid rows to a stream in CSV format.
        /// </summary>
        public static void WriteRowsToStream(Stream stream, IEnumerable<IReadOnlyList<object?>> rows, IReadOnlyList<string>? headers = null, char delimiter = ',', Encoding? encoding = null)
        {
            bool guard = FormulaInjectionProtection;
            WriteRowsInternal(stream, rows, headers, delimiter, encoding, guard);
        }

        /// <summary>
        /// Writes raw 2D grid rows to a stream in CSV format with custom options.
        /// </summary>
        public static void WriteRowsToStream(Stream stream, IEnumerable<IReadOnlyList<object?>> rows, CsvWriterOptions? options, IReadOnlyList<string>? headers = null)
        {
            var opt = options ?? new CsvWriterOptions();
            bool guard = opt.FormulaInjection != FormulaInjectionMode.Disabled;
            WriteRowsInternal(stream, rows, headers, opt.Delimiter, opt.Encoding, guard);
        }

        private static void WriteRowsInternal(Stream stream, IEnumerable<IReadOnlyList<object?>> rows, IReadOnlyList<string>? headers, char delimiter, Encoding? encoding, bool guard)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (delimiter == '"' || delimiter == '\r' || delimiter == '\n')
                throw new ArgumentException("Delimiter cannot be a double quote, CR or LF.", nameof(delimiter));

            using var writer = new StreamWriter(stream, encoding ?? new UTF8Encoding(true), 4096, leaveOpen: true);

            // 1. Header
            if (headers != null && headers.Count > 0)
            {
                for (int i = 0; i < headers.Count; i++)
                {
                    if (i > 0) writer.Write(delimiter);
                    writer.Write(EscapeCsvField(headers[i], delimiter, guard));
                }
                writer.WriteLine();
            }

            // 2. Rows
            foreach (var row in rows)
            {
                if (row == null)
                {
                    writer.WriteLine();
                    continue;
                }

                for (int i = 0; i < row.Count; i++)
                {
                    if (i > 0) writer.Write(delimiter);
                    writer.Write(FormatValue(row[i], delimiter, guard));
                }
                writer.WriteLine();
            }

            writer.Flush();
        }

        #endregion

        #region Formatting & Escaping

        private static string FormatValue(object? val, char delimiter, bool guard)
        {
            switch (val)
            {
                case null:
                    return string.Empty;
                case DBNull _:
                    return string.Empty;
                case string s:
                    return EscapeCsvField(s, delimiter, guard);
                case DateTime dt:
                    // InvariantCulture: ':' in custom formats is the culture's time separator otherwise.
                    return EscapeCsvField(dt.TimeOfDay == TimeSpan.Zero
                        ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), delimiter, sanitize: false);
                case sbyte or byte or short or ushort or int or uint or long or ulong:
                    return EscapeCsvField(Convert.ToString(val, CultureInfo.InvariantCulture), delimiter, sanitize: false);
                case float f:
                    return EscapeCsvField(f.ToString("R", CultureInfo.InvariantCulture), delimiter, sanitize: false);
                case double d:
                    return EscapeCsvField(d.ToString("R", CultureInfo.InvariantCulture), delimiter, sanitize: false);
                case decimal dec:
                    return EscapeCsvField(dec.ToString(CultureInfo.InvariantCulture), delimiter, sanitize: false);
                case bool b:
                    return EscapeCsvField(b ? "true" : "false", delimiter, sanitize: false);
                case TimeSpan ts:
                    return EscapeCsvField(ts.ToString("c", CultureInfo.InvariantCulture), delimiter, sanitize: false);
                case DateTimeOffset dto:
                    return EscapeCsvField(dto.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture), delimiter, sanitize: false);
                case IFormattable formattable:
                    return EscapeCsvField(formattable.ToString(null, CultureInfo.InvariantCulture), delimiter, guard);
                default:
                    return EscapeCsvField(val.ToString(), delimiter, guard);
            }
        }

        private static string EscapeCsvField(string? field, char delimiter, bool sanitize)
        {
            if (string.IsNullOrEmpty(field)) return string.Empty;

            string processed = sanitize ? FormulaInjectionGuard.Sanitize(field) : field!;

            bool mustQuote = processed.IndexOf(delimiter) >= 0 ||
                             processed.IndexOf('"') >= 0 ||
                             processed.IndexOf('\n') >= 0 ||
                             processed.IndexOf('\r') >= 0;

            if (!mustQuote) return processed;

            // Double up internal quotes: " -> ""
            return "\"" + processed.Replace("\"", "\"\"") + "\"";
        }

        #endregion
    }
}
