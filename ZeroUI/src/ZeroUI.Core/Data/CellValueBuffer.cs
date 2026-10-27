using System;
using ZeroUI.Core.Common;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Stack-only ref struct payload passed to cell renderers. Guarantees 0 GC allocations.
    /// </summary>
    public ref struct CellValueBuffer
    {
        public ReadOnlySpan<char> Text;
        public CellAlignment Alignment;
        public uint TextColor;       // 0x00BBGGRR (Win32 COLORREF format)
        public uint BackColor;       // 0x00BBGGRR
        public bool HasCustomBackground;
        public bool IsBold;
        public float DataBarPercent; // -1.0f to 1.0f (-1 = no data bar, >= 0 = data bar fill percentage)
        public ReadOnlySpan<float> SparklineValues; // in-cell micro-trend series

        public CellValueBuffer(ReadOnlySpan<char> initialText)
        {
            Text = initialText;
            Alignment = CellAlignment.Left;
            TextColor = 0x00000000;  // Default black
            BackColor = 0x00FFFFFF;  // Default white
            HasCustomBackground = false;
            IsBold = false;
            DataBarPercent = -1.0f;
            SparklineValues = ReadOnlySpan<float>.Empty;
        }

        public void Reset()
        {
            Text = ReadOnlySpan<char>.Empty;
            Alignment = CellAlignment.Left;
            TextColor = 0x00000000;
            BackColor = 0x00FFFFFF;
            HasCustomBackground = false;
            IsBold = false;
            DataBarPercent = -1.0f;
            SparklineValues = ReadOnlySpan<float>.Empty;
        }
    }
}
