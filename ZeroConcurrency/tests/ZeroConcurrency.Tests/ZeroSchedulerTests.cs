using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ZeroPlatform.Concurrency.Tests
{
    public class ZeroSchedulerTests
    {
        private sealed class TestWorkItem : IZeroWorkItem
        {
            public bool Ran { get; private set; }
            public ManualResetEventSlim Event { get; } = new ManualResetEventSlim(false);

            public void Execute()
            {
                Ran = true;
                Event.Set();
            }
        }

        [Fact]
        public void UnsafeRun_Action_ExecutesSuccessfully()
        {
            using var doneEvent = new ManualResetEventSlim(false);
            bool executed = false;

            ZeroScheduler.UnsafeRun(() =>
            {
                executed = true;
                doneEvent.Set();
            });

            Assert.True(doneEvent.Wait(2000));
            Assert.True(executed);
        }

        [Fact]
        public void UnsafeRun_IZeroWorkItem_ExecutesZeroAllocation()
        {
            var item = new TestWorkItem();
            ZeroScheduler.UnsafeRun(item);

            Assert.True(item.Event.Wait(2000));
            Assert.True(item.Ran);
        }

        [Fact]
        public void DedicatedWorker_StartAndStop_ExecutesLoopCleanly()
        {
            int counter = 0;
            using var worker = new ZeroDedicatedWorker("TestWorker", () =>
            {
                if (counter < 10)
                {
                    Interlocked.Increment(ref counter);
                    return true;
                }
                return false;
            });

            worker.Start();
            Assert.True(worker.IsRunning);

            // Wait for counter to reach 10
            SpinWait.SpinUntil(() => Volatile.Read(ref counter) >= 10, 2000);
            Assert.True(counter >= 10);

            Assert.True(worker.Stop(1000));
            Assert.False(worker.IsRunning);
        }
    }
}
