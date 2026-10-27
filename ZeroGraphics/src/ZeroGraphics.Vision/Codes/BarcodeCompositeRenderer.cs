using System;
using System.Collections.Generic;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// Pure C# sovereign barcode and Human Readable Interpretation (HRI) composite symbol renderer.
    /// Renders pixel-perfect 1D barcodes and crisp typography into zero-allocation ImageBuffer instances
    /// without GDI+, DirectWrite, or external fonts.
    /// </summary>
    public static unsafe class BarcodeCompositeRenderer
    {
        // 5x7 ASCII bitmap font tables (Rows 0..6, 5 bits wide: bits 4..0)
        private static readonly byte[][] Font5x7 = InitializeFont();

        /// <summary>
        /// Renders an EAN-13 barcode symbol with GS1 standard grouped HRI text and protruding guard bars.
        /// </summary>
        public static ImageBuffer RenderEan13(string digits, HriOptions? options = null)
        {
            options ??= new HriOptions { Alignment = HriAlignment.EanGrouped, ProtrudeGuardBars = true };
            bool[] modules = Ean13Encoder.Encode(digits);

            // Canonical 13-digit string including checksum
            string cleanDigits = digits.Replace(" ", "").Trim();
            if (cleanDigits.Length == 12)
            {
                cleanDigits += Ean13Encoder.CalculateChecksum(cleanDigits).ToString();
            }

            var layout = HriLayoutEngine.ComputeLayout(BarcodeSymbology.Ean13, modules.Length, cleanDigits, options);
            var buffer = ImageBuffer.CreateGray8(layout.TotalWidth, layout.TotalHeight);

            // Fill background white
            FillSolid(buffer, 255);

            // Draw EAN-13 bars with protruding guard bar logic
            int quietPixels = layout.QuietZonePixels;
            int moduleWidth = layout.ModuleWidth;
            int barStartY = layout.BarStartY;
            int dataBarHeight = layout.BarHeight;
            int guardBarHeight = options.ProtrudeGuardBars ? (dataBarHeight + options.GuardBarExtension) : dataBarHeight;

            for (int m = 0; m < modules.Length; m++)
            {
                if (!modules[m]) continue;

                // Check if module is part of Start Guard (0..2), Center Guard (45..49), or End Guard (92..94)
                bool isGuard = (m <= 2) || (m >= 45 && m <= 49) || (m >= 92 && m <= 94);
                int h = isGuard ? guardBarHeight : dataBarHeight;
                int x0 = quietPixels + (m * moduleWidth);

                DrawRect(buffer, x0, barStartY, moduleWidth, h, 0);
            }

            // Draw HRI text spans
            foreach (var span in layout.TextSpans)
            {
                DrawString(buffer, span.Text, span.X, span.Y, span.Scale, 0);
            }

            return buffer;
        }

        /// <summary>
        /// Renders a Code 128 or GS1-128 symbol with centered or aligned HRI text.
        /// </summary>
        public static ImageBuffer RenderCode128(string text, HriOptions? options = null, bool isGs1 = false)
        {
            options ??= new HriOptions();
            bool[] modules = Code128Encoder.Encode(text, isGs1);
            string hriText = isGs1 ? Gs1HriFormatter.FormatHri(text) : text;

            var layout = HriLayoutEngine.ComputeLayout(BarcodeSymbology.Code128, modules.Length, hriText, options);
            var buffer = ImageBuffer.CreateGray8(layout.TotalWidth, layout.TotalHeight);

            FillSolid(buffer, 255);

            int quietPixels = layout.QuietZonePixels;
            int moduleWidth = layout.ModuleWidth;

            for (int m = 0; m < modules.Length; m++)
            {
                if (!modules[m]) continue;
                int x0 = quietPixels + (m * moduleWidth);
                DrawRect(buffer, x0, layout.BarStartY, moduleWidth, layout.BarHeight, 0);
            }

            foreach (var span in layout.TextSpans)
            {
                DrawString(buffer, span.Text, span.X, span.Y, span.Scale, 0);
            }

            return buffer;
        }

        /// <summary>
        /// Renders a GS1-128 symbol directly from an HRI bracketed string (e.g. "(01)08801234567891(10)LOT123").
        /// </summary>
        public static ImageBuffer RenderGs1(string bracketedHri, HriOptions? options = null)
        {
            string payload = Gs1HriFormatter.ToBarcodePayload(bracketedHri);
            options ??= new HriOptions();
            bool[] modules = Code128Encoder.Encode(payload, isGs1: true);
            string formattedHri = Gs1HriFormatter.FormatHri(bracketedHri);

            var layout = HriLayoutEngine.ComputeLayout(BarcodeSymbology.Code128, modules.Length, formattedHri, options);
            var buffer = ImageBuffer.CreateGray8(layout.TotalWidth, layout.TotalHeight);

            FillSolid(buffer, 255);

            int quietPixels = layout.QuietZonePixels;
            int moduleWidth = layout.ModuleWidth;

            for (int m = 0; m < modules.Length; m++)
            {
                if (!modules[m]) continue;
                int x0 = quietPixels + (m * moduleWidth);
                DrawRect(buffer, x0, layout.BarStartY, moduleWidth, layout.BarHeight, 0);
            }

            foreach (var span in layout.TextSpans)
            {
                DrawString(buffer, span.Text, span.X, span.Y, span.Scale, 0);
            }

            return buffer;
        }

        /// <summary>
        /// Universal barcode rendering dispatcher supporting multiple 1D symbologies.
        /// </summary>
        public static ImageBuffer Render(BarcodeSymbology symbology, string payload, string? hriText = null, HriOptions? options = null)
        {
            switch (symbology)
            {
                case BarcodeSymbology.Ean13:
                case BarcodeSymbology.UpcA:
                    return RenderEan13(payload, options);
                case BarcodeSymbology.Code128:
                    return RenderCode128(payload, options, isGs1: false);
                default:
                    return RenderCode128(payload, options, isGs1: false);
            }
        }

        #region Raster Drawing Primitives
        private static void FillSolid(ImageBuffer buffer, byte gray)
        {
            byte* scan = buffer.Scan0;
            int stride = buffer.Stride;
            int h = buffer.Height;
            int w = buffer.Width;

            for (int y = 0; y < h; y++)
            {
                byte* row = scan + (y * stride);
                for (int x = 0; x < w; x++)
                {
                    row[x] = gray;
                }
            }
        }

        private static void DrawRect(ImageBuffer buffer, int x, int y, int width, int height, byte gray)
        {
            int maxX = Math.Min(buffer.Width, x + width);
            int maxY = Math.Min(buffer.Height, y + height);
            int minX = Math.Max(0, x);
            int minY = Math.Max(0, y);

            byte* scan = buffer.Scan0;
            int stride = buffer.Stride;

            for (int py = minY; py < maxY; py++)
            {
                byte* row = scan + (py * stride);
                for (int px = minX; px < maxX; px++)
                {
                    row[px] = gray;
                }
            }
        }

        private static void DrawString(ImageBuffer buffer, string text, int startX, int startY, int scale, byte gray)
        {
            int cursorX = startX;
            int charSpacing = 1 * scale;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                DrawChar(buffer, c, cursorX, startY, scale, gray);
                cursorX += (HriLayoutEngine.BaseGlyphWidth * scale) + charSpacing;
            }
        }

        private static void DrawChar(ImageBuffer buffer, char c, int x, int y, int scale, byte gray)
        {
            int index = (int)c;
            if (index < 32 || index >= Font5x7.Length)
                index = 32; // Default to space

            byte[] rows = Font5x7[index];
            for (int r = 0; r < 7; r++)
            {
                byte rowBits = rows[r];
                for (int col = 0; col < 5; col++)
                {
                    // Bit 4 is leftmost column, bit 0 is rightmost column
                    bool isSet = (rowBits & (1 << (4 - col))) != 0;
                    if (isSet)
                    {
                        DrawRect(buffer, x + (col * scale), y + (r * scale), scale, scale, gray);
                    }
                }
            }
        }
        #endregion

        #region Embedded 5x7 Glyph Table
        private static byte[][] InitializeFont()
        {
            // ASCII 32 to 126 glyph bitmaps (7 rows per glyph, 5 bits per row)
            byte[][] font = new byte[128][];
            for (int i = 0; i < font.Length; i++) font[i] = new byte[7];

            // Space (32)
            font[32] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            // ! (33)
            font[33] = new byte[] { 0x04, 0x04, 0x04, 0x04, 0x00, 0x00, 0x04 };
            // ( (40)
            font[40] = new byte[] { 0x02, 0x04, 0x08, 0x08, 0x08, 0x04, 0x02 };
            // ) (41)
            font[41] = new byte[] { 0x08, 0x04, 0x02, 0x02, 0x02, 0x04, 0x08 };
            // + (43)
            font[43] = new byte[] { 0x00, 0x04, 0x04, 0x1F, 0x04, 0x04, 0x00 };
            // , (44)
            font[44] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x04, 0x04, 0x08 };
            // - (45)
            font[45] = new byte[] { 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00 };
            // . (46)
            font[46] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x04 };
            // / (47)
            font[47] = new byte[] { 0x01, 0x02, 0x04, 0x08, 0x10, 0x00, 0x00 };

            // Digits 0..9 (48..57)
            font[48] = new byte[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E }; // 0
            font[49] = new byte[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E }; // 1
            font[50] = new byte[] { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F }; // 2
            font[51] = new byte[] { 0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E }; // 3
            font[52] = new byte[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 }; // 4
            font[53] = new byte[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E }; // 5
            font[54] = new byte[] { 0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E }; // 6
            font[55] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 }; // 7
            font[56] = new byte[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E }; // 8
            font[57] = new byte[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C }; // 9

            // : (58)
            font[58] = new byte[] { 0x00, 0x04, 0x04, 0x00, 0x04, 0x04, 0x00 };

            // Uppercase A..Z (65..90)
            font[65] = new byte[] { 0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 }; // A
            font[66] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E }; // B
            font[67] = new byte[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E }; // C
            font[68] = new byte[] { 0x1C, 0x12, 0x11, 0x11, 0x11, 0x12, 0x1C }; // D
            font[69] = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F }; // E
            font[70] = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10 }; // F
            font[71] = new byte[] { 0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F }; // G
            font[72] = new byte[] { 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 }; // H
            font[73] = new byte[] { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E }; // I
            font[74] = new byte[] { 0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C }; // J
            font[75] = new byte[] { 0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11 }; // K
            font[76] = new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F }; // L
            font[77] = new byte[] { 0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11 }; // M
            font[78] = new byte[] { 0x11, 0x11, 0x19, 0x15, 0x13, 0x11, 0x11 }; // N
            font[79] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E }; // O
            font[80] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 }; // P
            font[81] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D }; // Q
            font[82] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11 }; // R
            font[83] = new byte[] { 0x0E, 0x11, 0x10, 0x0E, 0x01, 0x11, 0x0E }; // S
            font[84] = new byte[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 }; // T
            font[85] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E }; // U
            font[86] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04 }; // V
            font[87] = new byte[] { 0x11, 0x11, 0x11, 0x15, 0x15, 0x15, 0x0A }; // W
            font[88] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11 }; // X
            font[89] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04 }; // Y
            font[90] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F }; // Z

            // Mirror lowercase to uppercase
            for (int c = 'a'; c <= 'z'; c++)
            {
                font[c] = font[c - 32];
            }

            return font;
        }
        #endregion
    }
}
