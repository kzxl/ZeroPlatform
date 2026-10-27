using System;

namespace ZeroSecurity.Anomaly;

/// <summary>
/// High-performance Shannon Entropy analysis for detecting packed, compressed, or ransomware-encrypted binary payloads.
/// </summary>
public static class ShannonEntropy
{
    private const double Ln2Inv = 1.4426950408889634; // 1 / ln(2)

    /// <summary>
    /// Calculates the Shannon Entropy of the input data span.
    /// Returns a value between 0.0 (all identical bytes) and 8.0 (maximal randomness/entropy).
    /// </summary>
    public static double Calculate(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return 0.0;

        Span<int> frequencies = stackalloc int[256];
        frequencies.Clear();

        for (int i = 0; i < data.Length; i++)
        {
            frequencies[data[i]]++;
        }

        double len = data.Length;
        double entropy = 0.0;

        for (int i = 0; i < 256; i++)
        {
            int count = frequencies[i];
            if (count > 0)
            {
                double p = count / len;
                entropy -= p * (Math.Log(p) * Ln2Inv);
            }
        }

        return entropy;
    }

    /// <summary>
    /// Checks if a buffer is likely encrypted (e.g. ransomware payload) or packed by an executable packer.
    /// </summary>
    /// <param name="data">The byte buffer to analyze.</param>
    /// <param name="threshold">Entropy threshold (standard industrial threshold is 7.2).</param>
    public static bool IsLikelyEncryptedOrPacked(ReadOnlySpan<byte> data, double threshold = 7.2)
    {
        if (data.Length < 64) return false; // Insufficient statistical sample
        return Calculate(data) >= threshold;
    }
}
