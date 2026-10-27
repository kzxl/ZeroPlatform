using System;
using System.Collections.Generic;
using ZeroReports.Barcodes;

namespace ZeroReports.Engine
{
    /// <summary>
    /// Base class for all structural report bands.
    /// </summary>
    public abstract class ReportBand
    {
        public string Name { get; set; } = string.Empty;
        public float Height { get; set; } = 30f;
        public bool Visible { get; set; } = true;
        public List<ReportControl> Controls { get; } = new List<ReportControl>();

        public T AddControl<T>(T control) where T : ReportControl
        {
            Controls.Add(control);
            return control;
        }

        public LabelControl AddLabel(
            string text,
            float left,
            float top,
            float width,
            float height,
            float fontSize = 10f,
            bool isBold = false,
            ReportTextAlignment align = ReportTextAlignment.Left)
        {
            var lbl = new LabelControl(text, left, top, width, height)
            {
                FontSize = fontSize,
                IsBold = isBold,
                Alignment = align
            };
            Controls.Add(lbl);
            return lbl;
        }

        public LineControl AddLine(float left, float top, float width, float lineWidth = 1f)
        {
            var line = new LineControl(left, top, width) { LineWidth = lineWidth };
            Controls.Add(line);
            return line;
        }

        public BoxControl AddBox(float left, float top, float width, float height, (float R, float G, float B) fillColor)
        {
            var box = new BoxControl(left, top, width, height)
            {
                Fill = true,
                FillColor = fillColor
            };
            Controls.Add(box);
            return box;
        }

        public BarcodeControl AddBarcode(
            BarcodeSymbology symbology,
            string data,
            float left,
            float top,
            float width = 150f,
            float height = 50f)
        {
            var bc = new BarcodeControl(symbology, data, left, top, width, height);
            Controls.Add(bc);
            return bc;
        }

        public Table.ReportTable AddTable(Table.ReportTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            Controls.Add(table);
            return table;
        }

        public Table.ReportTable AddTable(float left, float top, float width)
        {
            var table = new Table.ReportTable(left, top, width);
            Controls.Add(table);
            return table;
        }

        public ExpressionControl AddExpression(
            string expression,
            float left,
            float top,
            float width,
            float height,
            float fontSize = 10f,
            bool isBold = false,
            ReportTextAlignment alignment = ReportTextAlignment.Left,
            ExpressionScope scope = ExpressionScope.Group,
            string format = "")
        {
            var expr = new ExpressionControl(expression, left, top, width, height)
            {
                FontSize = fontSize,
                IsBold = isBold,
                Alignment = alignment,
                Scope = scope,
                Format = format
            };
            Controls.Add(expr);
            return expr;
        }

        public ExpressionControl AddExpression(
            string expression,
            float left,
            float top,
            float width,
            float height,
            ExpressionScope scope,
            string format = "")
        {
            return AddExpression(expression, left, top, width, height, 10f, false, ReportTextAlignment.Left, scope, format);
        }

        public ChartReportControl AddChart(
            byte[] chartImageData,
            float left,
            float top,
            float width = 350f,
            float height = 180f)
        {
            var chart = new ChartReportControl(chartImageData, left, top, width, height);
            Controls.Add(chart);
            return chart;
        }
    }

    /// <summary>
    /// Band rendered once at the beginning of the entire report document.
    /// </summary>
    public class ReportHeaderBand : ReportBand
    {
        public ReportHeaderBand(float height = 60f) { Height = height; }
    }

    /// <summary>
    /// Band rendered at the top margin of every document page.
    /// </summary>
    public class PageHeaderBand : ReportBand
    {
        public bool PrintOnFirstPage { get; set; } = true;
        public PageHeaderBand(float height = 30f) { Height = height; }
    }

    /// <summary>
    /// Band rendered before a new category or group of data records begins.
    /// </summary>
    public class GroupHeaderBand : ReportBand
    {
        public string GroupField { get; set; } = string.Empty;
        public bool SortAscending { get; set; } = true;
        public bool PageBreakBefore { get; set; } = false;

        public GroupHeaderBand(string groupField = "", float height = 25f)
        {
            GroupField = groupField;
            Height = height;
        }
    }

    /// <summary>
    /// Band repeated for each record in the data source.
    /// </summary>
    public class DetailBand : ReportBand
    {
        public DetailBand(float height = 20f) { Height = height; }
    }

    /// <summary>
    /// Band rendered when a group of data records finishes, typically displaying group sub-totals.
    /// </summary>
    public class GroupFooterBand : ReportBand
    {
        public string GroupField { get; set; } = string.Empty;
        public bool PageBreakAfter { get; set; } = false;

        public GroupFooterBand(string groupField = "", float height = 25f)
        {
            GroupField = groupField;
            Height = height;
        }
    }

    /// <summary>
    /// Band rendered at the bottom margin of every document page.
    /// </summary>
    public class PageFooterBand : ReportBand
    {
        public bool PrintOnLastPage { get; set; } = true;
        public PageFooterBand(float height = 25f) { Height = height; }
    }

    /// <summary>
    /// Band rendered once at the conclusion of the report document, displaying grand totals and signatures.
    /// </summary>
    public class ReportFooterBand : ReportBand
    {
        public ReportFooterBand(float height = 40f) { Height = height; }
    }
}
