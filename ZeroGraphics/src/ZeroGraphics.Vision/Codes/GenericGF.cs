using System;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// Galois Field GF(2^m) arithmetic engine with precomputed exponential and logarithmic tables.
    /// Provides zero-allocation finite field operations for Reed-Solomon error correction.
    /// </summary>
    public sealed class GenericGF
    {
        public static readonly GenericGF DataMatrix256 = new GenericGF(0x12D, 256, 1); // x^8 + x^4 + x^3 + x^2 + 1
        public static readonly GenericGF QrCode256 = new GenericGF(0x11D, 256, 0);     // x^8 + x^4 + x^3 + x + 1

        private readonly int[] _expTable;
        private readonly int[] _logTable;
        private readonly GenericGFPoly _zero;
        private readonly GenericGFPoly _one;

        public int Size { get; }
        public int GeneratorBase { get; }
        public GenericGFPoly Zero => _zero;
        public GenericGFPoly One => _one;

        public GenericGF(int primitive, int size, int generatorBase)
        {
            Size = size;
            GeneratorBase = generatorBase;
            _expTable = new int[size * 2];
            _logTable = new int[size];

            int x = 1;
            for (int i = 0; i < size - 1; i++)
            {
                _expTable[i] = x;
                _logTable[x] = i;
                x <<= 1;
                if (x >= size)
                {
                    x ^= primitive;
                    x &= (size - 1);
                }
            }

            for (int i = size - 1; i < size * 2; i++)
            {
                _expTable[i] = _expTable[i - (size - 1)];
            }

            _zero = new GenericGFPoly(this, new int[] { 0 });
            _one = new GenericGFPoly(this, new int[] { 1 });
        }

        public GenericGFPoly BuildMonomial(int degree, int coefficient)
        {
            if (degree < 0)
                throw new ArgumentException("Degree must be non-negative.");
            if (coefficient == 0)
                return _zero;

            int[] coefficients = new int[degree + 1];
            coefficients[0] = coefficient;
            return new GenericGFPoly(this, coefficients);
        }

        public static int AddOrSubtract(int a, int b) => a ^ b;

        public int Exp(int a) => _expTable[a];

        public int Log(int a)
        {
            if (a == 0)
                throw new ArithmeticException("Log of zero is undefined in Galois Field.");
            return _logTable[a];
        }

        public int Inverse(int a)
        {
            if (a == 0)
                throw new ArithmeticException("Inverse of zero is undefined in Galois Field.");
            return _expTable[(Size - 1) - _logTable[a]];
        }

        public int Multiply(int a, int b)
        {
            if (a == 0 || b == 0)
                return 0;
            return _expTable[_logTable[a] + _logTable[b]];
        }
    }
}
