using System;
using Xunit;
using ZeroNeural.Core.Autograd;
using ZeroNeural.Core.Optim;
using ZeroTensor.Core;

namespace ZeroNeural.Tests
{
    public class OptimizersTests
    {
        [Fact]
        public void SGD_UpdatesWeightsInDirectionOfNegativeGradient()
        {
            var w = new Variable(Tensor.FromArray(new[] { 10.0f }), requiresGrad: true);
            w.Grad = Tensor.FromArray(new[] { 2.0f }); // positive gradient -> weight should decrease

            var opt = new SGD(new[] { w }, learningRate: 0.1f);
            opt.Step();

            // w_new = 10 - 0.1 * 2 = 9.8
            Assert.True(Math.Abs(w.Data[0] - 9.8f) < 1e-4f, $"w = {w.Data[0]}, expected 9.8");
        }

        [Fact]
        public void SGD_WithMomentum_AcceleratesStep()
        {
            var w = new Variable(Tensor.FromArray(new[] { 10.0f }), requiresGrad: true);
            var opt = new SGD(new[] { w }, learningRate: 0.1f, momentum: 0.9f);

            // Step 1: grad = 1.0 -> v = 1.0 -> w = 10 - 0.1*1.0 = 9.9
            w.Grad = Tensor.FromArray(new[] { 1.0f });
            opt.Step();
            Assert.True(Math.Abs(w.Data[0] - 9.9f) < 1e-4f);

            // Step 2: grad = 1.0 -> v = 0.9*1.0 + 1.0 = 1.9 -> w = 9.9 - 0.1*1.9 = 9.71
            w.Grad = Tensor.FromArray(new[] { 1.0f });
            opt.Step();
            Assert.True(Math.Abs(w.Data[0] - 9.71f) < 1e-3f, $"w = {w.Data[0]}, expected 9.71");
        }

        [Fact]
        public void Adam_AdaptsStepSizePerParameter()
        {
            var w = new Variable(Tensor.FromArray(new[] { 5.0f }), requiresGrad: true);
            var opt = new Adam(new[] { w }, learningRate: 0.1f);

            w.Grad = Tensor.FromArray(new[] { 2.0f });
            opt.Step();

            // After 1 step of Adam, weight must have decreased
            Assert.True(w.Data[0] < 5.0f, $"w = {w.Data[0]}, expected < 5.0");
        }

        [Fact]
        public void AdamW_DecoupledWeightDecay_ReducesWeightMagnitude()
        {
            var w = new Variable(Tensor.FromArray(new[] { 10.0f }), requiresGrad: true);
            w.Grad = Tensor.FromArray(new[] { 0.0f }); // zero gradient: purely weight decay active

            var opt = new AdamW(new[] { w }, learningRate: 0.1f, weightDecay: 0.2f);
            opt.Step();

            // w_new = w * (1 - lr * weightDecay) = 10 * (1 - 0.02) = 9.8
            Assert.True(Math.Abs(w.Data[0] - 9.8f) < 1e-3f, $"w = {w.Data[0]}, expected 9.8");
        }
    }
}
