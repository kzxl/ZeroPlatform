using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ZeroUI.Core.Runtime;

namespace ZeroUI.Core.Scada
{
    /// <summary>
    /// Type of industrial alarm condition.
    /// </summary>
    public enum AlarmConditionType : byte
    {
        HighHigh = 0,
        High = 1,
        Low = 2,
        LowLow = 3,
        Discrete = 4,
        Deviation = 5
    }

    /// <summary>
    /// Configuration definition for an analog or discrete alarm rule.
    /// </summary>
    public sealed class AlarmRuleDefinition
    {
        public string Id { get; }
        public int TagId { get; }
        public string TagPath { get; }
        public string Description { get; }
        public AlarmConditionType ConditionType { get; }
        public ScadaAlarmSeverity Severity { get; }
        public double LimitValue { get; }
        public double Deadband { get; }
        public bool IsDiscreteTriggerHigh { get; }

        public AlarmRuleDefinition(
            string id,
            int tagId,
            string tagPath,
            string description,
            AlarmConditionType conditionType,
            ScadaAlarmSeverity severity,
            double limitValue = 0.0,
            double deadband = 0.0,
            bool isDiscreteTriggerHigh = true)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            TagId = tagId;
            TagPath = tagPath ?? string.Empty;
            Description = description ?? id;
            ConditionType = conditionType;
            Severity = severity;
            LimitValue = limitValue;
            Deadband = Math.Max(0.0, deadband);
            IsDiscreteTriggerHigh = isDiscreteTriggerHigh;
        }

        public bool EvaluateIsActive(in ScadaValue value, bool currentlyActive)
        {
            if (ConditionType == AlarmConditionType.Discrete)
            {
                bool flag = value.AsBoolean();
                return IsDiscreteTriggerHigh ? flag : !flag;
            }

            double num = value.AsDouble();
            double hysteresis = currentlyActive ? Deadband : 0.0;

            switch (ConditionType)
            {
                case AlarmConditionType.HighHigh:
                case AlarmConditionType.High:
                    return num >= (LimitValue - hysteresis);

                case AlarmConditionType.LowLow:
                case AlarmConditionType.Low:
                    return num <= (LimitValue + hysteresis);

                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Autonomous ISA-18.2 compliant industrial alarm runtime.
    /// Evaluates limit conditions with deadbands, manages acknowledgment lifecycles,
    /// and streams alarm notifications through <see cref="ZeroTelemetryBus"/>.
    /// </summary>
    public sealed class ZeroAlarmRuntime
    {
        private static readonly Lazy<ZeroAlarmRuntime> _shared =
            new Lazy<ZeroAlarmRuntime>(() => new ZeroAlarmRuntime("Shared", ZeroTelemetryBus.Shared));

        public static ZeroAlarmRuntime Shared => _shared.Value;

        private readonly string _name;
        private readonly ZeroTelemetryBus? _bus;

        private readonly ConcurrentDictionary<string, AlarmRuleDefinition> _rules =
            new ConcurrentDictionary<string, AlarmRuleDefinition>(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<int, List<AlarmRuleDefinition>> _tagRules =
            new ConcurrentDictionary<int, List<AlarmRuleDefinition>>();

        private readonly ConcurrentDictionary<string, ScadaAlarmRecord> _activeAlarms =
            new ConcurrentDictionary<string, ScadaAlarmRecord>(StringComparer.OrdinalIgnoreCase);

        public event Action<ScadaAlarmRecord>? AlarmTriggered;
        public event Action<ScadaAlarmRecord>? AlarmCleared;
        public event Action<ScadaAlarmRecord>? AlarmAcknowledged;

        public string Name => _name;
        public int TotalRules => _rules.Count;
        public int ActiveAlarmCount => _activeAlarms.Count;

        public ZeroAlarmRuntime(string name = "AlarmRuntime", ZeroTelemetryBus? bus = null)
        {
            _name = name;
            _bus = bus;
        }

        #region Rule Registration

        public void RegisterRule(AlarmRuleDefinition rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));

            _rules[rule.Id] = rule;
            _tagRules.AddOrUpdate(
                rule.TagId,
                _ => new List<AlarmRuleDefinition> { rule },
                (_, list) =>
                {
                    lock (list)
                    {
                        var copy = new List<AlarmRuleDefinition>(list) { rule };
                        return copy;
                    }
                });
        }

        public void RegisterAnalogLimits(
            string prefixId,
            int tagId,
            string tagPath,
            string baseDesc,
            double? lowLow = null,
            double? low = null,
            double? high = null,
            double? highHigh = null,
            double deadband = 0.0)
        {
            if (lowLow.HasValue)
                RegisterRule(new AlarmRuleDefinition($"{prefixId}_LL", tagId, tagPath, $"{baseDesc} (Low-Low)", AlarmConditionType.LowLow, ScadaAlarmSeverity.Critical, lowLow.Value, deadband));

            if (low.HasValue)
                RegisterRule(new AlarmRuleDefinition($"{prefixId}_L", tagId, tagPath, $"{baseDesc} (Low)", AlarmConditionType.Low, ScadaAlarmSeverity.Medium, low.Value, deadband));

            if (high.HasValue)
                RegisterRule(new AlarmRuleDefinition($"{prefixId}_H", tagId, tagPath, $"{baseDesc} (High)", AlarmConditionType.High, ScadaAlarmSeverity.Medium, high.Value, deadband));

            if (highHigh.HasValue)
                RegisterRule(new AlarmRuleDefinition($"{prefixId}_HH", tagId, tagPath, $"{baseDesc} (High-High)", AlarmConditionType.HighHigh, ScadaAlarmSeverity.Critical, highHigh.Value, deadband));
        }

        #endregion

        #region Evaluation Loop

        /// <summary>
        /// Evaluates all rules bound to the specified tag against an incoming value.
        /// Zero heap allocations when conditions are unchanged.
        /// </summary>
        public void Evaluate(int tagId, in ScadaValue value)
        {
            if (!_tagRules.TryGetValue(tagId, out var rules)) return;

            List<AlarmRuleDefinition> snapshot;
            lock (rules)
            {
                snapshot = rules;
            }

            for (int i = 0; i < snapshot.Count; i++)
            {
                var rule = snapshot[i];
                bool isCurrentlyActive = _activeAlarms.TryGetValue(rule.Id, out var existing);
                bool shouldBeActive = rule.EvaluateIsActive(in value, isCurrentlyActive);

                if (shouldBeActive && !isCurrentlyActive)
                {
                    // New alarm trigger!
                    var record = new ScadaAlarmRecord(
                        rule.Id,
                        rule.TagPath,
                        rule.Description,
                        rule.Severity,
                        ScadaAlarmState.ActiveUnacknowledged,
                        value.AsDouble(),
                        DateTime.UtcNow);

                    _activeAlarms[rule.Id] = record;
                    AlarmTriggered?.Invoke(record);
                    _bus?.Publish("alarms", record);
                }
                else if (!shouldBeActive && isCurrentlyActive)
                {
                    // Condition cleared!
                    if (existing != null)
                    {
                        existing.ClearedTimestamp = DateTime.UtcNow;

                        if (existing.State == ScadaAlarmState.ActiveAcknowledged)
                        {
                            // Acknowledged + Cleared -> Return to Normal
                            existing.State = ScadaAlarmState.Normal;
                            _activeAlarms.TryRemove(rule.Id, out _);
                        }
                        else
                        {
                            // Unacknowledged + Cleared -> ClearedUnacknowledged (latched until acked)
                            existing.State = ScadaAlarmState.ClearedUnacknowledged;
                        }

                        AlarmCleared?.Invoke(existing);
                        _bus?.Publish("alarms", existing);
                    }
                }
            }
        }

        #endregion

        #region Operator Actions (Acknowledge, Shelve)

        public bool Acknowledge(string alarmId, string user)
        {
            if (_activeAlarms.TryGetValue(alarmId, out var record))
            {
                record.AckTimestamp = DateTime.UtcNow;
                record.AckUser = user;

                if (record.State == ScadaAlarmState.ClearedUnacknowledged)
                {
                    record.State = ScadaAlarmState.Normal;
                    _activeAlarms.TryRemove(alarmId, out _);
                }
                else
                {
                    record.State = ScadaAlarmState.ActiveAcknowledged;
                }

                AlarmAcknowledged?.Invoke(record);
                _bus?.Publish("alarms", record);
                return true;
            }
            return false;
        }

        public bool Shelve(string alarmId, TimeSpan duration, string user)
        {
            if (_activeAlarms.TryGetValue(alarmId, out var record))
            {
                record.State = ScadaAlarmState.Shelved;
                record.ShelveUntil = DateTime.UtcNow + duration;
                record.AckUser = user;
                _bus?.Publish("alarms", record);
                return true;
            }
            return false;
        }

        public int ActiveCount => _activeAlarms.Count;

        public int UnacknowledgedCount
        {
            get
            {
                int count = 0;
                foreach (var kvp in _activeAlarms)
                {
                    if (kvp.Value.State == ScadaAlarmState.ActiveUnacknowledged ||
                        kvp.Value.State == ScadaAlarmState.ClearedUnacknowledged)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public void AcknowledgeAll(string user)
        {
            foreach (var kvp in _activeAlarms)
            {
                Acknowledge(kvp.Key, user);
            }
        }

        public IReadOnlyList<ScadaAlarmRecord> GetActiveAlarms()
        {
            return new List<ScadaAlarmRecord>(_activeAlarms.Values);
        }

        public AlarmSeverityCount GetSeverityCounts()
        {
            int diag = 0, low = 0, med = 0, high = 0, crit = 0;

            foreach (var kvp in _activeAlarms)
            {
                switch (kvp.Value.Severity)
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

        #endregion
    }
}
