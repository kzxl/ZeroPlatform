using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroComm.Core.Abstractions;
using ZeroComm.Core.Buffers;
using ZeroComm.Core.Omron;
using ZeroComm.Core.Transport;

namespace ZeroComm.Tests
{
    public class FinsTcpClientTests
    {
        private class MockFinsTransport : ITransport
        {
            public bool IsConnected { get; set; } = true;
            public byte[]? LastSentBuffer { get; private set; }
            public event Action<byte[], int, int>? DataReceived;
            public event Action? OnConnected;
#pragma warning disable CS0067
            public event Action<Exception>? OnError;
#pragma warning restore CS0067
            public event Action? OnDisconnected;

            public Func<byte[], byte[]?>? AutoResponder { get; set; }

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

                if (AutoResponder != null)
                {
                    var response = AutoResponder(LastSentBuffer);
                    if (response != null)
                    {
                        DataReceived?.Invoke(response, 0, response.Length);
                    }
                }

                return Task.CompletedTask;
            }

            public Task SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                byte[] arr = buffer.ToArray();
                return SendAsync(arr, 0, arr.Length, cancellationToken);
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
        public async Task FinsTcpClient_FullHandshakeAndReadWrite_Success()
        {
            var mock = new MockFinsTransport();

            mock.AutoResponder = request =>
            {
                // 1. Handshake request (Length 20, Command 0x00000000)
                if (request.Length == 20 && request[8] == 0 && request[11] == 0)
                {
                    // Response: 24 bytes, Command 0x00000001, ClientNode = 10, ServerNode = 1
                    return new byte[]
                    {
                        0x46, 0x49, 0x4E, 0x53, // 'FINS'
                        0x00, 0x00, 0x00, 0x10, // Length = 16
                        0x00, 0x00, 0x00, 0x01, // Command 1
                        0x00, 0x00, 0x00, 0x00, // Error 0
                        0x00, 0x00, 0x00, 0x0A, // Client Node = 10
                        0x00, 0x00, 0x00, 0x01  // Server Node = 1
                    };
                }

                // 2. FINS Data Send (Command 0x00000002)
                if (request.Length >= 16 && request[8] == 0 && request[11] == 2)
                {
                    // Check FINS Command Code at offset 16 + 10 = 26
                    byte mrc = request[26];
                    byte src = request[27];

                    if (mrc == 0x01 && src == 0x01) // Memory Area Read
                    {
                        // Return 2 words: 1234 (0x04D2) and 5678 (0x162E)
                        byte[] innerPayload = new byte[]
                        {
                            0xC0, 0x00, 0x02,       // ICF, RSV, GCT
                            0x00, 0x0A, 0x00,       // DNA, DA1 (10), DA2
                            0x00, 0x01, 0x00,       // SNA, SA1 (1), SA2
                            request[25],            // SID echoed
                            0x01, 0x01,             // MRC, SRC
                            0x00, 0x00,             // EndCode 0x0000 (Success)
                            0x04, 0xD2,             // Word 0 = 1234
                            0x16, 0x2E              // Word 1 = 5678
                        };

                        return FinsFrame.WrapInTcpHeader(innerPayload);
                    }

                    if (mrc == 0x01 && src == 0x02) // Memory Area Write
                    {
                        byte[] innerPayload = new byte[]
                        {
                            0xC0, 0x00, 0x02,
                            0x00, 0x0A, 0x00,
                            0x00, 0x01, 0x00,
                            request[25],
                            0x01, 0x02,
                            0x00, 0x00              // EndCode 0x0000 (Success)
                        };

                        return FinsFrame.WrapInTcpHeader(innerPayload);
                    }
                }

                return null;
            };

            using var client = new FinsTcpClient(mock);
            IIndustrialPlcClient plc = client;

            await plc.ConnectAsync();
            Assert.Equal(PlcConnectionState.Connected, plc.State);
            Assert.Equal(10, client.ClientNode);
            Assert.Equal(1, client.ServerNode);

            // Read tag using unified string syntax: D100
            ushort val = await plc.ReadAsync<ushort>("D100");
            Assert.Equal(1234, val);

            // Write tag using unified string syntax: D200
            bool writeSuccess = await plc.WriteAsync<ushort>("D200", 9999);
            Assert.True(writeSuccess);

            await plc.DisconnectAsync();
            Assert.Equal(PlcConnectionState.Disconnected, plc.State);
        }

        [Fact]
        public void StreamingFrameParser_ExtractsFinsTcpFrame_Success()
        {
            var ring = new CircularRingBuffer(4096);
            byte[] innerPayload = new byte[] { 0x80, 0x00, 0x02, 0x00, 0x01, 0x00, 0x00, 0x0A, 0x00, 0x01, 0x01, 0x01, 0x00, 0x00 };
            byte[] completeFrame = FinsFrame.WrapInTcpHeader(innerPayload);

            // 1. Partial frame write
            ring.Write(completeFrame, 0, 10);
            bool extracted = StreamingFrameParser.TryExtractFinsTcpFrame(ring, out byte[] frame);
            Assert.False(extracted);

            // 2. Complete remaining frame
            ring.Write(completeFrame, 10, completeFrame.Length - 10);
            extracted = StreamingFrameParser.TryExtractFinsTcpFrame(ring, out frame);
            Assert.True(extracted);
            Assert.Equal(completeFrame, frame);
            Assert.Equal(0, ring.Count);
        }
    }
}
