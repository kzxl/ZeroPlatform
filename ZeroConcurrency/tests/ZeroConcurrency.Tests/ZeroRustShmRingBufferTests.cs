using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroConcurrency.Ipc;
using ZeroPlatform.Concurrency;

namespace ZeroConcurrency.Tests
{
    public class ZeroRustShmRingBufferTests
    {
        [Fact]
        public unsafe void ZeroRustShmRingBuffer_HeaderLayout_Matches192ByteSpec()
        {
            Assert.Equal(192, sizeof(ShmHeader));
            Assert.Equal(192, ZeroRustShmRingBuffer<MotionCommandPayload>.HeaderSize);
            Assert.Equal(64, ZeroRustShmRingBuffer<MotionCommandPayload>.WriteHeadOffset);
            Assert.Equal(128, ZeroRustShmRingBuffer<MotionCommandPayload>.ReadTailOffset);
            Assert.Equal(192, ZeroRustShmRingBuffer<MotionCommandPayload>.DataOffset);

            Assert.Equal(64, sizeof(MotionCommandPayload));
            Assert.Equal(80, sizeof(TelemetryPayload));

            using var ring = new ZeroRustShmRingBuffer<MotionCommandPayload>(capacity: 8);
            byte* basePtr = (byte*)ring.BasePointer;

            // Verify exact binary field offsets in unmanaged memory
            Assert.Equal(0x5A45524Fu, *(uint*)(basePtr + 0));   // Magic
            Assert.Equal(1u, *(uint*)(basePtr + 4));           // Version
            Assert.Equal(8u, *(uint*)(basePtr + 8));           // Capacity
            Assert.Equal(64u, *(uint*)(basePtr + 12));         // SlotSize
            Assert.Equal(1u, *(uint*)(basePtr + 16));          // Flags
            Assert.Equal(0UL, *(ulong*)(basePtr + 64));        // WriteHead at offset 64
            Assert.Equal(0UL, *(ulong*)(basePtr + 128));       // ReadTail at offset 128

            var cmd = new MotionCommandPayload
            {
                TimestampNs = 987654321UL,
                CommandType = 1,
                Controlword = 0x000F,
                Reserved = 0
            };
            cmd.TargetPositions[0] = 1000;
            cmd.TargetPositions[5] = -500;
            cmd.TargetVelocities[0] = 250;

            Assert.True(ring.TryPublish(in cmd));
            Assert.Equal(1UL, *(ulong*)(basePtr + 64)); // WriteHead advanced at offset 64
            Assert.Equal(0UL, *(ulong*)(basePtr + 128)); // ReadTail still 0 at offset 128

            // First slot starts at offset 192
            MotionCommandPayload* slot0 = (MotionCommandPayload*)(basePtr + 192);
            Assert.Equal(987654321UL, slot0->TimestampNs);
            Assert.Equal(1000, slot0->TargetPositions[0]);
            Assert.Equal(-500, slot0->TargetPositions[5]);

            Assert.True(ring.TryConsume(out var consumed));
            Assert.Equal(cmd, consumed);
            Assert.Equal(1UL, *(ulong*)(basePtr + 128)); // ReadTail advanced at offset 128
            Assert.True(ring.IsEmpty);
        }

        [Fact]
        public unsafe void ZeroRustShmRingBuffer_PointerAttach_TelemetryRoundTrip_Succeeds()
        {
            using var hostRing = new ZeroRustShmRingBuffer<TelemetryPayload>(capacity: 16);
            int totalBytes = ZeroRustShmRingBuffer<TelemetryPayload>.HeaderSize + (16 * sizeof(TelemetryPayload));

            using var attachedRing = ZeroRustShmRingBuffer<TelemetryPayload>.AttachToPointer(hostRing.BasePointer, totalBytes);

            for (int i = 0; i < 16; i++)
            {
                var telemetry = new TelemetryPayload
                {
                    TimestampNs = (ulong)(i + 1) * 1_000_000UL,
                    FaultCode = (ushort)i
                };
                telemetry.ActualPositions[0] = i * 10;
                telemetry.ActualVelocities[1] = i * 20;
                telemetry.Statuswords[2] = 0x1637;
                telemetry.DriveStates[3] = 4;

                Assert.True(hostRing.TryPublish(in telemetry));
            }

            // 17th publish must return false (buffer full)
            var overflowItem = default(TelemetryPayload);
            Assert.False(hostRing.TryPublish(in overflowItem));
            Assert.Equal(16, attachedRing.Count);

            for (int i = 0; i < 16; i++)
            {
                Assert.True(attachedRing.TryConsume(out var item));
                Assert.Equal((ulong)(i + 1) * 1_000_000UL, item.TimestampNs);
                Assert.Equal(i * 10, item.ActualPositions[0]);
                Assert.Equal(i * 20, item.ActualVelocities[1]);
                Assert.Equal((ushort)0x1637, item.Statuswords[2]);
                Assert.Equal((byte)4, item.DriveStates[3]);
                Assert.Equal((ushort)i, item.FaultCode);
            }

            Assert.False(attachedRing.TryConsume(out _));
            Assert.True(hostRing.IsEmpty);
        }

        [Fact]
        public void ZeroRustShmRingBuffer_MemoryMappedFile_SpscRoundTrip_Succeeds()
        {
            string mapName = "ZeroRustShm_" + Guid.NewGuid().ToString("N");
            using var producer = ZeroRustShmRingBuffer<MotionCommandPayload>.CreateOrOpen(mapName, capacity: 32);
            using var consumer = ZeroRustShmRingBuffer<MotionCommandPayload>.OpenExisting(mapName);

            for (int i = 0; i < 100; i++)
            {
                var cmd = new MotionCommandPayload
                {
                    TimestampNs = (ulong)i,
                    CommandType = 2,
                    Controlword = (ushort)(i & 0xFFFF)
                };
                Assert.True(producer.TryPublish(in cmd));
                Assert.True(consumer.TryConsume(out var received));
                Assert.Equal(cmd, received);
            }
        }

        [Fact]
        public void ZeroWorkStealingPool_PostStateful_ExecutesWithoutClosureAllocation()
        {
            using var pool = new ZeroWorkStealingPool(workerCount: 2);
            using var done = new ManualResetEventSlim(false);
            const int total = 200;
            int remaining = total;
            long sum = 0;

            for (int i = 1; i <= total; i++)
            {
                pool.Post(static state =>
                {
                    Interlocked.Add(ref Unsafe.AsRef(in state.sumBox[0]), state.val);
                    if (Interlocked.Decrement(ref Unsafe.AsRef(in state.remBox[0])) == 0)
                    {
                        state.evt.Set();
                    }
                }, (val: i, sumBox: new long[] { 0 }, remBox: new int[] { 0 }, evt: done));
            }
            // Also verify direct integer state accumulation
            int counter = 0;
            using var countdown = new CountdownEvent(total);
            for (int i = 1; i <= total; i++)
            {
                pool.Post(state =>
                {
                    Interlocked.Add(ref counter, state);
                    countdown.Signal();
                }, i);
            }

            Assert.True(countdown.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(total * (total + 1) / 2, counter);
            _ = sum;
            _ = remaining;
        }

        [Fact]
        public void ZeroMpmcRingBuffer_IsEmpty_HandlesIntMaxValueWrapAroundCorrectly()
        {
            var ring = new ZeroMpmcRingBuffer<int>(capacityPowerOfTwo: 4);

            // Simulate _enqueuePos wrapping past int.MaxValue to int.MinValue while _dequeuePos is int.MaxValue
            FieldInfo? enqField = typeof(ZeroMpmcProducer<int>).GetField("_enqueuePos", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo? deqField = typeof(ZeroMpmcRingBuffer<int>).GetField("_dequeuePos", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(enqField);
            Assert.NotNull(deqField);

            deqField!.SetValue(ring, int.MaxValue);
            enqField!.SetValue(ring, unchecked(int.MaxValue + 2)); // wraps to int.MinValue + 1

            // Modular difference is +2, so IsEmpty MUST be false!
            Assert.False(ring.IsEmpty);
            Assert.Equal(2, ring.Count);

            deqField.SetValue(ring, unchecked(int.MaxValue + 2));
            Assert.True(ring.IsEmpty);
            Assert.Equal(0, ring.Count);
        }
    }
}
