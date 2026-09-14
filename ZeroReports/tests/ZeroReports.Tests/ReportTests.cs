using System;
using System.IO;
using System.Text;
using Xunit;
using ZeroReports.Dom;
using ZeroReports.Pdf;
using ZeroReports.Thermal;

namespace ZeroReports.Tests
{
    public class ReportTests
    {
        [Fact]
        public void ZplEncoder_ProducesValidZplStream()
        {
            var zpl = new ZplEncoder();
            zpl.SetDarkness(20)
               .SetPrintSpeed(6)
               .DrawText(50, 50, "Zero Universe Lot #8921", fontHeight: 30, fontWidth: 30)
               .DrawBox(40, 40, 500, 300, borderThickness: 3)
               .DrawBarcode128(60, 100, "ZERO-LOT-8921", height: 80)
               .DrawQrCode(350, 100, "https://zeroplatform.internal/lot/8921")
               .DrawDataMatrix(60, 220, "DATAMATRIX-PAYLOAD")
               .EndLabel();

            string output = zpl.ToZplString();

            Assert.Contains("^XA", output);
            Assert.Contains("^XZ", output);
            Assert.Contains("~SD20", output);
            Assert.Contains("^PR6", output);
            Assert.Contains("^FO50,50^A0N,30,30^FDZero Universe Lot #8921^FS", output);
            Assert.Contains("^FO40,40^GB500,300,3^FS", output);
            Assert.Contains("^FO60,100^BY2^BCN,80,Y,N,N^FDZERO-LOT-8921^FS", output);
            Assert.Contains("^FO350,100^BQN,2,4^FDQA,https://zeroplatform.internal/lot/8921^FS", output);
            Assert.Contains("^FO60,220^BXN,5,200,,,,1^FDDATAMATRIX-PAYLOAD^FS", output);
        }

        [Fact]
        public void TsplEncoder_ProducesValidTsplStream()
        {
            var tspl = new TsplEncoder(widthMm: 100, heightMm: 75);
            tspl.DrawText(20, 20, "PALLET #99102", font: "4")
                .DrawBox(10, 10, 780, 580, lineThickness: 2)
                .DrawBarcode(20, 100, "PL99102", type: "128", height: 70)
                .DrawQrCode(400, 100, "https://zeroplatform.io/p/99102")
                .Print(1);

            string output = tspl.ToTsplString();

            Assert.Contains("SIZE 100 mm, 75 mm", output);
            Assert.Contains("DIRECTION 1", output);
            Assert.Contains("CLS", output);
            Assert.Contains("TEXT 20,20,\"4\",0,1,1,\"PALLET #99102\"", output);
            Assert.Contains("BOX 10,10,780,580,2", output);
            Assert.Contains("BARCODE 20,100,\"128\",70,1,0,2,2,\"PL99102\"", output);
            Assert.Contains("QRCODE 400,100,L,4,A,0,\"https://zeroplatform.io/p/99102\"", output);
            Assert.Contains("PRINT 1", output);
        }

        [Fact]
        public void PdfDocument_GeneratesValidPdf14Binary()
        {
            using (var doc = new PdfDocument())
            {
                var page = doc.AddPage(PdfDocument.PageSizeA4.Width, PdfDocument.PageSizeA4.Height);

                page.SetFillColor(0.2f, 0.4f, 0.8f);
                page.DrawRectangle(50, 50, 495, 40, fill: true, stroke: false);

                page.SetFillColor(1f, 1f, 1f);
                page.DrawText("PRODUCTION WORK ORDER", 60, 75, fontSize: 16);

                page.SetStrokeColor(0.5f, 0.5f, 0.5f);
                page.SetLineWidth(1.5f);
                page.DrawLine(50, 110, 545, 110);

                page.SetFillColor(0f, 0f, 0f);
                page.DrawText("Work Order ID: WO-2026-9901", 50, 130, fontSize: 11);
                page.DrawText("Status: COMPLETED", 50, 150, fontSize: 11);

                byte[] pdfBytes = doc.ToByteArray();

                Assert.NotNull(pdfBytes);
                Assert.True(pdfBytes.Length > 200, "PDF binary must be non-trivial size.");

                string pdfAscii = Encoding.ASCII.GetString(pdfBytes);
                Assert.StartsWith("%PDF-1.4", pdfAscii);
                Assert.Contains("/Type /Catalog", pdfAscii);
                Assert.Contains("/Type /Pages", pdfAscii);
                Assert.Contains("/Type /Page", pdfAscii);
                Assert.Contains("/Type /Font /Subtype /Type1 /BaseFont /Helvetica", pdfAscii);
                Assert.Contains("xref", pdfAscii);
                Assert.Contains("trailer", pdfAscii);
                Assert.Contains("startxref", pdfAscii);
                Assert.Contains("%%EOF", pdfAscii);
            }
        }

        [Fact]
        public void ReportDocument_RendersToPdfAndZpl()
        {
            var report = new ReportDocument("INVENTORY INSPECTION MANIFEST")
            {
                Subtitle = "Batch: BATCH-8821 | Inspector: Agent-Zero"
            };

            var sec1 = report.AddSection("Batch Summary");
            sec1.AddKeyValue("Facility", "Sector 7 Fab")
                .AddKeyValue("Total Parts", "12,500 pcs")
                .AddKeyValue("Yield Rate", "99.82%");

            var sec2 = report.AddSection("Inspection Breakdown");
            sec2.AddTable(tbl =>
            {
                tbl.AddColumn("Item SKU")
                   .AddColumn("Description")
                   .AddColumn("Passed")
                   .AddColumn("Defect");

                tbl.AddRow("CPU-Z90", "Quantum Processor", "5,000", "2");
                tbl.AddRow("RAM-D5", "DDR5 High Speed", "7,500", "0");
            });

            // 1. PDF Export
            byte[] pdf = report.RenderToPdf();
            Assert.NotNull(pdf);
            Assert.True(pdf.Length > 300);
            string pdfText = Encoding.ASCII.GetString(pdf);
            Assert.StartsWith("%PDF-1.4", pdfText);
            Assert.Contains("INVENTORY INSPECTION MANIFEST", pdfText);

            // 2. ZPL Export
            string zpl = report.RenderToZpl();
            Assert.Contains("^XA", zpl);
            Assert.Contains("INVENTORY INSPECTION MANIFEST", zpl);
            Assert.Contains("Facility: Sector 7 Fab", zpl);
            Assert.Contains("^XZ", zpl);
        }
    }
}
