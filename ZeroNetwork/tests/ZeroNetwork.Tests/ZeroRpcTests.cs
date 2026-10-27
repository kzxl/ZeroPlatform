using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroNetwork.Rpc;
using ZeroPlatform.Concurrency.RateLimiting;
using ZeroPrimitives.Core.Identifiers;

namespace ZeroNetwork.Tests
{
    public class ZeroRpcTests
    {
        [Fact]
        public void ZeroRpc_HeaderEncodingDecoding_RoundtripsSuccessfully()
        {
            var correlationId = FastUlid.NewUlid();
            var header = ZeroRpcHeader.CreateRequest(methodId: 42, payloadLength: 100, checksum: 0x12345678, correlationId: correlationId);

            Span<byte> buffer = stackalloc byte[ZeroRpcHeader.HeaderLength];
            header.Encode(buffer);

            Assert.True(ZeroRpcHeader.TryDecode(buffer, out var decoded));
            Assert.Equal(ZeroRpcHeader.ExpectedMagic, decoded.Magic);
            Assert.Equal(ZeroRpcHeader.CurrentVersion, decoded.Version);
            Assert.True(decoded.IsRequest);
            Assert.False(decoded.IsResponse);
            Assert.Equal(42, decoded.MethodId);
            Assert.Equal(correlationId, decoded.CorrelationId);
            Assert.Equal(100, decoded.PayloadLength);
            Assert.Equal(0x12345678u, decoded.Checksum);
        }

        [Fact]
        public async Task ZeroRpc_EchoRequestResponse_RoundtripsSuccessfully()
        {
            int port = 30000 + new Random().Next(1000, 9000);
            using var server = new ZeroRpcServer(IPAddress.Loopback, port);

            // Register Echo method (MethodId = 1)
            server.RegisterHandler(1, ctx =>
            {
                // Echo back the same payload
                return new ValueTask<(ZeroRpcStatus, ReadOnlyMemory<byte>)>(ZeroRpcContext.Success(ctx.Payload));
            });

            server.Start();

            using var client = new ZeroRpcClient();
            await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port));
            Assert.True(client.IsConnected);

            byte[] requestPayload = Encoding.UTF8.GetBytes("Hello Sovereign ZeroRpc!");
            var response = await client.InvokeAsync(1, requestPayload, timeout: TimeSpan.FromSeconds(5));

            Assert.True(response.IsSuccess);
            Assert.Equal(ZeroRpcStatus.Ok, response.Status);
            Assert.Equal("Hello Sovereign ZeroRpc!", Encoding.UTF8.GetString(response.Payload.ToArray()));
        }

        [Fact]
        public async Task ZeroRpc_MultiplexedConcurrentCalls_InterleavesAcrossSingleConnection()
        {
            int port = 30000 + new Random().Next(1000, 9000);
            using var server = new ZeroRpcServer(IPAddress.Loopback, port);

            // Register handler that doubles an integer
            server.RegisterHandler(2, ctx =>
            {
                int val = BitConverter.ToInt32(ctx.Payload.ToArray(), 0);
                byte[] resp = BitConverter.GetBytes(val * 2);
                return new ValueTask<(ZeroRpcStatus, ReadOnlyMemory<byte>)>(ZeroRpcContext.Success(resp));
            });

            server.Start();

            using var client = new ZeroRpcClient();
            await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port));

            const int callCount = 20;
            var tasks = new Task[callCount];

            for (int i = 0; i < callCount; i++)
            {
                int num = i + 1;
                tasks[i] = Task.Run(async () =>
                {
                    byte[] payload = BitConverter.GetBytes(num);
                    var resp = await client.InvokeAsync(2, payload, timeout: TimeSpan.FromSeconds(5));
                    Assert.True(resp.IsSuccess);
                    int result = BitConverter.ToInt32(resp.Payload.ToArray(), 0);
                    Assert.Equal(num * 2, result);
                });
            }

            await Task.WhenAll(tasks);
        }

        [Fact]
        public async Task ZeroRpc_MethodNotFound_Returns404()
        {
            int port = 30000 + new Random().Next(1000, 9000);
            using var server = new ZeroRpcServer(IPAddress.Loopback, port);
            server.Start();

            using var client = new ZeroRpcClient();
            await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port));

            var resp = await client.InvokeAsync(999, ReadOnlyMemory<byte>.Empty, timeout: TimeSpan.FromSeconds(5));
            Assert.False(resp.IsSuccess);
            Assert.Equal(ZeroRpcStatus.NotFound, resp.Status);
        }

        [Fact]
        public async Task ZeroRpc_Heartbeat_PingsAndPongs()
        {
            int port = 30000 + new Random().Next(1000, 9000);
            using var server = new ZeroRpcServer(IPAddress.Loopback, port);
            server.Start();

            using var client = new ZeroRpcClient();
            await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port));

            bool pong = await client.PingAsync(TimeSpan.FromSeconds(3));
            Assert.True(pong);
        }

        [Fact]
        public async Task ZeroRpc_OneWayEvent_DispatchesWithoutResponse()
        {
            int port = 30000 + new Random().Next(1000, 9000);
            using var server = new ZeroRpcServer(IPAddress.Loopback, port);

            var eventReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            server.RegisterHandler(5, ctx =>
            {
                Assert.True(ctx.IsOneWay);
                eventReceived.TrySetResult(Encoding.UTF8.GetString(ctx.Payload.ToArray()));
                return new ValueTask<(ZeroRpcStatus, ReadOnlyMemory<byte>)>(ZeroRpcContext.Success(ReadOnlyMemory<byte>.Empty));
            });

            server.Start();

            using var client = new ZeroRpcClient();
            await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port));

            await client.SendOneWayAsync(5, Encoding.UTF8.GetBytes("Fire and Forget Event"));

            var completedTask = await Task.WhenAny(eventReceived.Task, Task.Delay(3000));
            Assert.Equal(eventReceived.Task, completedTask);
            Assert.Equal("Fire and Forget Event", await eventReceived.Task);
        }

        [Fact]
        public async Task ZeroRpc_RateLimiting_RejectsExcessCallsWith429()
        {
            int port = 30000 + new Random().Next(1000, 9000);
            using var server = new ZeroRpcServer(IPAddress.Loopback, port);

            // Allow only 2 calls initially, 0.1 tokens/sec refill
            var limiter = new TokenBucketRateLimiter(capacity: 2, tokensPerSecond: 0.1);
            server.WithRateLimiter(limiter);

            server.RegisterHandler(10, ctx =>
            {
                return new ValueTask<(ZeroRpcStatus, ReadOnlyMemory<byte>)>(ZeroRpcContext.Success(Encoding.UTF8.GetBytes("OK")));
            });

            server.Start();

            using var client = new ZeroRpcClient();
            await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port));

            // Call 1 -> OK
            var r1 = await client.InvokeAsync(10, ReadOnlyMemory<byte>.Empty, timeout: TimeSpan.FromSeconds(3));
            Assert.True(r1.IsSuccess);

            // Call 2 -> OK
            var r2 = await client.InvokeAsync(10, ReadOnlyMemory<byte>.Empty, timeout: TimeSpan.FromSeconds(3));
            Assert.True(r2.IsSuccess);

            // Call 3 -> 429 Too Many Requests
            var r3 = await client.InvokeAsync(10, ReadOnlyMemory<byte>.Empty, timeout: TimeSpan.FromSeconds(3));
            Assert.False(r3.IsSuccess);
            Assert.Equal(ZeroRpcStatus.TooManyRequests, r3.Status);
        }
    }
}
