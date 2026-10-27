using System;
using System.Globalization;
using System.Net;
using System.Text;
using ZeroReports.Engine.Pagination;

namespace ZeroReports.Export
{
    /// <summary>
    /// Exports a RenderedDocument to standalone HTML with paper-page styling and SVG vector barcodes.
    /// Perfect for interactive preview in WebView2 or web browsers without third-party dependencies.
    /// </summary>
    public static class HtmlReportExporter
    {
        public static string Export(RenderedDocument doc)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"utf-8\" />");
            sb.AppendLine($"  <title>{WebUtility.HtmlEncode(doc.Title)}</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine("    body { background: #525659; margin: 0; padding: 20px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; }");
            sb.AppendLine("    .page-container { display: flex; flex-direction: column; align-items: center; gap: 20px; }");
            sb.AppendLine($"    .page {{ background: #ffffff; width: {doc.PageWidth:F1}pt; height: {doc.PageHeight:F1}pt; position: relative; box-shadow: 0 4px 12px rgba(0,0,0,0.3); overflow: hidden; page-break-after: always; }}");
            sb.AppendLine("    .report-text { position: absolute; white-space: nowrap; line-height: 1; user-select: text; }");
            sb.AppendLine("    .report-line { position: absolute; }");
            sb.AppendLine("    .report-box { position: absolute; box-sizing: border-box; }");
            sb.AppendLine("    .report-svg { position: absolute; }");
            sb.AppendLine("    @media print { body { background: none; padding: 0; } .page { box-shadow: none; } }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("  <div class=\"page-container\">");

            foreach (var page in doc.Pages)
            {
                sb.AppendLine($"    <div class=\"page\">");

                foreach (var item in page.Items)
                {
                    if (item is RenderedBoxItem box)
                    {
                        int r = (int)(box.FillColor.R * 255);
                        int g = (int)(box.FillColor.G * 255);
                        int b = (int)(box.FillColor.B * 255);
                        string bgStyle = box.Fill ? $"background-color: rgb({r},{g},{b});" : "";

                        int br = (int)(box.BorderColor.R * 255);
                        int bg = (int)(box.BorderColor.G * 255);
                        int bb = (int)(box.BorderColor.B * 255);
                        string borderStyle = box.Stroke ? $"border: {box.BorderWidth:F1}pt solid rgb({br},{bg},{bb});" : "";

                        sb.AppendLine($"      <div class=\"report-box\" style=\"left: {box.X:F1}pt; top: {box.Y:F1}pt; width: {box.Width:F1}pt; height: {box.Height:F1}pt; {bgStyle} {borderStyle}\"></div>");
                    }
                    else if (item is RenderedLineItem line)
                    {
                        int r = (int)(line.Color.R * 255);
                        int g = (int)(line.Color.G * 255);
                        int b = (int)(line.Color.B * 255);
                        sb.AppendLine($"      <div class=\"report-line\" style=\"left: {line.X:F1}pt; top: {line.Y:F1}pt; width: {line.Width:F1}pt; height: {line.LineWidth:F1}pt; background-color: rgb({r},{g},{b});\"></div>");
                    }
                    else if (item is RenderedTextItem text)
                    {
                        int r = (int)(text.Color.R * 255);
                        int g = (int)(text.Color.G * 255);
                        int b = (int)(text.Color.B * 255);
                        string align = text.Alignment.ToString().ToLowerInvariant();
                        string weight = text.IsBold ? "bold" : "normal";

                        sb.AppendLine($"      <div class=\"report-text\" style=\"left: {text.X:F1}pt; top: {text.Y:F1}pt; width: {text.Width:F1}pt; font-size: {text.FontSize:F1}pt; font-weight: {weight}; text-align: {align}; color: rgb({r},{g},{b});\">{WebUtility.HtmlEncode(text.Text)}</div>");
                    }
                    else if (item is RenderedBarcodeItem bc)
                    {
                        if (bc.Result1D != null)
                        {
                            float barH = bc.Height - 12f;
                            sb.AppendLine($"      <svg class=\"report-svg\" style=\"left: {bc.X:F1}pt; top: {bc.Y:F1}pt; width: {bc.Width:F1}pt; height: {bc.Height:F1}pt;\">");
                            foreach (var bar in bc.Result1D.Bars)
                            {
                                sb.AppendLine($"        <rect x=\"{bar.X.ToString("F1", CultureInfo.InvariantCulture)}\" y=\"0\" width=\"{bar.Width.ToString("F1", CultureInfo.InvariantCulture)}\" height=\"{barH.ToString("F1", CultureInfo.InvariantCulture)}\" fill=\"black\" />");
                            }
                            if (!string.IsNullOrEmpty(bc.Text))
                            {
                                sb.AppendLine($"        <text x=\"4\" y=\"{bc.Height.ToString("F1", CultureInfo.InvariantCulture)}\" font-size=\"8\" font-family=\"monospace\">{WebUtility.HtmlEncode(bc.Text)}</text>");
                            }
                            sb.AppendLine("      </svg>");
                        }
                        else if (bc.Result2D != null)
                        {
                            int sz = bc.Result2D.Size;
                            float mod = Math.Min(bc.Width, bc.Height) / sz;
                            sb.AppendLine($"      <svg class=\"report-svg\" style=\"left: {bc.X:F1}pt; top: {bc.Y:F1}pt; width: {bc.Width:F1}pt; height: {bc.Height:F1}pt;\">");
                            for (int my = 0; my < sz; my++)
                            {
                                for (int mx = 0; mx < sz; mx++)
                                {
                                    if (bc.Result2D[mx, my])
                                    {
                                        float px = mx * mod;
                                        float py = my * mod;
                                        sb.AppendLine($"        <rect x=\"{px.ToString("F1", CultureInfo.InvariantCulture)}\" y=\"{py.ToString("F1", CultureInfo.InvariantCulture)}\" width=\"{mod.ToString("F1", CultureInfo.InvariantCulture)}\" height=\"{mod.ToString("F1", CultureInfo.InvariantCulture)}\" fill=\"black\" />");
                                    }
                                }
                            }
                            sb.AppendLine("      </svg>");
                        }
                    }
                    else if (item is RenderedImageItem img && img.ImageData != null && img.ImageData.Length > 0)
                    {
                        string b64 = Convert.ToBase64String(img.ImageData);
                        sb.AppendLine($"      <img src=\"data:image/png;base64,{b64}\" style=\"position: absolute; left: {img.X.ToString("F1", CultureInfo.InvariantCulture)}pt; top: {img.Y.ToString("F1", CultureInfo.InvariantCulture)}pt; width: {img.Width.ToString("F1", CultureInfo.InvariantCulture)}pt; height: {img.Height.ToString("F1", CultureInfo.InvariantCulture)}pt; object-fit: contain;\" />");
                    }
                    else if (item is RenderedWatermarkItem wm)
                    {
                        int r = (int)(wm.Color.R * 255);
                        int g = (int)(wm.Color.G * 255);
                        int b = (int)(wm.Color.B * 255);
                        sb.AppendLine($"      <div class=\"report-watermark\" style=\"position: absolute; left: {wm.X.ToString("F1", CultureInfo.InvariantCulture)}pt; top: {wm.Y.ToString("F1", CultureInfo.InvariantCulture)}pt; transform: translate(-50%, -50%) rotate({(-wm.RotationAngle).ToString("F1", CultureInfo.InvariantCulture)}deg); font-size: {wm.FontSize.ToString("F1", CultureInfo.InvariantCulture)}pt; font-weight: bold; color: rgb({r},{g},{b}); opacity: {wm.Opacity.ToString("F2", CultureInfo.InvariantCulture)}; pointer-events: none; white-space: nowrap; user-select: none;\">{WebUtility.HtmlEncode(wm.Text)}</div>");
                    }
                }

                sb.AppendLine("    </div>");
            }

            sb.AppendLine("  </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
    }
}
