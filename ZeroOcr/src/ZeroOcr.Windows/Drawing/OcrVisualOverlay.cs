using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ZeroOcr.Core.Imaging;
using ZeroOcr.Core.Inspection;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Windows.Drawing;

/// <summary>
/// Visualization options for rendering OCR bounding boxes and text badges.
/// </summary>
public sealed class OcrVisualOptions
{
    public bool DrawWords { get; set; } = true;
    public bool DrawLines { get; set; } = false;
    public bool ShowTextLabels { get; set; } = true;
    public bool ShowConfidence { get; set; } = true;
    public float Scale { get; set; } = 1.0f;
    public int BorderThickness { get; set; } = 2;
    public Font? LabelFont { get; set; }
}

/// <summary>
/// GDI+ visualization engine for rendering OCR bounding boxes, text badges, and inspection verdicts.
/// </summary>
public static class OcrVisualOverlay
{
    /// <summary>
    /// Converts a System.Drawing.Bitmap into a memory-safe OcrImageBuffer using direct memory locking.
    /// </summary>
    public static OcrImageBuffer ToOcrImageBuffer(this Bitmap bitmap)
    {
        if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));

        int width = bitmap.Width;
        int height = bitmap.Height;

        var rect = new Rectangle(0, 0, width, height);
        var bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            int stride = Math.Abs(bmpData.Stride);
            int totalBytes = stride * height;
            byte[] bytes = new byte[totalBytes];

            System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, bytes, 0, totalBytes);
            return OcrImageBuffer.FromBgra32(width, height, bytes, stride);
        }
        finally
        {
            bitmap.UnlockBits(bmpData);
        }
    }

    /// <summary>
    /// Renders OCR result bounding boxes and labels onto a Graphics surface.
    /// </summary>
    public static void DrawToGraphics(
        Graphics g,
        OcrResult result,
        OcrVisualOptions? options = null)
    {
        if (g == null || result == null || !result.Success) return;

        options ??= new OcrVisualOptions();
        float scale = Math.Max(0.1f, options.Scale);
        using var font = options.LabelFont ?? new Font("Segoe UI", 9f * scale, FontStyle.Bold);
        using var bgBrush = new SolidBrush(Color.FromArgb(180, 20, 20, 20));
        using var textBrush = new SolidBrush(Color.White);

        if (options.DrawWords)
        {
            foreach (var word in result.Words)
            {
                var r = word.BoundingBox;
                var drawRect = new RectangleF(
                    r.X * scale,
                    r.Y * scale,
                    r.Width * scale,
                    r.Height * scale);

                Color boxColor = word.Confidence switch
                {
                    >= 0.85f => Color.FromArgb(0, 230, 118), // Vibrant Green
                    >= 0.60f => Color.FromArgb(255, 179, 0), // Amber
                    _ => Color.FromArgb(255, 61, 0)          // Bright Red
                };

                using var pen = new Pen(boxColor, options.BorderThickness);
                g.DrawRectangle(pen, drawRect.X, drawRect.Y, drawRect.Width, drawRect.Height);

                if (options.ShowTextLabels)
                {
                    string label = options.ShowConfidence
                        ? $"{word.Text} ({word.Confidence:P0})"
                        : word.Text;

                    var labelSize = g.MeasureString(label, font);
                    var labelRect = new RectangleF(drawRect.X, Math.Max(0, drawRect.Y - labelSize.Height), labelSize.Width, labelSize.Height);

                    g.FillRectangle(bgBrush, labelRect);
                    g.DrawString(label, font, textBrush, labelRect.X, labelRect.Y);
                }
            }
        }
    }

    /// <summary>
    /// Renders an industrial pass/fail inspection badge overlay.
    /// </summary>
    public static void DrawInspectionVerdict(
        Graphics g,
        OcrInspectionVerdict verdict,
        PointF position,
        float scale = 1.0f)
    {
        if (g == null || verdict == null) return;

        Color verdictColor = verdict.IsPassed
            ? Color.FromArgb(0, 200, 83)
            : Color.FromArgb(213, 0, 0);

        using var font = new Font("Segoe UI", 14f * scale, FontStyle.Bold);
        using var smallFont = new Font("Segoe UI", 9f * scale, FontStyle.Regular);
        using var pen = new Pen(verdictColor, 3f * scale);
        using var brush = new SolidBrush(verdictColor);
        using var textBrush = new SolidBrush(Color.White);

        string header = verdict.IsPassed ? "PASS [OK]" : "FAIL [NG]";
        string detail = $"{verdict.InspectionName}: {verdict.Actual}";

        var headerSize = g.MeasureString(header, font);
        var detailSize = g.MeasureString(detail, smallFont);

        float width = Math.Max(headerSize.Width, detailSize.Width) + (20f * scale);
        float height = headerSize.Height + detailSize.Height + (15f * scale);

        var badgeRect = new RectangleF(position.X, position.Y, width, height);

        using var bgBrush = new SolidBrush(Color.FromArgb(220, 25, 25, 25));
        g.FillRectangle(bgBrush, badgeRect);
        g.DrawRectangle(pen, badgeRect.X, badgeRect.Y, badgeRect.Width, badgeRect.Height);

        g.DrawString(header, font, brush, position.X + (10f * scale), position.Y + (5f * scale));
        g.DrawString(detail, smallFont, textBrush, position.X + (10f * scale), position.Y + headerSize.Height + (5f * scale));
    }
}
