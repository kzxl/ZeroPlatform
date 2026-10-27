using System;
using Xunit;
using ZeroNeural.Core.Autograd;
using ZeroNeural.Core.Loss;
using ZeroNeural.Core.nn;
using ZeroNeural.Core.Optim;
using ZeroTensor.Core;

namespace ZeroNeural.Tests
{
    public class TrainingConvergenceTests
    {
        [Fact]
        public void MLP_LearnsNonlinearXORProblem_AndConverges()
        {
            // XOR Truth Table:
            // [0, 0] -> 0
            // [0, 1] -> 1
            // [1, 0] -> 1
            // [1, 1] -> 0
            var XData = Tensor.FromArray(new float[]
            {
                0f, 0f,
                0f, 1f,
                1f, 0f,
                1f, 1f
            }, 4, 2);

            var YData = Tensor.FromArray(new float[]
            {
                0f,
                1f,
                1f,
                0f
            }, 4, 1);

            var X = new Variable(XData, requiresGrad: false);
            var Y = new Variable(YData, requiresGrad: false);

            // Architecture: Linear(2, 8) -> Tanh -> Linear(8, 1) -> Sigmoid
            var model = new Sequential(
                new Linear(2, 8, seed: 42),
                new Tanh(),
                new Linear(8, 1, seed: 101),
                new Sigmoid()
            );

            var optimizer = new Adam(model.Parameters(), learningRate: 0.08f);

            float initialLoss = 0f;
            float finalLoss = 0f;

            // Train for 200 epochs
            for (int epoch = 0; epoch < 200; epoch++)
            {
                optimizer.ZeroGrad();

                var pred = model.Forward(X);
                var loss = MSELoss.Compute(pred, Y);

                if (epoch == 0)
                {
                    initialLoss = loss.Data.Scalar;
                }
                finalLoss = loss.Data.Scalar;

                loss.Backward();
                optimizer.Step();
            }

            // Verify strong loss convergence (loss must drop dramatically)
            Assert.True(finalLoss < 0.05f, $"Final loss was {finalLoss}, initial was {initialLoss}");

            // Verify predictions match XOR table
            var finalPred = model.Forward(X);
            Assert.True(finalPred.Data[0, 0] < 0.2f, $"[0, 0] prediction was {finalPred.Data[0, 0]} (expected < 0.2)");
            Assert.True(finalPred.Data[1, 0] > 0.8f, $"[0, 1] prediction was {finalPred.Data[1, 0]} (expected > 0.8)");
            Assert.True(finalPred.Data[2, 0] > 0.8f, $"[1, 0] prediction was {finalPred.Data[2, 0]} (expected > 0.8)");
            Assert.True(finalPred.Data[3, 0] < 0.2f, $"[1, 1] prediction was {finalPred.Data[3, 0]} (expected < 0.2)");
        }

        [Fact]
        public void MultiClassClassifier_ConvergesWithCrossEntropyLoss()
        {
            // 3-class classification problem with 3 sample points
            var XData = Tensor.FromArray(new float[]
            {
                1.0f, 0.0f,  // Class 0
                0.0f, 1.0f,  // Class 1
                -1.0f, -1.0f // Class 2
            }, 3, 2);

            int[] targets = new[] { 0, 1, 2 };

            var X = new Variable(XData);
            var model = new Sequential(
                new Linear(2, 6, seed: 7),
                new ReLU(),
                new Linear(6, 3, seed: 8)
            );

            var opt = new Adam(model.Parameters(), learningRate: 0.05f);

            float initialLoss = 0f;
            float finalLoss = 0f;

            for (int epoch = 0; epoch < 100; epoch++)
            {
                opt.ZeroGrad();
                var logits = model.Forward(X);
                var loss = CrossEntropyLoss.Compute(logits, targets);

                if (epoch == 0) initialLoss = loss.Data.Scalar;
                finalLoss = loss.Data.Scalar;

                loss.Backward();
                opt.Step();
            }

            Assert.True(finalLoss < initialLoss * 0.1f, $"Final loss {finalLoss} vs initial {initialLoss}");
        }
    }
}
