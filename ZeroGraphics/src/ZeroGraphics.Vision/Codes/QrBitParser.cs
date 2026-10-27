using System;
using System.Collections.Generic;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// ISO/IEC 18004 QR Code bit matrix parsing, function pattern masking, and codeword extraction engine.
    /// </summary>
    public static class QrBitParser
    {
        /// <summary>
        /// Builds a boolean mask indicating all function pattern modules (finders, separators, timing, alignments, format info).
        /// </summary>
        public static bool[,] BuildFunctionPattern(QrVersion version)
        {
            int dim = version.Dimension;
            bool[,] func = new bool[dim, dim];

            // 1. Top-Left Finder + Separator (9x9)
            for (int r = 0; r <= 8 && r < dim; r++)
                for (int c = 0; c <= 8 && c < dim; c++)
                    func[r, c] = true;

            // 2. Top-Right Finder + Separator (9x9)
            for (int r = 0; r <= 8 && r < dim; r++)
                for (int c = dim - 8; c < dim; c++)
                    func[r, c] = true;

            // 3. Bottom-Left Finder + Separator (9x9)
            for (int r = dim - 8; r < dim; r++)
                for (int c = 0; c <= 8 && c < dim; c++)
                    func[r, c] = true;

            // 4. Timing tracks
            for (int i = 8; i < dim - 8; i++)
            {
                func[6, i] = true; // Horizontal
                func[i, 6] = true; // Vertical
            }

            // 5. Alignment Patterns
            int[] centers = version.AlignmentPatternCenters;
            for (int i = 0; i < centers.Length; i++)
            {
                for (int j = 0; j < centers.Length; j++)
                {
                    int ar = centers[i];
                    int ac = centers[j];

                    // Skip if inside any of the 3 finders
                    if ((ar <= 8 && ac <= 8) ||
                        (ar <= 8 && ac >= dim - 8) ||
                        (ar >= dim - 8 && ac <= 8))
                    {
                        continue;
                    }

                    // 5x5 alignment box
                    for (int dr = -2; dr <= 2; dr++)
                    {
                        for (int dc = -2; dc <= 2; dc++)
                        {
                            int r = ar + dr;
                            int c = ac + dc;
                            if (r >= 0 && r < dim && c >= 0 && c < dim)
                            {
                                func[r, c] = true;
                            }
                        }
                    }
                }
            }

            // 6. Format info coordinates
            for (int i = 0; i <= 8; i++)
            {
                func[8, i] = true;
                func[i, 8] = true;
            }
            for (int i = dim - 8; i < dim; i++)
            {
                func[8, i] = true;
                func[i, 8] = true;
            }

            // 7. Version info for Version >= 7 (6x3 rects)
            if (version.VersionNumber >= 7)
            {
                for (int r = 0; r < 6; r++)
                    for (int c = dim - 11; c < dim - 8; c++)
                        func[r, c] = true;

                for (int r = dim - 11; r < dim - 8; r++)
                    for (int c = 0; c < 6; c++)
                        func[r, c] = true;
            }

            return func;
        }

        /// <summary>
        /// Unmasks the data modules in-place or returns an unmasked copy.
        /// </summary>
        public static bool[,] ApplyMask(bool[,] grid, int maskPattern, bool[,] functionPattern)
        {
            int dim = grid.GetLength(0);
            bool[,] unmasked = new bool[dim, dim];

            for (int r = 0; r < dim; r++)
            {
                for (int c = 0; c < dim; c++)
                {
                    bool val = grid[r, c];
                    if (!functionPattern[r, c] && IsMasked(maskPattern, r, c))
                    {
                        val = !val;
                    }
                    unmasked[r, c] = val;
                }
            }

            return unmasked;
        }

        public static bool IsMasked(int mask, int r, int c)
        {
            switch (mask)
            {
                case 0: return ((r + c) & 1) == 0;
                case 1: return (r & 1) == 0;
                case 2: return c % 3 == 0;
                case 3: return (r + c) % 3 == 0;
                case 4: return (((r / 2) + (c / 3)) & 1) == 0;
                case 5: return ((r * c) % 2) + ((r * c) % 3) == 0;
                case 6: return ((((r * c) % 2) + ((r * c) % 3)) & 1) == 0;
                case 7: return ((((r + c) % 2) + ((r * c) % 3)) & 1) == 0;
                default: return false;
            }
        }

        /// <summary>
        /// Reads codeword bytes from the unmasked grid via 2-column snake traversal.
        /// </summary>
        public static byte[] ReadCodewords(bool[,] unmaskedGrid, QrVersion version, bool[,] functionPattern)
        {
            int dim = version.Dimension;
            byte[] result = new byte[version.TotalCodewords];
            int resultOffset = 0;

            bool readingUp = true;
            int currentByte = 0;
            int bitsRead = 0;

            for (int j = dim - 1; j > 0; j -= 2)
            {
                if (j == 6)
                {
                    // Skip vertical timing track
                    j--;
                }

                for (int count = 0; count < dim; count++)
                {
                    int r = readingUp ? dim - 1 - count : count;
                    for (int col = 0; col < 2; col++)
                    {
                        int c = j - col;
                        if (!functionPattern[r, c])
                        {
                            currentByte <<= 1;
                            if (unmaskedGrid[r, c])
                            {
                                currentByte |= 1;
                            }
                            bitsRead++;

                            if (bitsRead == 8)
                            {
                                if (resultOffset < result.Length)
                                {
                                    result[resultOffset++] = (byte)currentByte;
                                }
                                bitsRead = 0;
                                currentByte = 0;
                            }
                        }
                    }
                }

                readingUp = !readingUp;
            }

            return result;
        }

        /// <summary>
        /// Represents a de-interleaved data block with data codewords and error correction codewords.
        /// </summary>
        public sealed class QrDataBlock
        {
            public int NumDataCodewords { get; }
            public byte[] Codewords { get; }

            public QrDataBlock(int numDataCodewords, byte[] codewords)
            {
                NumDataCodewords = numDataCodewords;
                Codewords = codewords;
            }
        }

        /// <summary>
        /// De-interleaves raw codewords into individual Reed-Solomon data blocks.
        /// </summary>
        public static QrDataBlock[] Deinterleave(byte[] rawCodewords, QrVersion version, QrErrorCorrectionLevel ecLevel)
        {
            var ecBlocks = version.GetECBlocks(ecLevel);

            int totalBlocks = 0;
            foreach (var b in ecBlocks.Blocks)
            {
                totalBlocks += b.Count;
            }

            var result = new QrDataBlock[totalBlocks];
            int blockIdx = 0;
            foreach (var ecb in ecBlocks.Blocks)
            {
                for (int i = 0; i < ecb.Count; i++)
                {
                    int numData = ecb.DataCodewords;
                    int numTotal = ecBlocks.ECCodewordsPerBlock + numData;
                    result[blockIdx++] = new QrDataBlock(numData, new byte[numTotal]);
                }
            }

            // Find where longer blocks start
            int shorterBlocksTotalCodewords = result[0].Codewords.Length;
            int longerBlocksStartAt = result.Length - 1;
            while (longerBlocksStartAt >= 0)
            {
                if (result[longerBlocksStartAt].Codewords.Length == shorterBlocksTotalCodewords)
                    break;
                longerBlocksStartAt--;
            }
            longerBlocksStartAt++;

            int shorterBlocksNumDataCodewords = shorterBlocksTotalCodewords - ecBlocks.ECCodewordsPerBlock;
            int rawOffset = 0;

            // 1. Interleaved data codewords
            for (int i = 0; i < shorterBlocksNumDataCodewords; i++)
            {
                for (int j = 0; j < totalBlocks; j++)
                {
                    if (rawOffset < rawCodewords.Length)
                        result[j].Codewords[i] = rawCodewords[rawOffset++];
                }
            }

            // 2. Extra data byte in longer blocks
            for (int j = longerBlocksStartAt; j < totalBlocks; j++)
            {
                if (rawOffset < rawCodewords.Length)
                    result[j].Codewords[shorterBlocksNumDataCodewords] = rawCodewords[rawOffset++];
            }

            // 3. Interleaved error correction codewords
            int maxTotal = result[0].Codewords.Length;
            for (int i = shorterBlocksNumDataCodewords; i < maxTotal; i++)
            {
                for (int j = 0; j < totalBlocks; j++)
                {
                    int iOffset = (j < longerBlocksStartAt) ? i : i + 1;
                    if (rawOffset < rawCodewords.Length && iOffset < result[j].Codewords.Length)
                        result[j].Codewords[iOffset] = rawCodewords[rawOffset++];
                }
            }

            return result;
        }
    }
}
