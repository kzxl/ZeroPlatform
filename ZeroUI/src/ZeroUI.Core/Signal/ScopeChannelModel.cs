using System;

namespace ZeroUI.Core.Signal
{
    public enum ScopeChannelType
    {
        Analog,
        DigitalLogic
    }

    public enum TriggerSlope
    {
        RisingEdge,
        FallingEdge
    }

    public enum TriggerMode
    {
        Auto,
        Normal,
        Single
    }

    /// <summary>
    /// Represents an individual oscilloscope analog or digital logic trace.
    /// </summary>
    public class ScopeChannel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ScopeChannelType ChannelType { get; set; } = ScopeChannelType.Analog;
        public uint ColorArgb { get; set; } = 0xFFFACC15; // Oscilloscope Ch1 Yellow default
        public bool IsVisible { get; set; } = true;

        public float VoltsPerDiv { get; set; } = 1.0f;
        public float VerticalOffsetDiv { get; set; } = 0.0f;
        public string Unit { get; set; } = "V";

        public SignalRingBuffer Buffer { get; }

        private long _lastMetricsTicks;
        private float _cachedMin;
        private float _cachedMax;
        private float _cachedP2P;
        private float _cachedRms;

        public void GetOrComputeMetrics(out float min, out float max, out float p2p, out float rms)
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (_lastMetricsTicks == 0 || (now - _lastMetricsTicks) * 1000 / System.Diagnostics.Stopwatch.Frequency >= 200)
            {
                Buffer.ComputeMetrics(out _cachedMin, out _cachedMax, out _cachedP2P, out _cachedRms);
                _lastMetricsTicks = now;
            }
            min = _cachedMin;
            max = _cachedMax;
            p2p = _cachedP2P;
            rms = _cachedRms;
        }

        public ScopeChannel(int id, string name, ScopeChannelType type = ScopeChannelType.Analog, uint color = 0xFFFACC15, int bufferCapacity = 65536)
        {
            Id = id;
            Name = name;
            ChannelType = type;
            ColorArgb = color;
            Buffer = new SignalRingBuffer(bufferCapacity);
        }
    }

    /// <summary>
    /// Hardware-style signal trigger configuration for stable waveform capture.
    /// </summary>
    public class ScopeTrigger
    {
        public int ChannelId { get; set; } = 1;
        public float Threshold { get; set; } = 0.0f;
        public TriggerSlope Slope { get; set; } = TriggerSlope.RisingEdge;
        public TriggerMode Mode { get; set; } = TriggerMode.Auto;
        public bool IsTriggered { get; set; } = false;
    }

    /// <summary>
    /// Precision dual measurement cursors for delta-time and delta-voltage readouts.
    /// </summary>
    public class ScopeCursor
    {
        public bool IsEnabled { get; set; } = true;

        // Horizontal cursors (Time)
        public double X1 { get; set; } = 0.25; // 0.0 to 1.0 normalized screen width
        public double X2 { get; set; } = 0.75;

        // Vertical cursors (Voltage)
        public double Y1 { get; set; } = 0.35; // 0.0 to 1.0 normalized screen height
        public double Y2 { get; set; } = 0.65;

        public double DeltaX => Math.Abs(X2 - X1);
        public double DeltaY => Math.Abs(Y2 - Y1);

        public double CalculateDeltaTime(double totalVisibleTimeSec) => DeltaX * totalVisibleTimeSec;
        public double CalculateFrequency(double totalVisibleTimeSec)
        {
            double dt = CalculateDeltaTime(totalVisibleTimeSec);
            return dt > 1e-9 ? 1.0 / dt : 0.0;
        }
    }
}
