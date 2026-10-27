using System;

namespace ZeroAudio.Analysis
{
    /// <summary>
    /// Pure C# in-place Cooley-Tukey Radix-2 Decimation-In-Time Fast Fourier Transform (FFT).
    /// Minimal-allocation, high-performance spectral analysis for audio and acoustic DSP.
    /// </summary>
    public static class FastFourierTransform
    {
        /// <summary>
        /// Computes in-place Radix-2 Forward FFT.
        /// </summary>
        public static void Forward(Span<float> real, Span<float> imag)
        {
            Transform(real, imag, forward: true);
        }

        /// <summary>
        /// Computes in-place Radix-2 Inverse FFT (IFFT) normalized by 1/N.
        /// </summary>
        public static void Inverse(Span<float> real, Span<float> imag)
        {
            Transform(real, imag, forward: false);

            int n = real.Length;
            float scale = 1.0f / n;
            for (int i = 0; i < n; i++)
            {
                real[i] *= scale;
                imag[i] *= scale;
            }
        }

        private static void Transform(Span<float> real, Span<float> imag, bool forward)
        {
            int n = real.Length;
            if (n != imag.Length)
                throw new ArgumentException("Real and imaginary spans must have equal length.");
            if (n == 0 || (n & (n - 1)) != 0)
                throw new ArgumentException("FFT length must be a non-zero power of 2.", nameof(real));

            // 1. Bit-reversal permutation
            int j = 0;
            for (int i = 0; i < n - 1; i++)
            {
                if (i < j)
                {
                    float tempR = real[i];
                    real[i] = real[j];
                    real[j] = tempR;

                    float tempI = imag[i];
                    imag[i] = imag[j];
                    imag[j] = tempI;
                }

                int k = n >> 1;
                while (k <= j)
                {
                    j -= k;
                    k >>= 1;
                }
                j += k;
            }

            // 2. Cooley-Tukey butterfly computation
            double sign = forward ? -1.0 : 1.0;
            for (int len = 2; len <= n; len <<= 1)
            {
                int halfLen = len >> 1;
                double angle = sign * 2.0 * Math.PI / len;
                float wStepR = (float)Math.Cos(angle);
                float wStepI = (float)Math.Sin(angle);

                for (int i = 0; i < n; i += len)
                {
                    float wR = 1.0f;
                    float wI = 0.0f;

                    for (int k = 0; k < halfLen; k++)
                    {
                        int uIdx = i + k;
                        int vIdx = i + k + halfLen;

                        float uR = real[uIdx];
                        float uI = imag[uIdx];

                        float vR = real[vIdx];
                        float vI = imag[vIdx];

                        float tR = vR * wR - vI * wI;
                        float tI = vR * wI + vI * wR;

                        real[uIdx] = uR + tR;
                        imag[uIdx] = uI + tI;
                        real[vIdx] = uR - tR;
                        imag[vIdx] = uI - tI;

                        float nextWR = wR * wStepR - wI * wStepI;
                        float nextWI = wR * wStepI + wI * wStepR;
                        wR = nextWR;
                        wI = nextWI;
                    }
                }
            }
        }
    }
}
