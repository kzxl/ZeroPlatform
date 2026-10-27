using System;

namespace ZeroSignal.Core.Spectral
{
    /// <summary>
    /// Radix-2 Decimation-in-Time Cooley-Tukey Fast Fourier Transform (FFT) and Inverse FFT (IFFT).
    /// Pure C# with zero external dependencies.
    /// </summary>
    public static class FastFourierTransform
    {
        public static int NextPowerOfTwo(int n)
        {
            if (n <= 1) return 1;
            int p = 1;
            while (p < n) p <<= 1;
            return p;
        }

        public static void FFT(Complex64[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            int n = buffer.Length;
            if ((n & (n - 1)) != 0)
                throw new ArgumentException("Buffer length must be an exact power of 2 for Radix-2 FFT.", nameof(buffer));

            // Bit-reversal permutation
            int j = 0;
            for (int i = 0; i < n - 1; i++)
            {
                if (i < j)
                {
                    var temp = buffer[i];
                    buffer[i] = buffer[j];
                    buffer[j] = temp;
                }
                int k = n >> 1;
                while (k <= j)
                {
                    j -= k;
                    k >>= 1;
                }
                j += k;
            }

            // Cooley-Tukey butterfly stages
            for (int len = 2; len <= n; len <<= 1)
            {
                double angle = -2.0 * Math.PI / len;
                var wlen = new Complex64(Math.Cos(angle), Math.Sin(angle));

                for (int i = 0; i < n; i += len)
                {
                    var w = Complex64.One;
                    int half = len >> 1;
                    for (int k = 0; k < half; k++)
                    {
                        var u = buffer[i + k];
                        var v = buffer[i + k + half] * w;
                        buffer[i + k] = u + v;
                        buffer[i + k + half] = u - v;
                        w = w * wlen;
                    }
                }
            }
        }

        public static void IFFT(Complex64[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            int n = buffer.Length;
            if ((n & (n - 1)) != 0)
                throw new ArgumentException("Buffer length must be an exact power of 2 for Radix-2 IFFT.", nameof(buffer));

            // Conjugate input
            for (int i = 0; i < n; i++)
            {
                buffer[i] = new Complex64(buffer[i].Real, -buffer[i].Imaginary);
            }

            // Forward FFT
            FFT(buffer);

            // Conjugate and scale by 1/N
            double invN = 1.0 / n;
            for (int i = 0; i < n; i++)
            {
                buffer[i] = new Complex64(buffer[i].Real * invN, -buffer[i].Imaginary * invN);
            }
        }
    }
}
