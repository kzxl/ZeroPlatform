using System;
using System.Linq;
using Xunit;
using ZeroUI.Core.Spreadsheet;

namespace ZeroUI.Core.Tests.Spreadsheet
{
    public class SpreadsheetTests
    {
        [Fact]
        public void CellAddress_ParsingAndFormatting_ValidatesCoordinates()
        {
            var a1 = new CellAddress(0, 0);
            Assert.Equal("A1", a1.Name);
            Assert.Equal(0, a1.Row);
            Assert.Equal(0, a1.Column);

            var b2 = new CellAddress(1, 1);
            Assert.Equal("B2", b2.Name);

            var z26 = new CellAddress(25, 25);
            Assert.Equal("Z26", z26.Name);

            var aa1 = new CellAddress(0, 26);
            Assert.Equal("AA1", aa1.Name);

            Assert.True(CellAddress.TryParse("C15", out var parsedC15));
            Assert.Equal(14, parsedC15.Row);
            Assert.Equal(2, parsedC15.Column);

            Assert.True(CellAddress.TryParse("ab100", out var parsedAb100));
            Assert.Equal(99, parsedAb100.Row);
            Assert.Equal(27, parsedAb100.Column); // A=0..Z=25, AA=26, AB=27

            Assert.False(CellAddress.TryParse("INVALID", out _));
            Assert.False(CellAddress.TryParse("A", out _));
            Assert.False(CellAddress.TryParse("123", out _));
        }

        [Fact]
        public void CellRange_ParsingAndContainment_ValidatesRange()
        {
            Assert.True(CellRange.TryParse("B2:D5", out var range));
            Assert.Equal(1, range.Start.Row);
            Assert.Equal(1, range.Start.Column);
            Assert.Equal(4, range.End.Row);
            Assert.Equal(3, range.End.Column);

            Assert.True(range.Contains(new CellAddress(1, 1)));
            Assert.True(range.Contains(new CellAddress(2, 2)));
            Assert.True(range.Contains(new CellAddress(4, 3)));
            Assert.False(range.Contains(new CellAddress(0, 0)));
            Assert.False(range.Contains(new CellAddress(5, 4)));

            var allCells = range.GetAddresses().ToList();
            Assert.Equal(12, allCells.Count);
            Assert.Equal(12, range.TotalCells);
        }

        [Fact]
        public void SpreadsheetWorksheet_SparseStorageAndDimensions_BehavesCorrectly()
        {
            var ws = new SpreadsheetWorksheet("Test Sheet", 20, 10);
            Assert.Null(ws.GetCell(5, 5));

            ws.SetValue(5, 5, "123.45");
            var cell = ws.GetCell(5, 5);
            Assert.NotNull(cell);
            Assert.Equal("123.45", cell!.RawValue);
            Assert.Equal(123.45, cell.EvaluatedValue);

            // Custom dimensions
            ws.SetColumnWidth(5, 140.0);
            Assert.Equal(140.0, ws.GetColumnWidth(5));
            Assert.Equal(80.0, ws.GetColumnWidth(1)); // default

            ws.SetRowHeight(5, 30.0);
            Assert.Equal(30.0, ws.GetRowHeight(5));
            Assert.Equal(22.0, ws.GetRowHeight(1)); // default

            // Clear cell
            ws.ClearCell(5, 5);
            Assert.Null(ws.GetCell(5, 5));
        }

        [Fact]
        public void FormulaEngine_BasicArithmeticAndParentheses_EvaluatesAccurately()
        {
            var ws = new SpreadsheetWorksheet();
            ws.SetValue("A1", "=10 + 5 * 2");
            ws.SetValue("A2", "=(10 + 5) * 2");
            ws.SetValue("A3", "=2 ^ 3");
            ws.SetValue("A4", "=100 / 4 - 5");

            ws.RecalculateAllFormulas();

            Assert.Equal(20.0, ws.GetCell(0, 0)?.EvaluatedValue);
            Assert.Equal(30.0, ws.GetCell(1, 0)?.EvaluatedValue);
            Assert.Equal(8.0, ws.GetCell(2, 0)?.EvaluatedValue);
            Assert.Equal(20.0, ws.GetCell(3, 0)?.EvaluatedValue);
        }

        [Fact]
        public void FormulaEngine_CellReferences_CalculatesDependentValues()
        {
            var ws = new SpreadsheetWorksheet();
            ws.SetValue("A1", "15");
            ws.SetValue("B1", "25");
            ws.SetValue("C1", "=A1 + B1");
            ws.SetValue("D1", "=C1 * 3");

            ws.RecalculateAllFormulas();

            Assert.Equal(15.0, ws.GetCell(0, 0)?.EvaluatedValue);
            Assert.Equal(25.0, ws.GetCell(0, 1)?.EvaluatedValue);
            Assert.Equal(40.0, ws.GetCell(0, 2)?.EvaluatedValue);
            Assert.Equal(120.0, ws.GetCell(0, 3)?.EvaluatedValue);
        }

        [Fact]
        public void FormulaEngine_AggregateFunctions_CalculatesRanges()
        {
            var ws = new SpreadsheetWorksheet();
            ws.SetValue("A1", "10");
            ws.SetValue("A2", "20");
            ws.SetValue("A3", "30");
            ws.SetValue("A4", "40");
            ws.SetValue("A5", "50");

            ws.SetValue("B1", "=SUM(A1:A5)");
            ws.SetValue("B2", "=AVERAGE(A1:A5)");
            ws.SetValue("B3", "=MIN(A1:A5)");
            ws.SetValue("B4", "=MAX(A1:A5)");
            ws.SetValue("B5", "=COUNT(A1:A5)");

            ws.RecalculateAllFormulas();

            Assert.Equal(150.0, ws.GetCell(0, 1)?.EvaluatedValue);
            Assert.Equal(30.0, ws.GetCell(1, 1)?.EvaluatedValue);
            Assert.Equal(10.0, ws.GetCell(2, 1)?.EvaluatedValue);
            Assert.Equal(50.0, ws.GetCell(3, 1)?.EvaluatedValue);
            Assert.Equal(5.0, ws.GetCell(4, 1)?.EvaluatedValue);
        }

        [Fact]
        public void FormulaEngine_IfFunction_EvaluatesConditionals()
        {
            var ws = new SpreadsheetWorksheet();
            ws.SetValue("A1", "85");
            ws.SetValue("B1", "=IF(A1 >= 80, \"PASS\", \"FAIL\")");

            ws.SetValue("A2", "45");
            ws.SetValue("B2", "=IF(A2 >= 80, \"PASS\", \"FAIL\")");

            ws.RecalculateAllFormulas();

            Assert.Equal("PASS", ws.GetCell(0, 1)?.EvaluatedValue);
            Assert.Equal("FAIL", ws.GetCell(1, 1)?.EvaluatedValue);
        }

        [Fact]
        public void FormulaEngine_CircularDependency_DetectsCycleAndSetsError()
        {
            var ws = new SpreadsheetWorksheet();
            ws.SetValue("A1", "=B1 + 1");
            ws.SetValue("B1", "=A1 + 1");

            ws.RecalculateAllFormulas();

            var a1 = ws.GetCell(0, 0);
            var b1 = ws.GetCell(0, 1);

            Assert.NotNull(a1);
            Assert.NotNull(b1);
            Assert.True(a1!.HasError || b1!.HasError);
            Assert.Contains("#CIRCULAR!", a1.Error ?? b1.Error);
        }

        [Fact]
        public void SpreadsheetSampleGenerator_FactoryCostingBomSheet_EvaluatesCorrectTotals()
        {
            var ws = SpreadsheetSampleGenerator.CreateFactoryCostingWorksheet();

            Assert.NotNull(ws);
            Assert.Equal("Plant Costing & BOM", ws.Title);

            // Verify populated cells
            var populated = ws.GetPopulatedCells().ToList();
            Assert.True(populated.Count > 25);

            // Row 8 (0-based) is summary row 9
            // Sum Qty in C9 (row 8, col 2)
            var sumQtyCell = ws.GetCell(8, 2);
            Assert.NotNull(sumQtyCell);
            Assert.False(sumQtyCell!.HasError);
            double totalQty = Convert.ToDouble(sumQtyCell.EvaluatedValue);
            Assert.Equal(45000.0, totalQty);

            // Sum Base Cost in E9 (row 8, col 4)
            var sumBaseCell = ws.GetCell(8, 4);
            Assert.NotNull(sumBaseCell);
            Assert.False(sumBaseCell!.HasError);
            double totalBase = Convert.ToDouble(sumBaseCell.EvaluatedValue);
            Assert.True(totalBase > 15000.0);

            // Average Yield in F9 (row 8, col 5)
            var avgYieldCell = ws.GetCell(8, 5);
            Assert.NotNull(avgYieldCell);
            Assert.False(avgYieldCell!.HasError);
            double avgYield = Convert.ToDouble(avgYieldCell.EvaluatedValue);
            Assert.True(avgYield > 0.90 && avgYield < 1.0);

            // Sum Adjusted Cost in G9 (row 8, col 6)
            var sumAdjCell = ws.GetCell(8, 6);
            Assert.NotNull(sumAdjCell);
            Assert.False(sumAdjCell!.HasError);
            double totalAdj = Convert.ToDouble(sumAdjCell.EvaluatedValue);
            Assert.True(totalAdj > totalBase); // Adjusted cost must exceed base cost due to yield < 1.0
        }

        [Fact]
        public void SpreadsheetSampleGenerator_SixSigmaQcSheet_EvaluatesQualityMetrics()
        {
            var ws = SpreadsheetSampleGenerator.CreateQualitySpcWorksheet();

            Assert.NotNull(ws);
            Assert.Equal("QC Six Sigma Sheet", ws.Title);

            // Check sample row 2 (sample #1: target 12.0, measured 12.002)
            var devCell = ws.GetCell(2, 3);
            Assert.NotNull(devCell);
            Assert.False(devCell!.HasError);

            var passCell = ws.GetCell(2, 4);
            Assert.NotNull(passCell);
            Assert.Equal("PASS", passCell!.EvaluatedValue);

            // Check summary statistics (row 12, col 2: =AVERAGE(C3:C12))
            var avgValCell = ws.GetCell(12, 2);
            Assert.NotNull(avgValCell);
            Assert.False(avgValCell!.HasError);
            double avgVal = Convert.ToDouble(avgValCell.EvaluatedValue);
            Assert.True(avgVal > 11.99 && avgVal < 12.02);
        }
    }
}
