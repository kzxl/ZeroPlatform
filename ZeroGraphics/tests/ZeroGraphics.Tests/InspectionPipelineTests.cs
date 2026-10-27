using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Gpu;
using ZeroGraphics.Vision.Calibration;
using ZeroGraphics.Vision.Codes;
using ZeroGraphics.Vision.Metrology;
using ZeroGraphics.Vision.Pipeline;

namespace ZeroGraphics.Tests
{
    public class InspectionPipelineTests
    {
        [Fact]
        public async Task RingBuffer_LockFreeSlotAcquisition_IsThreadSafe()
        {
            using var ringBuffer = new UnmanagedFrameRingBuffer(slotCount: 8, maxFrameWidth: 64, maxFrameHeight: 64);

            int iterations = 1000;
            int producerCount = 4;
            int consumerCount = 4;

            var cts = new CancellationTokenSource();
            long producedTotal = 0;
            long consumedTotal = 0;

            // Start consumers
            var consumers = new Task[consumerCount];
            for (int c = 0; c < consumerCount; c++)
            {
                consumers[c] = Task.Run(() =>
                {
                    while (!cts.Token.IsCancellationRequested || Volatile.Read(ref producedTotal) < iterations * producerCount)
                    {
                        if (ringBuffer.TryAcquireReadySlot(out int slotIndex))
                        {
                            Interlocked.Increment(ref consumedTotal);
                            ringBuffer.ReleaseSlot(slotIndex);
                        }
                        else
                        {
                            Thread.Sleep(0);
                        }
                    }
                });
            }

            // Start producers
            var producers = new Task[producerCount];
            for (int p = 0; p < producerCount; p++)
            {
                producers[p] = Task.Run(() =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        int slotIndex;
                        while (!ringBuffer.TryAcquireWritingSlot(out slotIndex))
                        {
                            Thread.Sleep(0);
                        }

                        Interlocked.Increment(ref producedTotal);
                        ringBuffer.CommitWritingSlot(slotIndex, i, 64, 64, ImageFormatMode.Gray8, 64);
                    }
                });
            }

            await Task.WhenAll(producers);
            cts.CancelAfter(2000);

            SpinWait.SpinUntil(() => Volatile.Read(ref consumedTotal) >= Volatile.Read(ref producedTotal), 3000);

            Assert.Equal(producedTotal, consumedTotal);
            Assert.Equal(producedTotal, ringBuffer.PushedCount);
        }

        [Fact]
        public void PipelineHost_ProcessesHighThroughputStream_WithoutFrameDrops()
        {
            // Synthesize an EAN-13 barcode image
            string payload = "8935217402731";
            bool[,] grid = Ean13Encoder.EncodeSymbol(payload, quietZone: 10, height: 30);
            int h = grid.GetLength(0);
            int w = grid.GetLength(1);

            using var testImage = ImageBuffer.CreateGray8(w, h);
            testImage.Clear(255);
            unsafe
            {
                for (int y = 0; y < h; y++)
                {
                    byte* row = testImage.GetRowPointer(y);
                    for (int x = 0; x < w; x++)
                    {
                        if (grid[y, x]) row[x] = 0;
                    }
                }
            }

            var options = new InspectionPipelineOptions
            {
                SlotCount = 8,
                WorkerCount = 2,
                MaxFrameWidth = w,
                MaxFrameHeight = h
            };

            using var pipeline = new InspectionPipelineHost(options);

            int targetFrames = 20;
            int inspectedCount = 0;
            var receivedCodes = new List<string>();
            var allDoneEvent = new ManualResetEventSlim(false);

            pipeline.OnInspectionCompleted += (result) =>
            {
                if (result.IsPass && result.Barcode != null)
                {
                    lock (receivedCodes)
                    {
                        receivedCodes.Add(result.Barcode.Text);
                    }
                }

                if (Interlocked.Increment(ref inspectedCount) >= targetFrames)
                {
                    allDoneEvent.Set();
                }
            };

            // Push 20 frames into pipeline
            for (int i = 0; i < targetFrames; i++)
            {
                bool pushed = pipeline.PushFrame(testImage, userData: $"Batch_{i}");
                Assert.True(pushed, $"Frame #{i} should be successfully queued into ring buffer.");
                Thread.Sleep(5); // Simulate ~200 FPS camera interval
            }

            bool completedInTime = allDoneEvent.Wait(5000);
            Assert.True(completedInTime, "Pipeline should process all 20 frames within 5 seconds.");

            Assert.Equal(targetFrames, inspectedCount);
            Assert.Equal(targetFrames, receivedCodes.Count);
            Assert.All(receivedCodes, code => Assert.Equal(payload, code));
        }

        [Fact]
        public void PipelineHost_CustomInspector_CalculatesPhysicalDimensions()
        {
            var options = new InspectionPipelineOptions
            {
                SlotCount = 8,
                WorkerCount = 1,
                MaxFrameWidth = 100,
                MaxFrameHeight = 100
            };

            using var pipeline = new InspectionPipelineHost(options);
            var calib = MetricCalibration2D.FromScale(0.05, MetricUnit.Millimeters);

            // Register custom metrology inspector
            pipeline.Inspector = (image, ctx) =>
            {
                PointF p1 = new PointF(10, 50);
                PointF p2 = new PointF(90, 50); // 80 pixels * 0.05 mm/px = 4.00 mm

                var dist = MetricMeasurement.MeasureDistance(p1, p2, calib);

                var payload = new InspectionResultPayload(
                    ctx.FrameId,
                    isPass: true,
                    statusMessage: $"Width: {dist.Text}",
                    durationMs: 1.0);

                payload.Measurements.Add(dist);
                payload.Overlays.Add(new CameraOverlayBox(
                    new RectangleF(10, 45, 80, 10),
                    Color.Lime,
                    label: dist.Text));

                return payload;
            };

            InspectionResultPayload? receivedPayload = null;
            var resetEvent = new ManualResetEventSlim(false);

            pipeline.OnInspectionCompleted += (result) =>
            {
                receivedPayload = result;
                resetEvent.Set();
            };

            using var frame = ImageBuffer.CreateGray8(100, 100);
            frame.Clear(128);

            pipeline.PushFrame(frame);

            bool ok = resetEvent.Wait(2000);
            Assert.True(ok);
            Assert.NotNull(receivedPayload);
            Assert.True(receivedPayload!.IsPass);
            Assert.Single(receivedPayload.Measurements);
            Assert.Equal(4.0, receivedPayload.Measurements[0].MetricDistance, 2);
            Assert.Single(receivedPayload.Overlays);
        }
    }
}
