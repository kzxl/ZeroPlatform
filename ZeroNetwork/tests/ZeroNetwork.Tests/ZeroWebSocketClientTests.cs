using System;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroNetwork.RealTime;

namespace ZeroNetwork.Tests
{
    public class ZeroWebSocketClientTests
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
        public async Task WebSocketClient_ConnectEchoAndClose_Succeeds()
        {
            int port = GetFreePort();
            string prefix = $"http://127.0.0.1:{port}/ws/";
            string wsUri = $"ws://127.0.0.1:{port}/ws/";

            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(prefix);
                listener.Start();

                var serverTask = Task.Run(async () =>
                {
                    var ctx = await listener.GetContextAsync();
                    if (ctx.Request.IsWebSocketRequest)
                    {
                        var wsCtx = await ctx.AcceptWebSocketAsync(null);
                        var ws = wsCtx.WebSocket;
                        var buffer = new byte[8192];

                        while (ws.State == WebSocketState.Open)
                        {
                            var res = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                            if (res.MessageType == WebSocketMessageType.Close)
                            {
                                await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                                break;
                            }
                            await ws.SendAsync(new ArraySegment<byte>(buffer, 0, res.Count), res.MessageType, res.EndOfMessage, CancellationToken.None);
                        }
                    }
                });

                var options = new ZeroWebSocketOptions
                {
                    ServerUri = new Uri(wsUri),
                    ConnectTimeout = TimeSpan.FromSeconds(5),
                    AutoReconnect = false
                };

                using (var client = new ZeroWebSocketClient(options))
                {
                    var msgTcs = new TaskCompletionSource<string>();
                    var binTcs = new TaskCompletionSource<byte[]>();

                    client.MessageReceived += msg => msgTcs.TrySetResult(msg);
                    client.BinaryReceived += bin => binTcs.TrySetResult(bin);

                    await client.ConnectAsync();
                    Assert.True(client.IsConnected);

                    // 1. Send Text Echo
                    await client.SendTextAsync("ZeroNetwork_WebSocket_Live_Echo");
                    var completedTask = await Task.WhenAny(msgTcs.Task, Task.Delay(5000));
                    Assert.Same(msgTcs.Task, completedTask);
                    Assert.Equal("ZeroNetwork_WebSocket_Live_Echo", await msgTcs.Task);

                    // 2. Send Binary Echo
                    byte[] payload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03 };
                    await client.SendBinaryAsync(payload);
                    var completedBinTask = await Task.WhenAny(binTcs.Task, Task.Delay(5000));
                    Assert.Same(binTcs.Task, completedBinTask);
                    Assert.Equal(payload, await binTcs.Task);

                    // 3. Graceful Close
                    await client.CloseAsync();
                    Assert.False(client.IsConnected);
                }

                await serverTask;
                listener.Stop();
            }
        }

        [Fact]
        public async Task WebSocketClient_SendJson_EchoesCorrectly()
        {
            int port = GetFreePort();
            string prefix = $"http://127.0.0.1:{port}/jsonws/";
            string wsUri = $"ws://127.0.0.1:{port}/jsonws/";

            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(prefix);
                listener.Start();

                var serverTask = Task.Run(async () =>
                {
                    var ctx = await listener.GetContextAsync();
                    if (ctx.Request.IsWebSocketRequest)
                    {
                        var wsCtx = await ctx.AcceptWebSocketAsync(null);
                        var ws = wsCtx.WebSocket;
                        var buffer = new byte[4096];

                        var res = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        await ws.SendAsync(new ArraySegment<byte>(buffer, 0, res.Count), res.MessageType, res.EndOfMessage, CancellationToken.None);

                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    }
                });

                using (var client = new ZeroWebSocketClient(wsUri))
                {
                    var tcs = new TaskCompletionSource<string>();
                    client.MessageReceived += msg => tcs.TrySetResult(msg);

                    await client.ConnectAsync();

                    var dto = new TestSensorDto { SensorId = "TEMP-01", Temperature = 36.6, Active = true };
                    await client.SendJsonAsync(dto);

                    var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
                    Assert.Same(tcs.Task, completed);
                    string receivedJson = await tcs.Task;
                    Assert.Contains("TEMP-01", receivedJson);
                    Assert.Contains("36.6", receivedJson);

                    await client.CloseAsync();
                }

                await serverTask;
                listener.Stop();
            }
        }

        [Fact]
        public async Task WebSocketServer_DirectClientConnectionAndBroadcast_Succeeds()
        {
            int port = GetFreePort();

            using (var server = new ZeroWebSocketServer(port, "127.0.0.1"))
            {
                var serverReceivedTcs = new TaskCompletionSource<string>();
                server.SessionConnected += session =>
                {
                    session.TextReceived += (s, msg) =>
                    {
                        serverReceivedTcs.TrySetResult(msg);
                        // Echo response
                        _ = s.SendTextAsync($"Echo:{msg}");
                    };
                };

                server.Start();
                Assert.True(server.IsRunning);

                string wsUri = $"ws://127.0.0.1:{port}/";
                var options = new ZeroWebSocketOptions
                {
                    ServerUri = new Uri(wsUri),
                    ConnectTimeout = TimeSpan.FromSeconds(5),
                    AutoReconnect = false
                };

                using (var client = new ZeroWebSocketClient(options))
                {
                    var clientReceivedTcs = new TaskCompletionSource<string>();
                    client.MessageReceived += msg => clientReceivedTcs.TrySetResult(msg);

                    await client.ConnectAsync();
                    Assert.True(client.IsConnected);
                    Assert.Equal(1, server.ActiveSessionCount);

                    await client.SendTextAsync("HelloPureCSharpServer");

                    var completedServer = await Task.WhenAny(serverReceivedTcs.Task, Task.Delay(3000));
                    Assert.Same(serverReceivedTcs.Task, completedServer);
                    Assert.Equal("HelloPureCSharpServer", await serverReceivedTcs.Task);

                    var completedClient = await Task.WhenAny(clientReceivedTcs.Task, Task.Delay(3000));
                    Assert.Same(clientReceivedTcs.Task, completedClient);
                    Assert.Equal("Echo:HelloPureCSharpServer", await clientReceivedTcs.Task);

                    // Test Broadcast
                    var broadcastTcs = new TaskCompletionSource<string>();
                    client.MessageReceived += msg =>
                    {
                        if (msg.StartsWith("Broadcast:"))
                            broadcastTcs.TrySetResult(msg);
                    };

                    await server.BroadcastTextAsync("Broadcast:AllUnitsActive");
                    var completedBroadcast = await Task.WhenAny(broadcastTcs.Task, Task.Delay(3000));
                    Assert.Same(broadcastTcs.Task, completedBroadcast);
                    Assert.Equal("Broadcast:AllUnitsActive", await broadcastTcs.Task);

                    await client.CloseAsync();
                }

                server.Stop();
                Assert.False(server.IsRunning);
            }
        }

        private class TestSensorDto
        {
            public string SensorId { get; set; } = "";
            public double Temperature { get; set; }
            public bool Active { get; set; }
        }
    }
}
