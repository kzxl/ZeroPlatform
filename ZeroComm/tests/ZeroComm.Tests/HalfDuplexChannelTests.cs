using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroComm.Core.Channels;

namespace ZeroComm.Tests
{
    public class HalfDuplexChannelTests
    {
        [Fact]
        public async Task ExecuteRequestAsync_Success()
        {
            using var channel = new HalfDuplexChannel<byte[]>();

            var executeTask = channel.ExecuteRequestAsync(
                token => Task.CompletedTask,
                timeoutMs: 1000);

            Assert.True(channel.HasPendingRequest);

            byte[] expectedResponse = new byte[] { 1, 2, 3, 4 };
            bool set = channel.TrySetResponse(expectedResponse);

            Assert.True(set);
            byte[] actual = await executeTask;
            Assert.Equal(expectedResponse, actual);
            Assert.False(channel.HasPendingRequest);
        }

        [Fact]
        public async Task ExecuteRequestAsync_Timeout_ThrowsTimeoutException()
        {
            using var channel = new HalfDuplexChannel<byte[]>();

            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await channel.ExecuteRequestAsync(
                    token => Task.CompletedTask,
                    timeoutMs: 50);
            });

            Assert.False(channel.HasPendingRequest);
        }

        [Fact]
        public async Task ExecuteRequestAsync_FaultPending_ThrowsException()
        {
            using var channel = new HalfDuplexChannel<byte[]>();

            var executeTask = channel.ExecuteRequestAsync(
                token => Task.CompletedTask,
                timeoutMs: 1000);

            channel.FaultPending(new IOException("Connection lost."));

            await Assert.ThrowsAsync<IOException>(async () => await executeTask);
            Assert.False(channel.HasPendingRequest);
        }

        [Fact]
        public async Task ExecuteRequestAsync_SerializesConcurrentRequests()
        {
            using var channel = new HalfDuplexChannel<int>();

            int firstCompleted = 0;
            int secondCompleted = 0;

            var task1 = channel.ExecuteRequestAsync(async token =>
            {
                await Task.Delay(20, token);
            }, 1000);

            var task2 = channel.ExecuteRequestAsync(async token =>
            {
                await Task.Delay(10, token);
            }, 1000);

            // Channel only has 1 pending at a time because task2 is waiting on gate
            channel.TrySetResponse(100);
            firstCompleted = await task1;

            channel.TrySetResponse(200);
            secondCompleted = await task2;

            Assert.Equal(100, firstCompleted);
            Assert.Equal(200, secondCompleted);
        }
    }
}
