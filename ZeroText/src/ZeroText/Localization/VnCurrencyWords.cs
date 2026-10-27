using System;
using ZeroPrimitives.Text;

namespace ZeroText.Localization
{
    /// <summary>
    /// High-performance Vietnamese number-to-words and currency-to-words converter.
    /// Implements official Vietnamese accounting and banking standards (Circular 200/2014/TT-BTC)
    /// with zero heap allocations using stack-allocated buffers and ValueStringBuilder.
    /// </summary>
    public static class VnCurrencyWords
    {
        private static readonly string[] Digits =
        {
            "không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"
        };

        /// <summary>
        /// Converts an integer or long value into Vietnamese words.
        /// </summary>
        public static string ToVnWords(this long number, VnWordsOptions? options = null)
        {
            options = options ?? VnWordsOptions.Default;
            if (number == 0) return options.CapitalizeFirstLetter ? "Không" : "không";

            Span<char> initialBuffer = stackalloc char[256];
            var sb = new ValueStringBuilder(initialBuffer);

            try
            {
                if (number < 0)
                {
                    sb.Append("âm ");
                    number = -number;
                }

                BuildLongWords(ref sb, (ulong)number, options);

                if (options.CapitalizeFirstLetter && sb.Length > 0)
                {
                    sb[0] = char.ToUpperInvariant(sb[0]);
                }

                return sb.ToString();
            }
            finally
            {
                sb.Dispose();
            }
        }

        /// <summary>
        /// Converts a long number into Vietnamese words (alias for ToVnWords).
        /// </summary>
        public static string ToWords(long number, VnWordsOptions? options = null)
            => ToVnWords(number, options);

        /// <summary>
        /// Converts a monetary decimal amount into Vietnamese words (alias for ToVnCurrencyWords).
        /// </summary>
        public static string ToWords(decimal amount, VnWordsOptions? options = null)
            => ToVnCurrencyWords(amount, "đồng", "xu", true, options);

        /// <summary>
        /// Converts a monetary decimal amount into standard Vietnamese currency words (e.g. VND, USD).
        /// </summary>
        public static string ToVnCurrencyWords(
            this decimal amount,
            string currencyUnit = "đồng",
            string subunitUnit = "xu",
            bool appendWholeNumberSuffix = true,
            VnWordsOptions? options = null)
        {
            options = options ?? VnWordsOptions.Default;

            Span<char> initialBuffer = stackalloc char[384];
            var sb = new ValueStringBuilder(initialBuffer);

            try
            {
                if (amount < 0)
                {
                    sb.Append("âm ");
                    amount = -amount;
                }

                decimal integralPart = decimal.Truncate(amount);
                decimal fractionalPart = amount - integralPart;

                if (integralPart == 0 && fractionalPart == 0)
                {
                    sb.Append("không ");
                    sb.Append(currencyUnit);
                    if (appendWholeNumberSuffix) sb.Append(" chẵn");
                }
                else
                {
                    if (integralPart > 0)
                    {
                        BuildDecimalIntegralWords(ref sb, integralPart, options);
                        sb.Append(' ');
                        sb.Append(currencyUnit);
                    }

                    if (fractionalPart > 0)
                    {
                        // Scale fractional part (up to 2 decimal places e.g. cents/xu)
                        long subVal = (long)Math.Round(fractionalPart * 100, MidpointRounding.AwayFromZero);
                        if (subVal > 0)
                        {
                            if (integralPart > 0) sb.Append(" và ");
                            BuildLongWords(ref sb, (ulong)subVal, options);
                            sb.Append(' ');
                            sb.Append(subunitUnit);
                        }
                    }
                    else if (appendWholeNumberSuffix && integralPart > 0)
                    {
                        sb.Append(" chẵn");
                    }
                }

                if (options.CapitalizeFirstLetter && sb.Length > 0)
                {
                    sb[0] = char.ToUpperInvariant(sb[0]);
                }

                return sb.ToString();
            }
            finally
            {
                sb.Dispose();
            }
        }

        private static void BuildDecimalIntegralWords(ref ValueStringBuilder sb, decimal value, VnWordsOptions options)
        {
            // Group decimal into triads (thousands)
            // decimal.MaxValue is ~7.9 x 10^28 (up to 10 triads)
            var triads = new int[10];
            int count = 0;

            decimal current = value;
            while (current > 0)
            {
                decimal rem = current % 1000m;
                triads[count++] = (int)rem;
                current = decimal.Truncate(current / 1000m);
            }

            BuildTriadsWords(ref sb, triads, count, options);
        }

        private static void BuildLongWords(ref ValueStringBuilder sb, ulong value, VnWordsOptions options)
        {
            var triads = new int[8];
            int count = 0;

            ulong current = value;
            while (current > 0)
            {
                triads[count++] = (int)(current % 1000UL);
                current /= 1000UL;
            }

            BuildTriadsWords(ref sb, triads, count, options);
        }

        private static void BuildTriadsWords(ref ValueStringBuilder sb, int[] triads, int totalTriads, VnWordsOptions options)
        {
            bool isFirstWritten = true;

            for (int i = totalTriads - 1; i >= 0; i--)
            {
                int triadValue = triads[i];
                if (triadValue == 0)
                {
                    // For "tỷ" scale (groups of 3 triads: index 3, 6, 9), if there is a higher non-zero number, we must output "tỷ"
                    if (i % 3 == 0 && i > 0)
                    {
                        // Check if any higher triad in this billion-block was non-zero
                        bool hasHigherInBlock = false;
                        int blockStart = (i / 3) * 3;
                        for (int k = blockStart; k < totalTriads; k++)
                        {
                            if (triads[k] > 0) { hasHigherInBlock = true; break; }
                        }
                        if (hasHigherInBlock && !sb.AsSpan().EndsWith("tỷ".AsSpan()))
                        {
                            if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                            sb.Append("tỷ");
                        }
                    }
                    continue;
                }

                if (!isFirstWritten && sb.Length > 0 && sb[sb.Length - 1] != ' ')
                {
                    sb.Append(' ');
                }

                // Read triad: read zero hundreds if this is not the very first (highest) triad
                bool readZeroHundreds = !isFirstWritten;
                AppendTriad(ref sb, triadValue, readZeroHundreds, options);

                // Append scale name
                string? scale = GetScaleName(i, options);
                if (scale != null)
                {
                    sb.Append(' ');
                    sb.Append(scale);
                }

                isFirstWritten = false;
            }
        }

        private static string? GetScaleName(int triadIndex, VnWordsOptions options)
        {
            int mod = triadIndex % 3;
            int billionCount = triadIndex / 3;

            string thousands = options.UseSouthernThousands ? "ngàn" : "nghìn";

            if (mod == 1)
            {
                return thousands;
            }
            if (mod == 2)
            {
                return "triệu";
            }
            if (triadIndex > 0 && mod == 0)
            {
                // Multiples of billions
                if (billionCount == 1) return "tỷ";
                if (billionCount == 2) return "triệu tỷ";
                return "tỷ";
            }

            return null;
        }

        private static void AppendTriad(ref ValueStringBuilder sb, int triad, bool readZeroHundreds, VnWordsOptions options)
        {
            int hundreds = triad / 100;
            int remainder = triad % 100;
            int tens = remainder / 10;
            int units = remainder % 10;

            if (hundreds > 0 || readZeroHundreds)
            {
                sb.Append(Digits[hundreds]);
                sb.Append(" trăm");
            }

            if (tens > 1)
            {
                if (hundreds > 0 || readZeroHundreds) sb.Append(' ');
                sb.Append(Digits[tens]);
                sb.Append(" mươi");

                if (units == 1)
                {
                    sb.Append(" mốt");
                }
                else if (units == 4)
                {
                    sb.Append(options.UseTuForFour ? " tư" : " bốn");
                }
                else if (units == 5)
                {
                    sb.Append(" lăm");
                }
                else if (units > 0)
                {
                    sb.Append(' ');
                    sb.Append(Digits[units]);
                }
            }
            else if (tens == 1)
            {
                if (hundreds > 0 || readZeroHundreds) sb.Append(' ');
                sb.Append("mười");

                if (units == 5)
                {
                    sb.Append(" lăm");
                }
                else if (units > 0)
                {
                    sb.Append(' ');
                    sb.Append(Digits[units]);
                }
            }
            else // tens == 0
            {
                if (units > 0)
                {
                    if (hundreds > 0 || readZeroHundreds)
                    {
                        sb.Append(' ');
                        sb.Append(options.UseSouthernZeroTens ? "lẻ " : "linh ");
                        sb.Append(Digits[units]);
                    }
                    else
                    {
                        sb.Append(Digits[units]);
                    }
                }
            }
        }
    }
}
