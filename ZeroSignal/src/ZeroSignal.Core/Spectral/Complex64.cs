using System;

namespace ZeroSignal.Core.Spectral
{
    /// <summary>
    /// Double-precision complex number for high-speed spectral DSP.
    /// Pure C# with zero external dependencies.
    /// </summary>
    public readonly struct Complex64 : IEquatable<Complex64>
    {
        public double Real { get; }
        public double Imaginary { get; }

        public double Magnitude => Math.Sqrt(Real * Real + Imaginary * Imaginary);
        public double MagnitudeSquared => Real * Real + Imaginary * Imaginary;
        public double Phase => Math.Atan2(Imaginary, Real);

        public static readonly Complex64 Zero = new Complex64(0, 0);
        public static readonly Complex64 One = new Complex64(1, 0);

        public Complex64(double real, double imaginary = 0.0)
        {
            Real = real;
            Imaginary = imaginary;
        }

        public static Complex64 FromPolar(double magnitude, double phase) =>
            new Complex64(magnitude * Math.Cos(phase), magnitude * Math.Sin(phase));

        public static Complex64 operator +(Complex64 a, Complex64 b) =>
            new Complex64(a.Real + b.Real, a.Imaginary + b.Imaginary);

        public static Complex64 operator -(Complex64 a, Complex64 b) =>
            new Complex64(a.Real - b.Real, a.Imaginary - b.Imaginary);

        public static Complex64 operator *(Complex64 a, Complex64 b) =>
            new Complex64(a.Real * b.Real - a.Imaginary * b.Imaginary, a.Real * b.Imaginary + a.Imaginary * b.Real);

        public static Complex64 operator *(Complex64 a, double scalar) =>
            new Complex64(a.Real * scalar, a.Imaginary * scalar);

        public static Complex64 operator /(Complex64 a, double scalar) =>
            new Complex64(a.Real / scalar, a.Imaginary / scalar);

        public bool Equals(Complex64 other) =>
            Math.Abs(Real - other.Real) < 1e-12 && Math.Abs(Imaginary - other.Imaginary) < 1e-12;

        public override bool Equals(object? obj) => obj is Complex64 other && Equals(other);
        public override int GetHashCode() => unchecked((Real.GetHashCode() * 397) ^ Imaginary.GetHashCode());
        public override string ToString() => $"{Real:F4} + {Imaginary:F4}i";
    }
}
