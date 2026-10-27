using System;
using System.IO;
using System.Text;
using Xunit;
using ZeroCompression.Core;
using ZeroCompression.Core.Analysis;
using ZeroCompression.Core.Benchmarks;

namespace ZeroCompression.Tests
{
    public class DataClassifierTests
    {
        [Fact]
        public void Classify_TelemetryData_DetectsTelemetryTimeSeries_And_RecommendsZeroTelemetry()
        {
            var telemetry = CompressionBenchmarkSuite.GenerateTelemetryDataset(100);
            var result = DataClassifier.Classify(telemetry.RawBytes);

            Assert.Equal(DetectedDataType.TelemetryTimeSeries, result.DetectedType);
            Assert.Equal(CompressionMethod.ZeroTelemetry, result.RecommendedMethod);
            Assert.Contains("ZeroTelemetry", result.Reason);
        }

        [Fact]
        public void Classify_StructuredJson_DetectsStructuredJson_And_RecommendsZstdWithLdm()
        {
            byte[] jsonBytes = CompressionBenchmarkSuite.GenerateStructuredJsonDataset(50);
            var result = DataClassifier.Classify(jsonBytes, "access_log.json");

            Assert.Equal(DetectedDataType.StructuredJson, result.DetectedType);
            Assert.Equal(CompressionMethod.Zstd, result.RecommendedMethod);
            Assert.True(result.LongDistanceMatching);
            Assert.True(result.RecommendedLevel >= 19);
        }

        [Fact]
        public void Classify_DelimitedCsv_DetectsDelimitedText()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 100; i++)
            {
                sb.AppendLine($"100{i},PROD-A{i},250.50,IN_STOCK,2026-09-18");
            }
            byte[] csvBytes = Encoding.UTF8.GetBytes(sb.ToString());

            var result = DataClassifier.Classify(csvBytes, "inventory.csv");
            Assert.Equal(DetectedDataType.DelimitedText, result.DetectedType);
            Assert.Equal(CompressionMethod.Zstd, result.RecommendedMethod);
        }

        [Fact]
        public void Classify_ExecutableBinary_DetectsPE_And_RecommendsLzma()
        {
            byte[] peHeader = new byte[1024];
            peHeader[0] = 0x4D; // 'M'
            peHeader[1] = 0x5A; // 'Z'

            var result = DataClassifier.Classify(peHeader, "app.exe");
            Assert.Equal(DetectedDataType.ExecutableBinary, result.DetectedType);
            Assert.Equal(CompressionMethod.Lzma, result.RecommendedMethod);
        }

        [Fact]
        public void Classify_PrecompressedZip_DetectsAlreadyCompressed_And_RecommendsStore()
        {
            byte[] zipHeader = new byte[512];
            zipHeader[0] = 0x50; // 'P'
            zipHeader[1] = 0x4B; // 'K'
            zipHeader[2] = 0x03;
            zipHeader[3] = 0x04;

            var result = DataClassifier.Classify(zipHeader, "backup.zip");
            Assert.Equal(DetectedDataType.AlreadyCompressed, result.DetectedType);
            Assert.Equal(CompressionMethod.Store, result.RecommendedMethod);
        }

        [Fact]
        public void Classify_HighEntropyRandomData_DetectsAlreadyCompressed()
        {
            byte[] highEntropy = new byte[8192];
            new Random(12345).NextBytes(highEntropy);

            var result = DataClassifier.Classify(highEntropy);
            Assert.Equal(DetectedDataType.AlreadyCompressed, result.DetectedType);
            Assert.Equal(CompressionMethod.Store, result.RecommendedMethod);
            Assert.True(result.ShannonEntropy >= 7.90);
        }

        [Fact]
        public void Classify_PlainText_DetectsPlainText()
        {
            string text = "This is a simple plain text document describing software design principles and architecture.";
            byte[] textBytes = Encoding.UTF8.GetBytes(text);

            var result = DataClassifier.Classify(textBytes, "readme.txt");
            Assert.Equal(DetectedDataType.PlainText, result.DetectedType);
            Assert.Equal(CompressionMethod.Brotli, result.RecommendedMethod);
        }

        [Fact]
        public void CompressionOptions_AutoDetect_ReturnsConfiguredOptions()
        {
            var telemetry = CompressionBenchmarkSuite.GenerateTelemetryDataset(50);
            var opt = CompressionOptions.AutoDetect(telemetry.RawBytes);

            Assert.Equal(CompressionMethod.ZeroTelemetry, opt.Method);
        }
    }
}
