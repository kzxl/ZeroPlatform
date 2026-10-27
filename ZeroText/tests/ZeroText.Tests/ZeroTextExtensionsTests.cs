using System;
using Xunit;
using ZeroText;
using ZeroText.Localization;
using ZeroText.Normalization;
using ZeroText.Validation;

namespace ZeroText.Tests
{
    public class ZeroTextExtensionsTests
    {
        [Fact]
        public void NormalizationExtensions_MatchStaticMethods()
        {
            string sample = "Cộng Hòa Xã Hội Chủ Nghĩa Việt Nam";

            Assert.Equal(VietnameseSearchNormalizer.ToSearchKeyword(sample), sample.ToSearchKeyword());
            Assert.Equal(VietnameseSearchNormalizer.ToSlug(sample), sample.ToSlug());
            Assert.Equal(VietnameseSearchNormalizer.RemoveDiacritics(sample), sample.RemoveDiacritics());
            Assert.Equal(VietnameseSearchNormalizer.UnSignedTransfer(sample), sample.UnSignedTransfer());
        }

        [Fact]
        public void CurrencyExtensions_MatchStaticMethods()
        {
            long number = 1250000L;
            Assert.Equal(VnCurrencyWords.ToVnWords(number), number.ToVnWords());

            decimal amount = 987654321m;
            Assert.Equal(VnCurrencyWords.ToVnCurrencyWords(amount), amount.ToVnCurrencyWords());
        }

        [Fact]
        public void ValidationExtensions_MatchStaticMethods()
        {
            string mst = "0100109106";
            Assert.Equal(VnMasterDataValidators.IsValidMst(mst.AsSpan()), mst.IsValidMst());
            Assert.Equal(VnMasterDataValidators.IsValidTaxCode(mst), mst.IsValidTaxCode());

            string cccd = "079095012345";
            Assert.True(cccd.IsValidCccd(out int y, out bool m, out string? p));
            Assert.Equal(1995, y);
            Assert.True(m);
            Assert.Equal("TP. Hồ Chí Minh", p);
            Assert.True(cccd.IsValidCccd());

            string phone = "+84 90 123 4567";
            Assert.True(phone.IsValidVnPhone());
            Assert.Equal("0901234567", phone.NormalizeVnPhone(international: false));
            Assert.Equal("+84901234567", phone.NormalizeVnPhone(international: true));
        }
    }
}
