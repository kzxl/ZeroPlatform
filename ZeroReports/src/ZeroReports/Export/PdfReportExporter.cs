using System;
using ZeroReports.Barcodes;
using ZeroReports.Engine.Pagination;
using ZeroReports.Pdf;

namespace ZeroReports.Export
{
    /// <summary>
    /// Exports a multi-page RenderedDocument to a vector PDF 1.4 byte stream.
    /// Draws razor-sharp vector text, boxes, lines, and pure C# barcodes without raster blur.
    /// </summary>
    public static class PdfReportExporter
    {
        public static byte[] Export(RenderedDocument doc)
        {
            using (var pdf = new PdfDocument())
            {
                foreach (var page in doc.Pages)
                {
                    var pdfPage = pdf.AddPage(doc.PageWidth, doc.PageHeight);

                    foreach (var item in page.Items)
                    {
                        if (item is RenderedBoxItem box)
                        {
                            if (box.Fill)
                            {
                                pdfPage.SetFillColor(box.FillColor.R, box.FillColor.G, box.FillColor.B);
                            }
                            if (box.Stroke)
                            {
                                pdfPage.SetStrokeColor(box.BorderColor.R, box.BorderColor.G, box.BorderColor.B);
                                pdfPage.SetLineWidth(box.BorderWidth);
                            }
                            pdfPage.DrawRectangle(box.X, box.Y, box.Width, box.Height, fill: box.Fill, stroke: box.Stroke);
                        }
                        else if (item is RenderedLineItem line)
                        {
                            pdfPage.SetStrokeColor(line.Color.R, line.Color.G, line.Color.B);
                            pdfPage.SetLineWidth(line.LineWidth);
                            pdfPage.DrawLine(line.X, line.Y, line.X2, line.Y2);
                        }
                        else if (item is RenderedTextItem text)
                        {
                            pdfPage.SetFillColor(text.Color.R, text.Color.G, text.Color.B);

                            float drawX = text.X;
                            float approxTextWidth = text.Text.Length * (text.FontSize * 0.52f);

                            if (text.Alignment == Engine.ReportTextAlignment.Center)
                            {
                                drawX = text.X + Math.Max(0f, (text.Width - approxTextWidth) * 0.5f);
                            }
                            else if (text.Alignment == Engine.ReportTextAlignment.Right)
                            {
                                drawX = text.X + Math.Max(0f, text.Width - approxTextWidth);
                            }

                            float drawY = text.Y + text.FontSize;
                            if (text.IsBold)
                            {
                                pdfPage.DrawTextBold(text.Text, drawX, drawY, text.FontSize);
                            }
                            else
                            {
                                pdfPage.DrawText(text.Text, drawX, drawY, text.FontSize);
                            }
                        }
                        else if (item is RenderedBarcodeItem bc)
                        {
                            pdfPage.SetFillColor(0f, 0f, 0f);

                            if (bc.Result1D != null)
                            {
                                float barHeight = bc.Height - 12f;
                                foreach (var bar in bc.Result1D.Bars)
                                {
                                    pdfPage.DrawRectangle(bc.X + bar.X, bc.Y, bar.Width, barHeight, fill: true, stroke: false);
                                }

                                if (!string.IsNullOrEmpty(bc.Text))
                                {
                                    pdfPage.DrawText(bc.Text, bc.X + 4f, bc.Y + bc.Height, fontSize: 8f);
                                }
                            }
                            else if (bc.Result2D != null)
                            {
                                int matrixSize = bc.Result2D.Size;
                                float moduleSize = Math.Min(bc.Width, bc.Height) / matrixSize;

                                for (int my = 0; my < matrixSize; my++)
                                {
                                    for (int mx = 0; mx < matrixSize; mx++)
                                    {
                                        if (bc.Result2D[mx, my])
                                        {
                                            pdfPage.DrawRectangle(
                                                bc.X + (mx * moduleSize),
                                                bc.Y + (my * moduleSize),
                                                moduleSize,
                                                moduleSize,
                                                fill: true,
                                                stroke: false);
                                        }
                                    }
                                }
                            }
                        }
                        else if (item is RenderedImageItem img)
                        {
                            var pdfImg = pdf.AddImage(img.ImageData);
                            pdfPage.DrawImage(pdfImg, img.X, img.Y, img.Width, img.Height);
                        }
                        else if (item is RenderedWatermarkItem wm)
                        {
                            pdfPage.SetFillColor(wm.Color.R, wm.Color.G, wm.Color.B);
                            pdfPage.DrawTextRotated(wm.Text, wm.X, wm.Y, wm.FontSize, wm.RotationAngle);
                        }
                    }
                }

                return pdf.ToByteArray();
            }
        }
    }
}
