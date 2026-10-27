using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ZeroData.Core.Arrow
{
    /// <summary>
    /// Pure C# deserializer for Apache Arrow IPC streams and RecordBatches into native DataFrames.
    /// Eliminates external dependencies while maintaining microsecond inter-process exchange speeds.
    /// </summary>
    public static class ArrowIpcReader
    {
        private const int ContinuationMarker = -1; // 0xFFFFFFFF
        private const byte MessageTypeSchema = 1;
        private const byte MessageTypeRecordBatch = 2;

        public static DataFrame Deserialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) throw new ArgumentNullException(nameof(bytes));
            using (var ms = new MemoryStream(bytes))
            {
                return ReadFromStream(ms);
            }
        }

        public static DataFrame ReadFromStream(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
            {
                List<ArrowField>? fields = null;
                DataFrame? resultDf = null;

                while (stream.Position < stream.Length)
                {
                    int marker = reader.ReadInt32();
                    if (marker != ContinuationMarker)
                        throw new InvalidDataException("Invalid Arrow IPC continuation marker.");

                    int metaLength = reader.ReadInt32();
                    if (metaLength == 0)
                    {
                        // EOS (End of Stream)
                        break;
                    }

                    byte[] metaBytes = reader.ReadBytes(metaLength);
                    SkipPadding(reader, metaLength);

                    using (var metaMs = new MemoryStream(metaBytes))
                    using (var metaReader = new BinaryReader(metaMs, Encoding.UTF8))
                    {
                        byte msgType = metaReader.ReadByte();

                        if (msgType == MessageTypeSchema)
                        {
                            int fieldCount = metaReader.ReadInt32();
                            fields = new List<ArrowField>(fieldCount);

                            for (int i = 0; i < fieldCount; i++)
                            {
                                int nameLen = metaReader.ReadInt32();
                                byte[] nameBytes = metaReader.ReadBytes(nameLen);
                                string fieldName = Encoding.UTF8.GetString(nameBytes);
                                var typeId = (ArrowTypeId)metaReader.ReadByte();
                                bool isNullable = metaReader.ReadByte() != 0;
                                fields.Add(new ArrowField(fieldName, typeId, isNullable));
                            }
                        }
                        else if (msgType == MessageTypeRecordBatch)
                        {
                            if (fields == null)
                                throw new InvalidDataException("Encountered RecordBatch before Schema in Arrow IPC stream.");

                            int rowCount = metaReader.ReadInt32();
                            int colCount = metaReader.ReadInt32();

                            int[] colByteLengths = new int[colCount];
                            int totalBodyLength = 0;
                            for (int c = 0; c < colCount; c++)
                            {
                                colByteLengths[c] = metaReader.ReadInt32();
                                totalBodyLength += colByteLengths[c];
                            }

                            // Read Body
                            var columns = new List<IDataColumn>(colCount);
                            for (int c = 0; c < colCount; c++)
                            {
                                var field = fields[c];
                                int byteLen = colByteLengths[c];

                                switch (field.TypeId)
                                {
                                    case ArrowTypeId.Int32:
                                        int[] iData = new int[rowCount];
                                        for (int r = 0; r < rowCount; r++) iData[r] = reader.ReadInt32();
                                        columns.Add(new DataColumn<int>(field.Name, iData));
                                        break;

                                    case ArrowTypeId.Double:
                                        double[] dData = new double[rowCount];
                                        for (int r = 0; r < rowCount; r++) dData[r] = reader.ReadDouble();
                                        columns.Add(new DataColumn<double>(field.Name, dData));
                                        break;

                                    case ArrowTypeId.Float:
                                        float[] fData = new float[rowCount];
                                        for (int r = 0; r < rowCount; r++) fData[r] = reader.ReadSingle();
                                        columns.Add(new DataColumn<float>(field.Name, fData));
                                        break;

                                    case ArrowTypeId.Int64:
                                        long[] lData = new long[rowCount];
                                        for (int r = 0; r < rowCount; r++) lData[r] = reader.ReadInt64();
                                        columns.Add(new DataColumn<long>(field.Name, lData));
                                        break;

                                    case ArrowTypeId.Boolean:
                                        bool[] bData = new bool[rowCount];
                                        for (int r = 0; r < rowCount; r++) bData[r] = reader.ReadBoolean();
                                        columns.Add(new DataColumn<bool>(field.Name, bData));
                                        break;

                                    case ArrowTypeId.Date64:
                                        DateTime[] dtData = new DateTime[rowCount];
                                        for (int r = 0; r < rowCount; r++) dtData[r] = new DateTime(reader.ReadInt64());
                                        columns.Add(new DataColumn<DateTime>(field.Name, dtData));
                                        break;

                                    case ArrowTypeId.Decimal128:
                                        decimal[] decData = new decimal[rowCount];
                                        int[] bits = new int[4];
                                        for (int r = 0; r < rowCount; r++)
                                        {
                                            bits[0] = reader.ReadInt32();
                                            bits[1] = reader.ReadInt32();
                                            bits[2] = reader.ReadInt32();
                                            bits[3] = reader.ReadInt32();
                                            decData[r] = new decimal(bits);
                                        }
                                        columns.Add(new DataColumn<decimal>(field.Name, decData));
                                        break;

                                    case ArrowTypeId.Timestamp:
                                        DateTimeOffset[] dtoData = new DateTimeOffset[rowCount];
                                        for (int r = 0; r < rowCount; r++) dtoData[r] = DateTimeOffset.FromUnixTimeMilliseconds(reader.ReadInt64());
                                        columns.Add(new DataColumn<DateTimeOffset>(field.Name, dtoData));
                                        break;

                                    case ArrowTypeId.Utf8:
                                        int offsetCount = rowCount + 1;
                                        int[] offsets = new int[offsetCount];
                                        for (int o = 0; o < offsetCount; o++) offsets[o] = reader.ReadInt32();

                                        int strPayloadLen = byteLen - (offsetCount * sizeof(int));
                                        byte[] strBytes = reader.ReadBytes(strPayloadLen);

                                        string?[] sData = new string?[rowCount];
                                        for (int r = 0; r < rowCount; r++)
                                        {
                                            int start = offsets[r];
                                            int end = offsets[r + 1];
                                            int len = end - start;
                                            sData[r] = len > 0 ? Encoding.UTF8.GetString(strBytes, start, len) : string.Empty;
                                        }
                                        columns.Add(new DataColumn<string>(field.Name, sData!));
                                        break;

                                    default:
                                        if (byteLen > 0) reader.ReadBytes(byteLen);
                                        break;
                                }
                            }

                            SkipPadding(reader, totalBodyLength);
                            resultDf = new DataFrame(columns.ToArray());
                        }
                    }
                }

                return resultDf ?? new DataFrame();
            }
        }

        private static void SkipPadding(BinaryReader reader, int length)
        {
            int rem = length % 8;
            if (rem != 0)
            {
                int pad = 8 - rem;
                reader.ReadBytes(pad);
            }
        }

#pragma warning disable CA1416
        /// <summary>
        /// Reads a DataFrame from a named Memory-Mapped File with zero external process copying.
        /// </summary>
        public static DataFrame ReadFromMemoryMappedFile(string mapName)
        {
            if (string.IsNullOrEmpty(mapName)) throw new ArgumentNullException(nameof(mapName));

            using (var mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.OpenExisting(mapName))
            using (var accessor = mmf.CreateViewAccessor())
            {
                int len = accessor.ReadInt32(0);
                byte[] bytes = new byte[len];
                accessor.ReadArray(4, bytes, 0, len);
                return Deserialize(bytes);
            }
        }
#pragma warning restore CA1416
    }
}
