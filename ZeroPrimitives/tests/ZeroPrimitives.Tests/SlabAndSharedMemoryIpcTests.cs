using System;
using System.Threading.Tasks;
using Xunit;
using ZeroPrimitives.Memory;

namespace ZeroPrimitives.Tests
{
    public class SlabAndSharedMemoryIpcTests
    {
        [Fact]
        public unsafe void SlabAllocator_RentAndReturn_PointerAndBlock_Succeeds()
        {
            const int blockSize = 1024;
            const int alignment = 64;
            using var slab = new SlabAllocator(slabSize: blockSize, initialSlabs: 4, maxSlabs: 16, alignment: alignment);

            Assert.Equal(4, slab.TotalBlocks);
            Assert.Equal(4, slab.AvailableBlocks);
            Assert.Equal(0, slab.ActiveBlocks);

            // 1. Rent pointer
            byte* ptr = slab.RentPointer();
            Assert.True(ptr != null);
            Assert.True(((nuint)ptr % (nuint)alignment) == 0, "Pointer must be 64-byte aligned.");
            Assert.Equal(1, slab.ActiveBlocks);
            Assert.Equal(3, slab.AvailableBlocks);

            ptr[0] = 0xAA;
            ptr[blockSize - 1] = 0xBB;
            slab.Return(ptr);

            Assert.Equal(0, slab.ActiveBlocks);
            Assert.Equal(4, slab.AvailableBlocks);

            // 2. RentScoped
            using (var scope = slab.RentScoped())
            {
                Assert.Equal(1, slab.ActiveBlocks);
                Assert.Equal(blockSize, scope.Length);
                scope.Span[0] = 42;
                Assert.Equal(42, scope.Span[0]);
            }
            Assert.Equal(0, slab.ActiveBlocks);

            // 3. Rent via NativeMemoryBlock
            using (var block = slab.Rent())
            {
                Assert.Equal(1, slab.ActiveBlocks);
                Assert.Equal(blockSize, block.Capacity);
                block.Span[0] = 77;
            }
            Assert.Equal(0, slab.ActiveBlocks);
        }

        [Fact]
        public unsafe void SlabAllocator_AutoExpansion_WhenExhausted_Succeeds()
        {
            using var slab = new SlabAllocator(slabSize: 512, initialSlabs: 2, maxSlabs: 16);
            Assert.Equal(2, slab.TotalBlocks);

            byte* p1 = slab.RentPointer();
            byte* p2 = slab.RentPointer();
            Assert.Equal(2, slab.ActiveBlocks);
            Assert.Equal(0, slab.AvailableBlocks);

            // Triggers growth
            byte* p3 = slab.RentPointer();
            Assert.Equal(3, slab.ActiveBlocks);
            int totalAfterGrowth = slab.TotalBlocks;
            Assert.True(totalAfterGrowth > 2);

            slab.Return(p1);
            slab.Return(p2);
            slab.Return(p3);

            Assert.Equal(0, slab.ActiveBlocks);
            Assert.Equal(totalAfterGrowth, slab.AvailableBlocks);
        }

        [Fact]
        public void SharedMemoryRingBuffer_WriteAndReadMessage_Basic_Succeeds()
        {
            string mapName = "ZeroIPC_Test_" + Guid.NewGuid().ToString("N");
            using var ring = SharedMemoryRingBuffer.CreateOrOpen(mapName, payloadCapacity: 64 * 1024);

            Assert.True(ring.Capacity >= 65536);
            Assert.Equal(0, ring.AvailableReadBytes);

            byte[] message = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            bool writeSuccess = ring.TryWriteMessage(message);
            Assert.True(writeSuccess);

            Assert.True(ring.AvailableReadBytes >= message.Length + 4);

            Span<byte> readBuffer = stackalloc byte[32];
            bool readSuccess = ring.TryReadMessage(readBuffer, out int bytesRead);

            Assert.True(readSuccess);
            Assert.Equal(message.Length, bytesRead);
            for (int i = 0; i < message.Length; i++)
            {
                Assert.Equal(message[i], readBuffer[i]);
            }

            Assert.Equal(0, ring.AvailableReadBytes);
        }

        [Fact]
        public void SharedMemoryRingBuffer_CircularWrapAround_Integrity()
        {
            string mapName = "ZeroIPC_Wrap_" + Guid.NewGuid().ToString("N");
            // 4KB capacity to easily trigger wrap-around
            using var ring = SharedMemoryRingBuffer.CreateOrOpen(mapName, payloadCapacity: 4096);

            byte[] payload = new byte[256];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i & 0xFF);
            }

            Span<byte> readDest = stackalloc byte[512];

            // Perform 50 writes and reads to wrap multiple times around the 4KB buffer
            for (int cycle = 0; cycle < 50; cycle++)
            {
                payload[0] = (byte)cycle;
                bool written = ring.TryWriteMessage(payload);
                Assert.True(written, $"Failed to write at cycle {cycle}");

                bool read = ring.TryReadMessage(readDest, out int readLen);
                Assert.True(read, $"Failed to read at cycle {cycle}");
                Assert.Equal(payload.Length, readLen);
                Assert.Equal((byte)cycle, readDest[0]);
                Assert.Equal(payload[100], readDest[100]);
            }
        }

        [Fact]
        public async Task SharedMemoryRingBuffer_ProducerConsumer_ConcurrentStream_Succeeds()
        {
            string mapName = "ZeroIPC_Conc_" + Guid.NewGuid().ToString("N");
            using var ring = SharedMemoryRingBuffer.CreateOrOpen(mapName, payloadCapacity: 64 * 1024);

            const int totalMessages = 2000;

            var producer = Task.Run(() =>
            {
                byte[] buffer = new byte[sizeof(int)];
                for (int i = 0; i < totalMessages; i++)
                {
                    BitConverter.GetBytes(i).CopyTo(buffer, 0);
                    while (!ring.TryWriteMessage(buffer))
                    {
                        // Spin wait until consumer drains
                        System.Threading.Thread.Yield();
                    }
                }
            });

            var consumer = Task.Run(() =>
            {
                byte[] readBuf = new byte[sizeof(int)];
                for (int i = 0; i < totalMessages; i++)
                {
                    int bytesRead;
                    while (!ring.TryReadMessage(readBuf, out bytesRead))
                    {
                        System.Threading.Thread.Yield();
                    }
                    int value = BitConverter.ToInt32(readBuf, 0);
                    Assert.Equal(i, value);
                }
            });

            await Task.WhenAll(producer, consumer);
            Assert.Equal(0, ring.AvailableReadBytes);
        }
    }
}
