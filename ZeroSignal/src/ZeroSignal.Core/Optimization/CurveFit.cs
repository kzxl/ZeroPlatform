using System;
using System.Linq;

namespace ZeroSignal.Core.Optimization
{
    /// <summary>
    /// High-level industrial curve fitting functions utilizing Levenberg-Marquardt optimization.
    /// Provides Gaussian peak fitting, exponential decay, sinusoidal fitting, and general custom models.
    /// </summary>
    public static class CurveFit
    {
        /// <summary>
        /// Fits a Gaussian peak model: y(x) = a * exp(-0.5 * ((x - mu) / sigma)^2) + c.
        /// Returns (a, mu, sigma, c).
        /// </summary>
        public static (double a, double mu, double sigma, double c) FitGaussian(
            double[] x,
            double[] y,
            double[]? initialGuess = null)
        {
            if (x == null || y == null) throw new ArgumentNullException();
            if (x.Length != y.Length || x.Length < 4)
                throw new ArgumentException("At least 4 data points are required to fit a Gaussian peak.");

            // Automatic heuristic initial guess if none provided
            if (initialGuess == null)
            {
                double minY = y.Min();
                double maxY = y.Max();
                int maxIdx = 0;
                for (int i = 0; i < y.Length; i++)
                {
                    if (y[i] == maxY) { maxIdx = i; break; }
                }

                double a = maxY - minY;
                double mu = x[maxIdx];
                double sigma = Math.Max((x.Max() - x.Min()) / 6.0, 1e-3);
                double c = minY;
                initialGuess = new[] { a, mu, sigma, c };
            }

            Func<double, double[], double> model = (xi, p) =>
            {
                double a = p[0];
                double mu = p[1];
                double sigma = Math.Max(Math.Abs(p[2]), 1e-9);
                double c = p[3];
                double z = (xi - mu) / sigma;
                return a * Math.Exp(-0.5 * z * z) + c;
            };

            var res = LevenbergMarquardt.Minimize(model, x, y, initialGuess);
            return (res.Parameters[0], res.Parameters[1], Math.Abs(res.Parameters[2]), res.Parameters[3]);
        }

        /// <summary>
        /// Fits an exponential decay/growth model: y(x) = a * exp(-b * x) + c.
        /// Returns (a, b, c).
        /// </summary>
        public static (double a, double b, double c) FitExponential(
            double[] x,
            double[] y,
            double[]? initialGuess = null)
        {
            if (x == null || y == null) throw new ArgumentNullException();
            if (x.Length != y.Length || x.Length < 3)
                throw new ArgumentException("At least 3 data points are required to fit an exponential curve.");

            if (initialGuess == null)
            {
                double c = y.Last();
                double a = y.First() - c;
                double b = 1.0;
                initialGuess = new[] { a, b, c };
            }

            Func<double, double[], double> model = (xi, p) =>
            {
                double a = p[0];
                double b = p[1];
                double c = p[2];
                return a * Math.Exp(-b * xi) + c;
            };

            var res = LevenbergMarquardt.Minimize(model, x, y, initialGuess);
            return (res.Parameters[0], res.Parameters[1], res.Parameters[2]);
        }

        /// <summary>
        /// Fits a sinusoidal oscillation: y(x) = a * sin(omega * x + phi) + c.
        /// Returns (a, omega, phi, c).
        /// </summary>
        public static (double a, double omega, double phi, double c) FitSine(
            double[] x,
            double[] y,
            double[]? initialGuess = null)
        {
            if (x == null || y == null) throw new ArgumentNullException();
            if (x.Length != y.Length || x.Length < 4)
                throw new ArgumentException("At least 4 data points are required to fit a sine wave.");

            if (initialGuess == null)
            {
                double minY = y.Min();
                double maxY = y.Max();
                double a = (maxY - minY) / 2.0;
                double c = (maxY + minY) / 2.0;
                double spanX = x.Max() - x.Min();
                double omega = 2.0 * Math.PI / Math.Max(spanX, 1e-3);
                double phi = 0.0;
                initialGuess = new[] { a, omega, phi, c };
            }

            Func<double, double[], double> model = (xi, p) =>
            {
                double a = p[0];
                double omega = p[1];
                double phi = p[2];
                double c = p[3];
                return a * Math.Sin(omega * xi + phi) + c;
            };

            var res = LevenbergMarquardt.Minimize(model, x, y, initialGuess);
            return (res.Parameters[0], res.Parameters[1], res.Parameters[2], res.Parameters[3]);
        }
    }
}
