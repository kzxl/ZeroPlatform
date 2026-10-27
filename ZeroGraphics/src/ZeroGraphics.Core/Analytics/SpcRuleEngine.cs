using System;
using System.Collections.Generic;

namespace ZeroGraphics.Core.Analytics
{
    public enum SpcRuleViolation
    {
        None = 0,
        /// <summary>
        /// Rule 1: One point is beyond Zone A (outside 3-sigma limits: > UCL or &lt; LCL).
        /// Indicates a special cause or catastrophic failure.
        /// </summary>
        Rule1_Beyond3Sigma = 1,

        /// <summary>
        /// Rule 2: Nine points in a row on the same side of the center line.
        /// Indicates a process mean shift.
        /// </summary>
        Rule2_RunOf9 = 2,

        /// <summary>
        /// Rule 3: Six points in a row continually increasing or continually decreasing.
        /// Indicates a trend (tool wear, chemical degradation, or thermal drift).
        /// </summary>
        Rule3_TrendOf6 = 3,

        /// <summary>
        /// Rule 4: Fourteen points in a row alternating up and down.
        /// Indicates systematic oscillation (e.g. alternating shifts or dual raw material feeds).
        /// </summary>
        Rule4_Alternating14 = 4
    }

    public enum SpcViolationSeverity
    {
        Info,
        Warning,
        Critical
    }

    public struct SpcAlarm
    {
        public int SampleIndex;
        public float Value;
        public SpcRuleViolation Rule;
        public SpcViolationSeverity Severity;
        public string Message;

        public SpcAlarm(int sampleIndex, float value, SpcRuleViolation rule, SpcViolationSeverity severity, string message)
        {
            SampleIndex = sampleIndex;
            Value = value;
            Rule = rule;
            Severity = severity;
            Message = message;
        }

        public override string ToString() => $"[Index {SampleIndex}] {Severity}: {Rule} - {Message} (Val={Value:F3})";
    }

    /// <summary>
    /// Automated quality control detection engine implementing Western Electric and Nelson SPC rules.
    /// Evaluates real-time sensor and inspection streams to detect out-of-control conditions immediately.
    /// </summary>
    public static class SpcRuleEngine
    {
        /// <summary>
        /// Evaluates samples against SPC control limits and flags all rule violations.
        /// </summary>
        /// <param name="samples">Array of measurement samples.</param>
        /// <param name="summary">SPC baseline summary containing mean and control limits.</param>
        /// <returns>List of detected alarms.</returns>
        public static List<SpcAlarm> EvaluateRules(float[] samples, SpcSummary summary)
        {
            List<SpcAlarm> alarms = new List<SpcAlarm>();
            if (samples == null || samples.Length == 0) return alarms;

            float cl = summary.CenterLine;
            float ucl = summary.UCL;
            float lcl = summary.LCL;

            int n = samples.Length;

            for (int i = 0; i < n; i++)
            {
                float val = samples[i];

                // Rule 1: One point beyond 3-sigma (UCL / LCL)
                if (val > ucl)
                {
                    alarms.Add(new SpcAlarm(i, val, SpcRuleViolation.Rule1_Beyond3Sigma, SpcViolationSeverity.Critical,
                        $"Point {val:F3} exceeds UCL {ucl:F3}"));
                }
                else if (val < lcl)
                {
                    alarms.Add(new SpcAlarm(i, val, SpcRuleViolation.Rule1_Beyond3Sigma, SpcViolationSeverity.Critical,
                        $"Point {val:F3} falls below LCL {lcl:F3}"));
                }

                // Rule 2: Nine points in a row on the same side of the center line
                if (i >= 8)
                {
                    bool allAbove = true;
                    bool allBelow = true;
                    for (int k = i - 8; k <= i; k++)
                    {
                        if (samples[k] <= cl) allAbove = false;
                        if (samples[k] >= cl) allBelow = false;
                    }

                    if (allAbove)
                    {
                        alarms.Add(new SpcAlarm(i, val, SpcRuleViolation.Rule2_RunOf9, SpcViolationSeverity.Warning,
                            "9 consecutive points above Center Line (Process shift upward)"));
                    }
                    else if (allBelow)
                    {
                        alarms.Add(new SpcAlarm(i, val, SpcRuleViolation.Rule2_RunOf9, SpcViolationSeverity.Warning,
                            "9 consecutive points below Center Line (Process shift downward)"));
                    }
                }

                // Rule 3: Six points in a row continually increasing or decreasing
                if (i >= 5)
                {
                    bool increasing = true;
                    bool decreasing = true;
                    for (int k = i - 4; k <= i; k++)
                    {
                        if (samples[k] <= samples[k - 1]) increasing = false;
                        if (samples[k] >= samples[k - 1]) decreasing = false;
                    }

                    if (increasing)
                    {
                        alarms.Add(new SpcAlarm(i, val, SpcRuleViolation.Rule3_TrendOf6, SpcViolationSeverity.Warning,
                            "6 consecutive points continually increasing (Upward drift)"));
                    }
                    else if (decreasing)
                    {
                        alarms.Add(new SpcAlarm(i, val, SpcRuleViolation.Rule3_TrendOf6, SpcViolationSeverity.Warning,
                            "6 consecutive points continually decreasing (Downward drift)"));
                    }
                }

                // Rule 4: Fourteen points in a row alternating up and down
                if (i >= 13)
                {
                    bool alternating = true;
                    for (int k = i - 12; k <= i; k++)
                    {
                        bool prevUp = samples[k - 1] > samples[k - 2];
                        bool currUp = samples[k] > samples[k - 1];
                        if (prevUp == currUp)
                        {
                            alternating = false;
                            break;
                        }
                    }

                    if (alternating)
                    {
                        alarms.Add(new SpcAlarm(i, val, SpcRuleViolation.Rule4_Alternating14, SpcViolationSeverity.Warning,
                            "14 consecutive points alternating up and down (Systematic oscillation)"));
                    }
                }
            }

            return alarms;
        }
    }
}
