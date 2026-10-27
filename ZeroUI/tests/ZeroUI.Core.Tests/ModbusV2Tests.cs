using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroUI.Core.Common;
using ZeroUI.Core.Communication;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Tests
{
    public class ModbusV2Tests
    {
        [Fact]
        public void ModbusAddressPlanner_CoalescesProximateRegisters_IntoSingleBlock()
        {
            var tags = new List<AdapterTagDefinition>
            {
                new AdapterTagDefinition("Line1.Speed", "40001", TagDataType.Float32),    // 40001..40002 (2 regs)
                new AdapterTagDefinition("Line1.Pressure", "40003", TagDataType.Int16),   // 40003..40003 (1 reg)
                new AdapterTagDefinition("Line1.Temp", "40005", TagDataType.Float32),       // 40005..40006 (2 regs, gap of 1 at 40004)
            };

            var blocks = ModbusAddressPlanner.PlanBlocks(tags, maxBlockRegisters: 120, maxRegisterGap: 5);

            Assert.Single(blocks);
            var block = blocks[0];
            Assert.Equal(40001, block.StartAddress);
            Assert.Equal(6, block.RegisterCount); // 40001 to 40006 = 6 registers
            Assert.Equal(3, block.TagMappings.Count);

            // Verify offsets
            Assert.Equal(0, block.TagMappings[0].RelativeRegisterOffset);
            Assert.Equal(2, block.TagMappings[1].RelativeRegisterOffset);
            Assert.Equal(4, block.TagMappings[2].RelativeRegisterOffset);
        }

        [Fact]
        public void ModbusAddressPlanner_SplitsBlocks_WhenGapExceedsThreshold()
        {
            var tags = new List<AdapterTagDefinition>
            {
                new AdapterTagDefinition("Line1.Motor1", "100", TagDataType.Int16),
                new AdapterTagDefinition("Line1.Motor2", "101", TagDataType.Int16),
                new AdapterTagDefinition("Line2.Motor1", "200", TagDataType.Int16), // Gap of 98 > maxGap=5
            };

            var blocks = ModbusAddressPlanner.PlanBlocks(tags, maxBlockRegisters: 120, maxRegisterGap: 5);

            Assert.Equal(2, blocks.Count);
            Assert.Equal(100, blocks[0].StartAddress);
            Assert.Equal(2, blocks[0].RegisterCount);
            Assert.Equal(2, blocks[0].TagMappings.Count);

            Assert.Equal(200, blocks[1].StartAddress);
            Assert.Equal(1, blocks[1].RegisterCount);
            Assert.Single(blocks[1].TagMappings);
        }

        [Fact]
        public void ModbusAddressPlanner_SplitsBlocks_WhenRegisterLimitExceeded()
        {
            var tags = new List<AdapterTagDefinition>();
            // Add 100 2-register tags = 200 registers total
            for (int i = 0; i < 100; i++)
            {
                tags.Add(new AdapterTagDefinition($"Tag.{i}", (1000 + i * 2).ToString(), TagDataType.Float32));
            }

            // Max 100 registers per block
            var blocks = ModbusAddressPlanner.PlanBlocks(tags, maxBlockRegisters: 100, maxRegisterGap: 0);

            Assert.Equal(2, blocks.Count);
            Assert.Equal(100, blocks[0].RegisterCount);
            Assert.Equal(100, blocks[1].RegisterCount);
        }

        [Fact]
        public async Task ModbusTcpAdapter_BlockPolling_MeasuresSeparatedObservabilityMetrics()
        {
            // Start mock Modbus TCP server answering coalesced block requests
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var serverTask = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync();
                var stream = client.GetStream();
                var reqBuf = new byte[12];

                int read = await stream.ReadAsync(reqBuf, 0, reqBuf.Length);
                if (read >= 12 && reqBuf[7] == 0x03) // FC3
                {
                    ushort transId = BinaryPrimitives.ReadUInt16BigEndian(reqBuf.AsSpan(0, 2));
                    ushort startAddr = BinaryPrimitives.ReadUInt16BigEndian(reqBuf.AsSpan(8, 2));
                    ushort regCount = BinaryPrimitives.ReadUInt16BigEndian(reqBuf.AsSpan(10, 2));

                    Assert.Equal(40010, startAddr);
                    Assert.Equal(3, regCount); // 1 reg (Int16) + 2 regs (Float32) = 3 regs

                    // Build response: 7-byte MBAP + 1-byte FC + 1-byte byteCount + 6 bytes data
                    byte byteCount = (byte)(regCount * 2);
                    var resp = new byte[9 + byteCount];
                    BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(0, 2), transId);
                    BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(2, 2), 0); // Modbus
                    BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(4, 2), (ushort)(byteCount + 3)); // Length
                    resp[6] = 1; // Unit ID
                    resp[7] = 0x03; // FC3
                    resp[8] = byteCount; // 6 bytes

                    // Reg 40010: Int16 = 500
                    BinaryPrimitives.WriteInt16BigEndian(resp.AsSpan(9, 2), 500);

                    // Reg 40011-40012: Float32 = 98.6f
                    float tempVal = 98.6f;
                    uint tempBits = (uint)ZeroMemory.SingleToInt32Bits(tempVal);
                    BinaryPrimitives.WriteUInt32BigEndian(resp.AsSpan(11, 4), tempBits);

                    await stream.WriteAsync(resp, 0, resp.Length);
                }
            });

            using var adapter = new ModbusTcpAdapter("PLC_BLOCK_TEST", "127.0.0.1", port, unitId: 1, maxInFlight: 1);
            adapter.RegisterTag(new AdapterTagDefinition("Tank.Level", "40010", TagDataType.Int16));
            adapter.RegisterTag(new AdapterTagDefinition("Tank.Temperature", "40011", TagDataType.Float32));

            await adapter.ConnectAsync(cts.Token);
            Assert.Equal(AdapterConnectionState.Connected, adapter.State);

            await adapter.PollOnceAsync(cts.Token);
            await serverTask;
            listener.Stop();

            // Verify values updated in TagEngine
            var levelTag = ZeroTagEngine.GetTag("Tank.Level");
            var tempTag = ZeroTagEngine.GetTag("Tank.Temperature");

            Assert.NotNull(levelTag);
            Assert.NotNull(tempTag);
            Assert.Equal(500.0, Convert.ToDouble(levelTag!.Value), precision: 1);
            Assert.Equal(98.6, Convert.ToDouble(tempTag!.Value), precision: 1);

            // Verify Observability Metrics separation
            var metrics = adapter.Metrics;
            Assert.Equal(1, metrics.TotalPollCycles);
            Assert.Equal(1, metrics.PlannedBlockCount);
            Assert.Equal(1, metrics.TotalRequestsSent);
            Assert.Equal(1, metrics.TotalResponsesReceived);
            Assert.True(metrics.TotalBytesSent >= 12);
            Assert.True(metrics.TotalBytesReceived >= 15);
            Assert.True(metrics.NetworkRtt >= TimeSpan.Zero);
            Assert.True(metrics.DecodeTime >= TimeSpan.Zero);
            Assert.True(metrics.TagUpdateTime >= TimeSpan.Zero);
            Assert.True(metrics.TotalCycleTime >= metrics.NetworkRtt);

            // Verify Latency returns NetworkRtt
            Assert.Equal(metrics.NetworkRtt, adapter.Latency);
        }
    }
}
