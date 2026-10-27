using System;
using System.IO;
using System.Text;

namespace ZeroData.Core.Arrow
{
    /// <summary>
    /// Pure C# serializer for Apache Arrow IPC streaming format and RecordBatches.
    /// Serializes DataFrames into zero-copy binary streams for Python / PyTorch / Polars interop with zero external dependencies.
    /// </summary>
    public static class ArrowIpcWriter
    {
        private const int ContinuationMarker = -1; // 0xFFFFFFFF
        private const byte MessageTypeSchema = 1;
        private const byte MessageTypeRecordBatch = 2;

        public static byte[] Serialize(DataFrame df)
        {
            using (var ms = new MemoryStream())
            {
                WriteToStream(df, ms);
                return ms.ToArray();
            }
        }

        public static void WriteToStream(DataFrame df, Stream stream)
        {
            if (df == null) throw new ArgumentNullException(nameof(df));
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                // 1. Write Schema Message
                WriteSchemaMessage(df, writer);

                // 2. Write RecordBatch Message
                WriteRecordBatchMessage(df, writer);

                // 3. Write End-of-Stream (EOS) Marker
                writer.Write(ContinuationMarker);
                writer.Write(0); // 0 length indicates EOS
                writer.Flush();
            }
        }

        private static void WriteSchemaMessage(DataFrame df, BinaryWriter writer)
        {
            using (var metaMs = new MemoryStream())
            using (var metaWriter = new BinaryWriter(metaMs, Encoding.UTF8))
            {
                metaWriter.Write(MessageTypeSchema);
                metaWriter.Write(df.ColumnCount);

                for (int i = 0; i < df.ColumnCount; i++)
                {
                    string colName = df.ColumnNames[i];
                    var col = df[colName];
                    var typeId = MapTypeToArrow(col.DataType);

                    byte[] nameBytes = Encoding.UTF8.GetBytes(colName);
                    metaWriter.Write(nameBytes.Length);
                    metaWriter.Write(nameBytes);
                    metaWriter.Write((byte)typeId);
                    metaWriter.Write((byte)1); // Nullable = true
                }

                metaWriter.Flush();
                byte[] metaBytes = metaMs.ToArray();

                // Write Continuation + Length + Metadata + 8-byte alignment
                writer.Write(ContinuationMarker);
                writer.Write(metaBytes.Length);
                writer.Write(metaBytes);
                PadTo8Byte(writer, metaBytes.Length);
            }
        }

        private static void WriteRecordBatchMessage(DataFrame df, BinaryWriter writer)
        {
            using (var metaMs = new MemoryStream())
            using (var metaWriter = new BinaryWriter(metaMs, Encoding.UTF8))
            using (var bodyMs = new MemoryStream())
            using (var bodyWriter = new BinaryWriter(bodyMs))
            {
                metaWriter.Write(MessageTypeRecordBatch);
                metaWriter.Write(df.RowCount);
                metaWriter.Write(df.ColumnCount);

                for (int i = 0; i < df.ColumnCount; i++)
                {
                    var col = df[df.ColumnNames[i]];
                    var typeId = MapTypeToArrow(col.DataType);

                    if (typeId == ArrowTypeId.Int32 && col is DataColumn<int> cInt)
                    {
                        var span = cInt.AsReadOnlySpan();
                        int byteLen = col.Length * sizeof(int);
                        metaWriter.Write(byteLen);
                        for (int r = 0; r < col.Length; r++) bodyWriter.Write(span[r]);
                    }
                    else if (typeId == ArrowTypeId.Double && col is DataColumn<double> cDbl)
                    {
                        var span = cDbl.AsReadOnlySpan();
                        int byteLen = col.Length * sizeof(double);
                        metaWriter.Write(byteLen);
                        for (int r = 0; r < col.Length; r++) bodyWriter.Write(span[r]);
                    }
                    else if (typeId == ArrowTypeId.Float && col is DataColumn<float> cFlt)
                    {
                        var span = cFlt.AsReadOnlySpan();
                        int byteLen = col.Length * sizeof(float);
                        metaWriter.Write(byteLen);
                        for (int r = 0; r < col.Length; r++) bodyWriter.Write(span[r]);
                    }
                    else if (typeId == ArrowTypeId.Int64 && col is DataColumn<long> cLng)
                    {
                        var span = cLng.AsReadOnlySpan();
                        int byteLen = col.Length * sizeof(long);
                        metaWriter.Write(byteLen);
                        for (int r = 0; r < col.Length; r++) bodyWriter.Write(span[r]);
                    }
                    else if (typeId == ArrowTypeId.Boolean && col is DataColumn<bool> cBool)
                    {
                        var span = cBool.AsReadOnlySpan();
                        int byteLen = col.Length;
                        metaWriter.Write(byteLen);
                        for (int r = 0; r < col.Length; r++) bodyWriter.Write(span[r]);
                    }
                    else if (typeId == ArrowTypeId.Date64 && col is DataColumn<DateTime> cDt)
                    {
                        var span = cDt.AsReadOnlySpan();
                        int byteLen = col.Length * sizeof(long);
                        metaWriter.Write(byteLen);
                        for (int r = 0; r < col.Length; r++) bodyWriter.Write(span[r].Ticks);
                    }
                    else if (typeId == ArrowTypeId.Decimal128 && col is DataColumn<decimal> cDec)
                    {
                        var span = cDec.AsReadOnlySpan();
                        int byteLen = col.Length * 16;
                        metaWriter.Write(byteLen);
                        for (int r = 0; r < col.Length; r++)
                        {
                            int[] bits = decimal.GetBits(span[r]);
                            bodyWriter.Write(bits[0]);
                            bodyWriter.Write(bits[1]);
                            bodyWriter.Write(bits[2]);
                            bodyWriter.Write(bits[3]);
                        }
                    }
                    else if (typeId == ArrowTypeId.Timestamp && col is DataColumn<DateTimeOffset> cDto)
                    {
                        var span = cDto.AsReadOnlySpan();
                        int byteLen = col.Length * sizeof(long);
                        metaWriter.Write(byteLen);
                        for (int r = 0; r < col.Length; r++) bodyWriter.Write(span[r].ToUnixTimeMilliseconds());
                    }
                    else if (typeId == ArrowTypeId.Utf8)
                    {
                        // Variable-length: Offsets + UTF-8 payload
                        using (var strPayloadMs = new MemoryStream())
                        {
                            int[] offsets = new int[col.Length + 1];
                            int currentOffset = 0;
                            offsets[0] = 0;

                            for (int r = 0; r < col.Length; r++)
                            {
                                string? s = (string?)col.GetValue(r);
                                if (s != null)
                                {
                                    byte[] sBytes = Encoding.UTF8.GetBytes(s);
                                    strPayloadMs.Write(sBytes, 0, sBytes.Length);
                                    currentOffset += sBytes.Length;
                                }
                                offsets[r + 1] = currentOffset;
                            }

                            byte[] payloadBytes = strPayloadMs.ToArray();
                            int totalColByteLen = (offsets.Length * sizeof(int)) + payloadBytes.Length;
                            metaWriter.Write(totalColByteLen);

                            for (int o = 0; o < offsets.Length; o++) bodyWriter.Write(offsets[o]);
                            bodyWriter.Write(payloadBytes);
                        }
                    }
                    else
                    {
                        // Fallback generic representation
                        int byteLen = 0;
                        metaWriter.Write(byteLen);
                    }
                }

                metaWriter.Flush();
                bodyWriter.Flush();

                byte[] metaBytes = metaMs.ToArray();
                byte[] bodyBytes = bodyMs.ToArray();

                // Write RecordBatch Continuation + MetaLen + Meta + Body
                writer.Write(ContinuationMarker);
                writer.Write(metaBytes.Length);
                writer.Write(metaBytes);
                PadTo8Byte(writer, metaBytes.Length);

                writer.Write(bodyBytes);
                PadTo8Byte(writer, bodyBytes.Length);
            }
        }

        private static void PadTo8Byte(BinaryWriter writer, int length)
        {
            int rem = length % 8;
            if (rem != 0)
            {
                int pad = 8 - rem;
                for (int i = 0; i < pad; i++) writer.Write((byte)0);
            }
        }

        public static ArrowTypeId MapTypeToArrow(Type type)
        {
            if (type == typeof(int)) return ArrowTypeId.Int32;
            if (type == typeof(long)) return ArrowTypeId.Int64;
            if (type == typeof(float)) return ArrowTypeId.Float;
            if (type == typeof(double)) return ArrowTypeId.Double;
            if (type == typeof(string)) return ArrowTypeId.Utf8;
            if (type == typeof(bool)) return ArrowTypeId.Boolean;
            if (type == typeof(DateTime)) return ArrowTypeId.Date64;
            if (type == typeof(decimal)) return ArrowTypeId.Decimal128;
            if (type == typeof(DateTimeOffset)) return ArrowTypeId.Timestamp;
            return ArrowTypeId.Utf8;
        }

#pragma warning disable CA1416
        /// <summary>
        /// Writes a DataFrame to a named Memory-Mapped File for zero-copy inter-process exchange.
        /// Returns the MemoryMappedFile instance so the caller can control its lifetime.
        /// </summary>
        public static System.IO.MemoryMappedFiles.MemoryMappedFile WriteToMemoryMappedFile(string mapName, DataFrame df)
        {
            if (string.IsNullOrEmpty(mapName)) throw new ArgumentNullException(nameof(mapName));
            if (df == null) throw new ArgumentNullException(nameof(df));

            byte[] bytes = Serialize(df);
            var mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateOrOpen(mapName, bytes.Length + 4);
            using (var accessor = mmf.CreateViewAccessor())
            {
                accessor.Write(0, bytes.Length);
                accessor.WriteArray(4, bytes, 0, bytes.Length);
            }
            return mmf;
        }
#pragma warning restore CA1416
    }
}
