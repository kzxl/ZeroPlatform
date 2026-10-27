using System;
using Xunit;
using ZeroSignal.Core.Optimization;

namespace ZeroSignal.Tests
{
    public class OptimizationTests
    {
        [Fact]
        public void LevenbergMarquardt_FitsExponentialDecay()
        {
            // Ground truth: y = a * exp(-b * x) + c
            double trueA = 4.0;
            double trueB = 0.5;
            double trueC = 1.0;

            int n = 40;
            double[] x = new double[n];
            double[] y = new double[n];

            for (int i = 0; i < n; i++)
            {
                x[i] = i * 0.2;
                y[i] = trueA * Math.Exp(-trueB * x[i]) + trueC;
            }

            var (fitA, fitB, fitC) = CurveFit.FitExponential(x, y, new[] { 2.0, 0.2, 0.5 });

            Assert.InRange(fitA, trueA - 0.05, trueA + 0.05);
            Assert.InRange(fitB, trueB - 0.05, trueB + 0.05);
            Assert.InRange(fitC, trueC - 0.05, trueC + 0.05);
        }

        [Fact]
        public void LevenbergMarquardt_FitsGaussianPeak()
        {
            // Ground truth: y = a * exp(-0.5 * ((x - mu)/sigma)^2) + c
            double trueA = 12.0;
            double trueMu = 5.0;
            double trueSigma = 1.2;
            double trueC = 1.5;

            int n = 50;
            double[] x = new double[n];
            double[] y = new double[n];

            for (int i = 0; i < n; i++)
            {
                x[i] = i * 0.2;
                double z = (x[i] - trueMu) / trueSigma;
                y[i] = trueA * Math.Exp(-0.5 * z * z) + trueC;
            }

            var (fitA, fitMu, fitSigma, fitC) = CurveFit.FitGaussian(x, y);

            Assert.InRange(fitA, trueA - 0.1, trueA + 0.1);
            Assert.InRange(fitMu, trueMu - 0.05, trueMu + 0.05);
            Assert.InRange(fitSigma, trueSigma - 0.05, trueSigma + 0.05);
            Assert.InRange(fitC, trueC - 0.1, trueC + 0.1);
        }

        [Fact]
        public void LevenbergMarquardt_FitsSineWave()
        {
            // Ground truth: y = a * sin(omega * x + phi) + c
            double trueA = 3.0;
            double trueOmega = 2.0;
            double truePhi = 0.5;
            double trueC = 2.0;

            int n = 60;
            double[] x = new double[n];
            double[] y = new double[n];

            for (int i = 0; i < n; i++)
            {
                x[i] = i * 0.1;
                y[i] = trueA * Math.Sin(trueOmega * x[i] + truePhi) + trueC;
            }

            var (fitA, fitOmega, fitPhi, fitC) = CurveFit.FitSine(x, y, new[] { 2.5, 2.1, 0.4, 1.8 });

            Assert.InRange(Math.Abs(fitA), trueA - 0.1, trueA + 0.1);
            Assert.InRange(fitOmega, trueOmega - 0.05, trueOmega + 0.05);
            Assert.InRange(fitC, trueC - 0.1, trueC + 0.1);
        }

        [Fact]
        public void LevenbergMarquardt_CustomNonLinearModel_Converges()
        {
            // Model: y = p0 / (1.0 + exp(-p1 * (x - p2))) (Logistic sigmoid)
            double trueL = 10.0;
            double trueK = 1.5;
            double trueX0 = 3.0;

            int n = 30;
            double[] x = new double[n];
            double[] y = new double[n];

            for (int i = 0; i < n; i++)
            {
                x[i] = i * 0.25;
                y[i] = trueL / (1.0 + Math.Exp(-trueK * (x[i] - trueX0)));
            }

            Func<double, double[], double> model = (xi, p) =>
            {
                return p[0] / (1.0 + Math.Exp(-p[1] * (xi - p[2])));
            };

            var res = LevenbergMarquardt.Minimize(model, x, y, new[] { 8.0, 1.0, 2.0 });

            Assert.True(res.Converged);
            Assert.InRange(res.Parameters[0], trueL - 0.1, trueL + 0.1);
            Assert.InRange(res.Parameters[1], trueK - 0.1, trueK + 0.1);
            Assert.InRange(res.Parameters[2], trueX0 - 0.1, trueX0 + 0.1);
        }
    }
}
