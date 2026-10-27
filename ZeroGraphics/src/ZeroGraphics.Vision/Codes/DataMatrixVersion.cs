using System;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// ISO/IEC 16022 DataMatrix ECC200 symbol version attributes and codeword capacities.
    /// </summary>
    public sealed class DataMatrixVersion
    {
        public int SymbolWidth { get; }
        public int SymbolHeight { get; }
        public int DataRows { get; }
        public int DataColumns { get; }
        public int DataCodewords { get; }
        public int ErrorCodewords { get; }
        public int TotalCodewords => DataCodewords + ErrorCodewords;

        public DataMatrixVersion(
            int symbolWidth,
            int symbolHeight,
            int dataRows,
            int dataColumns,
            int dataCodewords,
            int errorCodewords)
        {
            SymbolWidth = symbolWidth;
            SymbolHeight = symbolHeight;
            DataRows = dataRows;
            DataColumns = dataColumns;
            DataCodewords = dataCodewords;
            ErrorCodewords = errorCodewords;
        }

        public static readonly DataMatrixVersion[] AllVersions = new DataMatrixVersion[]
        {
            new DataMatrixVersion(10, 10, 8,  8,  3,  5),
            new DataMatrixVersion(12, 12, 10, 10, 5,  7),
            new DataMatrixVersion(14, 14, 12, 12, 8,  10),
            new DataMatrixVersion(16, 16, 14, 14, 12, 12),
            new DataMatrixVersion(18, 18, 16, 16, 18, 14),
            new DataMatrixVersion(20, 20, 18, 18, 22, 18),
            new DataMatrixVersion(22, 22, 20, 20, 30, 20),
            new DataMatrixVersion(24, 24, 22, 22, 36, 24),
            new DataMatrixVersion(26, 26, 24, 24, 44, 28)
        };

        public static DataMatrixVersion? FindVersion(int symbolWidth, int symbolHeight)
        {
            foreach (var v in AllVersions)
            {
                if (v.SymbolWidth == symbolWidth && v.SymbolHeight == symbolHeight)
                    return v;
            }
            return null;
        }
    }
}
