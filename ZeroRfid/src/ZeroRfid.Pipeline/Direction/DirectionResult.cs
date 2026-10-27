using System;

namespace ZeroRfid.Pipeline.Direction;

/// <summary>
/// Direction classification of a tag traversing an RFID portal gate.
/// </summary>
public enum GateDirection
{
    /// <summary>Insufficient spatial/temporal data to evaluate direction.</summary>
    Unknown = 0,

    /// <summary>Tag moved from outer zone to inner zone (e.g. Inbound / Receiving).</summary>
    Inbound = 1,

    /// <summary>Tag moved from inner zone to outer zone (e.g. Outbound / Shipping).</summary>
    Outbound = 2,

    /// <summary>Tag remained in the gate zone without directional movement.</summary>
    Stationary = 3
}

/// <summary>
/// Evaluation result of a tag passing through an RFID portal gate.
/// </summary>
public sealed class DirectionEvaluation
{
    public string Epc { get; }
    public GateDirection Direction { get; }
    public double Confidence { get; }
    public int InboundAntenna { get; }
    public int OutboundAntenna { get; }
    public DateTime FirstSeenUtc { get; }
    public DateTime LastSeenUtc { get; }
    public double DurationMs => (LastSeenUtc - FirstSeenUtc).TotalMilliseconds;

    public DirectionEvaluation(
        string epc,
        GateDirection direction,
        double confidence,
        int inboundAntenna,
        int outboundAntenna,
        DateTime firstSeenUtc,
        DateTime lastSeenUtc)
    {
        Epc = epc;
        Direction = direction;
        Confidence = Math.Max(0.0, Math.Min(1.0, confidence));
        InboundAntenna = inboundAntenna;
        OutboundAntenna = outboundAntenna;
        FirstSeenUtc = firstSeenUtc;
        LastSeenUtc = lastSeenUtc;
    }

    public override string ToString() =>
        $"[Gate] EPC={Epc}, Dir={Direction} ({Confidence * 100:F0}% confidence), Duration={DurationMs:F0}ms";
}
