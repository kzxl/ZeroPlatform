using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ZeroReports.Pdf
{
    /// <summary>
    /// Represents an embedded raster image within a PDF document.
    /// Supports direct JPEG (DCTDecode) embedding without third-party dependencies.
    /// </summary>
    public class PdfImage
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; } = string.Empty;
        public int Width { get; }
        public int Height { get; }
        public byte[] Data { get; }

        public PdfImage(byte[] data, int width, int height)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Lightweight, high-performance pure C# vector PDF 1.4 document generator.
    /// Completely zero external dependencies, producing valid PDF 1.4 files with
    /// vector graphics, multi-page support, RGB coloring, embedded images, and typography.
    /// </summary>
    public class PdfDocument : IDisposable
    {
        private readonly List<PdfPage> _pages = new List<PdfPage>();
        private readonly List<PdfImage> _images = new List<PdfImage>();
        private readonly List<long> _objectOffsets = new List<long>();
        private readonly MemoryStream _outputStream = new MemoryStream();

        public static readonly (float Width, float Height) PageSizeA4 = (595.28f, 841.89f);
        public static readonly (float Width, float Height) PageSizeLetter = (612.00f, 792.00f);
        public static readonly (float Width, float Height) PageSizeA3 = (841.89f, 1190.55f);

        public PdfDocument()
        {
        }

        /// <summary>
        /// Creates and adds a new page to the document.
        /// </summary>
        public PdfPage AddPage(float width = 595.28f, float height = 841.89f)
        {
            var page = new PdfPage(this, width, height);
            _pages.Add(page);
            return page;
        }

        public IReadOnlyList<PdfPage> Pages => _pages;
        public IReadOnlyList<PdfImage> Images => _images;

        /// <summary>
        /// Registers an image into the document and returns a <see cref="PdfImage"/> handle.
        /// Extracts JPEG dimensions automatically if not specified.
        /// </summary>
        public PdfImage AddImage(byte[] jpegData, int width = 0, int height = 0)
        {
            if (width <= 0 || height <= 0)
            {
                if (TryExtractJpegDimensions(jpegData, out int w, out int h))
                {
                    width = w;
                    height = h;
                }
                else
                {
                    width = 100;
                    height = 100;
                }
            }

            var img = new PdfImage(jpegData, width, height)
            {
                Name = $"Im{_images.Count + 1}"
            };
            _images.Add(img);
            return img;
        }

        /// <summary>
        /// Serializes the PDF structure into a byte array.
        /// </summary>
        public byte[] ToByteArray()
        {
            using (var ms = new MemoryStream())
            {
                SaveTo(ms);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Writes the entire PDF document to the destination stream.
        /// </summary>
        public void SaveTo(Stream destination)
        {
            var countingStream = new CountingStream(destination);
            var streamWriter = new StreamWriter(countingStream, Encoding.ASCII, 4096, leaveOpen: true);

            var offsets = new List<long>();

            // 1. PDF Header
            streamWriter.WriteLine("%PDF-1.4");
            streamWriter.WriteLine("%\xE2\xE3\xCF\xD3"); // Binary marker
            streamWriter.Flush();

            int currentObjId = 1;

            // Object 1: Catalog
            int catalogId = currentObjId++;
            offsets.Add(countingStream.Position);
            int pagesRootId = currentObjId++; // Object 2

            streamWriter.WriteLine($"{catalogId} 0 obj");
            streamWriter.WriteLine("<< /Type /Catalog /Pages 2 0 R >>");
            streamWriter.WriteLine("endobj");
            streamWriter.Flush();

            // Fonts: Helvetica (F1) and Helvetica-Bold (F2)
            int fontHelvId = currentObjId++;
            offsets.Add(countingStream.Position);
            streamWriter.WriteLine($"{fontHelvId} 0 obj");
            streamWriter.WriteLine("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
            streamWriter.WriteLine("endobj");
            streamWriter.Flush();

            int fontBoldId = currentObjId++;
            offsets.Add(countingStream.Position);
            streamWriter.WriteLine($"{fontBoldId} 0 obj");
            streamWriter.WriteLine("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");
            streamWriter.WriteLine("endobj");
            streamWriter.Flush();

            // Images XObjects
            var imageObjIds = new Dictionary<PdfImage, int>();
            for (int i = 0; i < _images.Count; i++)
            {
                var img = _images[i];
                int imgId = currentObjId++;
                img.Id = imgId;
                imageObjIds[img] = imgId;

                offsets.Add(countingStream.Position);
                streamWriter.WriteLine($"{imgId} 0 obj");
                streamWriter.WriteLine($"<< /Type /XObject /Subtype /Image /Width {img.Width} /Height {img.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {img.Data.Length} >>");
                streamWriter.WriteLine("stream");
                streamWriter.Flush();

                countingStream.Write(img.Data, 0, img.Data.Length);
                countingStream.Flush();

                streamWriter.WriteLine();
                streamWriter.WriteLine("endstream");
                streamWriter.WriteLine("endobj");
                streamWriter.Flush();
            }

            // Pre-calculate page object IDs
            var pageObjIds = new List<int>();
            var contentObjIds = new List<int>();
            for (int i = 0; i < _pages.Count; i++)
            {
                pageObjIds.Add(currentObjId++);
                contentObjIds.Add(currentObjId++);
            }

            // Object 2: Write Pages Root
            offsets.Insert(1, countingStream.Position); // Insert at index 1 for object 2
            streamWriter.WriteLine($"{pagesRootId} 0 obj");
            streamWriter.Write("<< /Type /Pages /Kids [");
            foreach (var pid in pageObjIds)
            {
                streamWriter.Write($"{pid} 0 R ");
            }
            streamWriter.WriteLine($"] /Count {_pages.Count} >>");
            streamWriter.WriteLine("endobj");
            streamWriter.Flush();

            // Build shared XObject dictionary string if any images exist
            string xObjectDict = "";
            if (_images.Count > 0)
            {
                var sbX = new StringBuilder();
                sbX.Append(" /XObject << ");
                foreach (var img in _images)
                {
                    sbX.Append($"/{img.Name} {imageObjIds[img]} 0 R ");
                }
                sbX.Append(">>");
                xObjectDict = sbX.ToString();
            }

            // Write each Page and its Content Stream
            for (int i = 0; i < _pages.Count; i++)
            {
                var page = _pages[i];
                int pid = pageObjIds[i];
                int cid = contentObjIds[i];

                // Page Object
                offsets.Add(countingStream.Position);
                streamWriter.WriteLine($"{pid} 0 obj");
                streamWriter.WriteLine($"<< /Type /Page /Parent {pagesRootId} 0 R /MediaBox [0 0 {page.Width.ToString("F2", CultureInfo.InvariantCulture)} {page.Height.ToString("F2", CultureInfo.InvariantCulture)}] /Contents {cid} 0 R /Resources << /Font << /F1 {fontHelvId} 0 R /F2 {fontBoldId} 0 R >>{xObjectDict} >> >>");
                streamWriter.WriteLine("endobj");
                streamWriter.Flush();

                // Content Stream Object
                byte[] streamBytes = page.GetContentBytes();
                offsets.Add(countingStream.Position);
                streamWriter.WriteLine($"{cid} 0 obj");
                streamWriter.WriteLine($"<< /Length {streamBytes.Length} >>");
                streamWriter.WriteLine("stream");
                streamWriter.Flush();

                countingStream.Write(streamBytes, 0, streamBytes.Length);
                countingStream.Flush();

                streamWriter.WriteLine();
                streamWriter.WriteLine("endstream");
                streamWriter.WriteLine("endobj");
                streamWriter.Flush();
            }

            // Cross-Reference Table
            long xrefOffset = countingStream.Position;
            int totalObjects = offsets.Count + 1; // 0 is dummy

            streamWriter.WriteLine("xref");
            streamWriter.WriteLine($"0 {totalObjects}");
            streamWriter.WriteLine("0000000000 65535 f ");
            foreach (var off in offsets)
            {
                streamWriter.WriteLine($"{off:D10} 00000 n ");
            }

            // Trailer
            streamWriter.WriteLine("trailer");
            streamWriter.WriteLine($"<< /Size {totalObjects} /Root {catalogId} 0 R >>");
            streamWriter.WriteLine("startxref");
            streamWriter.WriteLine(xrefOffset);
            streamWriter.WriteLine("%%EOF");
            streamWriter.Flush();
        }

        public void SaveToFile(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                SaveTo(fs);
            }
        }

        public static bool TryExtractJpegDimensions(byte[] data, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (data == null || data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
                return false;

            int i = 2;
            while (i < data.Length - 8)
            {
                if (data[i] != 0xFF) { i++; continue; }
                byte marker = data[i + 1];
                if (marker == 0xC0 || marker == 0xC2) // SOF0 or SOF2
                {
                    height = (data[i + 5] << 8) | data[i + 6];
                    width = (data[i + 7] << 8) | data[i + 8];
                    return width > 0 && height > 0;
                }
                int len = (data[i + 2] << 8) | data[i + 3];
                i += 2 + len;
            }
            return false;
        }

        public void Dispose()
        {
            _outputStream.Dispose();
        }

        private class CountingStream : Stream
        {
            private readonly Stream _baseStream;
            private long _position;

            public CountingStream(Stream baseStream)
            {
                _baseStream = baseStream;
                _position = baseStream.CanSeek ? baseStream.Position : 0;
            }

            public override bool CanRead => _baseStream.CanRead;
            public override bool CanSeek => _baseStream.CanSeek;
            public override bool CanWrite => _baseStream.CanWrite;
            public override long Length => _baseStream.Length;
            public override long Position
            {
                get => _position;
                set
                {
                    _baseStream.Position = value;
                    _position = value;
                }
            }

            public override void Flush() => _baseStream.Flush();
            public override int Read(byte[] buffer, int offset, int count) => _baseStream.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
            public override void SetLength(long value) => _baseStream.SetLength(value);

            public override void Write(byte[] buffer, int offset, int count)
            {
                _baseStream.Write(buffer, offset, count);
                _position += count;
            }
        }
    }

    /// <summary>
    /// Represents a single page in a PDF document with vector graphics and text commands.
    /// </summary>
    public class PdfPage
    {
        public PdfDocument Document { get; }
        public float Width { get; }
        public float Height { get; }

        private readonly StringBuilder _content = new StringBuilder();

        public PdfPage(PdfDocument document, float width, float height)
        {
            Document = document;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Converts standard top-left screen coordinates (x, y) to PDF bottom-left coordinates.
        /// </summary>
        public float ToPdfY(float topY) => Height - topY;

        public PdfPage SetStrokeColor(float r, float g, float b)
        {
            _content.Append(r.ToString("F3", CultureInfo.InvariantCulture)).Append(" ")
                    .Append(g.ToString("F3", CultureInfo.InvariantCulture)).Append(" ")
                    .Append(b.ToString("F3", CultureInfo.InvariantCulture)).AppendLine(" RG");
            return this;
        }

        public PdfPage SetFillColor(float r, float g, float b)
        {
            _content.Append(r.ToString("F3", CultureInfo.InvariantCulture)).Append(" ")
                    .Append(g.ToString("F3", CultureInfo.InvariantCulture)).Append(" ")
                    .Append(b.ToString("F3", CultureInfo.InvariantCulture)).AppendLine(" rg");
            return this;
        }

        public PdfPage SetLineWidth(float width)
        {
            _content.Append(width.ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" w");
            return this;
        }

        public PdfPage DrawLine(float x1, float y1, float x2, float y2)
        {
            float py1 = ToPdfY(y1);
            float py2 = ToPdfY(y2);
            _content.Append(x1.ToString("F2", CultureInfo.InvariantCulture)).Append(" ")
                    .Append(py1.ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" m")
                    .Append(x2.ToString("F2", CultureInfo.InvariantCulture)).Append(" ")
                    .Append(py2.ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" l S");
            return this;
        }

        public PdfPage DrawRectangle(float x, float y, float width, float height, bool fill = false, bool stroke = true)
        {
            float py = ToPdfY(y + height); // bottom-left in PDF coordinates
            _content.Append(x.ToString("F2", CultureInfo.InvariantCulture)).Append(" ")
                    .Append(py.ToString("F2", CultureInfo.InvariantCulture)).Append(" ")
                    .Append(width.ToString("F2", CultureInfo.InvariantCulture)).Append(" ")
                    .Append(height.ToString("F2", CultureInfo.InvariantCulture)).Append(" re ");

            if (fill && stroke)
                _content.AppendLine("B");
            else if (fill)
                _content.AppendLine("f");
            else if (stroke)
                _content.AppendLine("S");
            else
                _content.AppendLine("n");

            return this;
        }

        public PdfPage DrawText(string text, float x, float y, float fontSize = 12f, string font = "F1")
        {
            if (string.IsNullOrEmpty(text)) return this;
            float py = ToPdfY(y);

            string escaped = EscapePdfText(text);

            _content.AppendLine("BT");
            _content.Append("/").Append(font).Append(" ")
                    .Append(fontSize.ToString("F1", CultureInfo.InvariantCulture)).AppendLine(" Tf");
            _content.Append(x.ToString("F2", CultureInfo.InvariantCulture)).Append(" ")
                    .Append(py.ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" Td");
            _content.Append("(").Append(escaped).AppendLine(") Tj");
            _content.AppendLine("ET");

            return this;
        }

        public PdfPage DrawTextBold(string text, float x, float y, float fontSize = 12f)
        {
            return DrawText(text, x, y, fontSize, font: "F2");
        }

        public PdfPage DrawTextRotated(string text, float x, float y, float fontSize = 54f, float angleDegrees = 45f, string font = "F1")
        {
            if (string.IsNullOrEmpty(text)) return this;
            float py = ToPdfY(y);
            string escaped = EscapePdfText(text);

            double rad = angleDegrees * (Math.PI / 180.0);
            float cos = (float)Math.Cos(rad);
            float sin = (float)Math.Sin(rad);

            _content.AppendLine("q");
            _content.Append(cos.ToString("F4", CultureInfo.InvariantCulture)).Append(" ")
                    .Append(sin.ToString("F4", CultureInfo.InvariantCulture)).Append(" ")
                    .Append((-sin).ToString("F4", CultureInfo.InvariantCulture)).Append(" ")
                    .Append(cos.ToString("F4", CultureInfo.InvariantCulture)).Append(" ")
                    .Append(x.ToString("F2", CultureInfo.InvariantCulture)).Append(" ")
                    .Append(py.ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" cm");

            _content.AppendLine("BT");
            _content.Append("/").Append(font).Append(" ")
                    .Append(fontSize.ToString("F1", CultureInfo.InvariantCulture)).AppendLine(" Tf");
            _content.AppendLine("0 0 Td");
            _content.Append("(").Append(escaped).AppendLine(") Tj");
            _content.AppendLine("ET");
            _content.AppendLine("Q");

            return this;
        }

        public PdfPage DrawImage(PdfImage image, float x, float y, float width, float height)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            float py = ToPdfY(y + height);

            _content.AppendLine("q");
            _content.Append(width.ToString("F2", CultureInfo.InvariantCulture)).Append(" 0 0 ")
                    .Append(height.ToString("F2", CultureInfo.InvariantCulture)).Append(" ")
                    .Append(x.ToString("F2", CultureInfo.InvariantCulture)).Append(" ")
                    .Append(py.ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" cm");
            _content.Append("/").Append(image.Name).AppendLine(" Do");
            _content.AppendLine("Q");

            return this;
        }

        private static string EscapePdfText(string input)
        {
            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (c == '(' || c == ')' || c == '\\')
                {
                    sb.Append('\\').Append(c);
                }
                else if (c >= 32 && c <= 126)
                {
                    sb.Append(c);
                }
                else
                {
                    // Fallback for non-ASCII in basic Type1 font
                    sb.Append('?');
                }
            }
            return sb.ToString();
        }

        internal byte[] GetContentBytes()
        {
            return Encoding.ASCII.GetBytes(_content.ToString());
        }
    }
}
