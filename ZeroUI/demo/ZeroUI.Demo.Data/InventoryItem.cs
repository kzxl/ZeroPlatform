using System;

namespace ZeroUI.Demo.Data
{
    public struct InventoryItem
    {
        public bool IsActive;
        public string Category;
        public int Id;
        public string ItemCode;
        public string ItemName;
        public int Quantity;
        public double UnitPrice;
        public double TotalAmount;
        public float YieldRate;
        public string LotNumber;
        public string Status;

        public InventoryItem(int id, string code, string name, int qty, double price, string lot, string status, string category = "Standard", bool active = true, float yieldRate = 0.98f)
        {
            Id = id;
            ItemCode = code;
            ItemName = name;
            Quantity = qty;
            UnitPrice = price;
            TotalAmount = qty * price;
            LotNumber = lot;
            Status = status;
            Category = category;
            IsActive = active;
            YieldRate = yieldRate;
        }

        public InventoryItem(bool active, string category, int id, string code, string name, int qty, double price, float yieldRate, string lot, string status)
        {
            IsActive = active;
            Category = category;
            Id = id;
            ItemCode = code;
            ItemName = name;
            Quantity = qty;
            UnitPrice = price;
            TotalAmount = qty * price;
            YieldRate = yieldRate;
            LotNumber = lot;
            Status = status;
        }
    }
}
