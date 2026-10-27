using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroComm.Core.Abstractions;
using ZeroComm.Core.Buffers;
using ZeroComm.Core.Siemens;
using ZeroComm.Core.Transport;

namespace ZeroComm.Tests
{
    public class SiemensS7Tests
    {
        private class MockS7Transport : ITransport
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
        public void S7Frame_BuildConnectionRequest_ConstructsValidTpktAndCotp()
        {
            byte[] packet = S7Frame.BuildConnectionRequest(rack: 0, slot: 2); // S7-300 default slot 2

            Assert.Equal(22, packet.Length);
            Assert.Equal(0x03, packet[0]); // TPKT Version
            Assert.Equal(0x00, packet[1]);
            Assert.Equal(22, (packet[2] << 8) | packet[3]); // Total Length

            Assert.Equal(0x11, packet[4]); // COTP Length
            Assert.Equal(0xE0, packet[5]); // PDU Type: Connection Request
            Assert.Equal(0x01, packet[13]); // Src TSAP
            Assert.Equal(0x02, packet[18]); // Dst TSAP (slot 2)
        }

        [Fact]
        public void S7Frame_ValidateConnectionConfirm_AcceptsValidCcAndRejectsInvalid()
        {
            byte[] validCc = new byte[]
            {
                0x03, 0x00, 0x00, 0x16, // TPKT (22 bytes)
                0x11, 0xD0, 0x00, 0x01, 0x00, 0x02, 0x00, // COTP CC
                0xC0, 0x01, 0x0A, 0xC1, 0x02, 0x01, 0x00, 0xC2, 0x02, 0x01, 0x02
            };

            Assert.True(S7Frame.ValidateConnectionConfirm(validCc));

            byte[] invalidCc = new byte[] { 0x03, 0x00, 0x00, 0x07, 0x02, 0xE0, 0x00 };
            Assert.False(S7Frame.ValidateConnectionConfirm(invalidCc));
        }

        [Fact]
        public void S7Frame_BuildSetupCommunication_ConstructsValidSetupPdu()
        {
            byte[] packet = S7Frame.BuildSetupCommunication(sequenceNumber: 101, maxPduLength: 960);

            Assert.Equal(25, packet.Length);
            Assert.Equal(0x03, packet[0]); // TPKT
            Assert.Equal(0xF0, packet[5]); // COTP DT
            Assert.Equal(0x32, packet[7]); // S7 Protocol ID
            Assert.Equal(0x01, packet[8]); // ROSCTR Job
            Assert.Equal(101, (packet[11] << 8) | packet[12]); // Sequence number
            Assert.Equal(0xF0, packet[17]); // Function: Setup Comm
            Assert.Equal(960, (packet[23] << 8) | packet[24]); // Negotiated PDU length requested
        }

        [Fact]
        public void S7Frame_ParseSetupCommunicationResponse_ExtractsNegotiatedLength()
        {
            // Simulate S7 AckData for Setup Comm
            byte[] response = new byte[]
            {
                0x03, 0x00, 0x00, 0x1B, // TPKT (27 bytes)
                0x02, 0xF0, 0x80,       // COTP DT
                0x32, 0x03,             // S7 AckData
                0x00, 0x00,             // Redundancy
                0x00, 0x65,             // Seq 101
                0x00, 0x08,             // Param Len = 8
                0x00, 0x00,             // Data Len = 0
                0x00, 0x00,             // Error Class 0, Error Code 0
                0xF0, 0x00,             // Function Setup Comm
                0x00, 0x01, 0x00, 0x01, // AMQ
                0x01, 0xE0              // Negotiated PDU = 480 (0x01E0)
            };

            ushort pdu = S7Frame.ParseSetupCommunicationResponse(response);
            Assert.Equal(480, pdu);
        }

        [Fact]
        public void S7Frame_BuildReadRequest_SingleDbVariable_ValidCoordinates()
        {
            var varAddress = S7VariableAddress.ForDbBytes(dbNumber: 1, startByte: 10, count: 4);
            byte[] packet = S7Frame.BuildReadRequest(sequenceNumber: 202, new[] { varAddress });

            Assert.Equal(31, packet.Length);
            Assert.Equal(0x04, packet[17]); // Function 0x04 (Read Var)
            Assert.Equal(0x01, packet[18]); // 1 item
            Assert.Equal(0x12, packet[19]); // Variable spec
            Assert.Equal(0x0A, packet[20]); // Length of address spec (10 bytes)
            Assert.Equal(0x10, packet[21]); // Syntax ID S7Any
            Assert.Equal((byte)S7WordLength.Byte, packet[22]);
            Assert.Equal(4, (packet[23] << 8) | packet[24]); // Count = 4
            Assert.Equal(1, (packet[25] << 8) | packet[26]); // DB 1
            Assert.Equal((byte)S7Area.DB, packet[27]);       // Area DB (0x84)
            Assert.Equal(80, (packet[28] << 16) | (packet[29] << 8) | packet[30]); // StartByte 10 * 8 = 80 bits
        }

        [Fact]
        public void S7Frame_ParseReadResponse_ExtractsPayloadCorrectly()
        {
            // Simulate S7 Read Response: 4 bytes of payload (0xDE, 0xAD, 0xBE, 0xEF)
            byte[] response = new byte[]
            {
                0x03, 0x00, 0x00, 0x1D, // TPKT (29 bytes)
                0x02, 0xF0, 0x80,       // COTP DT
                0x32, 0x03,             // S7 AckData
                0x00, 0x00,             // Redundancy
                0x00, 0x01,             // Seq 1
                0x00, 0x02,             // Param Len = 2
                0x00, 0x08,             // Data Len = 8 (4 data header + 4 payload)
                0x00, 0x00,             // Error Class 0, Error Code 0
                0x04, 0x01,             // Function Read (0x04), Item Count 1
                // Data item:
                0xFF,                   // Return code: 0xFF (Success)
                0x04,                   // Transport size: Byte
                0x00, 0x20,             // Length in bits: 32 bits = 4 bytes
                0xDE, 0xAD, 0xBE, 0xEF  // Payload
            };

            var items = S7Frame.ParseReadResponse(response, expectedItems: 1);
            Assert.Single(items);
            Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, items[0]);
        }

        [Fact]
        public void S7Frame_BuildWriteRequest_AndParseWriteResponse_Succeeds()
        {
            var varAddress = S7VariableAddress.ForDbBytes(dbNumber: 2, startByte: 0, count: 2);
            byte[] payload = new byte[] { 0x12, 0x34 };

            byte[] request = S7Frame.BuildWriteRequest(sequenceNumber: 303, varAddress, payload);
            Assert.True(request.Length > 30);
            Assert.Equal(0x05, request[17]); // Function 0x05 (Write Var)

            // Simulate Write response: return code 0xFF
            byte[] response = new byte[]
            {
                0x03, 0x00, 0x00, 0x16, // TPKT (22 bytes)
                0x02, 0xF0, 0x80,       // COTP DT
                0x32, 0x03,             // S7 AckData
                0x00, 0x00,             // Redundancy
                0x01, 0x2F,             // Seq 303
                0x00, 0x02,             // Param Len = 2
                0x00, 0x01,             // Data Len = 1
                0x00, 0x00,             // Error Class 0, Error Code 0
                0x05, 0x01,             // Function Write (0x05), 1 item
                0xFF                    // Return code 0xFF (Success)
            };

            S7Frame.ParseWriteResponse(response); // Must not throw
        }

        [Fact]
        public async Task S7TcpClient_FullHandshakeAndReadWrite_MockTransport_Success()
        {
            var mock = new MockS7Transport();

            // Set up simulated S7 PLC responder
            mock.AutoResponder = request =>
            {
                // 1. Connection Request (Length 22, byte 5 == 0xE0)
                if (request.Length == 22 && request[5] == 0xE0)
                {
                    return new byte[]
                    {
                        0x03, 0x00, 0x00, 0x16,
                        0x11, 0xD0, 0x00, 0x01, 0x00, 0x02, 0x00,
                        0xC0, 0x01, 0x0A, 0xC1, 0x02, 0x01, 0x00, 0xC2, 0x02, 0x01, 0x02
                    };
                }

                // 2. Setup Comm (byte 17 == 0xF0)
                if (request.Length >= 25 && request[17] == 0xF0)
                {
                    ushort seq = (ushort)((request[11] << 8) | request[12]);
                    return new byte[]
                    {
                        0x03, 0x00, 0x00, 0x1B,
                        0x02, 0xF0, 0x80,
                        0x32, 0x03, 0x00, 0x00,
                        (byte)(seq >> 8), (byte)(seq & 0xFF),
                        0x00, 0x08, 0x00, 0x00,
                        0x00, 0x00,
                        0xF0, 0x00, 0x00, 0x01, 0x00, 0x01,
                        0x01, 0xE0 // 480 PDU
                    };
                }

                // 3. Read Var (byte 17 == 0x04)
                if (request.Length >= 21 && request[17] == 0x04)
                {
                    ushort seq = (ushort)((request[11] << 8) | request[12]);
                    // Return 4 bytes: 0x00, 0x01, 0x86, 0xA0 (100,000 in Big-Endian Int32)
                    return new byte[]
                    {
                        0x03, 0x00, 0x00, 0x1D,
                        0x02, 0xF0, 0x80,
                        0x32, 0x03, 0x00, 0x00,
                        (byte)(seq >> 8), (byte)(seq & 0xFF),
                        0x00, 0x02, 0x00, 0x08,
                        0x00, 0x00,
                        0x04, 0x01,
                        0xFF, 0x04, 0x00, 0x20,
                        0x00, 0x01, 0x86, 0xA0
                    };
                }

                // 4. Write Var (byte 17 == 0x05)
                if (request.Length >= 21 && request[17] == 0x05)
                {
                    ushort seq = (ushort)((request[11] << 8) | request[12]);
                    return new byte[]
                    {
                        0x03, 0x00, 0x00, 0x16,
                        0x02, 0xF0, 0x80,
                        0x32, 0x03, 0x00, 0x00,
                        (byte)(seq >> 8), (byte)(seq & 0xFF),
                        0x00, 0x02, 0x00, 0x01,
                        0x00, 0x00,
                        0x05, 0x01,
                        0xFF
                    };
                }

                return null;
            };

            using var client = new S7TcpClient(mock);

            // Handshake
            await client.ConnectHandshakeAsync(rack: 0, slot: 2);
            Assert.Equal(480, client.NegotiatedPduLength);

            // Read Int32
            int readInt = await client.ReadInt32Async(S7Area.DB, dbNumber: 1, startByte: 0);
            Assert.Equal(100000, readInt);

            // Write Int32
            await client.WriteInt32Async(S7Area.DB, dbNumber: 1, startByte: 0, 999999);
            Assert.NotNull(mock.LastSentBuffer);
            Assert.Equal(0x05, mock.LastSentBuffer[17]); // Write function
        }

        [Fact]
        public async Task S7TcpClient_TypedHelpers_FloatAndString_CorrectEndianness()
        {
            var mock = new MockS7Transport();

            mock.AutoResponder = request =>
            {
                if (request.Length >= 21 && request[17] == 0x04)
                {
                    ushort seq = (ushort)((request[11] << 8) | request[12]);

                    // Float value 123.456f in IEEE 754 Big-Endian = 0x42, 0xF6, 0xE9, 0x79
                    return new byte[]
                    {
                        0x03, 0x00, 0x00, 0x1D,
                        0x02, 0xF0, 0x80,
                        0x32, 0x03, 0x00, 0x00,
                        (byte)(seq >> 8), (byte)(seq & 0xFF),
                        0x00, 0x02, 0x00, 0x08,
                        0x00, 0x00,
                        0x04, 0x01,
                        0xFF, 0x04, 0x00, 0x20,
                        0x42, 0xF6, 0xE9, 0x79
                    };
                }

                return null;
            };

            using var client = new S7TcpClient(mock);
            float val = await client.ReadFloatAsync(S7Area.DB, 10, 0);
            Assert.Equal(123.456f, val, precision: 3);
        }

        [Fact]
        public void StreamingFrameParser_TryExtractS7Frame_HandlesStreamFragmentationAndCoalescing()
        {
            var ring = new CircularRingBuffer(1024);

            // Packet 1: 10 bytes (TPKT length = 10)
            byte[] packet1 = new byte[] { 0x03, 0x00, 0x00, 0x0A, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };
            // Packet 2: 8 bytes (TPKT length = 8)
            byte[] packet2 = new byte[] { 0x03, 0x00, 0x00, 0x08, 0xAA, 0xBB, 0xCC, 0xDD };

            // Scenario 1: Feed partial packet 1 (only 6 bytes)
            ring.Write(packet1, 0, 6);
            bool extracted = StreamingFrameParser.TryExtractS7Frame(ring, out byte[] frame1);
            Assert.False(extracted);
            Assert.Empty(frame1);

            // Feed remaining 4 bytes of packet 1 + entire packet 2 (Coalesced packet!)
            ring.Write(packet1, 6, 4);
            ring.Write(packet2, 0, packet2.Length);

            // Should extract packet 1
            extracted = StreamingFrameParser.TryExtractS7Frame(ring, out frame1);
            Assert.True(extracted);
            Assert.Equal(packet1, frame1);

            // Should extract packet 2 immediately from remaining ring buffer
            extracted = StreamingFrameParser.TryExtractS7Frame(ring, out byte[] frame2);
            Assert.True(extracted);
            Assert.Equal(packet2, frame2);

            Assert.Equal(0, ring.Count);
        }

        [Fact]
        public async Task S7TcpClient_AsIndustrialPlcClient_ReadAsync_Success()
        {
            var mock = new MockS7Transport();

            mock.AutoResponder = request =>
            {
                // 1. Connection Request
                if (request.Length == 22 && request[5] == 0xE0)
                {
                    return new byte[]
                    {
                        0x03, 0x00, 0x00, 0x16,
                        0x11, 0xD0, 0x00, 0x01, 0x00, 0x02, 0x00,
                        0xC0, 0x01, 0x0A, 0xC1, 0x02, 0x01, 0x00, 0xC2, 0x02, 0x01, 0x02
                    };
                }

                // 2. Setup Comm
                if (request.Length >= 25 && request[17] == 0xF0)
                {
                    ushort seq = (ushort)((request[11] << 8) | request[12]);
                    return new byte[]
                    {
                        0x03, 0x00, 0x00, 0x1B,
                        0x02, 0xF0, 0x80,
                        0x32, 0x03, 0x00, 0x00,
                        (byte)(seq >> 8), (byte)(seq & 0xFF),
                        0x00, 0x08, 0x00, 0x00,
                        0x00, 0x00,
                        0xF0, 0x00, 0x00, 0x01, 0x00, 0x01,
                        0x01, 0xE0
                    };
                }

                // 3. Read Var (byte 17 == 0x04)
                if (request.Length >= 21 && request[17] == 0x04)
                {
                    ushort seq = (ushort)((request[11] << 8) | request[12]);
                    return new byte[]
                    {
                        0x03, 0x00, 0x00, 0x1D,
                        0x02, 0xF0, 0x80,
                        0x32, 0x03, 0x00, 0x00,
                        (byte)(seq >> 8), (byte)(seq & 0xFF),
                        0x00, 0x02, 0x00, 0x08,
                        0x00, 0x00,
                        0x04, 0x01,
                        0xFF, 0x04, 0x00, 0x20,
                        0x00, 0x01, 0x86, 0xA0 // 100,000 in Big-Endian Int32
                    };
                }

                // 4. Write Var (byte 17 == 0x05)
                if (request.Length >= 21 && request[17] == 0x05)
                {
                    ushort seq = (ushort)((request[11] << 8) | request[12]);
                    return new byte[]
                    {
                        0x03, 0x00, 0x00, 0x16,
                        0x02, 0xF0, 0x80,
                        0x32, 0x03, 0x00, 0x00,
                        (byte)(seq >> 8), (byte)(seq & 0xFF),
                        0x00, 0x02, 0x00, 0x01,
                        0x00, 0x00,
                        0x05, 0x01,
                        0xFF
                    };
                }

                return null;
            };

            using var s7Client = new S7TcpClient(mock);
            IIndustrialPlcClient client = s7Client;
            await client.ConnectAsync();

            Assert.Equal(PlcVendor.SiemensS7, client.Vendor);
            Assert.Equal(PlcConnectionState.Connected, client.State);

            // Read tag using unified string syntax: DB1.DBD0
            int intVal = await client.ReadAsync<int>("DB1.DBD0");
            Assert.Equal(100000, intVal);

            // Write tag using unified string syntax
            bool writeSuccess = await client.WriteAsync<int>("DB1.DBD0", 12345);
            Assert.True(writeSuccess);

            await client.DisconnectAsync();
            Assert.Equal(PlcConnectionState.Disconnected, client.State);
        }
    }
}
