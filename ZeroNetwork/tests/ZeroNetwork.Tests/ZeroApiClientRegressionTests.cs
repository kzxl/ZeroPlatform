using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroNetwork.Http;

namespace ZeroNetwork.Tests
{
    /// <summary>
    /// End-to-end regression tests for defects found in the ZeroApiClient deep analysis (D1, D4, D5, D6, D9, D13).
    /// </summary>
    public class ZeroApiClientRegressionTests
    {
        private class ItemDto
        {
            public int Id { get; set; }
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static ZeroApiClientOptions FastOptions(int port, string basePath = "") => new ZeroApiClientOptions
        {
            BaseAddress = $"http://127.0.0.1:{port}{basePath}",
            InitialRetryDelay = TimeSpan.FromMilliseconds(1),
            MaxRetryAttempts = 3
        };

        [Fact] // D1
        public async Task Post_OnBadGateway_IsNotRetried_ButGetIs()
        {
            int port = GetFreePort();
            int postHits = 0, getHits = 0;

            using var server = new ZeroHttpServer(port, "127.0.0.1");
            server.MapPost("/orders", _ => { Interlocked.Increment(ref postHits); return ZeroHttpResponse.Text("bad gateway", statusCode: 502); });
            server.MapGet("/orders", _ => { Interlocked.Increment(ref getHits); return ZeroHttpResponse.Text("bad gateway", statusCode: 502); });
            server.Start();

            using var client = new ZeroApiClient(FastOptions(port));

            await Assert.ThrowsAsync<HttpRequestException>(() => client.PostAsync<ItemDto, ItemDto>("orders", new ItemDto { Id = 1 }));
            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync<ItemDto>("orders"));

            Assert.Equal(1, postHits);
            Assert.Equal(4, getHits);
        }

        [Fact] // D5
        public async Task Delete_NoContent_ReturnsDefault()
        {
            int port = GetFreePort();
            using var server = new ZeroHttpServer(port, "127.0.0.1");
            server.Map("DELETE", "/items/7", _ => ZeroHttpResponse.Text(string.Empty, statusCode: 204));
            server.Start();

            using var client = new ZeroApiClient(FastOptions(port));
            ItemDto? result = await client.DeleteAsync<ItemDto>("items/7");

            Assert.Null(result);
        }

        [Fact] // D7 (status surfaced)
        public async Task ErrorStatus_ThrowsHttpRequestExceptionWithStatusCode()
        {
            int port = GetFreePort();
            using var server = new ZeroHttpServer(port, "127.0.0.1");
            server.Start();

            using var client = new ZeroApiClient(FastOptions(port));
            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync<ItemDto>("missing"));

            Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
            Assert.Equal(404, ex.Data["StatusCode"]);
        }

        [Fact] // D9
        public async Task GetStreamAsync_ErrorStatus_Throws()
        {
            int port = GetFreePort();
            using var server = new ZeroHttpServer(port, "127.0.0.1");
            server.Start();

            using var client = new ZeroApiClient(FastOptions(port));
            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetStreamAsync("missing"));
        }

        [Fact] // D13
        public async Task LeadingSlash_IsResolvedAgainstBasePath()
        {
            int port = GetFreePort();
            using var server = new ZeroHttpServer(port, "127.0.0.1");
            server.MapGet("/api/v1/items", _ => ZeroHttpResponse.Json("{\"id\":5}"));
            server.Start();

            using var client = new ZeroApiClient(FastOptions(port, "/api/v1"));
            ItemDto? item = await client.GetAsync<ItemDto>("/items");

            Assert.NotNull(item);
            Assert.Equal(5, item!.Id);
        }

        [Fact] // D6
        public async Task Timeout_CoversStalledResponseBody()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var serverCts = new CancellationTokenSource();
            Task serverTask = RunStalledBodyServerAsync(listener, serverCts.Token);

            try
            {
                using var client = new ZeroApiClient(new ZeroApiClientOptions
                {
                    BaseAddress = $"http://127.0.0.1:{port}",
                    Timeout = TimeSpan.FromMilliseconds(500),
                    MaxRetryAttempts = 0
                });

                var stopwatch = Stopwatch.StartNew();
                Task<ItemDto?> call = client.GetAsync<ItemDto>("slow");
                Task finished = await Task.WhenAny(call, Task.Delay(TimeSpan.FromSeconds(10)));

                Assert.Same(call, finished); // must not hang
                var ex = await Assert.ThrowsAsync<TaskCanceledException>(() => call);
                Assert.IsType<TimeoutException>(ex.InnerException);
                Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Timed out too late: {stopwatch.Elapsed}");
            }
            finally
            {
                serverCts.Cancel();
                listener.Stop();
                try { await serverTask; } catch { /* listener stopped */ }
            }
        }

        [Fact] // caller cancellation is not reported as a timeout
        public async Task CallerCancellation_SurfacesAsOperationCanceled()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var serverCts = new CancellationTokenSource();
            Task serverTask = RunStalledBodyServerAsync(listener, serverCts.Token);

            try
            {
                using var client = new ZeroApiClient(new ZeroApiClientOptions
                {
                    BaseAddress = $"http://127.0.0.1:{port}",
                    Timeout = TimeSpan.FromSeconds(30),
                    MaxRetryAttempts = 0
                });

                using var callerCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
                var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync<ItemDto>("slow", cancellationToken: callerCts.Token));

                Assert.IsNotType<TimeoutException>(ex.InnerException);
            }
            finally
            {
                serverCts.Cancel();
                listener.Stop();
                try { await serverTask; } catch { /* listener stopped */ }
            }
        }

        [Fact] // D4
        public void AppendQueryParams_IsCultureInvariant_UnderVietnameseCulture()
        {
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("vi-VN");

                string url = ZeroApiClient.AppendQueryParams("https://erp.local/stock#top", new
                {
                    amount = 1500.5m,
                    ratio = 0.25,
                    active = true,
                    ids = new[] { 1, 2 },
                    at = new DateTime(2026, 10, 6, 9, 25, 39, DateTimeKind.Utc),
                    skipped = (string?)null
                });

                Assert.StartsWith("https://erp.local/stock?", url);
                Assert.Contains("amount=1500.5", url);
                Assert.Contains("ratio=0.25", url);
                Assert.Contains("active=true", url);
                Assert.Contains("ids=1&ids=2", url);
                Assert.Contains("at=2026-10-06T09%3A25%3A39.0000000Z", url);
                Assert.DoesNotContain("skipped", url);
                Assert.EndsWith("#top", url);
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [Theory]
        [InlineData("https://x/y?", "https://x/y?a=1")]
        [InlineData("https://x/y?b=2", "https://x/y?b=2&a=1")]
        [InlineData("https://x/y?b=2&", "https://x/y?b=2&a=1")]
        public void AppendQueryParams_HandlesExistingQuerySeparators(string input, string expected)
        {
            Assert.Equal(expected, ZeroApiClient.AppendQueryParams(input, new { a = 1 }));
        }

        [Fact] // review M4
        public async Task EmptyCloseDelimitedBody_ReturnsDefault()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task serverTask = RunRawResponseServerAsync(listener, "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nConnection: close\r\n\r\n");

            try
            {
                using var client = new ZeroApiClient(new ZeroApiClientOptions { BaseAddress = $"http://127.0.0.1:{port}", MaxRetryAttempts = 0 });
                ItemDto? item = await client.GetAsync<ItemDto>("empty");
                Assert.Null(item);
            }
            finally
            {
                listener.Stop();
                try { await serverTask; } catch { /* listener stopped */ }
            }
        }

        [Fact] // review M2
        public async Task ExplicitContentHeader_OverridesContentType_WithoutDuplicates()
        {
            int port = GetFreePort();
            string? receivedContentType = null;

            using var server = new ZeroHttpServer(port, "127.0.0.1");
            server.MapPost("/upload", req =>
            {
                req.Headers.TryGetValue("Content-Type", out receivedContentType);
                return ZeroHttpResponse.Json("{}");
            });
            server.Start();

            using var client = ZeroNetwork.Http.Client.ZeroHttpClient.CreateBuilder()
                .WithBaseAddress($"http://127.0.0.1:{port}")
                .Build();

            var content = new ZeroNetwork.Http.Client.ZeroByteArrayContent(Encoding.UTF8.GetBytes("hello")) { ContentType = "application/json" };
            content.Headers.Set("Content-Type", "text/plain; charset=utf-8");

            using var request = new ZeroNetwork.Http.Client.ZeroHttpRequestMessage(HttpMethod.Post, "upload") { Content = content };
            using var response = await client.SendAsync(request);

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("text/plain; charset=utf-8", receivedContentType);
        }

        private static async Task RunRawResponseServerAsync(TcpListener listener, string rawResponse)
        {
            using TcpClient client = await listener.AcceptTcpClientAsync();
            NetworkStream stream = client.GetStream();

            var buffer = new byte[4096];
            var received = new StringBuilder();
            while (!received.ToString().Contains("\r\n\r\n"))
            {
                int read = await stream.ReadAsync(buffer);
                if (read == 0) return;
                received.Append(Encoding.ASCII.GetString(buffer, 0, read));
            }

            byte[] payload = Encoding.ASCII.GetBytes(rawResponse);
            await stream.WriteAsync(payload);
            await stream.FlushAsync();
            client.Client.Shutdown(SocketShutdown.Send); // close-delimited body: EOF right after headers
        }

        private static async Task RunStalledBodyServerAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
            NetworkStream stream = client.GetStream();

            var buffer = new byte[4096];
            var received = new StringBuilder();
            while (!received.ToString().Contains("\r\n\r\n"))
            {
                int read = await stream.ReadAsync(buffer, cancellationToken);
                if (read == 0) return;
                received.Append(Encoding.ASCII.GetString(buffer, 0, read));
            }

            // Headers promise 100 bytes, but only a fragment of the body is ever sent.
            byte[] head = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 100\r\n\r\n{\"id\":");
            await stream.WriteAsync(head, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            try { await Task.Delay(Timeout.Infinite, cancellationToken); }
            catch (OperationCanceledException) { }
        }
    }
}
