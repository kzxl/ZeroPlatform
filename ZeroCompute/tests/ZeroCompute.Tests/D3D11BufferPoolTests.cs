using System;
using System.Threading.Tasks;
using Xunit;
using ZeroCompute.Core;
using ZeroCompute.Core.DirectX;
using ZeroTensor.Core;

namespace ZeroCompute.Tests
{
    [Collection("D3D11Hardware")]
    public class D3D11BufferPoolTests
    {
        [Fact]
        public void RentAndReturn_ReusesExactBufferInstance()
        {
            if (!ComputeDevice.IsGpuAvailable) return;

            var ctx = ComputeDevice.Gpu.D3D11Context;
            Assert.NotNull(ctx);
            var pool = ctx!.BufferPool;
            Assert.NotNull(pool);

            int count = 1024;
            int stride = 4;

            long hitsBefore = pool!.CacheHitCount;
            long rentsBefore = pool.TotalRentedCount;

            var buf1 = pool.Rent(count, stride, allowUav: true, allowSrv: true, allowCpuRead: false);
            Assert.NotNull(buf1);
            Assert.Equal(count, buf1.ElementCount);
            Assert.Equal(stride, buf1.ElementStride);

            pool.Return(buf1);
            Assert.True(pool.CachedBufferCount > 0);

            var buf2 = pool.Rent(count, stride, allowUav: true, allowSrv: true, allowCpuRead: false);
            Assert.Same(buf1, buf2); // Reused identical GPU buffer object
            Assert.True(pool.CacheHitCount >= hitsBefore + 1);
            Assert.True(pool.TotalRentedCount >= rentsBefore + 2);

            pool.Return(buf2);
        }

        [Fact]
        public void DifferentSizes_MaintainIsolatedBuckets()
        {
            if (!ComputeDevice.IsGpuAvailable) return;

            var pool = ComputeDevice.Gpu.BufferPool;
            Assert.NotNull(pool);

            var b512 = pool!.Rent(512, 4);
            var b1024 = pool.Rent(1024, 4);

            Assert.NotSame(b512, b1024);

            pool.Return(b512);
            pool.Return(b1024);

            var r1024 = pool.Rent(1024, 4);
            var r512 = pool.Rent(512, 4);

            Assert.Same(b1024, r1024);
            Assert.Same(b512, r512);

            pool.Return(r1024);
            pool.Return(r512);
        }

        [Fact]
        public void PooledDeviceTensor_ReturnsBufferToPoolOnDispose()
        {
            if (!ComputeDevice.IsGpuAvailable) return;

            var ctx = ComputeDevice.Gpu.D3D11Context!;
            var pool = ctx.BufferPool!;

            var shape = new TensorShape(64, 128); // 8192 elements
            int cachedBefore = pool.CachedBufferCount;

            Tensor<float> tensor = ctx.AllocateDeviceTensor<float>(shape, pooled: true);
            Assert.True(tensor.Storage is D3D11TensorStorage<float>);

            var devStorage = (D3D11TensorStorage<float>)tensor.Storage;
            var bufferRef = devStorage.Buffer;

            // Dispose tensor -> buffer returned to pool
            tensor.Dispose();

            Assert.True(pool.CachedBufferCount >= cachedBefore + 1);

            // Renting with same size reuses this buffer
            var reused = pool.Rent(shape.TotalElements, 4, allowUav: true, allowSrv: true, allowCpuRead: true);
            Assert.Same(bufferRef, reused);

            pool.Return(reused);
        }

        [Fact]
        public void ConsecutiveOperations_AchievePoolCacheHits()
        {
            if (!ComputeDevice.IsGpuAvailable) return;

            var ctx = ComputeDevice.Gpu.D3D11Context!;
            var pool = ctx.BufferPool!;

            var a = Tensor.Ones<float>(128, 128);
            var b = Tensor.Ones<float>(128, 128);
            var c = Tensor.Zeros<float>(128, 128);

            // First run: cold cache (allocates buffers into pool)
            ctx.Gemm(a, b, c);

            long hitsBefore = pool.CacheHitCount;

            // Second run: warm cache (reuses buffers from pool)
            ctx.Gemm(a, b, c);

            Assert.True(pool.CacheHitCount > hitsBefore);
        }

        [Fact]
        public void MultiThreaded_RentAndReturn_IsThreadSafe()
        {
            if (!ComputeDevice.IsGpuAvailable) return;

            var pool = ComputeDevice.Gpu.BufferPool!;

            Parallel.For(0, 16, i =>
            {
                int count = 256 * (1 + (i % 4));
                var buf = pool.Rent(count, 4);
                Assert.NotNull(buf);
                pool.Return(buf);
            });

            Assert.True(pool.CachedBufferCount > 0);
        }
    }
}
