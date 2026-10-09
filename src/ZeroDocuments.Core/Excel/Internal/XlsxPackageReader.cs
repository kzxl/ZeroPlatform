using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using ZeroDocuments.Excel.Models;

namespace ZeroDocuments.Excel.Internal
{
    /// <summary>
    /// Resolves the logical structure of an .xlsx package (workbook part, sheet order, relationship targets,
    /// shared strings, date styles) following OPC relationship semantics instead of hard-coded part names.
    /// </summary>
    internal sealed class XlsxPackageReader
    {
        private const string DefaultWorkbookPath = "xl/workbook.xml";

        private readonly Dictionary<string, ZipArchiveEntry> _entries;
        private readonly List<SheetInfo> _sheets = new List<SheetInfo>();
        private readonly string _workbookPath;
        private readonly ExcelReaderOptions _options;
        private string? _sharedStringsPath;
        private string? _stylesPath;

        internal sealed class SheetInfo
        {
            public string Name { get; set; } = string.Empty;
            public string? SheetId { get; set; }
            public string? PartPath { get; set; }
        }

        /// <summary>True when the workbook uses the 1904 date system (legacy Mac workbooks).</summary>
        public bool Date1904 { get; private set; }

        /// <summary>Sheets in workbook (tab) order.</summary>
        public IReadOnlyList<SheetInfo> Sheets => _sheets;

        private XlsxPackageReader(ZipArchive zip, ExcelReaderOptions? options = null)
        {
            _options = options ?? new ExcelReaderOptions();
            _entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in zip.Entries)
            {
                string key = NormalizePath(entry.FullName);
                if (!_entries.ContainsKey(key)) _entries[key] = entry;
            }

            _workbookPath = ResolveWorkbookPath() ?? DefaultWorkbookPath;
            LoadWorkbook();
        }

        public static XlsxPackageReader Open(ZipArchive zip, ExcelReaderOptions? options = null) =>
            new XlsxPackageReader(zip, options);

        public Stream OpenEntryStream(ZipArchiveEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            if (_options.MaxUncompressedEntryBytes > 0 && entry.Length > _options.MaxUncompressedEntryBytes)
            {
                throw new InvalidDataException(
                    $"Entry '{entry.FullName}' uncompressed size ({entry.Length} bytes) exceeds the maximum allowed limit of {_options.MaxUncompressedEntryBytes} bytes.");
            }

            var stream = entry.Open();
            if (_options.MaxUncompressedEntryBytes > 0)
            {
                return new BoundedReadStream(stream, _options.MaxUncompressedEntryBytes, entry.FullName);
            }
            return stream;
        }

        public ZipArchiveEntry? GetEntry(string path) =>
            _entries.TryGetValue(NormalizePath(path), out var entry) ? entry : null;

        #region Sheet Resolution

        /// <summary>
        /// Resolves the worksheet entry. When <paramref name="sheetName"/> is null, returns the first sheet in tab order.
        /// When a name is given but cannot be resolved, returns null (never a different sheet).
        /// </summary>
        public ZipArchiveEntry? FindSheetEntry(string? sheetName)
        {
            if (!string.IsNullOrEmpty(sheetName))
            {
                foreach (var sheet in _sheets)
                {
                    if (!string.Equals(sheet.Name, sheetName, StringComparison.OrdinalIgnoreCase)) continue;

                    // Never guess "sheet{sheetId}.xml": sheetId is not a file index once sheets are deleted or reordered.
                    return sheet.PartPath != null ? GetEntry(sheet.PartPath) : null;
                }

                return null;
            }

            foreach (var sheet in _sheets)
            {
                var entry = sheet.PartPath != null ? GetEntry(sheet.PartPath) : null;
                if (entry != null) return entry;
            }

            // Fallback for minimal packages without workbook metadata.
            var sheet1 = GetEntry("xl/worksheets/sheet1.xml");
            if (sheet1 != null) return sheet1;

            foreach (var kvp in _entries)
            {
                if (kvp.Key.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                    kvp.Key.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        private string? ResolveWorkbookPath()
        {
            var rels = ReadRelationships("_rels/.rels", sourcePartPath: string.Empty);
            foreach (var rel in rels.Values)
            {
                if (rel.Type.EndsWith("/officeDocument", StringComparison.OrdinalIgnoreCase) && GetEntry(rel.Target) != null)
                {
                    return rel.Target;
                }
            }
            return null;
        }

        private void LoadWorkbook()
        {
            var workbookEntry = GetEntry(_workbookPath);
            if (workbookEntry == null) return;

            var pendingRelIds = new List<string?>();

            using (var stream = OpenEntryStream(workbookEntry))
            using (var reader = SpreadsheetXml.CreateReader(stream))
            {
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element) continue;

                    if (reader.LocalName == "workbookPr")
                    {
                        string? d1904 = reader.GetAttribute("date1904");
                        Date1904 = d1904 == "1" || string.Equals(d1904, "true", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (reader.LocalName == "sheet")
                    {
                        var info = new SheetInfo
                        {
                            Name = reader.GetAttribute("name") ?? string.Empty,
                            SheetId = reader.GetAttribute("sheetId")
                        };
                        pendingRelIds.Add(GetRelationshipIdAttribute(reader));
                        _sheets.Add(info);
                    }
                    else if (reader.LocalName == "definedNames" || reader.LocalName == "calcPr")
                    {
                        // Sheets precede these elements; nothing further is needed.
                        if (_sheets.Count > 0) break;
                    }
                }
            }

            var rels = ReadRelationships(GetRelationshipsPath(_workbookPath), _workbookPath);
            for (int i = 0; i < _sheets.Count; i++)
            {
                string? relId = pendingRelIds[i];
                if (relId != null && rels.TryGetValue(relId, out var rel))
                {
                    _sheets[i].PartPath = rel.Target;
                }
            }

            foreach (var rel in rels.Values)
            {
                if (_sharedStringsPath == null && rel.Type.EndsWith("/sharedStrings", StringComparison.OrdinalIgnoreCase))
                    _sharedStringsPath = rel.Target;
                else if (_stylesPath == null && rel.Type.EndsWith("/styles", StringComparison.OrdinalIgnoreCase))
                    _stylesPath = rel.Target;
            }
        }

        private static string? GetRelationshipIdAttribute(XmlReader reader)
        {
            // Matches r:id regardless of prefix or transitional/strict relationship namespace.
            if (reader.MoveToFirstAttribute())
            {
                do
                {
                    if (reader.LocalName == "id" && !string.IsNullOrEmpty(reader.NamespaceURI))
                    {
                        string value = reader.Value;
                        reader.MoveToElement();
                        return value;
                    }
                } while (reader.MoveToNextAttribute());
                reader.MoveToElement();
            }
            return null;
        }

        #endregion

        #region Relationships & Paths

        private readonly struct Relationship
        {
            public Relationship(string type, string target)
            {
                Type = type;
                Target = target;
            }

            public string Type { get; }
            public string Target { get; }
        }

        private Dictionary<string, Relationship> ReadRelationships(string relsPath, string sourcePartPath)
        {
            var result = new Dictionary<string, Relationship>(StringComparer.Ordinal);
            var entry = GetEntry(relsPath);
            if (entry == null) return result;

            using var stream = OpenEntryStream(entry);
            using var reader = SpreadsheetXml.CreateReader(stream);
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Relationship") continue;
                if (string.Equals(reader.GetAttribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase)) continue;

                string? id = reader.GetAttribute("Id");
                string? target = reader.GetAttribute("Target");
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(target)) continue;

                result[id!] = new Relationship(reader.GetAttribute("Type") ?? string.Empty, ResolveTarget(sourcePartPath, target!));
            }

            return result;
        }

        /// <summary>
        /// Resolves a relationship target relative to its source part (handles absolute "/xl/..." and "../" segments).
        /// </summary>
        internal static string ResolveTarget(string sourcePartPath, string target)
        {
            string normalized = target.Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal))
            {
                return NormalizePath(normalized);
            }

            return CombinePath(GetDirectory(sourcePartPath), normalized);
        }

        private static string CombinePath(string baseDirectory, string relative)
        {
            var segments = new List<string>();
            foreach (var seg in baseDirectory.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                segments.Add(seg);
            }

            foreach (var seg in relative.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (seg == ".") continue;
                if (seg == "..")
                {
                    if (segments.Count > 0)
                    {
                        segments.RemoveAt(segments.Count - 1);
                    }
                    else
                    {
                        throw new InvalidDataException($"Path traversal escape attempt detected in relationship path: '{relative}' from base '{baseDirectory}'.");
                    }
                    continue;
                }
                segments.Add(seg);
            }

            return string.Join("/", segments);
        }

        private static string GetDirectory(string partPath)
        {
            int idx = partPath.LastIndexOf('/');
            return idx > 0 ? partPath.Substring(0, idx) : string.Empty;
        }

        private static string GetRelationshipsPath(string partPath)
        {
            string dir = GetDirectory(partPath);
            string file = partPath.Substring(partPath.LastIndexOf('/') + 1);
            return string.IsNullOrEmpty(dir) ? $"_rels/{file}.rels" : $"{dir}/_rels/{file}.rels";
        }

        private static string NormalizePath(string path)
        {
            string p = path.Replace('\\', '/').TrimStart('/');
            if (p.StartsWith("../") || p.Contains("/../") || p.EndsWith("/..") || p == "..")
            {
                var segments = p.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                int depth = 0;
                foreach (var seg in segments)
                {
                    if (seg == "..")
                    {
                        depth--;
                        if (depth < 0)
                            throw new InvalidDataException($"Malicious Zip entry path traversal (Zip Slip) detected: '{path}'.");
                    }
                    else if (seg != ".")
                    {
                        depth++;
                    }
                }
            }
            return p;
        }

        #endregion

        #region Shared Strings

        /// <summary>
        /// Loads the shared string table. Rich-text runs are concatenated; phonetic (rPh) runs are excluded.
        /// </summary>
        public List<string> LoadSharedStrings()
        {
            var list = new List<string>();
            var entry = GetEntry(_sharedStringsPath ?? CombinePath(GetDirectory(_workbookPath), "sharedStrings.xml"))
                        ?? GetEntry("xl/sharedStrings.xml");
            if (entry == null) return list;

            using var stream = OpenEntryStream(entry);
            using var reader = SpreadsheetXml.CreateReader(stream);

            var sb = new StringBuilder();
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element) continue;

                if (reader.LocalName == "sst")
                {
                    if (int.TryParse(reader.GetAttribute("uniqueCount"), out int unique) && unique > 0)
                    {
                        list.Capacity = Math.Min(unique, 1 << 20);
                    }
                    continue;
                }

                if (reader.LocalName != "si") continue;

                if (reader.IsEmptyElement)
                {
                    list.Add(string.Empty);
                    continue;
                }

                sb.Clear();
                ReadTextRuns(reader, sb);
                list.Add(sb.ToString());
            }

            return list;
        }

        /// <summary>
        /// Reads the content of the current non-empty container element (si / is), appending every &lt;t&gt;
        /// while skipping phonetic runs. Leaves the reader positioned on the container's end element.
        /// </summary>
        internal static void ReadTextRuns(XmlReader reader, StringBuilder sb)
        {
            int depth = reader.Depth;
            reader.Read();
            while (!reader.EOF && !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.LocalName == "t")
                    {
                        sb.Append(SpreadsheetXml.DecodeXString(reader.ReadElementContentAsString()));
                        continue;
                    }
                    if (reader.LocalName == "rPh" || reader.LocalName == "phoneticPr" || reader.LocalName == "extLst")
                    {
                        reader.Skip();
                        continue;
                    }
                }
                reader.Read();
            }
        }

        #endregion

        #region Styles

        /// <summary>
        /// Returns a map from cellXfs index to "is date/time format", or null when the workbook has no date styles.
        /// </summary>
        public bool[]? LoadDateStyleMap()
        {
            var entry = GetEntry(_stylesPath ?? CombinePath(GetDirectory(_workbookPath), "styles.xml"))
                        ?? GetEntry("xl/styles.xml");
            if (entry == null) return null;

            var customFormats = new Dictionary<int, string>();
            var xfFormatIds = new List<int>();

            using (var stream = OpenEntryStream(entry))
            using (var reader = SpreadsheetXml.CreateReader(stream))
            {
                bool inNumFmts = false;
                bool inCellXfs = false;

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        if (reader.LocalName == "numFmts") inNumFmts = false;
                        else if (reader.LocalName == "cellXfs") break; // nothing after cellXfs is needed
                        continue;
                    }

                    if (reader.NodeType != XmlNodeType.Element) continue;

                    switch (reader.LocalName)
                    {
                        case "numFmts":
                            inNumFmts = !reader.IsEmptyElement;
                            break;
                        case "cellXfs":
                            inCellXfs = !reader.IsEmptyElement;
                            if (!inCellXfs) goto done;
                            break;
                        case "numFmt" when inNumFmts:
                            if (int.TryParse(reader.GetAttribute("numFmtId"), out int fmtId))
                            {
                                customFormats[fmtId] = reader.GetAttribute("formatCode") ?? string.Empty;
                            }
                            break;
                        case "xf" when inCellXfs:
                            xfFormatIds.Add(int.TryParse(reader.GetAttribute("numFmtId"), out int xfFmt) ? xfFmt : 0);
                            break;
                    }
                }
            }
            done:

            bool[]? map = null;
            for (int i = 0; i < xfFormatIds.Count; i++)
            {
                if (IsDateFormat(xfFormatIds[i], customFormats))
                {
                    map ??= new bool[xfFormatIds.Count];
                    map[i] = true;
                }
            }
            return map;
        }

        internal static bool IsDateFormat(int numFmtId, Dictionary<int, string> customFormats)
        {
            if (customFormats.TryGetValue(numFmtId, out var code))
            {
                return IsDateFormatCode(code);
            }

            // Built-in date/time formats (ECMA-376 Part 1, 18.8.30) including East Asian locale variants.
            return (numFmtId >= 14 && numFmtId <= 22) ||
                   (numFmtId >= 27 && numFmtId <= 36) ||
                   (numFmtId >= 45 && numFmtId <= 47) ||
                   (numFmtId >= 50 && numFmtId <= 58);
        }

        /// <summary>
        /// Heuristically determines whether a custom number format code renders a date or time.
        /// Ignores quoted literals, escaped characters, padding directives, colors/locales/conditions in brackets,
        /// and inspects only the first (positive) section.
        /// </summary>
        internal static bool IsDateFormatCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            if (code!.Equals("General", StringComparison.OrdinalIgnoreCase)) return false;

            for (int i = 0; i < code.Length; i++)
            {
                char c = code[i];
                switch (c)
                {
                    case '"':
                        int close = code.IndexOf('"', i + 1);
                        if (close < 0) return false;
                        i = close;
                        break;
                    case '\\':
                    case '_':
                    case '*':
                        i++;
                        break;
                    case '[':
                        int end = code.IndexOf(']', i + 1);
                        if (end < 0) return false;
                        string token = code.Substring(i + 1, end - i - 1).ToLowerInvariant();
                        // Elapsed-time tokens: [h], [hh], [m], [mm], [s], [ss]
                        if (token.Length > 0 && token.Trim('h').Length == 0 || token.Length > 0 && token.Trim('m').Length == 0 || token.Length > 0 && token.Trim('s').Length == 0)
                            return true;
                        i = end;
                        break;
                    case ';':
                        return false;
                    case 'y': case 'Y':
                    case 'd': case 'D':
                    case 'm': case 'M':
                    case 'h': case 'H':
                    case 's': case 'S':
                        return true;
                }
            }

            return false;
        }

        #endregion

        #region Bounded Stream

        private sealed class BoundedReadStream : Stream
        {
            private readonly Stream _inner;
            private readonly long _maxBytes;
            private readonly string _entryName;
            private long _bytesRead;

            public BoundedReadStream(Stream inner, long maxBytes, string entryName)
            {
                _inner = inner;
                _maxBytes = maxBytes;
                _entryName = entryName;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = _inner.Read(buffer, offset, count);
                if (read > 0)
                {
                    _bytesRead += read;
                    if (_bytesRead > _maxBytes)
                    {
                        throw new InvalidDataException(
                            $"ZIP entry '{_entryName}' decompressed data exceeded the limit of {_maxBytes:N0} bytes (possible decompression bomb).");
                    }
                }
                return read;
            }

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _inner.Length;
            public override long Position
            {
                get => _inner.Position;
                set => throw new NotSupportedException();
            }

            public override void Flush() => _inner.Flush();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing) _inner.Dispose();
                base.Dispose(disposing);
            }
        }

        #endregion
    }
}
