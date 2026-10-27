using System;

namespace ZeroReports.Engine
{
    public enum PaperKind
    {
        A4,
        Letter,
        A3,
        Custom
    }

    public enum PageOrientation
    {
        Portrait,
        Landscape
    }

    /// <summary>
    /// Page geometry, dimensions, margins, and orientation settings for a report.
    /// </summary>
    public class ReportPageSettings
    {
        public PaperKind PaperKind { get; set; } = PaperKind.A4;
        public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;

        public float CustomWidth { get; set; } = 595.28f;
        public float CustomHeight { get; set; } = 841.89f;

        public float MarginLeft { get; set; } = 36f;
        public float MarginTop { get; set; } = 36f;
        public float MarginRight { get; set; } = 36f;
        public float MarginBottom { get; set; } = 36f;

        public ReportPageSettings() { }

        public ReportPageSettings(float width, float height, float marginLeft = 36f, float marginTop = 36f, float marginRight = 36f, float marginBottom = 36f)
        {
            PaperKind = PaperKind.Custom;
            CustomWidth = width;
            CustomHeight = height;
            MarginLeft = marginLeft;
            MarginTop = marginTop;
            MarginRight = marginRight;
            MarginBottom = marginBottom;
        }

        public float Width
        {
            get
            {
                float baseW;
                switch (PaperKind)
                {
                    case PaperKind.Letter: baseW = 612.00f; break;
                    case PaperKind.A3: baseW = 841.89f; break;
                    case PaperKind.Custom: baseW = CustomWidth; break;
                    case PaperKind.A4:
                    default: baseW = 595.28f; break;
                }

                float baseH;
                switch (PaperKind)
                {
                    case PaperKind.Letter: baseH = 792.00f; break;
                    case PaperKind.A3: baseH = 1190.55f; break;
                    case PaperKind.Custom: baseH = CustomHeight; break;
                    case PaperKind.A4:
                    default: baseH = 841.89f; break;
                }

                return Orientation == PageOrientation.Portrait ? baseW : baseH;
            }
        }

        public float Height
        {
            get
            {
                float baseW;
                switch (PaperKind)
                {
                    case PaperKind.Letter: baseW = 612.00f; break;
                    case PaperKind.A3: baseW = 841.89f; break;
                    case PaperKind.Custom: baseW = CustomWidth; break;
                    case PaperKind.A4:
                    default: baseW = 595.28f; break;
                }

                float baseH;
                switch (PaperKind)
                {
                    case PaperKind.Letter: baseH = 792.00f; break;
                    case PaperKind.A3: baseH = 1190.55f; break;
                    case PaperKind.Custom: baseH = CustomHeight; break;
                    case PaperKind.A4:
                    default: baseH = 841.89f; break;
                }

                return Orientation == PageOrientation.Portrait ? baseH : baseW;
            }
        }

        public float UsableWidth => Width - MarginLeft - MarginRight;
        public float UsableHeight => Height - MarginTop - MarginBottom;
    }
}
