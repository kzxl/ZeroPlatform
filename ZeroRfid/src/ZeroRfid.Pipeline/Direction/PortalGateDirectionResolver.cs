using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroRfid.Core.Abstractions;
using ZeroRfid.Core.Models;

namespace ZeroRfid.Pipeline.Direction;

/// <summary>
/// Real-time portal gate vector direction detector analyzing temporal antenna transitions and peak RSSI.
/// </summary>
public sealed class PortalGateDirectionResolver : ITagStreamFilter, IDisposable
{
    private sealed class AntennaObservation
    {
        public DateTime FirstUtc { get; set; }
        public DateTime PeakUtc { get; set; }
        public double PeakRssi { get; set; }
        public int Count { get; set; }

        public AntennaObservation(DateTime now, double rssi)
        {
            FirstUtc = now;
            PeakUtc = now;
            PeakRssi = rssi;
            Count = 1;
        }

        public void Update(DateTime now, double rssi)
        {
            Count++;
            if (rssi > PeakRssi)
            {
                PeakRssi = rssi;
                PeakUtc = now;
            }
        }
    }

    private sealed class PassageSession
    {
        public string Epc { get; }
        public DateTime FirstSeenUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public Dictionary<int, AntennaObservation> Antennas { get; } = new();

        public PassageSession(string epc, DateTime now)
        {
            Epc = epc;
            FirstSeenUtc = now;
            LastSeenUtc = now;
        }
    }

    private readonly int _inboundAntenna;
    private readonly int _outboundAntenna;
    private readonly TimeSpan _passageWindow;
    private readonly ConcurrentDictionary<string, PassageSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _purgeTimer;

    /// <summary>
    /// Raised whenever a tag passage is concluded and direction is resolved.
    /// </summary>
    public event EventHandler<DirectionEvaluation>? DirectionResolved;

    /// <summary>
    /// Initializes a new portal gate direction detector.
    /// </summary>
    /// <param name="inboundAntenna">Antenna ID located on the entry/outer side (e.g. 1).</param>
    /// <param name="outboundAntenna">Antenna ID located on the exit/inner side (e.g. 2).</param>
    /// <param name="passageWindow">Maximum time duration of a transit (default: 3 seconds).</param>
    public PortalGateDirectionResolver(int inboundAntenna = 1, int outboundAntenna = 2, TimeSpan? passageWindow = null)
    {
        _inboundAntenna = inboundAntenna;
        _outboundAntenna = outboundAntenna;
        _passageWindow = passageWindow ?? TimeSpan.FromSeconds(3);

        int purgeMs = Math.Max(250, (int)(_passageWindow.TotalMilliseconds / 2));
        _purgeTimer = new Timer(OnPurgeTick, null, purgeMs, purgeMs);
    }

    public ValueTask<TagReadEvent?> ProcessAsync(TagReadEvent tagEvent, CancellationToken ct = default)
    {
        if (tagEvent == null)
            return new ValueTask<TagReadEvent?>((TagReadEvent?)null);

        DateTime now = tagEvent.TimestampUtc;
        _sessions.AddOrUpdate(
            tagEvent.Epc,
            addValueFactory: epc =>
            {
                var session = new PassageSession(epc, now);
                session.Antennas[tagEvent.AntennaId] = new AntennaObservation(now, tagEvent.Rssi);
                return session;
            },
            updateValueFactory: (_, session) =>
            {
                session.LastSeenUtc = now;
                if (session.Antennas.TryGetValue(tagEvent.AntennaId, out var obs))
                {
                    obs.Update(now, tagEvent.Rssi);
                }
                else
                {
                    session.Antennas[tagEvent.AntennaId] = new AntennaObservation(now, tagEvent.Rssi);
                }
                return session;
            });

        // Direction resolver observes stream without suppressing it
        return new ValueTask<TagReadEvent?>(tagEvent);
    }

    /// <summary>
    /// Forces immediate resolution and evacuation of an active tag passage session (useful for unit tests).
    /// </summary>
    public DirectionEvaluation? ResolveImmediate(string epc)
    {
        if (_sessions.TryRemove(epc, out var session))
        {
            var evaluation = EvaluateSession(session);
            DirectionResolved?.Invoke(this, evaluation);
            return evaluation;
        }

        return null;
    }

    private void OnPurgeTick(object? state)
    {
        DateTime now = DateTime.UtcNow;
        var expiredEpcs = new List<string>();

        foreach (var kvp in _sessions)
        {
            if (now - kvp.Value.LastSeenUtc > _passageWindow)
            {
                expiredEpcs.Add(kvp.Key);
            }
        }

        foreach (var epc in expiredEpcs)
        {
            if (_sessions.TryRemove(epc, out var session))
            {
                var evaluation = EvaluateSession(session);
                try
                {
                    DirectionResolved?.Invoke(this, evaluation);
                }
                catch
                {
                    // Suppress subscriber exceptions
                }
            }
        }
    }

    private DirectionEvaluation EvaluateSession(PassageSession session)
    {
        bool hasInbound = session.Antennas.TryGetValue(_inboundAntenna, out var inObs);
        bool hasOutbound = session.Antennas.TryGetValue(_outboundAntenna, out var outObs);

        if (!hasInbound || !hasOutbound || inObs == null || outObs == null)
        {
            // Only detected on single antenna, direction cannot be conclusively determined
            return new DirectionEvaluation(
                session.Epc,
                GateDirection.Stationary,
                confidence: 0.3,
                _inboundAntenna,
                _outboundAntenna,
                session.FirstSeenUtc,
                session.LastSeenUtc);
        }

        // Temporal comparison: peak of antenna 1 vs peak of antenna 2
        TimeSpan deltaPeak = outObs.PeakUtc - inObs.PeakUtc;

        GateDirection direction;
        double confidence;

        if (deltaPeak.TotalMilliseconds > 50)
        {
            // Inbound: Peak at Inbound antenna happened significantly earlier than Outbound
            direction = GateDirection.Inbound;
            confidence = 0.90;
        }
        else if (deltaPeak.TotalMilliseconds < -50)
        {
            // Outbound: Peak at Outbound antenna happened earlier than Inbound
            direction = GateDirection.Outbound;
            confidence = 0.90;
        }
        else
        {
            // Peaks are almost simultaneous (e.g. tag stationary in middle of gate)
            direction = GateDirection.Stationary;
            confidence = 0.50;
        }

        return new DirectionEvaluation(
            session.Epc,
            direction,
            confidence,
            _inboundAntenna,
            _outboundAntenna,
            session.FirstSeenUtc,
            session.LastSeenUtc);
    }

    public void Dispose()
    {
        _purgeTimer.Dispose();
        _sessions.Clear();
    }
}
