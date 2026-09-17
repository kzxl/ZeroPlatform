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
                    table.Columns.Add($"Column_{table.Columns.Count + 1}", typeof(string));
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
        /// </summary>
        public static IEnumerable<IReadOnlyList<string>> ReadRows(TextReader reader, char delimiter = ',')
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            var row = new List<string>();
            var fieldBuilder = new StringBuilder();
            bool inQuotes = false;
            int chInt;

            while ((chInt = reader.Read()) != -1)
            {
                char ch = (char)chInt;

                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        int next = reader.Peek();
                        if (next == '"')
                        {
                            // Escaped quote: "" -> "
                            reader.Read();
                            fieldBuilder.Append('"');
                        }
                        else
                        {
                            // Closing quote
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        fieldBuilder.Append(ch);
                    }
                }
                else
                {
                    if (ch == '"')
                    {
                        inQuotes = true;
                    }
                    else if (ch == delimiter)
                    {
                        row.Add(fieldBuilder.ToString());
                        fieldBuilder.Clear();
                    }
                    else if (ch == '\r')
                    {
                        // Check for CRLF
                        if (reader.Peek() == '\n')
                        {
                            reader.Read();
                        }
                        row.Add(fieldBuilder.ToString());
                        fieldBuilder.Clear();
                        yield return row.ToArray();
                        row.Clear();
                    }
                    else if (ch == '\n')
                    {
                        row.Add(fieldBuilder.ToString());
                        fieldBuilder.Clear();
                        yield return row.ToArray();
                        row.Clear();
                    }
                    else
                    {
                        fieldBuilder.Append(ch);
                    }
                }
            }

            // Flush last record if not empty
            if (fieldBuilder.Length > 0 || row.Count > 0)
            {
                row.Add(fieldBuilder.ToString());
                yield return row.ToArray();
            }
        }
    }
}
