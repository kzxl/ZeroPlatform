using System;
using System.Net;
using Xunit;
using ZeroComm.Core.P2P;

namespace ZeroComm.Tests
{
    public class StunClientTests
    {
        [Fact]
        public void BuildBindingRequest_CreatesValidRfc5389Header()
        {
            var req = StunClient.BuildBindingRequest();
            var request = req.Request;
            var transactionId = req.TransactionId;

            Assert.Equal(20, request.Length);
            Assert.Equal(12, transactionId.Length);

            // Message Type = 0x0001
            Assert.Equal(0x00, request[0]);
            Assert.Equal(0x01, request[1]);

            // Length = 0
            Assert.Equal(0x00, request[2]);
            Assert.Equal(0x00, request[3]);

            // Magic Cookie = 0x2112A442
            Assert.Equal(0x21, request[4]);
            Assert.Equal(0x12, request[5]);
            Assert.Equal(0xA4, request[6]);
            Assert.Equal(0x42, request[7]);

            // Transaction ID matches
            for (int i = 0; i < 12; i++)
            {
                Assert.Equal(transactionId[i], request[8 + i]);
            }
        }

        [Fact]
        public void ParseBindingResponse_ValidXorMappedAddress_DecodesCorrectly()
        {
            var transactionId = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

            // Target IP: 203.0.113.195, Port: 54320
            // Magic Cookie: 0x21, 0x12, 0xA4, 0x42
            // Port: 54320 = 0xD430
            // XOR Port with 0x2112 => 0xD430 ^ 0x2112 = 0xF522 (bytes: 0xF5, 0x22)
            // XOR IP:
            // 203 ^ 0x21 = 0xCB ^ 0x21 = 203 ^ 33 = 234 (0xEA)
            // 0 ^ 0x12 = 18 (0x12)
            // 113 ^ 0xA4 = 0x71 ^ 0xA4 = 213 (0xD5)
            // 195 ^ 0x42 = 0xC3 ^ 0x42 = 129 (0x81)

            byte xorPortHigh = (byte)((54320 >> 8) ^ 0x21);
            byte xorPortLow = (byte)((54320 & 0xFF) ^ 0x12);

            byte ip0 = (byte)(203 ^ 0x21);
            byte ip1 = (byte)(0 ^ 0x12);
            byte ip2 = (byte)(113 ^ 0xA4);
            byte ip3 = (byte)(195 ^ 0x42);

            // Build full response buffer: 20 bytes header + 12 bytes attribute (type 2, len 2, 8 value)
            var response = new byte[32];
            // Type = 0x0101 (Binding Response)
            response[0] = 0x01; response[1] = 0x01;
            // Length = 12 (0x000C)
            response[2] = 0x00; response[3] = 0x0C;
            // Cookie = 0x2112A442
            response[4] = 0x21; response[5] = 0x12; response[6] = 0xA4; response[7] = 0x42;
            // Transaction ID
            Array.Copy(transactionId, 0, response, 8, 12);

            // Attribute: XOR-MAPPED-ADDRESS (0x0020), length = 8
            response[20] = 0x00; response[21] = 0x20;
            response[22] = 0x00; response[23] = 0x08;
            response[24] = 0x00; // Reserved
            response[25] = 0x01; // Family: IPv4
            response[26] = xorPortHigh;
            response[27] = xorPortLow;
            response[28] = ip0;
            response[29] = ip1;
            response[30] = ip2;
            response[31] = ip3;

            var ep = StunClient.ParseBindingResponse(response, transactionId);

            Assert.NotNull(ep);
            Assert.Equal("203.0.113.195", ep.Address.ToString());
            Assert.Equal(54320, ep.Port);
        }

        [Fact]
        public void ParseBindingResponse_MismatchedTransactionId_ReturnsNull()
        {
            var sentId = new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            var returnedId = new byte[] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 };

            var response = new byte[20];
            response[0] = 0x01; response[1] = 0x01;
            response[4] = 0x21; response[5] = 0x12; response[6] = 0xA4; response[7] = 0x42;
            Array.Copy(returnedId, 0, response, 8, 12);

            var ep = StunClient.ParseBindingResponse(response, sentId);
            Assert.Null(ep);
        }

        [Fact]
        public void GetLocalIPAddress_ReturnsValidAddress()
        {
            var ip = StunClient.GetLocalIPAddress();
            Assert.NotNull(ip);
            Assert.NotEqual(IPAddress.None, ip);
        }
    }
}
