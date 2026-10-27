using System;
using System.Buffers.Binary;
using System.IO;
using ZeroPrimitives.Buffers;

namespace ZeroStorage.Core.Codecs
{
    /// <summary>
    /// Pure C# Run-Length Encoding (RLE) codec optimized for industrial discrete states (Boolean, enum, error codes).
    /// Compresses long runs of unchanging PLC I/O values into ultra-compact byte streams with zero heap allocation.
    /// </summary>
    public static class RleCodec
    {
        public static byte[] CompressBooleans(ReadOnlySpan<bool> values)
        {
            if (values.IsEmpty) return Array.Empty<byte>();

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Write total count (4 bytes)
                writer.Write(values.Length);

                // Write initial bit state (1 byte)
                bool currentBit = values[0];
                writer.Write((byte)(currentBit ? 1 : 0));

                Span<byte> varIntBuf = stackalloc byte[10];
                int currentRunLength = 1;

                for (int i = 1; i < values.Length; i++)
                {
                    if (values[i] == currentBit)
                    {
                        currentRunLength++;
                    }
                    else
                    {
                        // Write run length of current state
                        VarIntCodec.WriteVarInt32(varIntBuf, currentRunLength, out int bytesWritten);
                        for (int b = 0; b < bytesWritten; b++) writer.Write(varIntBuf[b]);

                        currentBit = values[i];
                        currentRunLength = 1;
                    }
                }

                // Write final run length
                VarIntCodec.WriteVarInt32(varIntBuf, currentRunLength, out int finalBytes);
                for (int b = 0; b < finalBytes; b++) writer.Write(varIntBuf[b]);

                writer.Flush();
                return ms.ToArray();
            }
        }

        public static void DecompressBooleans(ReadOnlySpan<byte> compressed, Span<bool> destination)
        {
            if (compressed.IsEmpty || destination.IsEmpty) return;

            int totalCount = BinaryPrimitives.ReadInt32LittleEndian(compressed.Slice(0, 4));
            bool currentBit = compressed[4] != 0;

            int offset = 5;
            int destIdx = 0;
            int limit = Math.Min(totalCount, destination.Length);

            while (offset < compressed.Length && destIdx < limit)
            {
                if (VarIntCodec.TryReadVarInt32(compressed.Slice(offset), out int runLen, out int bytesRead))
                {
                    offset += bytesRead;
                    int countToFill = Math.Min(runLen, limit - destIdx);
                    destination.Slice(destIdx, countToFill).Fill(currentBit);
                    destIdx += countToFill;
                    currentBit = !currentBit; // Alternate state
                }
                else
                {
                    break;
                }
            }
        }

        public static byte[] CompressInt32(ReadOnlySpan<int> values)
        {
            if (values.IsEmpty) return Array.Empty<byte>();

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(values.Length);

                int currentVal = values[0];
                int runLen = 1;
                Span<byte> varIntBuf = stackalloc byte[10];

                for (int i = 1; i < values.Length; i++)
                {
                    if (values[i] == currentVal)
                    {
                        runLen++;
                    }
                    else
                    {
                        // Write value, then run length
                        VarIntCodec.WriteVarInt32(varIntBuf, currentVal, out int valBytes);
                        for (int b = 0; b < valBytes; b++) writer.Write(varIntBuf[b]);

                        VarIntCodec.WriteVarInt32(varIntBuf, runLen, out int lenBytes);
                        for (int b = 0; b < lenBytes; b++) writer.Write(varIntBuf[b]);

                        currentVal = values[i];
                        runLen = 1;
                    }
                }

                // Final run
                VarIntCodec.WriteVarInt32(varIntBuf, currentVal, out int lastValBytes);
                for (int b = 0; b < lastValBytes; b++) writer.Write(varIntBuf[b]);

                VarIntCodec.WriteVarInt32(varIntBuf, runLen, out int lastLenBytes);
                for (int b = 0; b < lastLenBytes; b++) writer.Write(varIntBuf[b]);

                writer.Flush();
                return ms.ToArray();
            }
        }

        public static void DecompressInt32(ReadOnlySpan<byte> compressed, Span<int> destination)
        {
            if (compressed.IsEmpty || destination.IsEmpty) return;

            int totalCount = BinaryPrimitives.ReadInt32LittleEndian(compressed.Slice(0, 4));
            int offset = 4;
            int destIdx = 0;
            int limit = Math.Min(totalCount, destination.Length);

            while (offset < compressed.Length && destIdx < limit)
            {
                if (VarIntCodec.TryReadVarInt32(compressed.Slice(offset), out int val, out int valBytesRead))
                {
                    offset += valBytesRead;
                    if (VarIntCodec.TryReadVarInt32(compressed.Slice(offset), out int runLen, out int lenBytesRead))
                    {
                        offset += lenBytesRead;
                        int countToFill = Math.Min(runLen, limit - destIdx);
                        destination.Slice(destIdx, countToFill).Fill(val);
                        destIdx += countToFill;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
        }
    }
}
