using System;
using ZeroText.Localization;
using ZeroText.Normalization;
using ZeroText.Validation;

namespace ZeroText
{
    /// <summary>
    /// Fluent extension methods for string normalization, currency spelling, and master data validation.
    /// </summary>
    public static class ZeroTextExtensions
    {
        /// <summary>
        /// Converts Vietnamese text into an unaccented, lowercase search token string.
        /// </summary>
        public static string ToSearchKeyword(this string? text)
            => VietnameseSearchNormalizer.ToSearchKeyword(text);

        /// <summary>
        /// Converts string into a clean SEO slug.
        /// </summary>
        public static string ToSlug(this string? text)
            => VietnameseSearchNormalizer.ToSlug(text);

        /// <summary>
        /// Removes diacritics from Vietnamese string while preserving letter casing and special symbols.
        /// </summary>
        public static string RemoveDiacritics(this string? text)
            => VietnameseSearchNormalizer.RemoveDiacritics(text);

        /// <summary>
        /// Converts Vietnamese string into unaccented, URL-safe transfer string.
        /// Replaces special characters with '-', spaces with '_', and strips diacritics.
        /// </summary>
        public static string UnSignedTransfer(this string? text)
            => VietnameseSearchNormalizer.UnSignedTransfer(text);

        /// <summary>
        /// Converts an integer or long value into Vietnamese words.
        /// </summary>
        public static string ToVnWords(this long number, VnWordsOptions? options = null)
            => VnCurrencyWords.ToVnWords(number, options);

        /// <summary>
        /// Converts a monetary decimal amount into standard Vietnamese currency words (e.g. VND, USD).
        /// </summary>
        public static string ToVnCurrencyWords(
            this decimal amount,
            string currencyUnit = "đồng",
            string subunitUnit = "xu",
            bool appendWholeNumberSuffix = true,
            VnWordsOptions? options = null)
            => VnCurrencyWords.ToVnCurrencyWords(amount, currencyUnit, subunitUnit, appendWholeNumberSuffix, options);

        /// <summary>
        /// Returns true if the string is a valid Vietnamese Tax Code (MST) under Circular 105/2020/TT-BTC.
        /// </summary>
        public static bool IsValidMst(this string? text)
            => VnMasterDataValidators.IsValidMst(text);

        /// <summary>
        /// Returns true if the string is a valid Vietnamese Tax Code (alias for IsValidMst).
        /// </summary>
        public static bool IsValidTaxCode(this string? text)
            => VnMasterDataValidators.IsValidTaxCode(text);

        /// <summary>
        /// Validates a 12-digit Vietnamese Citizen Identity Card (CCCD) and decodes metadata.
        /// </summary>
        public static bool IsValidCccd(
            this string? text,
            out int birthYear,
            out bool isMale,
            out string? provinceName)
            => VnMasterDataValidators.IsValidCccd(text, out birthYear, out isMale, out provinceName);

        /// <summary>
        /// Validates a 12-digit Vietnamese Citizen Identity Card (CCCD).
        /// </summary>
        public static bool IsValidCccd(this string? text)
            => VnMasterDataValidators.IsValidCccd(text);

        /// <summary>
        /// Returns true if the string is a valid Vietnamese mobile phone number.
        /// </summary>
        public static bool IsValidVnPhone(this string? text)
            => VnMasterDataValidators.IsValidPhone(text);

        /// <summary>
        /// Normalizes a Vietnamese phone string, returning the formatted string or null if invalid.
        /// </summary>
        public static string? NormalizeVnPhone(this string? text, bool international = false)
            => VnMasterDataValidators.NormalizePhone(text, international);

        /// <summary>
        /// Computes the Levenshtein edit distance against another string.
        /// </summary>
        public static int LevenshteinDistance(this string? text, string? other, bool ignoreCase = false)
            => Matching.FastFuzzyMatch.LevenshteinDistance(text.AsSpan(), other.AsSpan(), ignoreCase);

        /// <summary>
        /// Computes the normalized Levenshtein similarity ratio (0.0 to 1.0) against another string.
        /// </summary>
        public static double FuzzySimilarity(this string? text, string? other, bool ignoreCase = false)
            => Matching.FastFuzzyMatch.LevenshteinSimilarity(text.AsSpan(), other.AsSpan(), ignoreCase);

        /// <summary>
        /// Computes the Jaro-Winkler string similarity metric (0.0 to 1.0) against another string.
        /// </summary>
        public static double JaroWinklerSimilarity(this string? text, string? other, double prefixScale = 0.1, bool ignoreCase = false)
            => Matching.FastFuzzyMatch.JaroWinklerSimilarity(text.AsSpan(), other.AsSpan(), prefixScale, ignoreCase);
    }
}
