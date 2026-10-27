using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace ZeroIoT.Mqtt
{
    /// <summary>
    /// Tracks in-flight QoS 2 packets for exact-once delivery semantics.
    /// </summary>
    public class Qos2FlightTable
    {
        private readonly ConcurrentDictionary<ushort, TaskCompletionSource<bool>> _outboundFlights = new ConcurrentDictionary<ushort, TaskCompletionSource<bool>>();
        private readonly ConcurrentDictionary<ushort, InboundQos2Message> _inboundPending = new ConcurrentDictionary<ushort, InboundQos2Message>();

        public struct InboundQos2Message
        {
            public string Topic { get; }
            public byte[] Payload { get; }
            public bool Retain { get; }

            public InboundQos2Message(string topic, byte[] payload, bool retain)
            {
                Topic = topic;
                Payload = payload;
                Retain = retain;
            }
        }

        public void RegisterOutbound(ushort packetId, TaskCompletionSource<bool> tcs)
        {
            _outboundFlights[packetId] = tcs;
        }

        public bool TryGetOutbound(ushort packetId, out TaskCompletionSource<bool>? tcs)
        {
            return _outboundFlights.TryGetValue(packetId, out tcs);
        }

        public bool CompleteOutbound(ushort packetId)
        {
            if (_outboundFlights.TryRemove(packetId, out var tcs))
            {
                tcs.TrySetResult(true);
                return true;
            }
            return false;
        }

        public void FailAll(Exception ex)
        {
            foreach (var kvp in _outboundFlights)
            {
                kvp.Value.TrySetException(ex);
            }
            _outboundFlights.Clear();
            _inboundPending.Clear();
        }

        public void StoreInbound(ushort packetId, string topic, byte[] payload, bool retain)
        {
            _inboundPending[packetId] = new InboundQos2Message(topic, payload, retain);
        }

        public bool TryReleaseInbound(ushort packetId, out InboundQos2Message message)
        {
            return _inboundPending.TryRemove(packetId, out message);
        }

        public void Clear()
        {
            _outboundFlights.Clear();
            _inboundPending.Clear();
        }
    }
}
