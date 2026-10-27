namespace ZeroAgent.Tools.Storage
{
    /// <summary>
    /// Represents an individual time-series telemetry data point.
    /// </summary>
    public sealed class TsdbDataPoint
    {
        public long TimestampUnixMs { get; set; }
        public double Value { get; set; }

        public TsdbDataPoint(long timestampUnixMs, double value)
        {
            TimestampUnixMs = timestampUnixMs;
            Value = value;
        }
    }
}
