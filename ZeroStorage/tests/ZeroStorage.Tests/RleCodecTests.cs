using System;
using Xunit;
using ZeroStorage.Core.Codecs;

namespace ZeroStorage.Tests
{
    public class RleCodecTests
    {
        [Fact]
        public void CompressBooleans_Empty_ReturnsEmpty()
        {
            byte[] compressed = RleCodec.CompressBooleans(ReadOnlySpan<bool>.Empty);
            Assert.Empty(compressed);

            bool[] dest = new bool[5];
            RleCodec.DecompressBooleans(Array.Empty<byte>(), dest);
        }

        [Fact]
        public void CompressBooleans_AllSame_HighCompression()
        {
            bool[] original = new bool[10000];
            Array.Fill(original, true);

            byte[] compressed = RleCodec.CompressBooleans(original);
            Assert.NotEmpty(compressed);
            // 10,000 bools compressed down to ~7-8 bytes
            Assert.True(compressed.Length < 15, $"Compressed size {compressed.Length} expected < 15 bytes");

            bool[] decompressed = new bool[original.Length];
            RleCodec.DecompressBooleans(compressed, decompressed);

            for (int i = 0; i < original.Length; i++)
            {
                Assert.True(decompressed[i]);
            }
        }

        [Fact]
        public void CompressBooleans_AlternatingRuns_RoundtripsAccurately()
        {
            bool[] original = new bool[2500];
            for (int i = 0; i < original.Length; i++)
            {
                // Run of 50 true, 50 false, ...
                original[i] = (i / 50) % 2 == 0;
            }

            byte[] compressed = RleCodec.CompressBooleans(original);
            Assert.NotEmpty(compressed);

            bool[] decompressed = new bool[original.Length];
            RleCodec.DecompressBooleans(compressed, decompressed);

            for (int i = 0; i < original.Length; i++)
            {
                Assert.Equal(original[i], decompressed[i]);
            }
        }

        [Fact]
        public void CompressInt32_Empty_ReturnsEmpty()
        {
            byte[] compressed = RleCodec.CompressInt32(ReadOnlySpan<int>.Empty);
            Assert.Empty(compressed);

            int[] dest = new int[5];
            RleCodec.DecompressInt32(Array.Empty<byte>(), dest);
        }

        [Fact]
        public void CompressInt32_DiscretePlcStates_RoundtripsAccurately()
        {
            // Simulates machine running state: 0 (Stopped), 1 (Running), 2 (Warning), 3 (Fault)
            int[] original = new int[5000];
            int idx = 0;
            // 2000 points of State 1
            for (int i = 0; i < 2000; i++) original[idx++] = 1;
            // 500 points of State 2
            for (int i = 0; i < 500; i++) original[idx++] = 2;
            // 500 points of State 3
            for (int i = 0; i < 500; i++) original[idx++] = 3;
            // 2000 points of State 0
            for (int i = 0; i < 2000; i++) original[idx++] = 0;

            byte[] compressed = RleCodec.CompressInt32(original);
            Assert.NotEmpty(compressed);
            // 5000 integers uncompressed = 20,000 bytes. With 4 runs, size should be under 30 bytes!
            Assert.True(compressed.Length < 30, $"Compressed size was {compressed.Length} bytes, expected < 30");

            int[] decompressed = new int[original.Length];
            RleCodec.DecompressInt32(compressed, decompressed);

            for (int i = 0; i < original.Length; i++)
            {
                Assert.Equal(original[i], decompressed[i]);
            }
        }

        [Fact]
        public void CompressInt32_AlternatingAndNegative_RoundtripsAccurately()
        {
            int[] original = new int[] { -100, -100, -100, 0, 0, 42, 42, 42, 42, -5, -5, 1000 };
            byte[] compressed = RleCodec.CompressInt32(original);
            Assert.NotEmpty(compressed);

            int[] decompressed = new int[original.Length];
            RleCodec.DecompressInt32(compressed, decompressed);

            for (int i = 0; i < original.Length; i++)
            {
                Assert.Equal(original[i], decompressed[i]);
            }
        }
    }
}
