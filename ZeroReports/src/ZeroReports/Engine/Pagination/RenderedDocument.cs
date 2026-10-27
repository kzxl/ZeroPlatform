using System;
using System.Collections.Generic;
using ZeroReports.Barcodes;

namespace ZeroReports.Engine.Pagination
{
    public abstract class RenderedItem
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }

    public class RenderedTextItem : RenderedItem
    {
        public string Text { get; set; } = string.Empty;
        public float FontSize { get; set; } = 10f;
        public bool IsBold { get; set; }
        public ReportTextAlignment Alignment { get; set; } = ReportTextAlignment.Left;
        public (float R, float G, float B) Color { get; set; } = (0f, 0f, 0f);
    }

    public class RenderedLineItem : RenderedItem
    {
        public float X2 { get; set; }
        public float Y2 { get; set; }
        public float LineWidth { get; set; } = 1f;
        public (float R, float G, float B) Color { get; set; } = (0.75f, 0.75f, 0.8f);
    }

    public class RenderedBoxItem : RenderedItem
    {
        public bool Fill { get; set; } = true;
        public bool Stroke { get; set; } = false;
        public float BorderWidth { get; set; } = 1f;
        public (float R, float G, float B) FillColor { get; set; } = (0.95f, 0.95f, 0.96f);
        public (float R, float G, float B) BorderColor { get; set; } = (0.8f, 0.8f, 0.8f);
    }

    public class RenderedBarcodeItem : RenderedItem
    {
        public BarcodeSymbology Symbology { get; set; }
        public string Text { get; set; } = string.Empty;
        public Barcode1DResult? Result1D { get; set; }
        public Barcode2DResult? Result2D { get; set; }
    }

    public class RenderedImageItem : RenderedItem
    {
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
    }

    public class RenderedWatermarkItem : RenderedItem
    {
        public string Text { get; set; } = string.Empty;
        public float FontSize { get; set; } = 54f;
        public float RotationAngle { get; set; } = 45f;
        public float Opacity { get; set; } = 0.15f;
        public (float R, float G, float B) Color { get; set; } = (0.85f, 0.85f, 0.88f);
    }

    public class RenderedPage
    {
        public int PageNumber { get; set; }
        public float Width { get; }
        public float Height { get; }
        public List<RenderedItem> Items { get; } = new List<RenderedItem>();

        public RenderedPage(int pageNumber, float width, float height)
        {
            PageNumber = pageNumber;
            Width = width;
            Height = height;
        }
    }

    public class RenderedDocument
    {
        public string Title { get; set; } = "Report";
        public float PageWidth { get; set; }
        public float PageHeight { get; set; }
        public List<RenderedPage> Pages { get; } = new List<RenderedPage>();

        public int TotalPages => Pages.Count;
    }
}
