using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ZeroOcr.Core.Engines;
using ZeroOcr.Core.Pdf;
using ZeroOcr.Inference.Pdf;
using ZeroOcr.Windows.Pdf;

namespace ZeroOcr.Tests;

public class PdfOcrProcessorTests
{
    [Fact]
    public async Task ProcessPdfAsync_BornDigitalPdf_ExecutesFastPathDirectExtraction()
    {
        // Synthesize valid minimal native digital PDF stream
        byte[] pdfBytes = CreateSampleNativePdf("ZeroPlatform Sovereign Deep OCR Engine for Vietnamese Documents");
        using var stream = new MemoryStream(pdfBytes);

        var mockEngine = new MockOcrEngine();
        var processor = new ZeroPdfOcrProcessor(mockEngine);

        var options = new PdfOcrOptions
        {
            Mode = PdfOcrMode.Auto,
            MinNativeCharsForFastPath = 10
        };

        var result = await processor.ProcessPdfAsync(stream, options);

        Assert.True(result.Success);
        Assert.Single(result.Pages);
        var page = result.Pages[0];

        Assert.False(page.IsScannedPage);
        Assert.Equal(PdfExtractionSource.NativeDigitalFastPath, page.ExtractionSource);
        Assert.Contains("ZeroPlatform Sovereign Deep OCR Engine", result.FullText);
    }

    [Fact]
    public async Task ProcessPdfAsync_WhenModeExtractTextOnly_ExtractsNativeWithoutOcr()
    {
        byte[] pdfBytes = CreateSampleNativePdf("Invoice No 2026-INV-9988 Hanoi Vietnam Total 15000000 VND");
        using var stream = new MemoryStream(pdfBytes);

        var mockEngine = new MockOcrEngine();
        var processor = new ZeroPdfOcrProcessor(mockEngine);

        var options = new PdfOcrOptions
        {
            Mode = PdfOcrMode.ExtractTextOnly,
            MinNativeCharsForFastPath = 10
        };

        var result = await processor.ProcessPdfAsync(stream, options);

        Assert.True(result.Success);
        Assert.Equal(PdfExtractionSource.NativeDigitalFastPath, result.Pages[0].ExtractionSource);
        Assert.Contains("Invoice No 2026-INV-9988", result.FullText);
    }

    [Fact]
    public async Task WindowsPdfPageRenderer_OnWindowsEnvironment_RendersPageToOcrImageBuffer()
    {
        var renderer = new WindowsPdfPageRenderer();
        if (!renderer.IsAvailable) return;

        byte[] pdfBytes = CreateSampleNativePdf("ZeroPlatform Rasterized Page Validation Check");
        using var stream = new MemoryStream(pdfBytes);

        int pageCount = await renderer.GetPageCountAsync(stream);
        Assert.True(pageCount >= 1);

        stream.Position = 0;
        using var buffer = await renderer.RenderPageAsync(stream, pageIndex: 0, dpi: 150);

        Assert.NotNull(buffer);
        Assert.True(buffer.Width > 100);
        Assert.True(buffer.Height > 100);
        Assert.Equal(ZeroOcr.Core.Imaging.OcrPixelFormat.Bgra32, buffer.Format);
    }

    private static byte[] CreateSampleNativePdf(string sampleText)
    {
        // Standard minimal valid PDF 1.4 specification stream
        string streamData = $"BT\n/F1 12 Tf\n50 750 Td\n({sampleText}) Tj\nET\n";
        byte[] streamBytes = Encoding.ASCII.GetBytes(streamData);

        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");
        sb.Append("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        sb.Append("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
        sb.Append("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>\nendobj\n");
        sb.Append($"4 0 obj\n<< /Length {streamBytes.Length} >>\nstream\n");
        sb.Append(streamData);
        sb.Append("endstream\nendobj\n");
        sb.Append("xref\n0 5\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n0000000115 00000 n \n0000000210 00000 n \n");
        sb.Append("trailer\n<< /Size 5 /Root 1 0 R >>\nstartxref\n350\n%%EOF\n");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}
