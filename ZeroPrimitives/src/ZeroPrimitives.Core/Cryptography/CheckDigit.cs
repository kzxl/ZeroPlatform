using System;

namespace ZeroPrimitives.Cryptography
{
    /// <summary>
    /// Zero-allocation international check-digit validators and generators for barcodes, financial accounts, and internal document codes:
    /// Luhn (Mod-10), GS1 GTIN/EAN/SSCC (Mod-10), ISO 7064 Mod 97-10 (IBAN), and Damm quasigroup.
    /// </summary>
    public static class CheckDigit
    {
        // Canonical totally anti-symmetric quasigroup of order 10 (H. Michael Damm)
        private static readonly byte[] DammTable =
        {
            0, 3, 1, 7, 5, 9, 8, 6, 4, 2,
            7, 0, 9, 2, 1, 5, 4, 8, 6, 3,
            4, 2, 0, 6, 8, 7, 1, 3, 5, 9,
            1, 7, 5, 0, 9, 8, 3, 4, 2, 6,
            6, 1, 2, 3, 0, 4, 5, 9, 7, 8,
            3, 6, 7, 4, 2, 0, 9, 5, 8, 1,
            5, 8, 6, 9, 7, 2, 0, 1, 3, 4,
            8, 9, 4, 5, 3, 6, 2, 0, 1, 7,
            9, 4, 3, 8, 6, 1, 7, 2, 0, 5,
            2, 5, 8, 1, 4, 3, 6, 7, 9, 0
        };

        #region Luhn (Mod-10)

        public static bool ValidateLuhn(ReadOnlySpan<char> digits)
        {
            if (digits.Length < 2) return false;

            int sum = 0;
            bool alt = false;
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                uint d = (uint)(digits[i] - '0');
                if (d > 9U) return false;
                if (alt)
                {
                    d <<= 1;
                    if (d > 9U) d -= 9U;
                }
                sum += (int)d;
                alt = !alt;
            }
            return (sum % 10) == 0;
        }

        public static bool ValidateLuhn(ReadOnlySpan<byte> asciiDigits)
        {
            if (asciiDigits.Length < 2) return false;

            int sum = 0;
            bool alt = false;
            for (int i = asciiDigits.Length - 1; i >= 0; i--)
            {
                uint d = (uint)(asciiDigits[i] - (byte)'0');
                if (d > 9U) return false;
                if (alt)
                {
                    d <<= 1;
                    if (d > 9U) d -= 9U;
                }
                sum += (int)d;
                alt = !alt;
            }
            return (sum % 10) == 0;
        }

        public static char ComputeLuhn(ReadOnlySpan<char> payloadDigits)
        {
            if (payloadDigits.IsEmpty)
                throw new ArgumentException("Payload must contain at least one digit.", nameof(payloadDigits));

            int sum = 0;
            bool alt = true;
            for (int i = payloadDigits.Length - 1; i >= 0; i--)
            {
                uint d = (uint)(payloadDigits[i] - '0');
                if (d > 9U)
                    throw new FormatException("Input contains a non-digit character.");
                if (alt)
                {
                    d <<= 1;
                    if (d > 9U) d -= 9U;
                }
                sum += (int)d;
                alt = !alt;
            }

            int check = (10 - (sum % 10)) % 10;
            return (char)('0' + check);
        }

        #endregion

        #region GS1 GTIN / EAN / SSCC

        public static bool ValidateGtin(ReadOnlySpan<char> gtin)
        {
            int len = gtin.Length;
            if (len != 8 && len != 12 && len != 13 && len != 14 && len != 18)
                return false;

            for (int i = 0; i < len; i++)
            {
                if ((uint)(gtin[i] - '0') > 9U) return false;
            }

            char expected = ComputeGtinCheckDigit(gtin.Slice(0, len - 1));
            return gtin[len - 1] == expected;
        }

        public static char ComputeGtinCheckDigit(ReadOnlySpan<char> payloadDigits)
        {
            int len = payloadDigits.Length;
            if (len != 7 && len != 11 && len != 12 && len != 13 && len != 17)
                throw new ArgumentException("GTIN payload length must be 7, 11, 12, 13, or 17 digits.", nameof(payloadDigits));

            int sum = 0;
            bool weight3 = true;
            for (int i = len - 1; i >= 0; i--)
            {
                uint d = (uint)(payloadDigits[i] - '0');
                if (d > 9U)
                    throw new FormatException("Input contains a non-digit character.");
                sum += (int)(weight3 ? d * 3U : d);
                weight3 = !weight3;
            }

            int check = (10 - (sum % 10)) % 10;
            return (char)('0' + check);
        }

        #endregion

        #region ISO 7064 Mod 97-10 & IBAN

        public static bool ValidateIban(ReadOnlySpan<char> iban)
        {
            Span<char> compact = stackalloc char[34];
            int len = 0;
            for (int i = 0; i < iban.Length; i++)
            {
                char c = iban[i];
                if (c == ' ' || c == '\t' || c == '-') continue;
                if (len >= 34) return false;
                compact[len++] = c;
            }

            if (len < 15) return false;
            if (!IsAsciiLetter(compact[0]) || !IsAsciiLetter(compact[1]) ||
                (uint)(compact[2] - '0') > 9U || (uint)(compact[3] - '0') > 9U)
            {
                return false;
            }

            int mod = 0;
            for (int i = 4; i < len; i++)
            {
                if (!FeedMod97Char(compact[i], ref mod)) return false;
            }
            for (int i = 0; i < 4; i++)
            {
                if (!FeedMod97Char(compact[i], ref mod)) return false;
            }

            return mod == 1;
        }

        public static bool ValidateMod97(ReadOnlySpan<char> input)
        {
            int count = 0;
            int mod = 0;
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == ' ' || c == '\t' || c == '-') continue;
                if (!FeedMod97Char(c, ref mod)) return false;
                count++;
            }
            return count >= 2 && mod == 1;
        }

        private static bool FeedMod97Char(char c, ref int mod)
        {
            if ((uint)(c - '0') <= 9U)
            {
                mod = ((mod * 10) + (c - '0')) % 97;
                return true;
            }
            if (c >= 'A' && c <= 'Z')
            {
                mod = ((mod * 100) + (c - 'A' + 10)) % 97;
                return true;
            }
            if (c >= 'a' && c <= 'z')
            {
                mod = ((mod * 100) + (c - 'a' + 10)) % 97;
                return true;
            }
            return false;
        }

        private static bool IsAsciiLetter(char c)
            => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

        #endregion

        #region Damm Quasigroup

        public static bool ValidateDamm(ReadOnlySpan<char> digits)
        {
            if (digits.Length < 2) return false;

            int interim = 0;
            for (int i = 0; i < digits.Length; i++)
            {
                uint d = (uint)(digits[i] - '0');
                if (d > 9U) return false;
                interim = DammTable[(interim * 10) + (int)d];
            }
            return interim == 0;
        }

        public static char ComputeDamm(ReadOnlySpan<char> payloadDigits)
        {
            if (payloadDigits.IsEmpty)
                throw new ArgumentException("Payload must contain at least one digit.", nameof(payloadDigits));

            int interim = 0;
            for (int i = 0; i < payloadDigits.Length; i++)
            {
                uint d = (uint)(payloadDigits[i] - '0');
                if (d > 9U)
                    throw new FormatException("Input contains a non-digit character.");
                interim = DammTable[(interim * 10) + (int)d];
            }
            return (char)('0' + interim);
        }

        #endregion
    }
}
