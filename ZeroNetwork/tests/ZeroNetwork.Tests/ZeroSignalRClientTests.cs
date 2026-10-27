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
    public class ZeroSignalRClientTests
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
        public void SignalRProtocol_ParsingTests_WorkCorrectly()
        {
            // 1. Invocation message from server
            string serverInvocation = "{\"type\":1,\"target\":\"OnStockUpdated\",\"arguments\":[{\"sku\":\"SKU-999\",\"qty\":150},\"Warehouse-A\"]}";
            var msg1 = SignalRProtocol.ParseHubMessage(serverInvocation);
            Assert.NotNull(msg1);
            Assert.Equal(1, msg1!.Type);
            Assert.Equal("OnStockUpdated", msg1.Target);
            Assert.Equal(2, msg1.RawArguments.Count);
            Assert.Equal("{\"sku\":\"SKU-999\",\"qty\":150}", msg1.RawArguments[0]);
            Assert.Equal("\"Warehouse-A\"", msg1.RawArguments[1]);

            // 2. Completion message
            string completion = "{\"type\":3,\"invocationId\":\"42\",\"result\":100}";
            var msg2 = SignalRProtocol.ParseHubMessage(completion);
            Assert.NotNull(msg2);
            Assert.Equal(3, msg2!.Type);
            Assert.Equal("42", msg2.InvocationId);
            Assert.Equal("100", msg2.RawResult);

            // 3. Ping message
            string ping = "{\"type\":6}";
            var msg3 = SignalRProtocol.ParseHubMessage(ping);
            Assert.NotNull(msg3);
            Assert.Equal(6, msg3!.Type);

            // 4. Close message
            string close = "{\"type\":7,\"error\":\"Server shutting down\"}";
            var msg4 = SignalRProtocol.ParseHubMessage(close);
            Assert.NotNull(msg4);
            Assert.Equal(7, msg4!.Type);
            Assert.Equal("Server shutting down", msg4.Error);
        }

        [Fact]
        public async Task SignalRClient_HandshakeAndInvocations_Succeeds()
        {
            int port = GetFreePort();
            string prefix = $"http://127.0.0.1:{port}/signalrhub/";
            string hubUrl = $"ws://127.0.0.1:{port}/signalrhub/";

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

                        // 1. Receive Handshake Request from Client: {"protocol":"json","version":1}\u001e
                        var res = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        string handshakeReq = Encoding.UTF8.GetString(buffer, 0, res.Count);
                        Assert.Contains("\"protocol\":\"json\"", handshakeReq);

                        // 2. Send Handshake Response: {}\u001e
                        byte[] handshakeResp = Encoding.UTF8.GetBytes("{}\u001e");
                        await ws.SendAsync(new ArraySegment<byte>(handshakeResp), WebSocketMessageType.Text, true, CancellationToken.None);

                        // 3. Send Server-to-Client invocation: OnNotification("Welcome to ZeroSignalR")
                        string serverInvocation = "{\"type\":1,\"target\":\"OnNotification\",\"arguments\":[\"Welcome to ZeroSignalR\"]}\u001e";
                        byte[] invBytes = Encoding.UTF8.GetBytes(serverInvocation);
                        await ws.SendAsync(new ArraySegment<byte>(invBytes), WebSocketMessageType.Text, true, CancellationToken.None);

                        // 4. Receive Client invocation and return result: Add(15, 25) => 40
                        var clientMsgRes = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        string clientMsg = Encoding.UTF8.GetString(buffer, 0, clientMsgRes.Count);
                        Assert.Contains("\"target\":\"Add\"", clientMsg);

                        var parsedClientMsg = SignalRProtocol.ParseHubMessage(clientMsg.TrimEnd('\u001e'));
                        Assert.NotNull(parsedClientMsg);

                        string completionMsg = $"{{\"type\":3,\"invocationId\":\"{parsedClientMsg!.InvocationId}\",\"result\":40}}\u001e";
                        byte[] compBytes = Encoding.UTF8.GetBytes(completionMsg);
                        await ws.SendAsync(new ArraySegment<byte>(compBytes), WebSocketMessageType.Text, true, CancellationToken.None);

                        // Keep open until close received
                        try
                        {
                            var closeRes = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                            if (closeRes.MessageType == WebSocketMessageType.Close)
                            {
                                await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                            }
                        }
                        catch { }
                    }
                });

                var options = new ZeroSignalROptions
                {
                    HubUri = new Uri(hubUrl),
                    HandshakeTimeout = TimeSpan.FromSeconds(5),
                    InvocationTimeout = TimeSpan.FromSeconds(5),
                    AutoReconnect = false
                };

                using (var client = new ZeroSignalRClient(options))
                {
                    var notifTcs = new TaskCompletionSource<string>();

                    client.On<string>("OnNotification", msg =>
                    {
                        notifTcs.TrySetResult(msg);
                    });

                    // Connect and perform handshake
                    await client.StartAsync();
                    Assert.True(client.IsConnected);

                    // Verify server-to-client invocation
                    var completedNotif = await Task.WhenAny(notifTcs.Task, Task.Delay(5000));
                    Assert.Same(notifTcs.Task, completedNotif);
                    Assert.Equal("Welcome to ZeroSignalR", await notifTcs.Task);

                    // Verify request-response invocation: Add(15, 25)
                    int result = await client.InvokeAsync<int>("Add", 15, 25);
                    Assert.Equal(40, result);

                    await client.StopAsync();
                    Assert.False(client.IsConnected);
                }

                await serverTask;
                listener.Stop();
            }
        }
    }
}
