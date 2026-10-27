using System;
using ZeroCompute.Core.Blas;
using ZeroCompute.Core.Context;
using ZeroTensor.Core;

namespace ZeroCompute.Core
{
    /// <summary>
    /// Factory and coordinator for high-performance hardware and CPU compute dispatch.
    /// Provides transparent execution across CPU thread pools and Direct3D 11 GPU compute shaders.
    /// </summary>
    public sealed class ComputeDevice : IComputeContext
    {
        private static readonly Lazy<ComputeDevice> _defaultCpu = new Lazy<ComputeDevice>(() => new ComputeDevice(ComputeBackend.CpuParallel));
        private static readonly Lazy<ComputeDevice> _defaultGpu = new Lazy<ComputeDevice>(() => new ComputeDevice(ComputeBackend.Direct3D11));

        public static ComputeDevice Cpu => _defaultCpu.Value;
        public static ComputeDevice Gpu => _defaultGpu.Value;

        private readonly ZeroCompute.Core.DirectX.D3D11ComputeContext? _d3d11Context;

        public ComputeBackend Backend { get; }
        public bool IsHardwareAccelerated => _d3d11Context?.IsHardwareAccelerated ?? false;
        public ZeroCompute.Core.DirectX.D3D11ComputeContext? D3D11Context => _d3d11Context;
        public ZeroCompute.Core.DirectX.D3D11BufferPool? BufferPool => _d3d11Context?.BufferPool;

        public ComputeDevice(ComputeBackend backend = ComputeBackend.CpuParallel)
        {
            Backend = backend;
            if (backend == ComputeBackend.Direct3D11)
            {
                _d3d11Context = new ZeroCompute.Core.DirectX.D3D11ComputeContext();
            }
        }

        public static ComputeDevice Create(ComputeBackend backend = ComputeBackend.CpuParallel)
        {
            return new ComputeDevice(backend);
        }

        public static bool IsGpuAvailable => Gpu.IsHardwareAccelerated;

        /// <summary>
        /// Automatically selects Direct3D 11 GPU acceleration if available on the current platform,
        /// otherwise returns the high-performance CPU parallel compute runtime.
        /// </summary>
        public static ComputeDevice GetBestDevice()
        {
            var gpuDevice = Gpu;
            if (gpuDevice.IsHardwareAccelerated)
            {
                return new ComputeDevice(ComputeBackend.Direct3D11);
            }
            return new ComputeDevice(ComputeBackend.CpuParallel);
        }

        /// <summary>
        /// Allocates a GPU VRAM-resident tensor if running on Direct3D 11, otherwise allocates a host CPU tensor.
        /// When pooled is true, uses the VRAM buffer pool for zero driver allocation overhead.
        /// </summary>
        public Tensor<T> AllocateDeviceTensor<T>(TensorShape shape, bool pooled = false) where T : unmanaged, IEquatable<T>
        {
            if (_d3d11Context != null && _d3d11Context.IsHardwareAccelerated)
            {
                return _d3d11Context.AllocateDeviceTensor<T>(shape, pooled);
            }
            return new Tensor<T>(shape);
        }

        /// <summary>
        /// Moves a host tensor to GPU VRAM if running on Direct3D 11, returning a device-resident tensor.
        /// </summary>
        public Tensor<T> ToDevice<T>(Tensor<T> hostTensor) where T : unmanaged, IEquatable<T>
        {
            if (_d3d11Context != null && _d3d11Context.IsHardwareAccelerated)
            {
                return _d3d11Context.ToDevice(hostTensor);
            }
            return hostTensor;
        }

        /// <summary>
        /// General Matrix Multiplication returning a newly allocated result tensor: C = alpha * (A x B).
        /// </summary>
        public Tensor<float> Gemm(Tensor<float> A, Tensor<float> B, float alpha = 1.0f, float beta = 0.0f)
        {
            if (A == null) throw new ArgumentNullException(nameof(A));
            if (B == null) throw new ArgumentNullException(nameof(B));
            if (A.Rank != 2 || B.Rank != 2)
                throw new ArgumentException("Tensors must be 2D matrices.");
            if (A.Shape[1] != B.Shape[0])
                throw new ArgumentException($"Inner dimensions must match: A is [{A.Shape[0]},{A.Shape[1]}], B is [{B.Shape[0]},{B.Shape[1]}].");

            var C = Tensor.Zeros<float>(A.Shape[0], B.Shape[1]);
            Gemm(A, B, C, alpha, beta);
            return C;
        }

        public void Gemm(
            Tensor<float> A,
            Tensor<float> B,
            Tensor<float> C,
            float alpha = 1.0f,
            float beta = 0.0f)
        {
            if (_d3d11Context != null)
                _d3d11Context.Gemm(A, B, C, alpha, beta);
            else
                BlasEngine.Gemm(A, B, C, alpha, beta);
        }

        public void BatchedGemm(
            Tensor<float> A,
            Tensor<float> B,
            Tensor<float> C,
            float alpha = 1.0f,
            float beta = 0.0f)
        {
            if (_d3d11Context != null)
                _d3d11Context.BatchedGemm(A, B, C, alpha, beta);
            else
                BlasEngine.BatchedGemm(A, B, C, alpha, beta);
        }

        public void Add(Tensor<float> A, Tensor<float> B, Tensor<float> C)
        {
            if (_d3d11Context != null)
                _d3d11Context.Add(A, B, C);
            else
                BlasEngine.Add(A, B, C);
        }

        public void Multiply(Tensor<float> A, Tensor<float> B, Tensor<float> C)
        {
            if (_d3d11Context != null)
                _d3d11Context.Multiply(A, B, C);
            else
                BlasEngine.Multiply(A, B, C);
        }

        public void Activation(Tensor<float> input, Tensor<float> output, ComputeActivationType type)
        {
            if (_d3d11Context != null)
                _d3d11Context.Activation(input, output, type);
            else
                BlasEngine.Activation(input, output, type);
        }

        public void RmsNorm(Tensor<float> input, Tensor<float> output, Tensor<float>? weight = null, float epsilon = 1e-5f)
        {
            if (_d3d11Context != null)
                _d3d11Context.RmsNorm(input, output, weight, epsilon);
            else
                BlasEngine.RmsNorm(input, output, weight, epsilon);
        }

        public void LayerNorm(Tensor<float> input, Tensor<float> output, Tensor<float>? weight = null, Tensor<float>? bias = null, float epsilon = 1e-5f)
        {
            if (_d3d11Context != null)
                _d3d11Context.LayerNorm(input, output, weight, bias, epsilon);
            else
                BlasEngine.LayerNorm(input, output, weight, bias, epsilon);
        }

        public void Softmax(Tensor<float> input, Tensor<float> output, int axis = -1)
        {
            if (_d3d11Context != null)
                _d3d11Context.Softmax(input, output, axis);
            else
                BlasEngine.Softmax(input, output, axis);
        }

        public Tensor<float> ReduceSum(Tensor<float> input, int axis)
        {
            if (_d3d11Context != null)
                return _d3d11Context.ReduceSum(input, axis);
            return BlasEngine.ReduceSum(input, axis);
        }

        public Tensor<float> ReduceMax(Tensor<float> input, int axis)
        {
            if (_d3d11Context != null)
                return _d3d11Context.ReduceMax(input, axis);
            return BlasEngine.ReduceMax(input, axis);
        }

        public void FusedGemm(
            Tensor<float> A,
            Tensor<float> B,
            Tensor<float>? bias,
            Tensor<float> C,
            ComputeActivationType activation = ComputeActivationType.None,
            float alpha = 1.0f)
        {
            if (_d3d11Context != null)
                _d3d11Context.FusedGemm(A, B, bias, C, activation, alpha);
            else
                BlasEngine.FusedGemm(A, B, bias, C, activation, alpha);
        }

        public void FusedResidualRmsNorm(
            Tensor<float> input,
            Tensor<float> residual,
            Tensor<float> output,
            Tensor<float>? weight = null,
            float epsilon = 1e-5f)
        {
            if (_d3d11Context != null)
                _d3d11Context.FusedResidualRmsNorm(input, residual, output, weight, epsilon);
            else
                BlasEngine.FusedResidualRmsNorm(input, residual, output, weight, epsilon);
        }

        public void FusedResidualLayerNorm(
            Tensor<float> input,
            Tensor<float> residual,
            Tensor<float> output,
            Tensor<float>? weight = null,
            Tensor<float>? bias = null,
            float epsilon = 1e-5f)
        {
            if (_d3d11Context != null)
                _d3d11Context.FusedResidualLayerNorm(input, residual, output, weight, bias, epsilon);
            else
                BlasEngine.FusedResidualLayerNorm(input, residual, output, weight, bias, epsilon);
        }

        public void ScaledDotProductAttention(
            Tensor<float> Q,
            Tensor<float> K,
            Tensor<float> V,
            Tensor<float> output,
            float? scale = null,
            bool isCausal = false)
        {
            if (_d3d11Context != null)
                _d3d11Context.ScaledDotProductAttention(Q, K, V, output, scale, isCausal);
            else
                BlasEngine.ScaledDotProductAttention(Q, K, V, output, scale, isCausal);
        }

        public void Gemm(Tensor<Half> A, Tensor<Half> B, Tensor<Half> C)
        {
            if (_d3d11Context != null)
                _d3d11Context.Gemm(A, B, C);
            else
                BlasEngine.Gemm(A, B, C);
        }

        public void GemmInt8(
            Tensor<float> A,
            Tensor<sbyte> B,
            Tensor<float> scales,
            Tensor<float> C,
            Tensor<float>? zeroPoints = null)
        {
            if (_d3d11Context != null && _d3d11Context.IsHardwareAccelerated)
                _d3d11Context.GemmInt8(A, B, scales, C, zeroPoints);
            else
                BlasEngine.GemmInt8(A, B, scales, C, zeroPoints);
        }

        public void GemmInt4(
            Tensor<float> A,
            Tensor<byte> packedWeights,
            Tensor<float> scales,
            Tensor<float> C,
            Tensor<float>? zeroPoints = null)
        {
            if (_d3d11Context != null && _d3d11Context.IsHardwareAccelerated)
                _d3d11Context.GemmInt4(A, packedWeights, scales, C, zeroPoints);
            else
                BlasEngine.GemmInt4(A, packedWeights, scales, C, zeroPoints);
        }

        public void Dispose()
        {
            _d3d11Context?.Dispose();
        }
    }
}
