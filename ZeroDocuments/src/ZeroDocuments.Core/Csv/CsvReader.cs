using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;

namespace ZeroDocuments.Csv
{
    /// <summary>
    /// Pure C# Zero-Dependency RFC 4180 compliant CSV Reader.
    /// Fast, low-allocation, supports streaming and DataTable conversion.
    /// </summary>
    public static class CsvReader
    {
        /// <summary>
        /// Reads CSV file from path into a DataTable.
        /// </summary>
        public static DataTable ReadToDataTable(string filePath, char delimiter = ',', bool hasHeader = true, Encoding? encoding = null)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException($"CSV file not found: {filePath}");

            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return ReadToDataTable(stream, delimiter, hasHeader, encoding);
        }

        /// <summary>
        /// Reads CSV stream into a DataTable.
        /// </summary>
        public static DataTable ReadToDataTable(Stream stream, char delimiter = ',', bool hasHeader = true, Encoding? encoding = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var table = new DataTable();
            using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8, true, 4096, leaveOpen: true);

            bool isFirst = true;
            foreach (var row in ReadRows(reader, delimiter))
            {
                // Skip completely empty or whitespace lines
                if (row.Count == 0 || (row.Count == 1 && string.IsNullOrWhiteSpace(row[0])))
                {
                    continue;
                }

                if (isFirst)
                {
                    isFirst = false;
                    if (hasHeader)
                    {
                        for (int i = 0; i < row.Count; i++)
                        {
                            string colName = string.IsNullOrWhiteSpace(row[i]) ? $"Column_{i + 1}" : row[i].Trim();
                            // Ensure unique column names in DataTable
                            string uniqueName = colName;
                            int suffix = 1;
                            while (table.Columns.Contains(uniqueName))
                            {
                                uniqueName = $"{colName}_{suffix++}";
                            }
                            table.Columns.Add(uniqueName, typeof(string));
                        }
                        continue;
                    }
                    else
                    {
                        for (int i = 0; i < row.Count; i++)
                        {
                            table.Columns.Add($"Column_{i + 1}", typeof(string));
                        }
                    }
                }

                while (table.Columns.Count < row.Count)
                {
                    string baseName = $"Column_{table.Columns.Count + 1}";
                    string uniqueName = baseName;
                    int suffix = 1;
                    while (table.Columns.Contains(uniqueName))
                    {
                        uniqueName = $"{baseName}_{suffix++}";
                    }
                    table.Columns.Add(uniqueName, typeof(string));
                }

                var rowData = new object?[table.Columns.Count];
                for (int i = 0; i < row.Count; i++)
                {
                    rowData[i] = row[i];
                }
                table.Rows.Add(rowData);
            }

            return table;
        }

        /// <summary>
        /// Streams parsed CSV records row by row from a TextReader.
        /// RFC 4180 compliant (handles quoted fields with delimiters, line breaks, and escaped double quotes).
        /// A double quote opens a quoted section only at the start of a field; elsewhere it is treated literally.
        /// </summary>
        public static IEnumerable<IReadOnlyList<string>> ReadRows(TextReader reader, char delimiter = ',')
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            return ReadRowsIterator(reader, delimiter);
        }

        private static IEnumerable<IReadOnlyList<string>> ReadRowsIterator(TextReader reader, char delimiter)
        {
            var buffer = new char[16 * 1024];
            var row = new List<string>();
            var field = new StringBuilder();

            bool inQuotes = false;
            bool quotePending = false;   // saw '"' inside quotes; next char decides escape vs. close
            bool skipLineFeed = false;   // previous char was '\r'
            bool fieldWasQuoted = false;
            bool isFirstChar = true;

            int length;
            while ((length = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < length; i++)
                {
                    char ch = buffer[i];

                    if (isFirstChar)
                    {
                        isFirstChar = false;
                        // Strip UTF-8 / Unicode BOM (U+FEFF) if present at stream start
                        if (ch == '\uFEFF') continue;
                    }

                    if (skipLineFeed)
                    {
                        skipLineFeed = false;
                        if (ch == '\n') continue;
                    }

                    if (quotePending)
                    {
                        quotePending = false;
                        if (ch == '"')
                        {
                            // Escaped quote: "" -> "
                            field.Append('"');
                            continue;
                        }
                        // Closing quote; process current char as unquoted content.
                        inQuotes = false;
                    }

                    if (inQuotes)
                    {
                        if (ch == '"') quotePending = true;
                        else field.Append(ch);
                        continue;
                    }

                    if (ch == '"' && field.Length == 0 && !fieldWasQuoted)
                    {
                        inQuotes = true;
                        fieldWasQuoted = true;
                    }
                    else if (ch == delimiter)
                    {
                        row.Add(field.ToString());
                        field.Clear();
                        fieldWasQuoted = false;
                    }
                    else if (ch == '\r' || ch == '\n')
                    {
                        row.Add(field.ToString());
                        field.Clear();
                        fieldWasQuoted = false;
                        skipLineFeed = ch == '\r';
                        yield return row.ToArray();
                        row.Clear();
                    }
                    else
                    {
                        field.Append(ch);
                    }
                }
            }

            // Flush last record if not empty (unterminated quotes at EOF are closed leniently)
            if (field.Length > 0 || row.Count > 0 || fieldWasQuoted)
            {
                row.Add(field.ToString());
                yield return row.ToArray();
            }
        }
    }
}
