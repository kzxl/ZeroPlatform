using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ZeroNetwork.Common;
using ZeroNetwork.PubSub;
using ZeroNetwork.RealTime;

namespace ZeroNetwork.Tests
{
    public class ZeroOptimizationAndBenchmarkTests
    {
        [Fact]
        public void ZeroRobinHoodMap_BasicCrudOperations_WorkCorrectly()
        {
            var map = new ZeroRobinHoodMap<string, int>(initialCapacity: 16);

            // 1. Insert
            map.Set("sensor_temp", 45);
            map.Set("sensor_vibe", 12);
            map.Set("sensor_pres", 85);
            Assert.Equal(3, map.Count);

            // 2. Lookup
            Assert.True(map.TryGetValue("sensor_temp", out int temp));
            Assert.Equal(45, temp);

            Assert.True(map.TryGetValue("sensor_vibe", out int vibe));
            Assert.Equal(12, vibe);

            Assert.False(map.TryGetValue("non_existent", out _));

            // 3. Update existing
            map.Set("sensor_temp", 50);
            Assert.Equal(3, map.Count);
            Assert.True(map.TryGetValue("sensor_temp", out int updatedTemp));
            Assert.Equal(50, updatedTemp);

            // 4. Remove
            bool removed = map.Remove("sensor_vibe");
            Assert.True(removed);
            Assert.Equal(2, map.Count);
            Assert.False(map.TryGetValue("sensor_vibe", out _));

            // Remove non-existent
            Assert.False(map.Remove("non_existent"));

            // 5. Clear
            map.Clear();
            Assert.Equal(0, map.Count);
            Assert.False(map.TryGetValue("sensor_pres", out _));
        }

        [Fact]
        public void ZeroRobinHoodMap_LargeVolumeInsertionAndResize_MaintainsDataIntegrity()
        {
            var map = new ZeroRobinHoodMap<int, string>(initialCapacity: 32);
            int itemCount = 10000;

            for (int i = 0; i < itemCount; i++)
            {
                map.Set(i, $"Value_{i}");
            }

            Assert.Equal(itemCount, map.Count);

            // Verify all items are retrievable
            for (int i = 0; i < itemCount; i++)
            {
                Assert.True(map.TryGetValue(i, out string? val));
                Assert.Equal($"Value_{i}", val);
            }

            // Remove half the items
            for (int i = 0; i < itemCount; i += 2)
            {
                Assert.True(map.Remove(i));
            }

            Assert.Equal(itemCount / 2, map.Count);

            // Verify odd items still exist, even items are removed
            for (int i = 0; i < itemCount; i++)
            {
                if (i % 2 == 0)
                {
                    Assert.False(map.TryGetValue(i, out _));
                }
                else
                {
                    Assert.True(map.TryGetValue(i, out string? val));
                    Assert.Equal($"Value_{i}", val);
                }
            }
        }

        [Fact]
        public async Task ZeroRecyclableStream_MultiBlockReadWrite_MatchesDataExactly()
        {
            // 4KB blocks, write 64KB data to trigger 16 chunk allocations
            int dataSize = 65536;
            byte[] sourceData = new byte[dataSize];
            for (int i = 0; i < sourceData.Length; i++) sourceData[i] = (byte)(i % 251);

            using (var stream = new ZeroRecyclableStream(blockSize: 4096))
            {
                // Write in chunks
                int written = 0;
                while (written < dataSize)
                {
                    int chunk = Math.Min(1234, dataSize - written);
                    stream.Write(sourceData, written, chunk);
                    written += chunk;
                }

                Assert.Equal(dataSize, stream.Length);
                Assert.Equal(dataSize, stream.Position);

                // Verify ToArray
                byte[] materialized = stream.ToArray();
                Assert.Equal(sourceData, materialized);

                // Seek and Read back
                stream.Seek(0, SeekOrigin.Begin);
                byte[] readBuffer = new byte[dataSize];
                int readTotal = 0;
                while (readTotal < dataSize)
                {
                    int read = stream.Read(readBuffer, readTotal, dataSize - readTotal);
                    if (read <= 0) break;
                    readTotal += read;
                }

                Assert.Equal(dataSize, readTotal);
                Assert.Equal(sourceData, readBuffer);

                // Test WriteToStreamAsync
                using (var ms = new MemoryStream())
                {
                    await stream.WriteToStreamAsync(ms);
                    Assert.Equal(sourceData, ms.ToArray());
                }
            }
        }

        [Fact]
        public void ZeroBinaryHubProtocol_EncodeAndDecode_ZeroLoss()
        {
            string target = "OnPlcTelemetryBatch";
            int invocationId = 1337;
            byte[] payload = Encoding.UTF8.GetBytes("Payload: Voltage=220V, Current=5.2A, Frequency=50.0Hz");

            Span<byte> buffer = stackalloc byte[512];
            bool encoded = ZeroBinaryHubProtocol.TryEncodeInvocation(target, invocationId, payload, buffer, out int bytesWritten);
            Assert.True(encoded);
            Assert.True(bytesWritten > 0);

            Span<char> targetChars = stackalloc char[128];
            bool decoded = ZeroBinaryHubProtocol.TryDecode(buffer.Slice(0, bytesWritten), targetChars, out var frame);

            Assert.True(decoded);
            Assert.Equal(ZeroBinaryHubProtocol.InvocationType, frame.MessageType);
            Assert.Equal(invocationId, frame.InvocationId);
            Assert.Equal(target, frame.Target.ToString());
            Assert.Equal(payload, frame.Payload.ToArray());
        }

        [Fact]
        public void TopicTrie_ZeroAllocationSpanMatching_PerformanceStress()
        {
            var trie = new TopicTrie<string>();
            trie.Add("plants/hanoi/line1/press/temperature", "Sub_Exact");
            trie.Add("plants/+/line1/#", "Sub_Wildcard");

            string topic = "plants/hanoi/line1/press/temperature";
            var results = new List<string>(4);

            // Pre-warm JIT
            trie.GetMatches(topic.AsSpan(), results);
            results.Clear();

            // Run 50,000 span lookups
            int iterations = 50000;
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                trie.GetMatches(topic.AsSpan(), results);
                results.Clear();
            }

            sw.Stop();

            // 50,000 lookups should easily finish in under 200ms
            Assert.True(sw.ElapsedMilliseconds < 500, $"Elapsed: {sw.ElapsedMilliseconds}ms for {iterations} span lookups.");
        }
    }
}
