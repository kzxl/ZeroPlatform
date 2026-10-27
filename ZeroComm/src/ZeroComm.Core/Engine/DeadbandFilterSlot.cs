using System;
using ZeroComm.Core.Abstractions;

namespace ZeroComm.Core.Engine
{
    /// <summary>
    /// Zero-allocation deadband filter slot for SCADA tag telemetry.
    /// Filters out negligible signal noise and suppresses redundant telemetry transmissions,
    /// while enforcing periodic heartbeat liveness events.
    /// </summary>
    public struct DeadbandFilterSlot
    {
        public int TagId { get; set; }
        public double DeadbandAbsolute { get; set; }
        public int HeartbeatIntervalMs { get; set; }

        public double LastReportedNumeric { get; private set; }
        public bool LastReportedBoolean { get; private set; }
        public long LastReportedTimestampMs { get; private set; }
        public bool HasReported { get; private set; }

        public DeadbandFilterSlot(int tagId, double deadbandAbsolute = 0.0, int heartbeatIntervalMs = 0)
        {
            TagId = tagId;
            DeadbandAbsolute = deadbandAbsolute;
            HeartbeatIntervalMs = heartbeatIntervalMs;
            LastReportedNumeric = 0.0;
            LastReportedBoolean = false;
            LastReportedTimestampMs = 0;
            HasReported = false;
        }

        /// <summary>
        /// Evaluates a new sample and determines whether it should be reported to the downstream SCADA pipeline.
        /// </summary>
        public bool Evaluate(in PlcTagValue sample, out PlcTagValue reportedValue)
        {
            reportedValue = default;

            if (sample.Quality != ScadaQuality.Good)
            {
                // Quality status changes must always be reported immediately
                reportedValue = sample;
                UpdateLastReported(sample);
                return true;
            }

            if (!HasReported)
            {
                // Initial baseline sample
                reportedValue = sample;
                UpdateLastReported(sample);
                return true;
            }

            // Check boolean edge transition
            if (sample.BooleanValue != LastReportedBoolean)
            {
                reportedValue = sample;
                UpdateLastReported(sample);
                return true;
            }

            // Check numeric deadband threshold
            if (DeadbandAbsolute > 0.0)
            {
                double delta = Math.Abs(sample.NumericValue - LastReportedNumeric);
                if (delta >= DeadbandAbsolute)
                {
                    reportedValue = sample;
                    UpdateLastReported(sample);
                    return true;
                }
            }
            else if (sample.NumericValue != LastReportedNumeric)
            {
                reportedValue = sample;
                UpdateLastReported(sample);
                return true;
            }

            // Check heartbeat timeout
            if (HeartbeatIntervalMs > 0)
            {
                long elapsed = sample.TimestampUtcMs - LastReportedTimestampMs;
                if (elapsed >= HeartbeatIntervalMs)
                {
                    reportedValue = sample;
                    UpdateLastReported(sample);
                    return true;
                }
            }

            return false;
        }

        private void UpdateLastReported(in PlcTagValue sample)
        {
            LastReportedNumeric = sample.NumericValue;
            LastReportedBoolean = sample.BooleanValue;
            LastReportedTimestampMs = sample.TimestampUtcMs;
            HasReported = true;
        }

        public void Reset()
        {
            HasReported = false;
            LastReportedTimestampMs = 0;
        }
    }
}
