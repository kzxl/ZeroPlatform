using System;
using System.Collections.Generic;
using Xunit;
using ZeroStorage.Core.Gorilla;

namespace ZeroStorage.Tests
{
    public class GorillaTests
    {
        [Fact]
        public void Gorilla_SinglePoint_RoundTrip()
        {
            var points = new List<TimeSeriesPoint>
            {
                new TimeSeriesPoint(1700000000000L, 42.5)
            };

            byte[] compressed = GorillaEncoder.Encode(points);
            var decoded = GorillaDecoder.Decode(compressed);

            Assert.Single(decoded);
            Assert.Equal(points[0].TimestampMs, decoded[0].TimestampMs);
            Assert.Equal(points[0].Value, decoded[0].Value);
        }

        [Fact]
        public void Gorilla_ConstantSensorData_CompressesExtremelyWellAndRestoresExactly()
        {
            int n = 1000;
            long startTime = 1700000000000L;
            var points = new List<TimeSeriesPoint>(n);

            // Simulating a steady pressure sensor: 1 reading every 100ms with value 101.325 kPa
            for (int i = 0; i < n; i++)
            {
                points.Add(new TimeSeriesPoint(startTime + i * 100L, 101.325));
            }

            byte[] compressed = GorillaEncoder.Encode(points);
            var decoded = GorillaDecoder.Decode(compressed);

            Assert.Equal(n, decoded.Count);
            for (int i = 0; i < n; i++)
            {
                Assert.Equal(points[i].TimestampMs, decoded[i].TimestampMs);
                Assert.Equal(points[i].Value, decoded[i].Value);
            }

            // Raw uncompressed size: 1000 * 16 bytes = 16,000 bytes.
            // With Gorilla, 1000 points of DOD=0 and XOR=0 should be < 500 bytes (over 30x compression!)
            Assert.True(compressed.Length < 600, $"Compressed size too large: {compressed.Length} bytes");
        }

        [Fact]
        public void Gorilla_DynamicVaryingData_RestoresExactBitValues()
        {
            int n = 500;
            long startTime = 1700000000000L;
            var points = new List<TimeSeriesPoint>(n);

            var rand = new Random(12345);
            long currTime = startTime;
            double currVal = 25.0;

            for (int i = 0; i < n; i++)
            {
                // Jittery sampling interval: 90ms to 110ms
                currTime += 90 + rand.Next(21);
                // Random temperature walk
                currVal += (rand.NextDouble() - 0.49) * 0.1;
                points.Add(new TimeSeriesPoint(currTime, currVal));
            }

            byte[] compressed = GorillaEncoder.Encode(points);
            var decoded = GorillaDecoder.Decode(compressed);

            Assert.Equal(n, decoded.Count);
            for (int i = 0; i < n; i++)
            {
                Assert.Equal(points[i].TimestampMs, decoded[i].TimestampMs);
                Assert.Equal(points[i].Value, decoded[i].Value);
            }
        }
    }
}
