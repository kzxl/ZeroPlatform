using System;
using System.Collections.Generic;
using ZeroStorage.Core.BitIO;

namespace ZeroStorage.Core.Gorilla
{
    /// <summary>
    /// Gorilla time-series decompression engine. Reconstructs identical timestamps and double values.
    /// </summary>
    public static class GorillaDecoder
    {
        public static List<TimeSeriesPoint> Decode(byte[] compressedBytes)
        {
            if (compressedBytes == null) throw new ArgumentNullException(nameof(compressedBytes));
            if (compressedBytes.Length == 0) return new List<TimeSeriesPoint>();

            var reader = new BitStreamReader(compressedBytes);

            int totalPoints = (int)reader.ReadBits(32);
            var result = new List<TimeSeriesPoint>(totalPoints);
            if (totalPoints == 0) return result;

            // Point 0
            long t0 = (long)reader.ReadBits(64);
            ulong v0Bits = reader.ReadBits(64);
            double v0 = BitConverter.Int64BitsToDouble((long)v0Bits);
            result.Add(new TimeSeriesPoint(t0, v0));

            if (totalPoints == 1) return result;

            // Point 1
            long prevDelta = (long)(uint)reader.ReadBits(32);
            long prevTime = t0 + prevDelta;

            ulong prevValBits = v0Bits;
            int prevLeadingZeros = 0;
            int prevMeaningful = 0;
            int prevTrailingZeros = 0;

            double v1 = DecodeValue(reader, ref prevValBits, ref prevLeadingZeros, ref prevMeaningful, ref prevTrailingZeros);
            result.Add(new TimeSeriesPoint(prevTime, v1));

            // Points 2 to N-1
            for (int i = 2; i < totalPoints; i++)
            {
                long dod = DecodeDeltaOfDelta(reader);
                long currDelta = prevDelta + dod;
                long currTime = prevTime + currDelta;

                double currVal = DecodeValue(reader, ref prevValBits, ref prevLeadingZeros, ref prevMeaningful, ref prevTrailingZeros);

                result.Add(new TimeSeriesPoint(currTime, currVal));

                prevDelta = currDelta;
                prevTime = currTime;
            }

            return result;
        }

        /// <summary>
        /// Decodes timestamps directly into a destination span without heap allocations.
        /// </summary>
        public static void DecodeTimestamps(byte[] compressedBytes, Span<long> destination)
        {
            if (compressedBytes == null) throw new ArgumentNullException(nameof(compressedBytes));
            if (compressedBytes.Length == 0 || destination.IsEmpty) return;

            var reader = new BitStreamReader(compressedBytes);
            int totalPoints = (int)reader.ReadBits(32);
            int count = Math.Min(totalPoints, destination.Length);
            if (count == 0) return;

            // Point 0
            destination[0] = (long)reader.ReadBits(64);
            reader.ReadBits(64); // skip v0Bits

            if (count == 1) return;

            long prevDelta = (long)(uint)reader.ReadBits(32);
            destination[1] = destination[0] + prevDelta;

            ulong prevValBits = 0;
            int prevLeadingZeros = 0;
            int prevMeaningful = 0;
            int prevTrailingZeros = 0;
            DecodeValue(reader, ref prevValBits, ref prevLeadingZeros, ref prevMeaningful, ref prevTrailingZeros);

            long prevTime = destination[1];

            for (int i = 2; i < count; i++)
            {
                long dod = DecodeDeltaOfDelta(reader);
                long currDelta = prevDelta + dod;
                long currTime = prevTime + currDelta;

                DecodeValue(reader, ref prevValBits, ref prevLeadingZeros, ref prevMeaningful, ref prevTrailingZeros);

                destination[i] = currTime;
                prevDelta = currDelta;
                prevTime = currTime;
            }
        }

        /// <summary>
        /// Decodes floating point values directly into a destination span without heap allocations.
        /// </summary>
        public static void DecodeValues(byte[] compressedBytes, Span<double> destination)
        {
            if (compressedBytes == null) throw new ArgumentNullException(nameof(compressedBytes));
            if (compressedBytes.Length == 0 || destination.IsEmpty) return;

            var reader = new BitStreamReader(compressedBytes);
            int totalPoints = (int)reader.ReadBits(32);
            int count = Math.Min(totalPoints, destination.Length);
            if (count == 0) return;

            // Point 0
            reader.ReadBits(64); // skip t0
            ulong v0Bits = reader.ReadBits(64);
            destination[0] = BitConverter.Int64BitsToDouble((long)v0Bits);

            if (count == 1) return;

            reader.ReadBits(32); // skip delta

            ulong prevValBits = v0Bits;
            int prevLeadingZeros = 0;
            int prevMeaningful = 0;
            int prevTrailingZeros = 0;

            destination[1] = DecodeValue(reader, ref prevValBits, ref prevLeadingZeros, ref prevMeaningful, ref prevTrailingZeros);

            for (int i = 2; i < count; i++)
            {
                DecodeDeltaOfDelta(reader);
                destination[i] = DecodeValue(reader, ref prevValBits, ref prevLeadingZeros, ref prevMeaningful, ref prevTrailingZeros);
            }
        }

        private static long DecodeDeltaOfDelta(BitStreamReader reader)
        {
            int bit0 = reader.ReadBit();
            if (bit0 == 0)
            {
                return 0; // '0'
            }

            int bit1 = reader.ReadBit();
            if (bit1 == 0)
            {
                // '10' -> 7 bits
                long val = (long)reader.ReadBits(7);
                return val - 63;
            }

            int bit2 = reader.ReadBit();
            if (bit2 == 0)
            {
                // '110' -> 9 bits
                long val = (long)reader.ReadBits(9);
                return val - 255;
            }

            int bit3 = reader.ReadBit();
            if (bit3 == 0)
            {
                // '1110' -> 12 bits
                long val = (long)reader.ReadBits(12);
                return val - 2047;
            }

            // '1111' -> 32 bits
            uint d32 = (uint)reader.ReadBits(32);
            return (long)(int)d32;
        }

        private static double DecodeValue(
            BitStreamReader reader,
            ref ulong prevValBits,
            ref int prevLeadingZeros,
            ref int prevMeaningful,
            ref int prevTrailingZeros)
        {
            int valChanged = reader.ReadBit();
            if (valChanged == 0)
            {
                // Value is identical to previous
                return BitConverter.Int64BitsToDouble((long)prevValBits);
            }

            int newBlock = reader.ReadBit();
            if (newBlock == 0)
            {
                // Reuse previous leading and trailing zeros
                ulong bits = reader.ReadBits(prevMeaningful);
                ulong xor = bits << prevTrailingZeros;
                ulong currValBits = prevValBits ^ xor;
                prevValBits = currValBits;
                return BitConverter.Int64BitsToDouble((long)currValBits);
            }
            else
            {
                // Read new leading zeros and meaningful length
                int leadingZeros = (int)reader.ReadBits(5);
                int meaningful = (int)reader.ReadBits(6);
                if (meaningful == 0) meaningful = 64;

                int trailingZeros = 64 - leadingZeros - meaningful;

                ulong bits = reader.ReadBits(meaningful);
                ulong xor = bits << trailingZeros;
                ulong currValBits = prevValBits ^ xor;

                prevValBits = currValBits;
                prevLeadingZeros = leadingZeros;
                prevMeaningful = meaningful;
                prevTrailingZeros = trailingZeros;

                return BitConverter.Int64BitsToDouble((long)currValBits);
            }
        }
    }
}
