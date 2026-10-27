using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroUI.Core.Communication;
using ZeroUI.Core.Historian;
using ZeroUI.Core.Runtime;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Scene;

namespace ZeroUI.Core.Tests
{
    public class ProtocolSchedulerAndHistorianPipelineTests
    {
        [Fact]
        public void ZeroProtocolScheduler_RegistersAndUnregistersAdapter()
        {
            var bus = new ZeroTelemetryBus("MockBus");
            var scheduler = new ZeroProtocolScheduler("TestProtoScheduler", bus);

            var mockAdapter = new MockProtocolAdapter("Modbus_Line1", "192.168.1.100:502");
            var item = scheduler.RegisterAdapter(mockAdapter, TimeSpan.FromMilliseconds(50));

            Assert.NotNull(item);
            Assert.Equal(1, scheduler.AdapterCount);
            Assert.True(scheduler.TryGetAdapter("Modbus_Line1", out var retrieved));
            Assert.Same(mockAdapter, retrieved!.Adapter);

            Assert.True(scheduler.UnregisterAdapter("Modbus_Line1"));
            Assert.Equal(0, scheduler.AdapterCount);
        }

        [Fact]
        public void ZeroHistorianPipeline_IngestsBatch_FillsRingBuffer()
        {
            var bus = new ZeroTelemetryBus("HistorianBus");
            var pipeline = new ZeroHistorianPipeline("TestHistorian", bus);

            int tagId = 42;
            pipeline.ConfigureTagRingBuffer(tagId, capacity: 50);

            Span<TagUpdate> batch = stackalloc TagUpdate[3];
            batch[0] = new TagUpdate(tagId, new ScadaValue(10.0), 1000);
            batch[1] = new TagUpdate(tagId, new ScadaValue(20.0), 2000);
            batch[2] = new TagUpdate(tagId, new ScadaValue(30.0), 3000);

            bus.Publish(batch);

            Assert.Equal(3, pipeline.TotalIngestedSamples);
            Assert.Equal(1, pipeline.TotalBatchesIngested);

            var snapshot = pipeline.QueryRecentInMemory(tagId);
            Assert.Equal(3, snapshot.Count);
            Assert.Equal(10.0, snapshot[0].Y);
            Assert.Equal(30.0, snapshot[2].Y);
        }

        [Fact]
        public void ZeroSceneNode_FactoryMethods_CreateValidArchetypes()
        {
            var tank = ZeroSceneNode.CreateTank("T1", "Main Tank", 100, 100, w: 80, h: 120, tagId: 5);
            Assert.Equal("T1", tank.Id);
            Assert.Equal(IndustrialNodeType.Tank, tank.NodeType);
            Assert.Equal(5, tank.BoundTagId);
            Assert.Equal(100f, tank.X);
            Assert.Equal(100f, tank.Y);
            Assert.Equal(80f, tank.Width);
            Assert.Equal(120f, tank.Height);

            var pump = ZeroSceneNode.CreatePump("P1", "Feed Pump", 250, 150, radius: 20, tagId: 6);
            Assert.Equal(IndustrialNodeType.Pump, pump.NodeType);
            Assert.Equal(6, pump.BoundTagId);
            Assert.Equal(40f, pump.Width);

            var sensor = ZeroSceneNode.CreateSensor("S1", "Temp Sensor", 300, 100, unit: "°C", tagId: 7);
            Assert.Equal(IndustrialNodeType.Sensor, sensor.NodeType);
            Assert.Equal(7, sensor.BoundTagId);
            Assert.Equal("°C", sensor.EngineeringUnit);
        }

        [Fact]
        public void ZeroSceneNode_BindTagAndUpdateTelemetry_UpdatesValue()
        {
            var node = new ZeroSceneNode("Node_1", "Test Node", IndustrialNodeType.Tank);
            node.BindTag(12, "Plant/Tank/Level");

            Assert.Equal(12, node.BoundTagId);
            Assert.Equal("Plant/Tank/Level", node.BoundTagPath);

            node.UpdateTelemetry(new ScadaValue(78.5, ScadaQuality.Good));
            Assert.Equal(78.5, node.Value);

            node.UpdateTelemetry(new ScadaValue(0.0, ScadaQuality.Bad));
            Assert.Equal(ScadaNodeState.Fault, node.State);
        }

        private sealed class MockProtocolAdapter : IProtocolAdapter
        {
            public string AdapterId { get; }
            public string Endpoint { get; }
            public AdapterConnectionState State { get; private set; } = AdapterConnectionState.Disconnected;
            public TimeSpan Latency => TimeSpan.FromMilliseconds(1.5);

            public event Action<IProtocolAdapter, AdapterConnectionState>? StateChanged;

            public MockProtocolAdapter(string id, string endpoint)
            {
                AdapterId = id;
                Endpoint = endpoint;
            }

            public void RegisterTag(AdapterTagDefinition tagDef) { }
            public IReadOnlyCollection<AdapterTagDefinition> GetRegisteredTags() => Array.Empty<AdapterTagDefinition>();

            public Task ConnectAsync(CancellationToken cancellationToken = default)
            {
                State = AdapterConnectionState.Connected;
                StateChanged?.Invoke(this, State);
                return Task.CompletedTask;
            }

            public Task DisconnectAsync(CancellationToken cancellationToken = default)
            {
                State = AdapterConnectionState.Disconnected;
                StateChanged?.Invoke(this, State);
                return Task.CompletedTask;
            }

            public Task PollOnceAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<bool> WriteTagAsync(string tagPath, object value, CancellationToken cancellationToken = default) => Task.FromResult(true);

            public void Dispose()
            {
                DisconnectAsync().GetAwaiter().GetResult();
            }
        }
    }
}
