using System;
using System.Linq;
using Xunit;
using ZeroNeural.Core.Autograd;
using ZeroNeural.Core.nn;
using ZeroTensor.Core;

namespace ZeroNeural.Tests
{
    public class LayersTests
    {
        [Fact]
        public void Linear_Layer_ForwardAndBackward_Succeeds()
        {
            var linear = new Linear(inFeatures: 4, outFeatures: 2, hasBias: true);
            Assert.Equal(2, linear.Parameters().Count()); // Weight + Bias

            var input = new Variable(Tensor.Uniform(0f, 1f, seed: 1, 3, 4), requiresGrad: true); // Batch = 3
            var output = linear.Forward(input);

            Assert.Equal(new TensorShape(3, 2), output.Shape);

            var loss = output.Sum();
            loss.Backward();

            Assert.NotNull(linear.Weight.Grad);
            Assert.NotNull(linear.Bias?.Grad);
            Assert.NotNull(input.Grad);
            Assert.Equal(new TensorShape(4, 2), linear.Weight.Grad.Shape);
            Assert.Equal(new TensorShape(1, 2), linear.Bias!.Grad.Shape);
            Assert.Equal(new TensorShape(3, 4), input.Grad.Shape);
        }

        [Fact]
        public void Sequential_Container_DiscoversAllChildParameters()
        {
            var model = new Sequential(
                new Linear(8, 16),
                new ReLU(),
                new Linear(16, 4),
                new Sigmoid()
            );

            // Linear(8, 16) has 2 params (W, B); Linear(16, 4) has 2 params (W, B) -> total 4 params
            var parameters = model.Parameters().ToList();
            Assert.Equal(4, parameters.Count);

            var input = new Variable(Tensor.Uniform(0f, 1f, seed: 123, 2, 8));
            var output = model.Forward(input);

            Assert.Equal(new TensorShape(2, 4), output.Shape);
        }

        [Fact]
        public void Conv2D_Layer_ComputesCorrectSpatialDimensionsAndGradients()
        {
            // Input: Batch=2, Channels=3, H=8, W=8
            // Conv: OutC=4, InC=3, Kernel=3, Stride=1, Padding=1 -> Out: (2, 4, 8, 8)
            var conv = new Conv2D(inChannels: 3, outChannels: 4, kernelSize: 3, stride: 1, padding: 1, hasBias: true);
            var input = new Variable(Tensor.Uniform(0f, 1f, seed: 42, 2, 3, 8, 8), requiresGrad: true);

            var output = conv.Forward(input);
            Assert.Equal(new TensorShape(2, 4, 8, 8), output.Shape);

            var loss = output.Sum();
            loss.Backward();

            Assert.NotNull(conv.Weight.Grad);
            Assert.NotNull(conv.Bias?.Grad);
            Assert.NotNull(input.Grad);

            Assert.Equal(new TensorShape(4, 3, 3, 3), conv.Weight.Grad.Shape);
            Assert.Equal(new TensorShape(4), conv.Bias!.Grad.Shape);
            Assert.Equal(new TensorShape(2, 3, 8, 8), input.Grad.Shape);
        }

        [Fact]
        public void BatchNorm2d_UpdatesRunningStatsDuringTraining()
        {
            var bn = new BatchNorm2d(numFeatures: 2);
            var input = new Variable(Tensor.Uniform(10f, 20f, seed: 99, 4, 2, 4, 4));

            bn.Train();
            var outTrain = bn.Forward(input);

            // Verify running stats moved from initial values (mean was 0, var was 1)
            Assert.True(bn.RunningMean[0] > 0f);
            Assert.True(bn.RunningMean[1] > 0f);

            // Switch to Eval mode
            bn.Eval();
            var outEval = bn.Forward(input);
            Assert.Equal(outTrain.Shape, outEval.Shape);
        }

        [Fact]
        public void Dropout_BehavesDifferentlyInTrainVsEval()
        {
            var dropout = new Dropout(p: 0.5f, seed: 100);
            var input = new Variable(Tensor.Ones(100));

            dropout.Train();
            var trainOut = dropout.Forward(input);
            int zeroCount = 0;
            trainOut.Data.ForEachElement(v => { if (v == 0f) zeroCount++; });
            Assert.True(zeroCount > 20 && zeroCount < 80, $"Zero count was {zeroCount}");

            dropout.Eval();
            var evalOut = dropout.Forward(input);
            evalOut.Data.ForEachElement(v => Assert.Equal(1.0f, v));
        }
    }
}
