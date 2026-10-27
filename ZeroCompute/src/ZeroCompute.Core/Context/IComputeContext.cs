using System;
using ZeroCompute.Core.Context;
using ZeroTensor.Core;

namespace ZeroCompute.Core.Context
{
    /// <summary>
    /// Unified compute interface for accelerating tensor operations on CPU multi-threading or GPU hardware.
    /// </summary>
    public interface IComputeContext : IDisposable
    {
        ComputeBackend Backend { get; }

        /// <summary>
        /// General Matrix Multiplication: C = alpha * (A x B) + beta * C.
        /// </summary>
        void Gemm(
            Tensor<float> A,
            Tensor<float> B,
            Tensor<float> C,
            float alpha = 1.0f,
            float beta = 0.0f);

        /// <summary>
        /// General Matrix Multiplication returning a newly allocated result tensor: C = alpha * (A x B).
        /// </summary>
        Tensor<float> Gemm(Tensor<float> A, Tensor<float> B, float alpha = 1.0f, float beta = 0.0f);

        /// <summary>
        /// Batched General Matrix Multiplication: C[b] = alpha * (A[b] x B[b]) + beta * C[b].
        /// </summary>
        void BatchedGemm(
            Tensor<float> A,
            Tensor<float> B,
            Tensor<float> C,
            float alpha = 1.0f,
            float beta = 0.0f);

        /// <summary>
        /// Element-wise addition: C = A + B.
        /// </summary>
        void Add(Tensor<float> A, Tensor<float> B, Tensor<float> C);

        /// <summary>
        /// Element-wise multiplication: C = A * B.
        /// </summary>
        void Multiply(Tensor<float> A, Tensor<float> B, Tensor<float> C);

        /// <summary>
        /// Applies element-wise activation function.
        /// </summary>
        void Activation(Tensor<float> input, Tensor<float> output, ComputeActivationType type);

        /// <summary>
        /// Root Mean Square Normalization (RMSNorm).
        /// </summary>
        void RmsNorm(Tensor<float> input, Tensor<float> output, Tensor<float>? weight = null, float epsilon = 1e-5f);

        /// <summary>
        /// Layer Normalization (LayerNorm).
        /// </summary>
        void LayerNorm(Tensor<float> input, Tensor<float> output, Tensor<float>? weight = null, Tensor<float>? bias = null, float epsilon = 1e-5f);

        /// <summary>
        /// Numerically stable row-wise Softmax over specified axis.
        /// </summary>
        void Softmax(Tensor<float> input, Tensor<float> output, int axis = -1);

        /// <summary>
        /// Fused GEMM + Bias Addition + Activation: C = Activation(alpha * (A x B) + bias).
        /// </summary>
        void FusedGemm(
            Tensor<float> A,
            Tensor<float> B,
            Tensor<float>? bias,
            Tensor<float> C,
            ComputeActivationType activation = ComputeActivationType.None,
            float alpha = 1.0f);

        /// <summary>
        /// Fused Residual Addition + RMSNorm: output = RMSNorm(input + residual, weight, epsilon).
        /// </summary>
        void FusedResidualRmsNorm(
            Tensor<float> input,
            Tensor<float> residual,
            Tensor<float> output,
            Tensor<float>? weight = null,
            float epsilon = 1e-5f);

        /// <summary>
        /// Fused Residual Addition + LayerNorm: output = LayerNorm(input + residual, weight, bias, epsilon).
        /// </summary>
        void FusedResidualLayerNorm(
            Tensor<float> input,
            Tensor<float> residual,
            Tensor<float> output,
            Tensor<float>? weight = null,
            Tensor<float>? bias = null,
            float epsilon = 1e-5f);

        /// <summary>
        /// Scaled Dot-Product Attention (Online Softmax FlashAttention): Output = Softmax(Q * K^T * scale) * V.
        /// </summary>
        void ScaledDotProductAttention(
            Tensor<float> Q,
            Tensor<float> K,
            Tensor<float> V,
            Tensor<float> output,
            float? scale = null,
            bool isCausal = false);

        /// <summary>
        /// Half-precision (FP16) General Matrix Multiplication: C = A @ B.
        /// </summary>
        void Gemm(Tensor<Half> A, Tensor<Half> B, Tensor<Half> C);

        /// <summary>
        /// Quantized INT8 General Matrix Multiplication: C = scale * (A @ (B - zp)).
        /// </summary>
        void GemmInt8(
            Tensor<float> A,
            Tensor<sbyte> B,
            Tensor<float> scales,
            Tensor<float> C,
            Tensor<float>? zeroPoints = null);

        /// <summary>
        /// Quantized Packed INT4 General Matrix Multiplication: C = scale * (A @ (W_dequant - zp)).
        /// </summary>
        void GemmInt4(
            Tensor<float> A,
            Tensor<byte> packedWeights,
            Tensor<float> scales,
            Tensor<float> C,
            Tensor<float>? zeroPoints = null);

        /// <summary>
        /// Reduces tensor along the specified axis (e.g., sum, max).
        /// </summary>
        Tensor<float> ReduceSum(Tensor<float> input, int axis);
        Tensor<float> ReduceMax(Tensor<float> input, int axis);
    }
}
