using System;
using System.IO;
using System.Text;
using System.Xml;
using ZeroDocuments.Excel.Models;

namespace ZeroDocuments.Excel.Internal
{
    /// <summary>
    /// Shared SpreadsheetML constants and low-level XML helpers used by both the writer and reader pipelines.
    /// </summary>
    internal static class SpreadsheetXml
    {
        public const string NsMain = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        public const string NsRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        public const string NsPackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
        public const string NsContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
        public const string NsDrawingSheet = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
        public const string NsDrawingMain = "http://schemas.openxmlformats.org/drawingml/2006/main";

        public const string RelTypeOfficeDocument = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";
        public const string RelTypeWorksheet = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet";
        public const string RelTypeStyles = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles";
        public const string RelTypeSharedStrings = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings";
        public const string RelTypeDrawing = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing";
        public const string RelTypeImage = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";

        /// <summary>Maximum number of columns supported by Excel (XFD).</summary>
        public const int MaxColumns = 16384;

        private static readonly string?[] ColumnNameCache = new string?[MaxColumns + 1];

        /// <summary>
        /// Returns the cached column letter(s) for a 1-based column index (e.g. 1 -> "A").
        /// Avoids re-allocating the same column names for every row during export.
        /// </summary>
        public static string GetColumnName(int columnIndex)
        {
            if (columnIndex <= 0 || columnIndex > MaxColumns)
            {
                return ExcelCellAddress.IndexToColumnName(columnIndex);
            }

            // Benign race: concurrent writers compute identical immutable strings.
            return ColumnNameCache[columnIndex] ??= ExcelCellAddress.IndexToColumnName(columnIndex);
        }

        /// <summary>
        /// Creates a non-indenting UTF-8 (no BOM) XmlWriter that leaves the underlying stream open.
        /// </summary>
        public static XmlWriter CreateWriter(Stream stream)
        {
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                OmitXmlDeclaration = false,
                Indent = false,
                CloseOutput = false,
                // Entitize '\r' so CR / CRLF survive XML end-of-line normalization on read.
                NewLineHandling = NewLineHandling.Entitize
            };
            return XmlWriter.Create(stream, settings);
        }

        /// <summary>
        /// Creates a hardened forward-only XmlReader (DTD prohibited, no external resolver).
        /// </summary>
        public static XmlReader CreateReader(Stream stream)
        {
            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                CloseInput = false
            };
            return XmlReader.Create(stream, settings);
        }

        /// <summary>
        /// Removes characters that are illegal in XML 1.0 while preserving valid surrogate pairs.
        /// Fast path: returns the original instance (zero allocation) when no illegal characters are present.
        /// </summary>
        public static string SanitizeXmlText(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            int firstInvalid = IndexOfInvalidXmlChar(text!);
            if (firstInvalid < 0) return text!;

            var sb = new StringBuilder(text!.Length);
            sb.Append(text, 0, firstInvalid);

            for (int i = firstInvalid; i < text.Length; i++)
            {
                char ch = text[i];

                if (char.IsHighSurrogate(ch) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    sb.Append(ch);
                    sb.Append(text[++i]);
                    continue;
                }

                if (IsValidXmlChar(ch))
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }

        private static int IndexOfInvalidXmlChar(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (char.IsHighSurrogate(ch) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    i++;
                    continue;
                }

                if (!IsValidXmlChar(ch)) return i;
            }

            return -1;
        }

        // XML 1.0 valid single-char code points: #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD]
        private static bool IsValidXmlChar(char ch) =>
            ch == 0x9 || ch == 0xA || ch == 0xD ||
            (ch >= 0x20 && ch <= 0xD7FF) ||
            (ch >= 0xE000 && ch <= 0xFFFD);

        /// <summary>
        /// Normalizes an Excel worksheet name: strips XML-illegal characters, replaces \ / ? * : [ ],
        /// trims surrounding whitespace and apostrophes, avoids the reserved name "History",
        /// and enforces the 31-char limit without splitting surrogate pairs.
        /// </summary>
        public static string SanitizeSheetName(string? name, string fallback)
        {
            if (string.IsNullOrWhiteSpace(name)) return fallback;

            string xmlSafe = SanitizeXmlText(name);
            var sb = new StringBuilder(xmlSafe.Length);
            foreach (char ch in xmlSafe)
            {
                if (ch == '\\' || ch == '/' || ch == '?' || ch == '*' || ch == ':' || ch == '[' || ch == ']')
                    sb.Append('_');
                else if (char.IsControl(ch))
                    continue;
                else
                    sb.Append(ch);
            }

            // Excel rejects names that begin or end with an apostrophe.
            string result = sb.ToString().Trim().Trim('\'').Trim();
            result = TruncateSafe(result, 31);

            if (string.IsNullOrEmpty(result)) return fallback;
            if (string.Equals(result, "History", StringComparison.OrdinalIgnoreCase)) return result + "_";
            return result;
        }

        /// <summary>
        /// Truncates to at most <paramref name="maxLength"/> UTF-16 units without leaving a lone high surrogate.
        /// </summary>
        public static string TruncateSafe(string text, int maxLength)
        {
            if (text.Length <= maxLength) return text;
            int length = maxLength;
            if (length > 0 && char.IsHighSurrogate(text[length - 1])) length--;
            return text.Substring(0, length);
        }

        /// <summary>
        /// Normalizes an RGB/ARGB hex color to 8-digit ARGB (e.g. "FFC7CE" -> "FFFFC7CE", "#F00" -> "FFFF0000").
        /// Throws <see cref="ArgumentException"/> for malformed values instead of emitting an invalid ST_UnsignedIntHex.
        /// </summary>
        public static string NormalizeColor(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return "FF000000";
            string clean = hex!.Trim().TrimStart('#');

            for (int i = 0; i < clean.Length; i++)
            {
                if (!Uri.IsHexDigit(clean[i]))
                    throw new ArgumentException($"Invalid hex color '{hex}'. Expected RGB, RRGGBB or AARRGGBB.", nameof(hex));
            }

            switch (clean.Length)
            {
                case 3:
                    return ("FF" + clean[0] + clean[0] + clean[1] + clean[1] + clean[2] + clean[2]).ToUpperInvariant();
                case 6:
                    return "FF" + clean.ToUpperInvariant();
                case 8:
                    return clean.ToUpperInvariant();
                default:
                    throw new ArgumentException($"Invalid hex color '{hex}'. Expected RGB, RRGGBB or AARRGGBB.", nameof(hex));
            }
        }

        /// <summary>
        /// Returns the OPC content type for a supported image extension, or null when unsupported.
        /// </summary>
        public static string? GetImageContentType(string? extension)
        {
            switch (extension?.Trim().TrimStart('.').ToLowerInvariant())
            {
                case "png": return "image/png";
                case "jpg":
                case "jpeg": return "image/jpeg";
                case "gif": return "image/gif";
                case "bmp": return "image/bmp";
                case "tif":
                case "tiff": return "image/tiff";
                case "emf": return "image/x-emf";
                case "wmf": return "image/x-wmf";
                default: return null;
            }
        }

        /// <summary>All image extensions declared as defaults in [Content_Types].xml.</summary>
        public static readonly string[] SupportedImageExtensions = { "png", "jpg", "jpeg", "gif", "bmp", "tif", "tiff", "emf", "wmf" };

        /// <summary>
        /// Decodes ECMA-376 ST_Xstring escape sequences (_xHHHH_) and unescapes _x005F_.
        /// Fast path: returns the original instance (zero allocation) when no "_x" sequence exists.
        /// </summary>
        public static string DecodeXString(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            int idx = text!.IndexOf("_x", StringComparison.Ordinal);
            if (idx < 0) return text;

            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                if (i + 6 < text.Length && text[i] == '_' && text[i + 1] == 'x' && text[i + 6] == '_')
                {
                    if (IsHexChar(text[i + 2]) && IsHexChar(text[i + 3]) &&
                        IsHexChar(text[i + 4]) && IsHexChar(text[i + 5]))
                    {
                        int code = (HexVal(text[i + 2]) << 12) |
                                   (HexVal(text[i + 3]) << 8) |
                                   (HexVal(text[i + 4]) << 4) |
                                   HexVal(text[i + 5]);

                        // Check for literal escape: _x005F_ followed by another _xHHHH_
                        if (code == 0x005F && i + 13 < text.Length &&
                            text[i + 7] == '_' && text[i + 8] == 'x' && text[i + 13] == '_' &&
                            IsHexChar(text[i + 9]) && IsHexChar(text[i + 10]) &&
                            IsHexChar(text[i + 11]) && IsHexChar(text[i + 12]))
                        {
                            sb.Append('_');
                            i += 6;
                            continue;
                        }

                        sb.Append((char)code);
                        i += 6;
                        continue;
                    }
                }

                sb.Append(text[i]);
            }

            return sb.ToString();
        }

        private static bool IsHexChar(char c) =>
            (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');

        private static int HexVal(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            return 0;
        }
    }
}
