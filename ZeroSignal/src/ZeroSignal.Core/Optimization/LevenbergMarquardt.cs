using System;

namespace ZeroSignal.Core.Optimization
{
    public sealed class LevenbergMarquardtOptions
    {
        public int MaxIterations { get; set; } = 100;
        public double InitialLambda { get; set; } = 1e-3;
        public double GradientTolerance { get; set; } = 1e-8;
        public double ParameterTolerance { get; set; } = 1e-8;
        public double CostTolerance { get; set; } = 1e-8;
        public double FiniteDifferenceStep { get; set; } = 1e-7;
    }

    public sealed class LevenbergMarquardtResult
    {
        public double[] Parameters { get; }
        public double FinalCost { get; }
        public int Iterations { get; }
        public bool Converged { get; }
        public string TerminationReason { get; }

        public LevenbergMarquardtResult(
            double[] parameters,
            double finalCost,
            int iterations,
            bool converged,
            string terminationReason)
        {
            Parameters = parameters;
            FinalCost = finalCost;
            Iterations = iterations;
            Converged = converged;
            TerminationReason = terminationReason;
        }
    }

    /// <summary>
    /// Non-linear Least Squares solver using the Levenberg-Marquardt algorithm
    /// with adaptive damping and Marquardt parameter scaling.
    /// </summary>
    public static class LevenbergMarquardt
    {
        /// <summary>
        /// Minimizes the sum of squared residuals: 0.5 * sum((f(x_i, p) - y_i)^2).
        /// </summary>
        /// <param name="model">Model function f(x, p) predicting y given scalar x and parameter vector p.</param>
        /// <param name="x">Observed independent variable data.</param>
        /// <param name="y">Observed dependent variable data.</param>
        /// <param name="initialParameters">Initial parameter vector guess.</param>
        /// <param name="options">Solver convergence criteria and parameters.</param>
        public static LevenbergMarquardtResult Minimize(
            Func<double, double[], double> model,
            double[] x,
            double[] y,
            double[] initialParameters,
            LevenbergMarquardtOptions? options = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (x == null) throw new ArgumentNullException(nameof(x));
            if (y == null) throw new ArgumentNullException(nameof(y));
            if (initialParameters == null) throw new ArgumentNullException(nameof(initialParameters));
            if (x.Length != y.Length)
                throw new ArgumentException("Length of x and y arrays must match.");
            if (x.Length < initialParameters.Length)
                throw new ArgumentException("Number of data points must be at least the number of parameters.");

            options ??= new LevenbergMarquardtOptions();

            int n = x.Length;
            int pCount = initialParameters.Length;
            double[] p = (double[])initialParameters.Clone();

            double lambda = options.InitialLambda;
            double[] residuals = new double[n];
            double currentCost = ComputeResiduals(model, x, y, p, residuals);

            double[,] J = new double[n, pCount];
            double[,] H = new double[pCount, pCount];
            double[] g = new double[pCount];
            double[,] A = new double[pCount, pCount];
            double[] deltaP = new double[pCount];
            double[] trialP = new double[pCount];
            double[] trialResiduals = new double[n];

            int iter = 0;
            bool converged = false;
            string terminationReason = "Max iterations reached.";

            for (iter = 0; iter < options.MaxIterations; iter++)
            {
                // Compute Jacobian via central finite difference
                ComputeJacobian(model, x, p, options.FiniteDifferenceStep, J);

                // Compute Hessian approx H = J^T * J and gradient g = J^T * residuals
                for (int r = 0; r < pCount; r++)
                {
                    double gSum = 0.0;
                    for (int i = 0; i < n; i++)
                    {
                        gSum += J[i, r] * residuals[i];
                    }
                    g[r] = gSum;

                    for (int c = r; c < pCount; c++)
                    {
                        double hSum = 0.0;
                        for (int i = 0; i < n; i++)
                        {
                            hSum += J[i, r] * J[i, c];
                        }
                        H[r, c] = hSum;
                        H[c, r] = hSum;
                    }
                }

                // Check gradient infinity norm
                double maxGrad = 0.0;
                for (int r = 0; r < pCount; r++)
                {
                    double absG = Math.Abs(g[r]);
                    if (absG > maxGrad) maxGrad = absG;
                }

                if (maxGrad < options.GradientTolerance)
                {
                    converged = true;
                    terminationReason = "Gradient tolerance satisfied.";
                    break;
                }

                // Build augmented matrix A = H + lambda * diag(H)
                for (int r = 0; r < pCount; r++)
                {
                    for (int c = 0; c < pCount; c++)
                    {
                        A[r, c] = H[r, c];
                    }
                    double diag = Math.Abs(H[r, r]) > 1e-12 ? H[r, r] : 1.0;
                    A[r, r] += lambda * diag;
                }

                // Solve A * deltaP = -g
                double[] rhs = new double[pCount];
                for (int r = 0; r < pCount; r++) rhs[r] = -g[r];

                if (!SolveLinearSystem(A, rhs, deltaP, pCount))
                {
                    // Singular matrix: increase damping and retry
                    lambda *= 10.0;
                    continue;
                }

                // Check parameter update step norm
                double deltaNormSq = 0.0;
                double pNormSq = 0.0;
                for (int r = 0; r < pCount; r++)
                {
                    deltaNormSq += deltaP[r] * deltaP[r];
                    pNormSq += p[r] * p[r];
                    trialP[r] = p[r] + deltaP[r];
                }

                if (Math.Sqrt(deltaNormSq) < options.ParameterTolerance * (Math.Sqrt(pNormSq) + options.ParameterTolerance))
                {
                    converged = true;
                    terminationReason = "Parameter step tolerance satisfied.";
                    break;
                }

                // Evaluate cost at trial parameters
                double trialCost = ComputeResiduals(model, x, y, trialP, trialResiduals);

                if (trialCost < currentCost)
                {
                    // Successful step: accept update, decrease damping
                    double costReduction = currentCost - trialCost;
                    Array.Copy(trialP, p, pCount);
                    Array.Copy(trialResiduals, residuals, n);
                    currentCost = trialCost;
                    lambda = Math.Max(lambda / 10.0, 1e-12);

                    if (costReduction < options.CostTolerance * currentCost)
                    {
                        converged = true;
                        terminationReason = "Cost reduction tolerance satisfied.";
                        break;
                    }
                }
                else
                {
                    // Unsuccessful step: reject, increase damping
                    lambda = Math.Min(lambda * 10.0, 1e12);
                }
            }

            return new LevenbergMarquardtResult(p, currentCost, iter, converged, terminationReason);
        }

        private static double ComputeResiduals(
            Func<double, double[], double> model,
            double[] x,
            double[] y,
            double[] p,
            double[] residuals)
        {
            double cost = 0.0;
            for (int i = 0; i < x.Length; i++)
            {
                double r = model(x[i], p) - y[i];
                residuals[i] = r;
                cost += 0.5 * r * r;
            }
            return cost;
        }

        private static void ComputeJacobian(
            Func<double, double[], double> model,
            double[] x,
            double[] p,
            double step,
            double[,] J)
        {
            int n = x.Length;
            int pCount = p.Length;
            double[] pCopy = (double[])p.Clone();

            for (int j = 0; j < pCount; j++)
            {
                double originalVal = pCopy[j];
                double h = step * Math.Max(Math.Abs(originalVal), 1.0);
                double inv2h = 1.0 / (2.0 * h);

                pCopy[j] = originalVal + h;
                double[] yPlus = new double[n];
                for (int i = 0; i < n; i++) yPlus[i] = model(x[i], pCopy);

                pCopy[j] = originalVal - h;
                double[] yMinus = new double[n];
                for (int i = 0; i < n; i++) yMinus[i] = model(x[i], pCopy);

                pCopy[j] = originalVal;

                for (int i = 0; i < n; i++)
                {
                    J[i, j] = (yPlus[i] - yMinus[i]) * inv2h;
                }
            }
        }

        /// <summary>
        /// Solves linear system A * x = b via Gaussian elimination with partial pivoting.
        /// </summary>
        private static bool SolveLinearSystem(double[,] A, double[] b, double[] x, int n)
        {
            double[,] M = (double[,])A.Clone();
            double[] v = (double[])b.Clone();

            for (int col = 0; col < n; col++)
            {
                // Partial pivoting
                int maxRow = col;
                double maxVal = Math.Abs(M[col, col]);
                for (int row = col + 1; row < n; row++)
                {
                    double val = Math.Abs(M[row, col]);
                    if (val > maxVal)
                    {
                        maxVal = val;
                        maxRow = row;
                    }
                }

                if (maxVal < 1e-15) return false; // Singular matrix

                if (maxRow != col)
                {
                    for (int k = col; k < n; k++)
                    {
                        double tmp = M[col, k];
                        M[col, k] = M[maxRow, k];
                        M[maxRow, k] = tmp;
                    }
                    double tmpV = v[col];
                    v[col] = v[maxRow];
                    v[maxRow] = tmpV;
                }

                // Elimination
                double pivot = M[col, col];
                for (int row = col + 1; row < n; row++)
                {
                    double factor = M[row, col] / pivot;
                    for (int k = col + 1; k < n; k++)
                    {
                        M[row, k] -= factor * M[col, k];
                    }
                    v[row] -= factor * v[col];
                }
            }

            // Back substitution
            for (int row = n - 1; row >= 0; row--)
            {
                double sum = v[row];
                for (int k = row + 1; k < n; k++)
                {
                    sum -= M[row, k] * x[k];
                }
                x[row] = sum / M[row, row];
            }

            return true;
        }
    }
}
