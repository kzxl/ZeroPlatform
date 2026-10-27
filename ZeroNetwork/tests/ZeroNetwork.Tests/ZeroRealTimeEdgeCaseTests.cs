using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroNetwork.RealTime;

namespace ZeroNetwork.Tests
{
    public class ZeroRealTimeEdgeCaseTests
    {
        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public class AdvancedEdgeCaseHub : ZeroHub
        {
            public static int ConnectCount;
            public static int DisconnectCount;

            public override Task OnConnectedAsync()
            {
                Interlocked.Increment(ref ConnectCount);
                return Task.CompletedTask;
            }

            public override Task OnDisconnectedAsync(Exception? exception)
            {
                Interlocked.Increment(ref DisconnectCount);
                return Task.CompletedTask;
            }

            public async Task JoinVipGroup()
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "VIP");
            }

            public async Task LeaveVipGroup()
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "VIP");
            }

            public async Task BroadcastToVip(string message)
            {
                await Clients.Group("VIP").SendAsync("OnVipMessage", message);
            }

            public async Task NotifyOthers(string message)
            {
                await Clients.Others.SendAsync("OnOtherMessage", message);
            }

            public void FaultyMethod()
            {
                throw new InvalidOperationException("Simulated PLC Hardware Communication Failure");
            }

            public int MultiplyThree(int a, int b, int c)
            {
                return a * b * c;
            }
        }

        [Fact]
        public async Task WebSocketServer_LargePayloadFraming_126And127Headers_Succeed()
        {
            int port = GetFreePort();

            using (var server = new ZeroWebSocketServer(port, "127.0.0.1"))
            {
                server.SessionConnected += session =>
                {
                    session.BinaryReceived += (s, bin) =>
                    {
                        // Echo back the binary data
                        _ = s.SendBinaryAsync(bin);
                    };
                };

                server.Start();

                string wsUri = $"ws://127.0.0.1:{port}/";
                var options = new ZeroWebSocketOptions
                {
                    ServerUri = new Uri(wsUri),
                    ConnectTimeout = TimeSpan.FromSeconds(5),
                    AutoReconnect = false
                };

                using (var client = new ZeroWebSocketClient(options))
                {
                    await client.ConnectAsync();
                    Assert.True(client.IsConnected);

                    // 1. Medium Payload (> 125 bytes -> 126 length header)
                    byte[] mediumPayload = new byte[1024];
                    for (int i = 0; i < mediumPayload.Length; i++) mediumPayload[i] = (byte)(i % 256);

                    var mediumTcs = new TaskCompletionSource<byte[]>();
                    Action<byte[]> mediumHandler = bin => mediumTcs.TrySetResult(bin);
                    client.BinaryReceived += mediumHandler;

                    await client.SendBinaryAsync(mediumPayload);
                    var completedMedium = await Task.WhenAny(mediumTcs.Task, Task.Delay(4000));
                    Assert.Same(mediumTcs.Task, completedMedium);
                    Assert.Equal(mediumPayload, await mediumTcs.Task);

                    client.BinaryReceived -= mediumHandler;

                    // 2. Large Payload (> 65535 bytes -> 127 length 64-bit header)
                    byte[] largePayload = new byte[70000];
                    for (int i = 0; i < largePayload.Length; i++) largePayload[i] = (byte)(i % 128);

                    var largeTcs = new TaskCompletionSource<byte[]>();
                    client.BinaryReceived += bin => largeTcs.TrySetResult(bin);

                    await client.SendBinaryAsync(largePayload);
                    var completedLarge = await Task.WhenAny(largeTcs.Task, Task.Delay(6000));
                    Assert.Same(largeTcs.Task, completedLarge);
                    Assert.Equal(largePayload, await largeTcs.Task);

                    await client.CloseAsync();
                }

                server.Stop();
            }
        }

        [Fact]
        public async Task HubServer_GroupIsolation_And_OthersProxy_BehaveCorrectly()
        {
            int port = GetFreePort();

            using (var server = new ZeroHubServer<AdvancedEdgeCaseHub>(port, "127.0.0.1"))
            {
                server.Start();

                string hubUrl = $"ws://127.0.0.1:{port}/edgehub";
                var optA = new ZeroSignalROptions { HubUri = new Uri(hubUrl), AutoReconnect = false };
                var optB = new ZeroSignalROptions { HubUri = new Uri(hubUrl), AutoReconnect = false };
                var optC = new ZeroSignalROptions { HubUri = new Uri(hubUrl), AutoReconnect = false };

                using (var clientA = new ZeroSignalRClient(optA))
                using (var clientB = new ZeroSignalRClient(optB))
                using (var clientC = new ZeroSignalRClient(optC))
                {
                    await clientA.StartAsync();
                    await clientB.StartAsync();
                    await clientC.StartAsync();

                    Assert.Equal(3, server.ConnectedCount);

                    // Client A and B join VIP group
                    await clientA.SendAsync("JoinVipGroup");
                    await clientB.SendAsync("JoinVipGroup");
                    await Task.Delay(100);

                    // Setup VIP message listeners
                    var vipTcsA = new TaskCompletionSource<string>();
                    var vipTcsB = new TaskCompletionSource<string>();
                    var vipTcsC = new TaskCompletionSource<string>();

                    clientA.On<string>("OnVipMessage", msg => vipTcsA.TrySetResult(msg));
                    clientB.On<string>("OnVipMessage", msg => vipTcsB.TrySetResult(msg));
                    clientC.On<string>("OnVipMessage", msg => vipTcsC.TrySetResult(msg));

                    // Broadcast to VIP group from Client A
                    await clientA.SendAsync("BroadcastToVip", "VIP_ONLY_DATA");

                    var completedA = await Task.WhenAny(vipTcsA.Task, Task.Delay(3000));
                    var completedB = await Task.WhenAny(vipTcsB.Task, Task.Delay(3000));
                    Assert.Same(vipTcsA.Task, completedA);
                    Assert.Same(vipTcsB.Task, completedB);
                    Assert.Equal("VIP_ONLY_DATA", await vipTcsA.Task);
                    Assert.Equal("VIP_ONLY_DATA", await vipTcsB.Task);

                    // Ensure Client C did NOT receive the VIP message
                    Assert.False(vipTcsC.Task.IsCompleted);

                    // Test Clients.Others
                    var otherTcsA = new TaskCompletionSource<string>();
                    var otherTcsB = new TaskCompletionSource<string>();
                    var otherTcsC = new TaskCompletionSource<string>();

                    clientA.On<string>("OnOtherMessage", msg => otherTcsA.TrySetResult(msg));
                    clientB.On<string>("OnOtherMessage", msg => otherTcsB.TrySetResult(msg));
                    clientC.On<string>("OnOtherMessage", msg => otherTcsC.TrySetResult(msg));

                    // Client A notifies others
                    await clientA.SendAsync("NotifyOthers", "Hello_Peers");

                    var completedOtherB = await Task.WhenAny(otherTcsB.Task, Task.Delay(3000));
                    var completedOtherC = await Task.WhenAny(otherTcsC.Task, Task.Delay(3000));
                    Assert.Same(otherTcsB.Task, completedOtherB);
                    Assert.Same(otherTcsC.Task, completedOtherC);

                    // Client A (caller) must NOT receive its own Others notification
                    Assert.False(otherTcsA.Task.IsCompleted);

                    await clientA.StopAsync();
                    await clientB.StopAsync();
                    await clientC.StopAsync();
                }

                server.Stop();
            }
        }

        [Fact]
        public async Task HubServer_FaultHandling_And_MultiArgInvocations_WorkReliably()
        {
            int port = GetFreePort();

            using (var server = new ZeroHubServer<AdvancedEdgeCaseHub>(port, "127.0.0.1"))
            {
                server.Start();

                string hubUrl = $"ws://127.0.0.1:{port}/faultyhub";
                var options = new ZeroSignalROptions
                {
                    HubUri = new Uri(hubUrl),
                    InvocationTimeout = TimeSpan.FromSeconds(5),
                    AutoReconnect = false
                };

                using (var client = new ZeroSignalRClient(options))
                {
                    await client.StartAsync();

                    // 1. Invocation of unknown method returns error
                    var exUnknown = await Assert.ThrowsAsync<InvalidOperationException>(() => client.InvokeAsync<string>("NonExistentMethod"));
                    Assert.Contains("not found", exUnknown.Message);

                    // 2. Invocation of faulty method returns exception error message
                    var exFault = await Assert.ThrowsAsync<InvalidOperationException>(() => client.InvokeAsync<string>("FaultyMethod"));
                    Assert.Contains("Simulated PLC Hardware Communication Failure", exFault.Message);

                    // 3. Multi-argument RPC with 3 parameters: MultiplyThree(4, 5, 6) = 120
                    int product = await client.InvokeAsync<int>("MultiplyThree", 4, 5, 6);
                    Assert.Equal(120, product);

                    await client.StopAsync();
                }

                server.Stop();
            }
        }
    }
}
