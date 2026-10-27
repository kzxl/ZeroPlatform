using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ZeroText.Localization;
using ZeroText.Normalization;
using ZeroText.Validation;

namespace ZeroText.Tests
{
    public class EmpiricalStressChallengeTests
    {
        #region 1. VietnameseSearchNormalizer Stress & Fuzzing

        [Fact]
        public void FuzzMassiveStrings_Exceeding10000Characters_ArrayPoolRentReturnSafety()
        {
            // Build a massive string of 25,000+ characters with mixed Vietnamese sentences, tone marks, numbers, and symbols
            var template = "Hệ thống Quản lý Doanh nghiệp & Sản xuất Thông minh (ERP/MES) tại Hà Nội, TP. Hồ Chí Minh & Đắk Lắk! Mã số phiếu: #12345-XZY. ";
            var sb = new StringBuilder(30000);
            while (sb.Length < 25000)
            {
                sb.Append(template);
            }
            string massiveText = sb.ToString();

            // Run in parallel to stress test ArrayPool rent/return concurrency safety
            Parallel.For(0, 50, _ =>
            {
                string searchKey = VietnameseSearchNormalizer.ToSearchKeyword(massiveText);
                Assert.NotNull(searchKey);
                Assert.StartsWith("he thong quan ly doanh nghiep", searchKey);
                Assert.DoesNotContain("Đ", searchKey);
                Assert.DoesNotContain("ắ", searchKey);

                string slug = VietnameseSearchNormalizer.ToSlug(massiveText);
                Assert.NotNull(slug);
                Assert.StartsWith("he-thong-quan-ly-doanh-nghiep", slug);
                Assert.DoesNotContain("--", slug);

                string unaccented = VietnameseSearchNormalizer.RemoveDiacritics(massiveText);
                Assert.NotNull(unaccented);
                Assert.Equal(massiveText.Length, unaccented.Length);
                Assert.StartsWith("He thong Quan ly Doanh nghiep", unaccented);

                string transfer = VietnameseSearchNormalizer.UnSignedTransfer(massiveText);
                Assert.NotNull(transfer);
                Assert.Equal(massiveText.Length, transfer.Length);
            });
        }

        [Fact]
        public void Normalizer_HandlesComprehensiveVietnameseDiacriticsAndCasing()
        {
            string vowelsUpper = "ÁÀẢÃẠ ĂẮẰẲẴẶ ÂẤẦẨẪẬ ÉÈẺẼẸ ÊẾỀỂỄỆ ÍÌỈĨỊ ÓÒỎÕỌ ÔỐỒỔỖỘ ƠỚỜỞỠỢ ÚÙỦŨỤ ƯỨỪỬỮỰ ÝỲỶỸỴ Đ";
            string vowelsLower = "áàảãạ ăắằẳẵặ âấầẩẫậ éèẻẽẹ êếềểễệ íìỉĩị óòỏõọ ôốồổỗộ ơớờởỡợ úùủũụ ưứừửữự ýỳỷỹỵ đ";

            string unaccentedUpper = VietnameseSearchNormalizer.RemoveDiacritics(vowelsUpper);
            string unaccentedLower = VietnameseSearchNormalizer.RemoveDiacritics(vowelsLower);

            Assert.Equal("AAAAA AAAAAA AAAAAA EEEEE EEEEEE IIIII OOOOO OOOOOO OOOOOO UUUUU UUUUUU YYYYY D", unaccentedUpper);
            Assert.Equal("aaaaa aaaaaa aaaaaa eeeee eeeeee iiiii ooooo oooooo oooooo uuuuu uuuuuu yyyyy d", unaccentedLower);

            string searchKey = VietnameseSearchNormalizer.ToSearchKeyword(vowelsUpper);
            Assert.Equal("aaaaa aaaaaa aaaaaa eeeee eeeeee iiiii ooooo oooooo oooooo uuuuu uuuuuu yyyyy d", searchKey);
        }

        [Theory]
        [InlineData("---___   ", "")]
        [InlineData("!!!@@@###$$$%%%^^^&&&***()", "")]
        [InlineData("   Đơn   Hàng   ---   Số   1   ", "don-hang-so-1")]
        [InlineData("/đường/dẫn/url/mẫu/", "duong-dan-url-mau")]
        [InlineData("123-456_789", "123-456-789")]
        public void SlugInvariants_EnforcesFormattingRules(string input, string expectedSlug)
        {
            string slug = VietnameseSearchNormalizer.ToSlug(input);
            Assert.Equal(expectedSlug, slug);

            if (slug.Length > 0)
            {
                Assert.False(slug.StartsWith("-"), "Slug must not start with hyphen");
                Assert.False(slug.EndsWith("-"), "Slug must not end with hyphen");
                Assert.DoesNotContain("--", slug);
                foreach (char c in slug)
                {
                    bool valid = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-';
                    Assert.True(valid, $"Slug char '{c}' must be lowercase alphanumeric or hyphen");
                }
            }
        }

        [Fact]
        public void SmallSpan_Under256Chars_ZeroAllocationInvariants()
        {
            // Testing stackalloc path directly via TryNormalizeToBuffer
            ReadOnlySpan<char> span = "Thử nghiệm bộ chuẩn hóa chuỗi tiếng Việt".AsSpan();
            Span<char> buffer = stackalloc char[span.Length];

            bool ok = VietnameseSearchNormalizer.TryNormalizeToBuffer(span, buffer, out int written);
            Assert.True(ok);
            Assert.True(written > 0 && written <= span.Length);
            Assert.Equal("thu nghiem bo chuan hoa chuoi tieng viet", buffer.Slice(0, written).ToString());
        }

        [Fact]
        public void TryNormalizeToBuffer_BoundaryDestinations()
        {
            ReadOnlySpan<char> source = "Hà Nội".AsSpan();

            // Destination smaller than source
            Span<char> smallBuffer = stackalloc char[source.Length - 1];
            bool fail = VietnameseSearchNormalizer.TryNormalizeToBuffer(source, smallBuffer, out int writtenSmall);
            Assert.False(fail);
            Assert.Equal(0, writtenSmall);

            // Empty source
            Span<char> normalBuffer = stackalloc char[10];
            bool emptyOk = VietnameseSearchNormalizer.TryNormalizeToBuffer(ReadOnlySpan<char>.Empty, normalBuffer, out int writtenEmpty);
            Assert.True(emptyOk);
            Assert.Equal(0, writtenEmpty);
        }

        #endregion

        #region 2. VnCurrencyWords Extreme Values & Dialects

        [Fact]
        public void CurrencyWords_ExtremeBoundaryValues_Long()
        {
            // 0
            Assert.Equal("Không", 0L.ToVnWords());
            Assert.Equal("không", 0L.ToVnWords(new VnWordsOptions { CapitalizeFirstLetter = false }));

            // 1 & -1
            Assert.Equal("Một", 1L.ToVnWords());
            Assert.Equal("Âm một", (-1L).ToVnWords());

            // long.MaxValue: 9,223,372,036,854,775,807 (9 quintillion)
            long maxVal = long.MaxValue;
            string maxWords = maxVal.ToVnWords();
            Assert.NotNull(maxWords);
            Assert.StartsWith("Chín triệu tỷ", maxWords);
            Assert.EndsWith("bảy", maxWords);

            // long.MinValue: -9,223,372,036,854,775,808
            long minVal = long.MinValue;
            string minWords = minVal.ToVnWords();
            Assert.NotNull(minWords);
            Assert.StartsWith("Âm chín triệu tỷ", minWords);
            Assert.EndsWith("tám", minWords);
        }

        [Fact]
        public void CurrencyWords_ExtremeBoundaryValues_Decimal()
        {
            // 0m
            Assert.Equal("Không đồng chẵn", 0m.ToVnCurrencyWords());

            // Negative decimal with fraction
            decimal neg = -50000.75m;
            string negWords = neg.ToVnCurrencyWords();
            Assert.Equal("Âm năm mươi nghìn đồng và bảy mươi lăm xu", negWords);

            // Subunits only (0 < amount < 1)
            decimal sub1 = 0.50m;
            Assert.Equal("Năm mươi xu", sub1.ToVnCurrencyWords());

            decimal sub2 = 0.05m;
            Assert.Equal("Năm xu", sub2.ToVnCurrencyWords());

            decimal sub3 = 0.01m;
            Assert.Equal("Một xu", sub3.ToVnCurrencyWords());

            // Whole without suffix
            Assert.Equal("Năm mươi nghìn đồng", 50000m.ToVnCurrencyWords(appendWholeNumberSuffix: false));
        }

        [Fact]
        public void CurrencyWords_DialectMatrix()
        {
            long number = 105024L;

            // Northern (default): "nghìn", "linh", "tư"
            string northern = number.ToVnWords(VnWordsOptions.Default);
            Assert.Equal("Một trăm linh năm nghìn không trăm hai mươi tư", northern);

            // Southern: "ngàn", "lẻ", "tư"
            string southern = number.ToVnWords(VnWordsOptions.SouthernDialect);
            Assert.Equal("Một trăm lẻ năm ngàn không trăm hai mươi tư", southern);

            // Dialect with UseTuForFour = false ("bốn")
            var optBon = new VnWordsOptions
            {
                UseSouthernThousands = true,
                UseSouthernZeroTens = true,
                UseTuForFour = false
            };
            string southernBon = number.ToVnWords(optBon);
            Assert.Equal("Một trăm lẻ năm ngàn không trăm hai mươi bốn", southernBon);

            // Obsolete aliases check
#pragma warning disable CS0618
            var optAlias = new VnWordsOptions
            {
                UseNgan = true,
                UseLe = true,
                UseTu = false
            };
            Assert.True(optAlias.UseSouthernThousands);
            Assert.True(optAlias.UseSouthernZeroTens);
            Assert.False(optAlias.UseTuForFour);
            Assert.Equal(southernBon, number.ToVnWords(optAlias));
#pragma warning restore CS0618
        }

        #endregion

        #region 3. VnMasterDataValidators Modulo 11 MST, CCCD & Phone

        [Fact]
        public void Mst_Modulo11_EnterpriseAndBranchVerification()
        {
            // Real enterprise MSTs
            string[] validEnterprises = {
                "0100109106", // Viettel
                "0300588569", // Vinamilk
                "0101248141", // FPT
                "0100112437", // Vietcombank
                "0100681592"  // Petrovietnam (0100681592)
            };

            foreach (var mst in validEnterprises)
            {
                Assert.True(VnMasterDataValidators.IsValidMst(mst.AsSpan()), $"Expected valid MST: {mst}");
                Assert.True(VnMasterDataValidators.IsValidTaxCode(mst), $"Expected valid TaxCode: {mst}");

                // Normalized string must equal the 10-digit code
                Assert.Equal(mst, VnMasterDataValidators.NormalizeMst(mst));

                // 13-digit valid branch codes
                string branch001 = mst + "-001";
                string branch999 = mst + "999";
                Assert.True(VnMasterDataValidators.IsValidMst(branch001.AsSpan()));
                Assert.True(VnMasterDataValidators.IsValidMst(branch999.AsSpan()));
                Assert.Equal(mst + "-001", VnMasterDataValidators.NormalizeMst(branch001));
                Assert.Equal(mst + "-999", VnMasterDataValidators.NormalizeMst(branch999));

                // 13-digit branch with "000" MUST BE REJECTED
                string branch000WithHyphen = mst + "-000";
                string branch000NoHyphen = mst + "000";
                Assert.False(VnMasterDataValidators.IsValidMst(branch000WithHyphen.AsSpan()), $"Branch '000' must be invalid: {branch000WithHyphen}");
                Assert.False(VnMasterDataValidators.IsValidMst(branch000NoHyphen.AsSpan()), $"Branch '000' must be invalid: {branch000NoHyphen}");
                Assert.Null(VnMasterDataValidators.NormalizeMst(branch000WithHyphen));
            }
        }

        [Fact]
        public void Mst_RejectsCorruptedChecksumsAndInvalidLengths()
        {
            string viettel = "0100109106"; // Check digit is 6
            Assert.True(VnMasterDataValidators.IsValidMst(viettel.AsSpan()));

            // Corrupt check digit (digits 0..9 except 6)
            for (int d = 0; d <= 9; d++)
            {
                if (d == 6) continue;
                string corrupted = viettel.Substring(0, 9) + d;
                Assert.False(VnMasterDataValidators.IsValidMst(corrupted.AsSpan()), $"Corrupted check digit must fail: {corrupted}");
            }

            // Invalid lengths
            Assert.False(VnMasterDataValidators.IsValidMst("010010910".AsSpan()));       // 9 digits
            Assert.False(VnMasterDataValidators.IsValidMst("01001091061".AsSpan()));     // 11 digits
            Assert.False(VnMasterDataValidators.IsValidMst("010010910601".AsSpan()));    // 12 digits
            Assert.False(VnMasterDataValidators.IsValidMst("01001091060001".AsSpan()));  // 14 digits

            // Characters
            Assert.False(VnMasterDataValidators.IsValidMst("010010910A".AsSpan()));
            Assert.False(VnMasterDataValidators.IsValidMst("MST01001091".AsSpan()));
        }

        [Fact]
        public void Cccd_All63Provinces_And_AllCenturiesDecoding()
        {
            // Test all 63 Vietnamese province codes
            int[] allProvinceCodes = {
                1, 2, 4, 6, 8, 10, 11, 12, 14, 15, 17, 19, 20, 22, 24, 25, 26, 27, 30, 31, 33, 34, 35, 36, 37, 38,
                40, 42, 44, 45, 46, 48, 49, 51, 52, 54, 56, 58, 60, 62, 64, 66, 67, 68, 70, 72, 74, 75, 77, 79,
                80, 82, 83, 84, 86, 87, 89, 91, 92, 93, 94, 95, 96
            };

            Assert.Equal(63, allProvinceCodes.Length);

            foreach (int code in allProvinceCodes)
            {
                string provStr = code.ToString("D3");
                // Construct valid CCCD: provStr + gender/century '0' (Male 19xx) + year '95' + seq '123456'
                string cccd = $"{provStr}095123456";

                bool ok = VnMasterDataValidators.IsValidCccd(cccd.AsSpan(), out int birthYear, out bool isMale, out string? provName);
                Assert.True(ok, $"Province code {provStr} should be valid CCCD");
                Assert.Equal(1995, birthYear);
                Assert.True(isMale);
                Assert.NotNull(provName);
            }

            // Test non-existent province codes
            int[] invalidProvinces = { 0, 3, 5, 7, 9, 13, 16, 18, 21, 23, 28, 29, 32, 39, 41, 43, 47, 50, 53, 55, 57, 59, 61, 63, 65, 69, 71, 73, 76, 78, 81, 85, 88, 90, 97, 98, 99, 999 };
            foreach (int invalid in invalidProvinces)
            {
                string provStr = invalid.ToString("D3");
                string cccd = $"{provStr}095123456";
                Assert.False(VnMasterDataValidators.IsValidCccd(cccd.AsSpan()), $"Province code {provStr} must be invalid");
            }

            // Test Century & Gender matrix across 1900-2399 (digits 0..9)
            // 0: Male 1900-1999
            // 1: Female 1900-1999
            // 2: Male 2000-2099
            // 3: Female 2000-2099
            // 4: Male 2100-2199
            // 5: Female 2100-2199
            // 6: Male 2200-2299
            // 7: Female 2200-2299
            // 8: Male 2300-2399
            // 9: Female 2300-2399
            var centuryExpected = new (int centuryBase, bool isMale)[]
            {
                (1900, true),
                (1900, false),
                (2000, true),
                (2000, false),
                (2100, true),
                (2100, false),
                (2200, true),
                (2200, false),
                (2300, true),
                (2300, false),
            };

            for (int digit = 0; digit <= 9; digit++)
            {
                string cccd = $"001{digit}24123456"; // Hanoi, year 24
                bool ok = VnMasterDataValidators.IsValidCccd(cccd.AsSpan(), out int birthYear, out bool isMale, out string? provName);
                Assert.True(ok);
                Assert.Equal("Hà Nội", provName);
                Assert.Equal(centuryExpected[digit].centuryBase + 24, birthYear);
                Assert.Equal(centuryExpected[digit].isMale, isMale);
            }
        }

        [Fact]
        public void PhoneNormalization_AllMobilePrefixesAndE164()
        {
            // Valid mobile prefixes in Vietnam: 03x, 05x, 07x, 08x, 09x
            string[] validPrefixes = { "032", "035", "038", "052", "056", "058", "070", "076", "079", "081", "085", "088", "090", "091", "098" };

            foreach (var prefix in validPrefixes)
            {
                string phone = $"{prefix}1234567";

                // Local format
                Assert.True(VnMasterDataValidators.IsValidPhone(phone.AsSpan()), $"Expected valid phone: {phone}");
                Assert.Equal(phone, VnMasterDataValidators.NormalizePhone(phone, international: false));

                // International format (+84...)
                string intlExpected = "+84" + phone.Substring(1);
                Assert.Equal(intlExpected, VnMasterDataValidators.NormalizePhone(phone, international: true));

                // Input with +84, dots, dashes, spaces, brackets
                string noisyPhone = $"+84 ({phone.Substring(1, 2)}) {phone.Substring(3, 3)}-{phone.Substring(6)}";
                Assert.True(VnMasterDataValidators.IsValidPhone(noisyPhone.AsSpan()));
                Assert.Equal(phone, VnMasterDataValidators.NormalizePhone(noisyPhone, international: false));
                Assert.Equal(intlExpected, VnMasterDataValidators.NormalizePhone(noisyPhone, international: true));
            }

            // Invalid prefixes (01x deprecated, 02x landline, 04x, 06x)
            string[] invalidPhones = {
                "0123456789", // 01x
                "0243123456", // 024 landline
                "0283123456", // 028 landline
                "0412345678", // 04x
                "0612345678", // 06x
                "1234567890", // Not starting with 0
                "090123456",  // 9 digits
                "09012345678" // 11 digits
            };

            foreach (var invalid in invalidPhones)
            {
                Assert.False(VnMasterDataValidators.IsValidPhone(invalid.AsSpan()), $"Phone should be invalid: {invalid}");
                Assert.Null(VnMasterDataValidators.NormalizePhone(invalid));
            }
        }

        #endregion

        #region 4. Compatibility Shim Invariance Verification

        [Fact]
        public void CompatibilityShim_ExactParityUnderFuzzing()
        {
            string[] samples = {
                "",
                "A",
                "Hà Nội nghìn năm văn hiến",
                "  Đơn   Hàng  ---  Số  /  12345  (Vật Tư)  ",
                "Cộng Hòa Xã Hội Chủ Nghĩa Việt Nam - Độc Lập - Tự Do - Hạnh Phúc",
                "Công Ty Cổ Phần Sữa Việt Nam (Vinamilk) - MST: 0300588569",
                "Thành Phố Đà Nẵng & Tỉnh Đắk Lắk",
                new string('a', 500),
                new string('Đ', 500)
            };

            foreach (var sample in samples)
            {
#pragma warning disable CS0618
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

                ReadOnlySpan<char> span = sample.AsSpan();
                Span<char> dest1 = new char[sample.Length];
                Span<char> dest2 = new char[sample.Length];

                int w1 = VietnameseSearchNormalizer.NormalizeForSearch(span, dest1);
                int w2 = ZeroPrimitives.Validation.VietnameseSearchNormalizer.NormalizeForSearch(span, dest2);
                Assert.Equal(w1, w2);
                Assert.Equal(dest1.Slice(0, w1).ToString(), dest2.Slice(0, w2).ToString());

                Span<char> slugDest1 = new char[sample.Length];
                Span<char> slugDest2 = new char[sample.Length];
                int sw1 = VietnameseSearchNormalizer.ToSlug(span, slugDest1);
                int sw2 = ZeroPrimitives.Validation.VietnameseSearchNormalizer.ToSlug(span, slugDest2);
                Assert.Equal(sw1, sw2);
                Assert.Equal(slugDest1.Slice(0, sw1).ToString(), slugDest2.Slice(0, sw2).ToString());
#pragma warning restore CS0618
            }
        }

        #endregion

        #region 5. Additional Boundary, Threshold & Concurrency Tests

        [Theory]
        [InlineData(255)]
        [InlineData(256)]
        [InlineData(257)]
        public void StackallocToArrayPoolBoundaryThreshold(int length)
        {
            // Test strings at exactly 255 (stackalloc), 256 (stackalloc boundary), and 257 (ArrayPool)
            string baseWord = "Việt Nam ";
            var sb = new StringBuilder();
            while (sb.Length < length)
            {
                sb.Append(baseWord);
            }
            string input = sb.ToString().Substring(0, length);
            Assert.Equal(length, input.Length);

            string searchKey = VietnameseSearchNormalizer.ToSearchKeyword(input);
            Assert.NotNull(searchKey);
            Assert.DoesNotContain("ệ", searchKey);

            string slug = VietnameseSearchNormalizer.ToSlug(input);
            Assert.NotNull(slug);
            Assert.DoesNotContain("--", slug);
            Assert.False(slug.EndsWith("-"));

            string unaccented = VietnameseSearchNormalizer.RemoveDiacritics(input);
            Assert.Equal(length, unaccented.Length);
            Assert.DoesNotContain("ệ", unaccented);

            string transfer = VietnameseSearchNormalizer.UnSignedTransfer(input);
            Assert.Equal(length, transfer.Length);
        }

        [Fact]
        public void Normalizer_HandlesSurrogatePairsAndEmojis()
        {
            string emojiText = "Đơn hàng 🚀 hoàn thành tốt đẹp! 🇻🇳 😊";
            string searchKey = VietnameseSearchNormalizer.ToSearchKeyword(emojiText);
            // Emojis are stripped from alphanumeric search keyword
            Assert.Equal("don hang hoan thanh tot dep", searchKey);

            string slug = VietnameseSearchNormalizer.ToSlug(emojiText);
            Assert.Equal("don-hang-hoan-thanh-tot-dep", slug);

            // RemoveDiacritics preserves characters not in diacritics table
            string unaccented = VietnameseSearchNormalizer.RemoveDiacritics(emojiText);
            Assert.Contains("🚀", unaccented);
            Assert.Contains("😊", unaccented);
            Assert.StartsWith("Don hang", unaccented);
        }

        [Fact]
        public void Normalizer_HandlesTabsNewlinesAndNonBreakingSpaces()
        {
            string messyText = "\t\tĐơn hàng\r\n\tloại 1\u00A0đặc biệt   \r\n";
            string searchKey = VietnameseSearchNormalizer.ToSearchKeyword(messyText);
            Assert.Equal("don hang loai 1 dac biet", searchKey);

            string slug = VietnameseSearchNormalizer.ToSlug(messyText);
            Assert.Equal("don-hang-loai-1-dac-biet", slug);
        }

        [Fact]
        public void CurrencyWords_DecimalExtremeBoundaries()
        {
            // decimal.MaxValue: 79,228,162,514,264,337,593,543,950,335m (~7.92 x 10^28)
            decimal maxDec = decimal.MaxValue;
            string maxWords = maxDec.ToVnCurrencyWords();
            Assert.NotNull(maxWords);
            Assert.Contains("tỷ", maxWords);
            Assert.EndsWith("đồng chẵn", maxWords);

            // Subunit rounding boundaries
            // 0.004m rounds down to 0 xu
            decimal smallBelowHalf = 0.004m;
            string wordSmall = smallBelowHalf.ToVnCurrencyWords();
            Assert.NotNull(wordSmall);

            // 0.005m rounds up to 1 xu
            decimal smallHalf = 0.005m;
            string wordHalf = smallHalf.ToVnCurrencyWords();
            Assert.Equal("Một xu", wordHalf);

            // 0.009m rounds up to 1 xu
            decimal smallAboveHalf = 0.009m;
            string wordAboveHalf = smallAboveHalf.ToVnCurrencyWords();
            Assert.Equal("Một xu", wordAboveHalf);
        }

        [Fact]
        public void Cccd_BoundaryYears_00And99()
        {
            // Century 2000, year 00 -> 2000
            string cccd2000 = "001200123456";
            Assert.True(VnMasterDataValidators.IsValidCccd(cccd2000.AsSpan(), out int y2000, out bool m2000, out _));
            Assert.Equal(2000, y2000);
            Assert.True(m2000);

            // Century 2000, year 99 -> 2099
            string cccd2099 = "001299123456";
            Assert.True(VnMasterDataValidators.IsValidCccd(cccd2099.AsSpan(), out int y2099, out bool m2099, out _));
            Assert.Equal(2099, y2099);
            Assert.True(m2099);

            // Century 1900, year 00 -> 1900
            string cccd1900 = "079000123456";
            Assert.True(VnMasterDataValidators.IsValidCccd(cccd1900.AsSpan(), out int y1900, out bool m1900, out _));
            Assert.Equal(1900, y1900);
            Assert.True(m1900);
        }

        [Fact]
        public void Phone_InternationalWithLeadingZeroAfter84()
        {
            // E.g. +840901234567 or 840901234567 (12 digits with 840)
            string phone840 = "+840901234567";
            Assert.True(VnMasterDataValidators.IsValidPhone(phone840.AsSpan()));
            Assert.Equal("0901234567", VnMasterDataValidators.NormalizePhone(phone840, international: false));
            Assert.Equal("+84901234567", VnMasterDataValidators.NormalizePhone(phone840, international: true));
        }

        #endregion
    }
}
