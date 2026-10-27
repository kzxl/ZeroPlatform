using System;
using System.Collections.Generic;
using Xunit;
using ZeroData.Core;
using ZeroStorage.Core.TimeSeries;

namespace ZeroStorage.Tests
{
    public class MultiMetricTests
    {
        [Fact]
        public void MultiMetricBlock_CompressAndDecompress_PreservesAllFieldTypes()
        {
            var schema = new MultiMetricSchema("MillingMachine_01",
                new MultiMetricField("Temperature", MultiMetricType.Float64),
                new MultiMetricField("Pressure", MultiMetricType.Float64),
                new MultiMetricField("RPM", MultiMetricType.Int32),
                new MultiMetricField("CycleCount", MultiMetricType.Int64),
                new MultiMetricField("IsRunning", MultiMetricType.Boolean),
                new MultiMetricField("HourlyCost", MultiMetricType.Decimal),
                new MultiMetricField("Operator", MultiMetricType.String)
            );

            int rowCount = 200;
            long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var rows = new List<MultiMetricRow>(rowCount);

            for (int i = 0; i < rowCount; i++)
            {
                long ts = baseTime + (i * 20); // 20ms intervals
                double temp = 65.0 + (i * 0.1);
                double press = 101.3 + Math.Sin(i) * 2.0;
                int rpm = 1200 + (i % 50);
                long cycles = 100000L + i;
                bool isRunning = (i % 2) == 0;
                decimal cost = 150.50m + (i * 0.25m);
                string op = (i % 3 == 0) ? "Operator_A" : "Operator_B";

                rows.Add(new MultiMetricRow(ts, temp, press, rpm, cycles, isRunning, cost, op));
            }

            // 1. Compress into MultiMetricBlock
            var block = MultiMetricBlock.Compress(schema, rows);
            Assert.Equal(rowCount, block.RowCount);
            Assert.NotEmpty(block.CompressedData);

            // Verify compression: raw data size would be ~ (8+8+8+4+8+1+16+12)*200 = ~13,000 bytes
            Assert.True(block.CompressedData.Length < 11000, $"Compressed size was {block.CompressedData.Length}");

            // 2. Decompress and verify
            var decompressed = block.Decompress();
            Assert.Equal(rowCount, decompressed.Count);

            for (int i = 0; i < rowCount; i++)
            {
                Assert.Equal(rows[i].TimestampMs, decompressed[i].TimestampMs);
                Assert.Equal((double)rows[i].Values[0]!, (double)decompressed[i].Values[0]!, 4);
                Assert.Equal((double)rows[i].Values[1]!, (double)decompressed[i].Values[1]!, 4);
                Assert.Equal(rows[i].Values[2], decompressed[i].Values[2]);
                Assert.Equal(rows[i].Values[3], decompressed[i].Values[3]);
                Assert.Equal(rows[i].Values[4], decompressed[i].Values[4]);
                Assert.Equal(rows[i].Values[5], decompressed[i].Values[5]);
                Assert.Equal(rows[i].Values[6], decompressed[i].Values[6]);
            }
        }

        [Fact]
        public void MultiMetricBlock_ToDataFrame_CreatesTypedColumnarDataFrame()
        {
            var schema = new MultiMetricSchema("Telemetry_Unit_09",
                new MultiMetricField("Voltage", MultiMetricType.Float64),
                new MultiMetricField("Current", MultiMetricType.Float64),
                new MultiMetricField("ErrorCode", MultiMetricType.Int32),
                new MultiMetricField("IsAlarm", MultiMetricType.Boolean),
                new MultiMetricField("PowerKwh", MultiMetricType.Decimal)
            );

            long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var rows = new List<MultiMetricRow>
            {
                new MultiMetricRow(baseTime, 220.5, 5.2, 0, false, 1.15m),
                new MultiMetricRow(baseTime + 100, 219.8, 5.4, 0, false, 1.16m),
                new MultiMetricRow(baseTime + 200, 180.2, 12.8, 404, true, 2.45m)
            };

            var block = MultiMetricBlock.Compress(schema, rows);

            // Bridge to DataFrame
            var df = block.ToDataFrame();

            Assert.Equal(3, df.RowCount);
            Assert.Equal(6, df.ColumnCount); // Timestamp + 5 fields
            Assert.True(df.HasColumn("Timestamp"));
            Assert.True(df.HasColumn("Voltage"));
            Assert.True(df.HasColumn("Current"));
            Assert.True(df.HasColumn("ErrorCode"));
            Assert.True(df.HasColumn("IsAlarm"));
            Assert.True(df.HasColumn("PowerKwh"));

            Assert.Equal(220.5, df.Column<double>("Voltage")[0]);
            Assert.Equal(404, df.Column<int>("ErrorCode")[2]);
            Assert.True(df.Column<bool>("IsAlarm")[2]);
            Assert.Equal(2.45m, df.Column<decimal>("PowerKwh")[2]);
        }

        [Fact]
        public void TimeSeriesBlock_ToDataFrame_BridgesUnivariateBlock()
        {
            var points = new List<ZeroStorage.Core.Gorilla.TimeSeriesPoint>
            {
                new ZeroStorage.Core.Gorilla.TimeSeriesPoint(1000, 55.5),
                new ZeroStorage.Core.Gorilla.TimeSeriesPoint(2000, 60.2),
                new ZeroStorage.Core.Gorilla.TimeSeriesPoint(3000, 58.7)
            };

            var block = TimeSeriesBlock.FromPoints(metricId: 42, points);

            var df = block.ToDataFrame();
            Assert.Equal(3, df.RowCount);
            Assert.Equal(2, df.ColumnCount);
            Assert.Equal(55.5, df.Column<double>("Value")[0]);
            Assert.Equal(60.2, df.Column<double>("Value")[1]);
            Assert.Equal(58.7, df.Column<double>("Value")[2]);
        }
    }
}
