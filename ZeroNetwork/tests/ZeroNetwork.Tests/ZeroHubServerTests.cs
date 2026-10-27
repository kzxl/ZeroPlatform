using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;
using ZeroNetwork.PubSub;
using ZeroNetwork.RealTime;

namespace ZeroNetwork.Tests
{
    public class ZeroHubServerTests
    {
        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public class TestChatHub : ZeroHub
        {
            public async Task BroadcastMessage(string user, string message)
            {
                await Clients.All.SendAsync("ReceiveMessage", user, message);
            }

            public int AddNumbers(int a, int b)
            {
                return a + b;
            }

            public async Task JoinRoom(string room)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, room);
            }

            public async Task SendToRoom(string room, string message)
            {
                await Clients.Group(room).SendAsync("ReceiveRoomMessage", message);
            }
        }

        [Fact]
        public async Task ZeroHubServer_FullDuplexRpcAndBroadcast_Succeeds()
        {
            int port = GetFreePort();

            using (var server = new ZeroHubServer<TestChatHub>(port, "127.0.0.1"))
            {
                server.Start();
                Assert.True(server.IsRunning);

                string hubUrl = $"ws://127.0.0.1:{port}/chathub";
                var options = new ZeroSignalROptions
                {
                    HubUri = new Uri(hubUrl),
                    HandshakeTimeout = TimeSpan.FromSeconds(5),
                    InvocationTimeout = TimeSpan.FromSeconds(5),
                    AutoReconnect = false
                };

                using (var client = new ZeroSignalRClient(options))
                {
                    var msgTcs = new TaskCompletionSource<(string User, string Msg)>();

                    client.On<string, string>("ReceiveMessage", (user, msg) =>
                    {
                        msgTcs.TrySetResult((user, msg));
                    });

                    // 1. Connect and perform SignalR Handshake
                    await client.StartAsync();
                    Assert.True(client.IsConnected);
                    Assert.Equal(1, server.ConnectedCount);

                    // 2. Client sends invocation -> Hub broadcasts back to All
                    await client.SendAsync("BroadcastMessage", "ScadaOperator", "Machine Ready");

                    var completed = await Task.WhenAny(msgTcs.Task, Task.Delay(4000));
                    Assert.Same(msgTcs.Task, completed);

                    var received = await msgTcs.Task;
                    Assert.Equal("ScadaOperator", received.User);
                    Assert.Equal("Machine Ready", received.Msg);

                    // 3. Request-response RPC with return value
                    int sum = await client.InvokeAsync<int>("AddNumbers", 125, 75);
                    Assert.Equal(200, sum);

                    await client.StopAsync();
                }

                server.Stop();
                Assert.False(server.IsRunning);
            }
        }

        [Fact]
        public async Task ZeroPubSubHub_TopicRoutingBetweenMultipleClients_Succeeds()
        {
            int port = GetFreePort();

            using (var server = new ZeroHubServer<ZeroPubSubHub>(port, "127.0.0.1"))
            {
                server.Start();

                string hubUrl = $"ws://127.0.0.1:{port}/pubsubhub";
                var options1 = new ZeroSignalROptions { HubUri = new Uri(hubUrl), AutoReconnect = false };
                var options2 = new ZeroSignalROptions { HubUri = new Uri(hubUrl), AutoReconnect = false };

                using (var client1 = new ZeroSignalRClient(options1))
                using (var client2 = new ZeroSignalRClient(options2))
                {
                    var topicTcs = new TaskCompletionSource<(string Topic, string Payload)>();

                    client1.On<string, string>("OnTopicMessage", (topic, payload) =>
                    {
                        topicTcs.TrySetResult((topic, payload));
                    });

                    await client1.StartAsync();
                    await client2.StartAsync();

                    Assert.Equal(2, server.ConnectedCount);

                    // Client 1 subscribes with single wildcard
                    await client1.SendAsync("Subscribe", "scada/+/temperature");

                    // Small delay to ensure subscription propagation
                    await Task.Delay(100);

                    // Client 2 publishes to matching concrete topic
                    await client2.SendAsync("Publish", "scada/furnace1/temperature", "1024.5 C");

                    var completed = await Task.WhenAny(topicTcs.Task, Task.Delay(4000));
                    Assert.Same(topicTcs.Task, completed);

                    var result = await topicTcs.Task;
                    Assert.Equal("scada/furnace1/temperature", result.Topic);
                    Assert.Equal("1024.5 C", result.Payload);

                    await client1.StopAsync();
                    await client2.StopAsync();
                }

                server.Stop();
            }
        }
    }
}
