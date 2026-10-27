using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ZeroUI.Core.Runtime;

namespace ZeroUI.Core.Mes
{
    /// <summary>
    /// Downtime classification categories following the industrial 5M+1E standard.
    /// </summary>
    public enum DowntimeCategory
    {
        MachineBreakdown,
        MethodChangeover,
        MaterialShortage,
        OperatorShortage,
        MeasurementQualityInspection,
        EnvironmentPowerUtility
    }

    /// <summary>
    /// Immutable record of a downtime interval.
    /// </summary>
    public sealed class DowntimeEntry
    {
        public DowntimeCategory Category { get; }
        public string Reason { get; }
        public TimeSpan Duration { get; }
        public DateTime Timestamp { get; }

        public DowntimeEntry(DowntimeCategory category, string reason, TimeSpan duration, DateTime? timestamp = null)
        {
            Category = category;
            Reason = reason ?? string.Empty;
            Duration = duration;
            Timestamp = timestamp ?? DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Immutable snapshot of computed OEE factors.
    /// </summary>
    public readonly struct OeeSnapshot
    {
        public double Availability { get; }
        public double Performance { get; }
        public double Quality { get; }
        public double OverallOee { get; }

        public TimeSpan PlannedProductionTime { get; }
        public TimeSpan OperatingTime { get; }
        public TimeSpan TotalDowntime { get; }

        public long TotalUnits { get; }
        public long GoodUnits { get; }
        public long DefectUnits { get; }

        public OeeSnapshot(
            double availability,
            double performance,
            double quality,
            double overallOee,
            TimeSpan plannedTime,
            TimeSpan operatingTime,
            TimeSpan totalDowntime,
            long totalUnits,
            long goodUnits,
            long defectUnits)
        {
            Availability = availability;
            Performance = performance;
            Quality = quality;
            OverallOee = overallOee;
            PlannedProductionTime = plannedTime;
            OperatingTime = operatingTime;
            TotalDowntime = totalDowntime;
            TotalUnits = totalUnits;
            GoodUnits = goodUnits;
            DefectUnits = defectUnits;
        }

        public override string ToString()
            => $"OEE: {OverallOee:P1} (A: {Availability:P1} | P: {Performance:P1} | Q: {Quality:P1})";
    }

    /// <summary>
    /// Real-time Overall Equipment Effectiveness (OEE) calculation engine.
    /// Computes Availability x Performance x Quality, tracks downtime intervals,
    /// and synchronizes performance KPIs into StateStore.
    /// </summary>
    public sealed class OeeEngine
    {
        private readonly string _machineId;
        private readonly double _idealCycleTimeSeconds;
        private readonly object _lock = new object();

        private TimeSpan _plannedProductionTime;
        private readonly List<DowntimeEntry> _downtimeHistory = new List<DowntimeEntry>();
        private long _totalUnits;
        private long _goodUnits;
        private long _defectUnits;

        public string MachineId => _machineId;
        public double IdealCycleTimeSeconds => _idealCycleTimeSeconds;

        public OeeEngine(string machineId, double idealCycleTimeSeconds, TimeSpan initialPlannedTime)
        {
            if (idealCycleTimeSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(idealCycleTimeSeconds), "Ideal cycle time must be positive.");

            _machineId = machineId ?? throw new ArgumentNullException(nameof(machineId));
            _idealCycleTimeSeconds = idealCycleTimeSeconds;
            _plannedProductionTime = initialPlannedTime;
        }

        /// <summary>
        /// Registers a downtime interval with category and root cause reason.
        /// </summary>
        public void RecordDowntime(DowntimeCategory category, string reason, TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero) return;

            lock (_lock)
            {
                _downtimeHistory.Add(new DowntimeEntry(category, reason, duration));
            }

            PublishKpisToStateStore();
        }

        /// <summary>
        /// Updates piece counts (Total = Good + Defect).
        /// </summary>
        public void RecordProduction(long goodCount, long defectCount)
        {
            lock (_lock)
            {
                _goodUnits += Math.Max(0, goodCount);
                _defectUnits += Math.Max(0, defectCount);
                _totalUnits = _goodUnits + _defectUnits;
            }

            PublishKpisToStateStore();
        }

        /// <summary>
        /// Updates planned production time (e.g. shift duration minus planned maintenance).
        /// </summary>
        public void SetPlannedProductionTime(TimeSpan plannedTime)
        {
            lock (_lock)
            {
                _plannedProductionTime = plannedTime;
            }

            PublishKpisToStateStore();
        }

        /// <summary>
        /// Calculates the current real-time OEE snapshot.
        /// </summary>
        public OeeSnapshot CalculateSnapshot()
        {
            lock (_lock)
            {
                TimeSpan totalDowntime = TimeSpan.Zero;
                for (int i = 0; i < _downtimeHistory.Count; i++)
                {
                    totalDowntime += _downtimeHistory[i].Duration;
                }

                TimeSpan operatingTime = _plannedProductionTime > totalDowntime
                    ? _plannedProductionTime - totalDowntime
                    : TimeSpan.Zero;

                // Availability = Operating Time / Planned Production Time
                double availability = _plannedProductionTime.TotalSeconds > 0
                    ? Math.Min(1.0, Math.Max(0.0, operatingTime.TotalSeconds / _plannedProductionTime.TotalSeconds))
                    : 0.0;

                // Performance = (Total Units * Ideal Cycle Time) / Operating Time
                double performance = 0.0;
                if (operatingTime.TotalSeconds > 0)
                {
                    double expectedOperatingTime = _totalUnits * _idealCycleTimeSeconds;
                    performance = Math.Min(1.0, Math.Max(0.0, expectedOperatingTime / operatingTime.TotalSeconds));
                }

                // Quality = Good Units / Total Units
                double quality = _totalUnits > 0
                    ? Math.Min(1.0, Math.Max(0.0, (double)_goodUnits / _totalUnits))
                    : 1.0;

                // OEE = Availability * Performance * Quality
                double oee = availability * performance * quality;

                return new OeeSnapshot(
                    availability,
                    performance,
                    quality,
                    oee,
                    _plannedProductionTime,
                    operatingTime,
                    totalDowntime,
                    _totalUnits,
                    _goodUnits,
                    _defectUnits);
            }
        }

        private void PublishKpisToStateStore()
        {
            var snapshot = CalculateSnapshot();
            StateStore.Default.SetState($"OEE.{_machineId}.Overall", snapshot.OverallOee);
            StateStore.Default.SetState($"OEE.{_machineId}.Availability", snapshot.Availability);
            StateStore.Default.SetState($"OEE.{_machineId}.Performance", snapshot.Performance);
            StateStore.Default.SetState($"OEE.{_machineId}.Quality", snapshot.Quality);
            StateStore.Default.SetState($"OEE.{_machineId}.TotalUnits", snapshot.TotalUnits);
            StateStore.Default.SetState($"OEE.{_machineId}.GoodUnits", snapshot.GoodUnits);
            StateStore.Default.SetState($"OEE.{_machineId}.DefectUnits", snapshot.DefectUnits);
        }
    }
}
