using System;
using Xunit;
using ZeroText.Localization;

namespace ZeroText.Tests
{
    public class VnCurrencyWordsTests
    {
        [Fact]
        public void BasicNumbersToWords()
        {
            Assert.Equal("Không", 0L.ToVnWords());
            Assert.Equal("Một", 1L.ToVnWords());
            Assert.Equal("Mười", 10L.ToVnWords());
            Assert.Equal("Mười một", 11L.ToVnWords());
            Assert.Equal("Mười lăm", 15L.ToVnWords());
            Assert.Equal("Hai mươi", 20L.ToVnWords());
            Assert.Equal("Hai mươi mốt", 21L.ToVnWords());
            Assert.Equal("Hai mươi tư", 24L.ToVnWords());
            Assert.Equal("Hai mươi lăm", 25L.ToVnWords());
            Assert.Equal("Một trăm linh năm", 105L.ToVnWords());
            Assert.Equal("Một nghìn không trăm linh năm", 1005L.ToVnWords());
            Assert.Equal("Một triệu", 1000000L.ToVnWords());
            Assert.Equal("Một tỷ", 1000000000L.ToVnWords());
        }

        [Fact]
        public void CustomOptionsAndDialects()
        {
            var optionsLe = new VnWordsOptions { UseSouthernZeroTens = true };
            Assert.Equal("Một trăm lẻ năm", 105L.ToVnWords(optionsLe));

            var optionsNgan = new VnWordsOptions { UseSouthernThousands = true };
            Assert.Equal("Hai ngàn", 2000L.ToVnWords(optionsNgan));

            var optionsNoCap = new VnWordsOptions { CapitalizeFirstLetter = false };
            Assert.Equal("hai mươi", 20L.ToVnWords(optionsNoCap));

            // Test SouthernDialect preset
            Assert.Equal("Một trăm lẻ năm ngàn không trăm hai mươi tư", 105024L.ToVnWords(VnWordsOptions.SouthernDialect));

            // Test UseTuForFour false
            var optionsBon = new VnWordsOptions { UseTuForFour = false };
            Assert.Equal("Hai mươi bốn", 24L.ToVnWords(optionsBon));
        }

        [Fact]
        public void VnCurrencyWordsStandard()
        {
            decimal amount = 1234567890m;
            string words = amount.ToVnCurrencyWords();

            Assert.Equal("Một tỷ hai trăm ba mươi tư triệu năm trăm sáu mươi bảy nghìn tám trăm chín mươi đồng chẵn", words);
        }

        [Fact]
        public void CurrencyWithSubunits()
        {
            decimal usd = 12.50m;
            string words = usd.ToVnCurrencyWords(currencyUnit: "đô la Mỹ", subunitUnit: "xu");

            Assert.Equal("Mười hai đô la Mỹ và năm mươi xu", words);
        }

        [Fact]
        public void NegativeAndZeroAmounts()
        {
            Assert.Equal("Không đồng chẵn", 0m.ToVnCurrencyWords());
            Assert.Equal("Âm năm mươi nghìn đồng chẵn", (-50000m).ToVnCurrencyWords());
            Assert.Equal("Âm một trăm", (-100L).ToVnWords());
        }

        [Fact]
        public void ToWords_AliasesMatchExactOutput()
        {
            Assert.Equal(VnCurrencyWords.ToVnWords(54321L), VnCurrencyWords.ToWords(54321L));
            Assert.Equal(VnCurrencyWords.ToVnCurrencyWords(500000m), VnCurrencyWords.ToWords(500000m));
        }

        [Fact]
        public void ExtremelyLargeAmount_HandlesMultipleBillionTriads()
        {
            // 1,000,000,000,000 (1 trillion = 1 nghìn tỷ)
            decimal oneTrillion = 1000000000000m;
            string words = oneTrillion.ToVnCurrencyWords();
            Assert.Contains("nghìn tỷ", words);

            // 1,000,000,000,000,000 (1 quadrillion = 1 triệu tỷ)
            decimal oneQuadrillion = 1000000000000000m;
            string wordsQuad = oneQuadrillion.ToVnCurrencyWords();
            Assert.Contains("triệu tỷ", wordsQuad);
        }
    }
}
