using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroNetwork.Diagnostics;
using ZeroNetwork.Http.Client;

namespace ZeroNetwork.Tests
{
    /// <summary>
    /// Scripted in-memory transport: each step produces one response (or throws); the last step repeats.
    /// </summary>
    internal sealed class ScriptedTransport : IZeroHttpTransport
    {
        private readonly Queue<Func<ZeroHttpRequestMessage, ZeroHttpResponseMessage>> _steps = new Queue<Func<ZeroHttpRequestMessage, ZeroHttpResponseMessage>>();

        public string Name { get; }
        public int Calls;
        public bool Disposed;
        public readonly List<string> SeenUris = new List<string>();
        public readonly List<string?> SeenAuthorization = new List<string?>();

        public ScriptedTransport(string name = "default")
        {
            Name = name;
        }

        public ScriptedTransport Respond(int status, string body = "", params (string Name, string Value)[] headers)
        {
            _steps.Enqueue(_ => CreateResponse(status, body, headers));
            return this;
        }

        public ScriptedTransport Fail(Func<Exception> exceptionFactory)
        {
            _steps.Enqueue(_ => throw exceptionFactory());
            return this;
        }

        public ScriptedTransport FailWith(Func<ZeroHttpResponseMessage> thrower)
        {
            _steps.Enqueue(_ => thrower());
            return this;
        }

        public async Task<ZeroHttpResponseMessage> SendAsync(ZeroHttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Yield();
            Interlocked.Increment(ref Calls);
            lock (SeenUris)
            {
                SeenUris.Add(request.RequestUri.ToString());
                SeenAuthorization.Add(request.Headers.Get("Authorization"));
            }

            Func<ZeroHttpRequestMessage, ZeroHttpResponseMessage> step = _steps.Count > 1 ? _steps.Dequeue() : _steps.Peek();
            return step(request);
        }

        public void Dispose() => Disposed = true;

        public static ZeroHttpResponseMessage CreateResponse(int status, string body = "", params (string Name, string Value)[] headers)
        {
            var responseHeaders = new ZeroHttpHeaders();
            foreach (var (name, value) in headers) responseHeaders.Add(name, value);
            return new ZeroHttpResponseMessage(status, null, responseHeaders, new MemoryStream(Encoding.UTF8.GetBytes(body)));
        }
    }

    [Collection("ZeroTelemetryRegistry")]
    public class ZeroHttpPipelineTests
    {
        private const string BaseUrl = "http://device.local/api/v1/";

        private static ZeroHttpClient BuildClient(ScriptedTransport transport, Action<ZeroRetryOptions>? retry = null)
        {
            var builder = ZeroHttpClient.CreateBuilder().WithBaseAddress(BaseUrl);
            if (retry != null) builder.UseRetry(retry);
            return builder.UseTransport(transport).Build();
        }

        private static void FastRetry(ZeroRetryOptions o)
        {
            o.MaxRetryAttempts = 3;
            o.InitialDelay = TimeSpan.FromMilliseconds(1);
        }

        private static ZeroHttpRequestMessage Post(string uri = "orders")
            => new ZeroHttpRequestMessage(HttpMethod.Post, uri) { Content = new ZeroByteArrayContent(Encoding.UTF8.GetBytes("{}")) };

        [Fact]
        public async Task Middleware_ExecutesInRegistrationOrder()
        {
            var order = new List<string>();
            var transport = new ScriptedTransport().Respond(200);

            using var client = ZeroHttpClient.CreateBuilder()
                .WithBaseAddress(BaseUrl)
                .Use(async (req, next, ct) => { order.Add("outer-in"); var r = await next(req, ct); order.Add("outer-out"); return r; })
                .Use(async (req, next, ct) => { order.Add("inner-in"); var r = await next(req, ct); order.Add("inner-out"); return r; })
                .UseTransport(transport)
                .Build();

            using var request = new ZeroHttpRequestMessage(HttpMethod.Get, "status");
            using var response = await client.SendAsync(request);

            Assert.Equal(new[] { "outer-in", "inner-in", "inner-out", "outer-out" }, order);
        }

        [Fact]
        public async Task Retry_Get_On503_ThenSucceeds()
        {
            var transport = new ScriptedTransport().Respond(503).Respond(200, "ok");
            using var client = BuildClient(transport, FastRetry);

            using var request = new ZeroHttpRequestMessage(HttpMethod.Get, "status");
            using var response = await client.SendAsync(request);

            Assert.Equal(200, response.StatusCode);
            Assert.Equal(2, transport.Calls);
        }

        [Fact]
        public async Task Retry_Post_IsNotRetriedByDefault()
        {
            var transport = new ScriptedTransport().Respond(502);
            using var client = BuildClient(transport, FastRetry);

            using var request = Post();
            using var response = await client.SendAsync(request);

            Assert.Equal(502, response.StatusCode);
            Assert.Equal(1, transport.Calls);
        }

        [Fact]
        public async Task Retry_Post_IsRetriedWhenGloballyEnabled()
        {
            var transport = new ScriptedTransport().Respond(502).Respond(201);
            using var client = BuildClient(transport, o => { FastRetry(o); o.RetryNonIdempotentRequests = true; });

            using var request = Post();
            using var response = await client.SendAsync(request);

            Assert.Equal(201, response.StatusCode);
            Assert.Equal(2, transport.Calls);
        }

        [Fact]
        public async Task Retry_Post_IsRetriedWhenRequestMarkedIdempotent()
        {
            var transport = new ScriptedTransport().Respond(503).Respond(200);
            using var client = BuildClient(transport, FastRetry);

            using var request = Post();
            request.IsIdempotent = true;
            using var response = await client.SendAsync(request);

            Assert.Equal(200, response.StatusCode);
            Assert.Equal(2, transport.Calls);
        }

        [Fact]
        public async Task Retry_OneShotContent_IsNeverRetried()
        {
            var transport = new ScriptedTransport().Respond(503);
            using var client = BuildClient(transport, FastRetry);

            using var request = new ZeroHttpRequestMessage(HttpMethod.Put, "items/1") { Content = new OneShotContent() };
            using var response = await client.SendAsync(request);

            Assert.Equal(503, response.StatusCode);
            Assert.Equal(1, transport.Calls);
        }

        [Fact]
        public async Task Retry_Exhausted_ReturnsLastResponse()
        {
            var transport = new ScriptedTransport().Respond(503);
            using var client = BuildClient(transport, FastRetry);

            using var request = new ZeroHttpRequestMessage(HttpMethod.Get, "status");
            using var response = await client.SendAsync(request);

            Assert.Equal(503, response.StatusCode);
            Assert.Equal(4, transport.Calls);
        }

        [Fact]
        public async Task Retry_NonTransientStatus_IsNotRetried()
        {
            var transport = new ScriptedTransport().Respond(400);
            using var client = BuildClient(transport, FastRetry);

            using var request = new ZeroHttpRequestMessage(HttpMethod.Get, "status");
            using var response = await client.SendAsync(request);

            Assert.Equal(400, response.StatusCode);
            Assert.Equal(1, transport.Calls);
        }

        [Fact]
        public async Task Retry_TransientException_Exhausted_RethrowsWithOriginalStackTrace()
        {
            var transport = new ScriptedTransport().FailWith(ThrowNetworkFailure);
            using var client = BuildClient(transport, FastRetry);

            using var request = new ZeroHttpRequestMessage(HttpMethod.Get, "status");
            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(request));

            Assert.Equal(4, transport.Calls);
            Assert.Contains(nameof(ThrowNetworkFailure), ex.StackTrace);
        }

        [Fact]
        public async Task Retry_RetryAfterAboveMaxDelay_ReturnsResponseWithoutRetry()
        {
            var transport = new ScriptedTransport().Respond(429, "", ("Retry-After", "120"));
            using var client = BuildClient(transport, o => { FastRetry(o); o.MaxDelay = TimeSpan.FromSeconds(5); });

            using var request = new ZeroHttpRequestMessage(HttpMethod.Get, "status");
            using var response = await client.SendAsync(request);

            Assert.Equal(429, response.StatusCode);
            Assert.Equal(1, transport.Calls);
        }

        [Fact]
        public async Task Retry_RetryAfterSeconds_IsHonored()
        {
            var delays = new List<TimeSpan>();
            var transport = new ScriptedTransport().Respond(503, "", ("Retry-After", "1")).Respond(200);
            using var client = BuildClient(transport, o => { FastRetry(o); o.OnRetry = ctx => delays.Add(ctx.Delay); });

            using var request = new ZeroHttpRequestMessage(HttpMethod.Get, "status");
            using var response = await client.SendAsync(request);

            Assert.Equal(200, response.StatusCode);
            Assert.Equal(new[] { TimeSpan.FromSeconds(1) }, delays);
        }

        [Fact]
        public async Task Retry_BackoffDelay_IsCappedByMaxDelay()
        {
            var delays = new List<TimeSpan>();
            var transport = new ScriptedTransport().Respond(503);
            using var client = BuildClient(transport, o =>
            {
                o.MaxRetryAttempts = 3;
                o.InitialDelay = TimeSpan.FromSeconds(10);
                o.MaxDelay = TimeSpan.FromMilliseconds(40);
                o.OnRetry = ctx => delays.Add(ctx.Delay);
            });

            using var request = new ZeroHttpRequestMessage(HttpMethod.Get, "status");
            using var response = await client.SendAsync(request);

            Assert.Equal(3, delays.Count);
            Assert.All(delays, d => Assert.InRange(d.TotalMilliseconds, 20, 40));
        }

        [Fact]
        public async Task BearerMiddleware_AddsTokenOnlyWhenAuthorizationIsAbsent()
        {
            var transport = new ScriptedTransport().Respond(200);
            using var client = ZeroHttpClient.CreateBuilder()
                .WithBaseAddress(BaseUrl)
                .UseBearerToken("default-token")
                .UseTransport(transport)
                .Build();

            using (var plain = new ZeroHttpRequestMessage(HttpMethod.Get, "a"))
            using (await client.SendAsync(plain)) { }

            using (var explicitAuth = new ZeroHttpRequestMessage(HttpMethod.Get, "b"))
            {
                explicitAuth.Headers.Set("Authorization", "Bearer per-call");
                using (await client.SendAsync(explicitAuth)) { }
            }

            Assert.Equal(new[] { "Bearer default-token", "Bearer per-call" }, transport.SeenAuthorization);
        }

        [Fact]
        public async Task RouteHost_RoutesMatchingHostsToDedicatedTransport()
        {
            var bcl = new ScriptedTransport("bcl").Respond(200);
            var lan = new ScriptedTransport("lan").Respond(200);

            using var client = ZeroHttpClient.CreateBuilder()
                .UseTransport(bcl)
                .RouteHost("10.0.*.*", lan)
                .Build();

            using (var r1 = new ZeroHttpRequestMessage(HttpMethod.Get, "http://10.0.3.17/status")) using (await client.SendAsync(r1)) { }
            using (var r2 = new ZeroHttpRequestMessage(HttpMethod.Get, "https://erp.example.com/status")) using (await client.SendAsync(r2)) { }

            Assert.Equal(1, lan.Calls);
            Assert.Equal(1, bcl.Calls);
            Assert.StartsWith("http://10.0.3.17/", lan.SeenUris[0]);
        }

        [Theory]
        [InlineData("/orders", "http://device.local/api/v1/orders")]
        [InlineData("orders", "http://device.local/api/v1/orders")]
        [InlineData("orders?page=2", "http://device.local/api/v1/orders?page=2")]
        [InlineData("http://other.local/x", "http://other.local/x")]
        public async Task ResolveUri_KeepsBasePathForLeadingSlash(string input, string expected)
        {
            var transport = new ScriptedTransport().Respond(200);
            using var client = BuildClient(transport);

            using var request = new ZeroHttpRequestMessage(HttpMethod.Get, input);
            using (await client.SendAsync(request)) { }

            Assert.Equal(expected, transport.SeenUris[0]);
        }

        [Theory]
        [InlineData("X-Test", "value\r\nInjected: 1")]
        [InlineData("X-Test", "value\nInjected")]
        [InlineData("Bad Header", "value")]
        [InlineData("Bad:Header", "value")]
        public void Headers_RejectInjectionAndInvalidNames(string name, string value)
        {
            var headers = new ZeroHttpHeaders();
            Assert.Throws<ArgumentException>(() => headers.Add(name, value));
        }

        [Fact]
        public void Dispose_DisposesOwnedTransportsOnly()
        {
            var owned = new ScriptedTransport().Respond(200);
            var shared = new ScriptedTransport().Respond(200);

            var client = ZeroHttpClient.CreateBuilder()
                .UseTransport(owned)
                .RouteHost("shared.local", shared, disposeWithClient: false)
                .Build();
            client.Dispose();

            Assert.True(owned.Disposed);
            Assert.False(shared.Disposed);
        }

        [Fact]
        public async Task TelemetryMiddleware_RecordsRequestsAndUnsuccessfulResponses()
        {
            string prefix = "test_pipeline_" + Guid.NewGuid().ToString("N");
            var transport = new ScriptedTransport().Respond(200).Respond(500);

            using var client = ZeroHttpClient.CreateBuilder()
                .WithBaseAddress(BaseUrl)
                .UseTelemetry(prefix)
                .UseTransport(transport)
                .Build();

            using (var r1 = new ZeroHttpRequestMessage(HttpMethod.Get, "a")) using (await client.SendAsync(r1)) { }
            using (var r2 = new ZeroHttpRequestMessage(HttpMethod.Get, "b")) using (await client.SendAsync(r2)) { }

            Assert.Equal(2, ZeroTelemetry.CreateCounter(prefix + "_requests_total").Value);
            Assert.Equal(1, ZeroTelemetry.CreateCounter(prefix + "_unsuccessful_responses_total").Value);
            Assert.Equal(2, ZeroTelemetry.CreateHistogram(prefix + "_request_duration_ms").Count);
        }

        [Fact]
        public void BclHttpTransportOptions_DefaultValues_AreSensible()
        {
            var options = new BclHttpTransportOptions();
            Assert.Equal(TimeSpan.FromMinutes(15), options.PooledConnectionLifetime);
            Assert.Equal(TimeSpan.FromMinutes(2), options.PooledConnectionIdleTimeout);
            Assert.Equal(50, options.MaxConnectionsPerServer);
            Assert.True(options.EnableCompression);
            Assert.Null(options.ConnectTimeout);
            Assert.Null(options.Proxy);
            Assert.Null(options.ConfigureHandler);
        }

        [Fact]
        public void BclHttpTransport_CanBeInstantiatedWithOptions()
        {
            var options = new BclHttpTransportOptions
            {
                MaxConnectionsPerServer = 10,
                EnableCompression = false
            };
            using var transport = new BclHttpTransport(options);
            Assert.NotNull(transport.HttpClient);
        }

        private static ZeroHttpResponseMessage ThrowNetworkFailure()
        {
            throw new HttpRequestException("Simulated connection reset.");
        }

        private sealed class OneShotContent : ZeroHttpContent
        {
            public override long? Length => null;
            public override bool IsReplayable => false;
            public override Task CopyToAsync(Stream destination, CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}
