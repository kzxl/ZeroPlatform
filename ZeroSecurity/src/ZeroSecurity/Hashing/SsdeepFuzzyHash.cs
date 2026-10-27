using System;
using System.Text;

namespace ZeroSecurity.Hashing;

/// <summary>
/// Context-Triggered Piecewise Hashing (CTPH / SSDEEP).
/// Generates fuzzy hashes for binary and text payloads to detect malware similarity, variants, and morphed samples.
/// </summary>
public static class SsdeepFuzzyHash
{
    private const int MinBlockSize = 3;
    private const int MaxHashLength = 64;
    private const int RollingWindow = 7;
    private static readonly char[] B64Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/".ToCharArray();

    private sealed class RollState
    {
        private readonly byte[] _window = new byte[RollingWindow];
        private uint _h1, _h2, _h3;
        private int _idx;

        public void Roll(byte b)
        {
            _h2 -= _h1;
            _h2 += (uint)RollingWindow * b;
            _h1 += b;
            _h1 -= _window[_idx];
            _window[_idx] = b;
            _idx = (_idx + 1) % RollingWindow;
            _h3 = (_h3 << 5) ^ b;
        }

        public uint Value => _h1 + _h2 + _h3;

        public void Reset()
        {
            Array.Clear(_window, 0, _window.Length);
            _h1 = _h2 = _h3 = 0;
            _idx = 0;
        }
    }

    /// <summary>
    /// Computes the SSDEEP fuzzy hash string of the input data (format: "blocksize:hash1:hash2").
    /// </summary>
    public static string Compute(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return "3::";

        // Determine optimal block size
        uint blockSize = MinBlockSize;
        while (blockSize * MaxHashLength < data.Length)
        {
            blockSize *= 2;
        }

        while (blockSize >= MinBlockSize)
        {
            var hash1 = ComputeString(data, blockSize);
            var hash2 = ComputeString(data, blockSize * 2);

            if (hash1.Length >= MaxHashLength / 2 || blockSize == MinBlockSize)
            {
                return $"{blockSize}:{hash1}:{hash2}";
            }

            blockSize /= 2;
        }

        return $"{MinBlockSize}::";
    }

    private static string ComputeString(ReadOnlySpan<byte> data, uint blockSize)
    {
        var sb = new StringBuilder(MaxHashLength);
        var roll = new RollState();
        uint fnv = 0x811c9dc5;

        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            roll.Roll(b);

            // FNV-1 32-bit
            fnv = (fnv * 16777619) ^ b;

            if (roll.Value % blockSize == blockSize - 1)
            {
                sb.Append(B64Chars[(fnv ^ (fnv >> 6)) & 0x3F]);
                fnv = 0x811c9dc5;
                if (sb.Length >= MaxHashLength) break;
            }
        }

        if (sb.Length < MaxHashLength)
        {
            sb.Append(B64Chars[(fnv ^ (fnv >> 6)) & 0x3F]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Compares two SSDEEP fuzzy hashes and returns a similarity score from 0 (completely different) to 100 (identical).
    /// </summary>
    public static int Compare(string hash1, string hash2)
    {
        if (string.IsNullOrEmpty(hash1) || string.IsNullOrEmpty(hash2)) return 0;
        if (hash1.Equals(hash2, StringComparison.Ordinal)) return 100;

        var parts1 = hash1.Split(':');
        var parts2 = hash2.Split(':');

        if (parts1.Length != 3 || parts2.Length != 3) return 0;
        if (!uint.TryParse(parts1[0], out var b1) || !uint.TryParse(parts2[0], out var b2)) return 0;

        int score = 0;

        if (b1 == b2)
        {
            int s1 = ScoreStrings(parts1[1], parts2[1]);
            int s2 = ScoreStrings(parts1[2], parts2[2]);
            score = Math.Max(s1, s2);
        }
        else if (b1 == b2 * 2)
        {
            score = ScoreStrings(parts1[1], parts2[2]);
        }
        else if (b2 == b1 * 2)
        {
            score = ScoreStrings(parts1[2], parts2[1]);
        }

        return score;
    }

    private static int ScoreStrings(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0;
        int maxLen = Math.Max(s1.Length, s2.Length);
        if (maxLen == 0) return 0;

        int dist = LevenshteinDistance(s1, s2);
        int match = (maxLen - dist) * 100 / maxLen;
        return Math.Max(0, match);
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        if (n == 0) return m;
        if (m == 0) return n;

        int[] d = new int[m + 1];
        for (int j = 0; j <= m; j++) d[j] = j;

        for (int i = 1; i <= n; i++)
        {
            int prev = d[0];
            d[0] = i;

            for (int j = 1; j <= m; j++)
            {
                int temp = d[j];
                int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                d[j] = Math.Min(Math.Min(d[j] + 1, d[j - 1] + 1), prev + cost);
                prev = temp;
            }
        }

        return d[m];
    }
}
