using System;
using System.Text.RegularExpressions;

namespace ZeroUI.Core.Warehouse
{
    /// <summary>
    /// Represents a 6-level hierarchical warehouse storage location:
    /// Warehouse -> Zone -> Aisle -> Rack -> Shelf -> Bin.
    /// Provides distance heuristics and load capacity validation for picking/putaway engines.
    /// </summary>
    public sealed class WarehouseLocation : IEquatable<WarehouseLocation>
    {
        public string WarehouseId { get; }
        public string ZoneId { get; }
        public int Aisle { get; }
        public int Rack { get; }
        public int Shelf { get; }
        public int Bin { get; }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public double MaxCapacityKg { get; set; }
        public double CurrentWeightKg { get; set; }
        public bool IsLocked { get; set; }

        public string LocationCode => $"{WarehouseId}-{ZoneId}-A{Aisle:D2}-R{Rack:D2}-S{Shelf:D2}-B{Bin:D2}";
        public bool IsOccupied => CurrentWeightKg > 0.001;

        public WarehouseLocation(
            string warehouseId,
            string zoneId,
            int aisle,
            int rack,
            int shelf,
            int bin,
            double x = 0.0,
            double y = 0.0,
            double z = 0.0,
            double maxCapacityKg = 1000.0)
        {
            WarehouseId = warehouseId ?? "WH1";
            ZoneId = zoneId ?? "A";
            Aisle = Math.Max(1, aisle);
            Rack = Math.Max(1, rack);
            Shelf = Math.Max(1, shelf);
            Bin = Math.Max(1, bin);
            X = x;
            Y = y;
            Z = z;
            MaxCapacityKg = maxCapacityKg;
        }

        /// <summary>
        /// Parses a standardized location code string into a WarehouseLocation instance.
        /// Example: "WH1-DRY-A02-R05-S03-B01"
        /// </summary>
        public static WarehouseLocation Parse(string locationCode)
        {
            if (string.IsNullOrWhiteSpace(locationCode))
                throw new ArgumentNullException(nameof(locationCode));

            var match = Regex.Match(locationCode.Trim(), @"^([A-Z0-9]+)-([A-Z0-9]+)-A(\d+)-R(\d+)-S(\d+)-B(\d+)$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                throw new FormatException($"Invalid warehouse location code format: '{locationCode}'. Expected 'WH-ZONE-A01-R01-S01-B01'.");
            }

            string wh = match.Groups[1].Value.ToUpperInvariant();
            string zone = match.Groups[2].Value.ToUpperInvariant();
            int aisle = int.Parse(match.Groups[3].Value);
            int rack = int.Parse(match.Groups[4].Value);
            int shelf = int.Parse(match.Groups[5].Value);
            int bin = int.Parse(match.Groups[6].Value);

            // Compute default synthetic coordinates if not specified
            double x = aisle * 3.0;
            double y = rack * 1.5;
            double z = shelf * 1.2;

            return new WarehouseLocation(wh, zone, aisle, rack, shelf, bin, x, y, z);
        }

        /// <summary>
        /// Calculates the Manhattan traveling distance between two warehouse locations.
        /// |X1 - X2| + |Y1 - Y2| + |Z1 - Z2|
        /// </summary>
        public double CalculateManhattanDistance(WarehouseLocation other)
        {
            if (other == null) return double.MaxValue;
            return Math.Abs(X - other.X) + Math.Abs(Y - other.Y) + Math.Abs(Z - other.Z);
        }

        /// <summary>
        /// Checks whether the location has available capacity for the specified weight.
        /// </summary>
        public bool CanAccommodate(double additionalWeightKg)
        {
            if (IsLocked) return false;
            return (CurrentWeightKg + additionalWeightKg) <= MaxCapacityKg;
        }

        public bool Equals(WarehouseLocation? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(LocationCode, other.LocationCode, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj) => Equals(obj as WarehouseLocation);
        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(LocationCode);
        public override string ToString() => LocationCode;
    }
}
