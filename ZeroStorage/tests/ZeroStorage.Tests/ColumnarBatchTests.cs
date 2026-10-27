using System;
using System.Collections.Generic;
using Xunit;
using ZeroData.Core;
using ZeroStorage.Core.TimeSeries;

namespace ZeroStorage.Tests
{
    public class ColumnarBatchTests
    {
        [Fact]
        public void ColumnarBatch_CompressAndDecompress_FullFidelity()
        {
            var schema = new MultiMetricSchema("StationTelemetry",
                new MultiMetricField("Temperature", MultiMetricType.Float64),
                new MultiMetricField("Pressure", MultiMetricType.Float64),
                new MultiMetricField("CycleCount", MultiMetricType.Int32),
                new MultiMetricField("TotalEnergy", MultiMetricType.Int64),
                new MultiMetricField("IsRunning", MultiMetricType.Boolean),
                new MultiMetricField("CalibrationFactor", MultiMetricType.Decimal),
                new MultiMetricField("AlarmCode", MultiMetricType.String)
            );

            int rowCount = 500;
            var batch = new ColumnarBatch(schema, rowCount);
            batch.SetRowCount(rowCount);

            long baseTime = 1700000000000L;
            var tempCol = batch.GetFloat64Column("Temperature");
            var presCol = batch.GetFloat64Column("Pressure");
            var cycleCol = batch.GetInt32Column("CycleCount");
            var energyCol = batch.GetInt64Column("TotalEnergy");
            var isRunningCol = batch.GetBooleanColumn("IsRunning");
            var calCol = batch.GetDecimalColumn("CalibrationFactor");
            var alarmCol = batch.GetStringColumn("AlarmCode");

            for (int i = 0; i < rowCount; i++)
            {
                batch.Timestamps[i] = baseTime + (i * 100);
                tempCol[i] = 20.0 + Math.Sin(i * 0.05) * 5.0;
                presCol[i] = 101.3 + (i * 0.01);
                cycleCol[i] = i * 10;
                energyCol[i] = 5000000000L + i * 1000L;
                isRunningCol[i] = (i % 2 == 0);
                calCol[i] = 1.0005m + (i * 0.0001m);
                alarmCol[i] = (i % 50 == 0) ? $"ERR_{i}" : string.Empty;
            }

            // Compress ColumnarBatch
            var block = MultiMetricBlock.Compress(schema, batch);
            Assert.Equal(rowCount, block.RowCount);
            Assert.True(block.CompressedData.Length > 0);

            // Decompress back to ColumnarBatch
            var decompressed = block.DecompressColumnar();
            Assert.Equal(rowCount, decompressed.RowCount);
            Assert.Equal("StationTelemetry", decompressed.Schema.SeriesName);

            var dTemp = decompressed.GetFloat64Column("Temperature");
            var dPres = decompressed.GetFloat64Column("Pressure");
            var dCycle = decompressed.GetInt32Column("CycleCount");
            var dEnergy = decompressed.GetInt64Column("TotalEnergy");
            var dRun = decompressed.GetBooleanColumn("IsRunning");
            var dCal = decompressed.GetDecimalColumn("CalibrationFactor");
            var dAlarm = decompressed.GetStringColumn("AlarmCode");

            for (int i = 0; i < rowCount; i++)
            {
                Assert.Equal(batch.Timestamps[i], decompressed.Timestamps[i]);
                Assert.Equal(tempCol[i], dTemp[i], 4);
                Assert.Equal(presCol[i], dPres[i], 4);
                Assert.Equal(cycleCol[i], dCycle[i]);
                Assert.Equal(energyCol[i], dEnergy[i]);
                Assert.Equal(isRunningCol[i], dRun[i]);
                Assert.Equal(calCol[i], dCal[i]);
                Assert.Equal(alarmCol[i], dAlarm[i]);
            }
        }

        [Fact]
        public void ColumnarBatch_CrossCompatibility_WithLegacyRows()
        {
            var schema = new MultiMetricSchema("LegacyInterOp",
                new MultiMetricField("Voltage", MultiMetricType.Float64),
                new MultiMetricField("Counter", MultiMetricType.Int32)
            );

            var rows = new List<MultiMetricRow>();
            for (int i = 0; i < 50; i++)
            {
                rows.Add(new MultiMetricRow(1000L + i * 10, 220.0 + i * 0.1, i * 5));
            }

            // Compress with legacy rows
            var blockFromRows = MultiMetricBlock.Compress(schema, rows);

            // Decompress via ColumnarBatch
            var batch = blockFromRows.DecompressColumnar();
            Assert.Equal(50, batch.RowCount);
            Assert.Equal(220.0, batch.GetFloat64Column("Voltage")[0], 4);
            Assert.Equal(0, batch.GetInt32Column("Counter")[0]);
            Assert.Equal(220.0 + 49 * 0.1, batch.GetFloat64Column("Voltage")[49], 4);
            Assert.Equal(245, batch.GetInt32Column("Counter")[49]);

            // Re-compress via ColumnarBatch and decompress via legacy rows
            var blockFromBatch = MultiMetricBlock.Compress(schema, batch);
            var reRows = blockFromBatch.Decompress();

            Assert.Equal(50, reRows.Count);
            for (int i = 0; i < 50; i++)
            {
                Assert.Equal(rows[i].TimestampMs, reRows[i].TimestampMs);
                Assert.Equal((double)rows[i].Values[0]!, (double)reRows[i].Values[0]!, 4);
                Assert.Equal((int)rows[i].Values[1]!, (int)reRows[i].Values[1]!);
            }
        }

        [Fact]
        public void ColumnarBatch_ToDataFrame_DirectConversion()
        {
            var schema = new MultiMetricSchema("DFTest",
                new MultiMetricField("Load", MultiMetricType.Float64),
                new MultiMetricField("Status", MultiMetricType.Int32)
            );

            var batch = new ColumnarBatch(schema, 20);
            batch.SetRowCount(20);
            for (int i = 0; i < 20; i++)
            {
                batch.Timestamps[i] = 1600000000000L + i * 1000;
                batch.GetFloat64Column(0)[i] = 85.5 + i;
                batch.GetInt32Column(1)[i] = 1;
            }

            DataFrame df = batch.ToDataFrame();
            Assert.Equal(20, df.RowCount);
            Assert.Equal(3, df.ColumnCount); // Timestamp + Load + Status

            var block = MultiMetricBlock.Compress(schema, batch);
            DataFrame dfFromBlock = block.ToDataFrame();
            Assert.Equal(20, dfFromBlock.RowCount);
            Assert.Equal(3, dfFromBlock.ColumnCount);
        }
    }
}
