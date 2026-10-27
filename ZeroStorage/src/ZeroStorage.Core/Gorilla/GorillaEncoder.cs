using System;
using System.Collections.Generic;
using ZeroStorage.Core.BitIO;

namespace ZeroStorage.Core.Gorilla
{
    /// <summary>
    /// Gorilla time-series compression engine based on the Facebook Gorilla TSDB architecture.
    /// Uses Delta-of-Delta timestamp compression and IEEE 754 XOR floating point compression.
    /// </summary>
    public static class GorillaEncoder
    {
        public static byte[] Encode(IReadOnlyList<TimeSeriesPoint> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Count == 0) return Array.Empty<byte>();

            using (var writer = new BitStreamWriter(points.Count * 4 + 64))
            {
                // Write total number of points (32 bits)
                writer.WriteBits((ulong)points.Count, 32);

                // First point
                long t0 = points[0].TimestampMs;
                ulong v0Bits = (ulong)BitConverter.DoubleToInt64Bits(points[0].Value);
                writer.WriteBits((ulong)t0, 64);
                writer.WriteBits(v0Bits, 64);

                if (points.Count == 1)
                {
                    return writer.ToByteArray();
                }

                // Second point: write first delta (32 bits)
                long t1 = points[1].TimestampMs;
                long prevDelta = t1 - t0;
                writer.WriteBits((ulong)(uint)prevDelta, 32);

                // Encode v1
                ulong prevValBits = v0Bits;
                int prevLeadingZeros = int.MaxValue;
                int prevTrailingZeros = int.MaxValue;

                EncodeValue(writer, points[1].Value, ref prevValBits, ref prevLeadingZeros, ref prevTrailingZeros);

                long prevTime = t1;

                // Subsequent points
                for (int i = 2; i < points.Count; i++)
                {
                    long currTime = points[i].TimestampMs;
                    long currDelta = currTime - prevTime;
                    long dod = currDelta - prevDelta;

                    // Encode timestamp Delta-of-Delta
                    EncodeDeltaOfDelta(writer, dod);

                    prevDelta = currDelta;
                    prevTime = currTime;

                    // Encode value
                    EncodeValue(writer, points[i].Value, ref prevValBits, ref prevLeadingZeros, ref prevTrailingZeros);
                }

                return writer.ToByteArray();
            }
        }

        /// <summary>
        /// Highly-optimized zero-allocation encoder for timestamps vector.
        /// Assumes fixed zero values with zero-allocation bitstreams.
        /// </summary>
        public static byte[] EncodeTimestamps(ReadOnlySpan<long> timestamps)
        {
            if (timestamps.IsEmpty) return Array.Empty<byte>();

            using (var writer = new BitStreamWriter(timestamps.Length * 2 + 64))
            {
                writer.WriteBits((ulong)timestamps.Length, 32);

                long t0 = timestamps[0];
                writer.WriteBits((ulong)t0, 64);
                writer.WriteBits(0UL, 64);

                if (timestamps.Length == 1) return writer.ToByteArray();

                long t1 = timestamps[1];
                long prevDelta = t1 - t0;
                writer.WriteBits((ulong)(uint)prevDelta, 32);
                writer.WriteBit(0); // 0.0 == 0.0 -> XOR == 0

                long prevTime = t1;
                for (int i = 2; i < timestamps.Length; i++)
                {
                    long currTime = timestamps[i];
                    long currDelta = currTime - prevTime;
                    long dod = currDelta - prevDelta;

                    EncodeDeltaOfDelta(writer, dod);

                    prevDelta = currDelta;
                    prevTime = currTime;
                    writer.WriteBit(0); // value unchanged
                }

                return writer.ToByteArray();
            }
        }

        /// <summary>
        /// Highly-optimized zero-allocation encoder for floating point columns with implicit uniform sequence timestamps.
        /// </summary>
        public static byte[] EncodeValues(ReadOnlySpan<double> values)
        {
            if (values.IsEmpty) return Array.Empty<byte>();

            using (var writer = new BitStreamWriter(values.Length * 4 + 64))
            {
                writer.WriteBits((ulong)values.Length, 32);

                ulong v0Bits = (ulong)BitConverter.DoubleToInt64Bits(values[0]);
                writer.WriteBits(0UL, 64); // t0 = 0
                writer.WriteBits(v0Bits, 64);

                if (values.Length == 1) return writer.ToByteArray();

                writer.WriteBits(1U, 32); // prevDelta = 1

                ulong prevValBits = v0Bits;
                int prevLeadingZeros = int.MaxValue;
                int prevTrailingZeros = int.MaxValue;

                EncodeValue(writer, values[1], ref prevValBits, ref prevLeadingZeros, ref prevTrailingZeros);

                for (int i = 2; i < values.Length; i++)
                {
                    writer.WriteBit(0); // dod = 1 - 1 = 0
                    EncodeValue(writer, values[i], ref prevValBits, ref prevLeadingZeros, ref prevTrailingZeros);
                }

                return writer.ToByteArray();
            }
        }

        private static void EncodeDeltaOfDelta(BitStreamWriter writer, long dod)
        {
            if (dod == 0)
            {
                writer.WriteBit(0); // '0'
            }
            else if (dod >= -63 && dod <= 64)
            {
                // '10' + 7 bits
                writer.WriteBit(1);
                writer.WriteBit(0);
                writer.WriteBits((ulong)(dod + 63), 7);
            }
            else if (dod >= -255 && dod <= 256)
            {
                // '110' + 9 bits
                writer.WriteBit(1);
                writer.WriteBit(1);
                writer.WriteBit(0);
                writer.WriteBits((ulong)(dod + 255), 9);
            }
            else if (dod >= -2047 && dod <= 2048)
            {
                // '1110' + 12 bits
                writer.WriteBit(1);
                writer.WriteBit(1);
                writer.WriteBit(1);
                writer.WriteBit(0);
                writer.WriteBits((ulong)(dod + 2047), 12);
            }
            else
            {
                // '1111' + 32 bits
                writer.WriteBit(1);
                writer.WriteBit(1);
                writer.WriteBit(1);
                writer.WriteBit(1);
                writer.WriteBits((ulong)(uint)dod, 32);
            }
        }

        private static void EncodeValue(
            BitStreamWriter writer,
            double value,
            ref ulong prevValBits,
            ref int prevLeadingZeros,
            ref int prevTrailingZeros)
        {
            ulong currBits = (ulong)BitConverter.DoubleToInt64Bits(value);
            ulong xor = currBits ^ prevValBits;

            if (xor == 0)
            {
                writer.WriteBit(0); // Same value as previous
            }
            else
            {
                writer.WriteBit(1); // Value changed

                int leadingZeros = CountLeadingZeros(xor);
                if (leadingZeros > 31) leadingZeros = 31; // Clamp to 5 bits

                int trailingZeros = CountTrailingZeros(xor);

                int prevMeaningful = 64 - prevLeadingZeros - prevTrailingZeros;
                int meaningful = 64 - leadingZeros - trailingZeros;

                // Case: Reuse previous leading and trailing zeros block
                if (prevLeadingZeros != int.MaxValue &&
                    leadingZeros >= prevLeadingZeros &&
                    trailingZeros >= prevTrailingZeros)
                {
                    writer.WriteBit(0);
                    ulong meaningfulBits = xor >> prevTrailingZeros;
                    writer.WriteBits(meaningfulBits, prevMeaningful);
                }
                else
                {
                    // Case: Store new block
                    writer.WriteBit(1);
                    writer.WriteBits((ulong)leadingZeros, 5);
                    writer.WriteBits((ulong)(meaningful == 64 ? 0 : meaningful), 6);
                    ulong meaningfulBits = xor >> trailingZeros;
                    writer.WriteBits(meaningfulBits, meaningful);

                    prevLeadingZeros = leadingZeros;
                    prevTrailingZeros = trailingZeros;
                }

                prevValBits = currBits;
            }
        }

        public static int CountLeadingZeros(ulong x)
        {
            if (x == 0) return 64;
            int n = 0;
            if (x <= 0x00000000FFFFFFFFUL) { n += 32; x <<= 32; }
            if (x <= 0x0000FFFFFFFFFFFFUL) { n += 16; x <<= 16; }
            if (x <= 0x00FFFFFFFFFFFFFFUL) { n += 8;  x <<= 8;  }
            if (x <= 0x0FFFFFFFFFFFFFFFUL) { n += 4;  x <<= 4;  }
            if (x <= 0x3FFFFFFFFFFFFFFFUL) { n += 2;  x <<= 2;  }
            if (x <= 0x7FFFFFFFFFFFFFFFUL) { n += 1; }
            return n;
        }

        public static int CountTrailingZeros(ulong x)
        {
            if (x == 0) return 64;
            int n = 0;
            if ((x & 0x00000000FFFFFFFFUL) == 0) { n += 32; x >>= 32; }
            if ((x & 0x000000000000FFFFUL) == 0) { n += 16; x >>= 16; }
            if ((x & 0x00000000000000FFUL) == 0) { n += 8;  x >>= 8;  }
            if ((x & 0x000000000000000FUL) == 0) { n += 4;  x >>= 4;  }
            if ((x & 0x0000000000000003UL) == 0) { n += 2;  x >>= 2;  }
            if ((x & 0x0000000000000001UL) == 0) { n += 1; }
            return n;
        }
    }
}
