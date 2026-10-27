using System;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// Pure C# Reed-Solomon error correction encoder over Galois Field GF(2^m).
    /// Generates parity error correction codewords for DataMatrix ECC200 and QR codes.
    /// </summary>
    public static class ReedSolomonEncoder
    {
        /// <summary>
        /// Computes and appends Reed-Solomon parity codewords in-place to the end of toEncode array.
        /// </summary>
        /// <param name="field">Galois Field instance.</param>
        /// <param name="toEncode">Array containing data codewords followed by zero-initialized parity slots.</param>
        /// <param name="ecCount">Number of parity error correction codewords (2t).</param>
        public static void Encode(GenericGF field, int[] toEncode, int ecCount)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            if (toEncode == null) throw new ArgumentNullException(nameof(toEncode));
            if (ecCount <= 0 || ecCount >= toEncode.Length)
                throw new ArgumentException("Invalid error correction codeword count.", nameof(ecCount));

            int dataLength = toEncode.Length - ecCount;
            int[] infoCoeffs = new int[dataLength];
            Array.Copy(toEncode, 0, infoCoeffs, 0, dataLength);

            var generator = field.One;
            for (int i = 0; i < ecCount; i++)
            {
                var factor = new GenericGFPoly(field, new int[] { 1, field.Exp(i + field.GeneratorBase) });
                generator = generator.Multiply(factor);
            }

            var info = new GenericGFPoly(field, infoCoeffs).MultiplyByMonomial(ecCount, 1);
            var remainder = info.Divide(generator)[1];
            int[] remainderCoeffs = remainder.Coefficients;
            int numZeroCoeffs = ecCount - remainderCoeffs.Length;

            for (int i = 0; i < numZeroCoeffs; i++)
            {
                toEncode[dataLength + i] = 0;
            }
            Array.Copy(remainderCoeffs, 0, toEncode, dataLength + numZeroCoeffs, remainderCoeffs.Length);
        }
    }
}
