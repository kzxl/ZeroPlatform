using System;
using System.Collections.Generic;

namespace ZeroPrimitives.Validation
{
    /// <summary>
    /// High-performance, zero-allocation master data validators and normalizers for Vietnamese enterprise domains.
    /// Supports Tax Code (MST Modulo 11 check digit), Citizen ID (CCCD 12-digit decode), and Phone number normalization.
    /// </summary>
    public static class VnMasterDataValidators
    {
        #region Tax Identification Number (MST)

        private static readonly int[] MstWeights = { 31, 29, 23, 19, 17, 13, 7, 5, 3 };

        /// <summary>
        /// Validates Vietnamese Tax Code (MST) according to Circular 105/2020/TT-BTC.
        /// Supports 10-digit enterprise MST (with Modulo 11 check digit) and 13-digit branch MST (e.g. 0100109106-001 or 0100109106001).
        /// Zero heap allocation.
        /// </summary>
        public static bool IsValidMst(ReadOnlySpan<char> input)
        {
            // Clean/count digits
            Span<char> digits = stackalloc char[13];
            int digitCount = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c >= '0' && c <= '9')
                {
                    if (digitCount >= 13) return false;
                    digits[digitCount++] = c;
                }
                else if (c == '-' || c == ' ')
                {
                    // Allow hyphen/space separator
                    continue;
                }
                else
                {
                    return false;
                }
            }

            if (digitCount != 10 && digitCount != 13) return false;

            // Validate 10-digit primary MST using Modulo 11
            int sum = 0;
            for (int i = 0; i < 9; i++)
            {
                sum += (digits[i] - '0') * MstWeights[i];
            }

            int remainder = sum % 11;
            int checkDigit = 10 - remainder;

            if (checkDigit == 10)
            {
                // In Modulo 11 standard for MST, checkDigit = 10 means invalid code
                return false;
            }

            if (checkDigit != (digits[9] - '0'))
            {
                return false;
            }

            // If 13-digit branch MST, check branch suffix (digits 10, 11, 12 must not be "000")
            if (digitCount == 13)
            {
                int branchNum = (digits[10] - '0') * 100 + (digits[11] - '0') * 10 + (digits[12] - '0');
                if (branchNum == 0) return false;
            }

            return true;
        }

        /// <summary>
        /// Normalizes MST into standard canonical format (e.g. "0100109106" or "0100109106-001").
        /// </summary>
        public static bool TryNormalizeMst(ReadOnlySpan<char> input, Span<char> output, out int charsWritten)
        {
            charsWritten = 0;
            if (!IsValidMst(input)) return false;

            Span<char> digits = stackalloc char[13];
            int digitCount = 0;
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c >= '0' && c <= '9') digits[digitCount++] = c;
            }

            if (digitCount == 10)
            {
                if (output.Length < 10) return false;
                digits.Slice(0, 10).CopyTo(output);
                charsWritten = 10;
                return true;
            }
            else if (digitCount == 13)
            {
                if (output.Length < 14) return false;
                digits.Slice(0, 10).CopyTo(output);
                output[10] = '-';
                digits.Slice(10, 3).CopyTo(output.Slice(11));
                charsWritten = 14;
                return true;
            }

            return false;
        }

        #endregion

        #region Citizen ID (CCCD)

        private static readonly Dictionary<int, string> Provinces = new Dictionary<int, string>
        {
            { 1, "Hà Nội" }, { 2, "Hà Giang" }, { 4, "Cao Bằng" }, { 6, "Bắc Kạn" },
            { 8, "Tuyên Quang" }, { 10, "Lào Cai" }, { 11, "Điện Biên" }, { 12, "Lai Châu" },
            { 14, "Sơn La" }, { 15, "Yên Bái" }, { 17, "Hòa Bình" }, { 19, "Thái Nguyên" },
            { 20, "Lạng Sơn" }, { 22, "Quảng Ninh" }, { 24, "Bắc Giang" }, { 25, "Phú Thọ" },
            { 26, "Vĩnh Phúc" }, { 27, "Bắc Ninh" }, { 30, "Hải Dương" }, { 31, "Hải Phòng" },
            { 33, "Hưng Yên" }, { 34, "Thái Bình" }, { 35, "Hà Nam" }, { 36, "Nam Định" },
            { 37, "Ninh Bình" }, { 38, "Thanh Hóa" }, { 40, "Nghệ An" }, { 42, "Hà Tĩnh" },
            { 44, "Quảng Bình" }, { 45, "Quảng Trị" }, { 46, "Thừa Thiên Huế" }, { 48, "Đà Nẵng" },
            { 49, "Quảng Nam" }, { 51, "Quảng Ngãi" }, { 52, "Bình Định" }, { 54, "Phú Yên" },
            { 56, "Khánh Hòa" }, { 58, "Ninh Thuận" }, { 60, "Bình Thuận" }, { 62, "Kon Tum" },
            { 64, "Gia Lai" }, { 66, "Đắk Lắk" }, { 67, "Đắk Nông" }, { 68, "Lâm Đồng" },
            { 70, "Bình Phước" }, { 72, "Tây Ninh" }, { 74, "Bình Dương" }, { 75, "Đồng Nai" },
            { 77, "Bà Rịa - Vũng Tàu" }, { 79, "TP. Hồ Chí Minh" }, { 80, "Long An" }, { 82, "Tiền Giang" },
            { 83, "Bến Tre" }, { 84, "Trà Vinh" }, { 86, "Vĩnh Long" }, { 87, "Đồng Tháp" },
            { 89, "An Giang" }, { 91, "Kiên Giang" }, { 92, "Cần Thơ" }, { 93, "Hậu Giang" },
            { 94, "Sóc Trăng" }, { 95, "Bạc Liêu" }, { 96, "Cà Mau" }
        };

        /// <summary>
        /// Validates a 12-digit Vietnamese Citizen Identity Card (CCCD) and decodes metadata.
        /// </summary>
        /// <param name="input">12-digit CCCD string or span.</param>
        /// <param name="birthYear">Decoded full birth year (e.g. 1995, 2003).</param>
        /// <param name="isMale">True for male, false for female.</param>
        /// <param name="provinceName">Name of the province/municipality of registration.</param>
        /// <returns>True if the CCCD is valid; otherwise false.</returns>
        public static bool IsValidCccd(
            ReadOnlySpan<char> input,
            out int birthYear,
            out bool isMale,
            out string? provinceName)
        {
            birthYear = 0;
            isMale = false;
            provinceName = null;

            Span<char> digits = stackalloc char[12];
            int count = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c >= '0' && c <= '9')
                {
                    if (count >= 12) return false;
                    digits[count++] = c;
                }
                else if (c != ' ' && c != '.')
                {
                    return false;
                }
            }

            if (count != 12) return false;

            // Province code: 3 digits (001 to 096)
            int provCode = (digits[0] - '0') * 100 + (digits[1] - '0') * 10 + (digits[2] - '0');
            if (!Provinces.TryGetValue(provCode, out provinceName))
            {
                return false;
            }

            // Century & Gender: digit 3
            int genderDigit = digits[3] - '0';
            int centuryBase;

            switch (genderDigit)
            {
                case 0: isMale = true; centuryBase = 1900; break;
                case 1: isMale = false; centuryBase = 1900; break;
                case 2: isMale = true; centuryBase = 2000; break;
                case 3: isMale = false; centuryBase = 2000; break;
                case 4: isMale = true; centuryBase = 2100; break;
                case 5: isMale = false; centuryBase = 2100; break;
                case 6: isMale = true; centuryBase = 2200; break;
                case 7: isMale = false; centuryBase = 2200; break;
                case 8: isMale = true; centuryBase = 2300; break;
                case 9: isMale = false; centuryBase = 2300; break;
                default: return false;
            }

            // Birth year: digits 4 & 5
            int yearShort = (digits[4] - '0') * 10 + (digits[5] - '0');
            birthYear = centuryBase + yearShort;

            return true;
        }

        #endregion

        #region Vietnamese Phone Normalizer

        /// <summary>
        /// Validates and normalizes a Vietnamese mobile phone number (03x, 05x, 07x, 08x, 09x).
        /// Strips whitespace, dots, dashes, and handles +84 / 84 international prefix.
        /// Zero heap allocation.
        /// </summary>
        public static bool TryNormalizePhone(
            ReadOnlySpan<char> input,
            Span<char> output,
            out int charsWritten,
            bool international = false)
        {
            charsWritten = 0;

            // Strip separators into clean digits
            Span<char> digits = stackalloc char[15];
            int count = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c >= '0' && c <= '9')
                {
                    if (count >= 15) return false;
                    digits[count++] = c;
                }
                else if (c == '+' && count == 0)
                {
                    // Allow leading +
                    continue;
                }
                else if (c != ' ' && c != '.' && c != '-' && c != '(' && c != ')')
                {
                    return false;
                }
            }

            int startIdx;

            // Handle international 84 prefix (e.g. 84901234567 -> 11 digits)
            if (count == 11 && digits[0] == '8' && digits[1] == '4')
            {
                digits[1] = '0';
                startIdx = 1;
            }
            else if (count == 12 && digits[0] == '8' && digits[1] == '4' && digits[2] == '0')
            {
                startIdx = 2;
            }
            else if (count == 10 && digits[0] == '0')
            {
                startIdx = 0;
            }
            else
            {
                return false;
            }

            // Validate valid Vietnamese mobile prefix: 03, 05, 07, 08, 09
            char p0 = digits[startIdx];
            char p1 = digits[startIdx + 1];
            if (p0 != '0' || (p1 != '3' && p1 != '5' && p1 != '7' && p1 != '8' && p1 != '9'))
            {
                return false;
            }

            if (international)
            {
                if (output.Length < 12) return false;
                output[0] = '+';
                output[1] = '8';
                output[2] = '4';
                digits.Slice(startIdx + 1, 9).CopyTo(output.Slice(3));
                charsWritten = 12;
            }
            else
            {
                if (output.Length < 10) return false;
                digits.Slice(startIdx, 10).CopyTo(output);
                charsWritten = 10;
            }

            return true;
        }

        /// <summary>
        /// Returns true if the given input is a valid Vietnamese mobile phone number.
        /// </summary>
        public static bool IsValidPhone(ReadOnlySpan<char> input)
        {
            Span<char> dummy = stackalloc char[12];
            return TryNormalizePhone(input, dummy, out _, false);
        }

        /// <summary>
        /// Normalizes a Vietnamese phone string, returning the formatted string or null if invalid.
        /// </summary>
        public static string? NormalizePhone(string? input, bool international = false)
        {
            if (string.IsNullOrEmpty(input)) return null;

            Span<char> buffer = stackalloc char[12];
            if (TryNormalizePhone(input.AsSpan(), buffer, out int written, international))
            {
                return new string(buffer.Slice(0, written).ToArray());
            }
            return null;
        }

        #endregion
    }
}
