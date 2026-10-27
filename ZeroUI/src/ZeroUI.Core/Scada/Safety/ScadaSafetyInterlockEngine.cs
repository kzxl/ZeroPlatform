using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ZeroUI.Core.Scada.Safety
{
    /// <summary>
    /// Classification of safety interlock trip conditions.
    /// </summary>
    public enum SafetyTripCondition
    {
        /// <summary>Trips when numeric value exceeds HighLimit.</summary>
        AboveHighLimit,

        /// <summary>Trips when numeric value drops below LowLimit.</summary>
        BelowLowLimit,

        /// <summary>Trips when boolean value matches ExpectedTripValue.</summary>
        BooleanMatch,

        /// <summary>Custom high-speed predicate delegate.</summary>
        CustomPredicate
    }

    /// <summary>
    /// Definition of a high-speed safety interlock rule evaluated in Tier 1 (< 1 µs latency).
    /// </summary>
    public sealed class SafetyInterlockRule
    {
        public string RuleId { get; }
        public string TagPath { get; }
        public int TagId { get; internal set; } = -1;
        public string Description { get; }
        public SafetyTripCondition Condition { get; }
        public double ThresholdValue { get; }
        public bool ExpectedTripValue { get; }
        public Func<double, bool>? CustomPredicate { get; }
        public Action<SafetyInterlockRule, double>? OnTripped { get; }
        public string? OutputTripTagPath { get; }
        public int OutputTripTagId { get; internal set; } = -1;

        private int _isTripped;
        public bool IsTripped => Volatile.Read(ref _isTripped) == 1;

        public SafetyInterlockRule(
            string ruleId,
            string tagPath,
            string description,
            SafetyTripCondition condition,
            double thresholdValue = 0.0,
            bool expectedTripValue = true,
            Func<double, bool>? customPredicate = null,
            Action<SafetyInterlockRule, double>? onTripped = null,
            string? outputTripTagPath = null)
        {
            RuleId = ruleId ?? throw new ArgumentNullException(nameof(ruleId));
            TagPath = tagPath ?? throw new ArgumentNullException(nameof(tagPath));
            Description = description ?? string.Empty;
            Condition = condition;
            ThresholdValue = thresholdValue;
            ExpectedTripValue = expectedTripValue;
            CustomPredicate = customPredicate;
            OnTripped = onTripped;
            OutputTripTagPath = outputTripTagPath;
        }

        public void Reset()
        {
            Volatile.Write(ref _isTripped, 0);
        }

        internal bool Evaluate(double value)
        {
            bool tripped = false;
            switch (Condition)
            {
                case SafetyTripCondition.AboveHighLimit:
                    tripped = value > ThresholdValue;
                    break;
                case SafetyTripCondition.BelowLowLimit:
                    tripped = value < ThresholdValue;
                    break;
                case SafetyTripCondition.BooleanMatch:
                    tripped = (value != 0.0) == ExpectedTripValue;
                    break;
                case SafetyTripCondition.CustomPredicate:
                    tripped = CustomPredicate != null && CustomPredicate(value);
                    break;
            }

            if (tripped)
            {
                if (Interlocked.Exchange(ref _isTripped, 1) == 0)
                {
                    OnTripped?.Invoke(this, value);
                    return true; // Newly tripped
                }
            }
            else
            {
                Volatile.Write(ref _isTripped, 0);
            }

            return false;
        }
    }

    /// <summary>
    /// Ultra-low latency safety interlock engine evaluated in the Fast Tier (10 kHz).
    /// Executes within sub-microsecond time (< 1 µs) to guarantee equipment and personnel safety.
    /// </summary>
    public sealed class ScadaSafetyInterlockEngine
    {
        private static readonly Lazy<ScadaSafetyInterlockEngine> _shared =
            new Lazy<ScadaSafetyInterlockEngine>(() => new ScadaSafetyInterlockEngine());

        public static ScadaSafetyInterlockEngine Shared => _shared.Value;

        private readonly ConcurrentDictionary<string, SafetyInterlockRule> _rulesById =
            new ConcurrentDictionary<string, SafetyInterlockRule>(StringComparer.OrdinalIgnoreCase);

        // Fast lookup by TagId for zero-alloc evaluation in SetNumeric
        private SafetyInterlockRule[][] _rulesByTagId = new SafetyInterlockRule[1024][];
        private readonly object _syncLock = new object();

        private long _evaluationsCount;
        private long _tripsCount;

        public long EvaluationsCount => Interlocked.Read(ref _evaluationsCount);
        public long TripsCount => Interlocked.Read(ref _tripsCount);

        /// <summary>
        /// Registers a safety interlock rule into the fast evaluation engine.
        /// </summary>
        public void RegisterRule(SafetyInterlockRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));

            rule.TagId = ZeroTagEngine.GetOrRegisterTag(rule.TagPath);
            if (!string.IsNullOrEmpty(rule.OutputTripTagPath))
            {
                rule.OutputTripTagId = ZeroTagEngine.GetOrRegisterTag(rule.OutputTripTagPath!);
            }

            _rulesById[rule.RuleId] = rule;
            RebuildTagIndex();
        }

        /// <summary>
        /// Removes a safety interlock rule by its identifier.
        /// </summary>
        public bool RemoveRule(string ruleId)
        {
            if (string.IsNullOrEmpty(ruleId)) return false;
            if (_rulesById.TryRemove(ruleId, out _))
            {
                RebuildTagIndex();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Resets all tripped safety rules.
        /// </summary>
        public void ResetAll()
        {
            foreach (var rule in _rulesById.Values)
            {
                rule.Reset();
            }
        }

        /// <summary>
        /// Fast-path evaluation executed on Tier 1 (10 kHz) telemetry ingestion.
        /// Latency target: < 1 µs per tag update. Zero heap allocations.
        /// </summary>
        public bool EvaluateTag(int tagId, double value)
        {
            Interlocked.Increment(ref _evaluationsCount);

            var array = _rulesByTagId;
            if (tagId < 0 || tagId >= array.Length) return false;

            var rules = array[tagId];
            if (rules == null || rules.Length == 0) return false;

            bool anyTripped = false;
            for (int i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                if (rule.Evaluate(value))
                {
                    anyTripped = true;
                    Interlocked.Increment(ref _tripsCount);

                    if (rule.OutputTripTagId >= 0)
                    {
                        // Write trip tag directly in Fast Tier
                        ZeroTagEngine.SetBoolean(rule.OutputTripTagId, true);
                    }
                }
            }

            return anyTripped;
        }

        private void RebuildTagIndex()
        {
            lock (_syncLock)
            {
                var tagMap = new Dictionary<int, List<SafetyInterlockRule>>();
                int maxTagId = 0;

                foreach (var rule in _rulesById.Values)
                {
                    if (!tagMap.TryGetValue(rule.TagId, out var list))
                    {
                        list = new List<SafetyInterlockRule>();
                        tagMap[rule.TagId] = list;
                    }
                    list.Add(rule);
                    if (rule.TagId > maxTagId) maxTagId = rule.TagId;
                }

                int newSize = Math.Max(maxTagId + 1, _rulesByTagId.Length);
                var newArray = new SafetyInterlockRule[newSize][];

                foreach (var kvp in tagMap)
                {
                    newArray[kvp.Key] = kvp.Value.ToArray();
                }

                _rulesByTagId = newArray;
            }
        }
    }
}
