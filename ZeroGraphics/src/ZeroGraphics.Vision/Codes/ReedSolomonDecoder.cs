using System;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// Pure C# Reed-Solomon error correction decoder using Extended Euclidean algorithm,
    /// Chien search root finding, and Forney error magnitude evaluation over Galois Field GF(2^m).
    /// </summary>
    public sealed class ReedSolomonDecoder
    {
        private readonly GenericGF _field;

        public ReedSolomonDecoder(GenericGF field)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
        }

        /// <summary>
        /// Attempts to detect and correct errors in-place within the received codeword array.
        /// </summary>
        /// <param name="received">Array containing data codewords followed by error correction (parity) codewords.</param>
        /// <param name="twoS">Number of error correction codewords available (2t).</param>
        /// <returns>True if decoding and error correction succeeded; false if errors exceeded correction capacity.</returns>
        public bool Decode(int[] received, int twoS)
        {
            if (received == null || received.Length == 0 || twoS <= 0 || twoS >= received.Length)
                return false;

            var poly = new GenericGFPoly(_field, received);
            int[] syndromeCoefficients = new int[twoS];
            bool noError = true;

            for (int i = 0; i < twoS; i++)
            {
                int eval = poly.EvaluateAt(_field.Exp(i + _field.GeneratorBase));
                syndromeCoefficients[twoS - 1 - i] = eval;
                if (eval != 0)
                {
                    noError = false;
                }
            }

            if (noError)
                return true;

            var syndrome = new GenericGFPoly(_field, syndromeCoefficients);
            var sigmaOmega = RunEuclideanAlgorithm(_field.BuildMonomial(twoS, 1), syndrome, twoS);
            if (sigmaOmega == null)
                return false;

            var sigma = sigmaOmega[0];
            var omega = sigmaOmega[1];

            int[]? errorLocations = FindErrorLocations(sigma);
            if (errorLocations == null)
                return false;

            int[] errorMagnitudes = FindErrorMagnitudes(omega, errorLocations);

            for (int i = 0; i < errorLocations.Length; i++)
            {
                int position = received.Length - 1 - _field.Log(errorLocations[i]);
                if (position < 0 || position >= received.Length)
                    return false;

                received[position] = GenericGF.AddOrSubtract(received[position], errorMagnitudes[i]);
            }

            return true;
        }

        private GenericGFPoly[]? RunEuclideanAlgorithm(GenericGFPoly a, GenericGFPoly b, int R)
        {
            if (a.Degree < b.Degree)
            {
                var temp = a;
                a = b;
                b = temp;
            }

            var rLast = a;
            var r = b;
            var tLast = _field.Zero;
            var t = _field.One;

            while (2 * r.Degree >= R)
            {
                var rLastLast = rLast;
                var tLastLast = tLast;
                rLast = r;
                tLast = t;

                if (rLast.IsZero)
                    return null;

                r = rLastLast;
                var q = _field.Zero;
                int denominatorLeadingTerm = rLast.GetCoefficient(rLast.Degree);
                int dltInverse = _field.Inverse(denominatorLeadingTerm);

                while (r.Degree >= rLast.Degree && !r.IsZero)
                {
                    int degreeDiff = r.Degree - rLast.Degree;
                    int scale = _field.Multiply(r.GetCoefficient(r.Degree), dltInverse);
                    q = q.AddOrSubtract(_field.BuildMonomial(degreeDiff, scale));
                    r = r.AddOrSubtract(rLast.MultiplyByMonomial(degreeDiff, scale));
                }

                t = q.Multiply(tLast).AddOrSubtract(tLastLast);
            }

            int sigmaTildeAtZero = t.GetCoefficient(0);
            if (sigmaTildeAtZero == 0)
                return null;

            int inverse = _field.Inverse(sigmaTildeAtZero);
            var sigma = t.Multiply(inverse);
            var omega = r.Multiply(inverse);
            return new GenericGFPoly[] { sigma, omega };
        }

        private int[]? FindErrorLocations(GenericGFPoly errorLocator)
        {
            int numErrors = errorLocator.Degree;
            if (numErrors == 1)
            {
                return new int[] { errorLocator.GetCoefficient(1) };
            }

            int[] result = new int[numErrors];
            int e = 0;
            for (int i = 1; i < _field.Size && e < numErrors; i++)
            {
                if (errorLocator.EvaluateAt(i) == 0)
                {
                    result[e] = _field.Inverse(i);
                    e++;
                }
            }

            if (e != numErrors)
                return null;

            return result;
        }

        private int[] FindErrorMagnitudes(GenericGFPoly errorEvaluator, int[] errorLocations)
        {
            int s = errorLocations.Length;
            int[] result = new int[s];
            for (int i = 0; i < s; i++)
            {
                int xiInverse = _field.Inverse(errorLocations[i]);
                int denominator = 1;
                for (int j = 0; j < s; j++)
                {
                    if (i != j)
                    {
                        int term = _field.Multiply(errorLocations[j], xiInverse);
                        int termPlus1 = (term & 0x01) == 0 ? term | 1 : term & ~1;
                        denominator = _field.Multiply(denominator, termPlus1);
                    }
                }

                result[i] = _field.Multiply(errorEvaluator.EvaluateAt(xiInverse), _field.Inverse(denominator));
                if (_field.GeneratorBase != 0)
                {
                    result[i] = _field.Multiply(result[i], xiInverse);
                }
            }

            return result;
        }
    }
}
