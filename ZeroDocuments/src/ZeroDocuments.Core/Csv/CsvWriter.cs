using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;

namespace ZeroDocuments.Csv
{
    /// <summary>
    /// Pure C# Zero-Dependency RFC 4180 compliant CSV Writer.
    /// Supports streaming, proper character escaping, and DataTable/Collection exports.
    /// </summary>
    public static class CsvWriter
    {
        /// <summary>
        /// Writes a DataTable to a CSV file.
        /// </summary>
        public static void WriteToFile(string filePath, DataTable table, char delimiter = ',', bool includeHeaders = true, Encoding? encoding = null)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            WriteToStream(stream, table, delimiter, includeHeaders, encoding);
        }

        /// <summary>
        /// Writes a DataTable to a stream in CSV format.
        /// </summary>
        public static void WriteToStream(Stream stream, DataTable table, char delimiter = ',', bool includeHeaders = true, Encoding? encoding = null)
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

            WriteRowsToStream(stream, rows, includeHeaders ? headers : null, delimiter, encoding);
        }

        /// <summary>
        /// Writes a collection of objects to a CSV file.
        /// </summary>
        public static void WriteToFile<T>(string filePath, IEnumerable<T> data, char delimiter = ',', bool includeHeaders = true, Encoding? encoding = null)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            WriteToStream(stream, data, delimiter, includeHeaders, encoding);
        }

        /// <summary>
        /// Writes a collection of objects to a stream in CSV format.
        /// </summary>
        public static void WriteToStream<T>(Stream stream, IEnumerable<T> data, char delimiter = ',', bool includeHeaders = true, Encoding? encoding = null)
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

            WriteRowsToStream(stream, rows, includeHeaders ? headers : null, delimiter, encoding);
        }

        /// <summary>
        /// Writes raw 2D grid rows to a stream in CSV format.
        /// </summary>
        public static void WriteRowsToStream(Stream stream, IEnumerable<IReadOnlyList<object?>> rows, IReadOnlyList<string>? headers = null, char delimiter = ',', Encoding? encoding = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            using var writer = new StreamWriter(stream, encoding ?? new UTF8Encoding(true), 4096, leaveOpen: true);

            // 1. Write Header
            if (headers != null && headers.Count > 0)
            {
                for (int i = 0; i < headers.Count; i++)
                {
                    if (i > 0) writer.Write(delimiter);
                    writer.Write(EscapeCsvField(headers[i], delimiter));
                }
                writer.WriteLine();
            }

            // 2. Write Rows
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
                    var val = row[i];
                    string text = val switch
                    {
                        null => string.Empty,
                        DateTime dt => dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("yyyy-MM-dd") : dt.ToString("yyyy-MM-dd HH:mm:ss"),
                        _ => val.ToString() ?? string.Empty
                    };
                    writer.Write(EscapeCsvField(text, delimiter));
                }
                writer.WriteLine();
            }

            writer.Flush();
        }

        private static string EscapeCsvField(string? field, char delimiter)
        {
            if (string.IsNullOrEmpty(field)) return string.Empty;

            bool mustQuote = field!.IndexOf(delimiter) >= 0 ||
                             field.IndexOf('"') >= 0 ||
                             field.IndexOf('\n') >= 0 ||
                             field.IndexOf('\r') >= 0;

            if (!mustQuote) return field;

            // Double up internal quotes: " -> ""
            string escaped = field.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }
    }
}
