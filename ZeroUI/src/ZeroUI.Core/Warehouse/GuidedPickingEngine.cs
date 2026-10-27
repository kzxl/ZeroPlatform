using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroUI.Core.Warehouse
{
    /// <summary>
    /// Inventory allocation and route prioritization strategy.
    /// </summary>
    public enum PickingStrategy
    {
        /// <summary>
        /// Shortest physical travel distance using nearest-neighbor heuristic.
        /// </summary>
        ShortestDistance,

        /// <summary>
        /// Prioritize oldest inbound lots (First-In, First-Out) before distance.
        /// </summary>
        Fifo,

        /// <summary>
        /// Prioritize closest expiration dates (First-Expired, First-Out) before distance.
        /// </summary>
        Fefo
    }

    /// <summary>
    /// Represents an individual item in a guided warehouse pick wave.
    /// </summary>
    public sealed class PickTaskItem
    {
        public string TaskId { get; }
        public string SkuCode { get; }
        public string LotNumber { get; }
        public WarehouseLocation Location { get; }
        public double RequestedQuantity { get; }
        public double PickedQuantity { get; set; }
        public DateTime InboundDate { get; }
        public DateTime ExpiryDate { get; }

        public bool IsCompleted => PickedQuantity >= (RequestedQuantity - 0.0001);

        public PickTaskItem(
            string taskId,
            string skuCode,
            string lotNumber,
            WarehouseLocation location,
            double requestedQuantity,
            DateTime inboundDate,
            DateTime expiryDate)
        {
            TaskId = taskId ?? Guid.NewGuid().ToString("N");
            SkuCode = skuCode ?? throw new ArgumentNullException(nameof(skuCode));
            LotNumber = lotNumber ?? string.Empty;
            Location = location ?? throw new ArgumentNullException(nameof(location));
            RequestedQuantity = requestedQuantity;
            InboundDate = inboundDate;
            ExpiryDate = expiryDate;
        }
    }

    /// <summary>
    /// Guided warehouse picking and putaway optimization engine.
    /// Enforces FEFO/FIFO compliance, barcode verification, and route optimization.
    /// </summary>
    public static class GuidedPickingEngine
    {
        /// <summary>
        /// Optimizes the sequential order of pick tasks based on the chosen strategy.
        /// </summary>
        public static IReadOnlyList<PickTaskItem> OptimizePickSequence(
            IEnumerable<PickTaskItem> items,
            WarehouseLocation? startingLocation = null,
            PickingStrategy strategy = PickingStrategy.ShortestDistance)
        {
            if (items == null) return Array.Empty<PickTaskItem>();

            var list = items.Where(i => !i.IsCompleted).ToList();
            if (list.Count <= 1) return list;

            switch (strategy)
            {
                case PickingStrategy.Fefo:
                    // Sort by Expiry Date ascending, then by Aisle/Rack
                    return list.OrderBy(i => i.ExpiryDate)
                               .ThenBy(i => i.Location.Aisle)
                               .ThenBy(i => i.Location.Rack)
                               .ToList();

                case PickingStrategy.Fifo:
                    // Sort by Inbound Date ascending, then by Aisle/Rack
                    return list.OrderBy(i => i.InboundDate)
                               .ThenBy(i => i.Location.Aisle)
                               .ThenBy(i => i.Location.Rack)
                               .ToList();

                default:
                    // Shortest distance: Nearest neighbor heuristic
                    return SolveNearestNeighbor(list, startingLocation ?? list[0].Location);
            }
        }

        /// <summary>
        /// Verifies whether a scanned barcode matches the expected SKU code, Lot number, or Location.
        /// Supports concatenated GS1 formats (e.g. "SKU|LOT" or pure SKU match).
        /// </summary>
        public static bool VerifyScan(PickTaskItem item, string scannedBarcode, out string? errorMessage)
        {
            errorMessage = null;
            if (item == null)
            {
                errorMessage = "No active pick task item specified.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(scannedBarcode))
            {
                errorMessage = "Scanned barcode is empty.";
                return false;
            }

            string clean = scannedBarcode.Trim();

            // Check exact SKU or Lot match
            if (string.Equals(clean, item.SkuCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(clean, item.LotNumber, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Check composite barcode format: SKU:LOT or SKU|LOT
            var parts = clean.Split(new[] { '|', ':', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                if (string.Equals(parts[0], item.SkuCode, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(parts[1], item.LotNumber, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            errorMessage = $"Mismatch: Scanned '{clean}' does not match SKU '{item.SkuCode}' or Lot '{item.LotNumber}'.";
            return false;
        }

        /// <summary>
        /// Confirms a pick operation, incrementing picked quantity.
        /// </summary>
        public static bool ConfirmPick(PickTaskItem item, double quantityPicked, out string? error)
        {
            error = null;
            if (item == null)
            {
                error = "Task item cannot be null.";
                return false;
            }

            if (quantityPicked <= 0)
            {
                error = "Picked quantity must be greater than zero.";
                return false;
            }

            double remaining = item.RequestedQuantity - item.PickedQuantity;
            if (quantityPicked > remaining + 0.0001)
            {
                error = $"Overpick error: Requested remaining is {remaining}, but picked {quantityPicked}.";
                return false;
            }

            item.PickedQuantity += quantityPicked;
            return true;
        }

        private static List<PickTaskItem> SolveNearestNeighbor(List<PickTaskItem> items, WarehouseLocation start)
        {
            var unvisited = new HashSet<PickTaskItem>(items);
            var route = new List<PickTaskItem>(items.Count);
            var currentLoc = start;

            while (unvisited.Count > 0)
            {
                PickTaskItem? nearest = null;
                double minDistance = double.MaxValue;

                foreach (var item in unvisited)
                {
                    double dist = currentLoc.CalculateManhattanDistance(item.Location);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearest = item;
                    }
                }

                if (nearest != null)
                {
                    unvisited.Remove(nearest);
                    route.Add(nearest);
                    currentLoc = nearest.Location;
                }
                else
                {
                    break;
                }
            }

            return route;
        }
    }
}
