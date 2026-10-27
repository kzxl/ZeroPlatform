using System;
using System.Collections.Generic;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// ISO/IEC 16022 DataMatrix ECC200 encoder and symbol renderer.
    /// Generates valid data codewords, Reed-Solomon parity, Utah bit-placement, and full symbol grids.
    /// </summary>
    public static class DataMatrixEncoder
    {
        /// <summary>
        /// Encodes ASCII text into a full DataMatrix ECC200 symbol boolean grid.
        /// </summary>
        public static bool[,] EncodeSymbol(string text, DataMatrixVersion? targetVersion = null)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            var dataCodewords = EncodeAsciiPayload(text);

            DataMatrixVersion version;
            if (targetVersion != null)
            {
                if (targetVersion.DataCodewords < dataCodewords.Count)
                    throw new ArgumentException($"Payload requires {dataCodewords.Count} codewords, but selected version only holds {targetVersion.DataCodewords}.");
                version = targetVersion;
            }
            else
            {
                var fitted = FindFittingVersion(dataCodewords.Count);
                if (fitted == null)
                    throw new InvalidOperationException($"Payload length ({dataCodewords.Count} codewords) exceeds maximum supported DataMatrix ECC200 version.");
                version = fitted;
            }

            // Pad data codewords to version capacity
            PadCodewords(dataCodewords, version.DataCodewords);

            // Compute Reed-Solomon parity codewords
            int[] allCodewords = new int[version.TotalCodewords];
            for (int i = 0; i < version.DataCodewords; i++)
            {
                allCodewords[i] = dataCodewords[i];
            }

            ReedSolomonEncoder.Encode(GenericGF.DataMatrix256, allCodewords, version.ErrorCodewords);

            byte[] codewordBytes = new byte[version.TotalCodewords];
            for (int i = 0; i < version.TotalCodewords; i++)
            {
                codewordBytes[i] = (byte)allCodewords[i];
            }

            // Place codewords into data grid
            bool[,] dataGrid = new bool[version.DataRows, version.DataColumns];
            DataMatrixBitParser.PlaceCodewords(codewordBytes, dataGrid);

            // Assemble full symbol grid with L-finder and timing patterns
            return AssembleSymbol(dataGrid, version);
        }

        /// <summary>
        /// Renders a DataMatrix symbol grid into a Gray8 ImageBuffer with quiet zone.
        /// </summary>
        public static unsafe ImageBuffer RenderToImage(bool[,] symbolGrid, int modulePixelSize = 10, int quietZone = 2)
        {
            if (symbolGrid == null) throw new ArgumentNullException(nameof(symbolGrid));
            if (modulePixelSize <= 0) modulePixelSize = 10;
            if (quietZone < 0) quietZone = 0;

            int sRows = symbolGrid.GetLength(0);
            int sCols = symbolGrid.GetLength(1);

            int imgWidth = (sCols + quietZone * 2) * modulePixelSize;
            int imgHeight = (sRows + quietZone * 2) * modulePixelSize;

            var image = ImageBuffer.CreateGray8(imgWidth, imgHeight);
            byte* scan0 = image.Scan0;
            int stride = image.Stride;

            // Fill background with white (255)
            for (int y = 0; y < imgHeight; y++)
            {
                byte* row = scan0 + y * stride;
                for (int x = 0; x < imgWidth; x++)
                {
                    row[x] = 255;
                }
            }

            // Draw modules (black = 0)
            for (int r = 0; r < sRows; r++)
            {
                int startY = (r + quietZone) * modulePixelSize;
                for (int c = 0; c < sCols; c++)
                {
                    if (symbolGrid[r, c])
                    {
                        int startX = (c + quietZone) * modulePixelSize;
                        for (int dy = 0; dy < modulePixelSize; dy++)
                        {
                            byte* row = scan0 + (startY + dy) * stride;
                            for (int dx = 0; dx < modulePixelSize; dx++)
                            {
                                row[startX + dx] = 0;
                            }
                        }
                    }
                }
            }

            return image;
        }

        private static List<int> EncodeAsciiPayload(string text)
        {
            var codewords = new List<int>();
            int i = 0;
            while (i < text.Length)
            {
                // Check for 2 consecutive digits
                if (i + 1 < text.Length && char.IsDigit(text[i]) && char.IsDigit(text[i + 1]))
                {
                    int val = (text[i] - '0') * 10 + (text[i + 1] - '0');
                    codewords.Add(val + 130);
                    i += 2;
                }
                else
                {
                    int c = text[i];
                    if (c >= 0 && c <= 127)
                    {
                        codewords.Add(c + 1);
                    }
                    else
                    {
                        // Upper shift for Extended ASCII
                        codewords.Add(235);
                        codewords.Add((c - 128) + 1);
                    }
                    i++;
                }
            }
            return codewords;
        }

        private static void PadCodewords(List<int> codewords, int targetCount)
        {
            if (codewords.Count >= targetCount) return;

            // First pad byte is 129
            codewords.Add(129);

            // Subsequent pad bytes use 253-state pseudo-randomization
            int padPosition = codewords.Count;
            while (codewords.Count < targetCount)
            {
                int pseudoRandom = ((149 * padPosition) % 253) + 1;
                int temp = 129 + pseudoRandom;
                int padVal = temp <= 254 ? temp : temp - 254;
                codewords.Add(padVal);
                padPosition++;
            }
        }

        private static DataMatrixVersion? FindFittingVersion(int dataCodewordsCount)
        {
            foreach (var v in DataMatrixVersion.AllVersions)
            {
                if (v.DataCodewords >= dataCodewordsCount)
                    return v;
            }
            return null;
        }

        private static bool[,] AssembleSymbol(bool[,] dataGrid, DataMatrixVersion version)
        {
            int w = version.SymbolWidth;
            int h = version.SymbolHeight;
            bool[,] symbol = new bool[h, w];

            // 1. Left border: solid black (1)
            for (int r = 0; r < h; r++)
            {
                symbol[r, 0] = true;
            }

            // 2. Bottom border: solid black (1)
            for (int c = 0; c < w; c++)
            {
                symbol[h - 1, c] = true;
            }

            // 3. Top border: alternating (1, 0, 1, 0...)
            for (int c = 0; c < w; c++)
            {
                symbol[0, c] = (c % 2 == 0);
            }

            // 4. Right border: alternating starting from bottom right
            for (int r = 0; r < h; r++)
            {
                symbol[r, w - 1] = ((h - 1 - r) % 2 == 0);
            }

            // 5. Copy inner data region
            for (int r = 0; r < version.DataRows; r++)
            {
                for (int c = 0; c < version.DataColumns; c++)
                {
                    symbol[r + 1, c + 1] = dataGrid[r, c];
                }
            }

            return symbol;
        }
    }
}
