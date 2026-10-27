using System;
using Xunit;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Tests
{
    public class ZeroOffHeapTelemetrySeriesTests
    {
        [Fact]
        public void PushAndReadLatest_Sequential_ReturnsExactSamplesInOrder()
        {
            using var series = new ZeroOffHeapTelemetrySeries(capacity: 100);

            for (int i = 0; i < 10; i++)
            {
                series.Push(timestampUtcMs: 1000 + i, value: i * 2.5);
            }

            Assert.Equal(10, series.Count);

            Span<double> timestamps = stackalloc double[10];
            Span<double> values = stackalloc double[10];

            int read = series.ReadLatest(timestamps, values);

            Assert.Equal(10, read);
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(1000 + i, timestamps[i]);
                Assert.Equal(i * 2.5, values[i]);
            }
        }

        [Fact]
        public void SlidingWindow_WrapsAroundCapacity_OverwritesOldestCorrectly()
        {
            const int cap = 5;
            using var series = new ZeroOffHeapTelemetrySeries(capacity: cap);

            // Push 8 items into capacity of 5 -> items 0, 1, 2 should be overwritten
            for (int i = 0; i < 8; i++)
            {
                series.Push(100 + i, i * 10.0);
            }

            Assert.Equal(5, series.Count);

            Span<double> ts = stackalloc double[5];
            Span<double> val = stackalloc double[5];

            int read = series.ReadLatest(ts, val);
            Assert.Equal(5, read);

            // Expect items 3, 4, 5, 6, 7
            for (int i = 0; i < 5; i++)
            {
                int expectedIdx = 3 + i;
                Assert.Equal(100 + expectedIdx, ts[i]);
                Assert.Equal(expectedIdx * 10.0, val[i]);
            }
        }

        [Fact]
        public void Clear_ResetsCount_AllowsReusingBuffer()
        {
            using var series = new ZeroOffHeapTelemetrySeries(capacity: 10);
            series.Push(1.0, 10.0);
            series.Push(2.0, 20.0);
            Assert.Equal(2, series.Count);

            series.Clear();
            Assert.Equal(0, series.Count);

            Span<double> ts = stackalloc double[5];
            Span<double> val = stackalloc double[5];
            Assert.Equal(0, series.ReadLatest(ts, val));

            series.Push(99.0, 999.0);
            Assert.Equal(1, series.Count);
        }

        [Fact]
        public void Dispose_ReleasesMemoryBlock_ThrowsOnAccess()
        {
            var series = new ZeroOffHeapTelemetrySeries(capacity: 10);
            series.Push(1.0, 2.0);
            series.Dispose();

            Assert.Throws<ObjectDisposedException>(() => series.Push(3.0, 4.0));
            Assert.Throws<ObjectDisposedException>(() =>
            {
                Span<double> ts = stackalloc double[1];
                Span<double> val = stackalloc double[1];
                series.ReadLatest(ts, val);
            });
        }

        [Fact]
        public void StressTest_100kPushes_ExecutesUnder10ms()
        {
            using var series = new ZeroOffHeapTelemetrySeries(capacity: 2048);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < 100_000; i++)
            {
                series.Push(i, i * 0.1);
            }
            sw.Stop();

            Assert.Equal(2048, series.Count);
            Assert.True(sw.ElapsedMilliseconds < 50, $"100k pushes took {sw.ElapsedMilliseconds}ms, expected < 50ms");

            Span<double> ts = stackalloc double[10];
            Span<double> val = stackalloc double[10];
            int read = series.ReadLatest(ts, val);
            Assert.Equal(10, read);
            Assert.Equal(100_000 - 1, ts[9]);
        }
    }
}
