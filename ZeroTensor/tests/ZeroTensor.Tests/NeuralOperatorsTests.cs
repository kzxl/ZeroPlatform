using System;
using Xunit;
using ZeroTensor.Core;

namespace ZeroTensor.Tests
{
    public class NeuralOperatorsTests
    {
        [Fact]
        public void SoftmaxFast_NumericalStabilityAndSumToOne()
        {
            // Large values that would overflow naive exp without max subtraction
            var t = Tensor.FromArray(new float[] { 1000f, 1001f, 1002f }, 1, 3);
            var sm = TensorOps.SoftmaxFast(t, axis: -1);

            Assert.False(float.IsNaN(sm[0, 0]));
            Assert.False(float.IsNaN(sm[0, 1]));
            Assert.False(float.IsNaN(sm[0, 2]));

            float sum = sm[0, 0] + sm[0, 1] + sm[0, 2];
            Assert.True(Math.Abs(sum - 1.0f) < 1e-5f);

            // sm[0, 2] should be the largest
            Assert.True(sm[0, 2] > sm[0, 1]);
            Assert.True(sm[0, 1] > sm[0, 0]);
        }

        [Fact]
        public void RMSNorm_Correctness()
        {
            // x = [1, 2, 3, 4], d = 4
            // sum(x^2) = 1 + 4 + 9 + 16 = 30
            // mean(x^2) = 30 / 4 = 7.5
            // rms = sqrt(7.5 + 1e-5) = 2.738613
            var x = Tensor.FromArray(new float[] { 1f, 2f, 3f, 4f }, 1, 4);
            var weight = Tensor.FromArray(new float[] { 2f, 2f, 2f, 2f }, 4);

            var y = TensorOps.RMSNorm(x, weight, eps: 1e-5f);

            float expectedRms = (float)Math.Sqrt(7.5 + 1e-5);
            for (int i = 0; i < 4; i++)
            {
                float expectedVal = (x[0, i] / expectedRms) * 2f;
                Assert.True(Math.Abs(y[0, i] - expectedVal) < 1e-4f, $"Mismatch at index {i}: actual={y[0, i]}, expected={expectedVal}");
            }
        }

        [Fact]
        public void LayerNorm_Correctness()
        {
            // x = [2, 4, 6, 8], mean = 5
            // diff = [-3, -1, 1, 3]
            // var = (9 + 1 + 1 + 9) / 4 = 5
            // std = sqrt(5 + 1e-5) = 2.23607
            var x = Tensor.FromArray(new float[] { 2f, 4f, 6f, 8f }, 1, 4);
            var weight = Tensor.FromArray(new float[] { 1f, 1f, 1f, 1f }, 4);
            var bias = Tensor.FromArray(new float[] { 0.1f, 0.2f, 0.3f, 0.4f }, 4);

            var y = TensorOps.LayerNorm(x, weight, bias, eps: 1e-5f);

            float expectedStd = (float)Math.Sqrt(5.0 + 1e-5);
            float[] diffs = { -3f, -1f, 1f, 3f };
            float[] biases = { 0.1f, 0.2f, 0.3f, 0.4f };

            for (int i = 0; i < 4; i++)
            {
                float expectedVal = (diffs[i] / expectedStd) * 1f + biases[i];
                Assert.True(Math.Abs(y[0, i] - expectedVal) < 1e-4f, $"Mismatch at {i}: actual={y[0, i]}, expected={expectedVal}");
            }
        }

        [Fact]
        public void SiLU_Correctness()
        {
            var x = Tensor.FromArray(new float[] { -2f, 0f, 2f }, 3);
            var y = TensorOps.SiLU(x);

            // SiLU(0) = 0 * 0.5 = 0
            Assert.Equal(0f, y[1]);

            // SiLU(2) = 2 / (1 + exp(-2)) = 2 / (1 + 0.135335) = 1.76159
            float expected2 = 2.0f / (1.0f + (float)Math.Exp(-2.0));
            Assert.True(Math.Abs(y[2] - expected2) < 1e-5f);

            // SiLU(-2) = -2 / (1 + exp(2)) = -2 / (1 + 7.38905) = -0.2384
            float expectedNeg2 = -2.0f / (1.0f + (float)Math.Exp(2.0));
            Assert.True(Math.Abs(y[0] - expectedNeg2) < 1e-5f);
        }

        [Fact]
        public void GELU_Correctness()
        {
            var x = Tensor.FromArray(new float[] { 0f, 1f, -1f }, 3);
            var y = TensorOps.GELU(x);

            // GELU(0) = 0
            Assert.Equal(0f, y[0]);

            // GELU(1) ~= 0.8413
            Assert.True(Math.Abs(y[1] - 0.8413f) < 1e-3f);

            // GELU(-1) ~= -0.1587
            Assert.True(Math.Abs(y[2] - (-0.1587f)) < 1e-3f);
        }

        [Fact]
        public void ScaledDotProductAttention_Batched()
        {
            // Batch=1, Heads=2, Seq=3, Dim=4
            var q = Tensor.Zeros<float>(1, 2, 3, 4);
            var k = Tensor.Zeros<float>(1, 2, 3, 4);
            var v = Tensor.Zeros<float>(1, 2, 3, 4);

            q.Fill(1f);
            k.Fill(1f);
            v.Fill(2f);

            var outAttn = TensorOps.ScaledDotProductAttention(q, k, v);

            Assert.Equal(new[] { 1, 2, 3, 4 }, outAttn.Shape.Dimensions);

            // Since all Q and K are identical, attention weights are uniform (1/3 each),
            // and V is all 2.0, so output should be all 2.0
            for (int h = 0; h < 2; h++)
            {
                for (int s = 0; s < 3; s++)
                {
                    for (int d = 0; d < 4; d++)
                    {
                        Assert.True(Math.Abs(outAttn[0, h, s, d] - 2f) < 1e-4f);
                    }
                }
            }
        }

        [Fact]
        public void Conv2D_IdentityFilter()
        {
            // Input: 1 batch, 1 channel, 3x3 image
            var input = Tensor.FromArray(new float[]
            {
                1f, 2f, 3f,
                4f, 5f, 6f,
                7f, 8f, 9f
            }, 1, 1, 3, 3);

            // Weight: 1 out channel, 1 in channel, 1x1 filter with value 1 (identity)
            var weight = Tensor.FromArray(new float[] { 1f }, 1, 1, 1, 1);
            var bias = Tensor.FromArray(new float[] { 0.5f }, 1);

            var output = TensorOps.Conv2D(input, weight, bias, stride: 1, padding: 0);

            Assert.Equal(new[] { 1, 1, 3, 3 }, output.Shape.Dimensions);

            // Output should be input + 0.5
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    Assert.Equal(input[0, 0, r, c] + 0.5f, output[0, 0, r, c]);
                }
            }
        }

        [Fact]
        public void EmbeddingLookup_Correctness()
        {
            // Vocab = 4, Dim = 3
            var weight = Tensor.FromArray(new float[]
            {
                10f, 11f, 12f, // token 0
                20f, 21f, 22f, // token 1
                30f, 31f, 32f, // token 2
                40f, 41f, 42f  // token 3
            }, 4, 3);

            // Batch = 2, Seq = 2
            var indices = Tensor.FromArray(new int[]
            {
                3, 0,
                1, 2
            }, 2, 2);

            var emb = TensorOps.EmbeddingLookup(indices, weight);

            Assert.Equal(new[] { 2, 2, 3 }, emb.Shape.Dimensions);

            // batch 0, seq 0 -> token 3
            Assert.Equal(40f, emb[0, 0, 0]);
            Assert.Equal(41f, emb[0, 0, 1]);
            Assert.Equal(42f, emb[0, 0, 2]);

            // batch 0, seq 1 -> token 0
            Assert.Equal(10f, emb[0, 1, 0]);

            // batch 1, seq 0 -> token 1
            Assert.Equal(20f, emb[1, 0, 0]);

            // batch 1, seq 1 -> token 2
            Assert.Equal(30f, emb[1, 1, 0]);
        }

        [Fact]
        public void ApplyRoPE_NormPreservation()
        {
            // Seq = 2, HeadDim = 4
            var x = Tensor.FromArray(new float[]
            {
                1f, 2f, 3f, 4f, // pos 0
                5f, 6f, 7f, 8f  // pos 1
            }, 1, 1, 2, 4);

            var xRot = TensorOps.ApplyRoPE(x, startPos: 0);

            Assert.Equal(new[] { 1, 1, 2, 4 }, xRot.Shape.Dimensions);

            // At pos = 0, angle = 0 -> cos = 1, sin = 0, so vector must be unchanged!
            Assert.Equal(1f, xRot[0, 0, 0, 0]);
            Assert.Equal(2f, xRot[0, 0, 0, 1]);
            Assert.Equal(3f, xRot[0, 0, 0, 2]);
            Assert.Equal(4f, xRot[0, 0, 0, 3]);

            // At pos = 1, rotation is orthogonal, so L2 norm squared must be preserved!
            float normOrigSq = 5f * 5f + 6f * 6f + 7f * 7f + 8f * 8f; // 25 + 36 + 49 + 64 = 174
            float r0 = xRot[0, 0, 1, 0];
            float r1 = xRot[0, 0, 1, 1];
            float r2 = xRot[0, 0, 1, 2];
            float r3 = xRot[0, 0, 1, 3];
            float normRotSq = r0 * r0 + r1 * r1 + r2 * r2 + r3 * r3;

            Assert.True(Math.Abs(normOrigSq - normRotSq) < 1e-3f, $"Norm not preserved: orig={normOrigSq}, rot={normRotSq}");
        }

        [Fact]
        public void KVCache_AppendAndRetrieveValid()
        {
            // Batch = 1, Heads = 2, MaxSeq = 10, Dim = 4
            using var cache = new ZeroTensor.Core.Neural.KVCache<float>(1, 2, 10, 4);
            Assert.Equal(0, cache.CurrentLength);

            // 1. Prefill 3 tokens
            var kPrefill = Tensor.Ones(1, 2, 3, 4);
            var vPrefill = Tensor.Full(2f, 1, 2, 3, 4);
            cache.Append(kPrefill, vPrefill);
            Assert.Equal(3, cache.CurrentLength);

            var validK = cache.GetValidKeys();
            var validV = cache.GetValidValues();
            Assert.Equal(new[] { 1, 2, 3, 4 }, validK.Shape.Dimensions);
            Assert.Equal(new[] { 1, 2, 3, 4 }, validV.Shape.Dimensions);
            Assert.Equal(1f, validK[0, 0, 2, 0]);
            Assert.Equal(2f, validV[0, 0, 2, 0]);

            // 2. Decode 1 new token
            var kNext = Tensor.Full(3f, 1, 2, 1, 4);
            var vNext = Tensor.Full(4f, 1, 2, 1, 4);
            cache.Append(kNext, vNext);
            Assert.Equal(4, cache.CurrentLength);

            validK = cache.GetValidKeys();
            validV = cache.GetValidValues();
            Assert.Equal(new[] { 1, 2, 4, 4 }, validK.Shape.Dimensions);
            Assert.Equal(3f, validK[0, 0, 3, 0]);
            Assert.Equal(4f, validV[0, 0, 3, 0]);

            // 3. Reset
            cache.Reset();
            Assert.Equal(0, cache.CurrentLength);
        }

        [Fact]
        public void MaxPool2D_2x2_Stride2()
        {
            var input = Tensor.FromArray(new float[]
            {
                1f, 3f, 2f, 4f,
                5f, 6f, 7f, 8f,
                9f, 2f, 3f, 1f,
                4f, 8f, 5f, 6f
            }, 1, 1, 4, 4);

            var pooled = TensorOps.MaxPool2D(input, kernelSize: 2, stride: 2);

            Assert.Equal(new[] { 1, 1, 2, 2 }, pooled.Shape.Dimensions);
            // Window (0,0): max(1, 3, 5, 6) = 6
            Assert.Equal(6f, pooled[0, 0, 0, 0]);
            // Window (0,1): max(2, 4, 7, 8) = 8
            Assert.Equal(8f, pooled[0, 0, 0, 1]);
            // Window (1,0): max(9, 2, 4, 8) = 9
            Assert.Equal(9f, pooled[0, 0, 1, 0]);
            // Window (1,1): max(3, 1, 5, 6) = 6
            Assert.Equal(6f, pooled[0, 0, 1, 1]);
        }

        [Fact]
        public void AvgPool2D_2x2_Stride2()
        {
            var input = Tensor.FromArray(new float[]
            {
                1f, 3f, 2f, 4f,
                5f, 7f, 6f, 8f,
                1f, 1f, 2f, 2f,
                3f, 3f, 4f, 4f
            }, 1, 1, 4, 4);

            var pooled = TensorOps.AvgPool2D(input, kernelSize: 2, stride: 2);

            Assert.Equal(new[] { 1, 1, 2, 2 }, pooled.Shape.Dimensions);
            // Window (0,0): (1 + 3 + 5 + 7) / 4 = 16 / 4 = 4
            Assert.Equal(4f, pooled[0, 0, 0, 0]);
            // Window (0,1): (2 + 4 + 6 + 8) / 4 = 20 / 4 = 5
            Assert.Equal(5f, pooled[0, 0, 0, 1]);
            // Window (1,0): (1 + 1 + 3 + 3) / 4 = 8 / 4 = 2
            Assert.Equal(2f, pooled[0, 0, 1, 0]);
            // Window (1,1): (2 + 2 + 4 + 4) / 4 = 12 / 4 = 3
            Assert.Equal(3f, pooled[0, 0, 1, 1]);
        }

        [Fact]
        public void Interpolate2D_NearestAndBilinear()
        {
            // 2x2 image
            var input = Tensor.FromArray(new float[]
            {
                10f, 20f,
                30f, 40f
            }, 1, 1, 2, 2);

            // 1. Nearest upsample to 4x4
            var nearest = TensorOps.Interpolate2D(input, 4, 4, InterpolationMode.Nearest);
            Assert.Equal(new[] { 1, 1, 4, 4 }, nearest.Shape.Dimensions);
            Assert.Equal(10f, nearest[0, 0, 0, 0]);
            Assert.Equal(10f, nearest[0, 0, 1, 1]);
            Assert.Equal(20f, nearest[0, 0, 0, 3]);
            Assert.Equal(40f, nearest[0, 0, 3, 3]);

            // 2. Bilinear upsample to 3x3 with alignCorners = true
            var bilinear = TensorOps.Interpolate2D(input, 3, 3, InterpolationMode.Bilinear, alignCorners: true);
            Assert.Equal(new[] { 1, 1, 3, 3 }, bilinear.Shape.Dimensions);
            // Center pixel must be average of all 4 corners: (10 + 20 + 30 + 40) / 4 = 25
            Assert.Equal(10f, bilinear[0, 0, 0, 0]);
            Assert.Equal(20f, bilinear[0, 0, 0, 2]);
            Assert.Equal(30f, bilinear[0, 0, 2, 0]);
            Assert.Equal(40f, bilinear[0, 0, 2, 2]);
            Assert.True(Math.Abs(bilinear[0, 0, 1, 1] - 25f) < 1e-4f);
        }

        [Fact]
        public void GroupNorm_4Channels_2Groups()
        {
            // 4 channels, 2 groups -> 2 channels per group
            // Spatial 2x2: each group has 2 * 2 * 2 = 8 values
            var input = Tensor.Zeros<float>(1, 4, 2, 2);
            // Group 0: channels 0, 1
            for (int c = 0; c < 2; c++)
                for (int r = 0; r < 2; r++)
                    for (int col = 0; col < 2; col++)
                        input[0, c, r, col] = (c * 4 + r * 2 + col); // values 0..7

            // Group 1: channels 2, 3
            for (int c = 2; c < 4; c++)
                for (int r = 0; r < 2; r++)
                    for (int col = 0; col < 2; col++)
                        input[0, c, r, col] = (c * 4 + r * 2 + col) * 10f;

            var gn = TensorOps.GroupNorm(input, numGroups: 2, eps: 1e-5f);
            Assert.Equal(new[] { 1, 4, 2, 2 }, gn.Shape.Dimensions);

            // Group 0 mean = 3.5, variance = 5.25, std = sqrt(5.25) = 2.291288
            // Check normalized mean across group 0 is approx 0
            float g0Sum = 0f;
            for (int c = 0; c < 2; c++)
                for (int r = 0; r < 2; r++)
                    for (int col = 0; col < 2; col++)
                        g0Sum += gn[0, c, r, col];

            Assert.True(Math.Abs(g0Sum) < 1e-4f);

            // Check normalized mean across group 1 is approx 0
            float g1Sum = 0f;
            for (int c = 2; c < 4; c++)
                for (int r = 0; r < 2; r++)
                    for (int col = 0; col < 2; col++)
                        g1Sum += gn[0, c, r, col];

            Assert.True(Math.Abs(g1Sum) < 1e-4f);
        }

        [Fact]
        public void FlashAttentionCpu_MatchesStandardAttention()
        {
            // Batch = 1, Heads = 2, Seq = 8, Dim = 16
            var q = Tensor.Zeros<float>(1, 2, 8, 16);
            var k = Tensor.Zeros<float>(1, 2, 8, 16);
            var v = Tensor.Zeros<float>(1, 2, 8, 16);

            for (int i = 0; i < 8; i++)
            {
                for (int d = 0; d < 16; d++)
                {
                    q[0, 0, i, d] = (i + d) * 0.1f;
                    k[0, 0, i, d] = (i - d) * 0.05f;
                    v[0, 0, i, d] = (i * 2 + d) * 0.1f;

                    q[0, 1, i, d] = (i - d) * 0.1f;
                    k[0, 1, i, d] = (i + d) * 0.05f;
                    v[0, 1, i, d] = (d - i) * 0.1f;
                }
            }

            var expected = TensorOps.ScaledDotProductAttention(q, k, v);
            var actual = TensorOps.FlashAttentionCpu(q, k, v);

            Assert.Equal(expected.Shape.Dimensions, actual.Shape.Dimensions);

            for (int h = 0; h < 2; h++)
            {
                for (int i = 0; i < 8; i++)
                {
                    for (int d = 0; d < 16; d++)
                    {
                        float diff = Math.Abs(expected[0, h, i, d] - actual[0, h, i, d]);
                        Assert.True(diff < 1e-4f, $"Mismatch at h={h}, i={i}, d={d}: expected {expected[0, h, i, d]}, actual {actual[0, h, i, d]}");
                    }
                }
            }
        }

        [Fact]
        public void Conv1D_BasicExecution()
        {
            // Input: [Batch=1, InC=1, L=5]
            var input = Tensor.FromArray(new float[] { 1f, 2f, 3f, 4f, 5f }, 1, 1, 5);
            // Weight: [OutC=1, InC=1, K=3]
            var weight = Tensor.FromArray(new float[] { 1f, 0f, -1f }, 1, 1, 3);
            var bias = Tensor.FromArray(new float[] { 10f }, 1);

            // Output length = (5 - 3) / 1 + 1 = 3
            // ol=0: 1*1 + 2*0 + 3*(-1) = -2 + 10 = 8
            // ol=1: 2*1 + 3*0 + 4*(-1) = -2 + 10 = 8
            // ol=2: 3*1 + 4*0 + 5*(-1) = -2 + 10 = 8
            var output = TensorOps.Conv1D(input, weight, bias, stride: 1, padding: 0);

            Assert.Equal(new[] { 1, 1, 3 }, output.Shape.Dimensions);
            Assert.Equal(8f, output[0, 0, 0]);
            Assert.Equal(8f, output[0, 0, 1]);
            Assert.Equal(8f, output[0, 0, 2]);
        }

        [Fact]
        public void ConvTranspose2D_BasicExecution()
        {
            // Input: [1, 1, 2, 2]
            var input = Tensor.FromArray(new float[]
            {
                1f, 2f,
                3f, 4f
            }, 1, 1, 2, 2);

            // Weight: [1, 1, 2, 2]
            var weight = Tensor.FromArray(new float[]
            {
                1f, 1f,
                1f, 1f
            }, 1, 1, 2, 2);

            // With stride = 2, output size is (2 - 1)*2 + 2 = 4x4
            var output = TensorOps.ConvTranspose2D(input, weight, stride: 2, padding: 0);

            Assert.Equal(new[] { 1, 1, 4, 4 }, output.Shape.Dimensions);

            // Top-left 2x2 should be input[0,0] * 1 = 1
            Assert.Equal(1f, output[0, 0, 0, 0]);
            Assert.Equal(1f, output[0, 0, 0, 1]);
            Assert.Equal(1f, output[0, 0, 1, 0]);
            Assert.Equal(1f, output[0, 0, 1, 1]);

            // Top-right 2x2 should be input[0,1] * 1 = 2
            Assert.Equal(2f, output[0, 0, 0, 2]);
            Assert.Equal(2f, output[0, 0, 0, 3]);
            Assert.Equal(2f, output[0, 0, 1, 2]);
            Assert.Equal(2f, output[0, 0, 1, 3]);

            // Bottom-right 2x2 should be input[1,1] * 1 = 4
            Assert.Equal(4f, output[0, 0, 2, 2]);
            Assert.Equal(4f, output[0, 0, 3, 3]);
        }

        [Fact]
        public void InferenceWorkspace_PingPongSwap()
        {
            using var ws = new InferenceWorkspace<float>(2, 4);

            var initialInput = Tensor.FromArray(new float[]
            {
                1f, 2f, 3f, 4f,
                5f, 6f, 7f, 8f
            }, 2, 4);

            ws.Reset(initialInput);

            Assert.Equal(1f, ws.CurrentInput[0, 0]);
            Assert.Equal(8f, ws.CurrentInput[1, 3]);

            // Simulate Layer 1 forward: CurrentOutput = CurrentInput * 2
            var inSpan = ws.CurrentInput.AsReadOnlySpan();
            var outSpan = ws.CurrentOutput.AsSpan();
            for (int i = 0; i < inSpan.Length; i++) outSpan[i] = inSpan[i] * 2f;

            // Step to Layer 2: buffers swap in O(1)
            ws.Step();

            Assert.Equal(2f, ws.CurrentInput[0, 0]);
            Assert.Equal(16f, ws.CurrentInput[1, 3]);

            // Simulate Layer 2 forward: CurrentOutput = CurrentInput + 10
            inSpan = ws.CurrentInput.AsReadOnlySpan();
            outSpan = ws.CurrentOutput.AsSpan();
            for (int i = 0; i < inSpan.Length; i++) outSpan[i] = inSpan[i] + 10f;

            // Step to Layer 3
            ws.Step();

            Assert.Equal(12f, ws.CurrentInput[0, 0]);
            Assert.Equal(26f, ws.CurrentInput[1, 3]);
        }

        [Fact]
        public void SwiGLU_TwoTensors_Correctness()
        {
            var gate = Tensor.FromArray(new float[] { 0f, 1f, -1f, 2f }, 2, 2);
            var up = Tensor.FromArray(new float[] { 3f, 4f, 5f, 6f }, 2, 2);

            var result = Tensor.SwiGLU(gate, up);

            // Manual calculation: silu(g) * u = (g / (1 + exp(-g))) * u
            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < 2; c++)
                {
                    float g = gate[r, c];
                    float u = up[r, c];
                    float expected = (g / (1.0f + (float)Math.Exp(-g))) * u;
                    Assert.True(Math.Abs(result[r, c] - expected) < 1e-5f, $"Mismatch at [{r},{c}]: got {result[r, c]}, expected {expected}");
                }
            }
        }

        [Fact]
        public void SwiGLU_SingleTensorSplit_Correctness()
        {
            // Combined [gate, up] along last dimension: [1, 4]
            var combined = Tensor.FromArray(new float[] { 0f, 2f, 3f, 6f }, 1, 4);
            var result = Tensor.SwiGLU(combined, dim: -1);

            Assert.Equal(new[] { 1, 2 }, result.Shape.Dimensions);

            var expected = Tensor.SwiGLU(combined.Slice(1, 0, 2), combined.Slice(1, 2, 2));
            Assert.Equal(expected[0, 0], result[0, 0]);
            Assert.Equal(expected[0, 1], result[0, 1]);
        }

        [Fact]
        public void GeGLU_TwoTensors_Correctness()
        {
            var gate = Tensor.FromArray(new float[] { 0f, 1f, -1f, 2f }, 2, 2);
            var up = Tensor.FromArray(new float[] { 3f, 4f, 5f, 6f }, 2, 2);

            var result = Tensor.GeGLU(gate, up);

            // Manual calculation: gelu(g) * u
            var geluGate = TensorOps.GELU(gate);
            var expected = geluGate * up;

            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < 2; c++)
                {
                    Assert.True(Math.Abs(result[r, c] - expected[r, c]) < 1e-4f, $"Mismatch at [{r},{c}]: got {result[r, c]}, expected {expected[r, c]}");
                }
            }
        }

        [Fact]
        public void GemvQ4_0_Correctness()
        {
            // 1 row, 32 cols (1 block of Q4_0 = 18 bytes)
            byte[] packed = new byte[18];
            // scale = 1.0 (FP16 0x3C00)
            packed[0] = 0x00;
            packed[1] = 0x3C;

            // Fill 16 bytes with 0x9A -> low nibble 0xA = 10 -> (10 - 8) = 2
            //                         -> high nibble 0x9 = 9 -> (9 - 8) = 1
            for (int i = 0; i < 16; i++) packed[2 + i] = 0x9A;

            // Activation: all ones
            var act = Tensor.Ones(32);

            var y = TensorBlas.GemvQ4_0(act, packed, 1, 32);

            Assert.Equal(1, y.Length);
            // 16 elements with weight 2 * 1.0 = 32
            // 16 elements with weight 1 * 1.0 = 16
            // Total = 48
            Assert.True(Math.Abs(y[0] - 48f) < 1e-4f, $"Expected 48, got {y[0]}");
        }

        [Fact]
        public void GemvQ8_0_Correctness()
        {
            // 1 row, 32 cols (1 block of Q8_0 = 34 bytes)
            byte[] packed = new byte[34];
            // scale = 2.0 (FP16 0x4000)
            packed[0] = 0x00;
            packed[1] = 0x40;

            // Fill 32 bytes with signed value 3
            for (int i = 0; i < 32; i++) packed[2 + i] = 3;

            // Activation: all ones
            var act = Tensor.Ones(32);

            var y = TensorBlas.GemvQ8_0(act, packed, 1, 32);

            Assert.Equal(1, y.Length);
            // 32 elements with weight 3 * scale 2.0 = 6 * 32 = 192
            Assert.True(Math.Abs(y[0] - 192f) < 1e-4f, $"Expected 192, got {y[0]}");
        }

        [Fact]
        public void ZeroCopyGEMM_OnNativeMemoryBlock()
        {
            // Test that Pointer-based GEMM works on off-heap unmanaged memory without throwing or falling into ToArray trap
            using var blockA = ZeroPrimitives.Memory.NativeMemoryBlock.Allocate(4 * sizeof(float));
            using var blockB = ZeroPrimitives.Memory.NativeMemoryBlock.Allocate(4 * sizeof(float));

            var a = Tensor.FromNativeBlock<float>(blockA, 2, 2);
            var b = Tensor.FromNativeBlock<float>(blockB, 2, 2);

            a[0, 0] = 1f; a[0, 1] = 2f;
            a[1, 0] = 3f; a[1, 1] = 4f;

            b[0, 0] = 5f; b[0, 1] = 6f;
            b[1, 0] = 7f; b[1, 1] = 8f;

            // Verify that TryGetArray returns null on NativeMemoryStorage
            Assert.Null(a.Storage.TryGetArray(out _));

            // Run GEMM: C = A @ B
            var c = TensorBlas.MatMul(a, b);

            Assert.Equal(19f, c[0, 0]); // 1*5 + 2*7 = 19
            Assert.Equal(22f, c[0, 1]); // 1*6 + 2*8 = 22
            Assert.Equal(43f, c[1, 0]); // 3*5 + 4*7 = 43
            Assert.Equal(50f, c[1, 1]); // 3*6 + 4*8 = 50
        }

        [Fact]
        public void FlashAttentionCpu_GroupedQueryAttention_MatchesExpanded()
        {
            // Batch = 1, Q Heads = 4, KV Heads = 2 (GQA with group_size = 2), Seq = 8, Dim = 16
            int b = 1, hq = 4, hkv = 2, seq = 8, dim = 16;
            var q = Tensor.Zeros<float>(b, hq, seq, dim);
            var k = Tensor.Zeros<float>(b, hkv, seq, dim);
            var v = Tensor.Zeros<float>(b, hkv, seq, dim);

            for (int h = 0; h < hq; h++)
            {
                for (int s = 0; s < seq; s++)
                {
                    for (int d = 0; d < dim; d++)
                    {
                        q[0, h, s, d] = (float)Math.Sin(h * 10 + s + d * 0.1);
                    }
                }
            }

            for (int h = 0; h < hkv; h++)
            {
                for (int s = 0; s < seq; s++)
                {
                    for (int d = 0; d < dim; d++)
                    {
                        k[0, h, s, d] = (float)Math.Cos(h * 5 + s * 0.5 + d * 0.2);
                        v[0, h, s, d] = (float)Math.Sin(h * 3 + s * 0.2 + d * 0.3);
                    }
                }
            }

            // Reference: Expand K and V to 4 heads manually
            var kExp = Tensor.Zeros<float>(b, hq, seq, dim);
            var vExp = Tensor.Zeros<float>(b, hq, seq, dim);
            for (int h = 0; h < hq; h++)
            {
                int kvH = h / (hq / hkv); // 0, 1 -> 0; 2, 3 -> 1
                for (int s = 0; s < seq; s++)
                {
                    for (int d = 0; d < dim; d++)
                    {
                        kExp[0, h, s, d] = k[0, kvH, s, d];
                        vExp[0, h, s, d] = v[0, kvH, s, d];
                    }
                }
            }

            var expected = TensorOps.ScaledDotProductAttention(q, kExp, vExp);
            var actual = TensorOps.FlashAttentionCpu(q, k, v);

            Assert.Equal(expected.Shape.Dimensions, actual.Shape.Dimensions);

            for (int h = 0; h < hq; h++)
            {
                for (int s = 0; s < seq; s++)
                {
                    for (int d = 0; d < dim; d++)
                    {
                        float diff = Math.Abs(expected[0, h, s, d] - actual[0, h, s, d]);
                        Assert.True(diff < 1e-4f, $"GQA Mismatch at h={h}, s={s}, d={d}: expected {expected[0, h, s, d]}, actual {actual[0, h, s, d]}");
                    }
                }
            }
        }

        [Fact]
        public void ApplyRoPEInPlace_MatchesRotaryEmbedding()
        {
            // Batch = 1, Heads = 2, Seq = 4, Dim = 8
            var xDefault = Tensor.Zeros<float>(1, 2, 4, 8);
            var xInterleaved = Tensor.Zeros<float>(1, 2, 4, 8);
            for (int h = 0; h < 2; h++)
            {
                for (int s = 0; s < 4; s++)
                {
                    for (int d = 0; d < 8; d++)
                    {
                        float val = (h + 1) * 10f + (s + 1) + (d + 1) * 0.1f;
                        xDefault[0, h, s, d] = val;
                        xInterleaved[0, h, s, d] = val;
                    }
                }
            }

            var xCopy = xDefault.Clone();

            // 1. Run in-place RoPE split-half (default: Llama style)
            xDefault.ApplyRoPEInPlace(startPos: 5, thetaBase: 10000.0f, interleaved: false);

            int pos = 7; // token s = 2, startPos = 5
            int halfDim = 4;
            for (int h = 0; h < 2; h++)
            {
                for (int i = 0; i < halfDim; i++)
                {
                    double theta = 1.0 / Math.Pow(10000.0, (2.0 * i) / 8.0);
                    double angle = pos * theta;
                    float cos = (float)Math.Cos(angle);
                    float sin = (float)Math.Sin(angle);

                    float x1 = xCopy[0, h, 2, i];
                    float x2 = xCopy[0, h, 2, i + halfDim];

                    float exp1 = x1 * cos - x2 * sin;
                    float exp2 = x2 * cos + x1 * sin;

                    Assert.True(Math.Abs(xDefault[0, h, 2, i] - exp1) < 1e-4f);
                    Assert.True(Math.Abs(xDefault[0, h, 2, i + halfDim] - exp2) < 1e-4f);
                }
            }

            // 2. Run in-place RoPE interleaved (GPT-J / NeoX style)
            xInterleaved.ApplyRoPEInPlace(startPos: 5, thetaBase: 10000.0f, interleaved: true);
            for (int h = 0; h < 2; h++)
            {
                for (int i = 0; i < halfDim; i++)
                {
                    double theta = 1.0 / Math.Pow(10000.0, (2.0 * i) / 8.0);
                    double angle = pos * theta;
                    float cos = (float)Math.Cos(angle);
                    float sin = (float)Math.Sin(angle);

                    float x1 = xCopy[0, h, 2, 2 * i];
                    float x2 = xCopy[0, h, 2, 2 * i + 1];

                    float exp1 = x1 * cos - x2 * sin;
                    float exp2 = x2 * cos + x1 * sin;

                    Assert.True(Math.Abs(xInterleaved[0, h, 2, 2 * i] - exp1) < 1e-4f);
                    Assert.True(Math.Abs(xInterleaved[0, h, 2, 2 * i + 1] - exp2) < 1e-4f);
                }
            }
        }

        [Fact]
        public void KVCache_ContiguousBlockAppend_Correctness()
        {
            using var kv = new ZeroTensor.Core.Neural.KVCache<float>(batchSize: 1, numHeads: 2, maxSeqLength: 16, headDim: 4);

            var k1 = Tensor.Ones(1, 2, 3, 4);
            var v1 = Tensor.Zeros<float>(1, 2, 3, 4);
            v1.Fill(2f);

            kv.Append(k1, v1);
            Assert.Equal(3, kv.CurrentLength);

            var kView = kv.GetValidKeys();
            var vView = kv.GetValidValues();

            Assert.Equal(new[] { 1, 2, 3, 4 }, kView.Shape.Dimensions);
            Assert.Equal(1f, kView[0, 0, 0, 0]);
            Assert.Equal(2f, vView[0, 1, 2, 3]);

            // Append 2 more tokens
            var k2 = Tensor.Zeros<float>(1, 2, 2, 4);
            k2.Fill(5f);
            var v2 = Tensor.Zeros<float>(1, 2, 2, 4);
            v2.Fill(6f);

            kv.Append(k2, v2);
            Assert.Equal(5, kv.CurrentLength);

            var kView2 = kv.GetValidKeys();
            Assert.Equal(new[] { 1, 2, 5, 4 }, kView2.Shape.Dimensions);
            Assert.Equal(1f, kView2[0, 0, 2, 0]); // From first append
            Assert.Equal(5f, kView2[0, 0, 3, 0]); // From second append
            Assert.Equal(5f, kView2[0, 1, 4, 3]);
        }

        [Fact]
        public void InPlaceOps_OnNativeMemoryBlock()
        {
            using var blockA = ZeroPrimitives.Memory.NativeMemoryBlock.Allocate(8 * sizeof(float));
            using var blockB = ZeroPrimitives.Memory.NativeMemoryBlock.Allocate(8 * sizeof(float));

            var a = Tensor.FromNativeBlock<float>(blockA, 8);
            var b = Tensor.FromNativeBlock<float>(blockB, 8);

            for (int i = 0; i < 8; i++)
            {
                a[i] = i * 2f;
                b[i] = 1f;
            }

            // In-place Add_
            a.Add_(b);
            for (int i = 0; i < 8; i++)
            {
                Assert.Equal(i * 2f + 1f, a[i]);
            }

            // In-place MultiplyScalar_
            a.MultiplyScalar_(2f);
            for (int i = 0; i < 8; i++)
            {
                Assert.Equal((i * 2f + 1f) * 2f, a[i]);
            }

            // In-place Relu_
            a[0] = -10f;
            a.Relu_();
            Assert.Equal(0f, a[0]);
        }

        [Fact]
        public void AdaLN_And_AdaLNZero_Correctness()
        {
            // Input: [1, 2, 4]
            var x = Tensor.FromArray(new float[]
            {
                2f, 4f, 6f, 8f,
                1f, 3f, 5f, 7f
            }, 1, 2, 4);

            var scale = Tensor.FromArray(new float[] { 0.5f, 0.5f, 0.5f, 0.5f }, 4);
            var shift = Tensor.FromArray(new float[] { 1f, 1f, 1f, 1f }, 4);
            var gate = Tensor.FromArray(new float[] { 2f, 2f, 2f, 2f }, 4);

            var yAdaLN = TensorOps.AdaLN(x, scale, shift, eps: 1e-5f);
            var yAdaLNZero = TensorOps.AdaLNZero(x, scale, shift, gate, eps: 1e-5f);

            // Row 0: mean=5, std = sqrt(5 + 1e-5) ~= 2.236068
            // normed[0] = (2 - 5) / 2.236068 = -1.34164
            // AdaLN expected = (1 + 0.5) * (-1.34164) + 1 = -1.01246
            // AdaLNZero expected = 2 * (-1.01246) = -2.02492
            float norm0 = (2f - 5f) / (float)Math.Sqrt(5.0 + 1e-5);
            float expAdaLN0 = (1.0f + 0.5f) * norm0 + 1.0f;
            float expAdaLNZero0 = 2.0f * expAdaLN0;

            Assert.True(Math.Abs(yAdaLN[0, 0, 0] - expAdaLN0) < 1e-4f);
            Assert.True(Math.Abs(yAdaLNZero[0, 0, 0] - expAdaLNZero0) < 1e-4f);
        }

        [Fact]
        public void DepthwiseConv2D_MatchesManualSpatialFiltering()
        {
            // Batch=1, Channels=2, H=4, W=4
            var input = Tensor.Zeros<float>(1, 2, 4, 4);
            for (int c = 0; c < 2; c++)
                for (int h = 0; h < 4; h++)
                    for (int w = 0; w < 4; w++)
                        input[0, c, h, w] = (c + 1) * 10f + h * 4 + w;

            // Weight: [2, 1, 3, 3] all ones
            var weight = Tensor.Ones(2, 1, 3, 3);
            var bias = Tensor.FromArray(new float[] { 1f, 2f }, 2);

            // Stride=1, Padding=1 -> Output [1, 2, 4, 4]
            var output = TensorOps.DepthwiseConv2D(input, weight, bias, stride: 1, padding: 1);

            Assert.Equal(new[] { 1, 2, 4, 4 }, output.Shape.Dimensions);

            // Center element at [0, 0, 1, 1]: full 3x3 window around (1, 1) in channel 0
            // Channel 0 base = 10. Coordinates:
            // (0,0)=10, (0,1)=11, (0,2)=12
            // (1,0)=14, (1,1)=15, (1,2)=16
            // (2,0)=18, (2,1)=19, (2,2)=20
            // Sum = 10+11+12+14+15+16+18+19+20 = 135
            // Plus bias = 135 + 1 = 136
            Assert.Equal(136f, output[0, 0, 1, 1]);
        }

        [Fact]
        public void Pad2D_Correctness()
        {
            var x = Tensor.FromArray(new float[]
            {
                1f, 2f,
                3f, 4f
            }, 1, 1, 2, 2);

            // Pad left=1, right=2, top=1, bottom=1 with constant 0
            var padded = TensorOps.Pad2D(x, padLeft: 1, padRight: 2, padTop: 1, padBottom: 1, mode: PadMode.Constant, value: 0f);

            // Output shape: H = 2 + 1 + 1 = 4; W = 2 + 1 + 2 = 5
            Assert.Equal(new[] { 1, 1, 4, 5 }, padded.Shape.Dimensions);

            // Center original top-left at (1, 1)
            Assert.Equal(1f, padded[0, 0, 1, 1]);
            Assert.Equal(2f, padded[0, 0, 1, 2]);
            Assert.Equal(3f, padded[0, 0, 2, 1]);
            Assert.Equal(4f, padded[0, 0, 2, 2]);

            // Border paddings
            Assert.Equal(0f, padded[0, 0, 0, 0]);
            Assert.Equal(0f, padded[0, 0, 1, 0]);
            Assert.Equal(0f, padded[0, 0, 1, 3]);
            Assert.Equal(0f, padded[0, 0, 1, 4]);
        }

        [Fact]
        public void PagedAttention_NativeMatchesPureCSharp()
        {
            int numHeads = 4, numKvHeads = 2, headDim = 8, blockSize = 4, contextLen = 10;
            int numBlocks = 3;
            var q = Tensor.Normal(seed: 42, shape: new[] { numHeads, headDim });
            var kCache = Tensor.Normal(seed: 43, shape: new[] { numBlocks, blockSize, numKvHeads, headDim });
            var vCache = Tensor.Normal(seed: 44, shape: new[] { numBlocks, blockSize, numKvHeads, headDim });
            int[] blockTable = new[] { 2, 0, 1 };

            ZeroTensor.Core.Native.ZeroTensorNativeBridge.DispatchMode = ZeroTensor.Core.Native.NativeDispatchMode.ForcePureCSharp;
            var expected = TensorOps.PagedAttention(q, kCache, vCache, blockTable, contextLen);

            ZeroTensor.Core.Native.ZeroTensorNativeBridge.DispatchMode = ZeroTensor.Core.Native.NativeDispatchMode.ForceNative;
            var actual = TensorOps.PagedAttention(q, kCache, vCache, blockTable, contextLen);
            ZeroTensor.Core.Native.ZeroTensorNativeBridge.DispatchMode = ZeroTensor.Core.Native.NativeDispatchMode.Auto;

            for (int h = 0; h < numHeads; h++)
                for (int d = 0; d < headDim; d++)
                    Assert.True(Math.Abs(expected[h, d] - actual[h, d]) < 1e-3f);
        }

        [Fact]
        public void TransformerDecoderLayerInPlace_NativeMatchesPureCSharp()
        {
            int hiddenDim = 16, intermediateDim = 32, numHeads = 4, numKvHeads = 2;
            int kvDim = numKvHeads * (hiddenDim / numHeads);

            var x1 = Tensor.Ones<float>(hiddenDim);
            var x2 = Tensor.Ones<float>(hiddenDim);
            var rmsAttnW = Tensor.Ones<float>(hiddenDim);
            var wq = Tensor.Full(0.05f, hiddenDim, hiddenDim);
            var wk = Tensor.Full(0.05f, kvDim, hiddenDim);
            var wv = Tensor.Full(0.05f, kvDim, hiddenDim);
            var wo = Tensor.Full(0.05f, hiddenDim, hiddenDim);
            var rmsFfnW = Tensor.Ones<float>(hiddenDim);
            var wGate = Tensor.Full(0.05f, intermediateDim, hiddenDim);
            var wUp = Tensor.Full(0.05f, intermediateDim, hiddenDim);
            var wDown = Tensor.Full(0.05f, hiddenDim, intermediateDim);
            var kCache1 = Tensor.Zeros<float>(8, kvDim);
            var vCache1 = Tensor.Zeros<float>(8, kvDim);
            var kCache2 = Tensor.Zeros<float>(8, kvDim);
            var vCache2 = Tensor.Zeros<float>(8, kvDim);

            ZeroTensor.Core.Native.ZeroTensorNativeBridge.DispatchMode = ZeroTensor.Core.Native.NativeDispatchMode.ForcePureCSharp;
            TensorOps.TransformerDecoderLayerInPlace(
                x1, rmsAttnW, wq, wk, wv, wo, rmsFfnW, wGate, wUp, wDown, kCache1, vCache1,
                numHeads, numKvHeads, pos: 0);

            ZeroTensor.Core.Native.ZeroTensorNativeBridge.DispatchMode = ZeroTensor.Core.Native.NativeDispatchMode.ForceNative;
            TensorOps.TransformerDecoderLayerInPlace(
                x2, rmsAttnW, wq, wk, wv, wo, rmsFfnW, wGate, wUp, wDown, kCache2, vCache2,
                numHeads, numKvHeads, pos: 0);
            ZeroTensor.Core.Native.ZeroTensorNativeBridge.DispatchMode = ZeroTensor.Core.Native.NativeDispatchMode.Auto;

            for (int i = 0; i < hiddenDim; i++)
                Assert.True(Math.Abs(x1[i] - x2[i]) < 1e-3f);
        }
    }
}

