using System;
using System.Collections.Generic;
using ZeroReports.Barcodes;
using ZeroReports.Engine.Formatting;

namespace ZeroReports.Engine
{
    public enum ReportTextAlignment
    {
        Left,
        Center,
        Right
    }

    public enum ExpressionScope
    {
        Group,
        Report
    }

    /// <summary>
    /// Base visual control placed inside a report band.
    /// </summary>
    public abstract class ReportControl
    {
        public string Name { get; set; } = string.Empty;
        public float Left { get; set; }
        public float Top { get; set; }
        public float Width { get; set; } = 100f;
        public float Height { get; set; } = 20f;
        public bool Visible { get; set; } = true;

        public List<ConditionalFormattingRule> FormattingRules { get; } = new List<ConditionalFormattingRule>();
    }

    /// <summary>
    /// Text label control with template token formatting (e.g. "{ItemCode}: {ItemName}").
    /// </summary>
    public class LabelControl : ReportControl
    {
        public string Text { get; set; } = string.Empty;
        public string DataField { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public float FontSize { get; set; } = 10f;
        public bool IsBold { get; set; }
        public ReportTextAlignment Alignment { get; set; } = ReportTextAlignment.Left;

        public (float R, float G, float B) TextColor { get; set; } = (0f, 0f, 0f);
        public (float R, float G, float B)? BackgroundColor { get; set; }

        public LabelControl() { }

        public LabelControl(string text, float left, float top, float width, float height)
        {
            Text = text;
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Evaluated aggregate expression control (e.g. "SUM(TotalAmount)", "COUNT()").
    /// </summary>
    public class ExpressionControl : ReportControl
    {
        public string Expression { get; set; } = "COUNT()";
        public ExpressionScope Scope { get; set; } = ExpressionScope.Group;
        public string Format { get; set; } = string.Empty;
        public float FontSize { get; set; } = 10f;
        public bool IsBold { get; set; } = true;
        public ReportTextAlignment Alignment { get; set; } = ReportTextAlignment.Right;
        public (float R, float G, float B) TextColor { get; set; } = (0f, 0f, 0f);

        public ExpressionControl() { }

        public ExpressionControl(string expression, float left, float top, float width, float height)
        {
            Expression = expression;
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Horizontal or vertical divider line.
    /// </summary>
    public class LineControl : ReportControl
    {
        public float LineWidth { get; set; } = 1f;
        public (float R, float G, float B) LineColor { get; set; } = (0.75f, 0.75f, 0.8f);

        public LineControl()
        {
            Height = 2f;
        }

        public LineControl(float left, float top, float width)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = 2f;
        }
    }

    /// <summary>
    /// Rectangle box with optional border stroke and background fill.
    /// </summary>
    public class BoxControl : ReportControl
    {
        public bool Fill { get; set; } = true;
        public bool Stroke { get; set; } = false;
        public float BorderWidth { get; set; } = 1f;
        public (float R, float G, float B) FillColor { get; set; } = (0.95f, 0.95f, 0.96f);
        public (float R, float G, float B) BorderColor { get; set; } = (0.8f, 0.8f, 0.8f);

        public BoxControl() { }

        public BoxControl(float left, float top, float width, float height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Pure C# vector barcode control (Code 128, QR Code, EAN-13).
    /// </summary>
    public class BarcodeControl : ReportControl
    {
        public BarcodeSymbology Symbology { get; set; } = BarcodeSymbology.Code128;
        public string Data { get; set; } = string.Empty;
        public string DataField { get; set; } = string.Empty;
        public float ModuleWidth { get; set; } = 1.0f;
        public bool ShowText { get; set; } = true;
        public float TextFontSize { get; set; } = 8f;

        public BarcodeControl()
        {
            Height = 50f;
            Width = 150f;
        }

        public BarcodeControl(BarcodeSymbology symbology, string data, float left, float top, float width = 150f, float height = 50f)
        {
            Symbology = symbology;
            Data = data;
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Embedded image control.
    /// </summary>
    public class ImageControl : ReportControl
    {
        public byte[]? ImageData { get; set; }

        public ImageControl() { }

        public ImageControl(byte[] imageData, float left, float top, float width, float height)
        {
            ImageData = imageData;
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Dynamic two-pass page numbering control (e.g. "Page {PageNumber} of {TotalPages}").
    /// </summary>
    public class PageNumberControl : ReportControl
    {
        public string Format { get; set; } = "Page {PageNumber} of {TotalPages}";
        public float FontSize { get; set; } = 9f;
        public bool IsBold { get; set; } = false;
        public ReportTextAlignment Alignment { get; set; } = ReportTextAlignment.Right;
        public (float R, float G, float B) TextColor { get; set; } = (0.4f, 0.4f, 0.45f);

        public PageNumberControl() { }

        public PageNumberControl(float left, float top, float width, float height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Embedded chart control equivalent to DevExpress XRChart.
    /// Embeds high-density telemetry, bar, pie, radar, and KPI charts rendered via ZeroCharts.
    /// </summary>
    public class ChartReportControl : ReportControl
    {
        public byte[]? ChartImageData { get; set; }

        public ChartReportControl()
        {
            Width = 350f;
            Height = 180f;
        }

        public ChartReportControl(byte[] chartImageData, float left, float top, float width = 350f, float height = 180f)
        {
            ChartImageData = chartImageData;
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }
    }
}
