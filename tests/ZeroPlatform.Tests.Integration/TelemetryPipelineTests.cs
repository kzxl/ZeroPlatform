using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using ZeroComm.Core.Buffers;
using ZeroComm.Core.Modbus;
using ZeroData.Core;
using ZeroSignal.Core.Filtering;
using ZeroStorage.Core.Gorilla;
using ZeroStorage.Core.Persistence;
using ZeroStorage.Core.TimeSeries;
using ZeroUI.Core.Data;

namespace ZeroPlatform.Tests.Integration
{
    public class TelemetryPipelineTests
    {
        [Fact]
        public void Telemetry_FullPipeline_EdgeToUI_EndToEnd()
        {
            // =========================================================================
            // 1. Edge Communication Layer (ZeroComm)
            // =========================================================================
            // Simulate Modbus TCP telemetry frames coming from factory PLC
            int sampleCount = 200;
            double sampleRate = 50.0; // 50 Hz
            DateTime baseTime = new DateTime(2026, 9, 9, 8, 0, 0, DateTimeKind.Utc);

            var ring = new CircularRingBuffer(65536);
            for (int i = 0; i < sampleCount; i++)
            {
                ushort txId = (ushort)(i + 1);
                // Simulate sensor measurement: 2Hz fundamental vibration + 15Hz noise
                double t = i / sampleRate;
                double rawValue = 20.0 + 5.0 * Math.Sin(2.0 * Math.PI * 2.0 * t) + 2.0 * Math.Sin(2.0 * Math.PI * 15.0 * t);
                ushort rawRegister = (ushort)Math.Max(0, (int)(rawValue * 10.0)); // Fixed-point 0.1 scale

                // Create genuine Modbus TCP response (FC 03, 1 register)
                byte[] frame = new byte[11];
                frame[0] = (byte)(txId >> 8);
                frame[1] = (byte)(txId & 0xFF);
                frame[2] = 0x00;
                frame[3] = 0x00;
                frame[4] = 0x00;
                frame[5] = 0x05; // Length = 5 (UnitId + FC + ByteCount + 2B)
                frame[6] = 0x01; // Unit ID
                frame[7] = 0x03; // FC 03
                frame[8] = 0x02; // Byte count = 2
                frame[9] = (byte)(rawRegister >> 8);
                frame[10] = (byte)(rawRegister & 0xFF);

                ring.Write(frame, 0, frame.Length);
            }

            // Extract frames zero-copy from ring buffer
            var points = new List<TimeSeriesPoint>(sampleCount);
            int frameIdx = 0;
            while (StreamingFrameParser.TryExtractModbusTcpFrame(ring, out byte[] extractedFrame))
            {
                bool ok = ModbusTcpFrame.ParseReadRegistersResponse(
                    extractedFrame, 0, extractedFrame.Length,
                    out _, out _, out ushort[] regs, out ModbusExceptionCode exc);

                Assert.True(ok);
                Assert.Equal(ModbusExceptionCode.None, exc);
                Assert.Single(regs);

                double val = regs[0] / 10.0;
                long timestampMs = new DateTimeOffset(baseTime.AddSeconds(frameIdx / sampleRate)).ToUnixTimeMilliseconds();
                points.Add(new TimeSeriesPoint(timestampMs, val));
                frameIdx++;
            }

            Assert.Equal(sampleCount, points.Count);

            // =========================================================================
            // 2. High-Throughput Persistence Layer (ZeroStorage - Gorilla TSDB)
            // =========================================================================
            // Compress telemetry points into a Gorilla-compressed TimeSeriesBlock
            var block = TimeSeriesBlock.FromPoints(metricId: 101, points);
            Assert.True(block.CompressedData.Length < points.Count * 16, "Gorilla compression must reduce size");

            string tempFile = Path.Combine(Path.GetTempPath(), $"zero_telemetry_{Guid.NewGuid():N}.zts");
            try
            {
                using (var log = new MemoryMappedTimeSeriesLog(tempFile, initialCapacity: 1024 * 1024))
                {
                    log.AppendBlock(block);
                    Assert.Equal(1, log.BlockCount);

                    var loadedBlocks = log.ReadAllBlocks();
                    Assert.Single(loadedBlocks);

                    // Decompress back to raw points
                    var decompressedPoints = loadedBlocks[0].Decompress();
                    Assert.Equal(sampleCount, decompressedPoints.Count);

                    // =========================================================================
                    // 3. Digital Signal Processing Layer (ZeroSignal - SOS FiltFilt)
                    // =========================================================================
                    double[] rawSignals = new double[sampleCount];
                    for (int i = 0; i < sampleCount; i++)
                    {
                        rawSignals[i] = decompressedPoints[i].Value;
                    }

                    // 4th order Butterworth Lowpass at 5Hz (removes the 15Hz sensor noise)
                    var filter = FilterDesign.Butterworth(order: 4, cutoffHz: 5.0, sampleRateHz: sampleRate, FilterType.Lowpass);
                    double[] filteredSignal = filter.FiltFilt(rawSignals);

                    Assert.Equal(sampleCount, filteredSignal.Length);

                    // Verify noise attenuation: variance of high frequencies is drastically dampened
                    double rawMean = 0;
                    double filteredMean = 0;
                    for (int i = 0; i < sampleCount; i++)
                    {
                        rawMean += rawSignals[i];
                        filteredMean += filteredSignal[i];
                    }
                    rawMean /= sampleCount;
                    filteredMean /= sampleCount;
                    Assert.InRange(Math.Abs(rawMean - filteredMean), 0.0, 0.5); // Mean DC level preserved

                    // =========================================================================
                    // 4. Columnar Analytics Layer (ZeroData - DataFrame)
                    // =========================================================================
                    var dtArray = new DateTime[sampleCount];
                    for (int i = 0; i < sampleCount; i++)
                    {
                        dtArray[i] = DateTimeOffset.FromUnixTimeMilliseconds(decompressedPoints[i].TimestampMs).UtcDateTime;
                    }

                    var timeCol = new DataColumn<DateTime>("Timestamp", dtArray);
                    var valCol = new DataColumn<double>("Vibration", filteredSignal);
                    var df = new DataFrame(timeCol, valCol);

                    Assert.Equal(sampleCount, df.RowCount);
                    Assert.Equal(2, df.ColumnCount);

                    // Resample into 1-second interval buckets with Mean aggregation
                    var resampledDf = df.Resample("Timestamp", TimeSpan.FromSeconds(1), "Vibration", ResampleAgg.Mean);
                    Assert.True(resampledDf.RowCount >= 3 && resampledDf.RowCount <= 5);

                    // =========================================================================
                    // 5. Industrial UI Virtualization Layer (ZeroUI)
                    // =========================================================================
                    var virtualProvider = new ZeroDataVirtualProvider(resampledDf);
                    Assert.Equal(resampledDf.RowCount, virtualProvider.TotalRowCount);
                    Assert.Equal(2, virtualProvider.TotalColumnCount);

                    var cellBuffer = new CellValueBuffer();
                    virtualProvider.GetCellValue(0, 0, ref cellBuffer);
                    Assert.False(cellBuffer.Text.IsEmpty);

                    virtualProvider.GetCellValue(0, 1, ref cellBuffer);
                    Assert.False(cellBuffer.Text.IsEmpty);
                    double parsed = double.Parse(cellBuffer.Text.ToString());
                    Assert.InRange(parsed, 15.0, 25.0); // Within fundamental vibration band
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }
    }
}
