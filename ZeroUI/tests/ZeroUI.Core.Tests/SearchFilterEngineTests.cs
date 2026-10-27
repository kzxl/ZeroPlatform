using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Data;

namespace ZeroUI.Core.Tests
{
    public class SearchFilterEngineTests
    {
        [Fact]
        public void SetQuery_ParsesMultipleDelimitersAndRemovesDuplicates()
        {
            var engine = new SearchFilterEngine();
            engine.SetQuery("resistor   10k, smd; 10k");

            Assert.True(engine.HasFilter);
            Assert.Equal(3, engine.Tokens.Count);
            Assert.Equal("resistor", engine.Tokens[0]);
            Assert.Equal("10k", engine.Tokens[1]);
            Assert.Equal("smd", engine.Tokens[2]);
        }

        [Fact]
        public void Matches_AllMode_RequiresAllTokensCaseInsensitive()
        {
            var engine = new SearchFilterEngine();
            engine.SetQuery("MOTOR 24V");

            Assert.True(engine.Matches("Brushless DC Motor 24V 3000RPM"));
            Assert.False(engine.Matches("Brushless DC Motor 12V 3000RPM")); // missing 24V
            Assert.False(engine.Matches("Servo Drive 24V")); // missing Motor
        }

        [Fact]
        public void MatchesAnyColumn_AllMode_MatchesAcrossDifferentColumns()
        {
            var engine = new SearchFilterEngine();
            engine.SetQuery("IC-STM32 Arm");

            // Row 1: Code = "IC-STM32F401", Description = "Microcontroller ARM Cortex-M4"
            var row1 = new List<string> { "IC-STM32F401", "Microcontroller ARM Cortex-M4", "Active" };
            Assert.True(engine.MatchesAnyColumn(row1));

            // Row 2: Code = "IC-ATMEGA328P", Description = "8-bit AVR Microcontroller"
            var row2 = new List<string> { "IC-ATMEGA328P", "8-bit AVR Microcontroller", "Active" };
            Assert.False(engine.MatchesAnyColumn(row2));
        }

        [Fact]
        public void FormatStatusText_FormatsFilteredAndUnfilteredCorrectly()
        {
            var engine = new SearchFilterEngine();

            // Unfiltered
            string s1 = engine.FormatStatusText(50, 50000, 50000);
            Assert.Contains("50", s1);
            Assert.Contains("50,000", s1);

            // Filtered
            engine.SetQuery("STM32");
            string s2 = engine.FormatStatusText(50, 120, 50000);
            Assert.Contains("120", s2);
            Assert.Contains("50,000", s2);
        }
    }
}
