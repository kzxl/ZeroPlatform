using System;
using Xunit;
using ZeroNeural.Core.Autograd;
using ZeroTensor.Core;

namespace ZeroNeural.Tests
{
    public class AutogradTests
    {
        [Fact]
        public void Autograd_PolynomialScalar_ComputesExactGradient()
        {
            // f(x, y) = x^2 * y + 3*y
            // df/dx = 2*x*y
            // df/dy = x^2 + 3
            // Let x = 3, y = 4
            // df/dx = 2 * 3 * 4 = 24
            // df/dy = 3^2 + 3 = 9 + 3 = 12
            var x = new Variable(Tensor.FromArray(new[] { 3.0f }), requiresGrad: true);
            var y = new Variable(Tensor.FromArray(new[] { 4.0f }), requiresGrad: true);

            var xSquared = x * x;
            var term1 = xSquared * y;
            var term2 = y * 3.0f;
            var f = term1 + term2;

            Assert.Equal(48.0f, f.Data[0]); // 9*4 + 12 = 48

            f.Backward();

            Assert.NotNull(x.Grad);
            Assert.NotNull(y.Grad);
            Assert.True(Math.Abs(x.Grad[0] - 24.0f) < 1e-4f, $"df/dx = {x.Grad[0]}, expected 24.0");
            Assert.True(Math.Abs(y.Grad[0] - 12.0f) < 1e-4f, $"df/dy = {y.Grad[0]}, expected 12.0");
        }

        [Fact]
        public void Autograd_FanOut_AccumulatesMultipleGradients()
        {
            // f(x) = x + x + x = 3*x
            // df/dx = 3
            var x = new Variable(Tensor.FromArray(new[] { 5.0f }), requiresGrad: true);
            var f = x + x + x;

            f.Backward();

            Assert.NotNull(x.Grad);
            Assert.True(Math.Abs(x.Grad[0] - 3.0f) < 1e-4f, $"df/dx = {x.Grad[0]}, expected 3.0");
        }

        [Fact]
        public void Autograd_BroadcastingGradient_UnbroadcastsCorrectly()
        {
            // A (2, 3), B (1, 3)
            // C = A + B
            // Loss = Sum(C)
            // dL/dA should be (2, 3) filled with 1
            // dL/dB should be (1, 3) filled with 2 (summed along axis 0!)
            var A = new Variable(Tensor.Zeros<float>(2, 3), requiresGrad: true);
            var B = new Variable(Tensor.Zeros<float>(1, 3), requiresGrad: true);

            var C = A + B;
            var loss = C.Sum();

            loss.Backward();

            Assert.Equal(new TensorShape(2, 3), A.Grad!.Shape);
            Assert.Equal(new TensorShape(1, 3), B.Grad!.Shape);

            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 3; c++)
                    Assert.Equal(1.0f, A.Grad[r, c]);

            for (int c = 0; c < 3; c++)
                Assert.Equal(2.0f, B.Grad[0, c]);
        }

        [Fact]
        public void Autograd_MatMul_ComputesCorrectWeightAndInputGradients()
        {
            // X (2, 2), W (2, 2)
            // Y = X @ W
            // Loss = Sum(Y)
            // dL/dX = Ones(2, 2) @ W^T
            // dL/dW = X^T @ Ones(2, 2)
            var X = new Variable(Tensor.FromArray(new float[]
            {
                1, 2,
                3, 4
            }, 2, 2), requiresGrad: true);

            var W = new Variable(Tensor.FromArray(new float[]
            {
                5, 6,
                7, 8
            }, 2, 2), requiresGrad: true);

            var Y = X.MatMul(W);
            var loss = Y.Sum();

            loss.Backward();

            // W = [[5, 6], [7, 8]] -> sum of each row of W is: row0 = 11, row1 = 15
            // dL/dX = [[1, 1], [1, 1]] @ [[5, 7], [6, 8]] = [[11, 15], [11, 15]]
            Assert.Equal(11.0f, X.Grad![0, 0]);
            Assert.Equal(15.0f, X.Grad![0, 1]);
            Assert.Equal(11.0f, X.Grad![1, 0]);
            Assert.Equal(15.0f, X.Grad![1, 1]);

            // X = [[1, 2], [3, 4]] -> sum of each col of X is: col0 = 4, col1 = 6
            // dL/dW = [[1, 3], [2, 4]] @ [[1, 1], [1, 1]] = [[4, 4], [6, 6]]
            Assert.Equal(4.0f, W.Grad![0, 0]);
            Assert.Equal(4.0f, W.Grad![0, 1]);
            Assert.Equal(6.0f, W.Grad![1, 0]);
            Assert.Equal(6.0f, W.Grad![1, 1]);
        }

        [Fact]
        public void Autograd_ReLU_BackpropagatesOnlyThroughPositiveElements()
        {
            var x = new Variable(Tensor.FromArray(new float[] { -3.0f, 0.0f, 4.0f }), requiresGrad: true);
            var y = x.ReLU();
            var loss = y.Sum();

            loss.Backward();

            Assert.Equal(0.0f, x.Grad![0]);
            Assert.Equal(0.0f, x.Grad![1]);
            Assert.Equal(1.0f, x.Grad![2]);
        }

        [Fact]
        public void Autograd_Sigmoid_MatchesTheoreticalDerivative()
        {
            var x = new Variable(Tensor.FromArray(new[] { 0.0f }), requiresGrad: true);
            var y = x.Sigmoid(); // sigmoid(0) = 0.5
            y.Backward();

            // d/dx sigmoid(0) = 0.5 * (1 - 0.5) = 0.25
            Assert.True(Math.Abs(x.Grad![0] - 0.25f) < 1e-4f, $"Gradient was {x.Grad[0]}");
        }
    }
}
