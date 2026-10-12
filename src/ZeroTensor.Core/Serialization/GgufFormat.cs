using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif
using ZeroTensor.Core.Storage;

namespace ZeroTensor.Core
{
    /// <summary>
    /// Supported GGML numerical tensor data types in GGUF format containers.
    /// </summary>
    public enum GgmlType : uint
    {
        F32 = 0,
        F16 = 1,
        Q4_0 = 2,
        Q4_1 = 3,
        Q5_0 = 6,
        Q5_1 = 7,
        Q8_0 = 8,
        Q8_1 = 9,
        Q2_K = 10,
        Q3_K = 11,
        Q4_K = 12,
        Q5_K = 13,
        Q6_K = 14,
        Q8_K = 15,
        I8 = 16,
        I16 = 17,
        I32 = 18,
        I64 = 19,
        F64 = 20,
        BF16 = 30
    }

    /// <summary>
    /// Metadata descriptor for a tensor stored within a GGUF container.
    /// Represents raw tensor storage metadata at ZeroPlatform Layer 2 (Tensor Substrate).
    /// </summary>
    public sealed class GgufTensorInfo
    {
        public string Name { get; set; } = "";
        public int[] Dimensions { get; set; } = Array.Empty<int>();
        public GgmlType Type { get; set; }
        public ulong RelativeOffset { get; set; }
        public long AbsoluteOffset { get; set; }
        public long ByteSize { get; set; }
    }

    /// <summary>
    /// High-level memory-mapped reader and dequantizer for GGUF model files (ZeroPlatform Layer 2).
    /// Retains shared file mapping lifetime and offers direct access to dequantized model weights.
    /// <para>
    /// <b>Architectural Boundary:</b> At Layer 2 (ZeroTensor), this class functions strictly as a binary tensor
    /// container parser and raw dequantization engine. It intentionally does NOT handle tokenizers (BPE/SentencePiece),
    /// vocabulary mappings, prompt formatting, or transformer execution graphs. High-level LLM execution and
    /// inference orchestration belong to Layer 4 (<c>ZeroLlm</c>).
    /// </para>
    /// </summary>
    public sealed class GgufArchive : IDisposable
    {
        private readonly MemoryMappedFile _mmf;
        private readonly SharedResourceHolder _lifetime;
        private readonly Dictionary<string, object> _metadata;
        private readonly List<GgufTensorInfo> _tensorInfos;
        private readonly Dictionary<string, Tensor<float>> _fp32Tensors;

        public IReadOnlyDictionary<string, object> Metadata => _metadata;
        public IReadOnlyList<GgufTensorInfo> TensorInfos => _tensorInfos;
        public IReadOnlyDictionary<string, Tensor<float>> Tensors => _fp32Tensors;

        internal GgufArchive(
            MemoryMappedFile mmf,
            SharedResourceHolder lifetime,
            Dictionary<string, object> metadata,
            List<GgufTensorInfo> tensorInfos,
            Dictionary<string, Tensor<float>> fp32Tensors)
        {
            _mmf = mmf;
            _lifetime = lifetime;
            _metadata = metadata;
            _tensorInfos = tensorInfos;
            _fp32Tensors = fp32Tensors;
        }

        /// <summary>
        /// Retrieves a float tensor by name.
        /// If the tensor is F32, returns the zero-copy memory mapped instance.
        /// If the tensor is quantized (Q4_0, Q8_0) or F16/BF16, automatically dequantizes it into a new Tensor<float>.
        /// </summary>
        public Tensor<float> GetFloatTensor(string tensorName)
        {
            if (_fp32Tensors.TryGetValue(tensorName, out var existing))
            {
                return existing;
            }

            var info = _tensorInfos.Find(t => t.Name.Equals(tensorName, StringComparison.OrdinalIgnoreCase));
            if (info == null)
            {
                throw new KeyNotFoundException($"Tensor '{tensorName}' not found in GGUF archive.");
            }

            var shape = new TensorShape(info.Dimensions);
            int totalElems = shape.TotalElements;
            var result = new Tensor<float>(shape);

            using (var accessor = _mmf.CreateViewAccessor(info.AbsoluteOffset, info.ByteSize, MemoryMappedFileAccess.Read))
            {
                unsafe
                {
                    byte* pRaw = null;
                    accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pRaw);
                    try
                    {
                        byte* pBytes = pRaw + accessor.PointerOffset;
                        fixed (float* pDst = &result.Storage.GetPinnableReference(result.Offset))
                        {
                            IntPtr ptrSrc = (IntPtr)pBytes;
                            IntPtr ptrDst = (IntPtr)pDst;

                            switch (info.Type)
                            {
                                case GgmlType.Q4_0:
                                    GgufDequantizer.DequantizeQ4_0(ptrSrc, ptrDst, totalElems);
                                    break;
                                case GgmlType.Q8_0:
                                    GgufDequantizer.DequantizeQ8_0(ptrSrc, ptrDst, totalElems);
                                    break;
                                case GgmlType.F16:
                                    GgufDequantizer.ConvertF16ToFloat(ptrSrc, ptrDst, totalElems);
                                    break;
                                case GgmlType.BF16:
                                    GgufDequantizer.ConvertBF16ToFloat(ptrSrc, ptrDst, totalElems);
                                    break;
                                default:
                                    throw new NotSupportedException($"Dequantization for GGML type {info.Type} is not supported yet.");
                            }
                        }
                    }
                    finally
                    {
                        accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Computes high-performance streaming matrix multiplication on quantized weights directly from the archive:
        /// y = x @ W^T, where W is stored in the GGUF container as Q4_0, Q8_0, or F32.
        /// Achieves true zero-copy execution without allocating gigabytes of dequantized weights in RAM.
        /// </summary>
        public Tensor<float> MatMulQuantized(Tensor<float> x, string weightName)
        {
            if (x == null) throw new ArgumentNullException(nameof(x));
            var info = _tensorInfos.Find(t => t.Name.Equals(weightName, StringComparison.OrdinalIgnoreCase));
            if (info == null) throw new KeyNotFoundException($"Tensor '{weightName}' not found in GGUF archive.");

            if (info.Dimensions.Length != 2)
                throw new InvalidOperationException($"Quantized MatMul currently requires a 2D weight matrix, but '{weightName}' has rank {info.Dimensions.Length}.");

            int n = info.Dimensions[0]; // rows (out_features)
            int k = info.Dimensions[1]; // cols (in_features)

            // If F32, fallback to standard TensorBlas.MatMul
            if (info.Type == GgmlType.F32)
            {
                var w = GetFloatTensor(weightName);
                var wT = w.Transpose(0, 1);
                return TensorBlas.MatMul(x, wT);
            }

            var xContig = x.IsContiguous ? x : x.ToContiguous();
            int xLen = xContig.Length;

            if (xLen % k != 0)
                throw new InvalidOperationException($"Activation inner dimension mismatch: activation total elements ({xLen}) is not divisible by weight in_features ({k}).");

            int batchSize = xLen / k;

            int[] outDims;
            if (x.Rank == 1)
            {
                outDims = new[] { n };
            }
            else
            {
                outDims = new int[x.Rank];
                for (int i = 0; i < x.Rank - 1; i++) outDims[i] = x.Shape[i];
                outDims[x.Rank - 1] = n;
            }

            var result = new Tensor<float>(outDims);

            using (var accessor = _mmf.CreateViewAccessor(info.AbsoluteOffset, info.ByteSize, MemoryMappedFileAccess.Read))
            {
                unsafe
                {
                    byte* pRaw = null;
                    accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pRaw);
                    try
                    {
                        byte* pBytes = pRaw + accessor.PointerOffset;
                        fixed (float* pAct = &xContig.Storage.GetPinnableReference(xContig.Offset))
                        fixed (float* pDst = &result.Storage.GetPinnableReference(result.Offset))
                        {
                            IntPtr ptrWeight = (IntPtr)pBytes;

                            for (int b = 0; b < batchSize; b++)
                            {
                                float* curAct = pAct + b * k;
                                float* curDst = pDst + b * n;

                                switch (info.Type)
                                {
                                    case GgmlType.Q4_0:
                                        GgufDequantizer.GemvQ4_0(ptrWeight, curAct, curDst, n, k);
                                        break;
                                    case GgmlType.Q8_0:
                                        GgufDequantizer.GemvQ8_0(ptrWeight, curAct, curDst, n, k);
                                        break;
                                    default:
                                        throw new NotSupportedException($"Streaming quantized MatMul for GGML type {info.Type} is not implemented yet.");
                                }
                            }
                        }
                    }
                    finally
                    {
                        accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                    }
                }
            }

            return result;
        }

        public void Dispose()
        {
            foreach (var kvp in _fp32Tensors)
            {
                kvp.Value.Dispose();
            }
            _lifetime.Dispose();
        }
    }

    /// <summary>
    /// High-performance multi-threaded dequantizer for GGML block quantization formats.
    /// </summary>
    public static unsafe class GgufDequantizer
    {
        public const int Qk4_0 = 32;
        public const int Qk8_0 = 32;

        /// <summary>
        /// Dequantizes GGML Q4_0 blocks (scale + 16 bytes = 32 nibbles) to IEEE 32-bit floats.
        /// </summary>
        public static void DequantizeQ4_0(IntPtr ptrSrc, IntPtr ptrDst, int totalElements)
        {
            int numBlocks = (totalElements + Qk4_0 - 1) / Qk4_0;

            Parallel.For(0, numBlocks, b =>
            {
                byte* pSrc = (byte*)ptrSrc;
                float* pDst = (float*)ptrDst;

                byte* blockPtr = pSrc + b * 18; // 2 bytes Half scale + 16 bytes nibbles
                ushort rawScale = Unsafe.ReadUnaligned<ushort>(blockPtr);
                Half hScale = Unsafe.As<ushort, Half>(ref rawScale);
                float d = (float)hScale;

                byte* qs = blockPtr + 2;
                int dstOffset = b * Qk4_0;
                int remaining = Math.Min(Qk4_0, totalElements - dstOffset);

                for (int j = 0; j < 16; j++)
                {
                    byte val = qs[j];
                    int x0 = (val & 0x0F) - 8;
                    int x1 = (val >> 4) - 8;

                    if (j < remaining) pDst[dstOffset + j] = x0 * d;
                    if (j + 16 < remaining) pDst[dstOffset + j + 16] = x1 * d;
                }
            });
        }

        /// <summary>
        /// Dequantizes GGML Q8_0 blocks (scale + 32 signed bytes) to IEEE 32-bit floats.
        /// </summary>
        public static void DequantizeQ8_0(IntPtr ptrSrc, IntPtr ptrDst, int totalElements)
        {
            int numBlocks = (totalElements + Qk8_0 - 1) / Qk8_0;

            Parallel.For(0, numBlocks, b =>
            {
                byte* pSrc = (byte*)ptrSrc;
                float* pDst = (float*)ptrDst;

                byte* blockPtr = pSrc + b * 34; // 2 bytes Half scale + 32 bytes signed values
                ushort rawScale = Unsafe.ReadUnaligned<ushort>(blockPtr);
                Half hScale = Unsafe.As<ushort, Half>(ref rawScale);
                float d = (float)hScale;

                sbyte* qs = (sbyte*)(blockPtr + 2);
                int dstOffset = b * Qk8_0;
                int remaining = Math.Min(Qk8_0, totalElements - dstOffset);

                for (int j = 0; j < remaining; j++)
                {
                    pDst[dstOffset + j] = qs[j] * d;
                }
            });
        }

        /// <summary>
        /// Computes On-the-fly streaming Matrix-Vector multiplication with Q4_0 quantized weights:
        /// y (1 x N) = x (1 x K) @ W_q4 (N x K)^T.
        /// Evaluates directly on raw memory-mapped pointers with zero weight dequantization and zero heap allocation.
        /// </summary>
        public static void GemvQ4_0(IntPtr ptrWeight, float* pAct, float* pDst, int nRows, int kCols)
        {
            if (kCols % Qk4_0 != 0)
                throw new ArgumentException($"Inner dimension K ({kCols}) must be a multiple of block size ({Qk4_0}).");

            int numBlocksPerRow = kCols / Qk4_0;
            const int bytesPerBlock = 18;
            int rowByteStride = numBlocksPerRow * bytesPerBlock;

#if NET8_0_OR_GREATER
            if (Avx2.IsSupported && Fma.IsSupported)
            {
                var vMask = Vector256.Create(0x0F);
                var vOffset = Vector256.Create(8);

                Parallel.For(0, nRows, r =>
                {
                    byte* pRow = (byte*)ptrWeight + r * rowByteStride;
                    float sum = 0f;
                    int actOffset = 0;

                    for (int b = 0; b < numBlocksPerRow; b++)
                    {
                        byte* blockPtr = pRow + b * bytesPerBlock;
                        ushort rawScale = Unsafe.ReadUnaligned<ushort>(blockPtr);
                        Half hScale = Unsafe.As<ushort, Half>(ref rawScale);
                        float d = (float)hScale;
                        byte* qs = blockPtr + 2;

                        var raw0 = Avx2.ConvertToVector256Int32(qs);
                        var raw1 = Avx2.ConvertToVector256Int32(qs + 8);

                        var vW0_0 = Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.And(raw0, vMask), vOffset));
                        var vW1_0 = Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.And(Avx2.ShiftRightLogical(raw0, 4), vMask), vOffset));

                        var vW0_1 = Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.And(raw1, vMask), vOffset));
                        var vW1_1 = Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.And(Avx2.ShiftRightLogical(raw1, 4), vMask), vOffset));

                        var vDot0 = Avx.Multiply(vW0_0, Vector256.Load(pAct + actOffset));
                        var vDot1 = Avx.Multiply(vW0_1, Vector256.Load(pAct + actOffset + 8));
                        var vDot2 = Fma.MultiplyAdd(vW1_0, Vector256.Load(pAct + actOffset + 16), vDot0);
                        var vDot3 = Fma.MultiplyAdd(vW1_1, Vector256.Load(pAct + actOffset + 24), vDot1);

                        sum += Vector256.Sum(vDot2 + vDot3) * d;
                        actOffset += Qk4_0;
                    }

                    pDst[r] = sum;
                });
                return;
            }
#endif

            Parallel.For(0, nRows, r =>
            {
                byte* pRow = (byte*)ptrWeight + r * rowByteStride;
                float sum = 0f;
                int actOffset = 0;

                for (int b = 0; b < numBlocksPerRow; b++)
                {
                    byte* blockPtr = pRow + b * bytesPerBlock;
                    ushort rawScale = Unsafe.ReadUnaligned<ushort>(blockPtr);
                    Half hScale = Unsafe.As<ushort, Half>(ref rawScale);
                    float d = (float)hScale;
                    byte* qs = blockPtr + 2;

                    float blockSum = 0f;
                    for (int j = 0; j < 16; j++)
                    {
                        byte val = qs[j];
                        int x0 = (val & 0x0F) - 8;
                        int x1 = (val >> 4) - 8;

                        blockSum += pAct[actOffset + j] * x0 + pAct[actOffset + j + 16] * x1;
                    }

                    sum += blockSum * d;
                    actOffset += Qk4_0;
                }

                pDst[r] = sum;
            });
        }

        /// <summary>
        /// Computes On-the-fly streaming Matrix-Vector multiplication with Q8_0 quantized weights:
        /// y (1 x N) = x (1 x K) @ W_q8 (N x K)^T.
        /// Evaluates directly on raw memory-mapped pointers with zero weight dequantization and zero heap allocation.
        /// </summary>
        public static void GemvQ8_0(IntPtr ptrWeight, float* pAct, float* pDst, int nRows, int kCols)
        {
            if (kCols % Qk8_0 != 0)
                throw new ArgumentException($"Inner dimension K ({kCols}) must be a multiple of block size ({Qk8_0}).");

            int numBlocksPerRow = kCols / Qk8_0;
            const int bytesPerBlock = 34;
            int rowByteStride = numBlocksPerRow * bytesPerBlock;

#if NET8_0_OR_GREATER
            if (Avx2.IsSupported && Fma.IsSupported)
            {
                Parallel.For(0, nRows, r =>
                {
                    byte* pRow = (byte*)ptrWeight + r * rowByteStride;
                    float sum = 0f;
                    int actOffset = 0;

                    for (int b = 0; b < numBlocksPerRow; b++)
                    {
                        byte* blockPtr = pRow + b * bytesPerBlock;
                        ushort rawScale = Unsafe.ReadUnaligned<ushort>(blockPtr);
                        Half hScale = Unsafe.As<ushort, Half>(ref rawScale);
                        float d = (float)hScale;
                        sbyte* qs = (sbyte*)(blockPtr + 2);

                        var vW0 = Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(qs));
                        var vW1 = Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(qs + 8));
                        var vW2 = Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(qs + 16));
                        var vW3 = Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(qs + 24));

                        var vAct0 = Vector256.Load(pAct + actOffset);
                        var vAct1 = Vector256.Load(pAct + actOffset + 8);
                        var vAct2 = Vector256.Load(pAct + actOffset + 16);
                        var vAct3 = Vector256.Load(pAct + actOffset + 24);

                        var vDot0 = Avx.Multiply(vW0, vAct0);
                        var vDot1 = Avx.Multiply(vW1, vAct1);
                        var vDot2 = Fma.MultiplyAdd(vW2, vAct2, vDot0);
                        var vDot3 = Fma.MultiplyAdd(vW3, vAct3, vDot1);

                        sum += Vector256.Sum(vDot2 + vDot3) * d;
                        actOffset += Qk8_0;
                    }

                    pDst[r] = sum;
                });
                return;
            }
#endif

            Parallel.For(0, nRows, r =>
            {
                byte* pRow = (byte*)ptrWeight + r * rowByteStride;
                float sum = 0f;
                int actOffset = 0;

                for (int b = 0; b < numBlocksPerRow; b++)
                {
                    byte* blockPtr = pRow + b * bytesPerBlock;
                    ushort rawScale = Unsafe.ReadUnaligned<ushort>(blockPtr);
                    Half hScale = Unsafe.As<ushort, Half>(ref rawScale);
                    float d = (float)hScale;
                    sbyte* qs = (sbyte*)(blockPtr + 2);

                    float blockSum = 0f;
                    for (int j = 0; j < 32; j++)
                    {
                        blockSum += pAct[actOffset + j] * qs[j];
                    }

                    sum += blockSum * d;
                    actOffset += Qk8_0;
                }

                pDst[r] = sum;
            });
        }

        /// <summary>
        /// Converts IEEE 754 16-bit half-precision floats to IEEE 32-bit floats.
        /// </summary>
        public static void ConvertF16ToFloat(IntPtr ptrSrc, IntPtr ptrDst, int totalElements)
        {
            Parallel.For(0, totalElements, i =>
            {
                ushort* src16 = (ushort*)ptrSrc;
                float* pDst = (float*)ptrDst;
                ushort raw = src16[i];
                Half h = Unsafe.As<ushort, Half>(ref raw);
                pDst[i] = (float)h;
            });
        }

        /// <summary>
        /// Converts Brain Floating Point (BF16) to IEEE 32-bit floats.
        /// </summary>
        public static void ConvertBF16ToFloat(IntPtr ptrSrc, IntPtr ptrDst, int totalElements)
        {
            Parallel.For(0, totalElements, i =>
            {
                ushort* srcBf16 = (ushort*)ptrSrc;
                float* pDst = (float*)ptrDst;
                uint bits = (uint)srcBf16[i] << 16;
                pDst[i] = Unsafe.As<uint, float>(ref bits);
            });
        }

        /// <summary>
        /// Computes the packed byte size for a given GGML data type and element count.
        /// </summary>
        public static long ComputeByteSize(GgmlType type, int totalElements)
        {
            switch (type)
            {
                case GgmlType.F32:
                case GgmlType.I32:
                    return (long)totalElements * 4;
                case GgmlType.F16:
                case GgmlType.BF16:
                case GgmlType.I16:
                    return (long)totalElements * 2;
                case GgmlType.I8:
                    return (long)totalElements;
                case GgmlType.Q4_0:
                    return ((long)(totalElements + Qk4_0 - 1) / Qk4_0) * 18;
                case GgmlType.Q8_0:
                    return ((long)(totalElements + Qk8_0 - 1) / Qk8_0) * 34;
                default:
                    return (long)totalElements * 4;
            }
        }
    }

    /// <summary>
    /// High-performance pure C# GGUF (GGML Universal Format) file reader.
    /// Enables zero-copy memory-mapped loading of modern open-weights LLMs (LLaMA, Qwen, Mistral).
    /// </summary>
    public static class GgufFile
    {
        private const uint GgufMagic = 0x46554747; // 'GGUF' in little-endian

        /// <summary>
        /// Opens a GGUF file with true zero-copy OS memory-mapping.
        /// FP32 tensors are directly accessible as <see cref="Tensor{float}"/> backed by OS page cache.
        /// </summary>
        public static GgufArchive OpenMemoryMapped(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException("GGUF file not found.", filePath);

            var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var tensorInfos = new List<GgufTensorInfo>();
            long dataBaseOffset;
            uint alignment = 32;

            // 1. Parse header and metadata from file stream
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(fs, Encoding.UTF8))
            {
                uint magic = reader.ReadUInt32();
                if (magic != GgufMagic)
                {
                    throw new InvalidDataException($"Invalid GGUF magic header: 0x{magic:X8}, expected 0x{GgufMagic:X8}.");
                }

                uint version = reader.ReadUInt32();
                if (version < 2 || version > 3)
                {
                    throw new NotSupportedException($"GGUF version {version} is not supported (expected 2 or 3).");
                }

                ulong tensorCount = reader.ReadUInt64();
                ulong metadataKvCount = reader.ReadUInt64();

                // Read Metadata Key-Values
                for (ulong i = 0; i < metadataKvCount; i++)
                {
                    string key = ReadGgufString(reader);
                    uint valType = reader.ReadUInt32();
                    object val = ReadGgufValue(reader, valType);
                    metadata[key] = val;

                    if (key.Equals("general.alignment", StringComparison.OrdinalIgnoreCase) && val is uint alignVal)
                    {
                        alignment = alignVal;
                    }
                }

                // Read Tensor Info structures
                for (ulong i = 0; i < tensorCount; i++)
                {
                    string name = ReadGgufString(reader);
                    uint nDims = reader.ReadUInt32();

                    // GGUF stores dimensions in reverse (Fortran / col-major order)
                    var rawDims = new ulong[nDims];
                    for (uint d = 0; d < nDims; d++) rawDims[d] = reader.ReadUInt64();

                    var csharpDims = new int[nDims];
                    for (int d = 0; d < (int)nDims; d++)
                    {
                        csharpDims[d] = (int)rawDims[nDims - 1 - (uint)d];
                    }

                    uint ggmlType = reader.ReadUInt32();
                    ulong offset = reader.ReadUInt64();

                    tensorInfos.Add(new GgufTensorInfo
                    {
                        Name = name,
                        Dimensions = csharpDims,
                        Type = (GgmlType)ggmlType,
                        RelativeOffset = offset
                    });
                }

                // Compute aligned data start offset
                long currentPos = fs.Position;
                long remainder = currentPos % alignment;
                dataBaseOffset = remainder == 0 ? currentPos : currentPos + (alignment - remainder);
            }

            // 2. Open MemoryMappedFile and create zero-copy tensors
            var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            var lifetime = new SharedResourceHolder(mmf, Math.Max(tensorInfos.Count, 1));
            var fp32Tensors = new Dictionary<string, Tensor<float>>();

            foreach (var info in tensorInfos)
            {
                info.AbsoluteOffset = dataBaseOffset + (long)info.RelativeOffset;
                var shape = new TensorShape(info.Dimensions);
                int elemCount = shape.TotalElements;
                info.ByteSize = GgufDequantizer.ComputeByteSize(info.Type, elemCount);

                if (info.Type == GgmlType.F32)
                {
                    var storage = new MemoryMappedStorage<float>(mmf, info.AbsoluteOffset, elemCount, ownsMmf: false, sharedLifetime: lifetime);
                    var strides = TensorStrides.ComputeContiguousStrides(shape);
                    fp32Tensors[info.Name] = new Tensor<float>(storage, 0, shape, strides);
                }
            }

            return new GgufArchive(mmf, lifetime, metadata, tensorInfos, fp32Tensors);
        }

        #region Helpers

        private static string ReadGgufString(BinaryReader reader)
        {
            ulong len = reader.ReadUInt64();
            if (len > int.MaxValue) throw new InvalidDataException("String length exceeds maximum supported size.");
            byte[] bytes = reader.ReadBytes((int)len);
            return Encoding.UTF8.GetString(bytes);
        }

        private static object ReadGgufValue(BinaryReader reader, uint type)
        {
            switch (type)
            {
                case 0: return reader.ReadByte();
                case 1: return reader.ReadSByte();
                case 2: return reader.ReadUInt16();
                case 3: return reader.ReadInt16();
                case 4: return reader.ReadUInt32();
                case 5: return reader.ReadInt32();
                case 6: return reader.ReadSingle();
                case 7: return reader.ReadBoolean();
                case 8: return ReadGgufString(reader);
                case 9: // Array
                    uint itemType = reader.ReadUInt32();
                    ulong count = reader.ReadUInt64();
                    var list = new object[count];
                    for (ulong i = 0; i < count; i++)
                    {
                        list[i] = ReadGgufValue(reader, itemType);
                    }
                    return list;
                case 10: return reader.ReadUInt64();
                case 11: return reader.ReadInt64();
                case 12: return reader.ReadDouble();
                default:
                    throw new NotSupportedException($"Unsupported GGUF metadata value type: {type}.");
            }
        }

        #endregion
    }
}
