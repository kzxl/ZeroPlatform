using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ZeroUI.Core.Scada
{
    /// <summary>
    /// Alarm severity levels matching industrial standards (ISA-18.2 / EEMUA 191).
    /// </summary>
    public enum ScadaAlarmSeverity
    {
        Diagnostic = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    /// <summary>
    /// State machine lifecycle of an industrial alarm per ISA-18.2.
    /// </summary>
    public enum ScadaAlarmState
    {
        Normal = 0,
        ActiveUnacknowledged = 1,
        ActiveAcknowledged = 2,
        ClearedUnacknowledged = 3,
        Shelved = 4,
        Suppressed = 5
    }

    /// <summary>
    /// Snapshot record of an industrial alarm event.
    /// </summary>
    public sealed class ScadaAlarmRecord
    {
        public string Id { get; }
        public string TagPath { get; }
        public string Description { get; }
        public ScadaAlarmSeverity Severity { get; }
        public ScadaAlarmState State { get; internal set; }
        public object? TriggerValue { get; }
        public DateTime ActiveTimestamp { get; }
        public DateTime? AckTimestamp { get; internal set; }
        public DateTime? ClearedTimestamp { get; internal set; }
        public string? AckUser { get; internal set; }
        public DateTime? ShelveUntil { get; internal set; }

        public ScadaAlarmRecord(
            string id,
            string tagPath,
            string description,
            ScadaAlarmSeverity severity,
            ScadaAlarmState state = ScadaAlarmState.ActiveUnacknowledged,
            object? triggerValue = null,
            DateTime? activeTimestamp = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            TagPath = tagPath ?? throw new ArgumentNullException(nameof(tagPath));
            Description = description ?? "";
            Severity = severity;
            State = state;
            TriggerValue = triggerValue;
            ActiveTimestamp = activeTimestamp ?? DateTime.UtcNow;
        }

        public bool IsActive => State == ScadaAlarmState.ActiveUnacknowledged || State == ScadaAlarmState.ActiveAcknowledged;
        public bool NeedsAck => State == ScadaAlarmState.ActiveUnacknowledged || State == ScadaAlarmState.ClearedUnacknowledged;
    }

    /// <summary>
    /// Summary tally of alarms grouped by severity.
    /// </summary>
    public readonly struct AlarmSeverityCount
    {
        public int Diagnostic { get; }
        public int Low { get; }
        public int Medium { get; }
        public int High { get; }
        public int Critical { get; }
        public int TotalActive => Diagnostic + Low + Medium + High + Critical;

        public AlarmSeverityCount(int diag, int low, int med, int high, int crit)
        {
            Diagnostic = diag;
            Low = low;
            Medium = med;
            High = high;
            Critical = crit;
        }
    }

    /// <summary>
    /// Central thread-safe ISA-18.2 compliant Alarm Management Engine.
    /// Manages alarm lifecycles, acknowledgment, shelving, and operator audit trail.
    /// </summary>
    public static class ScadaAlarmEngine
    {
        private static readonly ConcurrentDictionary<string, ScadaAlarmRecord> _alarms =
            new ConcurrentDictionary<string, ScadaAlarmRecord>(StringComparer.OrdinalIgnoreCase);

        private static readonly object _eventLock = new object();

        /// <summary>
        /// Global notification when an alarm is raised, acknowledged, cleared, or shelved.
        /// </summary>
        public static event Action<ScadaAlarmRecord>? AlarmStateChanged;

        /// <summary>
        /// Raises or re-triggers an alarm for the specified ID.
        /// </summary>
        public static ScadaAlarmRecord RaiseAlarm(
            string id,
            string tagPath,
            string description,
            ScadaAlarmSeverity severity,
            object? triggerValue = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));

            var record = _alarms.AddOrUpdate(
                id,
                _ => new ScadaAlarmRecord(id, tagPath, description, severity, ScadaAlarmState.ActiveUnacknowledged, triggerValue),
                (_, existing) =>
                {
                    // If already shelved and not expired, remain shelved
                    if (existing.State == ScadaAlarmState.Shelved && existing.ShelveUntil.HasValue && existing.ShelveUntil.Value > DateTime.UtcNow)
                    {
                        return existing;
                    }

                    // Transition to ActiveUnack if previously Normal or Cleared
                    var updated = new ScadaAlarmRecord(id, tagPath, description, severity,
                        existing.State == ScadaAlarmState.ActiveAcknowledged
                            ? ScadaAlarmState.ActiveAcknowledged
                            : ScadaAlarmState.ActiveUnacknowledged,
                        triggerValue,
                        existing.ActiveTimestamp);
                    return updated;
                });

            NotifyChanged(record);
            return record;
        }

        /// <summary>
        /// Acknowledges an active or cleared alarm by an operator.
        /// </summary>
        public static bool Acknowledge(string id, string operatorName)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            if (_alarms.TryGetValue(id, out var record))
            {
                lock (record)
                {
                    if (record.State == ScadaAlarmState.ActiveUnacknowledged)
                    {
                        record.State = ScadaAlarmState.ActiveAcknowledged;
                        record.AckTimestamp = DateTime.UtcNow;
                        record.AckUser = operatorName;
                        NotifyChanged(record);
                        return true;
                    }
                    else if (record.State == ScadaAlarmState.ClearedUnacknowledged)
                    {
                        record.State = ScadaAlarmState.Normal;
                        record.AckTimestamp = DateTime.UtcNow;
                        record.AckUser = operatorName;
                        NotifyChanged(record);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Acknowledges all currently unacknowledged alarms.
        /// </summary>
        public static int AcknowledgeAll(string operatorName)
        {
            int ackCount = 0;
            foreach (var kvp in _alarms)
            {
                var rec = kvp.Value;
                if (rec.NeedsAck)
                {
                    if (Acknowledge(rec.Id, operatorName))
                    {
                        ackCount++;
                    }
                }
            }
            return ackCount;
        }

        /// <summary>
        /// Clears an alarm when physical condition returns to normal.
        /// </summary>
        public static bool ClearAlarm(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            if (_alarms.TryGetValue(id, out var record))
            {
                lock (record)
                {
                    record.ClearedTimestamp = DateTime.UtcNow;
                    if (record.State == ScadaAlarmState.ActiveUnacknowledged)
                    {
                        record.State = ScadaAlarmState.ClearedUnacknowledged;
                    }
                    else if (record.State == ScadaAlarmState.ActiveAcknowledged)
                    {
                        record.State = ScadaAlarmState.Normal;
                    }

                    NotifyChanged(record);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Shelves an alarm for a specified duration to prevent nuisance alarms during maintenance.
        /// </summary>
        public static bool ShelveAlarm(string id, TimeSpan duration)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            if (_alarms.TryGetValue(id, out var record))
            {
                lock (record)
                {
                    record.State = ScadaAlarmState.Shelved;
                    record.ShelveUntil = DateTime.UtcNow.Add(duration);
                    NotifyChanged(record);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns all alarms currently registered in the system.
        /// </summary>
        public static IReadOnlyList<ScadaAlarmRecord> GetAllAlarms()
        {
            return _alarms.Values.ToList().AsReadOnly();
        }

        /// <summary>
        /// Returns active alarms that require operator attention (ActiveUnack or ActiveAck).
        /// </summary>
        public static IReadOnlyList<ScadaAlarmRecord> GetActiveAlarms()
        {
            return _alarms.Values.Where(a => a.IsActive).OrderByDescending(a => a.Severity).ThenByDescending(a => a.ActiveTimestamp).ToList().AsReadOnly();
        }

        /// <summary>
        /// Aggregates count of currently active alarms by severity level.
        /// </summary>
        public static AlarmSeverityCount GetAlarmSummary()
        {
            int diag = 0, low = 0, med = 0, high = 0, crit = 0;
            foreach (var kvp in _alarms)
            {
                var r = kvp.Value;
                if (!r.IsActive) continue;

                switch (r.Severity)
                {
                    case ScadaAlarmSeverity.Diagnostic: diag++; break;
                    case ScadaAlarmSeverity.Low: low++; break;
                    case ScadaAlarmSeverity.Medium: med++; break;
                    case ScadaAlarmSeverity.High: high++; break;
                    case ScadaAlarmSeverity.Critical: crit++; break;
                }
            }

            return new AlarmSeverityCount(diag, low, med, high, crit);
        }

        private static void NotifyChanged(ScadaAlarmRecord record)
        {
            lock (_eventLock)
            {
                try
                {
                    AlarmStateChanged?.Invoke(record);
                }
                catch { }
            }
        }
    }
}
