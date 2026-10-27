using System;
using System.Collections.Generic;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// Represents an Error Correction Block group in a QR Code version.
    /// </summary>
    public sealed class QrECBlock
    {
        public int Count { get; }
        public int DataCodewords { get; }

        public QrECBlock(int count, int dataCodewords)
        {
            Count = count;
            DataCodewords = dataCodewords;
        }
    }

    /// <summary>
    /// Represents the error correction parameters for a specific EC level in a QR Code version.
    /// </summary>
    public sealed class QrECBlocks
    {
        public int ECCodewordsPerBlock { get; }
        public QrECBlock[] Blocks { get; }

        public int TotalDataCodewords
        {
            get
            {
                int total = 0;
                foreach (var b in Blocks) total += b.Count * b.DataCodewords;
                return total;
            }
        }

        public QrECBlocks(int ecCodewordsPerBlock, params QrECBlock[] blocks)
        {
            ECCodewordsPerBlock = ecCodewordsPerBlock;
            Blocks = blocks ?? new QrECBlock[0];
        }
    }

    /// <summary>
    /// ISO/IEC 18004 QR Code Version attributes, dimensions, and error correction block capacities.
    /// </summary>
    public sealed class QrVersion
    {
        public int VersionNumber { get; }
        public int Dimension => 17 + 4 * VersionNumber;
        public int[] AlignmentPatternCenters { get; }
        private readonly Dictionary<QrErrorCorrectionLevel, QrECBlocks> _ecBlocks;

        public int TotalCodewords { get; }

        public QrVersion(
            int versionNumber,
            int[] alignmentPatternCenters,
            QrECBlocks ecL,
            QrECBlocks ecM,
            QrECBlocks ecQ,
            QrECBlocks ecH)
        {
            VersionNumber = versionNumber;
            AlignmentPatternCenters = alignmentPatternCenters ?? new int[0];
            _ecBlocks = new Dictionary<QrErrorCorrectionLevel, QrECBlocks>
            {
                [QrErrorCorrectionLevel.L] = ecL,
                [QrErrorCorrectionLevel.M] = ecM,
                [QrErrorCorrectionLevel.Q] = ecQ,
                [QrErrorCorrectionLevel.H] = ecH
            };

            int total = 0;
            int ecCodewords = ecL.ECCodewordsPerBlock;
            foreach (var b in ecL.Blocks)
            {
                total += b.Count * (b.DataCodewords + ecCodewords);
            }
            TotalCodewords = total;
        }

        public QrECBlocks GetECBlocks(QrErrorCorrectionLevel level) => _ecBlocks[level];

        public static readonly QrVersion[] AllVersions = new QrVersion[]
        {
            // Version 1 (21x21)
            new QrVersion(1, new int[0],
                new QrECBlocks(7,  new QrECBlock(1, 19)),
                new QrECBlocks(10, new QrECBlock(1, 16)),
                new QrECBlocks(13, new QrECBlock(1, 13)),
                new QrECBlocks(17, new QrECBlock(1, 9))),

            // Version 2 (25x25)
            new QrVersion(2, new int[] { 6, 18 },
                new QrECBlocks(10, new QrECBlock(1, 34)),
                new QrECBlocks(16, new QrECBlock(1, 28)),
                new QrECBlocks(22, new QrECBlock(1, 22)),
                new QrECBlocks(28, new QrECBlock(1, 16))),

            // Version 3 (29x29)
            new QrVersion(3, new int[] { 6, 22 },
                new QrECBlocks(15, new QrECBlock(1, 55)),
                new QrECBlocks(26, new QrECBlock(1, 44)),
                new QrECBlocks(18, new QrECBlock(2, 17)),
                new QrECBlocks(22, new QrECBlock(2, 13))),

            // Version 4 (33x33)
            new QrVersion(4, new int[] { 6, 26 },
                new QrECBlocks(20, new QrECBlock(1, 80)),
                new QrECBlocks(18, new QrECBlock(2, 32)),
                new QrECBlocks(26, new QrECBlock(2, 24)),
                new QrECBlocks(16, new QrECBlock(4, 9))),

            // Version 5 (37x37)
            new QrVersion(5, new int[] { 6, 30 },
                new QrECBlocks(26, new QrECBlock(1, 108)),
                new QrECBlocks(24, new QrECBlock(2, 43)),
                new QrECBlocks(18, new QrECBlock(2, 15), new QrECBlock(2, 16)),
                new QrECBlocks(22, new QrECBlock(2, 11), new QrECBlock(2, 12))),

            // Version 6 (41x41)
            new QrVersion(6, new int[] { 6, 34 },
                new QrECBlocks(18, new QrECBlock(2, 68)),
                new QrECBlocks(16, new QrECBlock(4, 27)),
                new QrECBlocks(24, new QrECBlock(4, 19)),
                new QrECBlocks(28, new QrECBlock(4, 15))),

            // Version 7 (45x45)
            new QrVersion(7, new int[] { 6, 22, 38 },
                new QrECBlocks(20, new QrECBlock(2, 78)),
                new QrECBlocks(18, new QrECBlock(4, 31)),
                new QrECBlocks(18, new QrECBlock(2, 14), new QrECBlock(4, 15)),
                new QrECBlocks(26, new QrECBlock(4, 13), new QrECBlock(1, 14))),

            // Version 8 (49x49)
            new QrVersion(8, new int[] { 6, 24, 42 },
                new QrECBlocks(24, new QrECBlock(2, 97)),
                new QrECBlocks(22, new QrECBlock(2, 38), new QrECBlock(2, 39)),
                new QrECBlocks(22, new QrECBlock(4, 18), new QrECBlock(2, 19)),
                new QrECBlocks(26, new QrECBlock(4, 14), new QrECBlock(2, 15))),

            // Version 9 (53x53)
            new QrVersion(9, new int[] { 6, 26, 46 },
                new QrECBlocks(30, new QrECBlock(2, 116)),
                new QrECBlocks(22, new QrECBlock(3, 36), new QrECBlock(2, 37)),
                new QrECBlocks(20, new QrECBlock(4, 16), new QrECBlock(4, 17)),
                new QrECBlocks(24, new QrECBlock(4, 12), new QrECBlock(4, 13))),

            // Version 10 (57x57)
            new QrVersion(10, new int[] { 6, 28, 50 },
                new QrECBlocks(18, new QrECBlock(2, 68), new QrECBlock(2, 69)),
                new QrECBlocks(26, new QrECBlock(4, 43), new QrECBlock(1, 44)),
                new QrECBlocks(24, new QrECBlock(6, 19), new QrECBlock(2, 20)),
                new QrECBlocks(28, new QrECBlock(6, 15), new QrECBlock(2, 16)))
        };

        public static QrVersion Version1 => AllVersions[0];
        public static QrVersion Version2 => AllVersions[1];
        public static QrVersion Version3 => AllVersions[2];
        public static QrVersion Version4 => AllVersions[3];

        public static QrVersion? GetByVersionNumber(int versionNumber) => GetVersionForNumber(versionNumber);

        public static QrVersion? GetVersionForNumber(int versionNumber)
        {
            if (versionNumber >= 1 && versionNumber <= AllVersions.Length)
                return AllVersions[versionNumber - 1];
            return null;
        }

        public static QrVersion? GetVersionForDimension(int dimension)
        {
            if (dimension < 21 || (dimension - 17) % 4 != 0)
                return null;
            int v = (dimension - 17) / 4;
            return GetVersionForNumber(v);
        }
    }
}
