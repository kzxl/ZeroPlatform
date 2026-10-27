using System;
using System.Collections.Generic;
using System.Text;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// ISO/IEC 18004 QR Code symbol encoder and renderer.
    /// Supports Numeric, Alphanumeric, and Byte modes, Reed-Solomon parity generation,
    /// standard function pattern drawing, bit-interleaving, and masking.
    /// </summary>
    public static class QrEncoder
    {
        private static readonly char[] AlphanumericTable = new char[]
        {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
            'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
            'U', 'V', 'W', 'X', 'Y', 'Z',
            ' ', '$', '%', '*', '+', '-', '.', '/', ':'
        };

        private sealed class BitWriter
        {
            private readonly List<byte> _bytes = new List<byte>();
            private int _currentByte;
            private int _bitOffset;

            public int TotalBits => _bytes.Count * 8 + _bitOffset;

            public void WriteBits(int value, int numBits)
            {
                for (int i = numBits - 1; i >= 0; i--)
                {
                    int bit = (value >> i) & 1;
                    _currentByte = (_currentByte << 1) | bit;
                    _bitOffset++;
                    if (_bitOffset == 8)
                    {
                        _bytes.Add((byte)_currentByte);
                        _currentByte = 0;
                        _bitOffset = 0;
                    }
                }
            }

            public byte[] ToByteArray(int targetByteCount)
            {
                // Pad to byte boundary
                if (_bitOffset > 0)
                {
                    _currentByte <<= (8 - _bitOffset);
                    _bytes.Add((byte)_currentByte);
                    _bitOffset = 0;
                    _currentByte = 0;
                }

                // Add ISO 18004 pad bytes 0xEC and 0x11
                byte[] padBytes = new byte[] { 0xEC, 0x11 };
                int padIdx = 0;
                while (_bytes.Count < targetByteCount)
                {
                    _bytes.Add(padBytes[padIdx]);
                    padIdx = (padIdx + 1) % 2;
                }

                return _bytes.ToArray();
            }
        }

        /// <summary>
        /// Encodes text into a complete QR Code boolean symbol grid.
        /// </summary>
        public static bool[,] EncodeSymbol(
            string text,
            QrErrorCorrectionLevel ecLevel = QrErrorCorrectionLevel.M,
            int maskPattern = 0,
            QrVersion? forcedVersion = null)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            var mode = DetectMode(text);
            var version = forcedVersion ?? FindFittingVersion(text, mode, ecLevel);
            if (version == null)
                throw new InvalidOperationException("Payload exceeds capacity of supported QR versions.");

            var ecBlocks = version.GetECBlocks(ecLevel);
            int totalDataCodewords = ecBlocks.TotalDataCodewords;

            // 1. Build payload bitstream
            var writer = new BitWriter();
            writer.WriteBits((int)mode, 4);

            int countBits = GetCountBits(mode, version.VersionNumber);
            writer.WriteBits(text.Length, countBits);

            EncodeData(text, mode, writer);

            // Terminator
            int remainingBits = (totalDataCodewords * 8) - writer.TotalBits;
            int terminatorBits = Math.Min(4, Math.Max(0, remainingBits));
            writer.WriteBits(0, terminatorBits);

            byte[] dataCodewords = writer.ToByteArray(totalDataCodewords);

            // 2. Generate Reed-Solomon parity blocks and interleave
            byte[] rawCodewords = InterleaveCodewords(dataCodewords, version, ecLevel);

            // 3. Construct function pattern mask and canvas
            int dim = version.Dimension;
            bool[,] grid = new bool[dim, dim];
            bool[,] funcPattern = QrBitParser.BuildFunctionPattern(version);

            // Draw finders, separators, timing, alignment, format info
            DrawFunctionPatterns(grid, version, ecLevel, maskPattern);

            // 4. Place data bits and apply mask
            PlaceDataBits(grid, rawCodewords, version, funcPattern, maskPattern);

            return grid;
        }

        /// <summary>
        /// Renders a QR code grid into a Gray8 ImageBuffer with quiet zone.
        /// </summary>
        public static unsafe ImageBuffer RenderToImage(bool[,] grid, int modulePixelSize = 8, int quietZone = 4)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (modulePixelSize <= 0) modulePixelSize = 8;
            if (quietZone < 0) quietZone = 4;

            int dim = grid.GetLength(0);
            int imgSize = (dim + quietZone * 2) * modulePixelSize;

            var image = ImageBuffer.CreateGray8(imgSize, imgSize);
            byte* scan0 = image.Scan0;
            int stride = image.Stride;

            // Fill background white (255)
            for (int y = 0; y < imgSize; y++)
            {
                byte* row = scan0 + y * stride;
                for (int x = 0; x < imgSize; x++)
                {
                    row[x] = 255;
                }
            }

            // Draw black modules (0)
            for (int r = 0; r < dim; r++)
            {
                int startY = (r + quietZone) * modulePixelSize;
                for (int c = 0; c < dim; c++)
                {
                    if (grid[r, c])
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

        private static QrMode DetectMode(string text)
        {
            bool allDigits = true;
            bool allAlpha = true;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (!char.IsDigit(c)) allDigits = false;
                if (Array.IndexOf(AlphanumericTable, c) < 0) allAlpha = false;
            }

            if (allDigits) return QrMode.Numeric;
            if (allAlpha) return QrMode.Alphanumeric;
            return QrMode.Byte;
        }

        private static int GetCountBits(QrMode mode, int versionNumber)
        {
            switch (mode)
            {
                case QrMode.Numeric: return versionNumber <= 9 ? 10 : versionNumber <= 26 ? 12 : 14;
                case QrMode.Alphanumeric: return versionNumber <= 9 ? 9 : versionNumber <= 26 ? 11 : 13;
                case QrMode.Byte: return versionNumber <= 9 ? 8 : 16;
                case QrMode.Kanji: return versionNumber <= 9 ? 8 : versionNumber <= 26 ? 10 : 12;
                default: return 8;
            }
        }

        private static void EncodeData(string text, QrMode mode, BitWriter writer)
        {
            if (mode == QrMode.Numeric)
            {
                int i = 0;
                while (i + 2 < text.Length)
                {
                    int val = (text[i] - '0') * 100 + (text[i + 1] - '0') * 10 + (text[i + 2] - '0');
                    writer.WriteBits(val, 10);
                    i += 3;
                }
                if (i + 1 < text.Length)
                {
                    int val = (text[i] - '0') * 10 + (text[i + 1] - '0');
                    writer.WriteBits(val, 7);
                }
                else if (i < text.Length)
                {
                    int val = text[i] - '0';
                    writer.WriteBits(val, 4);
                }
            }
            else if (mode == QrMode.Alphanumeric)
            {
                int i = 0;
                while (i + 1 < text.Length)
                {
                    int c1 = Array.IndexOf(AlphanumericTable, text[i]);
                    int c2 = Array.IndexOf(AlphanumericTable, text[i + 1]);
                    writer.WriteBits(c1 * 45 + c2, 11);
                    i += 2;
                }
                if (i < text.Length)
                {
                    int c = Array.IndexOf(AlphanumericTable, text[i]);
                    writer.WriteBits(c, 6);
                }
            }
            else
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                foreach (byte b in bytes)
                {
                    writer.WriteBits(b, 8);
                }
            }
        }

        private static QrVersion? FindFittingVersion(string text, QrMode mode, QrErrorCorrectionLevel ecLevel)
        {
            foreach (var v in QrVersion.AllVersions)
            {
                var ecBlocks = v.GetECBlocks(ecLevel);
                int countBits = GetCountBits(mode, v.VersionNumber);
                int dataBits = 4 + countBits;

                if (mode == QrMode.Numeric)
                    dataBits += (text.Length / 3) * 10 + ((text.Length % 3) == 2 ? 7 : (text.Length % 3) == 1 ? 4 : 0);
                else if (mode == QrMode.Alphanumeric)
                    dataBits += (text.Length / 2) * 11 + ((text.Length % 2) == 1 ? 6 : 0);
                else
                    dataBits += Encoding.UTF8.GetByteCount(text) * 8;

                int requiredBytes = (dataBits + 4 + 7) / 8;
                if (ecBlocks.TotalDataCodewords >= requiredBytes)
                    return v;
            }
            return null;
        }

        private static byte[] InterleaveCodewords(byte[] dataCodewords, QrVersion version, QrErrorCorrectionLevel ecLevel)
        {
            var ecBlocks = version.GetECBlocks(ecLevel);
            var blocks = new List<byte[]>();
            var ecList = new List<byte[]>();

            int offset = 0;
            foreach (var b in ecBlocks.Blocks)
            {
                for (int i = 0; i < b.Count; i++)
                {
                    int numData = b.DataCodewords;
                    int numEc = ecBlocks.ECCodewordsPerBlock;

                    int[] blockInts = new int[numData + numEc];
                    byte[] dataBlock = new byte[numData];
                    for (int k = 0; k < numData; k++)
                    {
                        blockInts[k] = dataCodewords[offset];
                        dataBlock[k] = dataCodewords[offset++];
                    }

                    ReedSolomonEncoder.Encode(GenericGF.QrCode256, blockInts, numEc);

                    byte[] ecBlock = new byte[numEc];
                    for (int k = 0; k < numEc; k++)
                    {
                        ecBlock[k] = (byte)blockInts[numData + k];
                    }

                    blocks.Add(dataBlock);
                    ecList.Add(ecBlock);
                }
            }

            byte[] result = new byte[version.TotalCodewords];
            int resIdx = 0;

            // Interleave data
            int maxData = 0;
            foreach (var blk in blocks) maxData = Math.Max(maxData, blk.Length);

            for (int i = 0; i < maxData; i++)
            {
                for (int j = 0; j < blocks.Count; j++)
                {
                    if (i < blocks[j].Length)
                    {
                        result[resIdx++] = blocks[j][i];
                    }
                }
            }

            // Interleave EC
            int ecLen = ecBlocks.ECCodewordsPerBlock;
            for (int i = 0; i < ecLen; i++)
            {
                for (int j = 0; j < ecList.Count; j++)
                {
                    result[resIdx++] = ecList[j][i];
                }
            }

            return result;
        }

        private static void DrawFunctionPatterns(bool[,] grid, QrVersion version, QrErrorCorrectionLevel ecLevel, int maskPattern)
        {
            int dim = version.Dimension;

            // 1. Draw 3 Finder Patterns
            DrawFinder(grid, 0, 0);
            DrawFinder(grid, 0, dim - 7);
            DrawFinder(grid, dim - 7, 0);

            // 2. Draw Timing Patterns (alternating black/white)
            for (int i = 8; i < dim - 8; i++)
            {
                bool bit = (i & 1) == 0;
                grid[6, i] = bit;
                grid[i, 6] = bit;
            }

            // 3. Draw Alignment Patterns
            int[] centers = version.AlignmentPatternCenters;
            for (int i = 0; i < centers.Length; i++)
            {
                for (int j = 0; j < centers.Length; j++)
                {
                    int ar = centers[i];
                    int ac = centers[j];
                    if ((ar <= 8 && ac <= 8) || (ar <= 8 && ac >= dim - 8) || (ar >= dim - 8 && ac <= 8))
                        continue;
                    DrawAlignment(grid, ar, ac);
                }
            }

            // 4. Draw Dark Module at (4 * V + 9, 8)
            grid[4 * version.VersionNumber + 9, 8] = true;

            // 5. Draw Format Information
            int formatInfo = QrFormatInfo.EncodeFormatInformation(ecLevel, maskPattern);
            DrawFormatInfo(grid, formatInfo, dim);
        }

        private static void DrawFinder(bool[,] grid, int startR, int startC)
        {
            for (int r = 0; r < 7; r++)
            {
                for (int c = 0; c < 7; c++)
                {
                    bool isBlack = (r == 0 || r == 6 || c == 0 || c == 6 || (r >= 2 && r <= 4 && c >= 2 && c <= 4));
                    grid[startR + r, startC + c] = isBlack;
                }
            }
        }

        private static void DrawAlignment(bool[,] grid, int centerR, int centerC)
        {
            for (int dr = -2; dr <= 2; dr++)
            {
                for (int dc = -2; dc <= 2; dc++)
                {
                    bool isBlack = (Math.Abs(dr) == 2 || Math.Abs(dc) == 2 || (dr == 0 && dc == 0));
                    grid[centerR + dr, centerC + dc] = isBlack;
                }
            }
        }

        private static void DrawFormatInfo(bool[,] grid, int formatInfo, int dim)
        {
            // Position 1: Around Top-Left
            int[] r1 = { 8, 8, 8, 8, 8, 8, 8, 8, 7, 5, 4, 3, 2, 1, 0 };
            int[] c1 = { 0, 1, 2, 3, 4, 5, 7, 8, 8, 8, 8, 8, 8, 8, 8 };

            for (int i = 0; i < 15; i++)
            {
                bool bit = ((formatInfo >> i) & 1) == 1;
                grid[r1[i], c1[i]] = bit;
            }

            // Position 2: Bottom-Left and Top-Right
            int[] r2 = { 8, 8, 8, 8, 8, 8, 8, 8, dim - 7, dim - 6, dim - 5, dim - 4, dim - 3, dim - 2, dim - 1 };
            int[] c2 = { dim - 1, dim - 2, dim - 3, dim - 4, dim - 5, dim - 6, dim - 7, dim - 8, 8, 8, 8, 8, 8, 8, 8 };

            for (int i = 0; i < 15; i++)
            {
                bool bit = ((formatInfo >> i) & 1) == 1;
                grid[r2[i], c2[i]] = bit;
            }
        }

        private static void PlaceDataBits(bool[,] grid, byte[] codewords, QrVersion version, bool[,] funcPattern, int mask)
        {
            int dim = version.Dimension;
            int byteIdx = 0;
            int bitIdx = 7;
            bool readingUp = true;

            for (int j = dim - 1; j > 0; j -= 2)
            {
                if (j == 6) j--;

                for (int count = 0; count < dim; count++)
                {
                    int r = readingUp ? dim - 1 - count : count;
                    for (int col = 0; col < 2; col++)
                    {
                        int c = j - col;
                        if (!funcPattern[r, c])
                        {
                            bool bit = false;
                            if (byteIdx < codewords.Length)
                            {
                                bit = ((codewords[byteIdx] >> bitIdx) & 1) == 1;
                                bitIdx--;
                                if (bitIdx < 0)
                                {
                                    bitIdx = 7;
                                    byteIdx++;
                                }
                            }

                            // Apply mask
                            if (QrBitParser.IsMasked(mask, r, c))
                            {
                                bit = !bit;
                            }

                            grid[r, c] = bit;
                        }
                    }
                }

                readingUp = !readingUp;
            }
        }
    }
}
