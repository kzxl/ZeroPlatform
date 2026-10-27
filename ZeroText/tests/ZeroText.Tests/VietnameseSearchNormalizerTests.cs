using System;
using System.Text;
using Xunit;
using ZeroText.Normalization;

namespace ZeroText.Tests
{
    public class VietnameseSearchNormalizerTests
    {
        [Fact]
        public void ToSearchKeyword_RemovesDiacriticsAndNormalizesSpaces()
        {
            string raw = "  Đơn Hàng / Bán Lẻ - Mã Phiếu: 12345 (Hà Nội)  ";
            string searchKey = VietnameseSearchNormalizer.ToSearchKeyword(raw);

            Assert.Equal("don hang ban le ma phieu 12345 ha noi", searchKey);
        }

        [Fact]
        public void ToSlug_GeneratesHyphenatedSlug()
        {
            string raw = "  Đơn Hàng / Bán Lẻ - Mã Phiếu: 12345 (Hà Nội)  ";
            string slug = VietnameseSearchNormalizer.ToSlug(raw);

            Assert.Equal("don-hang-ban-le-ma-phieu-12345-ha-noi", slug);
        }

        [Fact]
        public void RemoveDiacritics_PreservesCasingAndPunctuation()
        {
            string input = "Công Ty Cổ Phần Sài Gòn & Đắk Lắk (TP.HCM)!";
            string expected = "Cong Ty Co Phan Sai Gon & Dak Lak (TP.HCM)!";

            string result = VietnameseSearchNormalizer.RemoveDiacritics(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void UnSignedTransfer_ReplacesSpecialsAndSpaces()
        {
            string input = "Phiếu Nhập Kho #123 (Vật Tư)!";
            string result = VietnameseSearchNormalizer.UnSignedTransfer(input);

            Assert.Equal("Phieu_Nhap_Kho_-123_-Vat_Tu--", result);
        }

        [Fact]
        public void LongString_UsesArrayPoolCorrectly()
        {
            // String longer than 256 characters exercises ArrayPool branch
            string longPart = "Đơn hàng sản xuất kiểm định chất lượng cao cho nhà máy số 1 ";
            var sb = new StringBuilder();
            for (int i = 0; i < 10; i++)
            {
                sb.Append(longPart);
            }
            string longInput = sb.ToString();

            string result = VietnameseSearchNormalizer.RemoveDiacritics(longInput);
            Assert.DoesNotContain("Đ", result);
            Assert.DoesNotContain("ả", result);
            Assert.DoesNotContain("ế", result);
            Assert.StartsWith("Don hang san xuat", result);

            string searchKey = VietnameseSearchNormalizer.ToSearchKeyword(longInput);
            Assert.StartsWith("don hang san xuat", searchKey);

            string slug = VietnameseSearchNormalizer.ToSlug(longInput);
            Assert.StartsWith("don-hang-san-xuat", slug);
        }

        [Fact]
        public void SpanOverload_ZeroAllocates()
        {
            ReadOnlySpan<char> input = "Phiếu Nhập Kho Thành Phẩm".AsSpan();
            Span<char> buffer = stackalloc char[input.Length];

            int written = VietnameseSearchNormalizer.NormalizeForSearch(input, buffer);
            string result = buffer.Slice(0, written).ToString();

            Assert.Equal("phieu nhap kho thanh pham", result);
        }

        [Fact]
        public void TryNormalizeToBuffer_SucceedsWhenBufferSufficient()
        {
            ReadOnlySpan<char> input = "Hồ Chí Minh".AsSpan();
            Span<char> dest = stackalloc char[input.Length];

            bool ok = VietnameseSearchNormalizer.TryNormalizeToBuffer(input, dest, out int written);
            Assert.True(ok);
            Assert.Equal("ho chi minh", dest.Slice(0, written).ToString());
        }

        [Fact]
        public void TryNormalizeToBuffer_ReturnsFalseWhenBufferTooSmall()
        {
            ReadOnlySpan<char> input = "Đà Nẵng".AsSpan();
            Span<char> dest = stackalloc char[2];

            bool ok = VietnameseSearchNormalizer.TryNormalizeToBuffer(input, dest, out int written);
            Assert.False(ok);
            Assert.Equal(0, written);
        }

        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        public void NullOrEmptyInputs_HandleSafely(string? input, string expected)
        {
            Assert.Equal(expected, VietnameseSearchNormalizer.ToSearchKeyword(input));
            Assert.Equal(expected, VietnameseSearchNormalizer.ToSlug(input));
            Assert.Equal(expected, VietnameseSearchNormalizer.RemoveDiacritics(input));
            Assert.Equal(expected, VietnameseSearchNormalizer.UnSignedTransfer(input));
        }

        [Fact]
        public void WhitespaceInput_BehaviorIsPreserved()
        {
            string whitespace = "   ";
            Assert.Equal("", VietnameseSearchNormalizer.ToSearchKeyword(whitespace));
            Assert.Equal("", VietnameseSearchNormalizer.ToSlug(whitespace));
            Assert.Equal("   ", VietnameseSearchNormalizer.RemoveDiacritics(whitespace));
            Assert.Equal("", VietnameseSearchNormalizer.UnSignedTransfer(whitespace));
        }

        [Fact]
        public void StripDiacriticPreserveCase_HandlesAllVietnameseVowels()
        {
            Assert.Equal('A', VietnameseSearchNormalizer.StripDiacriticPreserveCase('Á'));
            Assert.Equal('a', VietnameseSearchNormalizer.StripDiacriticPreserveCase('à'));
            Assert.Equal('E', VietnameseSearchNormalizer.StripDiacriticPreserveCase('Ê'));
            Assert.Equal('e', VietnameseSearchNormalizer.StripDiacriticPreserveCase('ệ'));
            Assert.Equal('I', VietnameseSearchNormalizer.StripDiacriticPreserveCase('Í'));
            Assert.Equal('i', VietnameseSearchNormalizer.StripDiacriticPreserveCase('ĩ'));
            Assert.Equal('O', VietnameseSearchNormalizer.StripDiacriticPreserveCase('Ô'));
            Assert.Equal('o', VietnameseSearchNormalizer.StripDiacriticPreserveCase('ợ'));
            Assert.Equal('U', VietnameseSearchNormalizer.StripDiacriticPreserveCase('Ư'));
            Assert.Equal('u', VietnameseSearchNormalizer.StripDiacriticPreserveCase('ự'));
            Assert.Equal('Y', VietnameseSearchNormalizer.StripDiacriticPreserveCase('Ý'));
            Assert.Equal('y', VietnameseSearchNormalizer.StripDiacriticPreserveCase('ỷ'));
            Assert.Equal('D', VietnameseSearchNormalizer.StripDiacriticPreserveCase('Đ'));
            Assert.Equal('d', VietnameseSearchNormalizer.StripDiacriticPreserveCase('đ'));
            Assert.Equal('Z', VietnameseSearchNormalizer.StripDiacriticPreserveCase('Z'));
        }

        [Fact]
        public void UnicodeNfd_DecomposedVietnamese_NormalizesIdenticallyToNfc()
        {
            string nfc = "Cộng Hòa Xã Hội Chủ Nghĩa Việt Nam - Độc Lập - Tự Do - Hạnh Phúc";
            string nfd = nfc.Normalize(NormalizationForm.FormD);

            // NFD is longer than NFC because tone/vowel marks are decomposed into U+0300..U+036F
            Assert.True(nfd.Length > nfc.Length);

            Assert.Equal(
                VietnameseSearchNormalizer.ToSearchKeyword(nfc),
                VietnameseSearchNormalizer.ToSearchKeyword(nfd));

            Assert.Equal(
                VietnameseSearchNormalizer.ToSlug(nfc),
                VietnameseSearchNormalizer.ToSlug(nfd));

            Assert.Equal(
                VietnameseSearchNormalizer.RemoveDiacritics(nfc),
                VietnameseSearchNormalizer.RemoveDiacritics(nfd));

            Assert.Equal(
                VietnameseSearchNormalizer.UnSignedTransfer(nfc),
                VietnameseSearchNormalizer.UnSignedTransfer(nfd));

            Assert.Equal(
                VietnameseSearchNormalizer.Normalize(nfc),
                VietnameseSearchNormalizer.Normalize(nfd));

            Span<char> spanOut = stackalloc char[nfd.Length];
            int written = VietnameseSearchNormalizer.NormalizeToSpan(nfd.AsSpan(), spanOut);
            Assert.Equal(nfc.Length, written);
            Assert.Equal("cong hoa xa hoi chu nghia viet nam - doc lap - tu do - hanh phuc", spanOut.Slice(0, written).ToString());
        }
    }
}
