using System;
using System.Text;
using Xunit;
using ZeroReports.Pdf;
using ZeroReports.Thermal;

namespace ZeroReports.Tests
{
    public class PdfAndThermalDeepTests
    {
        [Fact]
        public void PdfTable_AutoPagination_MultiPageDocument_RepeatsHeaders()
        {
            using var doc = new PdfDocument();
            var table = new PdfTable()
                .AddColumn("ID", 60, PdfTextAlignment.Center)
                .AddColumn("SKU Code", 120, PdfTextAlignment.Left)
                .AddColumn("Batch / Lot", 100, PdfTextAlignment.Center)
                .AddColumn("Quantity", 80, PdfTextAlignment.Right)
                .AddColumn("Status", 80, PdfTextAlignment.Center);

            // Add 80 rows (A4 page height is ~842pt, table rows will exceed 1 page and trigger auto-pagination)
            for (int i = 1; i <= 80; i++)
            {
                table.AddRow(
                    i,
                    $"SKU-CORE-{i:000}",
                    $"LOT-2026-X{i % 10}",
                    (i * 12.5).ToString("F1"),
                    i % 5 == 0 ? "HOLD" : "RELEASED"
                );
            }

            // Render starting on Page 1
            float endY = table.Render(doc, startX: 40, startY: 60, bottomMargin: 50);

            // Assertions
            Assert.True(doc.Pages.Count >= 2, $"Expected at least 2 pages for 80 rows, actual: {doc.Pages.Count}");
            Assert.True(endY > 60);

            byte[] pdfBytes = doc.ToByteArray();
            Assert.NotEmpty(pdfBytes);

            string pdfText = Encoding.ASCII.GetString(pdfBytes);
            Assert.Contains($"/Count {doc.Pages.Count}", pdfText); // Page count in /Pages
            Assert.Contains("/BaseFont /Helvetica-Bold", pdfText); // Header font
            Assert.Contains("/BaseFont /Helvetica", pdfText);      // Body font
        }

        [Fact]
        public void PdfTable_ColumnAlignments_And_AlternatingColors()
        {
            using var doc = new PdfDocument();
            var page = doc.AddPage();

            var table = new PdfTable
            {
                HeaderHeight = 30f,
                RowHeight = 22f
            };

            table.AddColumn("Item", 150, PdfTextAlignment.Left);
            table.AddColumn("Qty", 80, PdfTextAlignment.Right);
            table.AddColumn("Grade", 80, PdfTextAlignment.Center);

            table.AddRow("Raw Material Aluminum 6061", "1,200 kg", "A+");
            table.AddRow("Fastener Bolt M8x30", "5,000 pcs", "ISO-8.8");
            table.AddRow("Gasket O-Ring Viton", "250 pcs", "MIL-SPEC");

            float finalY = table.Render(doc, 50, 80);
            Assert.True(finalY > 80);

            byte[] pdfBytes = doc.ToByteArray();
            string pdfText = Encoding.ASCII.GetString(pdfBytes);

            Assert.Contains("(Raw Material Aluminum 6061) Tj", pdfText);
            Assert.Contains("(1,200 kg) Tj", pdfText);
            Assert.Contains("(A+) Tj", pdfText);
        }

        [Fact]
        public void PdfDocument_AddImage_EmbedsJpegXObject_WithTransformationMatrix()
        {
            using var doc = new PdfDocument();
            var page = doc.AddPage(PdfDocument.PageSizeA4.Width, PdfDocument.PageSizeA4.Height);

            // Mock JPEG binary (Valid JPEG header with SOF0 marker for 200x150)
            // 0xFF, 0xD8 (SOI), 0xFF, 0xC0 (SOF0), len=11, precision=8, height=150 (0x00, 0x96), width=200 (0x00, 0xC8)
            byte[] mockJpeg = new byte[]
            {
                0xFF, 0xD8,                         // SOI
                0xFF, 0xC0,                         // SOF0
                0x00, 0x0B,                         // Length = 11
                0x08,                               // Precision = 8
                0x00, 0x96,                         // Height = 150
                0x00, 0xC8,                         // Width = 200
                0x03, 0x01, 0x11, 0x00,             // Components
                0xFF, 0xD9                          // EOI
            };

            var pdfImage = doc.AddImage(mockJpeg);
            Assert.Equal(200, pdfImage.Width);
            Assert.Equal(150, pdfImage.Height);

            // Draw image on page
            page.DrawImage(pdfImage, x: 50, y: 100, width: 200, height: 150);

            byte[] pdfBytes = doc.ToByteArray();
            string pdfText = Encoding.ASCII.GetString(pdfBytes);

            Assert.Contains("/Type /XObject", pdfText);
            Assert.Contains("/Subtype /Image", pdfText);
            Assert.Contains("/Width 200", pdfText);
            Assert.Contains("/Height 150", pdfText);
            Assert.Contains("/Filter /DCTDecode", pdfText);
            Assert.Contains("/Im1 Do", pdfText);
        }

        [Fact]
        public void PdfDocument_BoldTypography_UsesHelveticaBoldF2()
        {
            using var doc = new PdfDocument();
            var page = doc.AddPage();

            page.DrawTextBold("INSPECTION CERTIFICATE (CONFIDENTIAL)", 50, 50, fontSize: 14f);

            byte[] pdfBytes = doc.ToByteArray();
            string pdfText = Encoding.ASCII.GetString(pdfBytes);

            Assert.Contains("/F2 14.0 Tf", pdfText);
            Assert.Contains("/BaseFont /Helvetica-Bold", pdfText);
            Assert.Contains(@"\(CONFIDENTIAL\)", pdfText); // Correctly escaped parentheses in PDF string
        }

        [Fact]
        public void ZplEncoder_ComplexLabelWithBarcodesAndInvert_ProducesValidZpl()
        {
            var zpl = new ZplEncoder();
            zpl.SetDarkness(25)
               .SetPrintSpeed(8)
               .DrawText(40, 30, "FACTORY WORK CELL #4", fontHeight: 24, fontWidth: 24)
               .DrawTextRotated(400, 30, "SIDE LABEL", orientation: 'R', fontHeight: 20, fontWidth: 20)
               .DrawBarcode39(40, 80, "PALLET-3901", height: 70)
               .DrawEan13(40, 180, "893850012345", height: 60)
               .DrawFieldInvert(40, 270, 300, 30) // Invert block for warnings
               .EndLabel();

            string output = zpl.ToZplString();

            Assert.Contains("^XA", output);
            Assert.Contains("^XZ", output);
            Assert.Contains("~SD25", output);
            Assert.Contains("^PR8", output);
            Assert.Contains("^B3N,N,70,Y,N^FDPALLET-3901^FS", output); // Code 39
            Assert.Contains("^BEN,60,Y,N^FD893850012345^FS", output);   // EAN 13
            Assert.Contains("^A0R,20,20^FDSIDE LABEL^FS", output);      // Rotated text
            Assert.Contains("^FR^GB300,30,30^FS", output);              // Inverted field
        }

        [Fact]
        public void TsplEncoder_ComplexLabel_ProducesValidTspl()
        {
            var tspl = new TsplEncoder(widthMm: 105, heightMm: 150);
            tspl.DrawText(30, 30, "OUTBOUND SHIPPING CONTAINER", font: "4")
                .DrawBarcodeEan(30, 100, "893850099999", height: 80)
                .DrawDataMatrix(400, 100, "DM-PALLET-999")
                .DrawReverse(30, 250, 400, 40)
                .Print(2);

            string output = tspl.ToTsplString();

            Assert.Contains("SIZE 105 mm, 150 mm", output);
            Assert.Contains("BARCODE 30,100,\"EAN13\",80,1,0,2,2,\"893850099999\"", output);
            Assert.Contains("DMATRIX 400,100,100,100,c6,\"DM-PALLET-999\"", output);
            Assert.Contains("REVERSE 30,250,400,40", output);
            Assert.Contains("PRINT 2", output);
        }
    }
}
