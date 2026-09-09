using System;
using Xunit;
using ZeroPrimitives.Text;

namespace ZeroPrimitives.Tests
{
    public class VnCurrencyWordsTests
    {
        [Fact]
        public void TestBasicNumbersToWords()
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
        public void TestCustomOptions()
        {
            var optionsLe = new VnWordsOptions { UseLe = true };
            Assert.Equal("Một trăm lẻ năm", 105L.ToVnWords(optionsLe));

            var optionsNgan = new VnWordsOptions { UseNgan = true };
            Assert.Equal("Hai ngàn", 2000L.ToVnWords(optionsNgan));

            var optionsKhongHoa = new VnWordsOptions { CapitalizeFirstLetter = false };
            Assert.Equal("hai mươi", 20L.ToVnWords(optionsKhongHoa));
        }

        [Fact]
        public void TestVnCurrencyWordsStandard()
        {
            decimal amount = 1234567890m;
            string words = amount.ToVnCurrencyWords();

            Assert.Equal("Một tỷ hai trăm ba mươi tư triệu năm trăm sáu mươi bảy nghìn tám trăm chín mươi đồng chẵn", words);
        }

        [Fact]
        public void TestCurrencyWithSubunits()
        {
            decimal usd = 12.50m;
            string words = usd.ToVnCurrencyWords(currencyUnit: "đô la Mỹ", subunitUnit: "xu");

            Assert.Equal("Mười hai đô la Mỹ và năm mươi xu", words);
        }

        [Fact]
        public void TestNegativeAndZeroAmounts()
        {
            Assert.Equal("Không đồng chẵn", 0m.ToVnCurrencyWords());
            Assert.Equal("Âm năm mươi nghìn đồng chẵn", (-50000m).ToVnCurrencyWords());
        }
    }
}
