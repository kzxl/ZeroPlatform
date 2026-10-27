using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Data;
using ZeroUI.Core.Editors;

namespace ZeroUI.Core.Tests
{
    public class FormEditorsEnterpriseSuiteTests
    {
        [Fact]
        public void ProcessNewValueEventArgs_QuickCreateFlow_SetsHandledAndValue()
        {
            var args = new ProcessNewValueEventArgs("Electronic Parts");
            Assert.Equal("Electronic Parts", args.DisplayText);
            Assert.False(args.Handled);
            Assert.Null(args.NewValue);

            // User handler resolves new record
            var newRecord = new { Id = 42, Name = "Electronic Parts" };
            args.NewValue = newRecord;
            args.Handled = true;

            Assert.True(args.Handled);
            Assert.NotNull(args.NewValue);
            Assert.Equal(42, ((dynamic)args.NewValue).Id);
        }

        [Fact]
        public void MultiSelectTokens_SplitAndDeduplicate_WorksCorrectly()
        {
            string rawInput = "C#, C++, Rust; Go, C#, Python";
            var parts = rawInput.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var tokens = new List<string>();

            foreach (var p in parts)
            {
                string s = p.Trim();
                if (!string.IsNullOrEmpty(s) && !tokens.Contains(s))
                {
                    tokens.Add(s);
                }
            }

            Assert.Equal(5, tokens.Count);
            Assert.Contains("C#", tokens);
            Assert.Contains("C++", tokens);
            Assert.Contains("Rust", tokens);
            Assert.Contains("Go", tokens);
            Assert.Contains("Python", tokens);
        }

        [Fact]
        public void MultiSelectSummary_Formatting_FollowsThresholdRules()
        {
            var selected = new List<string>();
            string placeholder = "Select items...";
            string format = "{0} items selected";

            // 0 items -> placeholder
            Assert.Equal(placeholder, FormatSummary(selected, placeholder, format));

            // 1-2 items -> joined
            selected.Add("Alpha");
            Assert.Equal("Alpha", FormatSummary(selected, placeholder, format));

            selected.Add("Beta");
            Assert.Equal("Alpha, Beta", FormatSummary(selected, placeholder, format));

            // >2 items -> count
            selected.Add("Gamma");
            Assert.Equal("3 items selected", FormatSummary(selected, placeholder, format));
        }

        private static string FormatSummary(List<string> items, string placeholder, string summaryFormat)
        {
            if (items.Count == 0) return placeholder;
            if (items.Count <= 2) return string.Join(", ", items);
            return string.Format(summaryFormat, items.Count);
        }

        [Fact]
        public void SearchFilterEngine_TokenMatching_HandlesMultipleTerms()
        {
            var engine = new SearchFilterEngine();
            engine.SetQuery("resistor 10k");
            Assert.True(engine.HasFilter);

            var matchingRow = new List<string> { "SMD Resistor", "10k Ohm", "Tape & Reel" };
            var nonMatchingRow = new List<string> { "Capacitor", "10uF", "Ceramic" };

            Assert.True(engine.MatchesAnyColumn(matchingRow));
            Assert.False(engine.MatchesAnyColumn(nonMatchingRow));
        }
    }
}
