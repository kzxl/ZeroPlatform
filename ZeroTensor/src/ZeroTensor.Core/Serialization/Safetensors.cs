using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ZeroTensor.Core
{
    /// <summary>
    /// Metadata describing an individual tensor stored within a Safetensors container.
    /// </summary>
    public sealed class SafetensorsTensorInfo
    {
        public string Name { get; set; } = "";
        public string Dtype { get; set; } = "F32";
        public int[] Shape { get; set; } = Array.Empty<int>();
        public long StartOffset { get; set; }
        public long EndOffset { get; set; }
    }

    /// <summary>
    /// High-performance pure C# reader and writer for the Hugging Face Safetensors format.
    /// Supports zero-dependency loading and saving of modern AI model weights.
    /// </summary>
    public static class SafetensorsFile
    {
        #region Save

        /// <summary>
        /// Saves a collection of tensors into a Safetensors file.
        /// </summary>
        public static void Save<T>(string filePath, IDictionary<string, Tensor<T>> tensors) where T : unmanaged, IEquatable<T>
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (tensors == null) throw new ArgumentNullException(nameof(tensors));

            using var stream = File.Create(filePath);
            Save(stream, tensors);
        }

        /// <summary>
        /// Saves a collection of tensors into a stream in Safetensors format.
        /// </summary>
        public static void Save<T>(Stream stream, IDictionary<string, Tensor<T>> tensors) where T : unmanaged, IEquatable<T>
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (tensors == null) throw new ArgumentNullException(nameof(tensors));

            string dtype = GetSafetensorsDtype<T>();
            int elementSize = Marshal.SizeOf<T>();

            // 1. Calculate offsets and build JSON header
            var sb = new StringBuilder();
            sb.Append('{');

            long currentOffset = 0;
            bool first = true;

            foreach (var kvp in tensors)
            {
                if (!first) sb.Append(',');
                first = false;

                var name = kvp.Key;
                var tensor = kvp.Value;
                long byteSize = (long)tensor.Length * elementSize;
                long start = currentOffset;
                long end = currentOffset + byteSize;
                currentOffset = end;

                sb.Append('"').Append(name).Append("\":{");
                sb.Append("\"dtype\":\"").Append(dtype).Append("\",");
                sb.Append("\"shape\":[").Append(string.Join(",", tensor.Shape.Dimensions)).Append("],");
                sb.Append("\"data_offsets\":[").Append(start).Append(',').Append(end).Append(']');
                sb.Append('}');
            }

            sb.Append('}');

            byte[] headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
            ulong headerSize = (ulong)headerBytes.Length;

            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(headerSize);
            writer.Write(headerBytes);

            // 2. Write raw tensor buffers sequentially
            foreach (var kvp in tensors)
            {
                var contig = kvp.Value.ToContiguous();
                int bytesToWrite = contig.Length * elementSize;
                byte[] raw = new byte[bytesToWrite];
                Buffer.BlockCopy(contig.Buffer, contig.Offset * elementSize, raw, 0, bytesToWrite);
                writer.Write(raw);
            }
        }

        #endregion

        #region Load

        /// <summary>
        /// Loads all tensors of type T from a Safetensors file.
        /// </summary>
        public static Dictionary<string, Tensor<T>> Load<T>(string filePath) where T : unmanaged, IEquatable<T>
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            using var stream = File.OpenRead(filePath);
            return Load<T>(stream);
        }

        /// <summary>
        /// Loads all tensors of type T from a stream in Safetensors format.
        /// </summary>
        public static Dictionary<string, Tensor<T>> Load<T>(Stream stream) where T : unmanaged, IEquatable<T>
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            ulong headerSize = reader.ReadUInt64();
            if (headerSize > int.MaxValue)
            {
                throw new InvalidDataException($"Safetensors header size {headerSize} exceeds supported limits.");
            }

            byte[] headerBytes = reader.ReadBytes((int)headerSize);
            string jsonHeader = Encoding.UTF8.GetString(headerBytes);

            long dataBaseOffset = 8 + (long)headerSize;
            var infos = ParseSafetensorsHeader(jsonHeader);

            string expectedDtype = GetSafetensorsDtype<T>();
            int elementSize = Marshal.SizeOf<T>();
            var results = new Dictionary<string, Tensor<T>>();

            foreach (var info in infos)
            {
                if (!info.Dtype.Equals(expectedDtype, StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Skip tensors not matching requested type T
                }

                var tensor = new Tensor<T>(new TensorShape(info.Shape));
                long byteCount = info.EndOffset - info.StartOffset;
                long expectedByteCount = (long)tensor.Length * elementSize;

                if (byteCount != expectedByteCount)
                {
                    throw new InvalidDataException($"Data offset mismatch for tensor '{info.Name}': expected {expectedByteCount} bytes, got {byteCount}.");
                }

                stream.Seek(dataBaseOffset + info.StartOffset, SeekOrigin.Begin);
                byte[] raw = reader.ReadBytes((int)byteCount);
                Buffer.BlockCopy(raw, 0, tensor.Buffer, tensor.Offset * elementSize, (int)byteCount);

                results[info.Name] = tensor;
            }

            return results;
        }

        /// <summary>
        /// Loads all FP32 tensors from a Safetensors file.
        /// </summary>
        public static Dictionary<string, Tensor<float>> Load(string filePath) => Load<float>(filePath);

        #endregion

        #region Memory-Mapped Zero-Copy Loading

        /// <summary>
        /// Opens a Safetensors file with true zero-copy OS memory-mapping.
        /// Tensors are mapped directly from OS file cache into memory without heap allocation or copying.
        /// </summary>
        public static Dictionary<string, Tensor<T>> OpenMemoryMapped<T>(string filePath) where T : unmanaged, IEquatable<T>
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException("Safetensors file not found.", filePath);

            ulong headerSize;
            byte[] headerBytes;
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(fs, Encoding.UTF8))
            {
                headerSize = reader.ReadUInt64();
                if (headerSize > int.MaxValue)
                {
                    throw new InvalidDataException($"Safetensors header size {headerSize} exceeds supported limits.");
                }
                headerBytes = reader.ReadBytes((int)headerSize);
            }

            string jsonHeader = Encoding.UTF8.GetString(headerBytes);
            var infos = ParseSafetensorsHeader(jsonHeader);
            long dataBaseOffset = 8 + (long)headerSize;

            string expectedDtype = GetSafetensorsDtype<T>();
            int elementSize = Marshal.SizeOf<T>();

            var matchingInfos = new List<SafetensorsTensorInfo>();
            foreach (var info in infos)
            {
                if (info.Dtype.Equals(expectedDtype, StringComparison.OrdinalIgnoreCase))
                {
                    matchingInfos.Add(info);
                }
            }

            var mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, System.IO.MemoryMappedFiles.MemoryMappedFileAccess.Read);
            var lifetime = new SharedResourceHolder(mmf, Math.Max(matchingInfos.Count, 1));
            var results = new Dictionary<string, Tensor<T>>(matchingInfos.Count);

            foreach (var info in matchingInfos)
            {
                long byteStart = dataBaseOffset + info.StartOffset;
                long byteCount = info.EndOffset - info.StartOffset;
                int elementCount = (int)(byteCount / elementSize);

                var storage = new Storage.MemoryMappedStorage<T>(mmf, byteStart, elementCount, ownsMmf: false, sharedLifetime: lifetime);
                var shape = new TensorShape(info.Shape);
                var strides = TensorStrides.ComputeContiguousStrides(shape);
                results[info.Name] = new Tensor<T>(storage, 0, shape, strides);
            }

            return results;
        }

        /// <summary>
        /// Opens all FP32 tensors from a Safetensors file with true zero-copy OS memory-mapping.
        /// </summary>
        public static Dictionary<string, Tensor<float>> OpenMemoryMapped(string filePath) => OpenMemoryMapped<float>(filePath);

        #endregion

        #region Helpers

        private static string GetSafetensorsDtype<T>()
        {
            if (typeof(T) == typeof(float)) return "F32";
            if (typeof(T) == typeof(double)) return "F64";
            if (typeof(T) == typeof(int)) return "I32";
            if (typeof(T) == typeof(long)) return "I64";
            if (typeof(T) == typeof(short)) return "I16";
            if (typeof(T) == typeof(byte)) return "U8";
            if (typeof(T) == typeof(sbyte)) return "I8";
            if (typeof(T) == typeof(bool)) return "BOOL";

            throw new NotSupportedException($"Type {typeof(T)} is not supported in Safetensors format.");
        }

        internal static List<SafetensorsTensorInfo> ParseSafetensorsHeader(string json)
        {
            var list = new List<SafetensorsTensorInfo>();
            int idx = 0;

            while (idx < json.Length)
            {
                // Find next key: "name": { ... }
                int quote1 = json.IndexOf('"', idx);
                if (quote1 < 0) break;
                int quote2 = json.IndexOf('"', quote1 + 1);
                if (quote2 < 0) break;

                string key = json.Substring(quote1 + 1, quote2 - quote1 - 1);
                int colon = json.IndexOf(':', quote2 + 1);
                if (colon < 0) break;

                int objStart = json.IndexOf('{', colon + 1);
                if (objStart < 0) break;

                // Find matching closing brace '}' for this tensor's descriptor
                int depth = 1;
                int objEnd = objStart + 1;
                while (objEnd < json.Length && depth > 0)
                {
                    if (json[objEnd] == '{') depth++;
                    else if (json[objEnd] == '}') depth--;
                    objEnd++;
                }

                if (depth != 0) break;

                string tensorBody = json.Substring(objStart, objEnd - objStart);
                idx = objEnd;

                if (key == "__metadata__")
                {
                    continue;
                }

                var info = new SafetensorsTensorInfo { Name = key };

                // Parse dtype: "dtype": "F32"
                int dtIdx = tensorBody.IndexOf("\"dtype\"", StringComparison.Ordinal);
                if (dtIdx >= 0)
                {
                    int dtColon = tensorBody.IndexOf(':', dtIdx);
                    int dtQ1 = tensorBody.IndexOf('"', dtColon + 1);
                    int dtQ2 = tensorBody.IndexOf('"', dtQ1 + 1);
                    if (dtQ1 >= 0 && dtQ2 > dtQ1)
                    {
                        info.Dtype = tensorBody.Substring(dtQ1 + 1, dtQ2 - dtQ1 - 1);
                    }
                }

                // Parse shape: "shape": [1, 2, 3]
                int shIdx = tensorBody.IndexOf("\"shape\"", StringComparison.Ordinal);
                if (shIdx >= 0)
                {
                    int b1 = tensorBody.IndexOf('[', shIdx);
                    int b2 = tensorBody.IndexOf(']', b1 + 1);
                    if (b1 >= 0 && b2 > b1)
                    {
                        string content = tensorBody.Substring(b1 + 1, b2 - b1 - 1).Trim();
                        if (string.IsNullOrEmpty(content))
                        {
                            info.Shape = Array.Empty<int>();
                        }
                        else
                        {
                            var parts = content.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            info.Shape = new int[parts.Length];
                            for (int i = 0; i < parts.Length; i++)
                            {
                                info.Shape[i] = int.Parse(parts[i].Trim());
                            }
                        }
                    }
                }

                // Parse data_offsets: "data_offsets": [0, 100]
                int doIdx = tensorBody.IndexOf("\"data_offsets\"", StringComparison.Ordinal);
                if (doIdx >= 0)
                {
                    int b1 = tensorBody.IndexOf('[', doIdx);
                    int b2 = tensorBody.IndexOf(']', b1 + 1);
                    if (b1 >= 0 && b2 > b1)
                    {
                        string content = tensorBody.Substring(b1 + 1, b2 - b1 - 1).Trim();
                        var parts = content.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            info.StartOffset = long.Parse(parts[0].Trim());
                            info.EndOffset = long.Parse(parts[1].Trim());
                        }
                    }
                }

                list.Add(info);
            }

            return list;
        }

        #endregion
    }

    internal sealed class SharedResourceHolder : IDisposable
    {
        private readonly IDisposable _resource;
        private int _refCount;

        public SharedResourceHolder(IDisposable resource, int initialRefCount)
        {
            _resource = resource ?? throw new ArgumentNullException(nameof(resource));
            _refCount = initialRefCount;
        }

        public void Dispose()
        {
            if (System.Threading.Interlocked.Decrement(ref _refCount) == 0)
            {
                _resource.Dispose();
            }
        }
    }
}
