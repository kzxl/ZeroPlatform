using System;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// ISO/IEC 18004 QR Code error correction levels and their 2-bit format representations.
    /// </summary>
    public enum QrErrorCorrectionLevel
    {
        /// <summary>Level L (~7% recovery capacity), bit format 01b</summary>
        L = 1,
        /// <summary>Level M (~15% recovery capacity), bit format 00b</summary>
        M = 0,
        /// <summary>Level Q (~25% recovery capacity), bit format 11b</summary>
        Q = 3,
        /// <summary>Level H (~30% recovery capacity), bit format 10b</summary>
        H = 2
    }

    /// <summary>
    /// ISO/IEC 18004 QR Code encodation mode indicators (4-bit headers).
    /// </summary>
    public enum QrMode
    {
        Terminator = 0,
        Numeric = 1,
        Alphanumeric = 2,
        StructuredAppend = 3,
        Byte = 4,
        Fnc1FirstPosition = 5,
        Eci = 7,
        Kanji = 8,
        Fnc1SecondPosition = 9,
        Hanzi = 13
    }
}
