using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Pdf
{
    /// <summary>
    /// Represents a keyword search result with page index, snippet, and spatial bounding box.
    /// </summary>
    public sealed class PdfSearchResult
    {
        public int PageIndex { get; set; }
        public int PageNumber => PageIndex + 1;
        public string MatchedText { get; set; } = string.Empty;
        public string Snippet { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    /// <summary>
    /// Search engine scanning text runs across PDF pages with snippet extraction and match highlights.
    /// </summary>
    public static class PdfSearchEngine
    {
        public static List<PdfSearchResult> Search(PdfDocumentModel document, string query, bool matchCase = false)
        {
            var results = new List<PdfSearchResult>();
            if (document == null || string.IsNullOrWhiteSpace(query))
                return results;

            var comp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            for (int p = 0; p < document.Pages.Count; p++)
            {
                var page = document.Pages[p];
                for (int t = 0; t < page.TextRuns.Count; t++)
                {
                    var run = page.TextRuns[t];
                    int matchIdx = run.Text.IndexOf(query, comp);
                    if (matchIdx >= 0)
                    {
                        // Calculate approximate matched substring bounding box
                        double charWidth = run.Width / Math.Max(1, run.Text.Length);
                        double matchX = run.X + matchIdx * charWidth;
                        double matchW = Math.Max(charWidth * query.Length, 12);

                        // Context snippet
                        int start = Math.Max(0, matchIdx - 15);
                        int len = Math.Min(run.Text.Length - start, query.Length + 30);
                        string snippet = run.Text.Substring(start, len);

                        results.Add(new PdfSearchResult
                        {
                            PageIndex = p,
                            MatchedText = query,
                            Snippet = snippet,
                            X = matchX,
                            Y = run.Y,
                            Width = matchW,
                            Height = run.Height
                        });
                    }
                }
            }

            return results;
        }
    }
}
