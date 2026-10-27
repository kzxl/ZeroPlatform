using System;

namespace ZeroSignal.Core.Estimation
{
    /// <summary>
    /// Extended Kalman Filter (EKF) for non-linear state estimation and dynamic sensor fusion.
    /// Pure C# with zero external dependencies.
    /// </summary>
    public sealed class ExtendedKalmanFilter
    {
        public int StateDim { get; }
        public int MeasurementDim { get; }

        public double[] State { get; set; }
        public double[,] Covariance { get; set; }
        public double[,] Q { get; set; }
        public double[,] R { get; set; }

        public Func<double[], double[]?, double[]> StateTransition { get; }
        public Func<double[], double[]> MeasurementFunction { get; }
        public Func<double[], double[]?, double[,]>? StateJacobian { get; set; }
        public Func<double[], double[,]>? MeasurementJacobian { get; set; }

        public ExtendedKalmanFilter(
            int stateDim,
            int measurementDim,
            Func<double[], double[]?, double[]> stateTransition,
            Func<double[], double[]> measurementFunction,
            double[] initialState,
            double[,] initialCovariance,
            double[,] processNoiseQ,
            double[,] measurementNoiseR)
        {
            if (stateDim <= 0) throw new ArgumentOutOfRangeException(nameof(stateDim));
            if (measurementDim <= 0) throw new ArgumentOutOfRangeException(nameof(measurementDim));

            StateDim = stateDim;
            MeasurementDim = measurementDim;
            StateTransition = stateTransition ?? throw new ArgumentNullException(nameof(stateTransition));
            MeasurementFunction = measurementFunction ?? throw new ArgumentNullException(nameof(measurementFunction));

            State = (double[])initialState.Clone();
            Covariance = (double[,])initialCovariance.Clone();
            Q = (double[,])processNoiseQ.Clone();
            R = (double[,])measurementNoiseR.Clone();
        }

        public void Predict(double[]? controlInput = null)
        {
            // 1. x_priori = f(x, u)
            double[] xPriori = StateTransition(State, controlInput);

            // 2. Jacobian F
            double[,] F = StateJacobian != null ? StateJacobian(State, controlInput) : ComputeNumericalJacobianF(StateTransition, State, controlInput);

            // 3. P_priori = F * P * F^T + Q
            double[,] FP = Multiply(F, Covariance);
            double[,] FPFt = Multiply(FP, Transpose(F));
            double[,] pPriori = Add(FPFt, Q);

            State = xPriori;
            Covariance = pPriori;
        }

        public void Update(double[] measurement)
        {
            if (measurement == null || measurement.Length != MeasurementDim)
                throw new ArgumentException($"Measurement vector must have dimension {MeasurementDim}.", nameof(measurement));

            // 1. Innovation / Residual y = z - h(x_priori)
            double[] zPred = MeasurementFunction(State);
            double[] y = new double[MeasurementDim];
            for (int i = 0; i < MeasurementDim; i++)
            {
                y[i] = measurement[i] - zPred[i];
            }

            // 2. Jacobian H
            double[,] H = MeasurementJacobian != null ? MeasurementJacobian(State) : ComputeNumericalJacobianH(MeasurementFunction, State, MeasurementDim);

            // 3. Innovation Covariance S = H * P * H^T + R
            double[,] HP = Multiply(H, Covariance);
            double[,] HPHt = Multiply(HP, Transpose(H));
            double[,] S = Add(HPHt, R);

            // 4. Kalman Gain K = P * H^T * S^-1
            double[,] Sinv = Invert(S);
            double[,] PHt = Multiply(Covariance, Transpose(H));
            double[,] K = Multiply(PHt, Sinv);

            // 5. Updated State x = x + K * y
            double[] Ky = Multiply(K, y);
            for (int i = 0; i < StateDim; i++)
            {
                State[i] += Ky[i];
            }

            // 6. Updated Covariance P = (I - K * H) * P
            double[,] I = Identity(StateDim);
            double[,] KH = Multiply(K, H);
            double[,] IminusKH = Subtract(I, KH);
            Covariance = Multiply(IminusKH, Covariance);
        }

        #region Numerical Jacobians

        private static double[,] ComputeNumericalJacobianF(Func<double[], double[]?, double[]> f, double[] x, double[]? u)
        {
            int n = x.Length;
            double[,] J = new double[n, n];
            const double h = 1e-6;
            double[] xPlus = (double[])x.Clone();
            double[] xMinus = (double[])x.Clone();

            for (int j = 0; j < n; j++)
            {
                xPlus[j] = x[j] + h;
                xMinus[j] = x[j] - h;

                double[] fPlus = f(xPlus, u);
                double[] fMinus = f(xMinus, u);

                for (int i = 0; i < n; i++)
                {
                    J[i, j] = (fPlus[i] - fMinus[i]) / (2.0 * h);
                }

                xPlus[j] = x[j];
                xMinus[j] = x[j];
            }

            return J;
        }

        private static double[,] ComputeNumericalJacobianH(Func<double[], double[]> hFunc, double[] x, int m)
        {
            int n = x.Length;
            double[,] J = new double[m, n];
            const double h = 1e-6;
            double[] xPlus = (double[])x.Clone();
            double[] xMinus = (double[])x.Clone();

            for (int j = 0; j < n; j++)
            {
                xPlus[j] = x[j] + h;
                xMinus[j] = x[j] - h;

                double[] hPlus = hFunc(xPlus);
                double[] hMinus = hFunc(xMinus);

                for (int i = 0; i < m; i++)
                {
                    J[i, j] = (hPlus[i] - hMinus[i]) / (2.0 * h);
                }

                xPlus[j] = x[j];
                xMinus[j] = x[j];
            }

            return J;
        }

        #endregion

        #region Matrix Operations

        private static double[,] Multiply(double[,] A, double[,] B)
        {
            int rA = A.GetLength(0);
            int cA = A.GetLength(1);
            int rB = B.GetLength(0);
            int cB = B.GetLength(1);
            if (cA != rB) throw new ArgumentException("Matrix dimensions do not match for multiplication.");

            double[,] C = new double[rA, cB];
            for (int i = 0; i < rA; i++)
            {
                for (int j = 0; j < cB; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < cA; k++) sum += A[i, k] * B[k, j];
                    C[i, j] = sum;
                }
            }
            return C;
        }

        private static double[] Multiply(double[,] A, double[] x)
        {
            int r = A.GetLength(0);
            int c = A.GetLength(1);
            if (c != x.Length) throw new ArgumentException("Matrix and vector dimension mismatch.");

            double[] result = new double[r];
            for (int i = 0; i < r; i++)
            {
                double sum = 0;
                for (int j = 0; j < c; j++) sum += A[i, j] * x[j];
                result[i] = sum;
            }
            return result;
        }

        private static double[,] Transpose(double[,] A)
        {
            int r = A.GetLength(0);
            int c = A.GetLength(1);
            double[,] T = new double[c, r];
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                    T[j, i] = A[i, j];
            return T;
        }

        private static double[,] Add(double[,] A, double[,] B)
        {
            int r = A.GetLength(0);
            int c = A.GetLength(1);
            double[,] C = new double[r, c];
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                    C[i, j] = A[i, j] + B[i, j];
            return C;
        }

        private static double[,] Subtract(double[,] A, double[,] B)
        {
            int r = A.GetLength(0);
            int c = A.GetLength(1);
            double[,] C = new double[r, c];
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                    C[i, j] = A[i, j] - B[i, j];
            return C;
        }

        private static double[,] Identity(int n)
        {
            double[,] I = new double[n, n];
            for (int i = 0; i < n; i++) I[i, i] = 1.0;
            return I;
        }

        private static double[,] Invert(double[,] A)
        {
            int n = A.GetLength(0);
            if (n != A.GetLength(1)) throw new ArgumentException("Matrix must be square for inversion.");

            double[,] aug = new double[n, 2 * n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++) aug[i, j] = A[i, j];
                aug[i, n + i] = 1.0;
            }

            // Gauss-Jordan elimination with partial pivoting
            for (int i = 0; i < n; i++)
            {
                // Find pivot
                int pivot = i;
                double maxVal = Math.Abs(aug[i, i]);
                for (int k = i + 1; k < n; k++)
                {
                    if (Math.Abs(aug[k, i]) > maxVal)
                    {
                        maxVal = Math.Abs(aug[k, i]);
                        pivot = k;
                    }
                }

                if (maxVal < 1e-15) throw new InvalidOperationException("Matrix is singular and cannot be inverted.");

                // Swap rows if needed
                if (pivot != i)
                {
                    for (int j = 0; j < 2 * n; j++)
                    {
                        double temp = aug[i, j];
                        aug[i, j] = aug[pivot, j];
                        aug[pivot, j] = temp;
                    }
                }

                // Scale row i
                double div = aug[i, i];
                for (int j = 0; j < 2 * n; j++) aug[i, j] /= div;

                // Eliminate other rows
                for (int k = 0; k < n; k++)
                {
                    if (k != i)
                    {
                        double factor = aug[k, i];
                        for (int j = 0; j < 2 * n; j++) aug[k, j] -= factor * aug[i, j];
                    }
                }
            }

            double[,] inv = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    inv[i, j] = aug[i, n + j];

            return inv;
        }

        #endregion
    }
}
