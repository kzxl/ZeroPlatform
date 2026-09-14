using System;
using Xunit;
using ZeroIoT.OpcUa;

namespace ZeroIoT.Tests
{
    public class OpcUaBinaryTests
    {
        [Fact]
        public void Primitives_RoundTrip_PreservesValues()
        {
            var enc = new UaBinaryEncoder();
            enc.WriteBoolean(true);
            enc.WriteByte(0xAB);
            enc.WriteSByte(-42);
            enc.WriteInt16(-12345);
            enc.WriteUInt16(54321);
            enc.WriteInt32(-987654321);
            enc.WriteUInt32(3141592653);
            enc.WriteInt64(-1234567890123456L);
            enc.WriteUInt64(9876543210987654UL);
            enc.WriteFloat(3.14159f);
            enc.WriteDouble(2.718281828459);
            enc.WriteString("Industrial Automation OPC-UA");
            DateTime now = DateTime.UtcNow;
            enc.WriteDateTime(now);
            Guid testGuid = Guid.NewGuid();
            enc.WriteGuid(testGuid);
            byte[] rawBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            enc.WriteByteString(rawBytes);

            byte[] buffer = enc.ToArray();
            var dec = new UaBinaryDecoder(buffer);

            Assert.True(dec.ReadBoolean());
            Assert.Equal((byte)0xAB, dec.ReadByte());
            Assert.Equal((sbyte)-42, dec.ReadSByte());
            Assert.Equal(-12345, dec.ReadInt16());
            Assert.Equal(54321, dec.ReadUInt16());
            Assert.Equal(-987654321, dec.ReadInt32());
            Assert.Equal(3141592653U, dec.ReadUInt32());
            Assert.Equal(-1234567890123456L, dec.ReadInt64());
            Assert.Equal(9876543210987654UL, dec.ReadUInt64());
            Assert.Equal(3.14159f, dec.ReadFloat(), 4);
            Assert.Equal(2.718281828459, dec.ReadDouble(), 8);
            Assert.Equal("Industrial Automation OPC-UA", dec.ReadString());

            DateTime readDt = dec.ReadDateTime();
            Assert.Equal(now.ToUniversalTime().Ticks / 10000, readDt.Ticks / 10000); // 1ms resolution match

            Assert.Equal(testGuid, dec.ReadGuid());
            Assert.Equal(rawBytes, dec.ReadByteString());
            Assert.Equal(0, dec.Remaining);
        }

        [Fact]
        public void NodeId_StringParsing_And_BinarySerialization()
        {
            var nodeNumeric = NodeId.FromString("ns=2;i=1001");
            Assert.Equal(2, nodeNumeric.NamespaceIndex);
            Assert.Equal(1001U, nodeNumeric.NumericIdentifier);

            var nodeString = NodeId.FromString("ns=3;s=Line1.Spindle_RPM");
            Assert.Equal(3, nodeString.NamespaceIndex);
            Assert.Equal("Line1.Spindle_RPM", nodeString.StringIdentifier);
            Assert.Equal("ns=3;s=Line1.Spindle_RPM", nodeString.ToString());

            var enc = new UaBinaryEncoder();
            enc.WriteNodeId(nodeNumeric);
            enc.WriteNodeId(nodeString);

            byte[] bytes = enc.ToArray();
            var dec = new UaBinaryDecoder(bytes);

            var readNum = dec.ReadNodeId();
            Assert.Equal(nodeNumeric, readNum);

            var readStr = dec.ReadNodeId();
            Assert.Equal(nodeString, readStr);
            Assert.Equal(0, dec.Remaining);
        }

        [Fact]
        public void Variant_ScalarAndArray_RoundTrip()
        {
            var vDouble = UaVariant.FromDouble(123.456);
            var vString = UaVariant.FromString("ZeroUniverse");
            var vArray = new UaVariant(new int[] { 10, 20, 30, 40, 50 }, UaBuiltInType.Int32, isArray: true);

            var enc = new UaBinaryEncoder();
            enc.WriteVariant(vDouble);
            enc.WriteVariant(vString);
            enc.WriteVariant(vArray);

            byte[] bytes = enc.ToArray();
            var dec = new UaBinaryDecoder(bytes);

            var rDouble = dec.ReadVariant();
            Assert.Equal(UaBuiltInType.Double, rDouble.Type);
            Assert.Equal(123.456, rDouble.ToDouble(), 5);

            var rString = dec.ReadVariant();
            Assert.Equal(UaBuiltInType.String, rString.Type);
            Assert.Equal("ZeroUniverse", rString.ToString());

            var rArray = dec.ReadVariant();
            Assert.True(rArray.IsArray);
            Assert.Equal(UaBuiltInType.Int32, rArray.Type);
            object[] items = (object[])rArray.Value!;
            Assert.Equal(5, items.Length);
            Assert.Equal(30, (int)items[2]);
        }

        [Fact]
        public void DataValue_RoundTrip_WithMetadata()
        {
            DateTime sampleTime = DateTime.UtcNow;
            var dataValue = new UaDataValue(UaVariant.FromDouble(98.6), statusCode: 0, sourceTimestamp: sampleTime);

            var enc = new UaBinaryEncoder();
            // DataValue serialization
            enc.WriteByte(0x05); // 0x01 value + 0x04 sourceTime
            enc.WriteVariant(dataValue.Value);
            enc.WriteDateTime(dataValue.SourceTimestamp!.Value);

            byte[] bytes = enc.ToArray();
            var dec = new UaBinaryDecoder(bytes);
            var decodedDataValue = dec.ReadDataValue();

            Assert.True(decodedDataValue.IsGood);
            Assert.Equal(98.6, decodedDataValue.Value.ToDouble(), 3);
            Assert.NotNull(decodedDataValue.SourceTimestamp);
            Assert.Equal(sampleTime.Ticks / 10000, decodedDataValue.SourceTimestamp.Value.Ticks / 10000);
        }

        [Fact]
        public void UaTcp_Hello_And_Acknowledge_Framing()
        {
            string url = "opc.tcp://192.168.1.100:4840/ZeroServer";
            byte[] helloPacket = UaBinaryEncoder.EncodeHello(url, 65536, 65536, 16777216, 5000);

            Assert.True(UaBinaryDecoder.TryReadHeader(helloPacket, out var header));
            Assert.Equal("HEL", header.MessageType);
            Assert.Equal('F', header.ChunkType);
            Assert.Equal((uint)helloPacket.Length, header.MessageSize);

            byte[] ackPacket = UaBinaryEncoder.EncodeAcknowledge(0, 32768, 32768, 8388608, 2500);
            Assert.True(UaBinaryDecoder.TryReadHeader(ackPacket, out var ackHeader));
            Assert.Equal("ACK", ackHeader.MessageType);
            Assert.Equal('F', ackHeader.ChunkType);
            Assert.Equal(32U, ackHeader.MessageSize);

            var dec = new UaBinaryDecoder(ackPacket.AsSpan(8));
            Assert.Equal(0U, dec.ReadUInt32()); // version
            Assert.Equal(32768U, dec.ReadUInt32()); // recv buf
            Assert.Equal(32768U, dec.ReadUInt32()); // send buf
            Assert.Equal(8388608U, dec.ReadUInt32()); // max msg size
            Assert.Equal(2500U, dec.ReadUInt32()); // max chunks
        }
    }
}
