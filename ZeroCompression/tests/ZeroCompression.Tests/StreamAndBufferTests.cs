using System;
using System.IO;
using System.Text;
using Xunit;
using ZeroCompression.Core.Buffers;
using ZeroCompression.Core.Hashing;
using ZeroCompression.Core.Streams;

namespace ZeroCompression.Tests
{
    public class StreamAndBufferTests
    {
        [Fact]
        public void BufferPool_Rent_And_Scope_WorksWithoutLeaks()
        {
            using (var buffer = BufferPool.Scope(1024))
            {
                Assert.NotNull(buffer.Array);
                Assert.True(buffer.Array.Length >= 1024);
                buffer.Span[0] = 42;
                Assert.Equal(42, buffer.Array[0]);
            }
        }

        [Fact]
        public void ObservableStream_CalculatesCrc_And_Progress()
        {
            var data = Encoding.UTF8.GetBytes("Data to test CRC32 calculation in streaming fashion.");
            uint expectedCrc = Crc32.Compute(data);

            long progressReported = 0;
            var progress = new Progress<long>(val => progressReported = val);

            using var target = new MemoryStream();
            using (var obs = new ObservableStream(target, computeCrc: true, progress: progress, reportEvery: 1))
            {
                obs.Write(data, 0, data.Length);
                obs.ReportFinal();
                Assert.Equal(expectedCrc, obs.Crc);
                Assert.Equal(data.Length, obs.BytesObserved);
            }

            Assert.Equal(data, target.ToArray());
        }

        [Fact]
        public void SubStream_ReadsWindowCorrectly()
        {
            byte[] full = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            using var ms = new MemoryStream(full);
            using var sub = new SubStream(ms, start: 3, length: 4);

            Assert.Equal(4, sub.Length);
            byte[] buf = new byte[4];
            int read = sub.Read(buf, 0, buf.Length);
            Assert.Equal(4, read);
            Assert.Equal(new byte[] { 3, 4, 5, 6 }, buf);
        }
    }
}
