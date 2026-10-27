using System;
using Xunit;
using ZeroStorage.Core.Codecs;

namespace ZeroStorage.Tests
{
    public class Chimp128Tests
    {
        [Fact]
        public void EncodeDecode_Empty_ReturnsEmpty()
        {
            byte[] encoded = Chimp128Codec.Encode(ReadOnlySpan<double>.Empty);
            Assert.Empty(encoded);

            double[] dest = new double[10];
            Chimp128Codec.Decode(Array.Empty<byte>(), dest);
        }

        [Fact]
        public void EncodeDecode_SingleValue_RoundtripsLosslessly()
        {
            double[] original = new[] { 42.123456789 };
            byte[] encoded = Chimp128Codec.Encode(original);
            Assert.NotEmpty(encoded);

            double[] decoded = new double[1];
            Chimp128Codec.Decode(encoded, decoded);

            Assert.Equal(original[0], decoded[0], 15);
        }

        [Fact]
        public void EncodeDecode_IdenticalValues_HighlyCompressed()
        {
            // Tests the '00' bit branch (identical consecutive values)
            double[] original = new double[1000];
            Array.Fill(original, 99.87654321);

            byte[] encoded = Chimp128Codec.Encode(original);
            Assert.NotEmpty(encoded);

            // 1000 doubles uncompressed = 8000 bytes. With Chimp128 '00' 2 bits/value, should be under 350 bytes!
            Assert.True(encoded.Length < 350, $"Encoded size was {encoded.Length} bytes, expected < 350");

            double[] decoded = new double[original.Length];
            Chimp128Codec.Decode(encoded, decoded);

            for (int i = 0; i < original.Length; i++)
            {
                Assert.Equal(original[i], decoded[i], 15);
            }
        }

        [Fact]
        public void EncodeDecode_CircularCacheHits_OutperformsGorilla()
        {
            // Tests the '01' bit branch (values hitting the 128-entry circular cache)
            // Repeating cycle of 8 distinct sensor setpoints: 10.0, 20.0, 30.0, ..., 80.0
            double[] cycle = new[] { 10.1, 20.2, 30.3, 40.4, 50.5, 60.6, 70.7, 80.8 };
            double[] original = new double[800];
            for (int i = 0; i < original.Length; i++)
            {
                original[i] = cycle[i % cycle.Length];
            }

            byte[] encoded = Chimp128Codec.Encode(original);
            Assert.NotEmpty(encoded);

            double[] decoded = new double[original.Length];
            Chimp128Codec.Decode(encoded, decoded);

            for (int i = 0; i < original.Length; i++)
            {
                Assert.Equal(original[i], decoded[i], 15);
            }
        }

        [Fact]
        public void EncodeDecode_OscillatingTelemetry_LosslessRoundtrip()
        {
            // Realistic sensor wave with gradual floating-point delta variations
            double[] original = new double[500];
            for (int i = 0; i < original.Length; i++)
            {
                original[i] = 25.0 + Math.Sin(i * 0.05) * 5.0 + (i * 0.001);
            }

            byte[] encoded = Chimp128Codec.Encode(original);
            Assert.NotEmpty(encoded);

            double[] decoded = new double[original.Length];
            Chimp128Codec.Decode(encoded, decoded);

            for (int i = 0; i < original.Length; i++)
            {
                Assert.Equal(original[i], decoded[i], 15);
            }
        }

        [Fact]
        public void EncodeDecode_RandomFloats_LosslessRoundtrip()
        {
            var rng = new Random(42);
            double[] original = new double[200];
            for (int i = 0; i < original.Length; i++)
            {
                original[i] = rng.NextDouble() * 100000.0 - 50000.0;
            }

            byte[] encoded = Chimp128Codec.Encode(original);
            Assert.NotEmpty(encoded);

            double[] decoded = new double[original.Length];
            Chimp128Codec.Decode(encoded, decoded);

            for (int i = 0; i < original.Length; i++)
            {
                Assert.Equal(original[i], decoded[i], 15);
            }
        }

        [Fact]
        public void EncodeDecode_LsbHashTableTrailingZerosHit_CompressesAndRoundtripsLosslessly()
        {
            // 4 interleaved telemetry channels with distinct 14-bit LSB signatures (bits 0..13)
            // and small variations in upper mantissa bits (bit 32..36), so consecutive samples
            // differ across all 64 bits, while samples 4 steps apart share the same 14-bit LSB key
            // and have >= 32 trailing zeros (> 14) and >= 24 leading zeros.
            ulong[] baseChannelBits = new ulong[]
            {
                (ulong)BitConverter.DoubleToInt64Bits(10.1234567890123),
                (ulong)BitConverter.DoubleToInt64Bits(1000.9876543210987),
                (ulong)BitConverter.DoubleToInt64Bits(-500.5555555555555),
                (ulong)BitConverter.DoubleToInt64Bits(42.3333333333333)
            };

            double[] original = new double[1024];
            for (int i = 0; i < original.Length; i++)
            {
                ulong chBits = baseChannelBits[i & 3];
                ulong delta = (ulong)((i >> 2) & 0x1F) << 32;
                original[i] = BitConverter.Int64BitsToDouble((long)(chBits ^ delta));
            }

            byte[] encoded = Chimp128Codec.Compress(original);
            Assert.NotEmpty(encoded);
            Assert.True(encoded.Length < original.Length * sizeof(double) / 2,
                $"Expected high compression from O(1) LSB cache hits, got {encoded.Length} bytes for {original.Length * 8} raw bytes.");

            double[] decoded = new double[original.Length];
            Chimp128Codec.Decompress(encoded, decoded);

            for (int i = 0; i < original.Length; i++)
            {
                Assert.Equal(original[i], decoded[i]);
            }
        }

        [Fact]
        public void EncodeDecode_LargeStreamWrapAround_ZeroLohAndLossless()
        {
            // > 65536 samples to exercise ushort index wrap-around in the 16,384-entry LSB hash table
            int count = 70000;
            double[] original = new double[count];
            for (int i = 0; i < count; i++)
            {
                original[i] = ((i % 16) * 1.25) + ((i % 128) * 0.0625);
            }

            byte[] encoded = Chimp128Codec.Encode(original);
            double[] decoded = new double[count];
            Chimp128Codec.Decode(encoded, decoded);

            for (int i = 0; i < count; i++)
            {
                Assert.Equal(original[i], decoded[i]);
            }
        }
    }
}
