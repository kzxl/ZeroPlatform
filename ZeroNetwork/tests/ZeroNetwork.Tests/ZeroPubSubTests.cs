using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroNetwork.PubSub;

namespace ZeroNetwork.Tests
{
    public class ZeroPubSubTests
    {
        private sealed class SensorReading
        {
            public string DeviceId { get; set; } = string.Empty;
            public double Value { get; set; }
        }

        [Fact]
        public void TopicTrie_ExactAndWildcardMatching_WorksCorrectly()
        {
            var trie = new TopicTrie<string>();

            trie.Add("plants/hanoi/line1/press", "Sub_Exact");
            trie.Add("plants/+/line1/press", "Sub_SingleWildcard");
            trie.Add("plants/hanoi/#", "Sub_MultiWildcard");

            // 1. Test topic matching all three
            var matches = new List<string>();
            trie.GetMatches("plants/hanoi/line1/press", matches);
            Assert.Contains("Sub_Exact", matches);
            Assert.Contains("Sub_SingleWildcard", matches);
            Assert.Contains("Sub_MultiWildcard", matches);
            Assert.Equal(3, matches.Count);

            // 2. Test topic matching single wildcard only
            matches.Clear();
            trie.GetMatches("plants/danang/line1/press", matches);
            Assert.Single(matches);
            Assert.Equal("Sub_SingleWildcard", matches[0]);

            // 3. Test topic matching multi wildcard only
            matches.Clear();
            trie.GetMatches("plants/hanoi/line2/vibration/axis_x", matches);
            Assert.Single(matches);
            Assert.Equal("Sub_MultiWildcard", matches[0]);

            // 4. Test topic matching nothing
            matches.Clear();
            trie.GetMatches("other/topic/path", matches);
            Assert.Empty(matches);
        }

        [Fact]
        public void TopicTrie_RemoveSubscriber_StopsMatching()
        {
            var trie = new TopicTrie<string>();
            trie.Add("scada/+/alarms", "AlarmSub1");
            trie.Add("scada/+/alarms", "AlarmSub2");

            var matches = new List<string>();
            trie.GetMatches("scada/motor1/alarms", matches);
            Assert.Equal(2, matches.Count);

            // Remove AlarmSub1
            bool removed = trie.Remove("scada/+/alarms", "AlarmSub1");
            Assert.True(removed);

            matches.Clear();
            trie.GetMatches("scada/motor1/alarms", matches);
            Assert.Single(matches);
            Assert.Equal("AlarmSub2", matches[0]);
        }

        [Theory]
        [InlineData("a/b/c", "a/b/c", true)]
        [InlineData("a/+/c", "a/b/c", true)]
        [InlineData("a/+/c", "a/b/d", false)]
        [InlineData("a/#", "a/b/c/d", true)]
        [InlineData("a/#", "a", true)]
        [InlineData("#", "any/arbitrary/topic", true)]
        [InlineData("+/b/+", "a/b/c", true)]
        [InlineData("+/b/+", "a/x/c", false)]
        public void TopicTrie_IsMatch_EvaluatesProperly(string pattern, string topic, bool expected)
        {
            Assert.Equal(expected, TopicTrie<string>.IsMatch(pattern, topic));
        }

        [Fact]
        public async Task InProcessZeroBus_TypedPublishSubscribe_DeliversMessage()
        {
            using (var bus = new InProcessZeroBus())
            {
                var received = new List<SensorReading>();
                var tcs = new TaskCompletionSource<bool>();

                using (var sub = bus.Subscribe<SensorReading>(reading =>
                {
                    received.Add(reading);
                    tcs.TrySetResult(true);
                }))
                {
                    var msg = new SensorReading { DeviceId = "PLC-01", Value = 42.5 };
                    await bus.PublishAsync(msg);

                    var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
                    Assert.Same(tcs.Task, completed);
                    Assert.Single(received);
                    Assert.Equal("PLC-01", received[0].DeviceId);
                    Assert.Equal(42.5, received[0].Value);
                }

                // After dispose of subscription, publish should no longer trigger
                await bus.PublishAsync(new SensorReading { DeviceId = "PLC-02", Value = 99.9 });
                Assert.Single(received);
            }
        }

        [Fact]
        public async Task InProcessZeroBus_TopicPublishSubscribe_DeliversPayload()
        {
            using (var bus = new InProcessZeroBus())
            {
                var received = new List<(string Topic, string Payload)>();
                var tcs = new TaskCompletionSource<bool>();

                using (var sub = bus.SubscribeTopic("plants/+/sensors/#", (topic, data) =>
                {
                    string text = Encoding.UTF8.GetString(data.ToArray());
                    received.Add((topic, text));
                    tcs.TrySetResult(true);
                }))
                {
                    byte[] payload = Encoding.UTF8.GetBytes("Temp=78.5C");
                    await bus.PublishTopicAsync("plants/hanoi/sensors/temp", payload);

                    var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
                    Assert.Same(tcs.Task, completed);
                    Assert.Single(received);
                    Assert.Equal("plants/hanoi/sensors/temp", received[0].Topic);
                    Assert.Equal("Temp=78.5C", received[0].Payload);
                }
            }
        }

        [Fact]
        public async Task IpcZeroBus_CrossChannelSharedMemory_DeliversPayload()
        {
            string mapName = $"ZeroIpcTest_{Guid.NewGuid():N}";

            using (var ipcBus = new IpcZeroBus(mapName, capacity: 64, slotSize: 4096, isListener: true))
            {
                var tcs = new TaskCompletionSource<(string Topic, string Text)>();

                ipcBus.SubscribeTopic("ipc/device/+", (topic, data) =>
                {
                    string text = Encoding.UTF8.GetString(data.ToArray());
                    tcs.TrySetResult((topic, text));
                });

                // Publish
                byte[] messageBytes = Encoding.UTF8.GetBytes("Status: RUNNING, RPM=1500");
                bool published = ipcBus.TryPublish("ipc/device/motorA", messageBytes);
                Assert.True(published);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(3000));
                Assert.Same(tcs.Task, completed);

                var result = await tcs.Task;
                Assert.Equal("ipc/device/motorA", result.Topic);
                Assert.Equal("Status: RUNNING, RPM=1500", result.Text);
            }
        }
    }
}
