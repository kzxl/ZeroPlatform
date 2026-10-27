using System;
using Xunit;
using ZeroText.Normalization;

namespace ZeroText.Tests
{
    public class ZeroPrimitivesCompatibilityShimTests
    {
        [Fact]
        public void CompatibilityShim_MethodsMatchCanonicalImplementation()
        {
            string sample = "Công Ty TNHH MTV Phần Mềm Số 1";

#pragma warning disable CS0618 // Type or member is obsolete
            Assert.Equal(
                VietnameseSearchNormalizer.ToSearchKeyword(sample),
                ZeroPrimitives.Validation.VietnameseSearchNormalizer.ToSearchKeyword(sample));

            Assert.Equal(
                VietnameseSearchNormalizer.ToSlug(sample),
                ZeroPrimitives.Validation.VietnameseSearchNormalizer.ToSlug(sample));

            Assert.Equal(
                VietnameseSearchNormalizer.RemoveDiacritics(sample),
                ZeroPrimitives.Validation.VietnameseSearchNormalizer.RemoveDiacritics(sample));

            Assert.Equal(
                VietnameseSearchNormalizer.UnSignedTransfer(sample),
                ZeroPrimitives.Validation.VietnameseSearchNormalizer.UnSignedTransfer(sample));

            Assert.Equal(
                VietnameseSearchNormalizer.StripDiacriticPreserveCase('Đ'),
                ZeroPrimitives.Validation.VietnameseSearchNormalizer.StripDiacriticPreserveCase('Đ'));

            ReadOnlySpan<char> span = sample.AsSpan();
            Span<char> dest1 = stackalloc char[sample.Length];
            Span<char> dest2 = stackalloc char[sample.Length];

            int w1 = VietnameseSearchNormalizer.NormalizeForSearch(span, dest1);
            int w2 = ZeroPrimitives.Validation.VietnameseSearchNormalizer.NormalizeForSearch(span, dest2);

            Assert.Equal(w1, w2);
            Assert.Equal(dest1.Slice(0, w1).ToString(), dest2.Slice(0, w2).ToString());
#pragma warning restore CS0618
        }
    }
}
