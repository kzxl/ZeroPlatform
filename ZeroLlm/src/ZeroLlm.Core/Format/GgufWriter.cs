using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ZeroLlm.Core.Format
{
    /// <summary>
    /// Binary serializer for generating standard GGUF v3 model streams.
    /// </summary>
    public sealed class GgufWriter
    {
        private readonly List<(string Key, GgufMetadataValueType Type, object Value)> _metadata = new List<(string, GgufMetadataValueType, object)>();
        private readonly List<(string Name, ulong[] Dims, GgufTensorType Type, byte[] Data)> _tensors = new List<(string, ulong[], GgufTensorType, byte[])>();
        public uint Alignment { get; set; } = 32;

        public void AddMetadata(string key, string value) => _metadata.Add((key, GgufMetadataValueType.String, value));
        public void AddMetadata(string key, uint value) => _metadata.Add((key, GgufMetadataValueType.UInt32, value));
        public void AddMetadata(string key, int value) => _metadata.Add((key, GgufMetadataValueType.Int32, value));
        public void AddMetadata(string key, ulong value) => _metadata.Add((key, GgufMetadataValueType.UInt64, value));
        public void AddMetadata(string key, float value) => _metadata.Add((key, GgufMetadataValueType.Float32, value));
        public void AddMetadata(string key, bool value) => _metadata.Add((key, GgufMetadataValueType.Bool, value));

        public void AddTensor(string name, ulong[] dims, GgufTensorType type, byte[] data)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (dims == null) throw new ArgumentNullException(nameof(dims));
            if (data == null) throw new ArgumentNullException(nameof(data));
            _tensors.Add((name, dims, type, data));
        }

        public void WriteTo(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using (var bw = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                // 1. Header
                bw.Write(GgufReader.GgufMagic);
                bw.Write((uint)3); // Version 3
                bw.Write((ulong)_tensors.Count);
                bw.Write((ulong)_metadata.Count);

                // 2. Metadata
                foreach (var (key, type, val) in _metadata)
                {
                    WriteString(bw, key);
                    bw.Write((uint)type);
                    WriteValue(bw, type, val);
                }

                // 3. Tensor Info List
                ulong currentOffset = 0;
                var tensorOffsets = new ulong[_tensors.Count];
                for (int i = 0; i < _tensors.Count; i++)
                {
                    var (name, dims, type, data) = _tensors[i];
                    WriteString(bw, name);
                    bw.Write((uint)dims.Length);
                    for (int d = 0; d < dims.Length; d++)
                    {
                        bw.Write(dims[d]);
                    }

                    bw.Write((uint)type);
                    bw.Write(currentOffset);
                    tensorOffsets[i] = currentOffset;

                    // Pad each tensor data to alignment
                    ulong dataLen = (ulong)data.Length;
                    ulong paddedLen = (dataLen + Alignment - 1) / Alignment * Alignment;
                    currentOffset += paddedLen;
                }

                // 4. Align start of tensor data
                long currentPos = stream.Position;
                long alignedPos = (currentPos + Alignment - 1) / Alignment * Alignment;
                int padCount = (int)(alignedPos - currentPos);
                if (padCount > 0)
                {
                    bw.Write(new byte[padCount]);
                }

                // 5. Tensor Binary Data
                for (int i = 0; i < _tensors.Count; i++)
                {
                    var (_, _, _, data) = _tensors[i];
                    bw.Write(data);

                    long afterData = stream.Position;
                    long nextAligned = (afterData + Alignment - 1) / Alignment * Alignment;
                    int tensorPad = (int)(nextAligned - afterData);
                    if (tensorPad > 0)
                    {
                        bw.Write(new byte[tensorPad]);
                    }
                }
            }
        }

        private static void WriteString(BinaryWriter bw, string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            bw.Write((ulong)bytes.Length);
            bw.Write(bytes);
        }

        private static void WriteValue(BinaryWriter bw, GgufMetadataValueType type, object val)
        {
            switch (type)
            {
                case GgufMetadataValueType.UInt8: bw.Write(Convert.ToByte(val)); break;
                case GgufMetadataValueType.Int8: bw.Write(Convert.ToSByte(val)); break;
                case GgufMetadataValueType.UInt16: bw.Write(Convert.ToUInt16(val)); break;
                case GgufMetadataValueType.Int16: bw.Write(Convert.ToInt16(val)); break;
                case GgufMetadataValueType.UInt32: bw.Write(Convert.ToUInt32(val)); break;
                case GgufMetadataValueType.Int32: bw.Write(Convert.ToInt32(val)); break;
                case GgufMetadataValueType.Float32: bw.Write(Convert.ToSingle(val)); break;
                case GgufMetadataValueType.Bool: bw.Write((bool)val ? (byte)1 : (byte)0); break;
                case GgufMetadataValueType.String: WriteString(bw, (string)val); break;
                case GgufMetadataValueType.UInt64: bw.Write(Convert.ToUInt64(val)); break;
                case GgufMetadataValueType.Int64: bw.Write(Convert.ToInt64(val)); break;
                case GgufMetadataValueType.Float64: bw.Write(Convert.ToDouble(val)); break;
                default: throw new NotSupportedException($"Writing metadata type {type} not supported.");
            }
        }
    }
}
