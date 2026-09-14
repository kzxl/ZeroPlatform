using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroIoT.OpcUa
{
    /// <summary>
    /// High-level, zero-dependency OPC-UA client managing connections, tag reading, and writing.
    /// </summary>
    public class OpcUaClient : IDisposable
    {
        private readonly UaTcpTransport _transport;
        private readonly ConcurrentDictionary<NodeId, UaDataValue> _tagCache = new ConcurrentDictionary<NodeId, UaDataValue>();

        public string EndpointUrl => _transport.EndpointUrl;
        public bool IsConnected => _transport.IsConnected;

        public event Action<NodeId, UaDataValue>? ValueChanged;

        public OpcUaClient(string host, int port = 4840, string? endpointUrl = null)
        {
            _transport = new UaTcpTransport(host, port, endpointUrl);
        }

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            return await _transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        public void Disconnect()
        {
            _transport.Disconnect();
        }

        /// <summary>
        /// Reads the current value of a NodeId.
        /// </summary>
        public UaDataValue Read(NodeId nodeId)
        {
            if (_tagCache.TryGetValue(nodeId, out var val))
            {
                return val;
            }
            return new UaDataValue(UaVariant.Null, 0x80000000); // BadNodeIdUnknown
        }

        /// <summary>
        /// Updates or writes a tag value in local memory/cache and notifies subscribers.
        /// </summary>
        public void Write(NodeId nodeId, UaVariant value, uint statusCode = 0)
        {
            var dataValue = new UaDataValue(value, statusCode, DateTime.UtcNow);
            _tagCache[nodeId] = dataValue;
            ValueChanged?.Invoke(nodeId, dataValue);
        }

        public void Dispose()
        {
            _transport.Dispose();
        }
    }
}
