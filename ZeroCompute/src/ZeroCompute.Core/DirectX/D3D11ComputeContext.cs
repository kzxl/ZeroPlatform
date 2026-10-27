using System;
using System.Runtime.InteropServices;
using ZeroCompute.Core.Blas;
using ZeroCompute.Core.Context;
using ZeroTensor.Core;
using ZeroTensor.Core.Storage;

namespace ZeroCompute.Core.DirectX
{
    /// <summary>
    /// Direct3D 11 Compute Shader execution context for hardware-accelerated tensor mathematics.
    /// Supports persistent GPU VRAM tensor residency via <see cref="D3D11TensorStorage{T}"/>,
    /// enabling chained compute execution without host-device PCI-e memory roundtrips.
    /// Pure C# COM interop with graceful fallback to <see cref="BlasEngine"/>.
    /// </summary>
    public sealed class D3D11ComputeContext : IComputeContext, IDisposable
    {
        private IntPtr _device;
        private IntPtr _context;
        private bool _isHardwareAccelerated;
        private bool _disposed;
        private D3D11BufferPool? _bufferPool;

        public ComputeBackend Backend => ComputeBackend.Direct3D11;
        public bool IsHardwareAccelerated => _isHardwareAccelerated;
        public IntPtr DeviceHandle => _device;
        public IntPtr ContextHandle => _context;
        public D3D11BufferPool? BufferPool => _bufferPool;
        private readonly object _syncLock = new object();
        public object SyncLock => _syncLock;

        // Cached compute shaders
        private IntPtr _gemmShader;
        private IntPtr _batchedGemmShader;
        private IntPtr _vectorShader;
        private IntPtr _activationShader;
        private IntPtr _rmsNormShader;
        private IntPtr _layerNormShader;
        private IntPtr _softmaxShader;
        private IntPtr _fusedGemmShader;
        private IntPtr _fusedResRmsNormShader;
        private IntPtr _sdpaShader;
        private IntPtr _int8GemmShader;
        private IntPtr _int4GemmShader;

        // Cached constant buffers
        private IntPtr _gemmCb;
        private IntPtr _batchedGemmCb;
        private IntPtr _vectorCb;
        private IntPtr _activationCb;
        private IntPtr _rmsNormCb;
        private IntPtr _layerNormCb;
        private IntPtr _softmaxCb;
        private IntPtr _fusedGemmCb;
        private IntPtr _fusedResRmsNormCb;
        private IntPtr _sdpaCb;
        private IntPtr _quantGemmCb;

        [StructLayout(LayoutKind.Sequential, Size = 32)]
        private struct GemmCbData
        {
            public uint M;
            public uint K;
            public uint N;
            public float Alpha;
            public float Beta;
            public float Pad0;
            public float Pad1;
            public float Pad2;
        }

        [StructLayout(LayoutKind.Sequential, Size = 32)]
        private struct BatchedGemmCbData
        {
            public uint M;
            public uint K;
            public uint N;
            public uint BatchCount;
            public float Alpha;
            public float Beta;
            public float Pad0;
            public float Pad1;
        }

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct VectorCbData
        {
            public uint Count;
            public uint OpType;
            public float Pad0;
            public float Pad1;
        }

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct ActivationCbData
        {
            public uint Count;
            public uint ActType;
            public float Pad0;
            public float Pad1;
        }

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct RmsNormCbData
        {
            public uint HiddenDim;
            public uint RowCount;
            public float Epsilon;
            public uint HasWeight;
        }

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct LayerNormCbData
        {
            public uint HiddenDim;
            public uint RowCount;
            public float Epsilon;
            public uint Flags; // Bit 0: HasWeight, Bit 1: HasBias
        }

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct SoftmaxCbData
        {
            public uint RowLength;
            public uint RowCount;
            public float Pad0;
            public float Pad1;
        }

        [StructLayout(LayoutKind.Sequential, Size = 32)]
        private struct FusedGemmCbData
        {
            public uint M;
            public uint K;
            public uint N;
            public float Alpha;
            public uint HasBias;
            public uint ActType;
            public float Pad0;
            public float Pad1;
        }

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct FusedResRmsNormCbData
        {
            public uint HiddenDim;
            public uint RowCount;
            public float Epsilon;
            public uint HasWeight;
        }

        [StructLayout(LayoutKind.Sequential, Size = 32)]
        private struct SdpaCbData
        {
            public uint SeqLenQ;
            public uint SeqLenK;
            public uint HeadDim;
            public uint BatchCount;
            public float Scale;
            public uint IsCausal;
            public float Pad0;
            public float Pad1;
        }

        [StructLayout(LayoutKind.Sequential, Size = 32)]
        private struct QuantGemmCbData
        {
            public uint M;
            public uint K;
            public uint N;
            public uint HasZeroPoint;
            public float Pad0;
            public float Pad1;
            public float Pad2;
            public float Pad3;
        }

        public D3D11ComputeContext()
        {
            InitializeDevice();
        }

        private void InitializeDevice()
        {
            try
            {
                int hr = D3D11Native.D3D11CreateDevice(
                    IntPtr.Zero,
                    D3D11Native.D3D_DRIVER_TYPE_HARDWARE,
                    IntPtr.Zero,
                    0,
                    null,
                    0,
                    D3D11Native.D3D11_SDK_VERSION,
                    out _device,
                    out _,
                    out _context);

                if (hr < 0 || _device == IntPtr.Zero)
                {
                    // Fallback to WARP (software GPU acceleration)
                    hr = D3D11Native.D3D11CreateDevice(
                        IntPtr.Zero,
                        D3D11Native.D3D_DRIVER_TYPE_WARP,
                        IntPtr.Zero,
                        0,
                        null,
                        0,
                        D3D11Native.D3D11_SDK_VERSION,
                        out _device,
                        out _,
                        out _context);
                }

                _isHardwareAccelerated = (hr >= 0 && _device != IntPtr.Zero);
                if (_isHardwareAccelerated)
                {
                    _bufferPool = new D3D11BufferPool(_device, _context, _syncLock);
                }
            }
            catch
            {
                _isHardwareAccelerated = false;
                _device = IntPtr.Zero;
                _context = IntPtr.Zero;
                _bufferPool = null;
            }
        }

        #region VRAM Persistent Tensor Management

        /// <summary>
        /// Allocates a persistent GPU VRAM tensor backed by a <see cref="D3D11ComputeBuffer"/>.
        /// Successive kernel executions on this tensor remain entirely in VRAM without CPU roundtrips.
        /// </summary>
        public Tensor<T> AllocateDeviceTensor<T>(TensorShape shape, bool pooled = false) where T : unmanaged, IEquatable<T>
        {
            if (!_isHardwareAccelerated)
                throw new InvalidOperationException("Direct3D 11 device is not available on this platform/configuration.");

            var buffer = pooled
                ? RentStructuredBuffer<T>(shape.TotalElements, allowUav: true, allowSrv: true, cpuRead: true)
                : CreateStructuredBuffer<T>(shape.TotalElements, allowUav: true, cpuRead: true);

            var storage = new D3D11TensorStorage<T>(buffer, ownsBuffer: true, originPool: pooled ? _bufferPool : null);
            var strides = TensorStrides.ComputeContiguousStrides(shape);
            return new Tensor<T>(storage, 0, shape, strides);
        }

        /// <summary>
        /// Uploads a host CPU tensor to persistent GPU VRAM, returning a device-resident tensor.
        /// If the tensor is already on the GPU, returns the same instance.
        /// </summary>
        public Tensor<T> ToDevice<T>(Tensor<T> hostTensor) where T : unmanaged, IEquatable<T>
        {
            if (hostTensor == null) throw new ArgumentNullException(nameof(hostTensor));
            if (!_isHardwareAccelerated || hostTensor.Storage is D3D11TensorStorage<T>)
                return hostTensor;

            var devTensor = AllocateDeviceTensor<T>(hostTensor.Shape);
            var contig = hostTensor.IsContiguous ? hostTensor : hostTensor.ToContiguous();
            ((D3D11TensorStorage<T>)devTensor.Storage).Buffer.Upload(contig.AsReadOnlySpan());
            return devTensor;
        }

        #endregion

        #region Buffer Management & Binding Helpers

        public D3D11ComputeBuffer CreateStructuredBuffer<T>(int count, bool allowUav = true, bool cpuRead = false) where T : unmanaged
        {
            if (!_isHardwareAccelerated)
                throw new InvalidOperationException("Direct3D 11 device is not available on this platform/configuration.");

            int stride = Marshal.SizeOf(typeof(T));
            return new D3D11ComputeBuffer(_device, _context, count, stride, allowUav, true, cpuRead, _syncLock);
        }

        public D3D11ComputeBuffer RentStructuredBuffer<T>(int count, bool allowUav = true, bool allowSrv = true, bool cpuRead = false) where T : unmanaged
        {
            if (!_isHardwareAccelerated)
                throw new InvalidOperationException("Direct3D 11 device is not available on this platform/configuration.");

            int stride = Marshal.SizeOf(typeof(T));
            if (_bufferPool != null)
            {
                return _bufferPool.Rent(count, stride, allowUav, allowSrv, cpuRead);
            }
            return new D3D11ComputeBuffer(_device, _context, count, stride, allowUav, allowSrv, cpuRead, _syncLock);
        }

        public void ReturnStructuredBuffer(D3D11ComputeBuffer buffer)
        {
            if (buffer == null) return;
            if (_bufferPool != null)
            {
                _bufferPool.Return(buffer);
            }
            else
            {
                buffer.Dispose();
            }
        }

        public IntPtr CreateComputeShader(byte[] bytecode)
        {
            if (!_isHardwareAccelerated)
                throw new InvalidOperationException("Direct3D 11 device is not available.");

            int hr = D3D11Native.CreateComputeShader(_device, bytecode, out IntPtr shader);
            if (hr < 0 || shader == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to create Direct3D 11 Compute Shader (HRESULT 0x{hr:X8}).");

            return shader;
        }

        private IntPtr CreateConstantBuffer(uint byteSize)
        {
            uint alignedSize = (byteSize + 15) & ~15u;
            var desc = new D3D11Native.D3D11_BUFFER_DESC
            {
                ByteWidth = alignedSize,
                Usage = D3D11Native.D3D11_USAGE_DEFAULT,
                BindFlags = D3D11Native.D3D11_BIND_CONSTANT_BUFFER,
                CPUAccessFlags = 0,
                MiscFlags = 0,
                StructureByteStride = 0
            };

            int hr = D3D11Native.CreateBuffer(_device, ref desc, IntPtr.Zero, out IntPtr buffer);
            if (hr < 0 || buffer == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to create D3D11 Constant Buffer (0x{hr:X8})");
            return buffer;
        }

        private readonly struct BoundBufferScope : IDisposable
        {
            public D3D11ComputeBuffer Buffer { get; }
            private readonly bool _isTemporary;
            private readonly D3D11BufferPool? _pool;

            public BoundBufferScope(D3D11ComputeBuffer buffer, bool isTemporary, D3D11BufferPool? pool = null)
            {
                Buffer = buffer;
                _isTemporary = isTemporary;
                _pool = pool;
            }

            public void Dispose()
            {
                if (_isTemporary && Buffer != null)
                {
                    if (_pool != null)
                    {
                        _pool.Return(Buffer);
                    }
                    else
                    {
                        Buffer.Dispose();
                    }
                }
            }
        }

        private BoundBufferScope BindInputBuffer<T>(Tensor<T> tensor, bool allowUav = false) where T : unmanaged, IEquatable<T>
        {
            if (tensor.Storage is D3D11TensorStorage<T> devStorage)
            {
                return new BoundBufferScope(devStorage.Buffer, isTemporary: false, pool: null);
            }

            var contig = tensor.IsContiguous ? tensor : tensor.ToContiguous();
            var tempBuffer = RentStructuredBuffer<T>(contig.Length, allowUav: allowUav, allowSrv: true, cpuRead: false);
            tempBuffer.Upload(contig.AsReadOnlySpan());
            return new BoundBufferScope(tempBuffer, isTemporary: true, pool: _bufferPool);
        }

        private BoundBufferScope BindOutputBuffer<T>(Tensor<T> tensor, out bool needsDownload) where T : unmanaged, IEquatable<T>
        {
            if (tensor.Storage is D3D11TensorStorage<T> devStorage)
            {
                needsDownload = false;
                return new BoundBufferScope(devStorage.Buffer, isTemporary: false, pool: null);
            }

            needsDownload = true;
            var tempBuffer = RentStructuredBuffer<T>(tensor.Length, allowUav: true, allowSrv: true, cpuRead: true);
            return new BoundBufferScope(tempBuffer, isTemporary: true, pool: _bufferPool);
        }

        public void DispatchShader(IntPtr computeShader, int groupsX, int groupsY, int groupsZ, D3D11ComputeBuffer[] uavs, D3D11ComputeBuffer[]? srvs = null)
        {
            if (!_isHardwareAccelerated)
                throw new InvalidOperationException("Direct3D 11 device is not available.");
            if (computeShader == IntPtr.Zero)
                throw new ArgumentNullException(nameof(computeShader));

            lock (_syncLock)
            {
                // Set CS Shader (Slot 69)
                D3D11Native.CSSetShader(_context, computeShader);

                // Bind UAVs (Slot 68)
                if (uavs != null && uavs.Length > 0)
                {
                    var uavHandles = new IntPtr[uavs.Length];
                    for (int i = 0; i < uavs.Length; i++) uavHandles[i] = uavs[i].UavHandle;
                    D3D11Native.CSSetUnorderedAccessViews(_context, 0, uavHandles);
                }

                // Bind SRVs (Slot 67)
                if (srvs != null && srvs.Length > 0)
                {
                    var srvHandles = new IntPtr[srvs.Length];
                    for (int i = 0; i < srvs.Length; i++) srvHandles[i] = srvs[i].SrvHandle;
                    D3D11Native.CSSetShaderResources(_context, 0, srvHandles);
                }

                // Dispatch (Slot 41)
                D3D11Native.Dispatch(_context, (uint)groupsX, (uint)groupsY, (uint)groupsZ);

                // Unbind
                if (uavs != null && uavs.Length > 0)
                {
                    var nullUavs = new IntPtr[uavs.Length];
                    D3D11Native.CSSetUnorderedAccessViews(_context, 0, nullUavs);
                }

                if (srvs != null && srvs.Length > 0)
                {
                    var nullSrvs = new IntPtr[srvs.Length];
                    D3D11Native.CSSetShaderResources(_context, 0, nullSrvs);
                }

                D3D11Native.CSSetShader(_context, IntPtr.Zero);
            }
        }

        #endregion

        #region Pipelines Initialization

        private void EnsureGemmPipeline()
        {
            if (_gemmShader == IntPtr.Zero)
            {
                byte[] bytecode = D3D11Compiler.CompileComputeShader(D3D11Shaders.GemmShaderSource, "CSGemm");
                _gemmShader = CreateComputeShader(bytecode);
                _gemmCb = CreateConstantBuffer((uint)Marshal.SizeOf<GemmCbData>());
            }
        }

        private void EnsureBatchedGemmPipeline()
        {
            if (_batchedGemmShader == IntPtr.Zero)
            {
                byte[] bytecode = D3D11Compiler.CompileComputeShader(D3D11Shaders.BatchedGemmShaderSource, "CSBatchedGemm");
                _batchedGemmShader = CreateComputeShader(bytecode);
                _batchedGemmCb = CreateConstantBuffer((uint)Marshal.SizeOf<BatchedGemmCbData>());
            }
        }

        private void EnsureVectorPipeline()
        {
            if (_vectorShader == IntPtr.Zero)
            {
                byte[] bytecode = D3D11Compiler.CompileComputeShader(D3D11Shaders.VectorOpShaderSource, "CSVectorOp");
                _vectorShader = CreateComputeShader(bytecode);
                _vectorCb = CreateConstantBuffer((uint)Marshal.SizeOf<VectorCbData>());
            }
        }

        private void EnsureActivationPipeline()
        {
            if (_activationShader == IntPtr.Zero)
            {
                byte[] bytecode = D3D11Compiler.CompileComputeShader(D3D11Shaders.ActivationShaderSource, "CSActivation");
                _activationShader = CreateComputeShader(bytecode);
                _activationCb = CreateConstantBuffer((uint)Marshal.SizeOf<ActivationCbData>());
            }
        }

        private void EnsureRmsNormPipeline()
        {
            if (_rmsNormShader == IntPtr.Zero)
            {
                byte[] bytecode = D3D11Compiler.CompileComputeShader(D3D11Shaders.RmsNormShaderSource, "CSRmsNorm");
                _rmsNormShader = CreateComputeShader(bytecode);
                _rmsNormCb = CreateConstantBuffer((uint)Marshal.SizeOf<RmsNormCbData>());
            }
        }

        private void EnsureLayerNormPipeline()
        {
            if (_layerNormShader == IntPtr.Zero)
            {
                byte[] bytecode = D3D11Compiler.CompileComputeShader(D3D11Shaders.LayerNormShaderSource, "CSLayerNorm");
                _layerNormShader = CreateComputeShader(bytecode);
                _layerNormCb = CreateConstantBuffer((uint)Marshal.SizeOf<LayerNormCbData>());
            }
        }

        private void EnsureSoftmaxPipeline()
        {
            if (_softmaxShader == IntPtr.Zero)
            {
                byte[] bytecode = D3D11Compiler.CompileComputeShader(D3D11Shaders.SoftmaxShaderSource, "CSSoftmax");
                _softmaxShader = CreateComputeShader(bytecode);
                _softmaxCb = CreateConstantBuffer((uint)Marshal.SizeOf<SoftmaxCbData>());
            }
        }

        private void EnsureFusedGemmPipeline()
        {
            if (_fusedGemmShader == IntPtr.Zero)
            {
                byte[] bytecode = D3D11Compiler.CompileComputeShader(D3D11Shaders.FusedGemmShaderSource, "CSFusedGemm");
                _fusedGemmShader = CreateComputeShader(bytecode);
                _fusedGemmCb = CreateConstantBuffer((uint)Marshal.SizeOf<FusedGemmCbData>());
            }
        }

        private void EnsureFusedResRmsNormPipeline()
        {
            if (_fusedResRmsNormShader == IntPtr.Zero)
            {
                byte[] bytecode = D3D11Compiler.CompileComputeShader(D3D11Shaders.FusedResidualRmsNormShaderSource, "CSFusedResidualRmsNorm");
                _fusedResRmsNormShader = CreateComputeShader(bytecode);
                _fusedResRmsNormCb = CreateConstantBuffer((uint)Marshal.SizeOf<FusedResRmsNormCbData>());
            }
        }

        private void EnsureSdpaPipeline()
        {
            if (_sdpaShader == IntPtr.Zero)
            {
                byte[] bytecode = D3D11Compiler.CompileComputeShader(D3D11Shaders.SdpaAttentionShaderSource, "CSSdpaAttention");
                _sdpaShader = CreateComputeShader(bytecode);
                _sdpaCb = CreateConstantBuffer((uint)Marshal.SizeOf<SdpaCbData>());
            }
        }

        private void EnsureInt8GemmPipeline()
        {
            if (_int8GemmShader == IntPtr.Zero)
            {
                byte[] bytecode = D3D11Compiler.CompileComputeShader(D3D11Shaders.Int8GemmShaderSource, "CSInt8Gemm");
                _int8GemmShader = CreateComputeShader(bytecode);
                if (_quantGemmCb == IntPtr.Zero)
                    _quantGemmCb = CreateConstantBuffer((uint)Marshal.SizeOf<QuantGemmCbData>());
            }
        }

        private void EnsureInt4GemmPipeline()
        {
            if (_int4GemmShader == IntPtr.Zero)
            {
                byte[] bytecode = D3D11Compiler.CompileComputeShader(D3D11Shaders.Int4GemmShaderSource, "CSInt4Gemm");
                _int4GemmShader = CreateComputeShader(bytecode);
                if (_quantGemmCb == IntPtr.Zero)
                    _quantGemmCb = CreateConstantBuffer((uint)Marshal.SizeOf<QuantGemmCbData>());
            }
        }

        #endregion

        #region Mathematical Tensor Operations

        public Tensor<float> Gemm(Tensor<float> A, Tensor<float> B, float alpha = 1, float beta = 0)
        {
            if (A == null) throw new ArgumentNullException(nameof(A));
            if (B == null) throw new ArgumentNullException(nameof(B));
            if (A.Rank != 2 || B.Rank != 2)
                throw new ArgumentException("Tensors must be 2D matrices.");
            if (A.Shape[1] != B.Shape[0])
                throw new ArgumentException($"Inner dimensions must match: A is [{A.Shape[0]},{A.Shape[1]}], B is [{B.Shape[0]},{B.Shape[1]}].");

            // Allocate device-resident tensor if inputs are device tensors, otherwise regular tensor
            Tensor<float> C = (A.Storage is D3D11TensorStorage<float> || B.Storage is D3D11TensorStorage<float>)
                ? AllocateDeviceTensor<float>(new TensorShape(A.Shape[0], B.Shape[1]))
                : Tensor.Zeros<float>(A.Shape[0], B.Shape[1]);

            Gemm(A, B, C, alpha, beta);
            return C;
        }

        public unsafe void Gemm(Tensor<float> A, Tensor<float> B, Tensor<float> C, float alpha = 1, float beta = 0)
        {
            if (!_isHardwareAccelerated)
            {
                BlasEngine.Gemm(A, B, C, alpha, beta);
                return;
            }

            try
            {
                EnsureGemmPipeline();

                int M = A.Shape[0];
                int K = A.Shape[1];
                int N = B.Shape[1];

                using var scopeA = BindInputBuffer(A, allowUav: false);
                using var scopeB = BindInputBuffer(B, allowUav: false);
                using var scopeC = BindOutputBuffer(C, out bool needsDownload);

                if (beta != 0.0f && needsDownload)
                {
                    scopeC.Buffer.Upload(C.ToContiguous().AsReadOnlySpan());
                }

                var cbData = new GemmCbData
                {
                    M = (uint)M,
                    K = (uint)K,
                    N = (uint)N,
                    Alpha = alpha,
                    Beta = beta
                };
                D3D11Native.UpdateSubresource(_context, _gemmCb, (IntPtr)(&cbData));
                D3D11Native.CSSetConstantBuffers(_context, 0, _gemmCb);

                int groupsX = (N + 15) / 16;
                int groupsY = (M + 15) / 16;
                DispatchShader(_gemmShader, groupsX, groupsY, 1, new[] { scopeC.Buffer }, new[] { scopeA.Buffer, scopeB.Buffer });

                if (needsDownload)
                {
                    var cContig = C.ToContiguous();
                    scopeC.Buffer.Download(cContig.AsSpan());
                    if (!ReferenceEquals(cContig, C)) cContig.CopyTo(C);
                }
            }
            catch
            {
                BlasEngine.Gemm(A, B, C, alpha, beta);
            }
        }

        public unsafe void BatchedGemm(Tensor<float> A, Tensor<float> B, Tensor<float> C, float alpha = 1.0f, float beta = 0.0f)
        {
            if (!_isHardwareAccelerated)
            {
                BlasEngine.BatchedGemm(A, B, C, alpha, beta);
                return;
            }

            try
            {
                EnsureBatchedGemmPipeline();

                int rank = A.Rank;
                int M = A.Shape[rank - 2];
                int K = A.Shape[rank - 1];
                int N = B.Shape[rank - 1];

                int batchCount = 1;
                for (int d = 0; d < rank - 2; d++) batchCount *= A.Shape[d];

                using var scopeA = BindInputBuffer(A, allowUav: false);
                using var scopeB = BindInputBuffer(B, allowUav: false);
                using var scopeC = BindOutputBuffer(C, out bool needsDownload);

                if (beta != 0.0f && needsDownload)
                {
                    scopeC.Buffer.Upload(C.ToContiguous().AsReadOnlySpan());
                }

                var cbData = new BatchedGemmCbData
                {
                    M = (uint)M,
                    K = (uint)K,
                    N = (uint)N,
                    BatchCount = (uint)batchCount,
                    Alpha = alpha,
                    Beta = beta
                };
                D3D11Native.UpdateSubresource(_context, _batchedGemmCb, (IntPtr)(&cbData));
                D3D11Native.CSSetConstantBuffers(_context, 0, _batchedGemmCb);

                int groupsX = (N + 15) / 16;
                int groupsY = (M + 15) / 16;
                DispatchShader(_batchedGemmShader, groupsX, groupsY, batchCount, new[] { scopeC.Buffer }, new[] { scopeA.Buffer, scopeB.Buffer });

                if (needsDownload)
                {
                    var cContig = C.ToContiguous();
                    scopeC.Buffer.Download(cContig.AsSpan());
                    if (!ReferenceEquals(cContig, C)) cContig.CopyTo(C);
                }
            }
            catch
            {
                BlasEngine.BatchedGemm(A, B, C, alpha, beta);
            }
        }

        public unsafe void Add(Tensor<float> A, Tensor<float> B, Tensor<float> C)
        {
            if (!_isHardwareAccelerated)
            {
                BlasEngine.Add(A, B, C);
                return;
            }

            try
            {
                EnsureVectorPipeline();

                int count = A.Length;
                using var scopeA = BindInputBuffer(A, allowUav: false);
                using var scopeB = BindInputBuffer(B, allowUav: false);
                using var scopeC = BindOutputBuffer(C, out bool needsDownload);

                D3D11ComputeBuffer srvA = scopeA.Buffer;
                D3D11ComputeBuffer srvB = scopeB.Buffer;
                D3D11ComputeBuffer? scratchA = null;
                D3D11ComputeBuffer? scratchB = null;

                if (scopeA.Buffer.BufferHandle == scopeC.Buffer.BufferHandle)
                {
                    scratchA = CreateStructuredBuffer<float>(count, allowUav: false, cpuRead: false);
                    D3D11Native.CopyResource(_context, scratchA.BufferHandle, scopeA.Buffer.BufferHandle);
                    srvA = scratchA;
                }
                if (scopeB.Buffer.BufferHandle == scopeC.Buffer.BufferHandle)
                {
                    scratchB = CreateStructuredBuffer<float>(count, allowUav: false, cpuRead: false);
                    D3D11Native.CopyResource(_context, scratchB.BufferHandle, scopeB.Buffer.BufferHandle);
                    srvB = scratchB;
                }

                try
                {
                    var cbData = new VectorCbData { Count = (uint)count, OpType = 0 };
                    D3D11Native.UpdateSubresource(_context, _vectorCb, (IntPtr)(&cbData));
                    D3D11Native.CSSetConstantBuffers(_context, 0, _vectorCb);

                    int groups = (count + 63) / 64;
                    DispatchShader(_vectorShader, groups, 1, 1, new[] { scopeC.Buffer }, new[] { srvA, srvB });

                    if (needsDownload)
                    {
                        var cContig = C.ToContiguous();
                        scopeC.Buffer.Download(cContig.AsSpan());
                        if (!ReferenceEquals(cContig, C)) cContig.CopyTo(C);
                    }
                }
                finally
                {
                    scratchA?.Dispose();
                    scratchB?.Dispose();
                }
            }
            catch
            {
                BlasEngine.Add(A, B, C);
            }
        }

        public unsafe void Multiply(Tensor<float> A, Tensor<float> B, Tensor<float> C)
        {
            if (!_isHardwareAccelerated)
            {
                BlasEngine.Multiply(A, B, C);
                return;
            }

            try
            {
                EnsureVectorPipeline();

                int count = A.Length;
                using var scopeA = BindInputBuffer(A, allowUav: false);
                using var scopeB = BindInputBuffer(B, allowUav: false);
                using var scopeC = BindOutputBuffer(C, out bool needsDownload);

                D3D11ComputeBuffer srvA = scopeA.Buffer;
                D3D11ComputeBuffer srvB = scopeB.Buffer;
                D3D11ComputeBuffer? scratchA = null;
                D3D11ComputeBuffer? scratchB = null;

                if (scopeA.Buffer.BufferHandle == scopeC.Buffer.BufferHandle)
                {
                    scratchA = CreateStructuredBuffer<float>(count, allowUav: false, cpuRead: false);
                    D3D11Native.CopyResource(_context, scratchA.BufferHandle, scopeA.Buffer.BufferHandle);
                    srvA = scratchA;
                }
                if (scopeB.Buffer.BufferHandle == scopeC.Buffer.BufferHandle)
                {
                    scratchB = CreateStructuredBuffer<float>(count, allowUav: false, cpuRead: false);
                    D3D11Native.CopyResource(_context, scratchB.BufferHandle, scopeB.Buffer.BufferHandle);
                    srvB = scratchB;
                }

                try
                {
                    var cbData = new VectorCbData { Count = (uint)count, OpType = 1 };
                    D3D11Native.UpdateSubresource(_context, _vectorCb, (IntPtr)(&cbData));
                    D3D11Native.CSSetConstantBuffers(_context, 0, _vectorCb);

                    int groups = (count + 63) / 64;
                    DispatchShader(_vectorShader, groups, 1, 1, new[] { scopeC.Buffer }, new[] { srvA, srvB });

                    if (needsDownload)
                    {
                        var cContig = C.ToContiguous();
                        scopeC.Buffer.Download(cContig.AsSpan());
                        if (!ReferenceEquals(cContig, C)) cContig.CopyTo(C);
                    }
                }
                finally
                {
                    scratchA?.Dispose();
                    scratchB?.Dispose();
                }
            }
            catch
            {
                BlasEngine.Multiply(A, B, C);
            }
        }

        public unsafe void Activation(Tensor<float> input, Tensor<float> output, ComputeActivationType type)
        {
            if (!_isHardwareAccelerated || type == ComputeActivationType.Softmax)
            {
                BlasEngine.Activation(input, output, type);
                return;
            }

            try
            {
                EnsureActivationPipeline();

                int count = input.Length;
                using var scopeIn = BindInputBuffer(input, allowUav: false);
                using var scopeOut = BindOutputBuffer(output, out bool needsDownload);

                D3D11ComputeBuffer srvBuffer = scopeIn.Buffer;
                D3D11ComputeBuffer? scratchBuf = null;
                if (scopeIn.Buffer.BufferHandle == scopeOut.Buffer.BufferHandle)
                {
                    scratchBuf = CreateStructuredBuffer<float>(count, allowUav: false, cpuRead: false);
                    D3D11Native.CopyResource(_context, scratchBuf.BufferHandle, scopeIn.Buffer.BufferHandle);
                    srvBuffer = scratchBuf;
                }

                try
                {
                    var cbData = new ActivationCbData { Count = (uint)count, ActType = (uint)type };
                    D3D11Native.UpdateSubresource(_context, _activationCb, (IntPtr)(&cbData));
                    D3D11Native.CSSetConstantBuffers(_context, 0, _activationCb);

                    int groups = (count + 63) / 64;
                    DispatchShader(_activationShader, groups, 1, 1, new[] { scopeOut.Buffer }, new[] { srvBuffer });

                    if (needsDownload)
                    {
                        var outContig = output.ToContiguous();
                        scopeOut.Buffer.Download(outContig.AsSpan());
                        if (!ReferenceEquals(outContig, output)) outContig.CopyTo(output);
                    }
                }
                finally
                {
                    scratchBuf?.Dispose();
                }
            }
            catch
            {
                BlasEngine.Activation(input, output, type);
            }
        }

        public unsafe void RmsNorm(Tensor<float> input, Tensor<float> output, Tensor<float>? weight = null, float epsilon = 1e-5f)
        {
            if (!_isHardwareAccelerated)
            {
                BlasEngine.RmsNorm(input, output, weight, epsilon);
                return;
            }

            try
            {
                EnsureRmsNormPipeline();

                int rank = input.Rank;
                int hiddenDim = input.Shape[rank - 1];
                int rowCount = input.Length / hiddenDim;

                using var scopeIn = BindInputBuffer(input, allowUav: false);
                using var scopeOut = BindOutputBuffer(output, out bool needsDownload);

                D3D11ComputeBuffer srvBuffer = scopeIn.Buffer;
                D3D11ComputeBuffer? scratchBuf = null;
                if (scopeIn.Buffer.BufferHandle == scopeOut.Buffer.BufferHandle)
                {
                    scratchBuf = CreateStructuredBuffer<float>(input.Length, allowUav: false, cpuRead: false);
                    D3D11Native.CopyResource(_context, scratchBuf.BufferHandle, scopeIn.Buffer.BufferHandle);
                    srvBuffer = scratchBuf;
                }

                D3D11ComputeBuffer? wBuf = null;
                bool isTempW = false;
                if (weight != null)
                {
                    if (weight.Storage is D3D11TensorStorage<float> devW)
                    {
                        wBuf = devW.Buffer;
                    }
                    else
                    {
                        wBuf = CreateStructuredBuffer<float>(weight.Length, allowUav: false, cpuRead: false);
                        wBuf.Upload(weight.ToContiguous().AsReadOnlySpan());
                        isTempW = true;
                    }
                }

                try
                {
                    var cbData = new RmsNormCbData
                    {
                        HiddenDim = (uint)hiddenDim,
                        RowCount = (uint)rowCount,
                        Epsilon = epsilon,
                        HasWeight = (uint)(weight != null ? 1 : 0)
                    };
                    D3D11Native.UpdateSubresource(_context, _rmsNormCb, (IntPtr)(&cbData));
                    D3D11Native.CSSetConstantBuffers(_context, 0, _rmsNormCb);

                    var srvs = wBuf != null ? new[] { srvBuffer, wBuf } : new[] { srvBuffer };
                    DispatchShader(_rmsNormShader, rowCount, 1, 1, new[] { scopeOut.Buffer }, srvs);

                    if (needsDownload)
                    {
                        var outContig = output.ToContiguous();
                        scopeOut.Buffer.Download(outContig.AsSpan());
                        if (!ReferenceEquals(outContig, output)) outContig.CopyTo(output);
                    }
                }
                finally
                {
                    scratchBuf?.Dispose();
                    if (isTempW && wBuf != null) wBuf.Dispose();
                }
            }
            catch
            {
                BlasEngine.RmsNorm(input, output, weight, epsilon);
            }
        }

        public unsafe void LayerNorm(Tensor<float> input, Tensor<float> output, Tensor<float>? weight = null, Tensor<float>? bias = null, float epsilon = 1e-5f)
        {
            if (!_isHardwareAccelerated)
            {
                BlasEngine.LayerNorm(input, output, weight, bias, epsilon);
                return;
            }

            try
            {
                EnsureLayerNormPipeline();

                int rank = input.Rank;
                int hiddenDim = input.Shape[rank - 1];
                int rowCount = input.Length / hiddenDim;

                using var scopeIn = BindInputBuffer(input, allowUav: false);
                using var scopeOut = BindOutputBuffer(output, out bool needsDownload);

                D3D11ComputeBuffer srvBuffer = scopeIn.Buffer;
                D3D11ComputeBuffer? scratchBuf = null;
                if (scopeIn.Buffer.BufferHandle == scopeOut.Buffer.BufferHandle)
                {
                    scratchBuf = CreateStructuredBuffer<float>(input.Length, allowUav: false, cpuRead: false);
                    D3D11Native.CopyResource(_context, scratchBuf.BufferHandle, scopeIn.Buffer.BufferHandle);
                    srvBuffer = scratchBuf;
                }

                D3D11ComputeBuffer? wBuf = null;
                bool isTempW = false;
                if (weight != null)
                {
                    if (weight.Storage is D3D11TensorStorage<float> devW) wBuf = devW.Buffer;
                    else
                    {
                        wBuf = CreateStructuredBuffer<float>(weight.Length, allowUav: false, cpuRead: false);
                        wBuf.Upload(weight.ToContiguous().AsReadOnlySpan());
                        isTempW = true;
                    }
                }

                D3D11ComputeBuffer? bBuf = null;
                bool isTempB = false;
                if (bias != null)
                {
                    if (bias.Storage is D3D11TensorStorage<float> devB) bBuf = devB.Buffer;
                    else
                    {
                        bBuf = CreateStructuredBuffer<float>(bias.Length, allowUav: false, cpuRead: false);
                        bBuf.Upload(bias.ToContiguous().AsReadOnlySpan());
                        isTempB = true;
                    }
                }

                try
                {
                    uint flags = 0;
                    if (weight != null) flags |= 1;
                    if (bias != null) flags |= 2;

                    var cbData = new LayerNormCbData
                    {
                        HiddenDim = (uint)hiddenDim,
                        RowCount = (uint)rowCount,
                        Epsilon = epsilon,
                        Flags = flags
                    };
                    D3D11Native.UpdateSubresource(_context, _layerNormCb, (IntPtr)(&cbData));
                    D3D11Native.CSSetConstantBuffers(_context, 0, _layerNormCb);

                    var srvList = new System.Collections.Generic.List<D3D11ComputeBuffer> { srvBuffer };
                    if (wBuf != null) srvList.Add(wBuf);
                    if (bBuf != null) srvList.Add(bBuf);

                    DispatchShader(_layerNormShader, rowCount, 1, 1, new[] { scopeOut.Buffer }, srvList.ToArray());

                    if (needsDownload)
                    {
                        var outContig = output.ToContiguous();
                        scopeOut.Buffer.Download(outContig.AsSpan());
                        if (!ReferenceEquals(outContig, output)) outContig.CopyTo(output);
                    }
                }
                finally
                {
                    scratchBuf?.Dispose();
                    if (isTempW && wBuf != null) wBuf.Dispose();
                    if (isTempB && bBuf != null) bBuf.Dispose();
                }
            }
            catch
            {
                BlasEngine.LayerNorm(input, output, weight, bias, epsilon);
            }
        }

        public unsafe void Softmax(Tensor<float> input, Tensor<float> output, int axis = -1)
        {
            if (!_isHardwareAccelerated || (axis != -1 && axis != input.Rank - 1))
            {
                BlasEngine.Softmax(input, output, axis);
                return;
            }

            try
            {
                EnsureSoftmaxPipeline();

                int rank = input.Rank;
                int rowLength = input.Shape[rank - 1];
                int rowCount = input.Length / rowLength;

                using var scopeIn = BindInputBuffer(input, allowUav: false);
                using var scopeOut = BindOutputBuffer(output, out bool needsDownload);

                D3D11ComputeBuffer srvBuffer = scopeIn.Buffer;
                D3D11ComputeBuffer? scratchBuf = null;
                if (scopeIn.Buffer.BufferHandle == scopeOut.Buffer.BufferHandle)
                {
                    scratchBuf = CreateStructuredBuffer<float>(input.Length, allowUav: false, cpuRead: false);
                    D3D11Native.CopyResource(_context, scratchBuf.BufferHandle, scopeIn.Buffer.BufferHandle);
                    srvBuffer = scratchBuf;
                }

                try
                {
                    var cbData = new SoftmaxCbData
                    {
                        RowLength = (uint)rowLength,
                        RowCount = (uint)rowCount
                    };
                    D3D11Native.UpdateSubresource(_context, _softmaxCb, (IntPtr)(&cbData));
                    D3D11Native.CSSetConstantBuffers(_context, 0, _softmaxCb);

                    DispatchShader(_softmaxShader, rowCount, 1, 1, new[] { scopeOut.Buffer }, new[] { srvBuffer });

                    if (needsDownload)
                    {
                        var outContig = output.ToContiguous();
                        scopeOut.Buffer.Download(outContig.AsSpan());
                        if (!ReferenceEquals(outContig, output)) outContig.CopyTo(output);
                    }
                }
                finally
                {
                    scratchBuf?.Dispose();
                }
            }
            catch
            {
                BlasEngine.Softmax(input, output, axis);
            }
        }

        public Tensor<float> ReduceSum(Tensor<float> input, int axis)
        {
            return BlasEngine.ReduceSum(input, axis);
        }

        public Tensor<float> ReduceMax(Tensor<float> input, int axis)
        {
            return BlasEngine.ReduceMax(input, axis);
        }

        #region Kernel Fusion & SDPA Operations

        public unsafe void FusedGemm(
            Tensor<float> A,
            Tensor<float> B,
            Tensor<float>? bias,
            Tensor<float> C,
            ComputeActivationType activation = ComputeActivationType.None,
            float alpha = 1.0f)
        {
            if (!_isHardwareAccelerated)
            {
                BlasEngine.FusedGemm(A, B, bias, C, activation, alpha);
                return;
            }

            try
            {
                EnsureFusedGemmPipeline();

                int M = A.Shape[0];
                int K = A.Shape[1];
                int N = B.Shape[1];

                using var scopeA = BindInputBuffer(A, allowUav: false);
                using var scopeB = BindInputBuffer(B, allowUav: false);
                using var scopeC = BindOutputBuffer(C, out bool needsDownload);

                D3D11ComputeBuffer srvA = scopeA.Buffer;
                D3D11ComputeBuffer srvB = scopeB.Buffer;
                D3D11ComputeBuffer? scratchA = null;
                D3D11ComputeBuffer? scratchB = null;

                if (scopeA.Buffer.BufferHandle == scopeC.Buffer.BufferHandle)
                {
                    scratchA = CreateStructuredBuffer<float>(A.Length, allowUav: false, cpuRead: false);
                    D3D11Native.CopyResource(_context, scratchA.BufferHandle, scopeA.Buffer.BufferHandle);
                    srvA = scratchA;
                }
                if (scopeB.Buffer.BufferHandle == scopeC.Buffer.BufferHandle)
                {
                    scratchB = CreateStructuredBuffer<float>(B.Length, allowUav: false, cpuRead: false);
                    D3D11Native.CopyResource(_context, scratchB.BufferHandle, scopeB.Buffer.BufferHandle);
                    srvB = scratchB;
                }

                D3D11ComputeBuffer? biasBuf = null;
                bool isTempBias = false;
                if (bias != null)
                {
                    if (bias.Storage is D3D11TensorStorage<float> devBias)
                    {
                        biasBuf = devBias.Buffer;
                    }
                    else
                    {
                        biasBuf = CreateStructuredBuffer<float>(bias.Length, allowUav: false, cpuRead: false);
                        biasBuf.Upload(bias.ToContiguous().AsReadOnlySpan());
                        isTempBias = true;
                    }
                }

                try
                {
                    uint actVal = 0;
                    if (activation == ComputeActivationType.ReLU) actVal = 1;
                    else if (activation == ComputeActivationType.GELU) actVal = 2;
                    else if (activation == ComputeActivationType.SiLU) actVal = 3;

                    var cbData = new FusedGemmCbData
                    {
                        M = (uint)M,
                        K = (uint)K,
                        N = (uint)N,
                        Alpha = alpha,
                        HasBias = (uint)(bias != null ? 1 : 0),
                        ActType = actVal
                    };
                    D3D11Native.UpdateSubresource(_context, _fusedGemmCb, (IntPtr)(&cbData));
                    D3D11Native.CSSetConstantBuffers(_context, 0, _fusedGemmCb);

                    var srvList = new System.Collections.Generic.List<D3D11ComputeBuffer> { srvA, srvB };
                    if (biasBuf != null) srvList.Add(biasBuf);

                    int groupsX = (N + 15) / 16;
                    int groupsY = (M + 15) / 16;
                    DispatchShader(_fusedGemmShader, groupsX, groupsY, 1, new[] { scopeC.Buffer }, srvList.ToArray());

                    if (needsDownload)
                    {
                        var cContig = C.ToContiguous();
                        scopeC.Buffer.Download(cContig.AsSpan());
                        if (!ReferenceEquals(cContig, C)) cContig.CopyTo(C);
                    }
                }
                finally
                {
                    scratchA?.Dispose();
                    scratchB?.Dispose();
                    if (isTempBias && biasBuf != null) biasBuf.Dispose();
                }
            }
            catch
            {
                BlasEngine.FusedGemm(A, B, bias, C, activation, alpha);
            }
        }

        public unsafe void FusedResidualRmsNorm(
            Tensor<float> input,
            Tensor<float> residual,
            Tensor<float> output,
            Tensor<float>? weight = null,
            float epsilon = 1e-5f)
        {
            if (!_isHardwareAccelerated)
            {
                BlasEngine.FusedResidualRmsNorm(input, residual, output, weight, epsilon);
                return;
            }

            try
            {
                EnsureFusedResRmsNormPipeline();

                int rank = input.Rank;
                int hiddenDim = input.Shape[rank - 1];
                int rowCount = input.Length / hiddenDim;

                using var scopeIn = BindInputBuffer(input, allowUav: false);
                using var scopeRes = BindInputBuffer(residual, allowUav: false);
                using var scopeOut = BindOutputBuffer(output, out bool needsDownload);

                D3D11ComputeBuffer srvIn = scopeIn.Buffer;
                D3D11ComputeBuffer srvRes = scopeRes.Buffer;
                D3D11ComputeBuffer? scratchIn = null;
                D3D11ComputeBuffer? scratchRes = null;

                if (scopeIn.Buffer.BufferHandle == scopeOut.Buffer.BufferHandle)
                {
                    scratchIn = CreateStructuredBuffer<float>(input.Length, allowUav: false, cpuRead: false);
                    D3D11Native.CopyResource(_context, scratchIn.BufferHandle, scopeIn.Buffer.BufferHandle);
                    srvIn = scratchIn;
                }
                if (scopeRes.Buffer.BufferHandle == scopeOut.Buffer.BufferHandle)
                {
                    scratchRes = CreateStructuredBuffer<float>(residual.Length, allowUav: false, cpuRead: false);
                    D3D11Native.CopyResource(_context, scratchRes.BufferHandle, scopeRes.Buffer.BufferHandle);
                    srvRes = scratchRes;
                }

                D3D11ComputeBuffer? wBuf = null;
                bool isTempW = false;
                if (weight != null)
                {
                    if (weight.Storage is D3D11TensorStorage<float> devW) wBuf = devW.Buffer;
                    else
                    {
                        wBuf = CreateStructuredBuffer<float>(weight.Length, allowUav: false, cpuRead: false);
                        wBuf.Upload(weight.ToContiguous().AsReadOnlySpan());
                        isTempW = true;
                    }
                }

                try
                {
                    var cbData = new FusedResRmsNormCbData
                    {
                        HiddenDim = (uint)hiddenDim,
                        RowCount = (uint)rowCount,
                        Epsilon = epsilon,
                        HasWeight = (uint)(weight != null ? 1 : 0)
                    };
                    D3D11Native.UpdateSubresource(_context, _fusedResRmsNormCb, (IntPtr)(&cbData));
                    D3D11Native.CSSetConstantBuffers(_context, 0, _fusedResRmsNormCb);

                    var srvList = new System.Collections.Generic.List<D3D11ComputeBuffer> { srvIn, srvRes };
                    if (wBuf != null) srvList.Add(wBuf);

                    DispatchShader(_fusedResRmsNormShader, rowCount, 1, 1, new[] { scopeOut.Buffer }, srvList.ToArray());

                    if (needsDownload)
                    {
                        var outContig = output.ToContiguous();
                        scopeOut.Buffer.Download(outContig.AsSpan());
                        if (!ReferenceEquals(outContig, output)) outContig.CopyTo(output);
                    }
                }
                finally
                {
                    scratchIn?.Dispose();
                    scratchRes?.Dispose();
                    if (isTempW && wBuf != null) wBuf.Dispose();
                }
            }
            catch
            {
                BlasEngine.FusedResidualRmsNorm(input, residual, output, weight, epsilon);
            }
        }

        public void FusedResidualLayerNorm(
            Tensor<float> input,
            Tensor<float> residual,
            Tensor<float> output,
            Tensor<float>? weight = null,
            Tensor<float>? bias = null,
            float epsilon = 1e-5f)
        {
            BlasEngine.FusedResidualLayerNorm(input, residual, output, weight, bias, epsilon);
        }

        public unsafe void ScaledDotProductAttention(
            Tensor<float> Q,
            Tensor<float> K,
            Tensor<float> V,
            Tensor<float> output,
            float? scale = null,
            bool isCausal = false)
        {
            if (!_isHardwareAccelerated)
            {
                BlasEngine.ScaledDotProductAttention(Q, K, V, output, scale, isCausal);
                return;
            }

            try
            {
                EnsureSdpaPipeline();

                int rank = Q.Rank;
                int D = Q.Shape[rank - 1];
                int seqLenQ = Q.Shape[rank - 2];
                int seqLenK = K.Shape[rank - 2];

                int batchCount = 1;
                for (int d = 0; d < rank - 2; d++) batchCount *= Q.Shape[d];

                float s = scale ?? (1.0f / (float)Math.Sqrt(D));

                using var scopeQ = BindInputBuffer(Q, allowUav: false);
                using var scopeK = BindInputBuffer(K, allowUav: false);
                using var scopeV = BindInputBuffer(V, allowUav: false);
                using var scopeOut = BindOutputBuffer(output, out bool needsDownload);

                var cbData = new SdpaCbData
                {
                    SeqLenQ = (uint)seqLenQ,
                    SeqLenK = (uint)seqLenK,
                    HeadDim = (uint)D,
                    BatchCount = (uint)batchCount,
                    Scale = s,
                    IsCausal = (uint)(isCausal ? 1 : 0)
                };
                D3D11Native.UpdateSubresource(_context, _sdpaCb, (IntPtr)(&cbData));
                D3D11Native.CSSetConstantBuffers(_context, 0, _sdpaCb);

                DispatchShader(_sdpaShader, seqLenQ, batchCount, 1, new[] { scopeOut.Buffer }, new[] { scopeQ.Buffer, scopeK.Buffer, scopeV.Buffer });

                if (needsDownload)
                {
                    var outContig = output.ToContiguous();
                    scopeOut.Buffer.Download(outContig.AsSpan());
                    if (!ReferenceEquals(outContig, output)) outContig.CopyTo(output);
                }
            }
            catch
            {
                BlasEngine.ScaledDotProductAttention(Q, K, V, output, scale, isCausal);
            }
        }

        public void Gemm(Tensor<Half> A, Tensor<Half> B, Tensor<Half> C)
        {
            BlasEngine.Gemm(A, B, C);
        }

        public unsafe void GemmInt8(
            Tensor<float> A,
            Tensor<sbyte> B,
            Tensor<float> scales,
            Tensor<float> C,
            Tensor<float>? zeroPoints = null)
        {
            if (!_isHardwareAccelerated)
            {
                BlasEngine.GemmInt8(A, B, scales, C, zeroPoints);
                return;
            }

            try
            {
                EnsureInt8GemmPipeline();

                int M = A.Shape[0];
                int K = A.Shape[1];
                int N = B.Shape[1];

                using var scopeA = BindInputBuffer(A, allowUav: false);
                using var scopeS = BindInputBuffer(scales, allowUav: false);
                using var scopeC = BindOutputBuffer(C, out bool needsDownload);

                int totalBytes = K * N;
                int uintCount = (totalBytes + 3) / 4;
                var bBuf = RentStructuredBuffer<uint>(uintCount, allowUav: false, allowSrv: true, cpuRead: false);
                var bContig = B.IsContiguous ? B : B.ToContiguous();
                bBuf.UploadBytes(MemoryMarshal.Cast<sbyte, byte>(bContig.AsReadOnlySpan()));

                D3D11ComputeBuffer? zpBuf = null;
                bool hasZp = zeroPoints != null;
                if (hasZp)
                {
                    zpBuf = RentStructuredBuffer<float>(zeroPoints!.Length, allowUav: false, allowSrv: true, cpuRead: false);
                    zpBuf.Upload(zeroPoints.ToContiguous().AsReadOnlySpan());
                }
                else
                {
                    zpBuf = RentStructuredBuffer<float>(1, allowUav: false, allowSrv: true, cpuRead: false);
                }

                try
                {
                    var cbData = new QuantGemmCbData
                    {
                        M = (uint)M,
                        K = (uint)K,
                        N = (uint)N,
                        HasZeroPoint = (uint)(hasZp ? 1 : 0)
                    };
                    D3D11Native.UpdateSubresource(_context, _quantGemmCb, (IntPtr)(&cbData));
                    D3D11Native.CSSetConstantBuffers(_context, 0, _quantGemmCb);

                    int groupsX = (N + 15) / 16;
                    int groupsY = (M + 15) / 16;
                    DispatchShader(_int8GemmShader, groupsX, groupsY, 1, new[] { scopeC.Buffer }, new[] { scopeA.Buffer, bBuf, scopeS.Buffer, zpBuf });

                    if (needsDownload)
                    {
                        var cContig = C.ToContiguous();
                        scopeC.Buffer.Download(cContig.AsSpan());
                        if (!ReferenceEquals(cContig, C)) cContig.CopyTo(C);
                    }
                }
                finally
                {
                    ReturnStructuredBuffer(bBuf);
                    if (zpBuf != null) ReturnStructuredBuffer(zpBuf);
                }
            }
            catch
            {
                BlasEngine.GemmInt8(A, B, scales, C, zeroPoints);
            }
        }

        public unsafe void GemmInt4(
            Tensor<float> A,
            Tensor<byte> packedWeights,
            Tensor<float> scales,
            Tensor<float> C,
            Tensor<float>? zeroPoints = null)
        {
            if (!_isHardwareAccelerated)
            {
                BlasEngine.GemmInt4(A, packedWeights, scales, C, zeroPoints);
                return;
            }

            try
            {
                EnsureInt4GemmPipeline();

                int M = A.Shape[0];
                int K = A.Shape[1];
                int N = packedWeights.Shape[1];

                using var scopeA = BindInputBuffer(A, allowUav: false);
                using var scopeS = BindInputBuffer(scales, allowUav: false);
                using var scopeC = BindOutputBuffer(C, out bool needsDownload);

                int totalBytes = packedWeights.Length;
                int uintCount = (totalBytes + 3) / 4;
                var bBuf = RentStructuredBuffer<uint>(uintCount, allowUav: false, allowSrv: true, cpuRead: false);
                var wContig = packedWeights.IsContiguous ? packedWeights : packedWeights.ToContiguous();
                bBuf.UploadBytes(wContig.AsReadOnlySpan());

                D3D11ComputeBuffer? zpBuf = null;
                bool hasZp = zeroPoints != null;
                if (hasZp)
                {
                    zpBuf = RentStructuredBuffer<float>(zeroPoints!.Length, allowUav: false, allowSrv: true, cpuRead: false);
                    zpBuf.Upload(zeroPoints.ToContiguous().AsReadOnlySpan());
                }
                else
                {
                    zpBuf = RentStructuredBuffer<float>(1, allowUav: false, allowSrv: true, cpuRead: false);
                }

                try
                {
                    var cbData = new QuantGemmCbData
                    {
                        M = (uint)M,
                        K = (uint)K,
                        N = (uint)N,
                        HasZeroPoint = (uint)(hasZp ? 1 : 0)
                    };
                    D3D11Native.UpdateSubresource(_context, _quantGemmCb, (IntPtr)(&cbData));
                    D3D11Native.CSSetConstantBuffers(_context, 0, _quantGemmCb);

                    int groupsX = (N + 15) / 16;
                    int groupsY = (M + 15) / 16;
                    DispatchShader(_int4GemmShader, groupsX, groupsY, 1, new[] { scopeC.Buffer }, new[] { scopeA.Buffer, bBuf, scopeS.Buffer, zpBuf });

                    if (needsDownload)
                    {
                        var cContig = C.ToContiguous();
                        scopeC.Buffer.Download(cContig.AsSpan());
                        if (!ReferenceEquals(cContig, C)) cContig.CopyTo(C);
                    }
                }
                finally
                {
                    ReturnStructuredBuffer(bBuf);
                    if (zpBuf != null) ReturnStructuredBuffer(zpBuf);
                }
            }
            catch
            {
                BlasEngine.GemmInt4(A, packedWeights, scales, C, zeroPoints);
            }
        }

        #endregion

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_gemmShader != IntPtr.Zero) { D3D11Native.Release(_gemmShader); _gemmShader = IntPtr.Zero; }
                if (_batchedGemmShader != IntPtr.Zero) { D3D11Native.Release(_batchedGemmShader); _batchedGemmShader = IntPtr.Zero; }
                if (_vectorShader != IntPtr.Zero) { D3D11Native.Release(_vectorShader); _vectorShader = IntPtr.Zero; }
                if (_activationShader != IntPtr.Zero) { D3D11Native.Release(_activationShader); _activationShader = IntPtr.Zero; }
                if (_rmsNormShader != IntPtr.Zero) { D3D11Native.Release(_rmsNormShader); _rmsNormShader = IntPtr.Zero; }
                if (_layerNormShader != IntPtr.Zero) { D3D11Native.Release(_layerNormShader); _layerNormShader = IntPtr.Zero; }
                if (_softmaxShader != IntPtr.Zero) { D3D11Native.Release(_softmaxShader); _softmaxShader = IntPtr.Zero; }
                if (_fusedGemmShader != IntPtr.Zero) { D3D11Native.Release(_fusedGemmShader); _fusedGemmShader = IntPtr.Zero; }
                if (_fusedResRmsNormShader != IntPtr.Zero) { D3D11Native.Release(_fusedResRmsNormShader); _fusedResRmsNormShader = IntPtr.Zero; }
                if (_sdpaShader != IntPtr.Zero) { D3D11Native.Release(_sdpaShader); _sdpaShader = IntPtr.Zero; }
                if (_int8GemmShader != IntPtr.Zero) { D3D11Native.Release(_int8GemmShader); _int8GemmShader = IntPtr.Zero; }
                if (_int4GemmShader != IntPtr.Zero) { D3D11Native.Release(_int4GemmShader); _int4GemmShader = IntPtr.Zero; }

                if (_gemmCb != IntPtr.Zero) { D3D11Native.Release(_gemmCb); _gemmCb = IntPtr.Zero; }
                if (_batchedGemmCb != IntPtr.Zero) { D3D11Native.Release(_batchedGemmCb); _batchedGemmCb = IntPtr.Zero; }
                if (_vectorCb != IntPtr.Zero) { D3D11Native.Release(_vectorCb); _vectorCb = IntPtr.Zero; }
                if (_activationCb != IntPtr.Zero) { D3D11Native.Release(_activationCb); _activationCb = IntPtr.Zero; }
                if (_rmsNormCb != IntPtr.Zero) { D3D11Native.Release(_rmsNormCb); _rmsNormCb = IntPtr.Zero; }
                if (_layerNormCb != IntPtr.Zero) { D3D11Native.Release(_layerNormCb); _layerNormCb = IntPtr.Zero; }
                if (_softmaxCb != IntPtr.Zero) { D3D11Native.Release(_softmaxCb); _softmaxCb = IntPtr.Zero; }
                if (_fusedGemmCb != IntPtr.Zero) { D3D11Native.Release(_fusedGemmCb); _fusedGemmCb = IntPtr.Zero; }
                if (_fusedResRmsNormCb != IntPtr.Zero) { D3D11Native.Release(_fusedResRmsNormCb); _fusedResRmsNormCb = IntPtr.Zero; }
                if (_sdpaCb != IntPtr.Zero) { D3D11Native.Release(_sdpaCb); _sdpaCb = IntPtr.Zero; }
                if (_quantGemmCb != IntPtr.Zero) { D3D11Native.Release(_quantGemmCb); _quantGemmCb = IntPtr.Zero; }

                if (_bufferPool != null)
                {
                    _bufferPool.Dispose();
                    _bufferPool = null;
                }

                if (_context != IntPtr.Zero)
                {
                    D3D11Native.Release(_context);
                    _context = IntPtr.Zero;
                }
                if (_device != IntPtr.Zero)
                {
                    D3D11Native.Release(_device);
                    _device = IntPtr.Zero;
                }
                _disposed = true;
            }
        }
    }
}
