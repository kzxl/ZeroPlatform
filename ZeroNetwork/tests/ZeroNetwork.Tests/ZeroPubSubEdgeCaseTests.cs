using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroNetwork.PubSub;

namespace ZeroNetwork.Tests
{
    public class ZeroPubSubEdgeCaseTests
    {
        [Fact]
        public void TopicTrie_InvalidPatterns_ThrowsExpectedExceptions()
        {
            var trie = new TopicTrie<string>();

            // Null or empty
            Assert.Throws<ArgumentNullException>(() => trie.Add(null!, "sub1"));
            Assert.Throws<ArgumentNullException>(() => trie.Add("", "sub1"));
            Assert.Throws<ArgumentNullException>(() => trie.Add("valid/topic", null!));

            // '#' not at the end of pattern
            Assert.Throws<ArgumentException>(() => trie.Add("sensors/#/temperature", "sub1"));
            Assert.Throws<ArgumentException>(() => trie.Add("#/all", "sub1"));
        }

        [Fact]
        public void TopicTrie_DeepHierarchyAndCombinations_MatchesCorrectly()
        {
            var trie = new TopicTrie<string>();

            trie.Add("a/b/c/d/e/f", "DeepExact");
            trie.Add("a/+/c/+/e/+", "DeepAlternating");
            trie.Add("+/b/c/d/e/f", "DeepFirstWildcard");
            trie.Add("a/b/c/#", "DeepMultiWildcard");

            var matches = new List<string>();
            trie.GetMatches("a/b/c/d/e/f", matches);

            Assert.Equal(4, matches.Count);
            Assert.Contains("DeepExact", matches);
            Assert.Contains("DeepAlternating", matches);
            Assert.Contains("DeepFirstWildcard", matches);
            Assert.Contains("DeepMultiWildcard", matches);

            // Shorter topic matching only DeepMultiWildcard
            matches.Clear();
            trie.GetMatches("a/b/c/d", matches);
            Assert.Single(matches);
            Assert.Equal("DeepMultiWildcard", matches[0]);

            // Topic that does not match (differs on literal segment 'y' instead of 'b')
            matches.Clear();
            trie.GetMatches("x/y/c/d/e/f", matches);
            Assert.Empty(matches);
        }

        [Fact]
        public void TopicTrie_ConcurrentAddRemoveLookup_ThreadSafe()
        {
            var trie = new TopicTrie<int>();
            int threadCount = 8;
            int opsPerThread = 500;
            var countdown = new CountdownEvent(threadCount);

            for (int t = 0; t < threadCount; t++)
            {
                int threadId = t;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    for (int i = 0; i < opsPerThread; i++)
                    {
                        string pattern = $"sensors/thread{threadId}/metric{i % 10}/#";
                        trie.Add(pattern, i);

                        var matches = new List<int>();
                        trie.GetMatches($"sensors/thread{threadId}/metric{i % 10}/value", matches);

                        if (i % 2 == 0)
                        {
                            trie.Remove(pattern, i);
                        }
                    }
                    countdown.Signal();
                });
            }

            Assert.True(countdown.Wait(TimeSpan.FromSeconds(10)));
        }

        [Fact]
        public async Task InProcessZeroBus_ExceptionHandlingModes_BehaveAsConfigured()
        {
            // 1. StopOnFirstException = false: Collects all exceptions into AggregateException
            var optionsCollect = new ZeroBusOptions { StopOnFirstException = false };
            using (var busCollect = new InProcessZeroBus(optionsCollect))
            {
                bool secondCalled = false;
                busCollect.Subscribe<string>(_ => throw new InvalidOperationException("First fault"));
                busCollect.Subscribe<string>(_ =>
                {
                    secondCalled = true;
                    throw new ArgumentException("Second fault");
                });

                var aggEx = await Assert.ThrowsAsync<AggregateException>(() => busCollect.PublishAsync("test").AsTask());
                Assert.Equal(2, aggEx.InnerExceptions.Count);
                Assert.True(secondCalled);
            }

            // 2. StopOnFirstException = true: Terminates immediately upon first exception
            var optionsStop = new ZeroBusOptions { StopOnFirstException = true };
            using (var busStop = new InProcessZeroBus(optionsStop))
            {
                bool secondCalled = false;
                busStop.Subscribe<string>(_ => throw new InvalidOperationException("Immediate fault"));
                busStop.Subscribe<string>(_ => secondCalled = true);

                await Assert.ThrowsAsync<InvalidOperationException>(() => busStop.PublishAsync("test").AsTask());
                Assert.False(secondCalled);
            }
        }

        [Fact]
        public async Task InProcessZeroBus_UnsubscribeWithinHandler_DoesNotCorruptOrThrow()
        {
            using (var bus = new InProcessZeroBus())
            {
                IZeroSubscription? sub = null;
                int invokeCount = 0;

                sub = bus.Subscribe<int>(num =>
                {
                    invokeCount++;
                    // Self-unsubscribe inside callback
                    sub?.Dispose();
                });

                await bus.PublishAsync(1);
                await bus.PublishAsync(2);
                await bus.PublishAsync(3);

                Assert.Equal(1, invokeCount);
            }
        }

        [Fact]
        public async Task InProcessZeroBus_HighThroughputBurst_ZeroLoss()
        {
            using (var bus = new InProcessZeroBus())
            {
                int messageCount = 10000;
                int receivedCount = 0;

                using (var sub = bus.Subscribe<int>(_ => Interlocked.Increment(ref receivedCount)))
                {
                    for (int i = 0; i < messageCount; i++)
                    {
                        await bus.PublishAsync(i);
                    }

                    Assert.Equal(messageCount, receivedCount);
                }
            }
        }

        [Fact]
        public async Task InProcessZeroBus_Disposed_ThrowsObjectDisposedException()
        {
            var bus = new InProcessZeroBus();
            bus.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => bus.PublishAsync("item").AsTask());
            Assert.Throws<ObjectDisposedException>(() => bus.Subscribe<string>(_ => { }));
            Assert.Throws<ObjectDisposedException>(() => bus.SubscribeTopic("test/#", (_, _) => { }));
        }

        [Fact]
        public void IpcZeroBus_OversizedPayload_ThrowsArgumentOutOfRangeException()
        {
            string mapName = $"IpcLimitTest_{Guid.NewGuid():N}";
            using (var ipc = new IpcZeroBus(mapName, capacity: 16, slotSize: 512, isListener: false))
            {
                byte[] oversized = new byte[1024]; // Exceeds 512B slot
                Assert.Throws<ArgumentOutOfRangeException>(() => ipc.TryPublish("topic", oversized));
            }
        }
    }
}
