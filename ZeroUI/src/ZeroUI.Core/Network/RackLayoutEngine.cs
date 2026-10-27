using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Network
{
    public enum RackUnitCategory
    {
        Server,
        Switch,
        Router,
        PatchPanel,
        Pdu,
        Ups,
        StorageArray,
        CableManager,
        BlankCover
    }

    public enum RackSlotStatus
    {
        Normal,
        Warning,
        Critical,
        Offline
    }

    public enum RackViewFace
    {
        Front,
        Rear
    }

    public class RackSlotItem
    {
        public int StartUnit { get; set; } = 1;
        public int UnitHeight { get; set; } = 1;
        public string Name { get; set; } = "Device";
        public string Model { get; set; } = string.Empty;
        public RackUnitCategory Category { get; set; } = RackUnitCategory.Server;
        public double PowerDrawWatts { get; set; } = 250;
        public double TemperatureCelsius { get; set; } = 28;
        public double WeightKg { get; set; } = 15;
        public RackSlotStatus Status { get; set; } = RackSlotStatus.Normal;
        public string AssetTag { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;

        public int EndUnit => StartUnit + UnitHeight - 1;

        public bool ContainsUnit(int unit)
        {
            return unit >= StartUnit && unit <= EndUnit;
        }
    }

    /// <summary>
    /// Pure computation engine for 19-inch equipment racks (12U, 24U, 42U, 48U).
    /// Handles slot positioning, thermal elevation gradients, and power/weight load rollups.
    /// </summary>
    public class RackLayoutEngine
    {
        private readonly List<RackSlotItem> _items = new List<RackSlotItem>();

        public int TotalUnits { get; set; } = 42;
        public double MaxWeightCapacityKg { get; set; } = 800.0;
        public double PduBreakerCapacityWatts { get; set; } = 10000.0;
        public RackViewFace ViewFace { get; set; } = RackViewFace.Front;

        public IReadOnlyList<RackSlotItem> Items => _items;

        public void AddItem(RackSlotItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!CanPlaceItem(item.StartUnit, item.UnitHeight, out string reason))
            {
                throw new InvalidOperationException($"Cannot place item: {reason}");
            }
            _items.Add(item);
        }

        public bool RemoveItem(RackSlotItem item)
        {
            return _items.Remove(item);
        }

        public void Clear()
        {
            _items.Clear();
        }

        public RackSlotItem? FindItemAtUnit(int unit)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].ContainsUnit(unit))
                {
                    return _items[i];
                }
            }
            return null;
        }

        public bool CanPlaceItem(int startUnit, int unitHeight, out string reason)
        {
            if (startUnit < 1 || startUnit > TotalUnits)
            {
                reason = $"Start unit {startUnit} is out of bounds (1..{TotalUnits}).";
                return false;
            }

            int endUnit = startUnit + unitHeight - 1;
            if (endUnit > TotalUnits)
            {
                reason = $"Item spans up to {endUnit}U, exceeding rack capacity of {TotalUnits}U.";
                return false;
            }

            for (int u = startUnit; u <= endUnit; u++)
            {
                var existing = FindItemAtUnit(u);
                if (existing != null)
                {
                    reason = $"Unit {u} is already occupied by '{existing.Name}' ({existing.StartUnit}U..{existing.EndUnit}U).";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        public double CalculateTotalPowerDrawWatts()
        {
            double sum = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                sum += _items[i].PowerDrawWatts;
            }
            return sum;
        }

        public double CalculateTotalWeightKg()
        {
            double sum = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                sum += _items[i].WeightKg;
            }
            return sum;
        }

        public double CalculatePowerUtilizationPercent()
        {
            if (PduBreakerCapacityWatts <= 0) return 0;
            return Math.Min(100.0, (CalculateTotalPowerDrawWatts() / PduBreakerCapacityWatts) * 100.0);
        }

        public double CalculateWeightUtilizationPercent()
        {
            if (MaxWeightCapacityKg <= 0) return 0;
            return Math.Min(100.0, (CalculateTotalWeightKg() / MaxWeightCapacityKg) * 100.0);
        }

        /// <summary>
        /// Calculates estimated thermal elevation (°C) for a given unit level (1..TotalUnits).
        /// Combines ambient inlet temperature, local equipment heat dissipation, and upward convection accumulation.
        /// </summary>
        public double CalculateThermalAtUnit(int unit, double ambientTempCelsius = 20.0)
        {
            if (unit < 1 || TotalUnits <= 0) return ambientTempCelsius;

            // Thermal convection factor: heat naturally accumulates towards top of rack (+3 to +8°C)
            double heightRatio = (double)(unit - 1) / Math.Max(1, TotalUnits - 1);
            double convectionRise = heightRatio * 5.5;

            // Local device temperature impact
            var item = FindItemAtUnit(unit);
            if (item != null)
            {
                return item.TemperatureCelsius;
            }

            // Interpolate from nearest items if blank
            double localHeat = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                int dist = Math.Abs(_items[i].StartUnit - unit);
                if (dist <= 3)
                {
                    double weight = (4 - dist) / 4.0;
                    localHeat += Math.Max(0, _items[i].TemperatureCelsius - ambientTempCelsius) * weight * 0.35;
                }
            }

            return ambientTempCelsius + convectionRise + localHeat;
        }
    }
}
