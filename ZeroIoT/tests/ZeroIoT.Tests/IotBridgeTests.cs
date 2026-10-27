using System;
using System.Collections.Generic;
using Xunit;
using ZeroIoT.Bridges;
using ZeroIoT.OpcUa;
using ZeroIoT.SparkplugB;
using ZeroStorage.Core.Gorilla;

namespace ZeroIoT.Tests
{
    public class IotBridgeTests
    {
        [Fact]
        public void GorillaTsdbSink_IngestAndFlush_CompressesAndDecompressesAccurately()
        {
            var sink = new IotGorillaTsdbSink();
            string tag = "spindle/vibration/x";
            long baseTime = 1726000000000L;
            int count = 1000;

            // Ingest 1000 high-frequency sensor points (10ms intervals, sine wave)
            for (int i = 0; i < count; i++)
            {
                long ts = baseTime + (i * 10);
                double val = 10.0 + Math.Sin(i * 0.05) * 2.5;
                sink.Record(tag, ts, val);
            }

            Assert.Equal(count, sink.GetPointCount(tag));

            // Flush to compressed Gorilla byte stream
            byte[] compressed = sink.Flush(tag);
            Assert.NotNull(compressed);
            Assert.True(compressed.Length > 0);

            // Raw uncompressed size: 1000 * 16 bytes = 16,000 bytes.
            // Gorilla compresses 64-bit IEEE floating points and timestamps below 8,000 bytes (> 2x reduction)
            Assert.True(compressed.Length < 8000, $"Compressed size {compressed.Length} exceeds expected threshold");

            // Decompress and verify bit-level fidelity
            var decodedPoints = GorillaDecoder.Decode(compressed);
            Assert.Equal(count, decodedPoints.Count);

            for (int i = 0; i < count; i++)
            {
                long expectedTs = baseTime + (i * 10);
                double expectedVal = 10.0 + Math.Sin(i * 0.05) * 2.5;

                Assert.Equal(expectedTs, decodedPoints[i].TimestampMs);
                Assert.Equal(expectedVal, decodedPoints[i].Value, 5);
            }

            // After flush, buffer count must be reset to 0
            Assert.Equal(0, sink.GetPointCount(tag));
        }

        [Fact]
        public void DataFrameBridge_SparkplugPayload_ConvertsToColumnarTable()
        {
            var payload = new SparkplugPayload(seq: 1);
            payload.AddMetric(SparkplugMetric.CreateDouble("FlowRate", 15.6));
            payload.AddMetric(SparkplugMetric.CreateDouble("Pressure", 2.4));
            payload.AddMetric(SparkplugMetric.CreateDouble("ValvePosition", 100.0));

            var df = IotDataFrameBridge.ToDataFrame(payload);
            Assert.NotNull(df);
            Assert.Equal(3, df.RowCount);
            Assert.Equal(4, df.ColumnCount);
            Assert.Contains("Timestamp", df.ColumnNames);
            Assert.Contains("TagName", df.ColumnNames);
            Assert.Contains("DataType", df.ColumnNames);
            Assert.Contains("Value", df.ColumnNames);
        }

        [Fact]
        public void DataFrameBridge_OpcUaReadings_ConvertsToColumnarTable()
        {
            var readings = new List<(NodeId, UaDataValue)>
            {
                (NodeId.FromString("ns=1;s=Tag1"), new UaDataValue(UaVariant.FromDouble(10.5))),
                (NodeId.FromString("ns=1;s=Tag2"), new UaDataValue(UaVariant.FromDouble(20.5))),
                (NodeId.FromString("ns=1;s=Tag3"), new UaDataValue(UaVariant.FromDouble(30.5)))
            };

            var df = IotDataFrameBridge.ToDataFrame(readings);
            Assert.NotNull(df);
            Assert.Equal(3, df.RowCount);
            Assert.Equal(4, df.ColumnCount);
            Assert.Contains("NodeId", df.ColumnNames);
            Assert.Contains("Value", df.ColumnNames);
            Assert.Contains("StatusCode", df.ColumnNames);
        }
    }
}
