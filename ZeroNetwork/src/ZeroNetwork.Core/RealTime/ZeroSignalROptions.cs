using System;
using System.Collections.Generic;

namespace ZeroNetwork.RealTime
{
    /// <summary>
    /// Configuration options for <see cref="ZeroSignalRClient"/>.
    /// </summary>
    public class ZeroSignalROptions
    {
        /// <summary>
        /// Gets or sets the target SignalR Hub URL (e.g. ws://localhost:5000/hub/chat or http://localhost:5000/hub/chat).
        /// </summary>
        public Uri? HubUri { get; set; }

        /// <summary>
        /// Gets or sets the timeout for initial protocol handshake. Default is 15 seconds.
        /// </summary>
        public TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Gets or sets the interval at which client ping keep-alive messages are sent. Default is 15 seconds.
        /// </summary>
        public TimeSpan PingInterval { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Gets or sets the default timeout for request-response invocations. Default is 30 seconds.
        /// </summary>
        public TimeSpan InvocationTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets whether to automatically reconnect when the connection drops. Default is true.
        /// </summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>
        /// Gets or sets the initial delay before reconnection attempt. Default is 1 second.
        /// </summary>
        public TimeSpan InitialReconnectDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the maximum delay between reconnection attempts. Default is 30 seconds.
        /// </summary>
        public TimeSpan MaxReconnectDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets custom HTTP headers sent during the initial WebSocket handshake (e.g., Bearer authorization).
        /// </summary>
        public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
