using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroComm.Core.Abstractions;
using ZeroComm.Core.AllenBradley;
using ZeroComm.Core.Buffers;
using ZeroComm.Core.Transport;

namespace ZeroComm.Tests
{
    public class EtherNetIpClientTests
    {
        private sealed class MockTransport : ITransport
        {
            public bool IsConnected { get; set; } = true;
            public event Action<byte[], int, int>? DataReceived;
            public event Action? OnConnected;
#pragma warning disable CS0067
            public event Action<Exception>? OnError;
#pragma warning restore CS0067
            public event Action? OnDisconnected;

            public byte[]? LastSentData { get; private set; }
            public Func<byte[], byte[]>? ResponseGenerator { get; set; }

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
                byte[] sent = new byte[count];
                Buffer.BlockCopy(buffer, offset, sent, 0, count);
                LastSentData = sent;

                if (ResponseGenerator != null)
                {
                    byte[] response = ResponseGenerator(sent);
                    if (response.Length > 0)
                    {
                        DataReceived?.Invoke(response, 0, response.Length);
                    }
                }

                return Task.CompletedTask;
            }

            public Task SendAsync(ReadOnlyMemory<byte> memory, CancellationToken cancellationToken = default)
            {
                byte[] sent = memory.ToArray();
                LastSentData = sent;

                if (ResponseGenerator != null)
                {
                    byte[] response = ResponseGenerator(sent);
                    if (response.Length > 0)
                    {
                        DataReceived?.Invoke(response, 0, response.Length);
                    }
                }

                return Task.CompletedTask;
            }

            public void SimulateIncoming(byte[] data)
            {
                DataReceived?.Invoke(data, 0, data.Length);
            }

            public void Dispose()
            {
                IsConnected = false;
            }
        }

        [Fact]
        public void EncodeTagPath_PlainSymbol_GeneratesAnsiExtendedSegment()
        {
            // "Tank1" -> 5 chars (odd) -> 0x91, 0x05, 'T', 'a', 'n', 'k', '1', 0x00 (pad) = 8 bytes = 4 words
            byte[] path = CipFrame.EncodeTagPath("Tank1", out byte words);

            Assert.Equal(4, words);
            Assert.Equal(8, path.Length);
            Assert.Equal(0x91, path[0]);
            Assert.Equal(5, path[1]);
            Assert.Equal((byte)'T', path[2]);
            Assert.Equal((byte)'1', path[6]);
            Assert.Equal(0x00, path[7]); // Pad byte
        }

        [Fact]
        public void EncodeTagPath_ArrayTag_GeneratesSymbolAndElementSegments()
        {
            // "Array[3]" -> "Array" (5 chars -> 8B) + Element 3 (0x28, 0x03 -> 2B) = 10 bytes = 5 words
            byte[] path = CipFrame.EncodeTagPath("Array[3]", out byte words);

            Assert.Equal(5, words);
            Assert.Equal(10, path.Length);
            Assert.Equal(0x91, path[0]);
            Assert.Equal(5, path[1]);
            Assert.Equal(0x28, path[8]); // 8-bit element segment
            Assert.Equal(0x03, path[9]);
        }

        [Fact]
        public void EncodeTagPath_NestedMember_GeneratesTwoSymbolSegments()
        {
            // "Data.Speed" -> "Data" (4 chars -> 0x91, 0x04, 'D','a','t','a' = 6B) + "Speed" (5 chars -> 0x91, 0x05, 'S','p','e','e','d', 0x00 = 8B) = 14B = 7 words
            byte[] path = CipFrame.EncodeTagPath("Data.Speed", out byte words);

            Assert.Equal(7, words);
            Assert.Equal(14, path.Length);
            Assert.Equal(0x91, path[0]);
            Assert.Equal(4, path[1]);
            Assert.Equal(0x91, path[6]);
            Assert.Equal(5, path[7]);
        }

        [Fact]
        public void BuildRegisterSession_And_ParseRegisterSessionResponse()
        {
            byte[] request = CipFrame.BuildRegisterSession();
            Assert.Equal(28, request.Length);
            Assert.Equal(0x65, request[0]); // RegisterSession
            Assert.Equal(0x04, request[2]); // Length 4

            // Simulate PLC response assigning SessionHandle = 0x12345678
            byte[] response = new byte[28];
            Buffer.BlockCopy(request, 0, response, 0, 28);
            response[4] = 0x78;
            response[5] = 0x56;
            response[6] = 0x34;
            response[7] = 0x12;

            bool ok = CipFrame.ParseRegisterSessionResponse(response, out uint sessionHandle, out uint status);
            Assert.True(ok);
            Assert.Equal(0x12345678u, sessionHandle);
            Assert.Equal(0u, status);
        }

        [Fact]
        public void BuildReadTagRequest_DirectMessage_BuildsValidSendRRDataFrame()
        {
            byte[] frame = CipFrame.BuildReadTagRequest(0x12345678, "Tank1", 1, slot: 0, useRouting: false);

            Assert.True(frame.Length > 24);
            Assert.Equal(0x6F, frame[0]); // SendRRData
            Assert.Equal(0x00, frame[1]);
            Assert.Equal(0x78, frame[4]); // Session handle
            Assert.Equal(0x12, frame[7]);

            // Item count at offset 30
            Assert.Equal(0x02, frame[30]);
            // Item 2 Type at offset 36: 0x00B2
            Assert.Equal(0xB2, frame[36]);
            Assert.Equal(0x00, frame[37]);

            // CIP Service at offset 40: ReadTag (0x4C)
            Assert.Equal(0x4C, frame[40]);
        }

        [Fact]
        public void BuildWriteTagRequest_DirectMessage_BuildsValidSendRRDataFrame()
        {
            byte[] data = new byte[] { 0x39, 0x05, 0x00, 0x00 }; // 1337 as DINT
            byte[] frame = CipFrame.BuildWriteTagRequest(0x12345678, "Tank1", CipDataType.Int32, data, 1, slot: 0, useRouting: false);

            Assert.True(frame.Length > 24);
            Assert.Equal(0x6F, frame[0]); // SendRRData

            // CIP Service at offset 40: WriteTag (0x4D)
            Assert.Equal(0x4D, frame[40]);
        }

        [Fact]
        public void StreamingFrameParser_ExtractsCompleteEtherNetIpFrame()
        {
            var ring = new CircularRingBuffer(1024);

            // Construct 24B header + 4B payload = 28B
            byte[] packet = new byte[28];
            packet[0] = 0x65; // RegisterSession
            packet[2] = 0x04; // Payload length = 4

            // Write only 20 bytes: should not extract
            ring.Write(packet, 0, 20);
            Assert.False(StreamingFrameParser.TryExtractEtherNetIpFrame(ring, out _));

            // Write remaining 8 bytes
            ring.Write(packet, 20, 8);
            Assert.True(StreamingFrameParser.TryExtractEtherNetIpFrame(ring, out byte[] extracted));
            Assert.Equal(28, extracted.Length);
            Assert.Equal(0x65, extracted[0]);
        }

        [Fact]
        public async Task EtherNetIpClient_EndToEndMock_HandshakeAndReadDint()
        {
            var mock = new MockTransport();
            using var client = new EtherNetIpClient(mock);

            mock.ResponseGenerator = sent =>
            {
                ushort command = (ushort)(sent[0] | (sent[1] << 8));
                if (command == (ushort)CipCommand.RegisterSession)
                {
                    // Return SessionHandle = 0xABCD1234
                    byte[] resp = new byte[28];
                    Buffer.BlockCopy(sent, 0, resp, 0, 28);
                    resp[4] = 0x34;
                    resp[5] = 0x12;
                    resp[6] = 0xCD;
                    resp[7] = 0xAB;
                    return resp;
                }

                if (command == (ushort)CipCommand.SendRRData)
                {
                    // Mock response for ReadTag: Service 0xCC, Status 0x00, Type DINT (0x00C4), Value 1337 (0x00000539)
                    // CIP Data Item: Service(1B) + Reserved(1B) + Status(1B) + AddWords(1B) + Type(2B) + Data(4B) = 10B
                    byte[] cipItem = new byte[] { 0xCC, 0x00, 0x00, 0x00, 0xC4, 0x00, 0x39, 0x05, 0x00, 0x00 };

                    // SendRRData wrapper: Header(24B) + Interface(4B) + Timeout(2B) + Items(2B) + NullAddr(4B) + DataItem(4B + 10B)
                    byte[] resp = new byte[24 + 4 + 2 + 2 + 4 + 4 + cipItem.Length];
                    resp[0] = 0x6F; // SendRRData
                    ushort encapLen = (ushort)(resp.Length - 24);
                    resp[2] = (byte)(encapLen & 0xFF);
                    resp[3] = (byte)((encapLen >> 8) & 0xFF);
                    // Copy session handle
                    Buffer.BlockCopy(sent, 4, resp, 4, 4);

                    // Item Count = 2
                    resp[30] = 0x02;
                    // Item 2: 0x00B2, len 10
                    resp[36] = 0xB2;
                    resp[37] = 0x00;
                    resp[38] = (byte)cipItem.Length;
                    resp[39] = 0x00;
                    Buffer.BlockCopy(cipItem, 0, resp, 40, cipItem.Length);

                    return resp;
                }

                return Array.Empty<byte>();
            };

            await client.ConnectHandshakeAsync();
            Assert.True(client.IsConnected);
            Assert.Equal(0xABCD1234u, client.SessionHandle);

            int value = await client.ReadInt32Async("Tag_ProcessValue");
            Assert.Equal(1337, value);

            // Test through unified IIndustrialPlcClient
            IIndustrialPlcClient plc = client;
            int genericValue = await plc.ReadAsync<int>("Tag_ProcessValue");
            Assert.Equal(1337, genericValue);
        }

        [Fact]
        public async Task EtherNetIpClient_ReadString_ParsesDintLengthPrefix()
        {
            var mock = new MockTransport();
            using var client = new EtherNetIpClient(mock);

            mock.ResponseGenerator = sent =>
            {
                ushort command = (ushort)(sent[0] | (sent[1] << 8));
                if (command == (ushort)CipCommand.RegisterSession)
                {
                    byte[] resp = new byte[28];
                    Buffer.BlockCopy(sent, 0, resp, 0, 28);
                    resp[4] = 0x01;
                    return resp;
                }

                if (command == (ushort)CipCommand.SendRRData)
                {
                    // String "HELLO" -> DINT length = 5, followed by 'H','E','L','L','O'
                    byte[] strBytes = Encoding.ASCII.GetBytes("HELLO");
                    byte[] cipItem = new byte[4 + 2 + 4 + strBytes.Length];
                    cipItem[0] = 0xCC; // ReadTag response
                    cipItem[1] = 0x00;
                    cipItem[2] = 0x00; // Success
                    cipItem[3] = 0x00; // Additional status count
                    cipItem[4] = 0xA0; // Struct type (0x02A0)
                    cipItem[5] = 0x02;
                    // DINT length = 5
                    cipItem[6] = 0x05;
                    cipItem[7] = 0x00;
                    cipItem[8] = 0x00;
                    cipItem[9] = 0x00;
                    Buffer.BlockCopy(strBytes, 0, cipItem, 10, strBytes.Length);

                    byte[] resp = new byte[24 + 16 + cipItem.Length];
                    resp[0] = 0x6F;
                    ushort encapLen = (ushort)(resp.Length - 24);
                    resp[2] = (byte)(encapLen & 0xFF);
                    resp[3] = (byte)((encapLen >> 8) & 0xFF);
                    Buffer.BlockCopy(sent, 4, resp, 4, 4);
                    resp[30] = 0x02;
                    resp[36] = 0xB2;
                    resp[38] = (byte)cipItem.Length;
                    Buffer.BlockCopy(cipItem, 0, resp, 40, cipItem.Length);

                    return resp;
                }

                return Array.Empty<byte>();
            };

            await client.ConnectHandshakeAsync();
            string strVal = await client.ReadStringAsync("Recipe_Name");
            Assert.Equal("HELLO", strVal);
        }
    }
}
