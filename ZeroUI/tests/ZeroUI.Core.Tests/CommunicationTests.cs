using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroUI.Core.Common;
using ZeroUI.Core.Communication;
using ZeroUI.Core.Runtime;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Tests
{
    public class CommunicationTests
    {
        [Fact]
        public async Task ModbusTcpAdapter_ConnectsAndPollsHoldingRegister_PushesToTagEngine()
        {
            // Start mock Modbus TCP server on ephemeral port
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

                if (read >= 12 && reqBuf[7] == 0x03) // FC3 Read Holding Registers
                {
                    ushort transId = BinaryPrimitives.ReadUInt16BigEndian(reqBuf.AsSpan(0, 2));

                    // Build response: MBAP (7 bytes) + FC (1 byte) + ByteCount (1 byte) + Data (4 bytes for Float32 = 42.5f)
                    var resp = new byte[13];
                    BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(0, 2), transId);
                    BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(2, 2), 0); // Protocol ID
                    BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(4, 2), 7); // Length
                    resp[6] = 1; // Unit ID
                    resp[7] = 0x03; // FC3
                    resp[8] = 4; // Byte count

                    // 42.5f in IEEE 754 Big-Endian
                    float testVal = 42.5f;
                    uint rawBits = (uint)ZeroMemory.SingleToInt32Bits(testVal);
                    BinaryPrimitives.WriteUInt32BigEndian(resp.AsSpan(9, 4), rawBits);

                    await stream.WriteAsync(resp, 0, resp.Length);
                }
            });

            using var adapter = new ModbusTcpAdapter("PLC_MODBUS_1", "127.0.0.1", port, unitId: 1);
            adapter.RegisterTag(new AdapterTagDefinition("Plant.Reactor.Temperature", "40001", TagDataType.Float32));

            await adapter.ConnectAsync(cts.Token);
            Assert.Equal(AdapterConnectionState.Connected, adapter.State);

            await adapter.PollOnceAsync(cts.Token);
            await serverTask;
            listener.Stop();

            var tag = ZeroTagEngine.GetTag("Plant.Reactor.Temperature");
            Assert.NotNull(tag);
            Assert.Equal(42.5, Convert.ToDouble(tag!.Value), precision: 2);
            Assert.Equal(ScadaQuality.Good, tag.Quality);
        }

        [Fact]
        public async Task GenericSocketClient_DelimiterFraming_ExtractsPacketsCorrectly()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var serverTask = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync();
                var stream = client.GetStream();

                // Send two framed barcode scans: "BC12345\r\n" and "BC67890\r\n"
                byte[] data = Encoding.ASCII.GetBytes("BC12345\r\nBC67890\r\n");
                await stream.WriteAsync(data, 0, data.Length);
            });

            using var socketClient = new GenericSocketClient(
                "127.0.0.1",
                port,
                SocketFramingMode.Delimiter,
                delimiter: new byte[] { (byte)'\r', (byte)'\n' });

            var receivedStrings = new System.Collections.Generic.List<string>();
            var receivedTcs = new TaskCompletionSource<bool>();

            socketClient.StringReceived += msg =>
            {
                receivedStrings.Add(msg);
                if (receivedStrings.Count == 2)
                {
                    receivedTcs.TrySetResult(true);
                }
            };

            await socketClient.ConnectAsync(cts.Token);
            await Task.WhenAny(receivedTcs.Task, Task.Delay(2000, cts.Token));

            await serverTask;
            listener.Stop();

            Assert.Equal(2, receivedStrings.Count);
            Assert.Equal("BC12345", receivedStrings[0]);
            Assert.Equal("BC67890", receivedStrings[1]);
        }

        [Fact]
        public void ConnectionManager_RegisterAndStateStoreSync_Succeeds()
        {
            var manager = new ConnectionManager();
            using var adapter = new ModbusTcpAdapter("METER_01", "192.168.1.100", 502);

            manager.RegisterAdapter(adapter, autoReconnect: false);

            var retrieved = manager.GetAdapter("METER_01");
            Assert.NotNull(retrieved);
            Assert.Equal("METER_01", retrieved!.AdapterId);
            Assert.Equal("192.168.1.100:502", retrieved.Endpoint);

            // Verify state in StateStore
            var status = StateStore.Default.GetState<string>("Connection.METER_01.Status");
            Assert.True(status == null || status == "Disconnected");
        }
    }
}
