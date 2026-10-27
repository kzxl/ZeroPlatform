using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using ZeroUI.Core.Pdf;

namespace ZeroUI.Core.Tests.Pdf
{
    public class PdfTests
    {
        [Fact]
        public void PdfSampleGenerator_CreatesValidThreePageDocument()
        {
            var doc = PdfSampleGenerator.CreateIndustrialCadAndSopDocument();

            Assert.NotNull(doc);
            Assert.Equal(3, doc.PageCount);
            Assert.Equal("1.7", doc.Version);

            // Page 1: CAD Schematic (Landscape)
            var p1 = doc.GetPage(0);
            Assert.NotNull(p1);
            Assert.Equal(1, p1.PageNumber);
            Assert.True(p1.Width > p1.Height, "Page 1 should be landscape");
            Assert.True(p1.Elements.Count > 15, "Page 1 should have rich vector elements");

            // Page 2: SOP (Portrait)
            var p2 = doc.GetPage(1);
            Assert.NotNull(p2);
            Assert.Equal(2, p2.PageNumber);
            Assert.True(p2.Height > p2.Width, "Page 2 should be portrait");
            Assert.Contains("EXTRUDER", p2.ExtractedText, StringComparison.OrdinalIgnoreCase);

            // Page 3: QC Certificate (Portrait)
            var p3 = doc.GetPage(2);
            Assert.NotNull(p3);
            Assert.Equal(3, p3.PageNumber);
            Assert.Contains("CALIBRATION", p3.ExtractedText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void PdfSearchEngine_FindsKeywordsAcrossDocumentPages()
        {
            var doc = PdfSampleGenerator.CreateIndustrialCadAndSopDocument();

            // Search for "Extruder"
            var extruderMatches = PdfSearchEngine.Search(doc, "Extruder");
            Assert.NotEmpty(extruderMatches);
            Assert.Contains(extruderMatches, m => m.PageNumber == 1);
            Assert.Contains(extruderMatches, m => m.PageNumber == 2);

            // Search for "PT-1049"
            var sensorMatches = PdfSearchEngine.Search(doc, "PT-1049");
            Assert.NotEmpty(sensorMatches);
            Assert.All(sensorMatches, m => Assert.False(string.IsNullOrEmpty(m.Snippet)));

            // Search for nonexistent term
            var emptyMatches = PdfSearchEngine.Search(doc, "NonexistentSecretToken999");
            Assert.Empty(emptyMatches);
        }

        [Fact]
        public void PdfLayoutEngine_ComputesContinuousScrollAndFitDimensions()
        {
            var doc = PdfSampleGenerator.CreateIndustrialCadAndSopDocument();
            var engine = new PdfLayoutEngine();

            engine.ComputeLayout(doc, viewportWidth: 1000, zoom: 1.0, viewMode: PdfViewMode.ContinuousScroll);

            Assert.True(engine.TotalHeight > 1500, "Total continuous height should exceed sum of 3 pages");
            Assert.Equal(3, engine.PageRects.Count);

            // Fit Width Zoom
            double fitWidthZoom = engine.CalculateFitWidthZoom(doc, viewportWidth: 1000);
            Assert.True(fitWidthZoom > 0.5 && fitWidthZoom < 2.0, $"Fit width zoom was {fitWidthZoom}");

            // Fit Page Zoom
            double fitPageZoom = engine.CalculateFitPageZoom(doc.Pages[0], viewportWidth: 1000, viewportHeight: 800);
            Assert.True(fitPageZoom > 0.5 && fitPageZoom < 2.0, $"Fit page zoom was {fitPageZoom}");

            // Viewport culling
            var visible = new List<PdfPageLayoutRect>();
            engine.GetVisiblePages(scrollY: 0, viewportHeight: 600, visible);
            Assert.NotEmpty(visible);
            Assert.Equal(0, visible[0].PageIndex);
        }

        [Fact]
        public void PdfParser_ParsesStandardPdfStreamWithObjectStructure()
        {
            // Synthetic minimal valid PDF
            string pdfContent =
                "%PDF-1.4\n" +
                "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n" +
                "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n" +
                "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>\nendobj\n" +
                "xref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n0000000115 00000 n \n" +
                "trailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n185\n%%EOF\n";

            byte[] pdfBytes = Encoding.ASCII.GetBytes(pdfContent);

            var parsedDoc = PdfParser.Parse(pdfBytes);

            Assert.NotNull(parsedDoc);
            Assert.Equal("1.4", parsedDoc.Version);
            Assert.True(parsedDoc.PageCount >= 1);
            Assert.Equal(612, parsedDoc.Pages[0].Width);
            Assert.Equal(792, parsedDoc.Pages[0].Height);
        }

        [Fact]
        public void PdfParser_ThrowsOnInvalidHeader()
        {
            byte[] invalidData = Encoding.ASCII.GetBytes("NOT A VALID PDF STREAM HEADER");

            Assert.Throws<InvalidDataException>(() => PdfParser.Parse(invalidData));
        }

        [Fact]
        public void PdfSampleGenerator_PopulatesDocumentBookmarksTree()
        {
            var doc = PdfSampleGenerator.CreateIndustrialCadAndSopDocument();

            Assert.NotNull(doc);
            Assert.NotEmpty(doc.Bookmarks);
            Assert.Equal(3, doc.Bookmarks.Count);

            // Bookmark 1
            Assert.Equal("1. Electrical CAD Schematic", doc.Bookmarks[0].Title);
            Assert.Equal(0, doc.Bookmarks[0].PageIndex);
            Assert.Equal(2, doc.Bookmarks[0].Children.Count);
            Assert.Equal("1.1 Power Incomer & Breakers", doc.Bookmarks[0].Children[0].Title);

            // Bookmark 2
            Assert.Equal("2. Extrusion Standard Operating Procedure", doc.Bookmarks[1].Title);
            Assert.Equal(1, doc.Bookmarks[1].PageIndex);
            Assert.Equal(2, doc.Bookmarks[1].Children.Count);

            // Bookmark 3
            Assert.Equal("3. Quality Compliance Certificate", doc.Bookmarks[2].Title);
            Assert.Equal(2, doc.Bookmarks[2].PageIndex);
            Assert.Equal(2, doc.Bookmarks[2].Children.Count);
        }
    }
}
