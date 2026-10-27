using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Pivot;

namespace ZeroUI.Core.Tests
{
    public class PivotEngineTests
    {
        private class SaleRecord
        {
            public string Category { get; set; } = string.Empty;
            public string Region { get; set; } = string.Empty;
            public int Year { get; set; }
            public string Quarter { get; set; } = string.Empty;
            public double Amount { get; set; }

            public SaleRecord(string cat, string reg, int yr, string qtr, double amt)
            {
                Category = cat;
                Region = reg;
                Year = yr;
                Quarter = qtr;
                Amount = amt;
            }
        }

        [Fact]
        public void BasicCrossTab_CalculatesSumAndTotalsCorrectly()
        {
            var data = new List<SaleRecord>
            {
                new SaleRecord("Electronics", "North", 2024, "Q1", 100),
                new SaleRecord("Electronics", "North", 2024, "Q2", 150),
                new SaleRecord("Electronics", "South", 2024, "Q1", 200),
                new SaleRecord("Furniture", "North", 2024, "Q1", 300),
                new SaleRecord("Furniture", "South", 2024, "Q2", 400)
            };

            var engine = new PivotEngine { DataSource = data };
            engine.AddField("Category", PivotArea.RowArea);
            engine.AddField("Region", PivotArea.ColumnArea);
            engine.AddField("Amount", PivotArea.DataArea, "Total Sales", PivotSummaryType.Sum);

            var model = engine.Calculate();

            Assert.Equal(2, model.RowCount); // Electronics, Furniture
            Assert.Equal(2, model.ColumnCount); // North, South

            // Row 0: Electronics
            // Col 0: North, Col 1: South
            Assert.Equal(250.0, (double)model.GetCellValue(0, 0)!); // Electronics North = 100 + 150
            Assert.Equal(200.0, (double)model.GetCellValue(0, 1)!); // Electronics South = 200
            Assert.Equal(450.0, (double)model.GetRowTotal(0)!); // Electronics Total = 450

            // Row 1: Furniture
            Assert.Equal(300.0, (double)model.GetCellValue(1, 0)!); // Furniture North = 300
            Assert.Equal(400.0, (double)model.GetCellValue(1, 1)!); // Furniture South = 400
            Assert.Equal(700.0, (double)model.GetRowTotal(1)!); // Furniture Total = 700

            // Column Totals
            Assert.Equal(550.0, (double)model.GetColumnTotal(0)!); // North Total = 250 + 300 = 550
            Assert.Equal(600.0, (double)model.GetColumnTotal(1)!); // South Total = 200 + 400 = 600

            // Grand Total
            Assert.Equal(1150.0, (double)model.GetGrandTotal()!);
        }

        [Fact]
        public void SummaryTypes_Count_Average_Min_Max_CalculateAccurately()
        {
            var data = new List<SaleRecord>
            {
                new SaleRecord("Hardware", "East", 2024, "Q1", 20),
                new SaleRecord("Hardware", "East", 2024, "Q2", 40),
                new SaleRecord("Hardware", "East", 2024, "Q3", 60)
            };

            var engine = new PivotEngine { DataSource = data };
            engine.AddField("Category", PivotArea.RowArea);
            engine.AddField("Region", PivotArea.ColumnArea);
            engine.AddField("Amount", PivotArea.DataArea, "Count", PivotSummaryType.Count);
            engine.AddField("Amount", PivotArea.DataArea, "Avg", PivotSummaryType.Average);
            engine.AddField("Amount", PivotArea.DataArea, "Min", PivotSummaryType.Min);
            engine.AddField("Amount", PivotArea.DataArea, "Max", PivotSummaryType.Max);

            var model = engine.Calculate();

            Assert.Equal(3, (int)model.GetCellValue(0, 0, 0)!); // Count = 3
            Assert.Equal(40.0, (double)model.GetCellValue(0, 0, 1)!); // Avg = (20+40+60)/3 = 40
            Assert.Equal(20.0, (double)model.GetCellValue(0, 0, 2)!); // Min = 20
            Assert.Equal(60.0, (double)model.GetCellValue(0, 0, 3)!); // Max = 60
        }

        [Fact]
        public void MultiLevelHierarchy_GeneratesCompositeKeys()
        {
            var data = new List<SaleRecord>
            {
                new SaleRecord("A", "N", 2023, "Q1", 10),
                new SaleRecord("A", "N", 2023, "Q2", 20),
                new SaleRecord("A", "N", 2024, "Q1", 30)
            };

            var engine = new PivotEngine { DataSource = data };
            engine.AddField("Year", PivotArea.RowArea, "Year", PivotSummaryType.Sum);
            engine.AddField("Quarter", PivotArea.RowArea, "Quarter", PivotSummaryType.Sum);
            engine.AddField("Amount", PivotArea.DataArea, "Total", PivotSummaryType.Sum);

            var model = engine.Calculate();

            Assert.Equal(3, model.RowCount); // (2023, Q1), (2023, Q2), (2024, Q1)
            Assert.Equal("2023 | Q1", model.RowKeys[0].ToString());
            Assert.Equal("2023 | Q2", model.RowKeys[1].ToString());
            Assert.Equal("2024 | Q1", model.RowKeys[2].ToString());
        }

        [Fact]
        public void EmptyDataSource_ReturnsEmptyMatrixSafely()
        {
            var engine = new PivotEngine { DataSource = null };
            engine.AddField("Category", PivotArea.RowArea);
            engine.AddField("Amount", PivotArea.DataArea);

            var model = engine.Calculate();

            Assert.Equal(0, model.RowCount);
            Assert.Equal(0, model.ColumnCount);
            Assert.Null(model.GetCellValue(0, 0));
            Assert.Null(model.GetGrandTotal());
        }

        [Fact]
        public void Sorting_DescendingOrder_ReversesKeyOrder()
        {
            var data = new List<SaleRecord>
            {
                new SaleRecord("Alpha", "X", 2024, "Q1", 10),
                new SaleRecord("Beta", "X", 2024, "Q1", 20),
                new SaleRecord("Gamma", "X", 2024, "Q1", 30)
            };

            var engine = new PivotEngine { DataSource = data };
            var catField = engine.AddField("Category", PivotArea.RowArea);
            catField.SortOrder = PivotSortOrder.Descending;
            engine.AddField("Amount", PivotArea.DataArea);

            var model = engine.Calculate();

            Assert.Equal("Gamma", model.RowKeys[0][0]);
            Assert.Equal("Beta", model.RowKeys[1][0]);
            Assert.Equal("Alpha", model.RowKeys[2][0]);
        }
    }
}
