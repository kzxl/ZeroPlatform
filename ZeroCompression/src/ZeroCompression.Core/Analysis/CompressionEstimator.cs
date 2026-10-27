using System;
using System.Collections.Generic;
using System.IO;
using ZstdSharp;

namespace ZeroCompression.Core.Analysis
{
    /// <summary>Result of a quick compressibility probe.</summary>
    public sealed class EstimateResult
    {
        public long TotalSize { get; init; }
        public long SampledBytes { get; init; }
        public double PredictedRatio { get; init; }
        public long PredictedSize { get; init; }
        public double SavingsPercent => (1.0 - PredictedRatio) * 100.0;
        public bool AlreadyCompressed => SavingsPercent < 3.0;

        public string Summary()
        {
            if (AlreadyCompressed)
                return $"Data is virtually incompressible (~{SavingsPercent:0.#}% savings). "
                     + "Mostly pre-compressed — deep compression will yield low benefit.";
            return $"Estimated savings: ~{SavingsPercent:0.#}% "
                 + $"({FormatSize(TotalSize)} -> ~{FormatSize(PredictedSize)}).";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 0) return "?";
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            double s = bytes; int i = 0;
            while (s >= 1024 && i < u.Length - 1) { s /= 1024; i++; }
            return $"{s:0.##} {u[i]}";
        }
    }

    /// <summary>
    /// Fast compressibility estimator. Samples a bounded slice of the source and test-compresses
    /// it with low-effort zstd to predict the final ratio in ~1 second, regardless of source size.
    /// </summary>
    public static class CompressionEstimator
    {
        private const int ProbeLevel = 3;             // fast
        private const long DefaultBudget = 64L << 20; // sample at most 64 MiB
        private const int ChunkSize = 1 << 20;        // 1 MiB sampling granularity

        public static EstimateResult Estimate(string sourcePath, long sampleBudget = DefaultBudget)
        {
            var files = EnumerateFiles(sourcePath, out long totalSize);
            if (totalSize == 0)
                return new EstimateResult { TotalSize = 0, SampledBytes = 0, PredictedRatio = 1, PredictedSize = 0 };

            long budget = Math.Min(sampleBudget, totalSize);
            byte[] sample = GatherSample(files, totalSize, budget, out long sampled);

            long compressed;
            using (var compressor = new Compressor(ProbeLevel))
            {
                Span<byte> dest = new byte[(int)Compressor.GetCompressBound(sample.Length)];
                compressed = compressor.Wrap(sample, dest);
            }

            double ratio = sampled > 0 ? (double)compressed / sampled : 1.0;
            ratio = Math.Clamp(ratio, 0.0001, 1.0);

            return new EstimateResult
            {
                TotalSize = totalSize,
                SampledBytes = sampled,
                PredictedRatio = ratio,
                PredictedSize = (long)(totalSize * ratio),
            };
        }

        private static List<string> EnumerateFiles(string sourcePath, out long totalSize)
        {
            var list = new List<string>();
            totalSize = 0;
            if (File.Exists(sourcePath))
            {
                list.Add(sourcePath);
                totalSize = new FileInfo(sourcePath).Length;
            }
            else if (Directory.Exists(sourcePath))
            {
                foreach (var f in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        long len = new FileInfo(f).Length;
                        list.Add(f);
                        totalSize += len;
                    }
                    catch { /* skip unreadable entries */ }
                }
            }
            else
            {
                throw new FileNotFoundException("Source path not found.", sourcePath);
            }
            return list;
        }

        private static byte[] GatherSample(List<string> files, long totalSize, long budget, out long sampled)
        {
            using var ms = new MemoryStream((int)Math.Min(budget, int.MaxValue));
            byte[] buf = new byte[ChunkSize];
            double fraction = (double)budget / totalSize;

            foreach (var f in files)
            {
                if (ms.Length >= budget) break;
                long len;
                try { len = new FileInfo(f).Length; } catch { continue; }
                if (len == 0) continue;

                long toReadFromFile = Math.Max(ChunkSize, (long)(len * fraction));
                toReadFromFile = Math.Min(toReadFromFile, len);

                try
                {
                    using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read);
                    long readSoFar = 0;
                    long[] offsets = len > 4L * ChunkSize
                        ? new[] { 0L, len / 2 }
                        : new[] { 0L };

                    foreach (long off in offsets)
                    {
                        if (ms.Length >= budget || readSoFar >= toReadFromFile) break;
                        fs.Seek(off, SeekOrigin.Begin);
                        long target = toReadFromFile / offsets.Length;
                        long got = 0;
                        while (got < target && ms.Length < budget)
                        {
                            int want = (int)Math.Min(buf.Length, target - got);
                            int n = fs.Read(buf, 0, want);
                            if (n == 0) break;
                            ms.Write(buf, 0, n);
                            got += n;
                            readSoFar += n;
                        }
                    }
                }
                catch { /* skip unreadable files */ }
            }

            sampled = ms.Length;
            return ms.ToArray();
        }
    }
}
