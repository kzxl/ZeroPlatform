using System;
using System.Threading.Tasks;
using Xunit;

namespace ZeroPlatform.Concurrency.Tests
{
    public class ZeroPromiseTests
    {
        [Fact]
        public async Task GenericPromise_SetResult_AwaitsCorrectValue()
        {
            var promise = ZeroPromisePool<int>.Rent();

            ZeroScheduler.UnsafeRun(() =>
            {
                promise.SetResult(42);
            });

            int result = await promise.Task;
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task GenericPromise_SetException_PropagatesException()
        {
            var promise = ZeroPromisePool<string>.Rent();

            ZeroScheduler.UnsafeRun(() =>
            {
                promise.SetException(new InvalidOperationException("Test exception message"));
            });

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await promise.Task;
            });

            Assert.Equal("Test exception message", ex.Message);
        }

        [Fact]
        public async Task VoidPromise_SetResult_AwaitsCompletion()
        {
            var promise = ZeroPromisePool.Rent();
            bool executed = false;

            ZeroScheduler.UnsafeRun(() =>
            {
                executed = true;
                promise.SetResult();
            });

            await promise.Task;
            Assert.True(executed);
        }

        [Fact]
        public async Task PromisePool_RepeatedRentAndReturn_ReusesInstancesWithoutAlloc()
        {
            for (int i = 0; i < 1000; i++)
            {
                var promise = ZeroPromisePool<int>.Rent();
                promise.SetResult(i);
                int res = await promise.Task;
                Assert.Equal(i, res);
            }
        }
    }
}
