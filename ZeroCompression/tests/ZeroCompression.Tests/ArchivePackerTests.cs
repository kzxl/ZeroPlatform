using System;
using System.IO;
using System.Text;
using Xunit;
using ZeroCompression.Core.Packaging;

namespace ZeroCompression.Tests
{
    public class ArchivePackerTests : IDisposable
    {
        private readonly string _tempDir;

        public ArchivePackerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "zc_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, true);
            }
            catch { }
        }

        [Fact]
        public void PackAndExtract_Tar_RoundtripsSuccessfully()
        {
            string srcDir = Path.Combine(_tempDir, "source");
            Directory.CreateDirectory(srcDir);
            File.WriteAllText(Path.Combine(srcDir, "hello.txt"), "Hello from ZeroUniverse!");
            string subDir = Path.Combine(srcDir, "nested");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "inner.json"), "{\"key\": 123}");

            using var tarMs = new MemoryStream();
            long packedBytes = ArchivePacker.PackToTar(srcDir, tarMs);
            Assert.True(packedBytes > 0);

            // Verify ListEntries
            tarMs.Position = 0;
            var entries = ArchivePacker.ListTarEntries(tarMs);
            Assert.NotEmpty(entries);

            // Verify Extract
            string outDir = Path.Combine(_tempDir, "extracted");
            tarMs.Position = 0;
            ArchivePacker.ExtractFromTar(tarMs, outDir);

            Assert.True(File.Exists(Path.Combine(outDir, "source", "hello.txt")));
            Assert.Equal("Hello from ZeroUniverse!", File.ReadAllText(Path.Combine(outDir, "source", "hello.txt")));
            Assert.True(File.Exists(Path.Combine(outDir, "source", "nested", "inner.json")));
        }
    }
}
