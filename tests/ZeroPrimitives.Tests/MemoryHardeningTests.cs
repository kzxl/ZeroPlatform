using System;
using System.Buffers;
using System.Threading.Tasks;
using Xunit;
using ZeroPrimitives.Buffers;
using ZeroPrimitives.Memory;
using ZeroPrimitives.Simd;

namespace ZeroPrimitives.Tests
{
    public class MemoryHardeningTests
    {
        [Fact]
        public void NativeMemoryPool_RentAndReturn_DifferentBuckets_Succeeds()
        {
            var pool = new NativeMemoryPool(maxPerBucket: 16);

            // Rent small (4KB bucket 0)
            using (var block4k = pool.Rent(1000))
            {
                Assert.Equal(1000, block4k.Length);
                Assert.True(block4k.Capacity >= 4096);
                block4k.Span.Fill(42);
                Assert.Equal(42, block4k.Span[0]);
            }

            // Rent medium (64KB bucket 4)
            using (var block64k = pool.Rent(50_000))
            {
                Assert.Equal(50_000, block64k.Length);
                Assert.True(block64k.Capacity >= 65536);
                block64k.Span[0] = 99;
                Assert.Equal(99, block64k.Span[0]);
            }

            // Rent large (2MB bucket 9)
            using (var block2m = pool.Rent(2 * 1024 * 1024))
            {
                Assert.Equal(2 * 1024 * 1024, block2m.Length);
                Assert.True(block2m.Capacity >= 2 * 1024 * 1024);
            }

            // Verify bucket counts
            int b0 = NativeMemoryPool.SelectBucketIndex(1000);
            int b4 = NativeMemoryPool.SelectBucketIndex(50_000);
            int b9 = NativeMemoryPool.SelectBucketIndex(2 * 1024 * 1024);

            Assert.True(pool.GetAvailableCount(b0) >= 1);
            Assert.True(pool.GetAvailableCount(b4) >= 1);
            Assert.True(pool.GetAvailableCount(b9) >= 1);

            pool.Dispose();
            Assert.True(pool.IsDisposed);
        }

        [Fact]
        public void NativeMemoryPool_ConcurrentRentAndReturn_ThreadSafe()
        {
            var pool = new NativeMemoryPool(maxPerBucket: 64);

            Parallel.For(0, 200, i =>
            {
                int size = 4000 + (i * 500); // Varied sizes spanning 4KB to 128KB
                using NativeMemoryBlock block = pool.Rent(size);
                block.Span[0] = (byte)(i % 256);
                block.Span[block.Span.Length - 1] = 0xAA;

                Assert.Equal((byte)(i % 256), block.Span[0]);
                Assert.Equal(0xAA, block.Span[block.Span.Length - 1]);
            });

            pool.Dispose();
        }

        [Fact]
        public unsafe void NativeMemoryPool_RentLease_And_ZeroGcSteadyState_Succeeds()
        {
            using var pool = new NativeMemoryPool(maxPerBucket: 32);
            using var slab = new SlabAllocator(slabSize: 1024, initialSlabs: 16, maxSlabs: 64);
            using var structPool = new StructArenaPool(slotsPerBlock: 32);

            // Warm up all pools
            using (NativeMemoryLease warmLease = pool.RentLease(2048))
            {
                Assert.True(warmLease.IsValid);
                Assert.True(warmLease.Pointer != IntPtr.Zero);
                Assert.Equal(2048, warmLease.Length);
                Assert.Equal(4096, warmLease.Capacity);
                warmLease.Span.Fill(0x5A);
                Assert.Equal(0x5A, warmLease.ReadOnlySpan[0]);
                warmLease.Clear();
                Assert.Equal(0, warmLease.ReadOnlySpan[0]);
            }

            using (NativeMemoryBlock warmBlock = pool.Rent(2048))
            {
                warmBlock.Span[0] = 1;
            }

            using (var warmSlab = slab.RentScoped())
            {
                warmSlab.Span[0] = 2;
            }

            using (var warmStruct = structPool.Rent<long>())
            {
                warmStruct.Value = 123L;
            }

#if NET8_0_OR_GREATER
            long before = GC.GetAllocatedBytesForCurrentThread();
#endif
            for (int i = 0; i < 1000; i++)
            {
                using (NativeMemoryLease lease = pool.RentLease(2048))
                {
                    lease.RawPointer[0] = (byte)i;
                }

                using (NativeMemoryBlock block = pool.Rent(2048))
                {
                    block.Pointer[0] = (byte)i;
                }

                using (var slabScope = slab.RentScoped())
                {
                    slabScope.Pointer[0] = (byte)i;
                }

                using (var structLease = structPool.Rent<long>())
                {
                    structLease.Value = i;
                }
            }
#if NET8_0_OR_GREATER
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.Equal(0L, after - before);
#endif
        }

        [Fact]
        public void PagingArenaAllocator_AutoExpandsAcrossChunks_AndResets()
        {
            // Chunk size = 64KB for test purposes
            using var arena = new PagingArenaAllocator(chunkSize: 64 * 1024);

            Assert.Equal(64 * 1024, arena.TotalCapacity);

            // Allocate 3 blocks of 30KB each -> exceeds single 64KB chunk, forces auto-expansion
            Span<byte> b1 = arena.Allocate(30 * 1024);
            b1.Fill(1);

            Span<byte> b2 = arena.Allocate(30 * 1024);
            b2.Fill(2);

            Span<byte> b3 = arena.Allocate(30 * 1024);
            b3.Fill(3);

            // Arena should have expanded to at least 2 chunks (128KB)
            Assert.True(arena.TotalCapacity >= 128 * 1024);

            Assert.Equal(1, b1[0]);
            Assert.Equal(2, b2[0]);
            Assert.Equal(3, b3[0]);

            // Reset should rewind to head chunk
            arena.Reset();

            // Next allocation should overwrite from head chunk cleanly
            Span<byte> bNew = arena.Allocate(1000);
            bNew.Fill(99);
            Assert.Equal(99, bNew[0]);
        }

        [Fact]
        public void PagingArenaAllocator_Alignment_HonorsPowerOfTwo()
        {
            using var arena = new PagingArenaAllocator(chunkSize: 64 * 1024);

            unsafe
            {
                byte* p16 = arena.AllocatePointer(100, alignment: 16);
                Assert.True(((nuint)p16 & 15) == 0, "Address must be 16-byte aligned.");

                byte* p32 = arena.AllocatePointer(100, alignment: 32);
                Assert.True(((nuint)p32 & 31) == 0, "Address must be 32-byte aligned.");

                byte* p64 = arena.AllocatePointer(100, alignment: 64);
                Assert.True(((nuint)p64 & 63) == 0, "Address must be 64-byte aligned.");
            }
        }

        [Fact]
        public void SimdVector_AddSubtractMultiplyFma_CalculatesAccurately()
        {
            int n = 127; // Non-power of two to test SIMD vector remainder loop
            float[] a = new float[n];
            float[] b = new float[n];
            float[] c = new float[n];

            for (int i = 0; i < n; i++)
            {
                a[i] = i * 2.0f;
                b[i] = i * 0.5f;
                c[i] = 10.0f;
            }

            float[] dstAdd = new float[n];
            SimdVector.Add(dstAdd, a, b);
            for (int i = 0; i < n; i++)
            {
                Assert.Equal(a[i] + b[i], dstAdd[i]);
            }

            float[] dstSub = new float[n];
            SimdVector.Subtract(dstSub, a, b);
            for (int i = 0; i < n; i++)
            {
                Assert.Equal(a[i] - b[i], dstSub[i]);
            }

            float[] dstMul = new float[n];
            SimdVector.Multiply(dstMul, a, b);
            for (int i = 0; i < n; i++)
            {
                Assert.Equal(a[i] * b[i], dstMul[i]);
            }

            float[] dstFma = new float[n];
            SimdVector.MultiplyAdd(dstFma, a, b, c);
            for (int i = 0; i < n; i++)
            {
                Assert.Equal((a[i] * b[i]) + c[i], dstFma[i]);
            }
        }

        [Fact]
        public void SimdVector_ScaleAndClamp_ExecutesAccurately()
        {
            int n = 65;
            float[] src = new float[n];
            for (int i = 0; i < n; i++) src[i] = i - 30.0f; // Range: -30 to +34

            float[] scaled = new float[n];
            SimdVector.Scale(scaled, src, 3.0f);
            for (int i = 0; i < n; i++)
            {
                Assert.Equal(src[i] * 3.0f, scaled[i]);
            }

            float[] clamped = new float[n];
            SimdVector.Clamp(clamped, src, 0.0f, 20.0f);
            for (int i = 0; i < n; i++)
            {
                float expected = Math.Max(0.0f, Math.Min(20.0f, src[i]));
                Assert.Equal(expected, clamped[i]);
            }
        }

        [Fact]
        public void SimdVector_NormalizeAndQuantize_RoundTrips()
        {
            byte[] bytes = new byte[100];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i * 2);

            float[] floats = new float[100];
            SimdVector.NormalizeByteToFloat(bytes, floats);

            for (int i = 0; i < bytes.Length; i++)
            {
                Assert.InRange(floats[i], 0.0f, 1.0f);
                Assert.InRange(Math.Abs(floats[i] - (bytes[i] / 255.0f)), 0.0f, 0.001f);
            }

            byte[] restored = new byte[100];
            SimdVector.QuantizeFloatToByte(floats, restored);

            for (int i = 0; i < bytes.Length; i++)
            {
                Assert.Equal(bytes[i], restored[i]);
            }
        }

        [Fact]
        public void SimdVector_SequenceEqual_MatchesAcrossLengths()
        {
            byte[] a = new byte[256];
            byte[] b = new byte[256];

            for (int i = 0; i < 256; i++) { a[i] = (byte)i; b[i] = (byte)i; }

            Assert.True(SimdVector.SequenceEqual(a, b));

            // Tamper one byte at start, middle, and end
            b[0] = 255;
            Assert.False(SimdVector.SequenceEqual(a, b));
            b[0] = a[0];

            b[128] = 255;
            Assert.False(SimdVector.SequenceEqual(a, b));
            b[128] = a[128];

            b[255] = 0;
            Assert.False(SimdVector.SequenceEqual(a, b));
        }

        [Fact]
        public void SequenceSpanReader_ReadsMultiSegmentFragmentedBuffer_Accurately()
        {
            // Create a fragmented 3-segment buffer:
            // Segment 1: 3 bytes
            // Segment 2: 5 bytes
            // Segment 3: 4 bytes
            byte[] seg1 = new byte[] { 0x01, 0x02, 0x03 };
            byte[] seg2 = new byte[] { 0x04, 0x05, 0x06, 0x07, 0x08 };
            byte[] seg3 = new byte[] { 0x09, 0x0A, 0x0B, 0x0C };

            var segment1 = new MemorySegment(seg1);
            var segment2 = segment1.Append(seg2);
            var segment3 = segment2.Append(seg3);

            var sequence = new ReadOnlySequence<byte>(segment1, 0, segment3, seg3.Length);

            Assert.False(sequence.IsSingleSegment);
            Assert.Equal(12, sequence.Length);

            var reader = new SequenceSpanReader(sequence);

            Assert.Equal(12, reader.Remaining);
            Assert.Equal(0, reader.Consumed);

            // Read individual bytes across segments
            Assert.Equal(0x01, reader.ReadByte());
            Assert.Equal(0x02, reader.ReadByte());
            Assert.Equal(0x03, reader.ReadByte()); // End of seg1
            Assert.Equal(0x04, reader.ReadByte()); // Beginning of seg2

            // Read a 32-bit int little endian spanning seg2 and seg3:
            // seg2 has [0x05, 0x06, 0x07, 0x08]
            int val = reader.ReadInt32LittleEndian();
            int expected = 0x05 | (0x06 << 8) | (0x07 << 16) | (0x08 << 24);
            Assert.Equal(expected, val);

            // Remaining 4 bytes in seg3
            Assert.Equal(4, reader.Remaining);

            Span<byte> dest = new byte[4];
            reader.ReadBytes(dest);
            Assert.Equal(new byte[] { 0x09, 0x0A, 0x0B, 0x0C }, dest.ToArray());

            Assert.True(reader.End);
            Assert.Equal(12, reader.Consumed);
            Assert.Equal(0, reader.Remaining);
        }

        private sealed class MemorySegment : ReadOnlySequenceSegment<byte>
        {
            public MemorySegment(ReadOnlyMemory<byte> memory)
            {
                Memory = memory;
            }

            public MemorySegment Append(ReadOnlyMemory<byte> memory)
            {
                var next = new MemorySegment(memory)
                {
                    RunningIndex = RunningIndex + Memory.Length
                };
                Next = next;
                return next;
            }
        }
    }
}
