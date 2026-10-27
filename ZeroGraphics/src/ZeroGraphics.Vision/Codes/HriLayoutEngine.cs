using System;
using System.Collections.Generic;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// Placement position of the Human Readable Interpretation (HRI) text relative to the barcode.
    /// </summary>
    public enum HriPosition
    {
        None = 0,
        Below = 1,
        Above = 2,
        Both = 3
    }

    /// <summary>
    /// Horizontal alignment of the HRI text.
    /// </summary>
    public enum HriAlignment
    {
        Center = 0,
        Left = 1,
        Right = 2,
        EanGrouped = 3 // EAN-13 lead digit + 6-digit left group + 6-digit right group
    }

    /// <summary>
    /// Options controlling the geometry and typography layout of a barcode symbol with HRI.
    /// </summary>
    public sealed class HriOptions
    {
        public HriPosition Position { get; set; } = HriPosition.Below;
        public HriAlignment Alignment { get; set; } = HriAlignment.Center;
        public int ModuleWidth { get; set; } = 2; // Pixels per module
        public int BarHeight { get; set; } = 60; // Height of bars in pixels
        public int QuietZoneModules { get; set; } = 10; // Number of quiet zone modules on left and right
        public int TextPadding { get; set; } = 4; // Vertical spacing between bars and text in pixels
        public int FontScale { get; set; } = 2; // Multiplier for built-in 5x7 glyphs
        public bool ProtrudeGuardBars { get; set; } = true; // For EAN-13: extend guard bars downward
        public int GuardBarExtension { get; set; } = 6; // Additional height for guard bars
    }

    /// <summary>
    /// Represents a discrete text run positioned within the symbol layout.
    /// </summary>
    public readonly struct HriTextSpan
    {
        public readonly string Text;
        public readonly int X;
        public readonly int Y;
        public readonly int Scale;

        public HriTextSpan(string text, int x, int y, int scale)
        {
            Text = text;
            X = x;
            Y = y;
            Scale = scale;
        }
    }

    /// <summary>
    /// Calculated spatial layout metrics for composite barcode and HRI rendering.
    /// </summary>
    public sealed class HriLayoutMetrics
    {
        public int TotalWidth { get; }
        public int TotalHeight { get; }
        public int BarStartY { get; }
        public int BarHeight { get; }
        public int QuietZonePixels { get; }
        public int ModuleWidth { get; }
        public IReadOnlyList<HriTextSpan> TextSpans { get; }

        public HriLayoutMetrics(
            int totalWidth,
            int totalHeight,
            int barStartY,
            int barHeight,
            int quietZonePixels,
            int moduleWidth,
            IReadOnlyList<HriTextSpan> textSpans)
        {
            TotalWidth = totalWidth;
            TotalHeight = totalHeight;
            BarStartY = barStartY;
            BarHeight = barHeight;
            QuietZonePixels = quietZonePixels;
            ModuleWidth = moduleWidth;
            TextSpans = textSpans;
        }
    }

    /// <summary>
    /// Pure C# geometric layout engine for computing pixel-perfect barcode and HRI placement.
    /// </summary>
    public static class HriLayoutEngine
    {
        // 5x7 font dimensions
        public const int BaseGlyphWidth = 5;
        public const int BaseGlyphHeight = 7;
        public const int BaseCharSpacing = 1;

        /// <summary>
        /// Computes exact layout coordinates and bounding boxes for standard 1D barcodes and EAN-13.
        /// </summary>
        public static HriLayoutMetrics ComputeLayout(
            BarcodeSymbology symbology,
            int moduleCount,
            string hriText,
            HriOptions options)
        {
            int moduleWidth = Math.Max(1, options.ModuleWidth);
            int quietPixels = options.QuietZoneModules * moduleWidth;
            int barcodeWidth = moduleCount * moduleWidth;
            int totalWidth = barcodeWidth + (quietPixels * 2);

            int fontScale = Math.Max(1, options.FontScale);
            int textHeight = BaseGlyphHeight * fontScale;
            int charWidth = (BaseGlyphWidth + BaseCharSpacing) * fontScale;

            int barStartY = 4; // Top padding
            int barHeight = Math.Max(20, options.BarHeight);
            int totalHeight = barStartY + barHeight;

            var textSpans = new List<HriTextSpan>();

            if (options.Position == HriPosition.None || string.IsNullOrEmpty(hriText))
            {
                totalHeight += 4;
                return new HriLayoutMetrics(totalWidth, totalHeight, barStartY, barHeight, quietPixels, moduleWidth, textSpans);
            }

            int textY = barStartY + barHeight + options.TextPadding;

            if (symbology == BarcodeSymbology.Ean13 && options.Alignment == HriAlignment.EanGrouped && hriText.Length == 13)
            {
                // EAN-13 Grouped Layout:
                // Lead digit (digit 0) placed in left quiet zone:
                string leadDigit = hriText.Substring(0, 1);
                int leadX = Math.Max(2, quietPixels - (charWidth + 2));
                textSpans.Add(new HriTextSpan(leadDigit, leadX, textY, fontScale));

                // Left 6 digits (digits 1..6) centered under left data section (modules 3..44)
                string leftGroup = hriText.Substring(1, 6);
                int leftSectionStartX = quietPixels + (3 * moduleWidth);
                int leftSectionWidth = 42 * moduleWidth;
                int leftTextWidth = leftGroup.Length * charWidth;
                int leftX = leftSectionStartX + ((leftSectionWidth - leftTextWidth) / 2);
                textSpans.Add(new HriTextSpan(leftGroup, leftX, textY, fontScale));

                // Right 6 digits (digits 7..12) centered under right data section (modules 50..91)
                string rightGroup = hriText.Substring(7, 6);
                int rightSectionStartX = quietPixels + (50 * moduleWidth);
                int rightSectionWidth = 42 * moduleWidth;
                int rightTextWidth = rightGroup.Length * charWidth;
                int rightX = rightSectionStartX + ((rightSectionWidth - rightTextWidth) / 2);
                textSpans.Add(new HriTextSpan(rightGroup, rightX, textY, fontScale));
            }
            else
            {
                // General 1D centered / left / right layout
                int textWidth = hriText.Length * charWidth;
                int textX;

                switch (options.Alignment)
                {
                    case HriAlignment.Left:
                        textX = quietPixels;
                        break;
                    case HriAlignment.Right:
                        textX = quietPixels + barcodeWidth - textWidth;
                        break;
                    default: // Center
                        textX = (totalWidth - textWidth) / 2;
                        break;
                }

                textSpans.Add(new HriTextSpan(hriText, Math.Max(0, textX), textY, fontScale));
            }

            totalHeight = textY + textHeight + 6; // Bottom margin

            return new HriLayoutMetrics(totalWidth, totalHeight, barStartY, barHeight, quietPixels, moduleWidth, textSpans);
        }
    }
}
