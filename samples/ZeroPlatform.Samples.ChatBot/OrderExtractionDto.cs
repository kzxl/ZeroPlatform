using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroPlatform.Samples.ChatBot
{
    public sealed class OrderItemDraft
    {
        public int LineIndex { get; set; }
        public string RawDescription { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Warehouse { get; set; } = "Kho Tổng";
        public decimal Quantity { get; set; } = 1;
        public string Unit { get; set; } = "Cái";
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount => Quantity * UnitPrice;
        public float Confidence { get; set; } = 0.95f;
        public bool IsLowConfidence => Confidence < 0.85f;

        public OrderItemDraft Clone() => new()
        {
            LineIndex = LineIndex,
            RawDescription = RawDescription,
            Sku = Sku,
            ItemName = ItemName,
            Warehouse = Warehouse,
            Quantity = Quantity,
            Unit = Unit,
            UnitPrice = UnitPrice,
            Confidence = Confidence
        };
    }

    public sealed class OrderDraft
    {
        public string OrderCode { get; set; } = "DH" + DateTime.Now.ToString("yyyyMMdd-HHmm");
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCode { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public DateTime PromiseDate { get; set; } = DateTime.Now.AddDays(3);
        public string DefaultWarehouse { get; set; } = "Kho Tổng";
        public string Notes { get; set; } = string.Empty;
        public List<OrderItemDraft> Items { get; set; } = new();
        public decimal TotalAmount => Items.Sum(i => i.TotalAmount);

        public OrderDraft Clone() => new()
        {
            OrderCode = OrderCode,
            CustomerName = CustomerName,
            CustomerCode = CustomerCode,
            OrderDate = OrderDate,
            PromiseDate = PromiseDate,
            DefaultWarehouse = DefaultWarehouse,
            Notes = Notes,
            Items = Items.Select(i => i.Clone()).ToList()
        };
    }

    /// <summary>
    /// Snapshot for atomic undo/redo operations in Conversational Order Entry.
    /// </summary>
    public sealed class OrderDraftSnapshot
    {
        public DateTime Timestamp { get; } = DateTime.Now;
        public string Description { get; }
        public OrderDraft State { get; }

        public OrderDraftSnapshot(string description, OrderDraft state)
        {
            Description = description;
            State = state.Clone();
        }
    }
}
