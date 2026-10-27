using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroComm.Core.Abstractions;
using ZeroComm.Core.Engine;

namespace ZeroComm.Tests
{
    public class PlcPollingEngineTests
    {
        private class MockPlcClient : IIndustrialPlcClient
        {
            public string ClientId { get; set; } = "MockS7";
            public PlcVendor Vendor { get; set; } = PlcVendor.SiemensS7;
            public PlcConnectionState State => PlcConnectionState.Connected;
            public PlcDriverCapabilities Capabilities { get; set; } = PlcDriverCapabilities.SiemensS7;
            public bool IsConnected => true;

#pragma warning disable CS0067
            public event Action<IIndustrialPlcClient, PlcConnectionState>? StateChanged;
#pragma warning restore CS0067

            public Dictionary<string, byte[]> SimulatedMemory { get; } = new Dictionary<string, byte[]>();

            public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task DisconnectAsync() => Task.CompletedTask;

            public Task<T> ReadAsync<T>(string tagAddress, CancellationToken cancellationToken = default) where T : unmanaged
            {
                throw new NotImplementedException();
            }

            public Task<bool> WriteAsync<T>(string tagAddress, T value, CancellationToken cancellationToken = default) where T : unmanaged
            {
                throw new NotImplementedException();
            }

            public Task<string> ReadStringAsync(string tagAddress, int length, Encoding? encoding = null, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<bool> WriteStringAsync(string tagAddress, string value, Encoding? encoding = null, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<byte[]> ReadRawBytesAsync(string tagAddress, int length, CancellationToken cancellationToken = default)
            {
                if (SimulatedMemory.TryGetValue(tagAddress, out var data))
                {
                    if (data.Length >= length)
                    {
                        byte[] result = new byte[length];
                        Buffer.BlockCopy(data, 0, result, 0, length);
                        return Task.FromResult(result);
                    }
                    return Task.FromResult(data);
                }

                return Task.FromResult(new byte[length]);
            }

            public Task<bool> WriteRawBytesAsync(string tagAddress, byte[] data, CancellationToken cancellationToken = default)
            {
                SimulatedMemory[tagAddress] = data;
                return Task.FromResult(true);
            }

            public void Dispose() { }
        }

        [Fact]
        public void BatchCoalescingEngine_CoalescesContiguousAndSmallGaps()
        {
            // DB1.DBD0 (4B), DB1.DBD4 (4B), DB1.DBD8 (4B), DB1.DBD16 (4B, gap of 4B from 12 to 16)
            var r1 = new PlcBatchReadRequest(1, PlcAddressParser.Parse("DB1.DBD0", PlcVendor.SiemensS7), 4);
            var r2 = new PlcBatchReadRequest(2, PlcAddressParser.Parse("DB1.DBD4", PlcVendor.SiemensS7), 4);
            var r3 = new PlcBatchReadRequest(3, PlcAddressParser.Parse("DB1.DBD8", PlcVendor.SiemensS7), 4);
            var r4 = new PlcBatchReadRequest(4, PlcAddressParser.Parse("DB1.DBD16", PlcVendor.SiemensS7), 4);

            var blocks = BatchCoalescingEngine.CoalesceRequests(new[] { r1, r2, r3, r4 }, maxContiguousBytes: 200, maxAllowedGapBytes: 10);

            Assert.Single(blocks);
            var block = blocks[0];
            Assert.Equal(0, block.StartOffset);
            Assert.Equal(20, block.ByteLength); // 0 to 20
            Assert.Equal(4, block.Mappings.Count);
            Assert.Equal(0, block.Mappings[0].RelativeOffsetInBlock);
            Assert.Equal(4, block.Mappings[1].RelativeOffsetInBlock);
            Assert.Equal(8, block.Mappings[2].RelativeOffsetInBlock);
            Assert.Equal(16, block.Mappings[3].RelativeOffsetInBlock);
        }

        [Fact]
        public void BatchCoalescingEngine_SeparatesLargeGapsIntoDistinctBlocks()
        {
            // DB1.DBD0 (4B) and DB1.DBD100 (4B) -> gap = 96B > 10B
            var r1 = new PlcBatchReadRequest(1, PlcAddressParser.Parse("DB1.DBD0", PlcVendor.SiemensS7), 4);
            var r2 = new PlcBatchReadRequest(2, PlcAddressParser.Parse("DB1.DBD100", PlcVendor.SiemensS7), 4);

            var blocks = BatchCoalescingEngine.CoalesceRequests(new[] { r1, r2 }, maxContiguousBytes: 200, maxAllowedGapBytes: 10);

            Assert.Equal(2, blocks.Count);
            Assert.Equal(0, blocks[0].StartOffset);
            Assert.Equal(4, blocks[0].ByteLength);

            Assert.Equal(100, blocks[1].StartOffset);
            Assert.Equal(4, blocks[1].ByteLength);
        }

        [Fact]
        public void CoalescedMemoryBlock_Unpack_DecodesValuesWithoutAllocation()
        {
            // Mock block containing:
            // Tag 1 at offset 0: Float32 (Siemens big endian) = 42.5f (0x42, 0x2A, 0x00, 0x00)
            // Tag 2 at offset 4: Int16 (Siemens big endian) = 1337 (0x05, 0x39)
            // Tag 3 at offset 6: Bit 2 = true (0x04)
            var rawData = new byte[] { 0x42, 0x2A, 0x00, 0x00, 0x05, 0x39, 0x04 };

            var mappings = new[]
            {
                new TagSliceMapping(1, 0, 4, TagDataType.Float, 0, false),
                new TagSliceMapping(2, 4, 2, TagDataType.Int16, 0, false),
                new TagSliceMapping(3, 6, 1, TagDataType.Bool, 2, true)
            };

            var block = new CoalescedMemoryBlock(PlcVendor.SiemensS7, 0x84, 1, 0, 7, mappings);
            var destination = new PlcTagValue[4];

            int unpacked = block.Unpack(rawData, destination, 1000L);
            Assert.Equal(3, unpacked);

            // Tag 1 (Float32 = 42.5)
            Assert.Equal(1, destination[0].TagId);
            Assert.Equal(42.5, destination[0].NumericValue, precision: 2);
            Assert.Equal(ScadaQuality.Good, destination[0].Quality);

            // Tag 2 (Int16 = 1337)
            Assert.Equal(2, destination[1].TagId);
            Assert.Equal(1337.0, destination[1].NumericValue);

            // Tag 3 (Bit 2 = true)
            Assert.Equal(3, destination[2].TagId);
            Assert.True(destination[2].BooleanValue);
            Assert.Equal(1.0, destination[2].NumericValue);
        }

        [Fact]
        public void DeadbandFilterSlot_FiltersSubThresholdNoise()
        {
            var slot = new DeadbandFilterSlot(tagId: 10, deadbandAbsolute: 1.0, heartbeatIntervalMs: 0);

            // 1. Initial sample -> Must always report
            var s1 = new PlcTagValue(10, 100.0, false, ScadaQuality.Good, 1000L);
            Assert.True(slot.Evaluate(in s1, out var r1));
            Assert.Equal(100.0, r1.NumericValue);

            // 2. Minor noise: 100.4 (delta = 0.4 < 1.0) -> Must be suppressed
            var s2 = new PlcTagValue(10, 100.4, false, ScadaQuality.Good, 1100L);
            Assert.False(slot.Evaluate(in s2, out _));

            // 3. Significant change: 101.5 (delta = 1.5 >= 1.0 from baseline 100.0) -> Must report
            var s3 = new PlcTagValue(10, 101.5, false, ScadaQuality.Good, 1200L);
            Assert.True(slot.Evaluate(in s3, out var r3));
            Assert.Equal(101.5, r3.NumericValue);

            // 4. Minor noise from new baseline: 101.8 (delta = 0.3 < 1.0) -> Must be suppressed
            var s4 = new PlcTagValue(10, 101.8, false, ScadaQuality.Good, 1300L);
            Assert.False(slot.Evaluate(in s4, out _));
        }

        [Fact]
        public void DeadbandFilterSlot_EnforcesHeartbeatLiveness()
        {
            var slot = new DeadbandFilterSlot(tagId: 20, deadbandAbsolute: 2.0, heartbeatIntervalMs: 1000);

            // 1. Initial sample at t=1000ms
            var s1 = new PlcTagValue(20, 50.0, false, ScadaQuality.Good, 1000L);
            Assert.True(slot.Evaluate(in s1, out _));

            // 2. Unchanged value at t=1500ms (elapsed = 500ms < 1000ms) -> Suppressed
            var s2 = new PlcTagValue(20, 50.0, false, ScadaQuality.Good, 1500L);
            Assert.False(slot.Evaluate(in s2, out _));

            // 3. Unchanged value at t=2100ms (elapsed = 1100ms >= 1000ms) -> Must report heartbeat
            var s3 = new PlcTagValue(20, 50.0, false, ScadaQuality.Good, 2100L);
            Assert.True(slot.Evaluate(in s3, out var r3));
            Assert.Equal(50.0, r3.NumericValue);
            Assert.Equal(2100L, r3.TimestampUtcMs);
        }

        [Fact]
        public async Task PlcPollingEngine_EndToEndBatchPolling_DispatchesChangesAndTelemetry()
        {
            var mock = new MockPlcClient();
            using var engine = new PlcPollingEngine(mock, maxContiguousBytes: 500, maxAllowedGapBytes: 10);

            // Setup simulated raw memory for DB1.DBB0 (length 8 bytes)
            // Offset 0..3: Float32 100.5f (0x42, 0xC9, 0x00, 0x00)
            // Offset 4..5: Int16 200 (0x00, 0xC8)
            // Offset 6..7: Bit 0 = 1 (0x01, 0x00)
            mock.SimulatedMemory["DB1.DBB0"] = new byte[] { 0x42, 0xC9, 0x00, 0x00, 0x00, 0xC8, 0x01, 0x00 };

            // Register tags
            engine.RegisterTag(1, "DB1.DBD0", TagDataType.Float, deadbandAbsolute: 0.5);
            engine.RegisterTag(2, "DB1.DBW4", TagDataType.Int16, deadbandAbsolute: 1.0);
            engine.RegisterTag(3, "DB1.DBX6.0", TagDataType.Bool);

            Assert.Single(engine.CoalescedBlocks);

            var receivedEvents = new List<IReadOnlyList<PlcTagValue>>();
            var telemetryPoints = new List<(int TagId, double Value, long Timestamp)>();

            engine.OnTagValuesChanged += vals => receivedEvents.Add(vals);
            engine.OnTelemetrySampled = (id, val, ts) => telemetryPoints.Add((id, val, ts));

            // Execute 1 poll cycle
            await engine.PollOnceAsync();

            Assert.Single(receivedEvents);
            var samples = receivedEvents[0];
            Assert.Equal(3, samples.Count);

            Assert.Equal(1, samples[0].TagId);
            Assert.Equal(100.5, samples[0].NumericValue, precision: 2);

            Assert.Equal(2, samples[1].TagId);
            Assert.Equal(200.0, samples[1].NumericValue);

            Assert.Equal(3, samples[2].TagId);
            Assert.True(samples[2].BooleanValue);

            Assert.Equal(3, telemetryPoints.Count);
            Assert.Equal(1, telemetryPoints[0].TagId);
            Assert.Equal(100.5, telemetryPoints[0].Value, precision: 2);

            // Modify only Tag 2 in memory: 200 -> 250
            mock.SimulatedMemory["DB1.DBB0"][5] = 0xFA; // 250

            receivedEvents.Clear();
            telemetryPoints.Clear();

            await engine.PollOnceAsync();

            // Only Tag 2 should be reported because Tag 1 and Tag 3 did not exceed deadband!
            Assert.Single(receivedEvents);
            Assert.Single(receivedEvents[0]);
            Assert.Equal(2, receivedEvents[0][0].TagId);
            Assert.Equal(250.0, receivedEvents[0][0].NumericValue);

            Assert.Single(telemetryPoints);
            Assert.Equal(2, telemetryPoints[0].TagId);
            Assert.Equal(250.0, telemetryPoints[0].Value);
        }
    }
}
