using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroUI.Core.Collections;
using ZeroUI.Core.Runtime;

namespace ZeroUI.Core.Tests
{
    public class UiDispatcherTests
    {
        [Fact]
        public void Coalescing_DeduplicatesRapidUpdates_PerKey()
        {
            UiDispatcher.Reset();
            int executionCount = 0;
            int lastVal = 0;

            // Enqueue 1,000 rapid updates for the same key
            for (int i = 1; i <= 1000; i++)
            {
                int capture = i;
                UiDispatcher.EnqueueDirty("Tank1.Level", () =>
                {
                    executionCount++;
                    lastVal = capture;
                });
            }

            // Flush
            int flushed = UiDispatcher.FlushPending();

            Assert.Equal(1, flushed);
            Assert.Equal(1, executionCount);
            Assert.Equal(1000, lastVal);
        }

        [Fact]
        public void Send_OnSameThread_ExecutesDirectlyWithoutDeadlock()
        {
            UiDispatcher.Reset();
            UiDispatcher.Initialize(); // Set current thread as UI thread

            bool executed = false;
            UiDispatcher.Send(() => executed = true);

            Assert.True(executed);
            Assert.True(UiDispatcher.IsOnUiDispatcherThread);
            UiDispatcher.Reset();
        }
    }

    public class WorkerQueueTests
    {
        [Fact]
        public async Task ProcessItems_ConcurrencyAndCount_Succeeds()
        {
            int processed = 0;
            using var queue = new WorkerQueue<int>(
                async (item, ct) =>
                {
                    await Task.Delay(5, ct);
                    Interlocked.Increment(ref processed);
                },
                capacity: 100,
                concurrency: 2);

            for (int i = 0; i < 20; i++)
            {
                await queue.EnqueueAsync(i);
            }

            await queue.CompleteAsync();
            Assert.Equal(20, processed);
            Assert.Equal(20, queue.ProcessedCount);
        }

        [Fact]
        public async Task DropOldest_Backpressure_DropsExcessItems()
        {
            var slowGate = new SemaphoreSlim(0);
            using var queue = new WorkerQueue<int>(
                async (item, ct) =>
                {
                    await slowGate.WaitAsync(ct);
                },
                capacity: 5,
                backpressure: QueueBackpressureMode.DropOldest,
                concurrency: 1);

            // Enqueue more than capacity
            for (int i = 0; i < 20; i++)
            {
                queue.TryEnqueue(i);
            }

            // Release gate for pending
            slowGate.Release(20);
            await queue.CompleteAsync();

            Assert.True(queue.DroppedCount > 0, $"Expected dropped items > 0, got {queue.DroppedCount}");
        }
    }

    public class RingBufferTests
    {
        [Fact]
        public void PowerOfTwoCapacity_AndBitwiseModulo_WrapsCorrectly()
        {
            var rb = new RingBuffer<int>(5); // Rounded up to 8
            Assert.Equal(8, rb.Capacity);

            // Write 12 items (wraps over 8)
            for (int i = 0; i < 12; i++)
            {
                rb.Write(i);
            }

            Assert.Equal(8, rb.Count);
            Assert.True(rb.IsFull);

            // Oldest retained should be 4, newest should be 11
            Assert.Equal(4, rb.GetAt(0));
            Assert.Equal(11, rb.GetAt(7));

            Assert.True(rb.TryGetLatest(out int latest));
            Assert.Equal(11, latest);
        }

        [Fact]
        public void CopyTo_Span_ZeroAllocationRetrieval()
        {
            var rb = new RingBuffer<int>(4);
            rb.Write(10);
            rb.Write(20);
            rb.Write(30);

            Span<int> dest = stackalloc int[3];
            int copied = rb.CopyTo(dest);

            Assert.Equal(3, copied);
            Assert.Equal(10, dest[0]);
            Assert.Equal(20, dest[1]);
            Assert.Equal(30, dest[2]);
        }
    }

    public class EventBusTests
    {
        private record TestEvent(string Message);

        [Fact]
        public void SubscribeAndPublish_Synchronous_DeliversPayload()
        {
            var bus = new EventBus();
            string? received = null;

            using (bus.Subscribe<TestEvent>(e => received = e.Message))
            {
                bus.Publish(new TestEvent("MotorStarted"));
                Assert.Equal("MotorStarted", received);
            }

            // After dispose, no longer receives
            bus.Publish(new TestEvent("MotorStopped"));
            Assert.Equal("MotorStarted", received); // Still old value
        }

        [Fact]
        public async Task PublishAsync_DeliversToAsyncSubscribers()
        {
            var bus = new EventBus();
            int counter = 0;

            using (bus.SubscribeAsync<TestEvent>(async e =>
            {
                await Task.Yield();
                Interlocked.Increment(ref counter);
            }))
            {
                await bus.PublishAsync(new TestEvent("Async1"));
                await bus.PublishAsync(new TestEvent("Async2"));
            }

            Assert.Equal(2, counter);
        }
    }

    public class CommandBusTests
    {
        private record StartPumpCommand(string PumpId, int SpeedRpm) : IIndustrialCommand
        {
            public string CommandId => $"CMD_START_{PumpId}";
        }

        [Fact]
        public async Task Execute_FullPipeline_InterlockRejected()
        {
            var bus = new CommandBus();

            // Interlock: If Speed > 3000, reject as interlock trip
            bus.RegisterInterlock<StartPumpCommand>(cmd =>
                cmd.SpeedRpm > 3000 ? "Maximum safe speed is 3000 RPM." : null);

            bus.RegisterHandler<StartPumpCommand>((cmd, ct) =>
                Task.FromResult(CommandResult.Success($"Pump {cmd.PumpId} running.")));

            var result = await bus.ExecuteAsync(new StartPumpCommand("PUMP_101", 3500));

            Assert.False(result.IsSuccess);
            Assert.Equal(CommandPipelineStage.Interlock, result.FailedStage);
            Assert.Contains("Maximum safe speed", result.Message);
        }

        [Fact]
        public async Task Execute_PermissionDenied_ReturnsAuthDenied()
        {
            var bus = new CommandBus();
            bus.SetPermissionProvider(role => role == "Supervisor");

            bus.RegisterHandler<StartPumpCommand>((cmd, ct) =>
                Task.FromResult(CommandResult.Success()));

            var result = await bus.ExecuteAsync(new StartPumpCommand("PUMP_101", 1500), requiredRole: "Administrator");

            Assert.False(result.IsSuccess);
            Assert.Equal(CommandPipelineStage.Permission, result.FailedStage);
            Assert.Equal("AUTH_DENIED", result.ErrorCode);
        }

        [Fact]
        public async Task Execute_ValidCommand_Succeeds()
        {
            var bus = new CommandBus();
            bus.SetPermissionProvider(_ => true);

            bus.RegisterValidator<StartPumpCommand>(cmd =>
                cmd.SpeedRpm < 0 ? "Speed cannot be negative." : null);

            bus.RegisterHandler<StartPumpCommand>((cmd, ct) =>
                Task.FromResult(CommandResult.Success($"Pump {cmd.PumpId} started.")));

            var result = await bus.ExecuteAsync(new StartPumpCommand("PUMP_101", 1800), requiredRole: "Operator");

            Assert.True(result.IsSuccess);
            Assert.Equal(CommandPipelineStage.None, result.FailedStage);
            Assert.Contains("started", result.Message);
        }
    }

    public class StateStoreTests
    {
        [Fact]
        public void StateStore_SetAndSubscribe_FiresNotification()
        {
            var store = new StateStore();
            string? capturedKey = null;
            object? capturedOld = null;
            object? capturedNew = null;

            using var sub = store.Subscribe("Line1.State", (key, oldVal, newVal) =>
            {
                capturedKey = key;
                capturedOld = oldVal;
                capturedNew = newVal;
            });

            store.SetState("Line1.State", "RUNNING");

            Assert.Equal("Line1.State", capturedKey);
            Assert.Null(capturedOld);
            Assert.Equal("RUNNING", capturedNew);

            // Compare and swap
            bool swapped = store.CompareAndSwap("Line1.State", "RUNNING", "STOPPED");
            Assert.True(swapped);
            Assert.Equal("STOPPED", store.GetState<string>("Line1.State"));

            bool falseSwap = store.CompareAndSwap("Line1.State", "RUNNING", "PAUSED");
            Assert.False(falseSwap);
            Assert.Equal("STOPPED", store.GetState<string>("Line1.State"));
        }
    }
}
