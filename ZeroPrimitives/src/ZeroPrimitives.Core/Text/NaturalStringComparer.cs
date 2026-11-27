using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Text
{
    /// <summary>
    /// Zero-allocation natural alphanumeric comparer so ERP codes, bin locations, and lot numbers sort human-naturally
    /// (<c>INV-1 &lt; INV-2 &lt; INV-10</c>) without integer overflow on arbitrary-length numeric segments.
    /// </summary>
    public sealed class NaturalStringComparer : IComparer<string?>, IEqualityComparer<string?>
    {
        public static readonly NaturalStringComparer Ordinal = new NaturalStringComparer(ignoreCase: false);
        public static readonly NaturalStringComparer OrdinalIgnoreCase = new NaturalStringComparer(ignoreCase: true);

        private readonly bool _ignoreCase;

        private NaturalStringComparer(bool ignoreCase)
        {
            _ignoreCase = ignoreCase;
        }

        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            return Compare(x.AsSpan(), y.AsSpan(), _ignoreCase);
        }

        public bool Equals(string? x, string? y) => Compare(x, y) == 0;

        public int GetHashCode(string? obj)
        {
            if (obj == null) return 0;
            return (_ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).GetHashCode(obj);
        }

        /// <summary>
        /// Compares two character spans in natural alphanumeric order with zero heap allocation.
        /// </summary>
        public static int Compare(ReadOnlySpan<char> left, ReadOnlySpan<char> right, bool ignoreCase = false)
        {
            int i = 0;
            int j = 0;
            int zeroPadTieBreak = 0;

            while (i < left.Length && j < right.Length)
            {
                char cl = left[i];
                char cr = right[j];

                if (IsAsciiDigit(cl) && IsAsciiDigit(cr))
                {
                    int iEnd = i + 1;
                    while (iEnd < left.Length && IsAsciiDigit(left[iEnd])) iEnd++;

                    int jEnd = j + 1;
                    while (jEnd < right.Length && IsAsciiDigit(right[jEnd])) jEnd++;

                    int iSig = i;
                    while (iSig < iEnd && left[iSig] == '0') iSig++;

                    int jSig = j;
                    while (jSig < jEnd && right[jSig] == '0') jSig++;

                    int leftSigLen = iEnd - iSig;
                    int rightSigLen = jEnd - jSig;
                    if (leftSigLen != rightSigLen)
                        return leftSigLen < rightSigLen ? -1 : 1;

                    for (int k = 0; k < leftSigLen; k++)
                    {
                        char dl = left[iSig + k];
                        char dr = right[jSig + k];
                        if (dl != dr)
                            return dl < dr ? -1 : 1;
                    }

                    if (zeroPadTieBreak == 0)
                    {
                        int rawLenCmp = (iEnd - i).CompareTo(jEnd - j);
                        if (rawLenCmp != 0) zeroPadTieBreak = rawLenCmp;
                    }

                    i = iEnd;
                    j = jEnd;
                }
                else
                {
                    if (ignoreCase)
                    {
                        cl = char.ToUpperInvariant(cl);
                        cr = char.ToUpperInvariant(cr);
                    }

                    if (cl != cr)
                        return cl < cr ? -1 : 1;

                    i++;
                    j++;
                }
            }

            if (i < left.Length || j < right.Length)
                return (left.Length - i).CompareTo(right.Length - j);

            return zeroPadTieBreak;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAsciiDigit(char c) => (uint)(c - '0') <= 9U;
    }
}
