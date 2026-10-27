using System;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// Represents a polynomial with coefficients from a Galois Field GenericGF.
    /// Internal coefficient representation is big-endian (coefficients[0] is the highest-degree term).
    /// </summary>
    public sealed class GenericGFPoly
    {
        private readonly GenericGF _field;
        private readonly int[] _coefficients;

        public GenericGF Field => _field;
        public int[] Coefficients => _coefficients;
        public int Degree => _coefficients.Length - 1;
        public bool IsZero => _coefficients[0] == 0;

        public GenericGFPoly(GenericGF field, int[] coefficients)
        {
            if (coefficients == null || coefficients.Length == 0)
                throw new ArgumentException("Coefficients cannot be null or empty.");

            _field = field;
            int coefficientsLength = coefficients.Length;

            if (coefficientsLength > 1 && coefficients[0] == 0)
            {
                // Strip leading zero coefficients
                int firstNonZero = 1;
                while (firstNonZero < coefficientsLength && coefficients[firstNonZero] == 0)
                {
                    firstNonZero++;
                }

                if (firstNonZero == coefficientsLength)
                {
                    _coefficients = new int[] { 0 };
                }
                else
                {
                    _coefficients = new int[coefficientsLength - firstNonZero];
                    Array.Copy(coefficients, firstNonZero, _coefficients, 0, _coefficients.Length);
                }
            }
            else
            {
                _coefficients = coefficients;
            }
        }

        public int GetCoefficient(int degree)
        {
            return _coefficients[_coefficients.Length - 1 - degree];
        }

        public int EvaluateAt(int a)
        {
            if (a == 0)
                return GetCoefficient(0);

            if (a == 1)
            {
                int result = 0;
                foreach (int coefficient in _coefficients)
                {
                    result = GenericGF.AddOrSubtract(result, coefficient);
                }
                return result;
            }

            int y = _coefficients[0];
            for (int i = 1; i < _coefficients.Length; i++)
            {
                y = GenericGF.AddOrSubtract(_field.Multiply(a, y), _coefficients[i]);
            }
            return y;
        }

        public GenericGFPoly AddOrSubtract(GenericGFPoly other)
        {
            if (!ReferenceEquals(_field, other._field))
                throw new ArgumentException("GenericGFPolys must have the same field.");

            if (IsZero) return other;
            if (other.IsZero) return this;

            int[] smallerCoefficients = _coefficients;
            int[] largerCoefficients = other._coefficients;
            if (smallerCoefficients.Length > largerCoefficients.Length)
            {
                int[] temp = smallerCoefficients;
                smallerCoefficients = largerCoefficients;
                largerCoefficients = temp;
            }

            int[] sumDiff = new int[largerCoefficients.Length];
            int lengthDiff = largerCoefficients.Length - smallerCoefficients.Length;
            Array.Copy(largerCoefficients, 0, sumDiff, 0, lengthDiff);

            for (int i = lengthDiff; i < largerCoefficients.Length; i++)
            {
                sumDiff[i] = GenericGF.AddOrSubtract(smallerCoefficients[i - lengthDiff], largerCoefficients[i]);
            }

            return new GenericGFPoly(_field, sumDiff);
        }

        public GenericGFPoly Multiply(GenericGFPoly other)
        {
            if (!ReferenceEquals(_field, other._field))
                throw new ArgumentException("GenericGFPolys must have the same field.");

            if (IsZero || other.IsZero)
                return _field.Zero;

            int[] aCoefficients = _coefficients;
            int aLength = aCoefficients.Length;
            int[] bCoefficients = other._coefficients;
            int bLength = bCoefficients.Length;
            int[] product = new int[aLength + bLength - 1];

            for (int i = 0; i < aLength; i++)
            {
                int aCoeff = aCoefficients[i];
                for (int j = 0; j < bLength; j++)
                {
                    product[i + j] = GenericGF.AddOrSubtract(product[i + j], _field.Multiply(aCoeff, bCoefficients[j]));
                }
            }

            return new GenericGFPoly(_field, product);
        }

        public GenericGFPoly Multiply(int scalar)
        {
            if (scalar == 0) return _field.Zero;
            if (scalar == 1) return this;

            int size = _coefficients.Length;
            int[] product = new int[size];
            for (int i = 0; i < size; i++)
            {
                product[i] = _field.Multiply(_coefficients[i], scalar);
            }

            return new GenericGFPoly(_field, product);
        }

        public GenericGFPoly MultiplyByMonomial(int degree, int coefficient)
        {
            if (degree < 0)
                throw new ArgumentException("Degree must be non-negative.");
            if (coefficient == 0)
                return _field.Zero;

            int size = _coefficients.Length;
            int[] product = new int[size + degree];
            for (int i = 0; i < size; i++)
            {
                product[i] = _field.Multiply(_coefficients[i], coefficient);
            }

            return new GenericGFPoly(_field, product);
        }

        /// <summary>
        /// Divides this polynomial by another, returning [Quotient, Remainder].
        /// </summary>
        public GenericGFPoly[] Divide(GenericGFPoly other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            if (other.IsZero) throw new DivideByZeroException("Cannot divide polynomial by zero.");

            var quotient = _field.Zero;
            var remainder = this;

            int denominatorLeadingTerm = other.GetCoefficient(other.Degree);
            int dltInverse = _field.Inverse(denominatorLeadingTerm);

            while (remainder.Degree >= other.Degree && !remainder.IsZero)
            {
                int degreeDiff = remainder.Degree - other.Degree;
                int scale = _field.Multiply(remainder.GetCoefficient(remainder.Degree), dltInverse);
                var term = _field.BuildMonomial(degreeDiff, scale);
                quotient = quotient.AddOrSubtract(term);
                remainder = remainder.AddOrSubtract(other.MultiplyByMonomial(degreeDiff, scale));
            }

            return new GenericGFPoly[] { quotient, remainder };
        }
    }
}
