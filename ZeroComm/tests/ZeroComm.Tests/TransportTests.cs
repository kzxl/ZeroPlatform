using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroComm.Core.Mitsubishi;
using ZeroComm.Core.Modbus;
using ZeroComm.Core.Transport;

namespace ZeroComm.Tests
{
    public class TransportTests
    {
        private class MockTransport : ITransport
        {
            public bool IsConnected { get; set; } = true;
            public byte[]? LastSentBuffer { get; private set; }
            public event Action<byte[], int, int>? DataReceived;
            public event Action? OnConnected;
#pragma warning disable CS0067
            public event Action<Exception>? OnError;
#pragma warning restore CS0067
            public event Action? OnDisconnected;

            public Task ConnectAsync(CancellationToken cancellationToken = default)
            {
                IsConnected = true;
                OnConnected?.Invoke();
                return Task.CompletedTask;
            }

            public Task DisconnectAsync()
            {
                IsConnected = false;
                OnDisconnected?.Invoke();
                return Task.CompletedTask;
            }

            public Task SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
            {
                LastSentBuffer = new byte[count];
                Array.Copy(buffer, offset, LastSentBuffer, 0, count);
                return Task.CompletedTask;
            }

            public Task SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                LastSentBuffer = buffer.ToArray();
                return Task.CompletedTask;
            }

            public void SimulateIncomingData(byte[] data)
            {
                DataReceived?.Invoke(data, 0, data.Length);
            }

            public void Dispose()
            {
                IsConnected = false;
            }
        }

        [Fact]
        public async Task ModbusTcpMaster_ReadHoldingRegisters_Success()
        {
            var mock = new MockTransport();
            using var master = new ModbusTcpMaster(mock);

            var readTask = master.ReadHoldingRegistersAsync(1, 100, 2);

            // Verify request was sent
            Assert.NotNull(mock.LastSentBuffer);
            byte[] sent = mock.LastSentBuffer;
            ushort txId = (ushort)((sent[0] << 8) | sent[1]);

            // Construct valid Modbus TCP response (FC 03, 4 bytes payload = 2 registers: 0x1234, 0x5678)
            byte[] response = new byte[]
            {
                (byte)(txId >> 8), (byte)(txId & 0xFF), // Transaction ID
                0x00, 0x00,                             // Protocol ID
                0x00, 0x07,                             // Length = 7 (UnitId + FC + ByteCount + 4 bytes data)
                0x01,                                   // Unit ID
                0x03,                                   // Function Code
                0x04,                                   // Byte count
                0x12, 0x34,                             // Reg 0 = 0x1234
                0x56, 0x78                              // Reg 1 = 0x5678
            };

            mock.SimulateIncomingData(response);

            ushort[] registers = await readTask;
            Assert.Equal(2, registers.Length);
            Assert.Equal(0x1234, registers[0]);
            Assert.Equal(0x5678, registers[1]);
        }

        [Fact]
        public async Task ModbusTcpMaster_ErrorResponse_ThrowsModbusException()
        {
            var mock = new MockTransport();
            using var master = new ModbusTcpMaster(mock);

            var readTask = master.ReadHoldingRegistersAsync(1, 500, 1);

            Assert.NotNull(mock.LastSentBuffer);
            byte[] sent = mock.LastSentBuffer;
            ushort txId = (ushort)((sent[0] << 8) | sent[1]);

            // Exception response: FC = 0x83, Exception Code = 0x02 (Illegal Data Address)
            byte[] response = new byte[]
            {
                (byte)(txId >> 8), (byte)(txId & 0xFF),
                0x00, 0x00,
                0x00, 0x03,
                0x01,
                0x83, // Error FC (0x03 | 0x80)
                0x02  // Illegal Data Address
            };

            mock.SimulateIncomingData(response);

            var exc = await Assert.ThrowsAsync<ModbusException>(() => readTask);
            Assert.Equal(0x03, exc.FunctionCode);
            Assert.Equal(ModbusExceptionCode.IllegalDataAddress, exc.ExceptionCode);
        }

        [Fact]
        public async Task McProtocolTcpClient_ReadWords_Success()
        {
            var mock = new MockTransport();
            using var client = new McProtocolTcpClient(mock);

            var readTask = client.ReadWordsAsync(McDeviceCode.D, 100, 2);

            Assert.NotNull(mock.LastSentBuffer);

            // Construct 3E Binary response:
            // Subheader: 0xD0, 0x00 (2B)
            // Route: 0x00, 0xFF, 0xFF, 0x03, 0x00 (5B)
            // Response Data Length: 6 bytes (EndCode 2B + Data 4B) -> 0x06, 0x00 (2B)
            // EndCode: 0x0000 (2B)
            // Data words: D100 = 0x0ABC (BC 0A), D101 = 0x0DEF (EF 0D)
            byte[] response = new byte[]
            {
                0xD0, 0x00,
                0x00, 0xFF, 0xFF, 0x03, 0x00,
                0x06, 0x00, // Length = 6
                0x00, 0x00, // EndCode = 0 (Success)
                0xBC, 0x0A, // D100 = 0x0ABC
                0xEF, 0x0D  // D101 = 0x0DEF
            };

            mock.SimulateIncomingData(response);

            ushort[] words = await readTask;
            Assert.Equal(2, words.Length);
            Assert.Equal(0x0ABC, words[0]);
            Assert.Equal(0x0DEF, words[1]);
        }

        [Fact]
        public async Task McProtocolTcpClient_ErrorResponse_ThrowsMcProtocolException()
        {
            var mock = new MockTransport();
            using var client = new McProtocolTcpClient(mock);

            var readTask = client.ReadWordsAsync(McDeviceCode.D, 9999, 1);

            Assert.NotNull(mock.LastSentBuffer);

            // Response with error EndCode = 0xC050
            byte[] response = new byte[]
            {
                0xD0, 0x00,
                0x00, 0xFF, 0xFF, 0x03, 0x00,
                0x02, 0x00, // Length = 2 (just EndCode)
                0x50, 0xC0  // EndCode = 0xC050
            };

            mock.SimulateIncomingData(response);

            var exc = await Assert.ThrowsAsync<McProtocolException>(() => readTask);
            Assert.Equal(0xC050, exc.EndCode);
        }

        [Fact]
        public async Task AsyncTcpTransport_LoopbackSocket_SendAndReceive()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var serverAcceptTask = Task.Run(async () =>
            {
                using var serverClient = await listener.AcceptTcpClientAsync();
                using var serverStream = serverClient.GetStream();

                byte[] srvBuf = new byte[100];
                int read = await serverStream.ReadAsync(srvBuf, 0, srvBuf.Length);

                // Echo back with modified prefix
                byte[] echo = new byte[read + 1];
                echo[0] = 0x7E;
                Array.Copy(srvBuf, 0, echo, 1, read);
                await serverStream.WriteAsync(echo, 0, echo.Length);
                await serverStream.FlushAsync();
            });

            using var clientTransport = new AsyncTcpTransport("127.0.0.1", port);
            await clientTransport.ConnectAsync();
            Assert.True(clientTransport.IsConnected);

            var tcs = new TaskCompletionSource<byte[]>();
            clientTransport.DataReceived += (buf, off, cnt) =>
            {
                byte[] received = new byte[cnt];
                Array.Copy(buf, off, received, 0, cnt);
                tcs.TrySetResult(received);
            };

            byte[] clientPayload = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await clientTransport.SendAsync(clientPayload, 0, clientPayload.Length);

            await serverAcceptTask;

            byte[] echoReceived = await tcs.Task;
            Assert.Equal(5, echoReceived.Length);
            Assert.Equal(0x7E, echoReceived[0]);
            Assert.Equal(0x01, echoReceived[1]);
            Assert.Equal(0x04, echoReceived[4]);

            await clientTransport.DisconnectAsync();
            Assert.False(clientTransport.IsConnected);
            listener.Stop();
        }
    }
}
