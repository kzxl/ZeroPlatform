using System;
using System.Collections.Generic;

namespace ZeroUI.WinForms.Warehouse.Models
{
    #region Barcode Scanning Models

    public class BarcodeScanResult
    {
        public string RawBarcode { get; set; } = "";
        public string ProductCode { get; set; } = "";
        public string LotNumber { get; set; } = "";
        public decimal Quantity { get; set; } = 1;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsHardwareScanner { get; set; } = false;
        public bool IsValid { get; set; } = true;
        public string ErrorMessage { get; set; } = "";
    }

    public class BarcodeScanEventArgs : EventArgs
    {
        public BarcodeScanResult Result { get; }

        public BarcodeScanEventArgs(BarcodeScanResult result)
        {
            Result = result;
        }
    }

    #endregion

    #region Inventory Card Models

    public class InventoryStockModel
    {
        public string ProductCode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public decimal AvailableQuantity { get; set; } = 0;
        public decimal WaitingQuantity { get; set; } = 0;
        public decimal ReservedQuantity { get; set; } = 0;
        public decimal TotalQuantity => AvailableQuantity + WaitingQuantity + ReservedQuantity;
        public string WarehouseCode { get; set; } = "WH01";
        public string WarehouseName { get; set; } = "Main Central Warehouse";
        public string LocationBin { get; set; } = "Zone A - Rack 03";
        public string UnitOfMeasure { get; set; } = "Pcs";
    }

    #endregion

    #region Lot Allocation Models

    public enum LotStatus
    {
        Available,
        Quarantined,
        Expired,
        LowStock
    }

    public enum LotAllocationStrategy
    {
        FIFO, // First In, First Out (oldest ImportDate first)
        FEFO, // First Expired, First Out (earliest ExpiryDate first)
        Manual
    }

    public class LotItemModel
    {
        public string LotNumber { get; set; } = "";
        public decimal AvailableQuantity { get; set; } = 0;
        public decimal TotalQuantity { get; set; } = 0;
        public DateTime ImportDate { get; set; } = DateTime.Today;
        public DateTime ExpiryDate { get; set; } = DateTime.Today.AddMonths(12);
        public LotStatus Status { get; set; } = LotStatus.Available;
        public decimal AllocatedQuantity { get; set; } = 0;
        public bool IsSelected { get; set; } = false;
    }

    public class SelectedLotModel
    {
        public string LotNumber { get; set; } = "";
        public decimal AllocatedQuantity { get; set; } = 0;
    }

    #endregion

    #region Stock Movement Timeline Models

    public enum StockMovementType
    {
        Inward,             // Initial receipt
        OutwardProduction,  // Production dispatch
        OutwardSales,       // Sales shipment
        Transfer,           // Warehouse transfer
        Balance             // Current inventory balance
    }

    public class StockMovementNode
    {
        public string Id { get; set; } = "";
        public StockMovementType Type { get; set; } = StockMovementType.Inward;
        public string Title { get; set; } = "";
        public string ReferenceNo { get; set; } = "";
        public decimal Quantity { get; set; } = 0;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string DestinationOrSource { get; set; } = "";
        public string Note { get; set; } = "";
    }

    public class StockMovementTraceModel
    {
        public string ProductCode { get; set; } = "";
        public string LotNumber { get; set; } = "";
        public string WarehouseCode { get; set; } = "";
        public List<StockMovementNode> Nodes { get; set; } = new List<StockMovementNode>();

        public decimal TotalInward
        {
            get
            {
                decimal sum = 0;
                for (int i = 0; i < Nodes.Count; i++)
                {
                    if (Nodes[i].Type == StockMovementType.Inward) sum += Nodes[i].Quantity;
                }
                return sum;
            }
        }

        public decimal TotalOutward
        {
            get
            {
                decimal sum = 0;
                for (int i = 0; i < Nodes.Count; i++)
                {
                    if (Nodes[i].Type == StockMovementType.OutwardProduction ||
                        Nodes[i].Type == StockMovementType.OutwardSales ||
                        Nodes[i].Type == StockMovementType.Transfer)
                    {
                        sum += Nodes[i].Quantity;
                    }
                }
                return sum;
            }
        }

        public decimal CurrentBalance => TotalInward - TotalOutward;
    }

    #endregion
}
