using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using ZeroCompression.Core.Benchmarks;

namespace ZeroCompression.Tests
{
    public class BenchmarkSuiteTests
    {
        private readonly ITestOutputHelper _output;

        public BenchmarkSuiteTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void BenchmarkSuite_ExecutesAllDatasets_SideBySide()
        {
            var reports = CompressionBenchmarkSuite.RunAllBenchmarks();

            Assert.NotEmpty(reports);
            Assert.Equal(3, reports.Count);

            foreach (var report in reports)
            {
                Assert.NotEmpty(report.Results);
                _output.WriteLine(report.ToConsoleTable());
                _output.WriteLine("\n" + report.ToMarkdownTable() + "\n");

                foreach (var result in report.Results)
                {
                    Assert.True(result.RoundtripVerified, $"Codec {result.CodecName} failed roundtrip verification on dataset {report.DatasetName}");
                    Assert.True(result.CompressedBytes > 0, $"Codec {result.CodecName} compressed size should be > 0");
                }
            }
        }
    }
}
