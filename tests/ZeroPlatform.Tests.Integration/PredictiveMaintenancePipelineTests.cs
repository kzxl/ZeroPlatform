using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using ZeroComm.Core.Buffers;
using ZeroComm.Core.Checksums;
using ZeroComm.Core.Modbus;
using ZeroData.Core;
using ZeroData.Core.Arrow;
using ZeroInference.Core.Layers;
using ZeroSignal.Core.Spectral;
using ZeroStorage.Core.Gorilla;
using ZeroStorage.Core.Persistence;
using ZeroStorage.Core.TimeSeries;
using ZeroTensor.Core;
using ZeroUI.Core.Data;

namespace ZeroPlatform.Tests.Integration
{
    public class PredictiveMaintenancePipelineTests
    {
        [Fact]
        public void PredictiveMaintenance_AutonomousPipeline_ModbusToAttention_EndToEnd()
        {
            // =========================================================================
            // 1. Edge Communication Layer (ZeroComm - Modbus RTU / Serial Frame Parsing)
            // =========================================================================
            int sampleCount = 128;
            double sampleRate = 100.0; // 100 Hz sampling on RS-485 edge sensor
            long baseTimestamp = 1700000000000L;

            var ring = new CircularRingBuffer(32768);

            // Generate synthetic vibration waveform:
            // Samples 0..79: normal operating vibration (10Hz shaft rotation + low amplitude noise)
            // Samples 80..127: anomalous high-frequency bearing chatter/defect spike
            for (int i = 0; i < sampleCount; i++)
            {
                double t = i / sampleRate;
                double vibration = 10.0 + 3.0 * Math.Sin(2.0 * Math.PI * 10.0 * t);
                if (i >= 80)
                {
                    // Bearing fault signature: severe high-frequency oscillation
                    vibration += 8.0 * Math.Sin(2.0 * Math.PI * 40.0 * t);
                }

                ushort regVal = (ushort)Math.Max(0, (int)(vibration * 10.0)); // Fixed-point 0.1 scale

                // Build genuine Modbus RTU Response:
                // [UnitId(1) + FC(0x03)(1) + ByteCount(2)(1) + RegHigh(1) + RegLow(1) + CRCLow(1) + CRCHigh(1)] = 7 bytes
                byte[] rtuFrame = new byte[7];
                rtuFrame[0] = 0x01; // Unit ID
                rtuFrame[1] = 0x03; // FC 03 (Read Holding Registers)
                rtuFrame[2] = 0x02; // 2 bytes payload
                rtuFrame[3] = (byte)(regVal >> 8);
                rtuFrame[4] = (byte)(regVal & 0xFF);

                ushort crc = Crc16.ComputeModbus(rtuFrame, 0, 5);
                rtuFrame[5] = (byte)(crc & 0xFF);
                rtuFrame[6] = (byte)((crc >> 8) & 0xFF);

                ring.Write(rtuFrame, 0, rtuFrame.Length);
            }

            // Extract Modbus RTU frames with CRC validation
            var extractedPoints = new List<TimeSeriesPoint>(sampleCount);
            int frameCount = 0;
            while (StreamingFrameParser.TryExtractModbusRtuFrame(ring, out byte[] extractedFrame))
            {
                bool parsed = ModbusRtuFrame.ParseReadRegistersResponse(
                    extractedFrame, 0, extractedFrame.Length,
                    out byte unitId, out ushort[] regs, out ModbusExceptionCode exc);

                Assert.True(parsed);
                Assert.Equal(0x01, unitId);
                Assert.Equal(ModbusExceptionCode.None, exc);
                Assert.Single(regs);

                double value = regs[0] / 10.0;
                long timestampMs = baseTimestamp + (long)(frameCount * (1000.0 / sampleRate));
                extractedPoints.Add(new TimeSeriesPoint(timestampMs, value));
                frameCount++;
            }

            Assert.Equal(sampleCount, extractedPoints.Count);

            // =========================================================================
            // 2. Storage & Lifecycle Layer (ZeroStorage - WAL, Gorilla TSDB & Retention)
            // =========================================================================
            string tempWal = Path.Combine(Path.GetTempPath(), $"pm_wal_{Guid.NewGuid():N}.wal");
            string tempRawLog = Path.Combine(Path.GetTempPath(), $"pm_raw_{Guid.NewGuid():N}.zts");
            string tempRollupLog = Path.Combine(Path.GetTempPath(), $"pm_rollup_{Guid.NewGuid():N}.zts");

            try
            {
                // A. Write-Ahead Log Durability
                using (var wal = new WriteAheadLog(tempWal))
                {
                    for (int i = 0; i < extractedPoints.Count; i++)
                    {
                        byte[] payload = BitConverter.GetBytes(extractedPoints[i].Value);
                        wal.Append(1, payload, flush: i == extractedPoints.Count - 1);
                    }
                    Assert.Equal(sampleCount, wal.LastLsn);
                }

                // Verify WAL replay recovery
                using (var wal = new WriteAheadLog(tempWal))
                {
                    var replayed = wal.ReadAllRecords();
                    Assert.Equal(sampleCount, replayed.Count);
                }

                // B. Gorilla Compressed TimeSeries Logging
                var rawBlock = TimeSeriesBlock.FromPoints(metricId: 401, extractedPoints);
                Assert.True(rawBlock.CompressedData.Length < sampleCount * 16, "Gorilla compression must compress data.");

                using (var rawLog = new MemoryMappedTimeSeriesLog(tempRawLog, 2 * 1024 * 1024))
                {
                    rawLog.AppendBlock(rawBlock);
                    Assert.Equal(1, rawLog.BlockCount);
                }

                // C. Autonomous Retention Policy & Downsampling Rollups
                // Set TTL such that all points are rolled up into 200ms buckets
                long currentTime = baseTimestamp + 10000;
                var retentionRule = new RetentionRule(
                    rawRetentionDurationMs: 1, // Trigger rollup on older raw telemetry
                    rollupBucketSizeMs: 200,   // Downsample to 200ms buckets
                    rollupAggregation: AggregationType.Mean,
                    archiveRetentionDurationMs: 1000000);

                var retentionResult = RetentionPolicyEngine.ExecuteLifecycle(
                    tempRawLog, tempRollupLog, currentTime, retentionRule);

                Assert.Equal(1, retentionResult.RawBlocksExamined);
                Assert.Equal(1, retentionResult.RawBlocksPurged);
                Assert.Equal(sampleCount, retentionResult.RawPointsAggregated);
                Assert.True(retentionResult.RollupBlocksCreated >= 1);

                // Verify rollup log contains downsampled aggregate blocks
                using (var rollupLog = new MemoryMappedTimeSeriesLog(tempRollupLog, 2 * 1024 * 1024))
                {
                    var rollups = rollupLog.ReadAllBlocks();
                    Assert.NotEmpty(rollups);
                    var downsampledPoints = rollups[0].Decompress();
                    Assert.True(downsampledPoints.Count < sampleCount, "Rollup must downsample point count");
                }

                // =========================================================================
                // 3. Digital Signal Processing Layer (ZeroSignal - STFT Spectrogram)
                // =========================================================================
                double[] signal = new double[sampleCount];
                for (int i = 0; i < sampleCount; i++)
                {
                    signal[i] = extractedPoints[i].Value;
                }

                // Compute STFT Spectrogram: windowSize=32, hopSize=16
                int windowSize = 32;
                int hopSize = 16;
                double[,] spectrogram = StftTransform.Spectrogram(
                    signal, windowSize, hopSize, WindowFunction.Hann, toDecibels: false);

                int freqBins = spectrogram.GetLength(0);
                int timeFrames = spectrogram.GetLength(1);
                Assert.True(freqBins > 0);
                Assert.True(timeFrames > 0);

                // Compute high-frequency energy profile across time frames
                // Bins near top represent high frequency vibration
                double[] frameHighFreqEnergy = new double[timeFrames];
                for (int f = 0; f < timeFrames; f++)
                {
                    double sumHighFreq = 0.0;
                    for (int b = freqBins / 2; b < freqBins; b++)
                    {
                        sumHighFreq += spectrogram[b, f];
                    }
                    frameHighFreqEnergy[f] = sumHighFreq;
                }

                // High-frequency energy in anomalous second half must exceed normal first half
                double earlyEnergy = frameHighFreqEnergy[0];
                double lateEnergy = frameHighFreqEnergy[timeFrames - 1];
                Assert.True(lateEnergy > earlyEnergy, "Spectrogram must capture elevated high-frequency fault vibration.");

                // =========================================================================
                // 4. Columnar Analytics & Arrow IPC Layer (ZeroData - Zero-Copy Exchange)
                // =========================================================================
                int[] frameIndices = new int[timeFrames];
                double[] energies = new double[timeFrames];
                for (int i = 0; i < timeFrames; i++)
                {
                    frameIndices[i] = i;
                    energies[i] = frameHighFreqEnergy[i];
                }

                var df = new DataFrame(
                    new DataColumn<int>("FrameIndex", frameIndices),
                    new DataColumn<double>("HighFreqEnergy", energies)
                );

                Assert.Equal(timeFrames, df.RowCount);

                // Serialize DataFrame to Apache Arrow IPC format (zero third-party NuGet)
                byte[] arrowIpcBytes = ArrowIpcWriter.Serialize(df);
                Assert.NotEmpty(arrowIpcBytes);

                // Deserialize Arrow IPC bytes back to DataFrame for verification
                var roundTripDf = ArrowIpcReader.Deserialize(arrowIpcBytes);
                Assert.Equal(df.RowCount, roundTripDf.RowCount);
                Assert.Equal(df.ColumnCount, roundTripDf.ColumnCount);

                var roundTripEnergyCol = (DataColumn<double>)roundTripDf["HighFreqEnergy"];
                for (int i = 0; i < timeFrames; i++)
                {
                    Assert.Equal(energies[i], roundTripEnergyCol[i], precision: 4);
                }

                // =========================================================================
                // 5. Sequence Attention Anomaly Detection Layer (ZeroInference)
                // =========================================================================
                // Project time frames into a sequence embedding for MultiHeadAttention: [SeqLen, EmbedDim]
                int seqLen = timeFrames;
                int embedDim = 8;
                int numHeads = 2;

                var embeddingData = new float[seqLen * embedDim];
                for (int s = 0; s < seqLen; s++)
                {
                    float energyNorm = (float)(roundTripEnergyCol[s] / 100.0);
                    for (int d = 0; d < embedDim; d++)
                    {
                        embeddingData[s * embedDim + d] = energyNorm * (d + 1);
                    }
                }

                var seqTensor = Tensor.FromArray(embeddingData, seqLen, embedDim);
                var mha = new MultiHeadAttention(embedDim: embedDim, numHeads: numHeads);

                // Execute forward attention pass
                var contextOutput = mha.Forward(seqTensor);
                Assert.Equal(2, contextOutput.Rank);
                Assert.Equal(seqLen, contextOutput.Shape[0]);
                Assert.Equal(embedDim, contextOutput.Shape[1]);

                // Compute self-attention attribution matrix [SeqLen, SeqLen]
                var attentionMap = mha.ComputeAttentionMap(seqTensor);
                Assert.Equal(seqLen, attentionMap.Shape[0]);
                Assert.Equal(seqLen, attentionMap.Shape[1]);

                // Verify attention probabilities sum to ~1.0 per query row
                for (int r = 0; r < seqLen; r++)
                {
                    float rowSum = 0.0f;
                    for (int c = 0; c < seqLen; c++)
                    {
                        rowSum += attentionMap[r, c];
                    }
                    Assert.InRange(rowSum, 0.99f, 1.01f);
                }

                // =========================================================================
                // 6. Industrial UI Virtual Grid Binding Layer (ZeroUI)
                // =========================================================================
                var virtualProvider = new ZeroDataVirtualProvider(roundTripDf);
                Assert.Equal(timeFrames, virtualProvider.TotalRowCount);
                Assert.Equal(2, virtualProvider.TotalColumnCount);

                var cellBuffer = new CellValueBuffer();
                virtualProvider.GetCellValue(0, 0, ref cellBuffer); // FrameIndex 0
                Assert.Equal("0", cellBuffer.Text.ToString());

                virtualProvider.GetCellValue(timeFrames - 1, 1, ref cellBuffer); // Late anomaly energy
                double parsedEnergy = double.Parse(cellBuffer.Text.ToString());
                Assert.True(parsedEnergy > 0.0);
            }
            finally
            {
                if (File.Exists(tempWal)) try { File.Delete(tempWal); } catch { }
                if (File.Exists(tempRawLog)) try { File.Delete(tempRawLog); } catch { }
                if (File.Exists(tempRollupLog)) try { File.Delete(tempRollupLog); } catch { }
            }
        }
    }
}
