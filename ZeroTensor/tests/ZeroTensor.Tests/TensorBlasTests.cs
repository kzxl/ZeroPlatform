using System;
using Xunit;
using ZeroTensor.Core;

namespace ZeroTensor.Tests
{
    public class TensorBlasTests
    {
        [Fact]
        public void DotProduct_And_Norm_AreAccurate()
        {
            var u = Tensor.FromArray(new float[] { 1, 2, 3 });
            var v = Tensor.FromArray(new float[] { 4, 5, 6 });

            // 1*4 + 2*5 + 3*6 = 4 + 10 + 18 = 32
            float dot = TensorBlas.Dot(u, v);
            Assert.Equal(32f, dot);

            // norm(3, 4) = 5
            var vec2 = Tensor.FromArray(new float[] { 3, 4 });
            float norm = TensorBlas.Norm(vec2);
            Assert.Equal(5f, norm);
        }

        [Fact]
        public void MatMul_2D_MatchesAnalyticalResult()
        {
            // A (2x3):
            // [1, 2, 3]
            // [4, 5, 6]
            var A = Tensor.FromArray(new float[]
            {
                1, 2, 3,
                4, 5, 6
            }, 2, 3);

            // B (3x2):
            // [7,  8]
            // [9,  1]
            // [2,  3]
            var B = Tensor.FromArray(new float[]
            {
                7, 8,
                9, 1,
                2, 3
            }, 3, 2);

            // C = A @ B (2x2):
            // row 0: [1*7 + 2*9 + 3*2,  1*8 + 2*1 + 3*3] = [7 + 18 + 6,  8 + 2 + 9]  = [31, 19]
            // row 1: [4*7 + 5*9 + 6*2,  4*8 + 5*1 + 6*3] = [28 + 45 + 12, 32 + 5 + 18] = [85, 55]
            var C = TensorBlas.MatMul(A, B);

            Assert.Equal(new TensorShape(2, 2), C.Shape);
            Assert.Equal(31f, C[0, 0]);
            Assert.Equal(19f, C[0, 1]);
            Assert.Equal(85f, C[1, 0]);
            Assert.Equal(55f, C[1, 1]);
        }

        [Fact]
        public void MatMul_WithIdentity_ReturnsOriginal()
        {
            var A = Tensor.FromArray(new float[]
            {
                2, 5, 7,
                1, 3, 4,
                8, 9, 6
            }, 3, 3);

            var I = Tensor.Eye(3);
            var result = TensorBlas.MatMul(A, I);

            Assert.Equal(A.ToArray(), result.ToArray());
        }

        [Fact]
        public void MatMul_MatrixVector_ProducesVector()
        {
            var A = Tensor.FromArray(new float[]
            {
                1, 2,
                3, 4
            }, 2, 2);

            var x = Tensor.FromArray(new float[] { 5, 6 }); // length 2

            // [1*5 + 2*6, 3*5 + 4*6] = [17, 39]
            var y = TensorBlas.MatMul(A, x);

            Assert.Equal(new TensorShape(2), y.Shape);
            Assert.Equal(17f, y[0]);
            Assert.Equal(39f, y[1]);
        }

        [Fact]
        public void ZeroRust_NativeBridge_IsAvailable_And_Executes()
        {
            Assert.True(ZeroTensor.Core.Native.ZeroTensorNativeBridge.IsAvailable);
            var a = Tensor.Ones<float>(16, 16);
            var b = Tensor.Ones<float>(16, 16);
            var c = TensorBlas.MatMul(a, b);
            Assert.Equal(16f, c[0, 0]);
            Assert.Equal(16f, c[15, 15]);
        }

        [Fact]
        public void ZeroRust_AdaptiveDispatch_Modes_Execute_With_Parity()
        {
            var prevMode = ZeroTensor.Core.Native.ZeroTensorNativeBridge.DispatchMode;
            try
            {
                var a = Tensor.Normal(seed: 42, shape: new[] { 64, 64 });
                var b = Tensor.Normal(seed: 43, shape: new[] { 64, 64 });

                // 1. Force Pure C#
                ZeroTensor.Core.Native.ZeroTensorNativeBridge.DispatchMode = ZeroTensor.Core.Native.NativeDispatchMode.ForcePureCSharp;
                var resCSharp = TensorBlas.MatMul(a, b);

                // 2. Force Native ZeroRust
                ZeroTensor.Core.Native.ZeroTensorNativeBridge.DispatchMode = ZeroTensor.Core.Native.NativeDispatchMode.ForceNative;
                var resNative = TensorBlas.MatMul(a, b);

                // 3. Auto Adaptive
                ZeroTensor.Core.Native.ZeroTensorNativeBridge.DispatchMode = ZeroTensor.Core.Native.NativeDispatchMode.Auto;
                var resAuto = TensorBlas.MatMul(a, b);

                // Verify numerical parity across all modes
                for (int i = 0; i < 64; i++)
                {
                    for (int j = 0; j < 64; j++)
                    {
                        Assert.Equal(resCSharp[i, j], resNative[i, j], precision: 4);
                        Assert.Equal(resCSharp[i, j], resAuto[i, j], precision: 4);
                    }
                }
            }
            finally
            {
                ZeroTensor.Core.Native.ZeroTensorNativeBridge.DispatchMode = prevMode;
            }
        }
    }
}
