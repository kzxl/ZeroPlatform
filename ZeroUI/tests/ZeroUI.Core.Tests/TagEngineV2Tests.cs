using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Tests
{
    [Collection("ScadaTagEngine")]
    public class TagEngineV2Tests
    {
        private sealed class MockBindableControl : IScadaBindable
        {
            public string? BoundTagPath { get; set; }
            public int NotificationCount { get; private set; }
            public IScadaTag? LastReceivedTag { get; private set; }

            public MockBindableControl(string boundTagPath)
            {
                BoundTagPath = boundTagPath;
            }

            public void OnTagValueChanged(IScadaTag tag)
            {
                NotificationCount++;
                LastReceivedTag = tag;
            }
        }

        [Fact]
        public void TagEngine_TagIdRegistrationAndFlatStorage_OperatesInConstantTime()
        {
            int id1 = ZeroTagEngine.GetOrRegisterTag("Plant.Line1.Motor.Speed");
            int id2 = ZeroTagEngine.GetOrRegisterTag("Plant.Line1.Motor.Temperature");

            Assert.True(id1 >= 0);
            Assert.True(id2 >= 0);
            Assert.NotEqual(id1, id2);

            // Re-registering returns identical TagId
            Assert.Equal(id1, ZeroTagEngine.GetOrRegisterTag("Plant.Line1.Motor.Speed"));
            Assert.Equal("Plant.Line1.Motor.Speed", ZeroTagEngine.GetTagPath(id1));

            // Set numeric directly via TagId
            bool updated = ZeroTagEngine.SetNumeric(id1, 1450.5, ScadaQuality.Good);
            Assert.True(updated);

            // Verify flat storage O(1) read
            var storedVal = ZeroTagEngine.Storage.GetValue(id1);
            Assert.Equal(1450.5, storedVal.AsDouble());
            Assert.Equal(ScadaQuality.Good, storedVal.Quality);

            // Verify GetTag returns accurate snapshot
            var tagSnapshot = ZeroTagEngine.GetTag(id1);
            Assert.NotNull(tagSnapshot);
            Assert.Equal(1450.5, Convert.ToDouble(tagSnapshot!.Value));
        }

        [Fact]
        public void TagEngine_InvertedIndex_DispatchesOnlyToTargetControls()
        {
            ZeroUI.Core.Runtime.UiDispatcher.Reset();
            var motorSpeedCtrl = new MockBindableControl("Plant.Line2.Speed");
            var motorTempCtrl = new MockBindableControl("Plant.Line2.Temperature");

            ZeroTagEngine.RegisterBindable(motorSpeedCtrl);
            ZeroTagEngine.RegisterBindable(motorTempCtrl);

            int speedCountBefore = motorSpeedCtrl.NotificationCount;
            int tempCountBefore = motorTempCtrl.NotificationCount;

            // Update speed
            ZeroTagEngine.SetTagValue("Plant.Line2.Speed", 2500.0);

            // Speed control notified, temperature control untouched
            Assert.Equal(speedCountBefore + 1, motorSpeedCtrl.NotificationCount);
            Assert.Equal(tempCountBefore, motorTempCtrl.NotificationCount);
            Assert.Equal(2500.0, Convert.ToDouble(motorSpeedCtrl.LastReceivedTag!.Value));

            // Clean up
            ZeroTagEngine.UnregisterBindable(motorSpeedCtrl);
            ZeroTagEngine.UnregisterBindable(motorTempCtrl);
        }

        [Fact]
        public void ZeroTripleBuffer_GuaranteesLockFreeLatestValueSwap()
        {
            var tripleBuffer = new ZeroTripleBuffer(64);

            // 1. Initial render acquisition has no update
            var renderBuf = tripleBuffer.AcquireRenderBuffer(out bool hasUpdate);
            Assert.False(hasUpdate);

            // 2. Writer publishes snapshot into write buffer
            var writeBuf = tripleBuffer.GetWriteBuffer();
            writeBuf.Set(5, new ScadaValue(123.45, ScadaQuality.Good), 1000);
            tripleBuffer.PublishWrite();

            // 3. Reader acquires published snapshot
            renderBuf = tripleBuffer.AcquireRenderBuffer(out hasUpdate);
            Assert.True(hasUpdate);
            Assert.Equal(123.45, renderBuf.GetValue(5).AsDouble());

            // 4. Second read without new write yields no update
            renderBuf = tripleBuffer.AcquireRenderBuffer(out hasUpdate);
            Assert.False(hasUpdate);
        }

        [Fact]
        public async Task TelemetryThrottleQueue_CoalescesUpdates_WithoutAllocations()
        {
            var flushedSnapshots = new List<IScadaTag>();
            using var flushSignal = new SemaphoreSlim(0, 10);

            using var queue = new TelemetryThrottleQueue(batch =>
            {
                for (int i = 0; i < batch.Count; i++)
                {
                    flushedSnapshots.Add(batch[i]);
                }
                flushSignal.Release();
            }, intervalMs: 20);

            // Enqueue 5 rapid updates for the same tag: should coalesce into latest (40.0)
            queue.Enqueue(new ScadaTag("Tank.Level", 10.0));
            queue.Enqueue(new ScadaTag("Tank.Level", 20.0));
            queue.Enqueue(new ScadaTag("Tank.Level", 30.0));
            queue.Enqueue(new ScadaTag("Tank.Level", 40.0));

            // Wait for timer flush
            bool received = await flushSignal.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(received);

            Assert.Single(flushedSnapshots);
            Assert.Equal("Tank.Level", flushedSnapshots[0].TagPath);
            Assert.Equal(40.0, Convert.ToDouble(flushedSnapshots[0].Value));
        }
    }
}
