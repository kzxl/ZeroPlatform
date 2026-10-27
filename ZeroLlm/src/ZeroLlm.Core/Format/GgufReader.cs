using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ZeroLlm.Core.Format
{
    /// <summary>
    /// Pure C# high-speed binary parser for GGUF (v2 and v3) model files.
    /// </summary>
    public sealed class GgufReader
    {
        public const uint GgufMagic = 0x46554747; // 'GGUF' in Little-Endian

        public uint Version { get; private set; }
        public ulong TensorCount { get; private set; }
        public ulong MetadataCount { get; private set; }
        public GgufMetadata Metadata { get; } = new GgufMetadata();
        public List<GgufTensorInfo> Tensors { get; } = new List<GgufTensorInfo>();
        public long TensorDataOffset { get; private set; }
        public uint Alignment { get; private set; } = 32;

        public static GgufReader FromStream(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            var reader = new GgufReader();
            reader.Read(stream);
            return reader;
        }

        public static GgufReader FromFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return FromStream(fs);
            }
        }

        private void Read(Stream stream)
        {
            using (var br = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
            {
                // 1. Magic
                var magic = br.ReadUInt32();
                if (magic != GgufMagic)
                {
                    throw new InvalidDataException($"Invalid GGUF magic header. Expected 0x{GgufMagic:X8}, got 0x{magic:X8}.");
                }

                // 2. Version
                Version = br.ReadUInt32();
                if (Version != 2 && Version != 3)
                {
                    throw new NotSupportedException($"Unsupported GGUF version: {Version}. Supported versions: 2, 3.");
                }

                // 3. Tensor count & Metadata count
                TensorCount = br.ReadUInt64();
                MetadataCount = br.ReadUInt64();

                // 4. Metadata KV pairs
                for (ulong i = 0; i < MetadataCount; i++)
                {
                    var key = ReadString(br);
                    var valType = (GgufMetadataValueType)br.ReadUInt32();
                    var val = ReadValue(br, valType);
                    Metadata.Set(key, val);
                }

                Alignment = Metadata.GetValueOrDefault("general.alignment", 32u);

                // 5. Tensor Infos
                Tensors.Clear();
                for (ulong i = 0; i < TensorCount; i++)
                {
                    var name = ReadString(br);
                    var nDims = br.ReadUInt32();
                    var dims = new ulong[nDims];
                    for (uint d = 0; d < nDims; d++)
                    {
                        dims[d] = br.ReadUInt64();
                    }

                    var tensorType = (GgufTensorType)br.ReadUInt32();
                    var offset = br.ReadUInt64();

                    Tensors.Add(new GgufTensorInfo(name, dims, tensorType, offset));
                }

                // 6. Align to tensor data start
                long currentPos = stream.Position;
                long alignedPos = (currentPos + Alignment - 1) / Alignment * Alignment;
                TensorDataOffset = alignedPos;
            }
        }

        private static string ReadString(BinaryReader br)
        {
            ulong len = br.ReadUInt64();
            if (len == 0) return string.Empty;
            if (len > int.MaxValue) throw new InvalidDataException("String length exceeds Int32.MaxValue.");

            byte[] bytes = br.ReadBytes((int)len);
            return Encoding.UTF8.GetString(bytes);
        }

        private static object ReadValue(BinaryReader br, GgufMetadataValueType type)
        {
            switch (type)
            {
                case GgufMetadataValueType.UInt8:
                    return br.ReadByte();
                case GgufMetadataValueType.Int8:
                    return br.ReadSByte();
                case GgufMetadataValueType.UInt16:
                    return br.ReadUInt16();
                case GgufMetadataValueType.Int16:
                    return br.ReadInt16();
                case GgufMetadataValueType.UInt32:
                    return br.ReadUInt32();
                case GgufMetadataValueType.Int32:
                    return br.ReadInt32();
                case GgufMetadataValueType.Float32:
                    return br.ReadSingle();
                case GgufMetadataValueType.Bool:
                    return br.ReadByte() != 0;
                case GgufMetadataValueType.String:
                    return ReadString(br);
                case GgufMetadataValueType.UInt64:
                    return br.ReadUInt64();
                case GgufMetadataValueType.Int64:
                    return br.ReadInt64();
                case GgufMetadataValueType.Float64:
                    return br.ReadDouble();
                case GgufMetadataValueType.Array:
                    var elemType = (GgufMetadataValueType)br.ReadUInt32();
                    var count = br.ReadUInt64();
                    if (count > int.MaxValue) throw new InvalidDataException("Array element count exceeds Int32.MaxValue.");
                    var arr = new object[(int)count];
                    for (int i = 0; i < (int)count; i++)
                    {
                        arr[i] = ReadValue(br, elemType);
                    }
                    return arr;
                default:
                    throw new NotSupportedException($"Unknown GGUF metadata type: {type}");
            }
        }
    }
}
