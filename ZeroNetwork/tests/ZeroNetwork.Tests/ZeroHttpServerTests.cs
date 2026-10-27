using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ZeroNetwork.Http;

namespace ZeroNetwork.Tests
{
    public class ZeroHttpServerTests
    {
        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        [Fact]
        public async Task ZeroHttpServer_RoutesAndMetrics_WorkCorrectly()
        {
            int port = GetFreePort();
            using (var server = new ZeroHttpServer(port, "127.0.0.1"))
            {
                server.MapGet("/api/hello", req => ZeroHttpResponse.Text("Hello ZeroNetwork"));
                server.MapGet("/api/status", req => ZeroHttpResponse.Json("{\"status\":\"OK\"}"));
                server.MapPost("/api/echo", req => ZeroHttpResponse.Text(req.Body));

                server.Start();
                Assert.True(server.IsRunning);

                using (var http = new HttpClient())
                {
                    string baseUrl = $"http://127.0.0.1:{port}";

                    // 1. Test custom GET text
                    var respHello = await http.GetAsync($"{baseUrl}/api/hello");
                    Assert.Equal(HttpStatusCode.OK, respHello.StatusCode);
                    string helloBody = await respHello.Content.ReadAsStringAsync();
                    Assert.Equal("Hello ZeroNetwork", helloBody);

                    // 2. Test custom GET json
                    var respStatus = await http.GetAsync($"{baseUrl}/api/status");
                    Assert.Equal(HttpStatusCode.OK, respStatus.StatusCode);
                    string statusBody = await respStatus.Content.ReadAsStringAsync();
                    Assert.Equal("{\"status\":\"OK\"}", statusBody);

                    // 3. Test built-in Prometheus /metrics endpoint
                    var respMetrics = await http.GetAsync($"{baseUrl}/metrics");
                    Assert.Equal(HttpStatusCode.OK, respMetrics.StatusCode);
                    string metricsBody = await respMetrics.Content.ReadAsStringAsync();
                    Assert.Contains("process_uptime_seconds", metricsBody);
                    Assert.Contains("zero_http_requests_total", metricsBody);

                    // 4. Test built-in /health endpoint
                    var respHealth = await http.GetAsync($"{baseUrl}/health");
                    Assert.Equal(HttpStatusCode.OK, respHealth.StatusCode);
                    string healthBody = await respHealth.Content.ReadAsStringAsync();
                    Assert.Contains("UP", healthBody);

                    // 5. Test 404 Not Found
                    var resp404 = await http.GetAsync($"{baseUrl}/unmapped/route");
                    Assert.Equal(HttpStatusCode.NotFound, resp404.StatusCode);

                    // 6. Test POST with body
                    var postContent = new StringContent("TelemetryDataStream_12345", Encoding.UTF8, "text/plain");
                    var respEcho = await http.PostAsync($"{baseUrl}/api/echo", postContent);
                    Assert.Equal(HttpStatusCode.OK, respEcho.StatusCode);
                    string echoBody = await respEcho.Content.ReadAsStringAsync();
                    Assert.Equal("TelemetryDataStream_12345", echoBody);
                }

                server.Stop();
                Assert.False(server.IsRunning);
            }
        }

        [Fact]
        public async Task ZeroHttpServer_RateLimiter_And_TraceId_WorkCorrectly()
        {
            int port = GetFreePort();
            using (var server = new ZeroHttpServer(port, "127.0.0.1"))
            {
                // Rate limiter: 1 req/sec, burst capacity = 1
                server.WithRateLimiter(requestsPerSecond: 1.0, burstCapacity: 1.0);
                server.MapGet("/api/test", req => ZeroHttpResponse.Text("OK"));
                server.Start();

                using (var http = new HttpClient())
                {
                    string url = $"http://127.0.0.1:{port}/api/test";

                    // First request should succeed and have X-Request-Id header
                    var resp1 = await http.GetAsync(url);
                    Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
                    Assert.True(resp1.Headers.Contains("X-Request-Id"));

                    // Immediate second request should be throttled (429 Too Many Requests)
                    var resp2 = await http.GetAsync(url);
                    Assert.Equal((HttpStatusCode)429, resp2.StatusCode);
                    Assert.True(resp2.Headers.Contains("Retry-After"));
                }

                server.Stop();
            }
        }
    }
}
