using System;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// ISO/IEC 18004 QR Code 15-bit Format Information with BCH (15, 5) error correction.
    /// Stores Error Correction Level and Data Mask pattern index.
    /// </summary>
    public sealed class QrFormatInfo
    {
        private const int FormatInfoMask = 0x5412;

        /// <summary>
        /// 32 precomputed valid 15-bit format patterns (with 0x5412 XOR mask applied).
        /// Index = (ecLevelBits &lt;&lt; 3) | maskPattern.
        /// </summary>
        private static readonly int[] ValidFormatPatterns = new int[]
        {
            0x5412, 0x5125, 0x5E7C, 0x5B4B, 0x45F9, 0x40CE, 0x4F97, 0x4AA0,
            0x77C4, 0x72F3, 0x7DAA, 0x789D, 0x662F, 0x6318, 0x6C41, 0x6976,
            0x1689, 0x13BE, 0x1CE7, 0x19D0, 0x0762, 0x0255, 0x0D0C, 0x083B,
            0x355F, 0x3068, 0x3F31, 0x3A06, 0x24B4, 0x2183, 0x2EDA, 0x2BED
        };

        public QrErrorCorrectionLevel ErrorCorrectionLevel { get; }
        public int DataMask { get; }

        public QrFormatInfo(QrErrorCorrectionLevel ecLevel, int dataMask)
        {
            ErrorCorrectionLevel = ecLevel;
            DataMask = dataMask;
        }

        /// <summary>
        /// Attempts to decode format information from one or both 15-bit raw values read from the code.
        /// Tolerates up to 3 corrupted bits via minimum Hamming distance search.
        /// </summary>
        public static QrFormatInfo? DecodeFormatInformation(int maskedFormatInfo1, int maskedFormatInfo2)
        {
            var info = DoDecode(maskedFormatInfo1);
            if (info != null) return info;

            return DoDecode(maskedFormatInfo2);
        }

        public static QrFormatInfo? DoDecode(int maskedFormatInfo)
        {
            int bestDifference = int.MaxValue;
            int bestIndex = -1;

            for (int i = 0; i < ValidFormatPatterns.Length; i++)
            {
                int target = ValidFormatPatterns[i];
                if (target == maskedFormatInfo)
                {
                    return CreateFromIndex(i);
                }

                int diff = HammingDistance(maskedFormatInfo, target);
                if (diff < bestDifference)
                {
                    bestDifference = diff;
                    bestIndex = i;
                }
            }

            // QR Code BCH (15, 5) code can reliably correct up to 3 bit errors
            if (bestDifference <= 3 && bestIndex >= 0)
            {
                return CreateFromIndex(bestIndex);
            }

            return null;
        }

        /// <summary>
        /// Computes the 15-bit masked format information integer for a given EC Level and Mask pattern.
        /// </summary>
        public static int EncodeFormatInformation(QrErrorCorrectionLevel ecLevel, int maskPattern)
        {
            int d = (((int)ecLevel) << 3) | (maskPattern & 0x07);
            return ValidFormatPatterns[d];
        }

        private static QrFormatInfo CreateFromIndex(int index)
        {
            int ecBits = (index >> 3) & 0x03;
            int mask = index & 0x07;

            QrErrorCorrectionLevel level = (QrErrorCorrectionLevel)ecBits;
            return new QrFormatInfo(level, mask);
        }

        private static int HammingDistance(int a, int b)
        {
            int x = a ^ b;
            int count = 0;
            while (x != 0)
            {
                count += x & 1;
                x >>= 1;
            }
            return count;
        }
    }
}
