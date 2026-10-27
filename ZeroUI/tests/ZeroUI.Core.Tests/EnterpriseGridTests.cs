using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Data;
using ZeroUI.Core.Virtualization;

namespace ZeroUI.Core.Tests
{
    public class EnterpriseGridTests
    {
        [Fact]
        public void GroupSummary_CalculateSummaries_ComputesAggregationsAccurately()
        {
            var map = new GroupedRowIndexMap();

            // Mock 10 rows: col 0 = Category ("A" or "B"), col 1 = Price (10, 20, 30...)
            string[] categories = { "A", "A", "B", "A", "B", "B", "A", "B", "A", "B" };
            double[] prices = { 10, 20, 100, 30, 200, 300, 40, 400, 50, 500 };

            map.BuildGroups(10, new[] { 0 }, (row, col) => categories[row]);

            Assert.True(map.HasGrouping);
            Assert.Equal(2, map.RootGroups.Count);

            var summaryItems = new List<GroupSummaryItem>
            {
                new GroupSummaryItem(1, GroupSummaryType.Count, prefix: "Count"),
                new GroupSummaryItem(1, GroupSummaryType.Sum, formatString: "{0:F0}", prefix: "Total"),
                new GroupSummaryItem(1, GroupSummaryType.Average, formatString: "{0:F1}", prefix: "Avg")
            };

            map.CalculateSummaries(summaryItems, (row, col) => prices[row]);

            var groupA = map.RootGroups[0];
            Assert.Equal("A", groupA.GroupKey);
            Assert.Equal(5, groupA.TotalDataRowCount);
            Assert.NotNull(groupA.Summaries);
            Assert.Equal(150.0, groupA.GetSummary(1, GroupSummaryType.Sum));
            Assert.Equal(5.0, groupA.GetSummary(1, GroupSummaryType.Count));
            Assert.Equal(30.0, groupA.GetSummary(1, GroupSummaryType.Average));
            Assert.Contains("Count: 5", groupA.FormattedSummaryText);
            Assert.Contains("Total: 150", groupA.FormattedSummaryText);
            Assert.Contains("Avg: 30.0", groupA.FormattedSummaryText);

            var groupB = map.RootGroups[1];
            Assert.Equal("B", groupB.GroupKey);
            Assert.Equal(5, groupB.TotalDataRowCount);
            Assert.Equal(1500.0, groupB.GetSummary(1, GroupSummaryType.Sum));
            Assert.Contains("Total: 1500", groupB.FormattedSummaryText);
        }

        [Fact]
        public void ConditionalFormattingRule_EvaluateNumeric_ThresholdsAndColorScale()
        {
            var highlightRule = new ConditionalFormattingRule(1, ConditionOperator.GreaterThan, 100.0, 0xFFFF0000, 0xFFFFFFFF);
            
            bool matchedHigh = highlightRule.EvaluateNumeric(150.0, out uint backColor, out uint textColor);
            Assert.True(matchedHigh);
            Assert.Equal(0xFFFF0000, backColor);
            Assert.Equal(0xFFFFFFFF, textColor);

            bool matchedLow = highlightRule.EvaluateNumeric(80.0, out _, out _);
            Assert.False(matchedLow);

            var scaleRule = new ConditionalFormattingRule
            {
                ColumnIndex = 2,
                RuleType = ConditionalRuleType.ColorScale,
                MinScaleValue = 0,
                MaxScaleValue = 100,
                MinColor = 0xFF00FF00, // Green
                MaxColor = 0xFFFF0000  // Red
            };

            bool scaleResult = scaleRule.EvaluateNumeric(50.0, out uint scaleBack, out _);
            Assert.True(scaleResult);
            // Midpoint between Green (0x00FF00) and Red (0xFF0000) should have both R and G components
            byte r = (byte)((scaleBack >> 16) & 0xFF);
            byte g = (byte)((scaleBack >> 8) & 0xFF);
            Assert.InRange(r, 100, 155);
            Assert.InRange(g, 100, 155);
        }

        [Fact]
        public void PivotDataEngine_Compute_GeneratesAccurateCrossTabAndTotals()
        {
            var engine = new PivotDataEngine();
            engine.RowDimensions.Add(new PivotDimension(0, "Region"));
            engine.ColumnDimensions.Add(new PivotDimension(1, "Quarter"));
            engine.Measures.Add(new PivotMeasure(2, "Sales", GroupSummaryType.Sum));

            // Data: 4 rows: (North, Q1, 100), (North, Q2, 200), (South, Q1, 300), (South, Q2, 400)
            string[,] data = {
                { "North", "Q1", "100" },
                { "North", "Q2", "200" },
                { "South", "Q1", "300" },
                { "South", "Q2", "400" }
            };

            engine.Compute(4, (r, c) => data[r, c], (r, c) => double.Parse(data[r, c]));

            Assert.Equal(2, engine.RowKeys.Count); // North, South
            Assert.Equal(2, engine.ColumnKeys.Count); // Q1, Q2
            Assert.Equal(100.0, engine.Cells[0, 0]); // North Q1
            Assert.Equal(200.0, engine.Cells[0, 1]); // North Q2
            Assert.Equal(300.0, engine.Cells[1, 0]); // South Q1
            Assert.Equal(400.0, engine.Cells[1, 1]); // South Q2

            Assert.Equal(300.0, engine.RowGrandTotals[0]); // North Total
            Assert.Equal(700.0, engine.RowGrandTotals[1]); // South Total
            Assert.Equal(400.0, engine.ColumnGrandTotals[0]); // Q1 Total
            Assert.Equal(600.0, engine.ColumnGrandTotals[1]); // Q2 Total
            Assert.Equal(1000.0, engine.GrandTotal);
        }

        [Fact]
        public void GanttTaskItem_PropertiesAndDuration_CalculateCorrectly()
        {
            var start = new DateTime(2026, 9, 1, 8, 0, 0);
            var end = new DateTime(2026, 9, 5, 17, 0, 0);
            var task = new GanttTaskItem(101, "Assembly Line Calibration", start, end, 0.45f, false, "Engineer A");
            task.PredecessorIds.Add(100);

            Assert.Equal(101, task.Id);
            Assert.Equal("Assembly Line Calibration", task.Name);
            Assert.Equal(0.45f, task.Progress);
            Assert.Single(task.PredecessorIds);
            Assert.Equal(100, task.PredecessorIds[0]);
            Assert.True(task.Duration.TotalDays > 4.0);
        }
    }
}
