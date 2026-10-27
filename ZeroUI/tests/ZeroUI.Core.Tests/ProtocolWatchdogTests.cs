using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroUI.Core.Communication;
using ZeroUI.Core.Runtime;

namespace ZeroUI.Core.Tests
{
    public class ProtocolWatchdogTests
    {
        private sealed class MockHangingAdapter : IProtocolAdapter
        {
            public string AdapterId { get; }
            public string Endpoint => "mock://127.0.0.1";
            public AdapterConnectionState State { get; private set; } = AdapterConnectionState.Disconnected;
            public TimeSpan Latency => TimeSpan.FromMilliseconds(1);
            public event Action<IProtocolAdapter, AdapterConnectionState>? StateChanged;

            public int ConnectCallCount = 0;
            public int DisconnectCallCount = 0;
            public int PollCallCount = 0;
            public bool ShouldHangOnPoll = false;

            public MockHangingAdapter(string id)
            {
                AdapterId = id;
            }

            public Task ConnectAsync(CancellationToken cancellationToken = default)
            {
                ConnectCallCount++;
                State = AdapterConnectionState.Connected;
                StateChanged?.Invoke(this, State);
                return Task.CompletedTask;
            }

            public Task DisconnectAsync(CancellationToken cancellationToken = default)
            {
                DisconnectCallCount++;
                State = AdapterConnectionState.Disconnected;
                StateChanged?.Invoke(this, State);
                return Task.CompletedTask;
            }

            public async Task PollOnceAsync(CancellationToken cancellationToken = default)
            {
                PollCallCount++;
                if (ShouldHangOnPoll)
                {
                    // Simulate frozen network socket: delay longer than watchdog
                    await Task.Delay(1000, cancellationToken);
                }
            }

            public Task<bool> WriteTagAsync(string tagPath, object value, CancellationToken cancellationToken = default)
                => Task.FromResult(true);

            public void RegisterTag(AdapterTagDefinition tagDef) { }
            public IReadOnlyCollection<AdapterTagDefinition> GetRegisteredTags() => Array.Empty<AdapterTagDefinition>();
            public void Dispose() => DisconnectAsync();
        }

        [Fact]
        public async Task ConnectionManager_Watchdog_DetectsHungSocketAndDisconnects()
        {
            using (var manager = new ConnectionManager())
            {
                var adapter = new MockHangingAdapter("watchdog_mock_1");

                // Poll every 30ms, Watchdog timeout after 100ms
                manager.RegisterAdapter(
                    adapter,
                    pollInterval: TimeSpan.FromMilliseconds(30),
                    autoReconnect: true,
                    watchdogTimeout: TimeSpan.FromMilliseconds(100));

                await manager.StartAllAsync();

                // Wait 100ms for initial normal connection and poll
                await Task.Delay(100);
                Assert.Equal(AdapterConnectionState.Connected, adapter.State);
                Assert.True(adapter.PollCallCount > 0);

                int initialDisconnects = adapter.DisconnectCallCount;

                // Now trigger a hang in socket communication
                adapter.ShouldHangOnPoll = true;

                // Wait for watchdog timeout (100ms) + buffer
                await Task.Delay(250);

                // Watchdog should have detected the hang, forced a disconnect, and attempted reconnect
                Assert.True(adapter.DisconnectCallCount > initialDisconnects,
                    $"Expected DisconnectCallCount > {initialDisconnects}, but got {adapter.DisconnectCallCount}");

                await manager.StopAllAsync();
            }
        }
    }
}
