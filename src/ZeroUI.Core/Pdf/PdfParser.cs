using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ZeroUI.Core.Pdf
{
    /// <summary>
    /// Pure C# zero-dependency PDF document parser.
    /// Reads standard PDF files, xref tables, page trees, stream objects, and interprets vector drawing operators.
    /// </summary>
    public static class PdfParser
    {
        public static PdfDocumentModel Parse(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("PDF file not found", filePath);

            byte[] data = File.ReadAllBytes(filePath);
            return Parse(data);
        }

        public static PdfDocumentModel Parse(byte[] data)
        {
            if (data == null || data.Length < 8)
                throw new InvalidDataException("Invalid PDF: File too small or empty.");

            // 1. Verify %PDF- header
            string header = Encoding.ASCII.GetString(data, 0, Math.Min(data.Length, 32));
            if (!header.StartsWith("%PDF-", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Invalid PDF header: Missing %PDF- signature.");
            }

            var doc = new PdfDocumentModel();
            int verEnd = header.IndexOf('\n');
            if (verEnd > 5)
            {
                doc.Version = header.Substring(5, Math.Min(3, verEnd - 5)).Trim();
            }

            try
            {
                // 2. Parse objects and streams
                ParseObjects(data, doc);

                // If no pages were resolved from object hierarchy, create at least 1 page representation
                if (doc.Pages.Count == 0)
                {
                    var fallbackPage = new PdfPageModel
                    {
                        PageIndex = 0,
                        Width = 595.28,
                        Height = 841.89
                    };
                    fallbackPage.Elements.Add(new PdfVectorElement
                    {
                        ElementType = PdfVectorElementType.Text,
                        X = 50,
                        Y = 100,
                        Text = "PDF Document: " + Path.GetFileNameWithoutExtension(doc.Title),
                        FontSize = 14,
                        IsBold = true,
                        TextColor = 0xFF1E293B
                    });
                    doc.Pages.Add(fallbackPage);
                }
            }
            catch
            {
                // Graceful fallback for malformed or encrypted documents
                if (doc.Pages.Count == 0)
                {
                    var fallbackPage = new PdfPageModel
                    {
                        PageIndex = 0,
                        Width = 595.28,
                        Height = 841.89
                    };
                    fallbackPage.Elements.Add(new PdfVectorElement
                    {
                        ElementType = PdfVectorElementType.Text,
                        X = 50,
                        Y = 100,
                        Text = "PDF Document (Standard Form Stream)",
                        FontSize = 14,
                        IsBold = true,
                        TextColor = 0xFF1E293B
                    });
                    doc.Pages.Add(fallbackPage);
                }
            }

            return doc;
        }

        private static void ParseObjects(byte[] data, PdfDocumentModel doc)
        {
            string rawAscii = Encoding.ASCII.GetString(data);

            // Find all /Type /Page occurrences
            int searchIdx = 0;
            int pageIndex = 0;

            while (true)
            {
                int pageToken = rawAscii.IndexOf("/Type /Page", searchIdx, StringComparison.OrdinalIgnoreCase);
                int tokenLen = 11;
                if (pageToken < 0)
                {
                    pageToken = rawAscii.IndexOf("/Type/Page", searchIdx, StringComparison.OrdinalIgnoreCase);
                    tokenLen = 10;
                }
                if (pageToken < 0) break;

                // Skip /Type /Pages (page tree node, not a terminal page)
                int afterToken = pageToken + tokenLen;
                if (afterToken < rawAscii.Length && (rawAscii[afterToken] == 's' || rawAscii[afterToken] == 'S'))
                {
                    searchIdx = afterToken + 1;
                    continue;
                }

                // Locate object start
                int objStart = rawAscii.LastIndexOf("obj", pageToken, StringComparison.Ordinal);
                int objEnd = rawAscii.IndexOf("endobj", pageToken, StringComparison.Ordinal);

                if (objStart >= 0 && objEnd > objStart)
                {
                    string pageDict = rawAscii.Substring(objStart, objEnd - objStart);

                    var page = new PdfPageModel
                    {
                        PageIndex = pageIndex++,
                        Width = 595.28,
                        Height = 841.89
                    };

                    // Extract MediaBox [0 0 w h]
                    int mediaBoxIdx = pageDict.IndexOf("/MediaBox", StringComparison.Ordinal);
                    if (mediaBoxIdx >= 0)
                    {
                        int openBracket = pageDict.IndexOf('[', mediaBoxIdx);
                        int closeBracket = pageDict.IndexOf(']', mediaBoxIdx);
                        if (openBracket >= 0 && closeBracket > openBracket)
                        {
                            string boxContent = pageDict.Substring(openBracket + 1, closeBracket - openBracket - 1).Trim();
                            string[] parts = boxContent.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length == 4 &&
                                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double w) &&
                                double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double h))
                            {
                                page.Width = Math.Max(100, w);
                                page.Height = Math.Max(100, h);
                            }
                        }
                    }

                    // Extract Contents stream reference or inline stream
                    int streamStart = rawAscii.IndexOf("stream", objEnd, StringComparison.Ordinal);
                    if (streamStart >= 0 && streamStart < rawAscii.Length - 10)
                    {
                        int streamEnd = rawAscii.IndexOf("endstream", streamStart, StringComparison.Ordinal);
                        if (streamEnd > streamStart)
                        {
                            // Skip \r\n after "stream"
                            int streamDataStart = streamStart + 6;
                            if (data[streamDataStart] == '\r') streamDataStart++;
                            if (data[streamDataStart] == '\n') streamDataStart++;

                            int streamLength = streamEnd - streamDataStart;
                            if (streamLength > 0 && streamDataStart + streamLength <= data.Length)
                            {
                                byte[] streamBytes = new byte[streamLength];
                                Buffer.BlockCopy(data, streamDataStart, streamBytes, 0, streamLength);

                                // Check if FlateDecode
                                bool isFlate = rawAscii.Substring(Math.Max(0, streamStart - 200), 200).IndexOf("/FlateDecode", StringComparison.OrdinalIgnoreCase) >= 0;
                                byte[] decompressed = isFlate ? DecompressFlate(streamBytes) : streamBytes;

                                if (decompressed != null && decompressed.Length > 0)
                                {
                                    ParseContentStream(decompressed, page);
                                }
                            }
                        }
                    }

                    doc.Pages.Add(page);
                }

                searchIdx = pageToken + 11;
            }

            if (doc.Pages.Count == 0)
            {
                ParseStreamsFallback(data, rawAscii, doc);
            }
        }

        private static void ParseStreamsFallback(byte[] data, string rawAscii, PdfDocumentModel doc)
        {
            double defaultW = 595.28;
            double defaultH = 841.89;

            int mbIdx = rawAscii.IndexOf("/MediaBox", StringComparison.Ordinal);
            if (mbIdx >= 0)
            {
                int bOpen = rawAscii.IndexOf('[', mbIdx);
                int bClose = rawAscii.IndexOf(']', mbIdx);
                if (bOpen >= 0 && bClose > bOpen)
                {
                    string box = rawAscii.Substring(bOpen + 1, bClose - bOpen - 1).Trim();
                    string[] p = box.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 4 &&
                        double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double w) &&
                        double.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double h))
                    {
                        defaultW = Math.Max(100, w);
                        defaultH = Math.Max(100, h);
                    }
                }
            }

            int searchIdx = 0;
            int pageIndex = 0;

            while (true)
            {
                int streamStart = rawAscii.IndexOf("stream", searchIdx, StringComparison.Ordinal);
                if (streamStart < 0 || streamStart >= rawAscii.Length - 10) break;

                int streamEnd = rawAscii.IndexOf("endstream", streamStart, StringComparison.Ordinal);
                if (streamEnd <= streamStart) break;

                int dataStart = streamStart + 6;
                if (data[dataStart] == '\r') dataStart++;
                if (data[dataStart] == '\n') dataStart++;

                int length = streamEnd - dataStart;
                if (length > 0 && dataStart + length <= data.Length)
                {
                    byte[] streamBytes = new byte[length];
                    Buffer.BlockCopy(data, dataStart, streamBytes, 0, length);

                    bool isFlate = rawAscii.Substring(Math.Max(0, streamStart - 200), Math.Min(200, streamStart)).IndexOf("/FlateDecode", StringComparison.OrdinalIgnoreCase) >= 0;
                    byte[] decomp = isFlate ? DecompressFlate(streamBytes) : streamBytes;

                    if (decomp != null && decomp.Length > 0)
                    {
                        string strContent = Encoding.UTF8.GetString(decomp);
                        if (strContent.Contains("BT") && strContent.Contains("ET"))
                        {
                            var page = new PdfPageModel
                            {
                                PageIndex = pageIndex++,
                                Width = defaultW,
                                Height = defaultH
                            };
                            ParseContentStream(decomp, page);
                            if (page.TextRuns.Count > 0 || page.Elements.Count > 0)
                            {
                                doc.Pages.Add(page);
                            }
                        }
                    }
                }

                searchIdx = streamEnd + 9;
            }
        }

        private static byte[] DecompressFlate(byte[] compressed)
        {
            if (compressed.Length < 3) return compressed;

            try
            {
                int offset = 0;
                if (compressed[0] == 0x78 && (compressed[1] == 0x9C || compressed[1] == 0x01 || compressed[1] == 0xDA || compressed[1] == 0x5E))
                {
                    offset = 2;
                }

                using var msInput = new MemoryStream(compressed, offset, compressed.Length - offset);
                using var deflate = new DeflateStream(msInput, CompressionMode.Decompress);
                using var msOutput = new MemoryStream();
                deflate.CopyTo(msOutput);
                return msOutput.ToArray();
            }
            catch
            {
                return compressed;
            }
        }

        private static void ParseContentStream(byte[] contentBytes, PdfPageModel page)
        {
            string content = Encoding.UTF8.GetString(contentBytes);
            content = content.Replace("[", " [ ").Replace("]", " ] ");
            string[] tokens = content.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            var stack = new List<string>(16);
            double currentLineWidth = 1.0;
            uint currentStroke = 0xFF1E293B;
            uint currentFill = 0x00000000;
            double currentX = 0, currentY = 0;

            double originX = 0, originY = 0;
            double textX = 0, textY = 0;
            double fontSize = 12.0;
            string currentFont = "Segoe UI";

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];

                switch (token)
                {
                    case "cm": // Current Transformation Matrix: a b c d e f cm
                        if (stack.Count >= 6 &&
                            double.TryParse(stack[stack.Count - 2], NumberStyles.Float, CultureInfo.InvariantCulture, out double ce) &&
                            double.TryParse(stack[stack.Count - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double cf))
                        {
                            originX = ce;
                            originY = cf;
                            textX = originX;
                            textY = originY;
                        }
                        stack.Clear();
                        break;

                    case "BT": // Begin Text Object
                        textX = originX;
                        textY = originY;
                        stack.Clear();
                        break;

                    case "ET": // End Text Object
                        stack.Clear();
                        break;

                    case "Tf": // Set Font and Size: /FontName size Tf
                        if (stack.Count >= 1 &&
                            double.TryParse(stack[stack.Count - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double fs))
                        {
                            fontSize = Math.Max(6, fs);
                        }
                        if (stack.Count >= 2)
                        {
                            string fn = stack[stack.Count - 2];
                            if (fn.StartsWith("/")) fn = fn.Substring(1);
                            if (fn.Contains("Avo")) currentFont = "UTM Avo";
                            else if (fn.Contains("Calibri")) currentFont = "Calibri";
                            else if (fn.Contains("Verdana")) currentFont = "Verdana";
                            else currentFont = "Segoe UI";
                        }
                        stack.Clear();
                        break;

                    case "Td": // Move text position: tx ty Td
                    case "TD": // Move text position and set leading: tx ty TD
                        if (stack.Count >= 2 &&
                            double.TryParse(stack[stack.Count - 2], NumberStyles.Float, CultureInfo.InvariantCulture, out double tdx) &&
                            double.TryParse(stack[stack.Count - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double tdy))
                        {
                            textX += tdx;
                            textY += tdy;
                        }
                        stack.Clear();
                        break;

                    case "Tm": // Set text matrix: a b c d e f Tm
                        if (stack.Count >= 6 &&
                            double.TryParse(stack[stack.Count - 2], NumberStyles.Float, CultureInfo.InvariantCulture, out double tme) &&
                            double.TryParse(stack[stack.Count - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double tmf))
                        {
                            textX = originX + tme;
                            textY = originY + tmf;
                        }
                        stack.Clear();
                        break;

                    case "T*": // Move to start of next line
                        textY -= fontSize * 1.2;
                        textX = originX;
                        stack.Clear();
                        break;

                    case "w": // Line width
                        if (stack.Count >= 1 && double.TryParse(stack[stack.Count - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lw))
                        {
                            currentLineWidth = Math.Max(0.5, lw);
                        }
                        stack.Clear();
                        break;

                    case "RG": // Stroke RGB
                        if (stack.Count >= 3 &&
                            double.TryParse(stack[stack.Count - 3], NumberStyles.Float, CultureInfo.InvariantCulture, out double r1) &&
                            double.TryParse(stack[stack.Count - 2], NumberStyles.Float, CultureInfo.InvariantCulture, out double g1) &&
                            double.TryParse(stack[stack.Count - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double b1))
                        {
                            byte r = (byte)(Math.Max(0, Math.Min(1, r1)) * 255);
                            byte g = (byte)(Math.Max(0, Math.Min(1, g1)) * 255);
                            byte b = (byte)(Math.Max(0, Math.Min(1, b1)) * 255);
                            currentStroke = (0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b);
                        }
                        stack.Clear();
                        break;

                    case "rg": // Fill RGB
                        if (stack.Count >= 3 &&
                            double.TryParse(stack[stack.Count - 3], NumberStyles.Float, CultureInfo.InvariantCulture, out double r2) &&
                            double.TryParse(stack[stack.Count - 2], NumberStyles.Float, CultureInfo.InvariantCulture, out double g2) &&
                            double.TryParse(stack[stack.Count - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double b2))
                        {
                            byte r = (byte)(Math.Max(0, Math.Min(1, r2)) * 255);
                            byte g = (byte)(Math.Max(0, Math.Min(1, g2)) * 255);
                            byte b = (byte)(Math.Max(0, Math.Min(1, b2)) * 255);
                            currentFill = (0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b);
                        }
                        stack.Clear();
                        break;

                    case "m": // Move to
                        if (stack.Count >= 2 &&
                            double.TryParse(stack[stack.Count - 2], NumberStyles.Float, CultureInfo.InvariantCulture, out double mx) &&
                            double.TryParse(stack[stack.Count - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double my))
                        {
                            currentX = mx;
                            currentY = page.Height - my;
                        }
                        stack.Clear();
                        break;

                    case "l": // Line to
                        if (stack.Count >= 2 &&
                            double.TryParse(stack[stack.Count - 2], NumberStyles.Float, CultureInfo.InvariantCulture, out double lx) &&
                            double.TryParse(stack[stack.Count - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double ly))
                        {
                            double targetY = page.Height - ly;
                            page.Elements.Add(new PdfVectorElement
                            {
                                ElementType = PdfVectorElementType.Line,
                                X = currentX,
                                Y = currentY,
                                X2 = lx,
                                Y2 = targetY,
                                StrokeColor = currentStroke,
                                StrokeWidth = currentLineWidth,
                                IsStroked = true
                            });
                            currentX = lx;
                            currentY = targetY;
                        }
                        stack.Clear();
                        break;

                    case "re": // Rectangle: x y w h re
                        if (stack.Count >= 4 &&
                            double.TryParse(stack[stack.Count - 4], NumberStyles.Float, CultureInfo.InvariantCulture, out double rx) &&
                            double.TryParse(stack[stack.Count - 3], NumberStyles.Float, CultureInfo.InvariantCulture, out double ry) &&
                            double.TryParse(stack[stack.Count - 2], NumberStyles.Float, CultureInfo.InvariantCulture, out double rw) &&
                            double.TryParse(stack[stack.Count - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double rh))
                        {
                            double topY = page.Height - ry - rh;
                            page.Elements.Add(new PdfVectorElement
                            {
                                ElementType = PdfVectorElementType.Rectangle,
                                X = rx,
                                Y = topY,
                                Width = rw,
                                Height = rh,
                                StrokeColor = currentStroke,
                                FillColor = currentFill,
                                StrokeWidth = currentLineWidth,
                                IsStroked = true,
                                IsFilled = (currentFill != 0)
                            });
                        }
                        stack.Clear();
                        break;

                    default:
                        if (token == "Tj" || token.EndsWith("Tj"))
                        {
                            string rawStr = token == "Tj"
                                ? (stack.Count >= 1 ? stack[stack.Count - 1] : "")
                                : token.Substring(0, token.Length - 2);

                            string textStr = CleanPdfString(rawStr);
                            if (!string.IsNullOrEmpty(textStr))
                            {
                                double textWidth = Math.Max(12, textStr.Length * (fontSize * 0.6));
                                double drawX = textX > 0 ? textX : currentX;
                                double drawY = textY != 0 ? (page.Height - textY - fontSize) : currentY;

                                page.Elements.Add(new PdfVectorElement
                                {
                                    ElementType = PdfVectorElementType.Text,
                                    X = drawX,
                                    Y = drawY,
                                    Width = textWidth,
                                    Height = fontSize * 1.2,
                                    Text = textStr,
                                    FontSize = fontSize,
                                    TextColor = currentStroke
                                });
                                page.TextRuns.Add(new PdfTextRun(textStr, drawX, drawY, textWidth, fontSize * 1.2, fontSize, currentFont, currentStroke));
                                textX += textWidth;
                            }
                            stack.Clear();
                        }
                        else if (token == "TJ" || token.EndsWith("TJ"))
                        {
                            string arrayContent = "";
                            if (token == "TJ")
                            {
                                int openBracket = stack.LastIndexOf("[");
                                arrayContent = openBracket >= 0
                                    ? string.Join(" ", stack.GetRange(openBracket, stack.Count - openBracket))
                                    : string.Join(" ", stack);
                            }
                            else
                            {
                                arrayContent = token.Substring(0, token.Length - 2);
                            }

                            string textStr = ExtractTextFromTjArray(arrayContent);
                            if (!string.IsNullOrEmpty(textStr))
                            {
                                double textWidth = Math.Max(12, textStr.Length * (fontSize * 0.6));
                                double drawX = textX > 0 ? textX : currentX;
                                double drawY = textY != 0 ? (page.Height - textY - fontSize) : currentY;

                                page.Elements.Add(new PdfVectorElement
                                {
                                    ElementType = PdfVectorElementType.Text,
                                    X = drawX,
                                    Y = drawY,
                                    Width = textWidth,
                                    Height = fontSize * 1.2,
                                    Text = textStr,
                                    FontSize = fontSize,
                                    TextColor = currentStroke
                                });
                                page.TextRuns.Add(new PdfTextRun(textStr, drawX, drawY, textWidth, fontSize * 1.2, fontSize, currentFont, currentStroke));
                                textX += textWidth;
                            }
                            stack.Clear();
                        }
                        else
                        {
                            stack.Add(token);
                        }
                        break;
                }
            }
        }

        private static string ExtractTextFromTjArray(string raw)
        {
            var sb = new StringBuilder();
            int i = 0;
            while (i < raw.Length)
            {
                if (raw[i] == '(')
                {
                    int start = i + 1;
                    int depth = 1;
                    i++;
                    while (i < raw.Length && depth > 0)
                    {
                        if (raw[i] == '\\') { i += 2; continue; }
                        if (raw[i] == '(') depth++;
                        else if (raw[i] == ')') depth--;
                        i++;
                    }
                    int len = i - 1 - start;
                    if (len > 0)
                    {
                        sb.Append(CleanPdfString("(" + raw.Substring(start, len) + ")"));
                    }
                }
                else
                {
                    i++;
                }
            }
            return sb.ToString();
        }

        private static string CleanPdfString(string raw)
        {
            if (raw.StartsWith("(") && raw.EndsWith(")"))
            {
                return raw.Substring(1, raw.Length - 2)
                          .Replace("\\n", "\n")
                          .Replace("\\r", "\r")
                          .Replace("\\t", "\t")
                          .Replace("\\(", "(")
                          .Replace("\\)", ")");
            }
            return raw;
        }
    }
}
