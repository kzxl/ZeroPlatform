using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlatform.Concurrency.RateLimiting;
using ZeroPrimitives.Core.Identifiers;

namespace ZeroNetwork.Http
{
    /// <summary>
    /// Represents an incoming HTTP request.
    /// </summary>
    public class ZeroHttpRequest
    {
        /// <summary>
        /// Gets or sets the unique, sortable 128-bit <see cref="FastUlid"/> correlation and trace ID.
        /// </summary>
        public FastUlid TraceId { get; set; } = FastUlid.NewUlid();
        public string Method { get; set; } = "GET";
        public string Path { get; set; } = "/";
        public string QueryString { get; set; } = string.Empty;
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string Body { get; set; } = string.Empty;
        public EndPoint? RemoteEndPoint { get; set; }
    }

    /// <summary>
    /// Represents an outgoing HTTP response.
    /// </summary>
    public class ZeroHttpResponse
    {
        public int StatusCode { get; set; } = 200;
        public string StatusMessage { get; set; } = "OK";
        public string ContentType { get; set; } = "text/plain; charset=utf-8";
        public byte[] BodyBytes { get; set; } = Array.Empty<byte>();
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static ZeroHttpResponse Text(string text, string contentType = "text/plain; charset=utf-8", int statusCode = 200)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            return new ZeroHttpResponse
            {
                StatusCode = statusCode,
                StatusMessage = statusCode == 200 ? "OK" : "Status " + statusCode,
                ContentType = contentType,
                BodyBytes = bytes
            };
        }

        public static ZeroHttpResponse Json(string json, int statusCode = 200)
        {
            return Text(json, "application/json; charset=utf-8", statusCode);
        }

        public static ZeroHttpResponse NotFound(string message = "404 Not Found")
        {
            return Text(message, "text/plain; charset=utf-8", 404);
        }

        public static ZeroHttpResponse BadRequest(string message = "400 Bad Request")
        {
            return Text(message, "text/plain; charset=utf-8", 400);
        }
    }

    /// <summary>
    /// Ultra-lightweight, sovereign embedded HTTP/1.1 micro-server for Edge REST APIs and Prometheus telemetry.
    /// Zero external dependencies, pure C# BCL socket listener, executes without Windows Administrator privileges.
    /// </summary>
    public class ZeroHttpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly int _port;
        private readonly string _host;
        private readonly Dictionary<string, Func<ZeroHttpRequest, ZeroHttpResponse>> _routes =
            new Dictionary<string, Func<ZeroHttpRequest, ZeroHttpResponse>>(StringComparer.OrdinalIgnoreCase);

        private CancellationTokenSource? _serverCts;
        private readonly object _lock = new object();
        private bool _disposed;
        private long _requestsHandled;
        private readonly DateTime _startTime = DateTime.UtcNow;
        private TokenBucketRateLimiter? _rateLimiter;

        public int Port => _port;
        public bool IsRunning => _serverCts != null && !_serverCts.IsCancellationRequested;

        /// <summary>
        /// Configures a token bucket rate limiter to protect the HTTP server against traffic spikes and DoS.
        /// </summary>
        /// <param name="requestsPerSecond">Sustained request refill rate per second.</param>
        /// <param name="burstCapacity">Maximum allowed burst requests before throttling.</param>
        public ZeroHttpServer WithRateLimiter(double requestsPerSecond, double burstCapacity)
        {
            _rateLimiter = new TokenBucketRateLimiter(burstCapacity, requestsPerSecond);
            return this;
        }

        public ZeroHttpServer(int port = 8080, string host = "0.0.0.0")
        {
            _port = port;
            _host = host;

            IPAddress bindAddress = host == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse(host);
            _listener = new TcpListener(bindAddress, port);

            // Register default Prometheus metrics route
            MapGet("/metrics", _ => HandlePrometheusMetrics());
            MapGet("/health", _ => ZeroHttpResponse.Json("{\"status\":\"UP\"}"));
        }

        /// <summary>
        /// Registers a GET route handler.
        /// </summary>
        public ZeroHttpServer MapGet(string path, Func<ZeroHttpRequest, ZeroHttpResponse> handler)
        {
            return Map("GET", path, handler);
        }

        /// <summary>
        /// Registers a POST route handler.
        /// </summary>
        public ZeroHttpServer MapPost(string path, Func<ZeroHttpRequest, ZeroHttpResponse> handler)
        {
            return Map("POST", path, handler);
        }

        /// <summary>
        /// Registers an HTTP verb and path route handler.
        /// </summary>
        public ZeroHttpServer Map(string method, string path, Func<ZeroHttpRequest, ZeroHttpResponse> handler)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            string routeKey = $"{method.ToUpperInvariant()}:{path.Trim()}";
            lock (_lock)
            {
                _routes[routeKey] = handler;
            }
            return this;
        }

        /// <summary>
        /// Starts accepting incoming HTTP requests asynchronously.
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_disposed || IsRunning) return;

                _listener.Start();
                _serverCts = new CancellationTokenSource();
                var token = _serverCts.Token;

                Task.Run(async () =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                            _ = Task.Run(() => ProcessClientAsync(client, token), token);
                        }
                        catch (ObjectDisposedException)
                        {
                            break;
                        }
                        catch (SocketException)
                        {
                            break;
                        }
                        catch
                        {
                            try { await Task.Delay(50, token).ConfigureAwait(false); } catch { break; }
                        }
                    }
                }, token);
            }
        }

        private async Task ProcessClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                try
                {
                    var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, leaveOpen: true);
                    string? requestLine = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(requestLine)) return;

                    string[] parts = requestLine.Split(' ');
                    if (parts.Length < 2) return;

                    string method = parts[0];
                    string rawUrl = parts[1];
                    string path = rawUrl;
                    string query = string.Empty;

                    int qIdx = rawUrl.IndexOf('?');
                    if (qIdx >= 0)
                    {
                        path = rawUrl.Substring(0, qIdx);
                        query = rawUrl.Substring(qIdx + 1);
                    }

                    var request = new ZeroHttpRequest
                    {
                        Method = method,
                        Path = path,
                        QueryString = query,
                        RemoteEndPoint = client.Client.RemoteEndPoint
                    };

                    int contentLength = 0;
                    string? headerLine;
                    while (!string.IsNullOrEmpty(headerLine = await reader.ReadLineAsync().ConfigureAwait(false)))
                    {
                        int cIdx = headerLine.IndexOf(':');
                        if (cIdx > 0)
                        {
                            string hKey = headerLine.Substring(0, cIdx).Trim();
                            string hVal = headerLine.Substring(cIdx + 1).Trim();
                            request.Headers[hKey] = hVal;

                            if (hKey.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                            {
                                int.TryParse(hVal, out contentLength);
                            }
                            else if (hKey.Equals("X-Request-Id", StringComparison.OrdinalIgnoreCase) || hKey.Equals("X-Trace-Id", StringComparison.OrdinalIgnoreCase))
                            {
                                if (FastUlid.TryParse(hVal.AsSpan(), out var parsedUlid))
                                {
                                    request.TraceId = parsedUlid;
                                }
                            }
                        }
                    }

                    if (contentLength > 0 && contentLength < 10 * 1024 * 1024) // Cap body at 10MB
                    {
                        char[] bodyChars = new char[contentLength];
                        int readTotal = 0;
                        while (readTotal < contentLength)
                        {
                            int read = await reader.ReadAsync(bodyChars, readTotal, contentLength - readTotal).ConfigureAwait(false);
                            if (read <= 0) break;
                            readTotal += read;
                        }
                        request.Body = new string(bodyChars, 0, readTotal);
                    }

                    Interlocked.Increment(ref _requestsHandled);

                    ZeroHttpResponse response;
                    if (_rateLimiter != null && !_rateLimiter.TryAcquire(1.0))
                    {
                        response = new ZeroHttpResponse
                        {
                            StatusCode = 429,
                            StatusMessage = "Too Many Requests",
                            ContentType = "text/plain; charset=utf-8",
                            BodyBytes = Encoding.UTF8.GetBytes("429 Too Many Requests\nRate limit exceeded.")
                        };
                        response.Headers["Retry-After"] = "1";
                    }
                    else
                    {
                        // Route lookup
                        string routeKey = $"{method.ToUpperInvariant()}:{path}";
                        Func<ZeroHttpRequest, ZeroHttpResponse>? handler = null;
                        lock (_lock)
                        {
                            _routes.TryGetValue(routeKey, out handler);
                        }

                        if (handler != null)
                        {
                            try
                            {
                                response = handler(request);
                            }
                            catch (Exception ex)
                            {
                                response = ZeroHttpResponse.Text("500 Internal Server Error\n" + ex.Message, statusCode: 500);
                            }
                        }
                        else
                        {
                            response = ZeroHttpResponse.NotFound();
                        }
                    }

                    // Send response
                    var sb = new StringBuilder();
                    sb.Append($"HTTP/1.1 {response.StatusCode} {response.StatusMessage}\r\n");
                    sb.Append($"Content-Type: {response.ContentType}\r\n");
                    sb.Append($"Content-Length: {response.BodyBytes.Length}\r\n");
                    sb.Append($"X-Request-Id: {request.TraceId}\r\n");
                    sb.Append("Connection: close\r\n");

                    foreach (var h in response.Headers)
                    {
                        sb.Append($"{h.Key}: {h.Value}\r\n");
                    }
                    sb.Append("\r\n");

                    byte[] headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
                    await stream.WriteAsync(headerBytes, 0, headerBytes.Length, cancellationToken).ConfigureAwait(false);
                    if (response.BodyBytes.Length > 0)
                    {
                        await stream.WriteAsync(response.BodyBytes, 0, response.BodyBytes.Length, cancellationToken).ConfigureAwait(false);
                    }
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore client connection drops
                }
            }
        }

        private ZeroHttpResponse HandlePrometheusMetrics()
        {
            double uptimeSec = (DateTime.UtcNow - _startTime).TotalSeconds;
            long memoryBytes = GC.GetTotalMemory(false);
            long requests = Interlocked.Read(ref _requestsHandled);

            var sb = new StringBuilder();
            sb.Append("# HELP zero_http_requests_total Total HTTP requests served by ZeroHttpServer.\n");
            sb.Append("# TYPE zero_http_requests_total counter\n");
            sb.Append($"zero_http_requests_total {requests}\n\n");

            sb.Append("# HELP process_uptime_seconds Process uptime in seconds.\n");
            sb.Append("# TYPE process_uptime_seconds gauge\n");
            sb.Append($"process_uptime_seconds {uptimeSec:F2}\n\n");

            sb.Append("# HELP dotnet_total_memory_bytes Total GC managed memory allocated.\n");
            sb.Append("# TYPE dotnet_total_memory_bytes gauge\n");
            sb.Append($"dotnet_total_memory_bytes {memoryBytes}\n\n");

            // Export all registered custom ZeroTelemetry metrics
            ZeroNetwork.Diagnostics.ZeroTelemetry.ExportPrometheus(sb);

            return ZeroHttpResponse.Text(sb.ToString(), "text/plain; version=0.0.4; charset=utf-8");
        }

        /// <summary>
        /// Stops the HTTP listener and aborts active connections.
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                _serverCts?.Cancel();
                _serverCts?.Dispose();
                _serverCts = null;

                try { _listener.Stop(); } catch { }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                Stop();
            }
        }
    }
}
