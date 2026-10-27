using System;
using Xunit;
using ZeroUI.Core.Data;

namespace ZeroUI.Core.Tests
{
    public class SearchFilterEngineVietnameseTests
    {
        [Fact]
        public void Matches_UnaccentedVietnameseQuery_MatchesAccentedText()
        {
            var engine = new SearchFilterEngine();
            engine.SetQuery("may han");

            // Should match target with accents
            Assert.True(engine.Matches("Máy Hàn Điện Tử Công Nghiệp"));
            Assert.True(engine.Matches("MÁY HÀN MIG-250"));
            Assert.False(engine.Matches("Máy Cắt Plasma"));
        }

        [Fact]
        public void Matches_AccentedQuery_MatchesUnaccentedText()
        {
            var engine = new SearchFilterEngine();
            engine.SetQuery("Máy Cắt");

            Assert.True(engine.Matches("may cat plasma"));
            Assert.True(engine.Matches("May Cat CNC"));
            Assert.False(engine.Matches("May Han TIG"));
        }

        [Fact]
        public void MatchesAnyColumn_VietnameseColumns_MatchesProperly()
        {
            var engine = new SearchFilterEngine();
            engine.SetQuery("Kho A hoa chat");

            var row = new[] { "LOT-9981", "Hóa chất tẩy rửa công nghiệp", "Kho A - Kệ 03" };
            Assert.True(engine.MatchesAnyColumn(row));

            var nonMatchingRow = new[] { "LOT-9982", "Dụng cụ bảo hộ", "Kho B - Kệ 01" };
            Assert.False(engine.MatchesAnyColumn(nonMatchingRow));
        }
    }
}
