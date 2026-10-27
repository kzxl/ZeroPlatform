using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroBus.Can;

namespace ZeroBus.CanOpen
{
    /// <summary>
    /// CANopen SYNC Producer (CiA 301).
    /// Broadcasts high-priority synchronization frames (COB-ID 0x080) across the fieldbus,
    /// triggering synchronized PDO latching and clock alignment across all servo drives.
    /// </summary>
    public class SyncProducer
    {
        private readonly ICanTransport _transport;
        public uint CobId { get; set; } = CanOpenConstants.CobIdSync; // 0x080

        public SyncProducer(ICanTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public async Task BroadcastSyncAsync(CancellationToken ct = default)
        {
            var syncFrame = CanFrame.CreateStandard(CobId, Array.Empty<byte>());
            await _transport.SendFrameAsync(syncFrame, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Heartbeat state representation per CiA 301.
    /// </summary>
    public class NodeHeartbeatState
    {
        public byte NodeId { get; set; }
        public byte NmtState { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public bool IsAlive { get; set; } = true;
    }

    /// <summary>
    /// CANopen Heartbeat Consumer & Liveness Monitor (CiA 301).
    /// Tracks periodic heartbeat frames (COB-ID 0x700 + NodeID) to monitor slave node health
    /// and trigger failsafe actions upon communication loss.
    /// </summary>
    public class HeartbeatMonitor
    {
        private readonly ConcurrentDictionary<byte, NodeHeartbeatState> _nodes = new ConcurrentDictionary<byte, NodeHeartbeatState>();
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMilliseconds(500);

        public event Action<byte, byte>? OnHeartbeatReceived;
        public event Action<byte>? OnNodeTimeout;

        public HeartbeatMonitor(ICanTransport transport)
        {
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            transport.OnFrameReceived += HandleFrame;
        }

        public void RegisterNode(byte nodeId)
        {
            _nodes[nodeId] = new NodeHeartbeatState
            {
                NodeId = nodeId,
                NmtState = 0,
                LastSeenUtc = DateTime.UtcNow,
                IsAlive = true
            };
        }

        public bool IsNodeAlive(byte nodeId)
        {
            return _nodes.TryGetValue(nodeId, out var state) && state.IsAlive;
        }

        public IEnumerable<NodeHeartbeatState> GetAllNodeStates() => _nodes.Values;

        public void CheckTimeouts() => CheckTimeouts(DefaultTimeout);

        public void CheckTimeouts(TimeSpan timeoutThreshold)
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in _nodes)
            {
                var state = kvp.Value;
                if (state.IsAlive && (now - state.LastSeenUtc) > timeoutThreshold)
                {
                    state.IsAlive = false;
                    OnNodeTimeout?.Invoke(kvp.Key);
                }
            }
        }

        private void HandleFrame(CanFrame frame)
        {
            if (frame.Id >= 0x700 && frame.Id <= 0x77F && frame.Data.Length >= 1)
            {
                byte nodeId = (byte)(frame.Id - 0x700);
                byte nmtState = frame.Data[0];

                var state = _nodes.GetOrAdd(nodeId, id => new NodeHeartbeatState { NodeId = id });
                state.NmtState = nmtState;
                state.LastSeenUtc = DateTime.UtcNow;
                state.IsAlive = true;

                OnHeartbeatReceived?.Invoke(nodeId, nmtState);
            }
        }
    }
}
