using System;

namespace ZeroReports.Engine.Watermark
{
    /// <summary>
    /// Document watermark definition equivalent to DevExpress XtraReport.Watermark.
    /// Renders diagonal security stamps ("CONFIDENTIAL", "BẢN NHÁP") or logo badges in the page background.
    /// </summary>
    public class ReportWatermark
    {
        public bool Enabled { get; set; } = true;
        public string Text { get; set; } = string.Empty;
        public byte[]? ImageData { get; set; }

        public float FontSize { get; set; } = 54f;
        public bool IsBold { get; set; } = true;
        public (float R, float G, float B) Color { get; set; } = (0.85f, 0.85f, 0.88f);
        public float Opacity { get; set; } = 0.18f;
        public float RotationAngle { get; set; } = 45f;
        public bool ShowBehind { get; set; } = true;

        public ReportWatermark() { }

        public ReportWatermark(string text, float opacity = 0.18f, float rotationAngle = 45f)
        {
            Text = text;
            Opacity = opacity;
            RotationAngle = rotationAngle;
            Enabled = true;
        }

        public ReportWatermark(byte[] imageData, float opacity = 0.18f)
        {
            ImageData = imageData;
            Opacity = opacity;
            Enabled = true;
        }

        public static ReportWatermark Draft => new ReportWatermark("BẢN NHÁP", 0.15f, 45f);
        public static ReportWatermark Confidential => new ReportWatermark("CONFIDENTIAL", 0.15f, 45f);
        public static ReportWatermark Void => new ReportWatermark("VOID", 0.20f, 45f);
    }
}
