using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using ZeroData.Core;
using ZeroReports.Barcodes;
using ZeroReports.Data;
using ZeroReports.Engine;
using ZeroReports.Engine.Formatting;
using ZeroReports.Engine.Pagination;
using ZeroReports.Engine.Table;
using ZeroReports.Export;

namespace ZeroReports.Tests
{
    public class BandedReportTests
    {
        public class OrderModel
        {
            public int Id { get; set; }
            public string Customer { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public string Status { get; set; } = "PASS";
            public List<OrderItemModel> Items { get; set; } = new List<OrderItemModel>();
        }

        public class OrderItemModel
        {
            public string Sku { get; set; } = string.Empty;
            public int Qty { get; set; }
            public decimal Price { get; set; }
        }

        [Fact]
        public void BandedReport_Engine_Header_Detail_Summary_CalculatesCorrectly()
        {
            var report = new Report
            {
                Title = "Sales Summary Report",
                PageSettings = new ReportPageSettings(595.28f, 841.89f, 20f, 20f, 20f, 20f)
            };

            // Report Header
            report.ReportHeader.Height = 40f;
            report.ReportHeader.AddLabel("COMPANY SALES REPORT", 0f, 10f, 500f, 20f, fontSize: 16f, isBold: true);

            // Page Header
            report.PageHeader.Height = 25f;
            report.PageHeader.AddLabel("Customer", 0f, 5f, 150f, 15f, fontSize: 10f, isBold: true);
            report.PageHeader.AddLabel("Category", 150f, 5f, 150f, 15f, fontSize: 10f, isBold: true);
            report.PageHeader.AddLabel("Amount", 300f, 5f, 100f, 15f, fontSize: 10f, isBold: true, align: ReportTextAlignment.Right);
            report.PageHeader.AddLine(0f, 22f, 555f, 22f);

            // Group Header
            report.GroupHeader.Height = 20f;
            report.GroupHeader.GroupField = "Category";
            report.GroupHeader.AddExpression("Category: [Category]", 0f, 2f, 300f, 16f, fontSize: 11f, isBold: true);

            // Detail
            report.Detail.Height = 20f;
            report.Detail.AddExpression("[Customer]", 0f, 2f, 150f, 16f);
            report.Detail.AddExpression("[Category]", 150f, 2f, 150f, 16f);
            report.Detail.AddExpression("[Amount:C0]", 300f, 2f, 100f, 16f, fontSize: 10f, isBold: false, alignment: ReportTextAlignment.Right);

            // Group Footer
            report.GroupFooter.Height = 25f;
            report.GroupFooter.AddLine(0f, 2f, 555f, 2f);
            report.GroupFooter.AddExpression("Group Count: [COUNT(Id)] | Subtotal: [SUM(Amount):C0]", 0f, 5f, 400f, 16f, fontSize: 10f, isBold: true);

            // Report Footer
            report.ReportFooter.Height = 30f;
            report.ReportFooter.AddLine(0f, 2f, 555f, lineWidth: 2f);
            report.ReportFooter.AddExpression("Grand Total: [SUM(Amount):C0]", 200f, 6f, 200f, 18f, fontSize: 12f, isBold: true, alignment: ReportTextAlignment.Right);

            var orders = new List<OrderModel>
            {
                new OrderModel { Id = 1, Customer = "Acme Corp", Category = "Electronics", Amount = 1250000m },
                new OrderModel { Id = 2, Customer = "Beta LLC", Category = "Electronics", Amount = 850000m },
                new OrderModel { Id = 3, Customer = "Gamma Ltd", Category = "Machinery", Amount = 3400000m },
            };

            report.DataSource = new ObjectReportDataSource<OrderModel>(orders);

            var doc = PageLayoutEngine.Render(report);

            Assert.NotNull(doc);
            Assert.True(doc.TotalPages >= 1);

            // Find rendered text items
            var allTexts = doc.Pages.SelectMany(p => p.Items.OfType<RenderedTextItem>()).ToList();

            Assert.Contains(allTexts, t => t.Text.Contains("COMPANY SALES REPORT"));
            Assert.Contains(allTexts, t => t.Text.Contains("Acme Corp"));
            Assert.Contains(allTexts, t => t.Text.Contains("Grand Total:"));
            // Grand total format: 1.25M + 850k + 3.4M = 5.5M
            Assert.Contains(allTexts, t => t.Text.Contains("5,500,000") || t.Text.Contains("5.500.000") || t.Text.Contains("5500000"));
        }

        [Fact]
        public void ReportTable_ColSpan_CellBorders_AutoWidth_LayoutsAccurately()
        {
            var report = new Report();
            report.Detail.Height = 60f;

            var table = new ReportTable(0f, 0f, 500f);
            table.SetColumns(100f, 200f, 200f);

            var bg = (0.95f, 0.95f, 0.95f);

            // Row 1: Header with ColSpan 2
            var row1 = new ReportRow(25f);
            row1.Cells.Add(new ReportCell { Text = "Spanned Header", ColSpan = 2, IsBold = true, Borders = CellBorders.All, BackgroundColor = bg });
            row1.Cells.Add(new ReportCell { Text = "Right Header", ColSpan = 1, IsBold = true, Borders = CellBorders.All, BackgroundColor = bg });
            table.Rows.Add(row1);

            // Row 2: Regular cells
            var row2 = new ReportRow(25f);
            row2.Cells.Add(new ReportCell { Text = "C1", Borders = CellBorders.All, BackgroundColor = bg });
            row2.Cells.Add(new ReportCell { Text = "C2", Borders = CellBorders.All, BackgroundColor = bg });
            row2.Cells.Add(new ReportCell { Text = "C3", Borders = CellBorders.All, BackgroundColor = bg });
            table.Rows.Add(row2);

            report.Detail.AddTable(table);

            var doc = PageLayoutEngine.Render(report);

            var firstPage = doc.Pages[0];
            var boxes = firstPage.Items.OfType<RenderedBoxItem>().ToList();
            var texts = firstPage.Items.OfType<RenderedTextItem>().ToList();
            var lines = firstPage.Items.OfType<RenderedLineItem>().ToList();

            // Row 1 has 2 cells (one with width 300 = 100+200, one with 200), Row 2 has 3 cells
            Assert.Equal(5, boxes.Count);
            var spannedCell = boxes.FirstOrDefault(b => Math.Abs(b.Width - 300f) < 0.1f);
            Assert.NotNull(spannedCell);

            Assert.True(lines.Count > 0); // Borders rendered as lines
            Assert.Contains(texts, t => t.Text == "Spanned Header");
            Assert.Contains(texts, t => t.Text == "Right Header");
            Assert.Contains(texts, t => t.Text == "C1");
        }

        [Fact]
        public void ConditionalFormattingRule_HighlightFailures_ChangesColors()
        {
            var report = new Report();
            report.Detail.Height = 25f;

            var label = report.Detail.AddExpression("[Customer] - [Status]", 0f, 0f, 300f, 20f);
            label.FormattingRules.Add(new ConditionalFormattingRule("Status", "==", "FAIL")
            {
                TextColor = (1f, 0f, 0f), // Red
                IsBold = true
            });

            var data = new List<OrderModel>
            {
                new OrderModel { Customer = "Normal User", Status = "PASS" },
                new OrderModel { Customer = "Bad User", Status = "FAIL" }
            };

            report.DataSource = new ObjectReportDataSource<OrderModel>(data);

            var doc = PageLayoutEngine.Render(report);
            var texts = doc.Pages[0].Items.OfType<RenderedTextItem>().ToList();

            var normalText = texts.FirstOrDefault(t => t.Text.Contains("Normal User"));
            var failText = texts.FirstOrDefault(t => t.Text.Contains("Bad User"));

            Assert.NotNull(normalText);
            Assert.NotNull(failText);

            // Normal text should not be red
            Assert.Equal(0f, normalText.Color.R);

            // Fail text should be formatted red and bold
            Assert.Equal(1f, failText.Color.R);
            Assert.True(failText.IsBold);
        }

        [Fact]
        public void Barcodes_PureCSharp_Code128_QrCode_Ean13_EncodeCorrectly()
        {
            // 1. Code 128
            var c128 = Code128Encoder.Encode("CODE128-TEST");
            Assert.NotNull(c128);
            Assert.NotEmpty(c128.Bars);
            Assert.True(c128.TotalWidth > 0);

            // 2. QR Code
            var qr = QrCodeEncoder.Encode("https://zerouniverse.vn");
            Assert.NotNull(qr);
            Assert.True(qr.Size >= 21); // Minimum QR version 1 is 21x21
            // Top-left finder pattern: (0,0) is black (true), (1,1) is white separator (false), (3,3) is center (true)
            Assert.True(qr[0, 0]);
            Assert.False(qr[1, 1]);
            Assert.True(qr[3, 3]);

            // 3. EAN 13 (95 data modules + 18 quiet zone modules = 113 modules)
            var ean = Ean13Encoder.Encode("893850597401"); // 12 digits
            Assert.NotNull(ean);
            Assert.NotEmpty(ean.Bars);
            Assert.Equal(113f, ean.TotalWidth);
        }

        [Fact]
        public void DataFrameReportDataSource_ZeroCopyBinding()
        {
            var df = new DataFrame();
            df.AddColumn(new DataColumn<int>("ProductId", new[] { 101, 102 }));
            df.AddColumn(new DataColumn<string>("ProductName", new[] { "Industrial Sensor X1", "Servo Controller Z2" }));
            df.AddColumn(new DataColumn<double>("UnitPrice", new[] { 450.50, 890.00 }));

            var report = new Report();
            report.Detail.Height = 20f;
            report.Detail.AddExpression("[ProductId]", 0f, 0f, 50f, 18f);
            report.Detail.AddExpression("[ProductName]", 60f, 0f, 200f, 18f);
            report.Detail.AddExpression("[UnitPrice:C2]", 270f, 0f, 100f, 18f, fontSize: 10f, isBold: false, alignment: ReportTextAlignment.Right);

            report.DataSource = new DataFrameReportDataSource(df);

            var doc = PageLayoutEngine.Render(report);
            var texts = doc.Pages[0].Items.OfType<RenderedTextItem>().ToList();

            Assert.Contains(texts, t => t.Text == "101");
            Assert.Contains(texts, t => t.Text == "Industrial Sensor X1");
            Assert.Contains(texts, t => t.Text == "102");
            Assert.Contains(texts, t => t.Text == "Servo Controller Z2");
        }

        [Fact]
        public void DetailReportBand_MasterDetail_RendersHierarchicalLevels()
        {
            var orders = new List<OrderModel>
            {
                new OrderModel
                {
                    Id = 1,
                    Customer = "Apex Industries",
                    Items = new List<OrderItemModel>
                    {
                        new OrderItemModel { Sku = "SKU-001", Qty = 5, Price = 100m },
                        new OrderItemModel { Sku = "SKU-002", Qty = 2, Price = 250m }
                    }
                }
            };

            var report = new Report();
            report.Detail.Height = 25f;
            report.Detail.AddExpression("PO #[Id]: [Customer]", 0f, 0f, 300f, 20f, fontSize: 10f, isBold: true);

            var detailBand = new DetailReportBand("Items");
            detailBand.Detail.Height = 18f;
            detailBand.Detail.AddExpression("  -> Item: [Sku] x [Qty] @ [Price:C0]", 20f, 0f, 300f, 16f);

            report.AddDetailReport(detailBand);
            report.DataSource = new ObjectReportDataSource<OrderModel>(orders);

            var doc = PageLayoutEngine.Render(report);
            var texts = doc.Pages[0].Items.OfType<RenderedTextItem>().ToList();

            Assert.Contains(texts, t => t.Text.Contains("PO #1: Apex Industries"));
            Assert.Contains(texts, t => t.Text.Contains("Item: SKU-001 x 5"));
            Assert.Contains(texts, t => t.Text.Contains("Item: SKU-002 x 2"));
        }

        [Fact]
        public void Watermark_PdfAndHtml_GeneratesValidOutputs()
        {
            var report = new Report();
            report.Detail.Height = 50f;
            report.Detail.AddLabel("TOP SECRET DOCUMENT", 50f, 20f, 300f, 25f, fontSize: 14f);
            report.SetWatermark("CONFIDENTIAL", opacity: 0.2f, fontSize: 60f);

            var doc = PageLayoutEngine.Render(report);

            Assert.Single(doc.Pages);
            var wmItem = doc.Pages[0].Items.OfType<RenderedWatermarkItem>().FirstOrDefault();
            Assert.NotNull(wmItem);
            Assert.Equal("CONFIDENTIAL", wmItem.Text);

            // Export to PDF
            byte[] pdfBytes = PdfReportExporter.Export(doc);
            Assert.NotNull(pdfBytes);
            Assert.True(pdfBytes.Length > 100);
            string pdfHeader = Encoding.ASCII.GetString(pdfBytes.Take(8).ToArray());
            Assert.StartsWith("%PDF-1.4", pdfHeader);

            // Export to HTML
            string html = HtmlReportExporter.Export(doc);
            Assert.NotNull(html);
            Assert.Contains("report-watermark", html);
            Assert.Contains("CONFIDENTIAL", html);
        }
    }
}
