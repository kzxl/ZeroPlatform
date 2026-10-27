using System;
using System.Threading.Tasks;
using Xunit;
using ZeroPrimitives.Memory;
using ZeroPrimitives.Simd;

namespace ZeroPrimitives.Tests
{
    public unsafe class MemoryAndSimdTests
    {
        [Fact]
        public void ArenaAllocator_AllocatesAndBumpsOffset()
        {
            using var arena = new ArenaAllocator(1024);
            Assert.Equal(1024, arena.Capacity);
            Assert.Equal(0, arena.UsedBytes);
            Assert.Equal(1024, arena.RemainingBytes);

            Span<byte> span1 = arena.Allocate(100, alignment: 16);
            Assert.Equal(100, span1.Length);
            Assert.True(arena.UsedBytes >= 100);

            // Write test data
            span1.Fill(0xAB);
            Assert.Equal(0xAB, span1[0]);
            Assert.Equal(0xAB, span1[99]);

            // Allocate second block
            Span<byte> span2 = arena.Allocate(200, alignment: 16);
            span2.Fill(0xCD);

            Assert.Equal(0xCD, span2[0]);
            Assert.Equal(0xAB, span1[99]); // Ensure no overwrite

            // Single-cycle Reset
            arena.Reset();
            Assert.Equal(0, arena.UsedBytes);
            Assert.Equal(1024, arena.RemainingBytes);

            // Re-allocate overwrites cleanly
            Span<byte> span3 = arena.Allocate(50);
            span3.Fill(0xEF);
            Assert.Equal(0xEF, span3[0]);
        }

        [Fact]
        public void ArenaAllocator_ExceedCapacity_ThrowsOutOfMemory()
        {
            using var arena = new ArenaAllocator(128);
            arena.Allocate(100);

            Assert.Throws<OutOfMemoryException>(() =>
            {
                arena.Allocate(50);
            });
        }

        [Fact]
        public void SlabAllocator_RentAndRecycle_WorksCorrectly()
        {
            using var slabs = new SlabAllocator(slabSize: 1024, initialSlabs: 2, maxSlabs: 4);
            Assert.Equal(1024, slabs.SlabSize);
            Assert.Equal(2, slabs.AllocatedSlabs);
            Assert.Equal(2, slabs.AvailableSlabs);

            // Rent slab 1
            NativeMemoryBlock block1 = slabs.Rent(500);
            Assert.True(block1.Pointer != null);
            Assert.Equal(500, block1.Length);
            Assert.Equal(1024, block1.Capacity);
            Assert.Equal(1, slabs.AvailableSlabs);

            block1.Span[0] = 42;
            Assert.Equal(42, block1.Span[0]);

            // Rent slab 2
            NativeMemoryBlock block2 = slabs.Rent(1000);
            Assert.Equal(0, slabs.AvailableSlabs);

            // Dispose block 1 -> recycles back to pool
            block1.Dispose();
            Assert.Equal(1, slabs.AvailableSlabs);

            // Rent again -> gets recycled block
            NativeMemoryBlock block3 = slabs.Rent(300);
            Assert.Equal(300, block3.Length);
            Assert.Equal(0, slabs.AvailableSlabs);

            block2.Dispose();
            block3.Dispose();
            Assert.Equal(2, slabs.AvailableSlabs);
        }

        [Fact]
        public void SlabAllocator_ConcurrentRentAndReturn_ThreadSafe()
        {
            using var slabs = new SlabAllocator(slabSize: 4096, initialSlabs: 8, maxSlabs: 32);

            Parallel.For(0, 100, i =>
            {
                using NativeMemoryBlock block = slabs.Rent(2048);
                block.Span.Fill((byte)(i % 256));
                Assert.Equal((byte)(i % 256), block.Span[0]);
            });

            Assert.True(slabs.AvailableSlabs > 0);
        }

        [Fact]
        public void SimdOps_CopyNonTemporal_ProducesExactCopy()
        {
            byte[] src = new byte[1024];
            byte[] dst = new byte[1024];

            var rnd = new Random(42);
            rnd.NextBytes(src);

            SimdOps.CopyNonTemporal(src, dst);

            Assert.Equal(src, dst);
        }

        [Fact]
        public void SimdOps_MinMax_CalculatesAccurately()
        {
            byte[] data = new byte[512];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(50 + (i % 100));
            }
            data[123] = 5;   // Min
            data[345] = 240; // Max

            SimdOps.MinMax(data, out byte min, out byte max);

            Assert.Equal(5, min);
            Assert.Equal(240, max);
        }

        [Fact]
        public void SimdOps_DotProduct_CalculatesAccurately()
        {
            float[] a = { 1.5f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f };
            float[] b = { 0.5f, 1.0f, 2.0f, 0.5f, 1.0f, 2.0f, 0.5f, 1.0f, 2.0f };

            float expected = 0f;
            for (int i = 0; i < a.Length; i++)
                expected += a[i] * b[i];

            float result = SimdOps.DotProduct(a, b);
            Assert.InRange(Math.Abs(result - expected), 0f, 0.001f);
        }

        [Fact]
        public void SimdColorConverter_Yuv420pToRgb_ConvertsPixelValues()
        {
            int width = 4;
            int height = 2;

            byte[] yPlane = new byte[width * height];
            byte[] uPlane = new byte[(width / 2) * (height / 2)];
            byte[] vPlane = new byte[(width / 2) * (height / 2)];
            byte[] rgbDest = new byte[width * height * 3];

            // White color in YUV: Y = 235, U = 128, V = 128
            yPlane.AsSpan().Fill((byte)235);
            uPlane.AsSpan().Fill((byte)128);
            vPlane.AsSpan().Fill((byte)128);


            SimdColorConverter.Yuv420pToRgb(yPlane, uPlane, vPlane, rgbDest, width, height, isBgr: false);

            // White should yield close to (255, 255, 255)
            Assert.InRange(rgbDest[0], 250, 255); // R
            Assert.InRange(rgbDest[1], 250, 255); // G
            Assert.InRange(rgbDest[2], 250, 255); // B
        }
    }
}
