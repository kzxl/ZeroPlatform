using System;
using System.Collections.Generic;
using System.Net.WebSockets;

namespace ZeroNetwork.RealTime
{
    /// <summary>
    /// Configuration options for <see cref="ZeroWebSocketClient"/>.
    /// </summary>
    public class ZeroWebSocketOptions
    {
        /// <summary>
        /// Gets or sets the target WebSocket server URI (e.g., ws://localhost:5000/socket or wss://...).
        /// </summary>
        public Uri? ServerUri { get; set; }

        /// <summary>
        /// Gets or sets the connection timeout. Default is 10 seconds.
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets the WebSocket protocol keep-alive (ping/pong) interval. Default is 30 seconds.
        /// </summary>
        public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets whether the client should automatically reconnect if the connection drops. Default is true.
        /// </summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>
        /// Gets or sets the initial delay before the first reconnection attempt. Default is 1 second.
        /// </summary>
        public TimeSpan InitialReconnectDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the maximum delay between reconnection attempts. Default is 30 seconds.
        /// </summary>
        public TimeSpan MaxReconnectDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the exponential backoff multiplier for reconnect attempts. Default is 2.0.
        /// </summary>
        public double ReconnectMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Gets or sets the receive buffer size in bytes. Default is 8192 (8 KB).
        /// </summary>
        public int BufferSize { get; set; } = 8192;

        /// <summary>
        /// Gets custom HTTP headers sent during the initial WebSocket upgrade handshake.
        /// </summary>
        public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Optional delegate to configure the underlying BCL <see cref="ClientWebSocketOptions"/> directly.
        /// </summary>
        public Action<ClientWebSocketOptions>? ConfigureClientWebSocket { get; set; }
    }
}
