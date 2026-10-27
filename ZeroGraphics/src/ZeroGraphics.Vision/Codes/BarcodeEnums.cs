using System;
using System.Drawing;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// Supported optical barcode and 2D matrix code symbologies.
    /// </summary>
    public enum BarcodeSymbology
    {
        Unknown = 0,
        Code128 = 1,
        Code39 = 2,
        DataMatrix = 3,
        QrCode = 4,
        Ean13 = 5,
        UpcA = 6,
        Itf14 = 7,
        Ean8 = 8
    }

    /// <summary>
    /// Represents the result of an optical barcode / matrix code decoding operation.
    /// </summary>
    public sealed class BarcodeResult
    {
        /// <summary>
        /// Decoded payload string text.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Identified code symbology.
        /// </summary>
        public BarcodeSymbology Symbology { get; }

        /// <summary>
        /// Raw payload bytes before text decoding.
        /// </summary>
        public byte[] RawBytes { get; }

        /// <summary>
        /// Bounding corner / scanline points in image pixel coordinates.
        /// </summary>
        public PointF[] CornerPoints { get; }

        /// <summary>
        /// Confidence score [0.0 .. 1.0] of the decoding operation.
        /// </summary>
        public double Confidence { get; }

        public BarcodeResult(
            string text,
            BarcodeSymbology symbology,
            byte[] rawBytes,
            PointF[] cornerPoints,
            double confidence = 1.0)
        {
            Text = text ?? string.Empty;
            Symbology = symbology;
            RawBytes = rawBytes ?? new byte[0];
            CornerPoints = cornerPoints ?? new PointF[0];
            Confidence = confidence;
        }

        public override string ToString() => $"[{Symbology}] \"{Text}\" (Confidence: {Confidence:P0})";
    }
}
