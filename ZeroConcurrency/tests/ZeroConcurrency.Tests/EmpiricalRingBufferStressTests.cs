using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroPlatform.Concurrency;

namespace ZeroConcurrency.Tests
{
    public class EmpiricalRingBufferStressTests
    {
        [Theory]
        [InlineData(-16)]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(7)]
        [InlineData(9)]
        [InlineData(15)]
        [InlineData(33)]
        [InlineData(100)]
        [InlineData(500)]
        [InlineData(1000)]
        [InlineData(1023)]
        [InlineData(1025)]
        [InlineData(4095)]
        public void ZeroNativeRingBuffer_Constructor_NonPowerOfTwo_StrictlyThrowsArgumentException(int invalidCapacity)
        {
            var ex = Assert.Throws<ArgumentException>(() => new ZeroNativeRingBuffer(capacityPowerOfTwo: invalidCapacity));
            Assert.Contains("power of two", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        [InlineData(64)]
        [InlineData(128)]
        [InlineData(256)]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(2048)]
        [InlineData(4096)]
        public void ZeroNativeRingBuffer_Constructor_PowerOfTwo_Succeeds(int validCapacity)
        {
            using var ring = new ZeroNativeRingBuffer(capacityPowerOfTwo: validCapacity);
            Assert.Equal(validCapacity, ring.Capacity);
            Assert.Equal(0, ring.Count);
            Assert.False(ring.IsDisposed);
        }

        [Fact]
        public void ZeroNativeRingBuffer_UndersizedDestinationSpan_ThrowsArgumentException_AndPreservesQueue()
        {
            using var ring = new ZeroNativeRingBuffer(capacityPowerOfTwo: 16);

            // Create payload 1: 100 bytes with pattern
            byte[] payload1 = new byte[100];
            for (int i = 0; i < payload1.Length; i++) payload1[i] = (byte)(i + 1);

            // Create payload 2: 200 bytes with pattern
            byte[] payload2 = new byte[200];
            for (int i = 0; i < payload2.Length; i++) payload2[i] = (byte)(i + 101);

            Assert.True(ring.TryWrite(payload1));
            Assert.True(ring.TryWrite(payload2));
            Assert.Equal(2, ring.Count);

            // Test various undersized destination spans: 0, 1, 50, 99
            int[] undersized = { 0, 1, 10, 50, 99 };
            foreach (int badSize in undersized)
            {
                byte[] tooSmall = new byte[badSize];
                var ex = Assert.Throws<ArgumentException>(() => ring.TryRead(tooSmall.AsSpan(), out _));
                Assert.Contains("smaller than payload block length", ex.Message);

                // Queue must remain intact
                Assert.Equal(2, ring.Count);
                Assert.True(ring.TryPeek(out int peekLength));
                Assert.Equal(100, peekLength);
            }

            // Now read with adequately sized buffer: exactly 100 bytes
            byte[] exactBuffer = new byte[100];
            Assert.True(ring.TryRead(exactBuffer.AsSpan(), out int bytesRead1));
            Assert.Equal(100, bytesRead1);
            Assert.Equal(payload1, exactBuffer);

            // Queue should now have 1 item left (payload2)
            Assert.Equal(1, ring.Count);
            Assert.True(ring.TryPeek(out int peekLength2));
            Assert.Equal(200, peekLength2);

            // Read payload2 with oversized buffer (e.g. 256 bytes)
            byte[] largeBuffer = new byte[256];
            Assert.True(ring.TryRead(largeBuffer.AsSpan(), out int bytesRead2));
            Assert.Equal(200, bytesRead2);
            for (int i = 0; i < 200; i++)
            {
                Assert.Equal(payload2[i], largeBuffer[i]);
            }

            Assert.Equal(0, ring.Count);
            Assert.False(ring.TryPeek(out _));
        }

        [Fact]
        public async Task ZeroNativeRingBuffer_HighContention_VariedAndNonPowerOfTwoPayloadLengths_NoLostData_NoDeadlock()
        {
            // Small ring capacity (64) forces high contention with continuous wrap-around and backpressure
            using var ring = new ZeroNativeRingBuffer(capacityPowerOfTwo: 64);
            const int totalItems = 50_000;

            // Non-power-of-two, prime, and varied payload lengths
            int[] variedLengths = { 1, 3, 7, 13, 27, 43, 89, 127, 241, 333, 513, 1001, 1999, 4097 };

            var errors = new ConcurrentBag<string>();
            var stopWatch = Stopwatch.StartNew();

            var producerTask = Task.Run(() =>
            {
                for (int seq = 0; seq < totalItems; seq++)
                {
                    int bodyLen = variedLengths[seq % variedLengths.Length];
                    int totalPacketLen = 8 + bodyLen; // 4 bytes seq + 4 bytes bodyLen + bodyLen
                    byte[] packet = new byte[totalPacketLen];

                    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(0, 4), seq);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(4, 4), bodyLen);

                    byte pattern = (byte)(seq & 0xFF);
                    for (int b = 0; b < bodyLen; b++)
                    {
                        packet[8 + b] = (byte)(pattern ^ (b & 0xFF));
                    }

                    var spin = new SpinWait();
                    while (!ring.TryWrite(packet))
                    {
                        spin.SpinOnce();
                        if (stopWatch.ElapsedMilliseconds > 25_000)
                        {
                            errors.Add($"Producer deadlock/timeout at seq {seq}");
                            return;
                        }
                    }
                }
            });

            var consumerTask = Task.Run(() =>
            {
                byte[] receiveBuffer = new byte[8192];
                for (int expectedSeq = 0; expectedSeq < totalItems; expectedSeq++)
                {
                    var spin = new SpinWait();
                    int bytesRead;
                    while (!ring.TryRead(receiveBuffer.AsSpan(), out bytesRead))
                    {
                        spin.SpinOnce();
                        if (stopWatch.ElapsedMilliseconds > 25_000)
                        {
                            errors.Add($"Consumer deadlock/timeout waiting for seq {expectedSeq}");
                            return;
                        }
                    }

                    int bodyLen = variedLengths[expectedSeq % variedLengths.Length];
                    int expectedPacketLen = 8 + bodyLen;

                    if (bytesRead != expectedPacketLen)
                    {
                        errors.Add($"Seq {expectedSeq}: Expected {expectedPacketLen} bytes, got {bytesRead}");
                        return;
                    }

                    int actualSeq = BitConverter.ToInt32(receiveBuffer, 0);
                    if (actualSeq != expectedSeq)
                    {
                        errors.Add($"Data reordered or lost! Expected seq {expectedSeq}, got {actualSeq}");
                        return;
                    }

                    int actualBodyLen = BitConverter.ToInt32(receiveBuffer, 4);
                    if (actualBodyLen != bodyLen)
                    {
                        errors.Add($"Seq {expectedSeq}: Body length mismatch: expected {bodyLen}, got {actualBodyLen}");
                        return;
                    }

                    byte pattern = (byte)(expectedSeq & 0xFF);
                    for (int b = 0; b < bodyLen; b++)
                    {
                        byte expectedByte = (byte)(pattern ^ (b & 0xFF));
                        if (receiveBuffer[8 + b] != expectedByte)
                        {
                            errors.Add($"Seq {expectedSeq}: Data corruption at byte {b}: expected {expectedByte}, got {receiveBuffer[8 + b]}");
                            return;
                        }
                    }
                }
            });

            await Task.WhenAll(producerTask, consumerTask).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Empty(errors);
            Assert.Equal(0, ring.Count);
        }

        [Fact]
        public async Task ZeroNativeRingBuffer_MultiProducer_Synchronized_Stress()
        {
            using var ring = new ZeroNativeRingBuffer(capacityPowerOfTwo: 128);
            const int producerCount = 4;
            const int itemsPerProducer = 15_000;
            const int totalItems = producerCount * itemsPerProducer;

            var syncLock = new object();
            var producerTasks = new Task[producerCount];
            var errors = new ConcurrentBag<string>();
            var stopWatch = Stopwatch.StartNew();

            for (int p = 0; p < producerCount; p++)
            {
                int producerId = p;
                producerTasks[p] = Task.Run(() =>
                {
                    for (int i = 0; i < itemsPerProducer; i++)
                    {
                        // Varied non-power-of-two lengths
                        int bodyLen = 17 + ((producerId + i) % 73);
                        byte[] packet = new byte[8 + bodyLen];
                        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(0, 4), producerId);
                        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(4, 4), i);

                        var spin = new SpinWait();
                        while (true)
                        {
                            lock (syncLock)
                            {
                                if (ring.TryWrite(packet))
                                    break;
                            }
                            spin.SpinOnce();
                            if (stopWatch.ElapsedMilliseconds > 25_000)
                            {
                                errors.Add($"Producer {producerId} timed out at item {i}");
                                return;
                            }
                        }
                    }
                });
            }

            var consumerCounts = new int[producerCount];
            var consumerTask = Task.Run(() =>
            {
                byte[] buf = new byte[256];
                for (int c = 0; c < totalItems; c++)
                {
                    var spin = new SpinWait();
                    int bytesRead;
                    while (!ring.TryRead(buf.AsSpan(), out bytesRead))
                    {
                        spin.SpinOnce();
                        if (stopWatch.ElapsedMilliseconds > 25_000)
                        {
                            errors.Add($"Consumer timed out at count {c}");
                            return;
                        }
                    }

                    int producerId = BitConverter.ToInt32(buf, 0);
                    int itemIdx = BitConverter.ToInt32(buf, 4);

                    if (producerId < 0 || producerId >= producerCount)
                    {
                        errors.Add($"Invalid producer id {producerId}");
                        return;
                    }

                    // Check per-producer sequential order
                    if (itemIdx != consumerCounts[producerId])
                    {
                        errors.Add($"Producer {producerId}: expected item {consumerCounts[producerId]}, got {itemIdx}");
                        return;
                    }
                    consumerCounts[producerId]++;
                }
            });

            var allTasks = new List<Task>(producerTasks) { consumerTask };
            await Task.WhenAll(allTasks).WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Empty(errors);

            for (int p = 0; p < producerCount; p++)
            {
                Assert.Equal(itemsPerProducer, consumerCounts[p]);
            }
            Assert.Equal(0, ring.Count);
        }

        [Fact]
        public void ZeroNativeRingBuffer_DisposeSafety_PendingSlotsRecycledAndMethodsThrow()
        {
            var ring = new ZeroNativeRingBuffer(capacityPowerOfTwo: 16);

            // Write 5 items
            for (int i = 0; i < 5; i++)
            {
                Assert.True(ring.TryWrite(new byte[] { 1, 2, 3 }));
            }
            Assert.Equal(5, ring.Count);

            // Dispose ring buffer with pending items
            ring.Dispose();
            Assert.True(ring.IsDisposed);

            // Second dispose must be idempotent
            ring.Dispose();

            // All operations must throw ObjectDisposedException
            Assert.Throws<ObjectDisposedException>(() => ring.TryWrite(new byte[] { 1 }));
            Assert.Throws<ObjectDisposedException>(() => ring.TryRead(new byte[10].AsSpan(), out _));
            Assert.Throws<ObjectDisposedException>(() => ring.TryPeek(out _));
        }

        [Fact]
        public void ZeroNativeRingBuffer_ZeroBytePayload_ThrowsArgumentOutOfRangeExceptionAndPreservesBuffer()
        {
            using var ring = new ZeroNativeRingBuffer(capacityPowerOfTwo: 8);

            // Attempting to write empty payload throws ArgumentOutOfRangeException from pool
            Assert.Throws<ArgumentOutOfRangeException>(() => ring.TryWrite(ReadOnlySpan<byte>.Empty));

            // Ring buffer state remains completely pristine
            Assert.Equal(0, ring.Count);
            Assert.False(ring.TryPeek(out _));

            // Valid write immediately after still succeeds
            Assert.True(ring.TryWrite(new byte[] { 42 }));
            Assert.Equal(1, ring.Count);

            Span<byte> dest = stackalloc byte[1];
            Assert.True(ring.TryRead(dest, out int read));
            Assert.Equal(1, read);
            Assert.Equal(42, dest[0]);
            Assert.Equal(0, ring.Count);
        }
    }
}
