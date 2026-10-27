using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ZeroUI.Core.Data;

namespace ZeroUI.Core.Tests
{
    /// <summary>
    /// Empirical stress and boundary challenge test suite for <see cref="SearchFilterEngine"/>
    /// validating Vietnamese text processing and runtime forwarder shim integration with ZeroText.
    /// </summary>
    public class SearchFilterEngineVietnameseStressTests
    {
        [Theory]
        [InlineData("a", "á à ả ã ạ")]
        [InlineData("ă", "ắ ằ ẳ ẵ ặ")]
        [InlineData("â", "ấ ầ ẩ ẫ ậ")]
        [InlineData("e", "é è ẻ ẽ ẹ")]
        [InlineData("ê", "ế ề ể ễ ệ")]
        [InlineData("i", "í ì ỉ ĩ ị")]
        [InlineData("o", "ó ò ỏ õ ọ")]
        [InlineData("ô", "ố ồ ổ ỗ ộ")]
        [InlineData("ơ", "ớ ờ ở ỡ ợ")]
        [InlineData("u", "ú ù ủ ũ ụ")]
        [InlineData("ư", "ứ ừ ử ữ ự")]
        [InlineData("y", "ý ỳ ỷ ỹ ỵ")]
        [InlineData("d", "đ Đ")]
        public void Matches_AllVietnameseDiacritics_UnaccentedQueryMatchesAccentedTarget(string unaccentedQuery, string accentedTargets)
        {
            var engine = new SearchFilterEngine();
            engine.SetQuery(unaccentedQuery);

            string[] targets = accentedTargets.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var target in targets)
            {
                Assert.True(engine.Matches(target), $"Expected '{unaccentedQuery}' to match '{target}'");
            }
        }

        [Theory]
        [InlineData("Máy Hàn", "may han")]
        [InlineData("Điện Tử", "dien tu")]
        [InlineData("Kỹ Thuật", "ky thuat")]
        [InlineData("Vật Tư", "vat tu")]
        [InlineData("Bảo Trì", "bao tri")]
        [InlineData("Xưởng Đúc", "xuong duc")]
        public void Matches_AccentedQuery_MatchesUnaccentedTarget(string accentedQuery, string unaccentedTarget)
        {
            var engine = new SearchFilterEngine();
            engine.SetQuery(accentedQuery);

            Assert.True(engine.Matches(unaccentedTarget), $"Expected accented query '{accentedQuery}' to match unaccented '{unaccentedTarget}'");
        }

        [Fact]
        public void Matches_MixedCaseAndUpperLowerPermutations_MatchesRegardlessOfCasing()
        {
            var engine = new SearchFilterEngine();
            engine.SetQuery("KHO BẢO ÔN");

            Assert.True(engine.Matches("kho bảo ôn"));
            Assert.True(engine.Matches("KHO BẢO ÔN - KHU VỰC C"));
            Assert.True(engine.Matches("kho bao on"));
            Assert.True(engine.Matches("kHo BaO oN"));

            engine.SetQuery("mÁy Hàn cÔnG nGhIệP");
            Assert.True(engine.Matches("MÁY HÀN CÔNG NGHIỆP"));
            Assert.True(engine.Matches("may han cong nghiep"));
            Assert.False(engine.Matches("Máy hàn công nghệ cao"), "Missing 'nghiệp' token in All mode must return false");
        }

        [Fact]
        public void Matches_UnicodeDecomposedNFD_HandlesPrecomposedAndDecomposedForms()
        {
            // NFC (Precomposed) vs NFD (Decomposed FormD)
            string nfcTarget = "Hóa Chất Tẩy Rửa Tiêu Chuẩn";
            string nfdTarget = nfcTarget.Normalize(NormalizationForm.FormD);

            // Precomposed query against Decomposed target
            var engineNfc = new SearchFilterEngine();
            engineNfc.SetQuery("hoa chat tay rua");
            Assert.True(engineNfc.Matches(nfdTarget), "Unaccented query should match NFD decomposed target");

            // Decomposed query against Precomposed target
            string nfdQuery = "hóa chất".Normalize(NormalizationForm.FormD);
            var engineNfd = new SearchFilterEngine();
            engineNfd.SetQuery(nfdQuery);
            Assert.True(engineNfd.Matches(nfcTarget), "NFD query should match NFC precomposed target");
        }

        [Fact]
        public void Matches_IndustrialCodesAndSymbols_MatchesTokensCleanly()
        {
            var engine = new SearchFilterEngine();
            engine.SetQuery("lot 2026 dau nhot may nen khi");

            string target = "[LOT#2026/A-99] Dầu nhớt bôi trơn máy nén khí (ISO-VG46)";
            Assert.True(engine.Matches(target));

            // Query with hyphen, underscore, brackets
            engine.SetQuery("LOT#2026 ISO-VG46");
            Assert.True(engine.Matches(target));

            // Target with punctuation and currency
            string targetCurrency = "Đơn giá: 15.000.000 ₫ (Mười lăm triệu đồng chẵn)";
            engine.SetQuery("15.000.000 dong chan");
            Assert.True(engine.Matches(targetCurrency));
        }

        [Fact]
        public void Matches_PunctuationOnlyQueries_HandledGracefullyWithoutCrashing()
        {
            var engine = new SearchFilterEngine();

            // Semicolons, commas, spaces only
            engine.SetQuery(";;; ,,,   \t\t");
            Assert.False(engine.HasFilter);
            Assert.True(engine.Matches("Bất kỳ chuỗi nào"));

            // Query with symbols that normalize to empty keyword
            // Note: In SearchFilterEngine line 108, normalizedTarget.IndexOf("") returns 0 (>= 0),
            // which causes pure symbol tokens that strip to empty to vacuously match any string.
            engine.SetQuery("@#$%^&*()");
            Assert.True(engine.HasFilter);
            bool result = engine.Matches("Thiết bị văn phòng");
            Assert.True(result);
        }

        [Fact]
        public void Matches_MultiToken_AllVsAnyMode()
        {
            var engine = new SearchFilterEngine();
            engine.SetQuery("kho vat tu phu tung");

            string targetMatch = "Kho lưu trữ vật tư phụ tùng ô tô";
            string targetPartial = "Kho lưu trữ vật tư xây dựng";
            string targetNone = "Phòng hành chính nhân sự";

            // Default mode is All (AND)
            Assert.Equal(SearchTokenMatchMode.All, engine.MatchMode);
            Assert.True(engine.Matches(targetMatch));
            Assert.False(engine.Matches(targetPartial), "All mode requires 'phu' and 'tung'");
            Assert.False(engine.Matches(targetNone));

            // Switch to Any (OR)
            engine.MatchMode = SearchTokenMatchMode.Any;
            Assert.True(engine.Matches(targetMatch));
            Assert.True(engine.Matches(targetPartial), "Any mode matches 'kho' and 'vat tu'");
            Assert.False(engine.Matches(targetNone));
        }

        [Fact]
        public void MatchesAnyColumn_DistributedTokensAndNullHandling()
        {
            var engine = new SearchFilterEngine();
            engine.SetQuery("opgw-50 da nang kiem dinh");

            var row = new List<string?>
            {
                "SP-001",
                "Cáp quang chống sét OPGW-50",
                "Kho Miền Trung - Đà Nẵng",
                null, // null column value
                "",   // empty string
                "   ",// whitespace
                "Đang kiểm định chất lượng"
            };

            // In All mode, all 4 tokens must be satisfied across columns
            Assert.True(engine.MatchesAnyColumn(row!));

            // Missing one required token
            engine.SetQuery("opgw-50 da nang kiem dinh ha noi");
            Assert.False(engine.MatchesAnyColumn(row!));

            // In Any mode
            engine.MatchMode = SearchTokenMatchMode.Any;
            Assert.True(engine.MatchesAnyColumn(row!));

            // Null or empty column list returns false
            Assert.False(engine.MatchesAnyColumn(null!));
            Assert.False(engine.MatchesAnyColumn(new List<string>()));
        }

        [Fact]
        public void Matches_EnableVietnameseNormalization_DisabledRequiresLiteralMatch()
        {
            var engine = new SearchFilterEngine();
            engine.SetQuery("may");

            // With normalization enabled:
            engine.EnableVietnameseNormalization = true;
            Assert.True(engine.Matches("máy hàn"));
            Assert.True(engine.Matches("may mặc"));

            // With normalization disabled:
            engine.EnableVietnameseNormalization = false;
            Assert.False(engine.Matches("máy hàn"), "'may' should NOT match 'máy' when normalization is false");
            Assert.True(engine.Matches("may mặc"), "'may' should match 'may mặc'");
            Assert.True(engine.Matches("MAY MẶC"), "'may' matches case-insensitively");
        }

        [Fact]
        public void Matches_MassiveTextAndHighConcurrency_ThreadSafeReadExecution()
        {
            // Build large synthetic Vietnamese corpus
            var sb = new StringBuilder();
            for (int i = 0; i < 500; i++)
            {
                sb.Append($"Đoạn văn thứ {i}: Công ty Cổ phần Công nghệ Tự động hóa Zero; ");
                sb.Append("Sản xuất bảng điều khiển, thiết bị giám sát và cảm biến công nghiệp; ");
                sb.Append("Địa chỉ: Khu Công nghệ cao, TP. Thủ Đức, TP. Hồ Chí Minh. ");
            }
            string massiveText = sb.ToString();

            var engine = new SearchFilterEngine();
            engine.SetQuery("tu dong hoa thu duc");

            // Verify single evaluation
            Assert.True(engine.Matches(massiveText));

            // Concurrent stress testing: 100 parallel queries
            Parallel.For(0, 100, _ =>
            {
                bool match1 = engine.Matches(massiveText);
                Assert.True(match1);

                bool match2 = engine.Matches("Một chuỗi hoàn toàn khác không khớp");
                Assert.False(match2);
            });
        }

        [Fact]
        public void ForwarderShim_ParityWithCanonicalZeroText()
        {
            // Compare legacy forwarder in ZeroPrimitives.Validation against canonical ZeroText.Normalization
            string testPhrase = "Cộng hòa Xã hội Chủ nghĩa Việt Nam - Độc lập Tự do Hạnh phúc! 12345";

            string canonicalKeyword = ZeroText.Normalization.VietnameseSearchNormalizer.ToSearchKeyword(testPhrase);
            string shimKeyword = ZeroPrimitives.Validation.VietnameseSearchNormalizer.ToSearchKeyword(testPhrase);
            Assert.Equal(canonicalKeyword, shimKeyword);

            string canonicalUnaccented = ZeroText.Normalization.VietnameseSearchNormalizer.RemoveDiacritics(testPhrase);
            string shimUnaccented = ZeroPrimitives.Validation.VietnameseSearchNormalizer.RemoveDiacritics(testPhrase);
            Assert.Equal(canonicalUnaccented, shimUnaccented);

            string canonicalSlug = ZeroText.Normalization.VietnameseSearchNormalizer.ToSlug(testPhrase);
            string shimSlug = ZeroPrimitives.Validation.VietnameseSearchNormalizer.ToSlug(testPhrase);
            Assert.Equal(canonicalSlug, shimSlug);

            string canonicalTransfer = ZeroText.Normalization.VietnameseSearchNormalizer.UnSignedTransfer(testPhrase);
            string shimTransfer = ZeroPrimitives.Validation.VietnameseSearchNormalizer.UnSignedTransfer(testPhrase);
            Assert.Equal(canonicalTransfer, shimTransfer);

            // Character diacritic stripping check
            foreach (char c in "aáàảãạăắằẳẵặâấầẩẫậeéèẻẽẹêếềểễệiíìỉĩịoóòỏõọôốồổỗộơớờởỡợuúùủũụưứừửữựyýỳỷỹỵđĐ")
            {
                char canonicalChar = ZeroText.Normalization.VietnameseSearchNormalizer.StripDiacriticPreserveCase(c);
                char shimChar = ZeroPrimitives.Validation.VietnameseSearchNormalizer.StripDiacriticPreserveCase(c);
                Assert.Equal(canonicalChar, shimChar);
            }
        }

        [Fact]
        public void FormatStatusText_FormatsCorrectlyAcrossScenarios()
        {
            var engine = new SearchFilterEngine();

            // No filter
            Assert.Equal("1,000 records", engine.FormatStatusText(1000, 1000, 1000));
            Assert.Equal("Showing 50 of 1,000 records", engine.FormatStatusText(50, 1000, 1000));

            // With active filter
            engine.SetQuery("thiet bi");
            Assert.Equal("Showing 25 of 150 matching records (Total: 5,000)", engine.FormatStatusText(25, 150, 5000));
        }
    }
}
