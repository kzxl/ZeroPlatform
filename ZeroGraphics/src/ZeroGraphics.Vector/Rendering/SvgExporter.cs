using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ZeroGraphics.Vector.Geometry;

namespace ZeroGraphics.Vector.Rendering
{
    public sealed class SvgElement
    {
        public Path2D Path { get; }
        public StrokeStyle? Stroke { get; }
        public string? StrokeColor { get; }
        public string? FillColor { get; }

        public SvgElement(Path2D path, StrokeStyle? stroke = null, string? strokeColor = null, string? fillColor = null)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Stroke = stroke;
            StrokeColor = strokeColor;
            FillColor = fillColor;
        }
    }

    /// <summary>
    /// Exports 2D vector paths and elements to standard SVG XML documents.
    /// </summary>
    public static class SvgExporter
    {
        public static string Export(
            IEnumerable<SvgElement> elements,
            int width,
            int height,
            string? backgroundColor = null)
        {
            var sb = new StringBuilder(1024);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{0}\" height=\"{1}\" viewBox=\"0 0 {0} {1}\">\n",
                width, height);

            if (!string.IsNullOrEmpty(backgroundColor))
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "  <rect width=\"100%\" height=\"100%\" fill=\"{0}\" />\n", backgroundColor);
            }

            foreach (var el in elements)
            {
                string d = el.Path.ToSvgPathData();
                if (string.IsNullOrEmpty(d)) continue;

                string stroke = el.StrokeColor ?? "none";
                string fill = el.FillColor ?? "none";
                float strokeWidth = el.Stroke?.Width ?? 1.0f;

                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "  <path d=\"{0}\" stroke=\"{1}\" fill=\"{2}\" stroke-width=\"{3}\" stroke-linecap=\"{4}\" stroke-linejoin=\"{5}\" />\n",
                    d, stroke, fill,
                    strokeWidth.ToString("0.##", CultureInfo.InvariantCulture),
                    (el.Stroke?.Cap.ToString() ?? "butt").ToLowerInvariant(),
                    (el.Stroke?.Join.ToString() ?? "miter").ToLowerInvariant());
            }

            sb.Append("</svg>");
            return sb.ToString();
        }

        public static string ExportSinglePath(
            Path2D path,
            int width,
            int height,
            StrokeStyle? stroke = null,
            string strokeColor = "#1976D2",
            string fillColor = "none")
        {
            var elem = new SvgElement(path, stroke, strokeColor, fillColor);
            return Export(new[] { elem }, width, height);
        }
    }
}
