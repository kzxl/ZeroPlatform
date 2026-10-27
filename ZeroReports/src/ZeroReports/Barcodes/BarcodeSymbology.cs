using System;
using System.Collections.Generic;

namespace ZeroReports.Barcodes
{
    /// <summary>
    /// Supported industrial 1D and 2D barcode symbologies.
    /// </summary>
    public enum BarcodeSymbology
    {
        Code128,
        QrCode,
        Ean13,
        Code39
    }

    /// <summary>
    /// Represents a single vertical bar (black or white) in a 1D barcode.
    /// </summary>
    public struct BarcodeBar
    {
        public float X;
        public float Width;
        public bool IsBlack;

        public BarcodeBar(float x, float width, bool isBlack)
        {
            X = x;
            Width = width;
            IsBlack = isBlack;
        }
    }

    /// <summary>
    /// Result of encoding data into a 1D barcode sequence of bars.
    /// </summary>
    public class Barcode1DResult
    {
        public List<BarcodeBar> Bars { get; } = new List<BarcodeBar>();
        public float TotalWidth { get; internal set; }
        public string Text { get; }

        public Barcode1DResult(string text)
        {
            Text = text ?? string.Empty;
        }
    }

    /// <summary>
    /// Result of encoding data into a 2D matrix (QR Code, DataMatrix).
    /// </summary>
    public class Barcode2DResult
    {
        public bool[,] Modules { get; }
        public int Size { get; }

        public Barcode2DResult(int size)
        {
            Size = size;
            Modules = new bool[size, size];
        }

        public bool this[int x, int y]
        {
            get => Modules[x, y];
            set => Modules[x, y] = value;
        }
    }
}
