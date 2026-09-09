using Xunit;
using ZeroPrimitives.Extensions;

namespace ZeroPrimitives.Tests
{
    public class StringExtensionsTests
    {
        [Fact]
        public void NullIfEmpty_ReturnsNullForBlankStrings()
        {
            Assert.Null("".NullIfEmpty());
            Assert.Null("   ".NullIfEmpty());
            Assert.Null(((string?)null).NullIfEmpty());
            Assert.Equal("Hello", "Hello".NullIfEmpty());
        }

        [Fact]
        public void Truncate_CutsTextCorrectly()
        {
            string text = "This is a long sentence that needs truncation.";
            Assert.Equal("This is...", text.Truncate(10));
            Assert.Equal(text, text.Truncate(100));
            Assert.Equal(string.Empty, ((string?)null).Truncate(10));
        }

        [Fact]
        public void TrimChars_RemovesFromStartOrEnd()
        {
            string text = "ABCDEF";
            Assert.Equal("ABCD", text.TrimChars(2, removeFromStart: false));
            Assert.Equal("CDEF", text.TrimChars(2, removeFromStart: true));
        }

        [Fact]
        public void AppendPathSegments_CombinesCleanly()
        {
            string baseUrl = "https://api.example.com/";
            string fullUrl = baseUrl.AppendPathSegments("/v1/", "users", "/123");
            Assert.Equal("https://api.example.com/v1/users/123", fullUrl);
        }

        [Fact]
        public void SetQueryParam_AddsParametersCorrectly()
        {
            string url1 = "https://example.com/api";
            Assert.Equal("https://example.com/api?page=1", url1.SetQueryParam("page", 1));

            string url2 = "https://example.com/api?filter=active";
            Assert.Equal("https://example.com/api?filter=active&sort=desc", url2.SetQueryParam("sort", "desc"));
        }

        [Fact]
        public void ExtractDigits_And_ExtractDigitsToInt_WorkAccurately()
        {
            Assert.Equal("12345", "ABC-12345-XYZ".ExtractDigits());
            Assert.Equal("999", "Lot No: 999".ExtractDigits());
            Assert.Equal(string.Empty, "NoDigitsHere".ExtractDigits());
            Assert.Equal(string.Empty, ((string?)null).ExtractDigits());

            // Standard ExtractDigitsToInt
            Assert.Equal(12345, "ABC-12345-XYZ".ExtractDigitsToInt());
            Assert.Equal(999, "Lot No: 999".ExtractDigitsToInt());
            Assert.Equal(0, "NoDigitsHere".ExtractDigitsToInt());
            Assert.Equal(0, ((object?)null).ExtractDigitsToInt());

            // Legacy RemoveLetterToInt backward-compatibility alias
            Assert.Equal(12345, "ABC-12345-XYZ".RemoveLetterToInt());
            Assert.Equal(999, "Lot No: 999".RemoveLetterToInt());
            Assert.Equal(0, "NoDigitsHere".RemoveLetterToInt());
            Assert.Equal(0, ((object?)null).RemoveLetterToInt());
        }

        [Fact]
        public void RemoveDiacritics_StripsVietnameseAccents()
        {
            string vn = "Công ty Cổ phần Giải pháp Phần mềm";
            string stripped = vn.RemoveDiacritics();
            Assert.Equal("Cong ty Co phan Giai phap Phan mem", stripped);

            Assert.Equal("Dau", "Đậu".RemoveDiacritics());
        }
    }
}
