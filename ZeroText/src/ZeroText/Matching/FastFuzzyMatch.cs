using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace ZeroText.Matching
{
    /// <summary>
    /// Pure C# ultra-fast, zero-allocation fuzzy matching algorithms.
    /// Provides Levenshtein distance/similarity and Jaro-Winkler similarity with stack-allocated
    /// rolling buffers and zero heap allocations for typical strings (&lt;= 256 characters).
    /// </summary>
    public static class FastFuzzyMatch
    {
        private const int MaxStackallocThreshold = 256;

        /// <summary>
        /// Computes the Levenshtein edit distance between two character spans.
        /// Trims common prefixes/suffixes, executes Myers' (1999) 64-bit bit-parallel algorithm in O(N) time for M &lt;= 64,
        /// and falls back to pre-normalized single-row DP for M &gt; 64.
        /// Zero managed allocation for strings up to 256 characters.
        /// </summary>
        /// <param name="s1">First character sequence.</param>
        /// <param name="s2">Second character sequence.</param>
        /// <param name="ignoreCase">True to ignore casing; false for exact ordinal match.</param>
        /// <returns>Minimum number of single-character edits (insertions, deletions, or substitutions).</returns>
        public static int LevenshteinDistance(ReadOnlySpan<char> s1, ReadOnlySpan<char> s2, bool ignoreCase = false)
        {
            if (s1.IsEmpty) return s2.Length;
            if (s2.IsEmpty) return s1.Length;

            // 1. Trim common prefix
            int prefixLen = 0;
            int minInitialLen = Math.Min(s1.Length, s2.Length);
            if (!ignoreCase)
            {
                while (prefixLen < minInitialLen && s1[prefixLen] == s2[prefixLen])
                    prefixLen++;
            }
            else
            {
                while (prefixLen < minInitialLen && ToUpperFast(s1[prefixLen]) == ToUpperFast(s2[prefixLen]))
                    prefixLen++;
            }

            if (prefixLen > 0)
            {
                s1 = s1.Slice(prefixLen);
                s2 = s2.Slice(prefixLen);
            }

            // 2. Trim common suffix
            int suffixLen = 0;
            int minRemLen = Math.Min(s1.Length, s2.Length);
            if (!ignoreCase)
            {
                while (suffixLen < minRemLen && s1[s1.Length - 1 - suffixLen] == s2[s2.Length - 1 - suffixLen])
                    suffixLen++;
            }
            else
            {
                while (suffixLen < minRemLen && ToUpperFast(s1[s1.Length - 1 - suffixLen]) == ToUpperFast(s2[s2.Length - 1 - suffixLen]))
                    suffixLen++;
            }

            if (suffixLen > 0)
            {
                s1 = s1.Slice(0, s1.Length - suffixLen);
                s2 = s2.Slice(0, s2.Length - suffixLen);
            }

            if (s1.IsEmpty) return s2.Length;
            if (s2.IsEmpty) return s1.Length;

            // Ensure s2 is the shorter pattern (length M <= N)
            if (s1.Length < s2.Length)
            {
                var temp = s1;
                s1 = s2;
                s2 = temp;
            }

            int m = s2.Length;
            int n = s1.Length;

            // 3. Myers' (1999) 64-bit Bit-Parallel Levenshtein Algorithm for M <= 64
            if (m <= 64)
            {
                return MyersBitParallelLevenshtein(s1, s2, n, m, ignoreCase);
            }

            // 4. Fallback to pre-normalized single-row DP for M > 64
            int bufferSize = m + 1;
            int[]? rentedRow = null;
            char[]? rentedPat = null;

            Span<int> row = bufferSize <= MaxStackallocThreshold
                ? stackalloc int[bufferSize]
                : (rentedRow = ArrayPool<int>.Shared.Rent(bufferSize)).AsSpan(0, bufferSize);

            Span<char> pat = m <= MaxStackallocThreshold
                ? stackalloc char[m]
                : (rentedPat = ArrayPool<char>.Shared.Rent(m)).AsSpan(0, m);

            try
            {
                for (int j = 0; j < m; j++)
                {
                    row[j] = j;
                    pat[j] = ignoreCase ? ToUpperFast(s2[j]) : s2[j];
                }
                row[m] = m;

                for (int i = 1; i <= n; i++)
                {
                    char c1 = ignoreCase ? ToUpperFast(s1[i - 1]) : s1[i - 1];
                    int prevDiagonal = row[0];
                    row[0] = i;

                    for (int j = 1; j <= m; j++)
                    {
                        int cost = c1 == pat[j - 1] ? 0 : 1;
                        int insertion = row[j] + 1;
                        int deletion = row[j - 1] + 1;
                        int substitution = prevDiagonal + cost;

                        prevDiagonal = row[j];
                        row[j] = Math.Min(Math.Min(insertion, deletion), substitution);
                    }
                }

                return row[m];
            }
            finally
            {
                if (rentedRow != null) ArrayPool<int>.Shared.Return(rentedRow);
                if (rentedPat != null) ArrayPool<char>.Shared.Return(rentedPat);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int MyersBitParallelLevenshtein(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern, int n, int m, bool ignoreCase)
        {
            Span<ulong> peq = stackalloc ulong[256];
            peq.Clear();
            Span<char> pat = stackalloc char[m];
            bool hasHighChar = false;

            for (int j = 0; j < m; j++)
            {
                char pc = ignoreCase ? ToUpperFast(pattern[j]) : pattern[j];
                pat[j] = pc;
                if (pc < 256)
                {
                    peq[pc] |= 1UL << j;
                }
                else
                {
                    hasHighChar = true;
                }
            }

            ulong pv = ~0UL;
            ulong mv = 0UL;
            int score = m;
            ulong lastBitMask = 1UL << (m - 1);

            unchecked
            {
                for (int i = 0; i < n; i++)
                {
                    char tc = ignoreCase ? ToUpperFast(text[i]) : text[i];
                    ulong eq;
                    if (tc < 256)
                    {
                        eq = peq[tc];
                    }
                    else if (hasHighChar)
                    {
                        eq = 0UL;
                        for (int j = 0; j < m; j++)
                        {
                            if (pat[j] == tc) eq |= 1UL << j;
                        }
                    }
                    else
                    {
                        eq = 0UL;
                    }

                    ulong xv = eq | mv;
                    ulong xh = (((eq & pv) + pv) ^ pv) | eq;

                    ulong ph = mv | ~(xh | pv);
                    ulong mh = pv & xh;

                    if ((ph & lastBitMask) != 0)
                        score++;
                    else if ((mh & lastBitMask) != 0)
                        score--;

                    ph = (ph << 1) | 1UL;
                    mh <<= 1;

                    pv = mh | ~(xv | ph);
                    mv = ph & xv;
                }
            }

            return score;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static char ToUpperFast(char c)
        {
            if ((uint)(c - 'a') <= ('z' - 'a'))
                return (char)(c & ~0x20);
            return c < 0x80 ? c : char.ToUpperInvariant(c);
        }

        /// <summary>
        /// Computes the normalized Levenshtein similarity ratio between 0.0 (completely dissimilar) and 1.0 (exact match).
        /// </summary>
        public static double LevenshteinSimilarity(ReadOnlySpan<char> s1, ReadOnlySpan<char> s2, bool ignoreCase = false)
        {
            if (s1.IsEmpty && s2.IsEmpty) return 1.0;
            int maxLen = Math.Max(s1.Length, s2.Length);
            if (maxLen == 0) return 1.0;

            int dist = LevenshteinDistance(s1, s2, ignoreCase);
            return 1.0 - ((double)dist / maxLen);
        }

        /// <summary>
        /// Computes the Jaro string similarity between 0.0 and 1.0.
        /// </summary>
        public static double JaroSimilarity(ReadOnlySpan<char> s1, ReadOnlySpan<char> s2, bool ignoreCase = false)
        {
            int len1 = s1.Length;
            int len2 = s2.Length;

            if (len1 == 0 && len2 == 0) return 1.0;
            if (len1 == 0 || len2 == 0) return 0.0;

            int matchWindow = Math.Max(Math.Max(len1, len2) / 2 - 1, 0);

            bool[]? rented1 = null;
            bool[]? rented2 = null;

            Span<bool> s1Matched = len1 <= MaxStackallocThreshold
                ? stackalloc bool[len1]
                : (rented1 = ArrayPool<bool>.Shared.Rent(len1)).AsSpan(0, len1);

            Span<bool> s2Matched = len2 <= MaxStackallocThreshold
                ? stackalloc bool[len2]
                : (rented2 = ArrayPool<bool>.Shared.Rent(len2)).AsSpan(0, len2);

            s1Matched.Clear();
            s2Matched.Clear();

            try
            {
                int matches = 0;

                for (int i = 0; i < len1; i++)
                {
                    char c1 = s1[i];
                    int start = Math.Max(0, i - matchWindow);
                    int end = Math.Min(i + matchWindow + 1, len2);

                    for (int j = start; j < end; j++)
                    {
                        if (s2Matched[j]) continue;

                        char c2 = s2[j];
                        bool match = ignoreCase
                            ? char.ToUpperInvariant(c1) == char.ToUpperInvariant(c2)
                            : c1 == c2;

                        if (match)
                        {
                            s1Matched[i] = true;
                            s2Matched[j] = true;
                            matches++;
                            break;
                        }
                    }
                }

                if (matches == 0) return 0.0;

                int k = 0;
                int transpositions = 0;

                for (int i = 0; i < len1; i++)
                {
                    if (!s1Matched[i]) continue;

                    while (!s2Matched[k]) k++;

                    char c1 = s1[i];
                    char c2 = s2[k];
                    bool match = ignoreCase
                        ? char.ToUpperInvariant(c1) == char.ToUpperInvariant(c2)
                        : c1 == c2;

                    if (!match)
                    {
                        transpositions++;
                    }

                    k++;
                }

                double halfTranspositions = transpositions / 2.0;
                return ((matches / (double)len1) + (matches / (double)len2) + ((matches - halfTranspositions) / matches)) / 3.0;
            }
            finally
            {
                if (rented1 != null) ArrayPool<bool>.Shared.Return(rented1);
                if (rented2 != null) ArrayPool<bool>.Shared.Return(rented2);
            }
        }

        /// <summary>
        /// Computes the Jaro-Winkler string similarity metric between 0.0 and 1.0.
        /// Boosts the score for strings with matching common prefixes up to 4 characters.
        /// </summary>
        /// <param name="s1">First string/span.</param>
        /// <param name="s2">Second string/span.</param>
        /// <param name="prefixScale">Standard prefix scaling factor (default 0.1, max 0.25).</param>
        /// <param name="ignoreCase">True to ignore character casing.</param>
        public static double JaroWinklerSimilarity(
            ReadOnlySpan<char> s1,
            ReadOnlySpan<char> s2,
            double prefixScale = 0.1,
            bool ignoreCase = false)
        {
            double jaro = JaroSimilarity(s1, s2, ignoreCase);

            int maxPrefix = Math.Min(4, Math.Min(s1.Length, s2.Length));
            int commonPrefix = 0;

            for (int i = 0; i < maxPrefix; i++)
            {
                char c1 = s1[i];
                char c2 = s2[i];
                bool match = ignoreCase
                    ? char.ToUpperInvariant(c1) == char.ToUpperInvariant(c2)
                    : c1 == c2;

                if (match)
                {
                    commonPrefix++;
                }
                else
                {
                    break;
                }
            }

            return jaro + (commonPrefix * prefixScale * (1.0 - jaro));
        }
    }
}
