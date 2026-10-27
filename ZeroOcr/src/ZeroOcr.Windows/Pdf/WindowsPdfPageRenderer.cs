using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Storage.Streams;
using ZeroOcr.Core.Imaging;
using ZeroOcr.Core.Pdf;

namespace ZeroOcr.Windows.Pdf;

/// <summary>
/// High-fidelity hardware-accelerated PDF page rasterizer utilizing Windows 10/11 native Windows.Data.Pdf engine.
/// Renders vector PDF pages directly into high-DPI in-memory OcrImageBuffer without third-party native binaries.
/// </summary>
public sealed class WindowsPdfPageRenderer : IPdfPageRenderer
{
    public bool IsAvailable => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public async Task<int> GetPageCountAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        if (pdfStream == null) throw new ArgumentNullException(nameof(pdfStream));
        cancellationToken.ThrowIfCancellationRequested();

        var randomAccessStream = pdfStream.AsRandomAccessStream();
        var pdfDoc = await PdfDocument.LoadFromStreamAsync(randomAccessStream).AsTask(cancellationToken);
        return (int)pdfDoc.PageCount;
    }

    public async Task<OcrImageBuffer> RenderPageAsync(
        Stream pdfStream,
        int pageIndex,
        int dpi = 250,
        CancellationToken cancellationToken = default)
    {
        if (pdfStream == null) throw new ArgumentNullException(nameof(pdfStream));
        cancellationToken.ThrowIfCancellationRequested();

        var randomAccessStream = pdfStream.AsRandomAccessStream();
        var pdfDoc = await PdfDocument.LoadFromStreamAsync(randomAccessStream).AsTask(cancellationToken);

        if (pageIndex < 0 || pageIndex >= (int)pdfDoc.PageCount)
            throw new ArgumentOutOfRangeException(nameof(pageIndex), $"Page index {pageIndex} is out of bounds (total: {pdfDoc.PageCount}).");

        using var page = pdfDoc.GetPage((uint)pageIndex);

        // Standard PDF points are 1/72 inch
        float scale = dpi / 72.0f;
        uint destWidth = (uint)Math.Max(32, Math.Round(page.Size.Width * scale));
        uint destHeight = (uint)Math.Max(32, Math.Round(page.Size.Height * scale));

        var renderOptions = new PdfPageRenderOptions
        {
            DestinationWidth = destWidth,
            DestinationHeight = destHeight
        };

        using var memStream = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(memStream, renderOptions).AsTask(cancellationToken);

        // Convert the rendered PNG/BMP stream into an uncompressed BGRA32 OcrImageBuffer
        using var netStream = memStream.AsStreamForRead();
        using var bmp = new Bitmap(netStream);

        int w = bmp.Width;
        int h = bmp.Height;
        var rect = new Rectangle(0, 0, w, h);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            int stride = data.Stride;
            byte[] bytes = new byte[stride * h];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

            return OcrImageBuffer.FromBgra32(w, h, bytes, stride);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    public void Dispose()
    {
        // No unmanaged resources requiring deterministic disposal
    }
}
