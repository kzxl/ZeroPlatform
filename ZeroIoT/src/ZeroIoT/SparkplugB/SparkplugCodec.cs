using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ZeroIoT.SparkplugB
{
    /// <summary>
    /// Sparkplug B Payload container with timestamp, sequence number, and metrics list.
    /// </summary>
    public class SparkplugPayload
    {
        public ulong Timestamp { get; set; }
        public ulong SequenceNumber { get; set; }
        public List<SparkplugMetric> Metrics { get; } = new List<SparkplugMetric>();

        public SparkplugPayload(ulong? seq = null, ulong? timestamp = null)
        {
            SequenceNumber = seq ?? 0;
            Timestamp = timestamp ?? (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public void AddMetric(SparkplugMetric metric)
        {
            if (metric != null) Metrics.Add(metric);
        }

        public SparkplugMetric? GetMetric(string name)
        {
            for (int i = 0; i < Metrics.Count; i++)
            {
                if (string.Equals(Metrics[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return Metrics[i];
            }
            return null;
        }
    }

    /// <summary>
    /// Pure C# Protocol Buffers encoder and decoder for Sparkplug B specification payloads.
    /// </summary>
    public static class SparkplugCodec
    {
        /// <summary>
        /// Formats standard Sparkplug B topic.
        /// </summary>
        public static string FormatTopic(string groupId, string messageType, string edgeNodeId, string? deviceId = null)
        {
            if (string.IsNullOrEmpty(deviceId))
                return $"spBv1.0/{groupId}/{messageType}/{edgeNodeId}";
            return $"spBv1.0/{groupId}/{messageType}/{edgeNodeId}/{deviceId}";
        }

        /// <summary>
        /// Encodes a SparkplugPayload into pure Protobuf binary bytes.
        /// </summary>
        public static byte[] Encode(SparkplugPayload payload)
        {
            using var ms = new MemoryStream();

            // Field 1: timestamp (uint64, varint)
            WriteTag(ms, 1, 0);
            WriteVarint(ms, payload.Timestamp);

            // Field 2: repeated Metric (length-delimited)
            for (int i = 0; i < payload.Metrics.Count; i++)
            {
                byte[] metricBytes = EncodeMetric(payload.Metrics[i]);
                WriteTag(ms, 2, 2);
                WriteVarint(ms, (ulong)metricBytes.Length);
                ms.Write(metricBytes, 0, metricBytes.Length);
            }

            // Field 3: seq (uint64, varint)
            WriteTag(ms, 3, 0);
            WriteVarint(ms, payload.SequenceNumber);

            return ms.ToArray();
        }

        private static byte[] EncodeMetric(SparkplugMetric metric)
        {
            using var ms = new MemoryStream();

            // Field 1: name (string, length-delimited)
            if (!string.IsNullOrEmpty(metric.Name))
            {
                byte[] nameBytes = Encoding.UTF8.GetBytes(metric.Name);
                WriteTag(ms, 1, 2);
                WriteVarint(ms, (ulong)nameBytes.Length);
                ms.Write(nameBytes, 0, nameBytes.Length);
            }

            // Field 2: alias (uint64)
            if (metric.Alias.HasValue)
            {
                WriteTag(ms, 2, 0);
                WriteVarint(ms, metric.Alias.Value);
            }

            // Field 3: timestamp (uint64)
            if (metric.Timestamp > 0)
            {
                WriteTag(ms, 3, 0);
                WriteVarint(ms, metric.Timestamp);
            }

            // Field 4: datatype (uint32)
            WriteTag(ms, 4, 0);
            WriteVarint(ms, (ulong)metric.DataType);

            // Field 7: is_null (bool)
            if (metric.IsNull)
            {
                WriteTag(ms, 7, 0);
                WriteVarint(ms, 1);
            }
            else if (metric.Value != null)
            {
                switch (metric.DataType)
                {
                    case SparkplugDataType.Int8:
                    case SparkplugDataType.Int16:
                    case SparkplugDataType.Int32:
                    case SparkplugDataType.UInt8:
                    case SparkplugDataType.UInt16:
                    case SparkplugDataType.UInt32:
                        WriteTag(ms, 9, 0); // int_value
                        WriteVarint(ms, Convert.ToUInt64(metric.Value));
                        break;
                    case SparkplugDataType.Int64:
                    case SparkplugDataType.UInt64:
                    case SparkplugDataType.DateTime:
                        WriteTag(ms, 10, 0); // long_value
                        WriteVarint(ms, Convert.ToUInt64(metric.Value));
                        break;
                    case SparkplugDataType.Float:
                        WriteTag(ms, 11, 5); // float_value (32-bit)
                        byte[] fBytes = BitConverter.GetBytes(Convert.ToSingle(metric.Value));
                        ms.Write(fBytes, 0, 4);
                        break;
                    case SparkplugDataType.Double:
                        WriteTag(ms, 12, 1); // double_value (64-bit)
                        byte[] dBytes = BitConverter.GetBytes(Convert.ToDouble(metric.Value));
                        ms.Write(dBytes, 0, 8);
                        break;
                    case SparkplugDataType.Boolean:
                        WriteTag(ms, 13, 0); // boolean_value
                        WriteVarint(ms, Convert.ToBoolean(metric.Value) ? 1UL : 0UL);
                        break;
                    case SparkplugDataType.String:
                    case SparkplugDataType.Text:
                    case SparkplugDataType.UUID:
                        string s = metric.Value.ToString() ?? "";
                        byte[] sBytes = Encoding.UTF8.GetBytes(s);
                        WriteTag(ms, 14, 2); // string_value
                        WriteVarint(ms, (ulong)sBytes.Length);
                        ms.Write(sBytes, 0, sBytes.Length);
                        break;
                    case SparkplugDataType.Bytes:
                        if (metric.Value is byte[] rawBytes)
                        {
                            WriteTag(ms, 15, 2); // bytes_value
                            WriteVarint(ms, (ulong)rawBytes.Length);
                            ms.Write(rawBytes, 0, rawBytes.Length);
                        }
                        break;
                }
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Decodes a Protobuf binary payload into a SparkplugPayload.
        /// </summary>
        public static SparkplugPayload Decode(ReadOnlySpan<byte> data)
        {
            var payload = new SparkplugPayload();
            int offset = 0;

            while (offset < data.Length)
            {
                if (!TryReadVarint(data, ref offset, out ulong tagRaw)) break;
                int fieldNumber = (int)(tagRaw >> 3);
                int wireType = (int)(tagRaw & 0x07);

                switch (fieldNumber)
                {
                    case 1: // timestamp
                        if (TryReadVarint(data, ref offset, out ulong ts))
                            payload.Timestamp = ts;
                        break;
                    case 2: // metrics (length-delimited)
                        if (TryReadVarint(data, ref offset, out ulong metricLen))
                        {
                            int len = (int)metricLen;
                            if (offset + len <= data.Length)
                            {
                                var metric = DecodeMetric(data.Slice(offset, len));
                                payload.AddMetric(metric);
                                offset += len;
                            }
                        }
                        break;
                    case 3: // seq
                        if (TryReadVarint(data, ref offset, out ulong seq))
                            payload.SequenceNumber = seq;
                        break;
                    default:
                        SkipField(data, ref offset, wireType);
                        break;
                }
            }

            return payload;
        }

        private static SparkplugMetric DecodeMetric(ReadOnlySpan<byte> data)
        {
            string name = string.Empty;
            ulong? alias = null;
            ulong timestamp = 0;
            SparkplugDataType dataType = SparkplugDataType.Unknown;
            object? value = null;
            bool isNull = false;

            int offset = 0;
            while (offset < data.Length)
            {
                if (!TryReadVarint(data, ref offset, out ulong tagRaw)) break;
                int fieldNumber = (int)(tagRaw >> 3);
                int wireType = (int)(tagRaw & 0x07);

                switch (fieldNumber)
                {
                    case 1: // name
                        if (TryReadVarint(data, ref offset, out ulong nameLen))
                        {
                            name = Encoding.UTF8.GetString(data.Slice(offset, (int)nameLen).ToArray());
                            offset += (int)nameLen;
                        }
                        break;
                    case 2: // alias
                        if (TryReadVarint(data, ref offset, out ulong al)) alias = al;
                        break;
                    case 3: // timestamp
                        if (TryReadVarint(data, ref offset, out ulong ts)) timestamp = ts;
                        break;
                    case 4: // datatype
                        if (TryReadVarint(data, ref offset, out ulong dt)) dataType = (SparkplugDataType)dt;
                        break;
                    case 7: // is_null
                        if (TryReadVarint(data, ref offset, out ulong nul)) isNull = nul != 0;
                        break;
                    case 9: // int_value
                        if (TryReadVarint(data, ref offset, out ulong iv)) value = (int)iv;
                        break;
                    case 10: // long_value
                        if (TryReadVarint(data, ref offset, out ulong lv)) value = (long)lv;
                        break;
                    case 11: // float_value (32-bit)
                        if (offset + 4 <= data.Length)
                        {
                            value = BitConverter.ToSingle(data.Slice(offset, 4).ToArray(), 0);
                            offset += 4;
                        }
                        break;
                    case 12: // double_value (64-bit)
                        if (offset + 8 <= data.Length)
                        {
                            value = BitConverter.ToDouble(data.Slice(offset, 8).ToArray(), 0);
                            offset += 8;
                        }
                        break;
                    case 13: // boolean_value
                        if (TryReadVarint(data, ref offset, out ulong bv)) value = bv != 0;
                        break;
                    case 14: // string_value
                        if (TryReadVarint(data, ref offset, out ulong sLen))
                        {
                            value = Encoding.UTF8.GetString(data.Slice(offset, (int)sLen).ToArray());
                            offset += (int)sLen;
                        }
                        break;
                    case 15: // bytes_value
                        if (TryReadVarint(data, ref offset, out ulong bLen))
                        {
                            value = data.Slice(offset, (int)bLen).ToArray();
                            offset += (int)bLen;
                        }
                        break;
                    default:
                        SkipField(data, ref offset, wireType);
                        break;
                }
            }

            var metric = new SparkplugMetric(name, dataType, isNull ? null : value, timestamp);
            metric.Alias = alias;
            metric.IsNull = isNull;
            return metric;
        }

        private static void WriteTag(Stream stream, int fieldNumber, int wireType)
        {
            ulong tag = (ulong)((fieldNumber << 3) | wireType);
            WriteVarint(stream, tag);
        }

        private static void WriteVarint(Stream stream, ulong value)
        {
            while (value >= 0x80)
            {
                stream.WriteByte((byte)((value & 0x7F) | 0x80));
                value >>= 7;
            }
            stream.WriteByte((byte)(value & 0x7F));
        }

        private static bool TryReadVarint(ReadOnlySpan<byte> data, ref int offset, out ulong value)
        {
            value = 0;
            int shift = 0;
            while (offset < data.Length)
            {
                byte b = data[offset++];
                value |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return true;
                shift += 7;
                if (shift >= 64) return false;
            }
            return false;
        }

        private static void SkipField(ReadOnlySpan<byte> data, ref int offset, int wireType)
        {
            switch (wireType)
            {
                case 0: // Varint
                    TryReadVarint(data, ref offset, out _);
                    break;
                case 1: // 64-bit
                    offset += 8;
                    break;
                case 2: // Length-delimited
                    if (TryReadVarint(data, ref offset, out ulong len))
                        offset += (int)len;
                    break;
                case 5: // 32-bit
                    offset += 4;
                    break;
            }
        }
    }
}
