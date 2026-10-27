using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ZeroPrimitives.Buffers;
using ZeroStorage.Core.BitIO;
using ZeroStorage.Core.Gorilla;

namespace ZeroStorage.Core.TimeSeries
{
    /// <summary>
    /// Encapsulates a multi-variable industrial time-series data block compressed with 
    /// Gorilla Delta-of-Delta timestamps, XOR float compression, and LEB128 integer codecs.
    /// </summary>
    public sealed class MultiMetricBlock
    {
        private const uint MagicNumber = 0x4D53545A; // 'ZTSM' (Zero TimeSeries Multi)

        public MultiMetricSchema Schema { get; }
        public int RowCount { get; }
        public long StartTimeMs { get; }
        public long EndTimeMs { get; }
        public byte[] CompressedData { get; }

        public MultiMetricBlock(
            MultiMetricSchema schema,
            int rowCount,
            long startTimeMs,
            long endTimeMs,
            byte[] compressedData)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            RowCount = rowCount;
            StartTimeMs = startTimeMs;
            EndTimeMs = endTimeMs;
            CompressedData = compressedData ?? throw new ArgumentNullException(nameof(compressedData));
        }

        public static MultiMetricBlock Compress(MultiMetricSchema schema, ColumnarBatch batch)
        {
            if (schema == null) throw new ArgumentNullException(nameof(schema));
            if (batch == null) throw new ArgumentNullException(nameof(batch));

            if (batch.RowCount == 0)
            {
                return new MultiMetricBlock(schema, 0, 0, 0, Array.Empty<byte>());
            }

            long start = batch.Timestamps[0];
            long end = batch.Timestamps[batch.RowCount - 1];

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms, Encoding.UTF8))
            {
                // 1. Magic + Schema Header
                writer.Write(MagicNumber);
                writer.Write(schema.SeriesName);
                writer.Write((byte)schema.Fields.Count);

                for (int f = 0; f < schema.Fields.Count; f++)
                {
                    writer.Write(schema.Fields[f].Name);
                    writer.Write((byte)schema.Fields[f].Type);
                }

                writer.Write(batch.RowCount);

                // 2. Compress Timestamps using Gorilla Delta-of-Delta
                byte[] tsCompressed = GorillaEncoder.EncodeTimestamps(batch.Timestamps.AsSpan(0, batch.RowCount));
                writer.Write(tsCompressed.Length);
                writer.Write(tsCompressed);

                // 3. Compress Each Column
                for (int f = 0; f < schema.Fields.Count; f++)
                {
                    var field = schema.Fields[f];
                    byte[] colBytes = CompressColumnFromBatch(field.Type, batch, f);
                    writer.Write(colBytes.Length);
                    writer.Write(colBytes);
                }

                writer.Flush();
                return new MultiMetricBlock(schema, batch.RowCount, start, end, ms.ToArray());
            }
        }

        public static MultiMetricBlock Compress(MultiMetricSchema schema, IReadOnlyList<MultiMetricRow> rows)
        {
            if (schema == null) throw new ArgumentNullException(nameof(schema));
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            if (rows.Count == 0)
            {
                return new MultiMetricBlock(schema, 0, 0, 0, Array.Empty<byte>());
            }

            long start = rows[0].TimestampMs;
            long end = rows[rows.Count - 1].TimestampMs;

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms, Encoding.UTF8))
            {
                // 1. Magic + Schema Header
                writer.Write(MagicNumber);
                writer.Write(schema.SeriesName);
                writer.Write((byte)schema.Fields.Count);

                for (int f = 0; f < schema.Fields.Count; f++)
                {
                    writer.Write(schema.Fields[f].Name);
                    writer.Write((byte)schema.Fields[f].Type);
                }

                writer.Write(rows.Count);

                // 2. Compress Timestamps using Gorilla Delta-of-Delta
                byte[] tsCompressed = CompressTimestamps(rows);
                writer.Write(tsCompressed.Length);
                writer.Write(tsCompressed);

                // 3. Compress Each Column
                for (int f = 0; f < schema.Fields.Count; f++)
                {
                    var field = schema.Fields[f];
                    byte[] colBytes = CompressColumn(field.Type, rows, f);
                    writer.Write(colBytes.Length);
                    writer.Write(colBytes);
                }

                writer.Flush();
                return new MultiMetricBlock(schema, rows.Count, start, end, ms.ToArray());
            }
        }

        public ColumnarBatch DecompressColumnar()
        {
            if (CompressedData.Length == 0 || RowCount == 0)
            {
                return new ColumnarBatch(Schema, 0);
            }

            using (var ms = new MemoryStream(CompressedData))
            using (var reader = new BinaryReader(ms, Encoding.UTF8))
            {
                uint magic = reader.ReadUInt32();
                if (magic != MagicNumber) throw new InvalidDataException("Invalid MultiMetricBlock header magic.");

                string seriesName = reader.ReadString();
                int fieldCount = reader.ReadByte();
                var fields = new List<MultiMetricField>(fieldCount);

                for (int f = 0; f < fieldCount; f++)
                {
                    string fName = reader.ReadString();
                    var fType = (MultiMetricType)reader.ReadByte();
                    fields.Add(new MultiMetricField(fName, fType));
                }

                int rowCount = reader.ReadInt32();
                var schema = new MultiMetricSchema(seriesName, fields);
                var batch = new ColumnarBatch(schema, rowCount);
                batch.SetRowCount(rowCount);

                // 1. Decompress Timestamps
                int tsByteLen = reader.ReadInt32();
                byte[] tsBytes = reader.ReadBytes(tsByteLen);
                GorillaDecoder.DecodeTimestamps(tsBytes, batch.Timestamps.AsSpan(0, rowCount));

                // 2. Decompress Columns
                for (int f = 0; f < fieldCount; f++)
                {
                    int colLen = reader.ReadInt32();
                    byte[] colBytes = reader.ReadBytes(colLen);
                    DecompressColumnIntoBatch(fields[f].Type, colBytes, batch, f, rowCount);
                }

                return batch;
            }
        }

        public List<MultiMetricRow> Decompress()
        {
            if (CompressedData.Length == 0 || RowCount == 0)
            {
                return new List<MultiMetricRow>();
            }

            using (var ms = new MemoryStream(CompressedData))
            using (var reader = new BinaryReader(ms, Encoding.UTF8))
            {
                uint magic = reader.ReadUInt32();
                if (magic != MagicNumber) throw new InvalidDataException("Invalid MultiMetricBlock header magic.");

                string seriesName = reader.ReadString();
                int fieldCount = reader.ReadByte();
                var fields = new List<MultiMetricField>(fieldCount);

                for (int f = 0; f < fieldCount; f++)
                {
                    string fName = reader.ReadString();
                    var fType = (MultiMetricType)reader.ReadByte();
                    fields.Add(new MultiMetricField(fName, fType));
                }

                int rowCount = reader.ReadInt32();

                // 1. Decompress Timestamps
                int tsByteLen = reader.ReadInt32();
                byte[] tsBytes = reader.ReadBytes(tsByteLen);
                long[] timestamps = DecompressTimestamps(tsBytes, rowCount);

                // 2. Decompress Columns
                var columnValues = new object?[fieldCount][];
                for (int f = 0; f < fieldCount; f++)
                {
                    int colLen = reader.ReadInt32();
                    byte[] colBytes = reader.ReadBytes(colLen);
                    columnValues[f] = DecompressColumn(fields[f].Type, colBytes, rowCount);
                }

                // 3. Assemble Rows
                var rows = new List<MultiMetricRow>(rowCount);
                for (int r = 0; r < rowCount; r++)
                {
                    var values = new object?[fieldCount];
                    for (int f = 0; f < fieldCount; f++)
                    {
                        values[f] = columnValues[f][r];
                    }
                    rows.Add(new MultiMetricRow(timestamps[r], values));
                }

                return rows;
            }
        }

        #region Internal Column Codecs

        private static byte[] CompressColumnFromBatch(MultiMetricType type, ColumnarBatch batch, int fieldIdx)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms, Encoding.UTF8))
            {
                int rowCount = batch.RowCount;
                switch (type)
                {
                    case MultiMetricType.Float64:
                        return GorillaEncoder.EncodeValues(batch.GetFloat64Span(fieldIdx));

                    case MultiMetricType.Int32:
                        var i32Col = batch.GetInt32Column(fieldIdx);
                        Span<byte> varIntBuf = stackalloc byte[10];
                        for (int i = 0; i < rowCount; i++)
                        {
                            VarIntCodec.WriteVarInt32(varIntBuf, i32Col[i], out int bytesWritten);
                            for (int b = 0; b < bytesWritten; b++) writer.Write(varIntBuf[b]);
                        }
                        break;

                    case MultiMetricType.Int64:
                        var i64Col = batch.GetInt64Column(fieldIdx);
                        Span<byte> varInt64Buf = stackalloc byte[10];
                        for (int i = 0; i < rowCount; i++)
                        {
                            VarIntCodec.WriteVarInt64(varInt64Buf, i64Col[i], out int bytesWritten);
                            for (int b = 0; b < bytesWritten; b++) writer.Write(varInt64Buf[b]);
                        }
                        break;

                    case MultiMetricType.Boolean:
                        var boolCol = batch.GetBooleanColumn(fieldIdx);
                        byte curByte = 0;
                        int bitPos = 0;
                        for (int i = 0; i < rowCount; i++)
                        {
                            if (boolCol[i]) curByte |= (byte)(1 << bitPos);
                            bitPos++;
                            if (bitPos == 8)
                            {
                                writer.Write(curByte);
                                curByte = 0;
                                bitPos = 0;
                            }
                        }
                        if (bitPos > 0) writer.Write(curByte);
                        break;

                    case MultiMetricType.Decimal:
                        var decCol = batch.GetDecimalColumn(fieldIdx);
                        for (int i = 0; i < rowCount; i++)
                        {
                            int[] bits = decimal.GetBits(decCol[i]);
                            writer.Write(bits[0]);
                            writer.Write(bits[1]);
                            writer.Write(bits[2]);
                            writer.Write(bits[3]);
                        }
                        break;

                    case MultiMetricType.String:
                        var strCol = batch.GetStringColumn(fieldIdx);
                        for (int i = 0; i < rowCount; i++)
                        {
                            writer.Write(strCol[i] ?? string.Empty);
                        }
                        break;
                }

                writer.Flush();
                return ms.ToArray();
            }
        }

        private static void DecompressColumnIntoBatch(MultiMetricType type, byte[] data, ColumnarBatch batch, int fieldIdx, int rowCount)
        {
            if (data.Length == 0 || rowCount == 0) return;

            switch (type)
            {
                case MultiMetricType.Float64:
                    GorillaDecoder.DecodeValues(data, batch.GetFloat64Span(fieldIdx));
                    break;

                case MultiMetricType.Int32:
                    var i32Col = batch.GetInt32Column(fieldIdx);
                    ReadOnlySpan<byte> span32 = data;
                    int offset32 = 0;
                    for (int i = 0; i < rowCount; i++)
                    {
                        if (offset32 < span32.Length && VarIntCodec.TryReadVarInt32(span32.Slice(offset32), out int val, out int bytesRead))
                        {
                            i32Col[i] = val;
                            offset32 += bytesRead;
                        }
                        else
                        {
                            i32Col[i] = 0;
                        }
                    }
                    break;

                case MultiMetricType.Int64:
                    var i64Col = batch.GetInt64Column(fieldIdx);
                    ReadOnlySpan<byte> span64 = data;
                    int offset64 = 0;
                    for (int i = 0; i < rowCount; i++)
                    {
                        if (offset64 < span64.Length && VarIntCodec.TryReadVarInt64(span64.Slice(offset64), out long val, out int bytesRead))
                        {
                            i64Col[i] = val;
                            offset64 += bytesRead;
                        }
                        else
                        {
                            i64Col[i] = 0L;
                        }
                    }
                    break;

                case MultiMetricType.Boolean:
                    var bCol = batch.GetBooleanColumn(fieldIdx);
                    for (int i = 0; i < rowCount; i++)
                    {
                        int byteIdx = i >> 3;
                        int bitIdx = i & 7;
                        bCol[i] = byteIdx < data.Length && (data[byteIdx] & (1 << bitIdx)) != 0;
                    }
                    break;

                case MultiMetricType.Decimal:
                    var decCol = batch.GetDecimalColumn(fieldIdx);
                    using (var ms = new MemoryStream(data))
                    using (var reader = new BinaryReader(ms))
                    {
                        int[] bits = new int[4];
                        for (int i = 0; i < rowCount; i++)
                        {
                            bits[0] = reader.ReadInt32();
                            bits[1] = reader.ReadInt32();
                            bits[2] = reader.ReadInt32();
                            bits[3] = reader.ReadInt32();
                            decCol[i] = new decimal(bits);
                        }
                    }
                    break;

                case MultiMetricType.String:
                    var strCol = batch.GetStringColumn(fieldIdx);
                    using (var ms = new MemoryStream(data))
                    using (var reader = new BinaryReader(ms, Encoding.UTF8))
                    {
                        for (int i = 0; i < rowCount; i++)
                        {
                            strCol[i] = reader.ReadString();
                        }
                    }
                    break;
            }
        }

        private static byte[] CompressTimestamps(IReadOnlyList<MultiMetricRow> rows)
        {
            var points = new List<TimeSeriesPoint>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                points.Add(new TimeSeriesPoint(rows[i].TimestampMs, 0.0));
            }
            return GorillaEncoder.Encode(points);
        }

        private static long[] DecompressTimestamps(byte[] data, int expectedCount)
        {
            var points = GorillaDecoder.Decode(data);
            var timestamps = new long[expectedCount];
            int count = Math.Min(points.Count, expectedCount);
            for (int i = 0; i < count; i++)
            {
                timestamps[i] = points[i].TimestampMs;
            }
            return timestamps;
        }

        private static byte[] CompressColumn(MultiMetricType type, IReadOnlyList<MultiMetricRow> rows, int fieldIdx)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms, Encoding.UTF8))
            {
                switch (type)
                {
                    case MultiMetricType.Float64:
                        var points = new List<TimeSeriesPoint>(rows.Count);
                        for (int i = 0; i < rows.Count; i++)
                        {
                            double v = rows[i].Values[fieldIdx] is double d ? d : Convert.ToDouble(rows[i].Values[fieldIdx]);
                            points.Add(new TimeSeriesPoint(i, v));
                        }
                        return GorillaEncoder.Encode(points);

                    case MultiMetricType.Int32:
                        Span<byte> varIntBuf = stackalloc byte[10];
                        for (int i = 0; i < rows.Count; i++)
                        {
                            int val = rows[i].Values[fieldIdx] is int iv ? iv : Convert.ToInt32(rows[i].Values[fieldIdx]);
                            VarIntCodec.WriteVarInt32(varIntBuf, val, out int bytesWritten);
                            for (int b = 0; b < bytesWritten; b++) writer.Write(varIntBuf[b]);
                        }
                        break;

                    case MultiMetricType.Int64:
                        Span<byte> varInt64Buf = stackalloc byte[10];
                        for (int i = 0; i < rows.Count; i++)
                        {
                            long val = rows[i].Values[fieldIdx] is long lv ? lv : Convert.ToInt64(rows[i].Values[fieldIdx]);
                            VarIntCodec.WriteVarInt64(varInt64Buf, val, out int bytesWritten);
                            for (int b = 0; b < bytesWritten; b++) writer.Write(varInt64Buf[b]);
                        }
                        break;

                    case MultiMetricType.Boolean:
                        byte curByte = 0;
                        int bitPos = 0;
                        for (int i = 0; i < rows.Count; i++)
                        {
                            bool b = rows[i].Values[fieldIdx] is bool bv && bv;
                            if (b) curByte |= (byte)(1 << bitPos);
                            bitPos++;
                            if (bitPos == 8)
                            {
                                writer.Write(curByte);
                                curByte = 0;
                                bitPos = 0;
                            }
                        }
                        if (bitPos > 0) writer.Write(curByte);
                        break;

                    case MultiMetricType.Decimal:
                        for (int i = 0; i < rows.Count; i++)
                        {
                            decimal d = rows[i].Values[fieldIdx] is decimal dv ? dv : Convert.ToDecimal(rows[i].Values[fieldIdx]);
                            int[] bits = decimal.GetBits(d);
                            writer.Write(bits[0]);
                            writer.Write(bits[1]);
                            writer.Write(bits[2]);
                            writer.Write(bits[3]);
                        }
                        break;

                    case MultiMetricType.String:
                        for (int i = 0; i < rows.Count; i++)
                        {
                            string s = rows[i].Values[fieldIdx]?.ToString() ?? string.Empty;
                            writer.Write(s);
                        }
                        break;
                }

                writer.Flush();
                return ms.ToArray();
            }
        }

        private static object?[] DecompressColumn(MultiMetricType type, byte[] data, int rowCount)
        {
            var result = new object?[rowCount];
            if (data.Length == 0 || rowCount == 0) return result;

            switch (type)
            {
                case MultiMetricType.Float64:
                    var points = GorillaDecoder.Decode(data);
                    for (int i = 0; i < rowCount; i++)
                    {
                        result[i] = i < points.Count ? points[i].Value : 0.0;
                    }
                    break;

                case MultiMetricType.Int32:
                    ReadOnlySpan<byte> span = data;
                    int offset = 0;
                    for (int i = 0; i < rowCount; i++)
                    {
                        if (offset < span.Length && VarIntCodec.TryReadVarInt32(span.Slice(offset), out int val, out int bytesRead))
                        {
                            result[i] = val;
                            offset += bytesRead;
                        }
                        else
                        {
                            result[i] = 0;
                        }
                    }
                    break;

                case MultiMetricType.Int64:
                    ReadOnlySpan<byte> span64 = data;
                    int offset64 = 0;
                    for (int i = 0; i < rowCount; i++)
                    {
                        if (offset64 < span64.Length && VarIntCodec.TryReadVarInt64(span64.Slice(offset64), out long val, out int bytesRead))
                        {
                            result[i] = val;
                            offset64 += bytesRead;
                        }
                        else
                        {
                            result[i] = 0L;
                        }
                    }
                    break;

                case MultiMetricType.Boolean:
                    for (int i = 0; i < rowCount; i++)
                    {
                        int byteIdx = i >> 3;
                        int bitIdx = i & 7;
                        if (byteIdx < data.Length)
                        {
                            result[i] = (data[byteIdx] & (1 << bitIdx)) != 0;
                        }
                        else
                        {
                            result[i] = false;
                        }
                    }
                    break;

                case MultiMetricType.Decimal:
                    using (var ms = new MemoryStream(data))
                    using (var reader = new BinaryReader(ms))
                    {
                        int[] bits = new int[4];
                        for (int i = 0; i < rowCount; i++)
                        {
                            bits[0] = reader.ReadInt32();
                            bits[1] = reader.ReadInt32();
                            bits[2] = reader.ReadInt32();
                            bits[3] = reader.ReadInt32();
                            result[i] = new decimal(bits);
                        }
                    }
                    break;

                case MultiMetricType.String:
                    using (var ms = new MemoryStream(data))
                    using (var reader = new BinaryReader(ms, Encoding.UTF8))
                    {
                        for (int i = 0; i < rowCount; i++)
                        {
                            result[i] = reader.ReadString();
                        }
                    }
                    break;
            }

            return result;
        }

        #endregion
    }
}
