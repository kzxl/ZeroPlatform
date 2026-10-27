using System;
using BenchmarkDotNet.Attributes;
using ZeroUI.Core.Data;

namespace ZeroUI.Core.Benchmarks
{
    [MemoryDiagnoser]
    [ShortRunJob]
    public class LttbBenchmarks
    {
        private TimePoint[] _data10k = null!;
        private TimePoint[] _data100k = null!;
        private TimePoint[] _buffer1000 = null!;

        [GlobalSetup]
        public void Setup()
        {
            _buffer1000 = new TimePoint[1000];

            _data10k = new TimePoint[10_000];
            for (int i = 0; i < _data10k.Length; i++)
            {
                _data10k[i] = new TimePoint(i, Math.Sin(i * 0.05) * 100.0 + (i % 7));
            }

            _data100k = new TimePoint[100_000];
            for (int i = 0; i < _data100k.Length; i++)
            {
                _data100k[i] = new TimePoint(i, Math.Sin(i * 0.05) * 100.0 + (i % 7));
            }
        }

        [Benchmark(Description = "LTTB Downsample 10,000 -> 1,000 pts")]
        public int Downsample_10k_to_1k()
        {
            return LttbDecimation.Downsample(_data10k, _buffer1000, 1000);
        }

        [Benchmark(Description = "LTTB Downsample 100,000 -> 1,000 pts")]
        public int Downsample_100k_to_1k()
        {
            return LttbDecimation.Downsample(_data100k, _buffer1000, 1000);
        }
    }
}
