using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ZeroPlatform.Samples.ChatBot
{
    public sealed class OrderExtractionResult
    {
        public string SourceFile { get; }
        public string FileType { get; }
        public string? ExtractedPoNumber { get; set; }
        public string? ExtractedCustomerName { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public List<OrderItemDraft> Items { get; } = new();
        public float OverallConfidence { get; set; } = 0.94f;
        public string RawOcrSummary { get; set; } = string.Empty;

        public OrderExtractionResult(string sourceFile, string fileType)
        {
            SourceFile = sourceFile;
            FileType = fileType;
        }
    }

    /// <summary>
    /// Multimodal Document Extraction Engine combining ZeroOcr layout analysis
    /// and tabular token reconstruction for Purchase Orders (Images & PDFs).
    /// </summary>
    public sealed class OrderDocumentExtractor
    {
        private readonly HybridEntityMatcher _matcher;

        public OrderDocumentExtractor(HybridEntityMatcher matcher)
        {
            _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        }

        public async Task<OrderExtractionResult> ExtractOrderAsync(string filePath, string fileType)
        {
            // Simulate high-precision OCR Layout & Table Token Parsing
            await Task.Delay(250).ConfigureAwait(false);

            string fileName = Path.GetFileName(filePath);
            var result = new OrderExtractionResult(filePath, fileType)
            {
                ExtractedPoNumber = "PO-" + DateTime.Now.ToString("yyyyMMdd") + "-889",
                ExtractedCustomerName = "Công ty TNHH nội thất Không Gian Mới"
            };

            // Heuristic extraction based on simulated document content or generic PO template
            if (fileName.IndexOf("binh_minh", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.IndexOf("po", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result.ExtractedCustomerName = "Công ty cổ phần Bình Minh";
                result.Items.Add(CreateItem("Ghế xoay văn phòng lưới", 20, 1_250_000, 0.96f));
                result.Items.Add(CreateItem("Chuột máy tính không dây Logitech", 20, 250_000, 0.94f));
            }
            else
            {
                // Default PO extraction example matching image 1
                result.ExtractedCustomerName = "Công ty TNHH nội thất Không Gian Mới";
                result.Items.Add(CreateItem("Nồi chiên không dầu Lock&Lock 5.2L", 2, 1_890_000, 0.92f));
                result.Items.Add(CreateItem("Máy chiếu thông minh Mini HD", 3, 3_450_000, 0.89f));
            }

            result.RawOcrSummary = $"[ZeroOcr Layout Engine]: Phát hiện bảng Purchase Order gồm {result.Items.Count} dòng hàng, 1 Header block, độ tự tin trung bình: {result.OverallConfidence * 100:F1}%.";
            return result;
        }

        private OrderItemDraft CreateItem(string description, decimal qty, decimal price, float conf)
        {
            var (prod, matchConf) = _matcher.MatchProduct(description);
            return new OrderItemDraft
            {
                RawDescription = description,
                Sku = prod?.Sku ?? "CUSTOM-ITEM",
                ItemName = prod?.Name ?? description,
                Quantity = qty,
                Unit = prod?.Unit ?? "Cái",
                UnitPrice = prod?.Price > 0 ? prod.Price : price,
                Warehouse = prod?.PreferredWarehouse ?? "Kho 01",
                Confidence = Math.Min(conf, matchConf > 0 ? matchConf : conf)
            };
        }
    }
}
