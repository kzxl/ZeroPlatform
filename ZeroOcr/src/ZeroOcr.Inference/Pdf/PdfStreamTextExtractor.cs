using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Inference.Pdf;

/// <summary>
/// High-speed pure C# PDF content stream parser and native text extractor.
/// Interrogates PDF page trees, decompresses FlateDecode content streams, and parses PDF text operators (BT, ET, Tj, TJ).
/// Operates with zero external dependencies across all supported target frameworks.
/// </summary>
public static class PdfStreamTextExtractor
{
    /// <summary>
    /// Quick probe: checks if a PDF byte sequence contains native digital text streams.
    /// </summary>
    public static bool HasNativeText(ReadOnlySpan<byte> pdfBytes, int minChars = 30)
    {
        int count = CountApproximateNativeCharacters(pdfBytes);
        return count >= minChars;
    }

    /// <summary>
    /// Inspects and counts approximate alphanumeric characters embedded in uncompressed or FlateDecode streams.
    /// </summary>
    public static int CountApproximateNativeCharacters(ReadOnlySpan<byte> pdfBytes)
    {
        if (pdfBytes.Length < 32) return 0;

        // Quick heuristic search for text operators in raw or decompressed streams
        byte[] bytes = pdfBytes.ToArray();
        string raw = Encoding.ASCII.GetString(bytes);

        int charCount = 0;
        int searchIdx = 0;

        // Scan for stream ... endstream blocks
        while (searchIdx < raw.Length)
        {
            int streamStart = raw.IndexOf("stream\r\n", searchIdx, StringComparison.Ordinal);
            int headerOffset = 8;
            if (streamStart < 0)
            {
                streamStart = raw.IndexOf("stream\n", searchIdx, StringComparison.Ordinal);
                headerOffset = 7;
            }
            if (streamStart < 0) break;

            int dataStart = streamStart + headerOffset;
            int streamEnd = raw.IndexOf("endstream", dataStart, StringComparison.Ordinal);
            if (streamEnd < 0) break;

            searchIdx = streamEnd + 9;

            // Check if stream is FlateDecode
            int dictStart = raw.LastIndexOf("<<", streamStart, StringComparison.Ordinal);
            bool isFlate = false;
            if (dictStart >= 0 && dictStart < streamStart)
            {
                string dict = raw.Substring(dictStart, streamStart - dictStart);
                isFlate = dict.IndexOf("/FlateDecode", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            int length = streamEnd - dataStart;
            if (length <= 0) continue;

            byte[] streamBytes;
            if (isFlate)
            {
                streamBytes = TryDecompressFlate(bytes, dataStart, length);
            }
            else
            {
                streamBytes = new byte[length];
                Buffer.BlockCopy(bytes, dataStart, streamBytes, 0, length);
            }

            if (streamBytes != null && streamBytes.Length > 0)
            {
                charCount += ExtractTextFromDecodedStream(streamBytes, out _);
            }
        }

        return charCount;
    }

    /// <summary>
    /// Extracts text lines from a decompressed PDF content stream.
    /// </summary>
    public static int ExtractTextFromDecodedStream(byte[] streamBytes, out List<string> lines)
    {
        lines = new List<string>();
        string content = Encoding.UTF8.GetString(streamBytes);

        var currentLine = new StringBuilder();
        int charCount = 0;

        // Simple token scanner for Tj and TJ operators
        int i = 0;
        int len = content.Length;

        while (i < len)
        {
            // Look for string literal (...)
            if (content[i] == '(')
            {
                int strStart = i + 1;
                int parenDepth = 1;
                i++;

                while (i < len && parenDepth > 0)
                {
                    if (content[i] == '\\' && i + 1 < len)
                    {
                        i += 2; // skip escape
                        continue;
                    }
                    if (content[i] == '(') parenDepth++;
                    else if (content[i] == ')') parenDepth--;
                    i++;
                }

                int strEnd = i - 1;
                if (strEnd >= strStart)
                {
                    string rawLiteral = content.Substring(strStart, strEnd - strStart);
                    string decoded = UnescapePdfString(rawLiteral);
                    currentLine.Append(decoded);
                    charCount += decoded.Length;
                }
            }
            else if (i + 1 < len && content[i] == 'T' && (content[i + 1] == 'j' || content[i + 1] == 'J'))
            {
                // Commit line on line break or text block completion
                i += 2;
            }
            else if (i + 2 < len && (content[i] == 'E' && content[i + 1] == 'T' || content[i] == 'T' && content[i + 1] == '*'))
            {
                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine.ToString().Trim());
                    currentLine.Clear();
                }
                i += 2;
            }
            else
            {
                i++;
            }
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine.ToString().Trim());
        }

        return charCount;
    }

    private static string UnescapePdfString(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        var sb = new StringBuilder(raw.Length);

        for (int i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '\\' && i + 1 < raw.Length)
            {
                char next = raw[++i];
                switch (next)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case '(': sb.Append('('); break;
                    case ')': sb.Append(')'); break;
                    case '\\': sb.Append('\\'); break;
                    default:
                        // Octal escapes \ddd
                        if (char.IsDigit(next) && i + 2 < raw.Length && char.IsDigit(raw[i + 1]) && char.IsDigit(raw[i + 2]))
                        {
                            int octal = Convert.ToInt32(raw.Substring(i, 3), 8);
                            sb.Append((char)octal);
                            i += 2;
                        }
                        else
                        {
                            sb.Append(next);
                        }
                        break;
                }
            }
            else
            {
                sb.Append(raw[i]);
            }
        }

        return sb.ToString();
    }

    private static byte[] TryDecompressFlate(byte[] source, int offset, int length)
    {
        try
        {
            // Skip 2-byte zlib header (usually 0x78 0x9C, 0x78 0x01, or 0x78 0xDA)
            int zlibOffset = offset;
            int zlibLength = length;

            if (length > 2 && source[offset] == 0x78)
            {
                zlibOffset += 2;
                zlibLength -= 2;
            }

            using var memStream = new MemoryStream(source, zlibOffset, zlibLength);
            using var deflate = new DeflateStream(memStream, CompressionMode.Decompress);
            using var outStream = new MemoryStream();

            byte[] buffer = new byte[4096];
            int read;
            while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
            {
                outStream.Write(buffer, 0, read);
            }

            return outStream.ToArray();
        }
        catch
        {
            // Fallback: return raw bytes if decompression fails
            byte[] rawFallback = new byte[length];
            Buffer.BlockCopy(source, offset, rawFallback, 0, length);
            return rawFallback;
        }
    }
}
