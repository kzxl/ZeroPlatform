using System;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Validation
{
    /// <summary>
    /// Backward-compatibility shim forwarding calls to <see cref="ZeroText.Normalization.VietnameseSearchNormalizer"/>.
    /// Enables non-breaking migration for consumers referencing the legacy ZeroPrimitives.Validation namespace.
    /// </summary>
    [Obsolete("Use ZeroText.Normalization.VietnameseSearchNormalizer instead. This shim will be removed in future major versions.")]
    public static class VietnameseSearchNormalizer
    {
        /// <summary>
        /// Normalizes Vietnamese text into unaccented, lowercase search tokens directly into destination span.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NormalizeForSearch(ReadOnlySpan<char> source, Span<char> destination)
            => ZeroText.Normalization.VietnameseSearchNormalizer.NormalizeForSearch(source, destination);

        /// <summary>
        /// Converts Vietnamese string into an unaccented, lowercase search string.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToSearchKeyword(string? source)
            => ZeroText.Normalization.VietnameseSearchNormalizer.ToSearchKeyword(source);

        /// <summary>
        /// Generates a clean, SEO-friendly slug with hyphen separators.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToSlug(ReadOnlySpan<char> source, Span<char> destination)
            => ZeroText.Normalization.VietnameseSearchNormalizer.ToSlug(source, destination);

        /// <summary>
        /// Converts string into a clean SEO slug.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToSlug(string? source)
            => ZeroText.Normalization.VietnameseSearchNormalizer.ToSlug(source);

        /// <summary>
        /// Removes diacritics from Vietnamese string while preserving letter casing and special symbols.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string RemoveDiacritics(string? source)
            => ZeroText.Normalization.VietnameseSearchNormalizer.RemoveDiacritics(source);

        /// <summary>
        /// Converts Vietnamese string into unaccented, URL-safe transfer string.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string UnSignedTransfer(string? source)
            => ZeroText.Normalization.VietnameseSearchNormalizer.UnSignedTransfer(source);

        /// <summary>
        /// Strips diacritics from a Vietnamese character while preserving original casing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char StripDiacriticPreserveCase(char c)
            => ZeroText.Normalization.VietnameseSearchNormalizer.StripDiacriticPreserveCase(c);
    }
}
