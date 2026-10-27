using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Pdf
{
    /// <summary>
    /// View mode for PDF document presentation.
    /// </summary>
    public enum PdfViewMode
    {
        ContinuousScroll,
        SinglePage
    }

    /// <summary>
    /// Layout item representing a page's layout coordinates in continuous document view space.
    /// </summary>
    public struct PdfPageLayoutRect
    {
        public int PageIndex;
        public double X;
        public double Y;
        public double Width;
        public double Height;

        public PdfPageLayoutRect(int pageIndex, double x, double y, double width, double height)
        {
            PageIndex = pageIndex;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public bool Intersects(double top, double bottom)
        {
            return (Y + Height >= top) && (Y <= bottom);
        }
    }

    /// <summary>
    /// Calculates document layout, page placement coordinates, continuous scrolling metrics, and viewport culling.
    /// </summary>
    public sealed class PdfLayoutEngine
    {
        public double PageSpacing { get; set; } = 16.0;

        private readonly List<PdfPageLayoutRect> _pageRects = new List<PdfPageLayoutRect>();

        public double TotalWidth { get; private set; }
        public double TotalHeight { get; private set; }

        public IReadOnlyList<PdfPageLayoutRect> PageRects => _pageRects;

        /// <summary>
        /// Recalculates page layout bounding boxes based on viewport dimensions and current zoom factor.
        /// </summary>
        public void ComputeLayout(PdfDocumentModel document, double viewportWidth, double zoom, PdfViewMode viewMode, int activeSinglePageIndex = 0)
        {
            _pageRects.Clear();
            TotalWidth = 0;
            TotalHeight = 0;

            if (document == null || document.Pages.Count == 0)
                return;

            if (viewMode == PdfViewMode.SinglePage)
            {
                int pIdx = Math.Max(0, Math.Min(document.Pages.Count - 1, activeSinglePageIndex));
                var page = document.Pages[pIdx];
                double scaledW = page.Width * zoom;
                double scaledH = page.Height * zoom;
                double x = Math.Max(PageSpacing, (viewportWidth - scaledW) / 2.0);
                double y = PageSpacing;

                _pageRects.Add(new PdfPageLayoutRect(pIdx, x, y, scaledW, scaledH));
                TotalWidth = scaledW + PageSpacing * 2;
                TotalHeight = scaledH + PageSpacing * 2;
                return;
            }

            // Continuous scroll mode
            double currentY = PageSpacing;
            double maxW = 0;

            for (int i = 0; i < document.Pages.Count; i++)
            {
                var page = document.Pages[i];
                double scaledW = page.Width * zoom;
                double scaledH = page.Height * zoom;

                double x = Math.Max(PageSpacing, (viewportWidth - scaledW) / 2.0);
                _pageRects.Add(new PdfPageLayoutRect(i, x, currentY, scaledW, scaledH));

                if (scaledW > maxW) maxW = scaledW;
                currentY += scaledH + PageSpacing;
            }

            TotalWidth = maxW + PageSpacing * 2;
            TotalHeight = currentY;
        }

        /// <summary>
        /// Computes zoom factor to fit the widest page to the available viewport width.
        /// </summary>
        public double CalculateFitWidthZoom(PdfDocumentModel document, double viewportWidth)
        {
            if (document == null || document.Pages.Count == 0 || viewportWidth <= 0)
                return 1.0;

            double maxPageW = 1.0;
            for (int i = 0; i < document.Pages.Count; i++)
            {
                if (document.Pages[i].Width > maxPageW)
                    maxPageW = document.Pages[i].Width;
            }

            double usableW = Math.Max(50, viewportWidth - PageSpacing * 2 - 24); // Deduct margins and scrollbar
            return Math.Max(0.2, Math.Min(4.0, usableW / maxPageW));
        }

        /// <summary>
        /// Computes zoom factor to fit the current page entirely inside viewport height and width.
        /// </summary>
        public double CalculateFitPageZoom(PdfPageModel page, double viewportWidth, double viewportHeight)
        {
            if (page == null || viewportWidth <= 0 || viewportHeight <= 0)
                return 1.0;

            double usableW = Math.Max(50, viewportWidth - PageSpacing * 2 - 24);
            double usableH = Math.Max(50, viewportHeight - PageSpacing * 2);

            double zoomW = usableW / page.Width;
            double zoomH = usableH / page.Height;
            return Math.Max(0.2, Math.Min(4.0, Math.Min(zoomW, zoomH)));
        }

        /// <summary>
        /// Identifies all pages visible within the current vertical scroll window (viewport culling).
        /// </summary>
        public void GetVisiblePages(double scrollY, double viewportHeight, List<PdfPageLayoutRect> visiblePages)
        {
            visiblePages.Clear();
            double viewTop = scrollY;
            double viewBottom = scrollY + viewportHeight;

            for (int i = 0; i < _pageRects.Count; i++)
            {
                var r = _pageRects[i];
                if (r.Intersects(viewTop, viewBottom))
                {
                    visiblePages.Add(r);
                }
            }
        }

        /// <summary>
        /// Returns the page index that currently occupies the majority of the visible viewport.
        /// </summary>
        public int GetActivePageIndex(double scrollY, double viewportHeight)
        {
            if (_pageRects.Count == 0) return 0;
            double centerY = scrollY + viewportHeight / 2.0;

            for (int i = 0; i < _pageRects.Count; i++)
            {
                var r = _pageRects[i];
                if (centerY >= r.Y && centerY <= r.Y + r.Height + PageSpacing)
                    return r.PageIndex;
            }

            if (scrollY < _pageRects[0].Y) return 0;
            return _pageRects[_pageRects.Count - 1].PageIndex;
        }
    }
}
