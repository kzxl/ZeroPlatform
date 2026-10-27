using System;
using Xunit;
using ZeroDocuments.Excel.Models;

namespace ZeroDocuments.Tests
{
    public class ExcelCellAddressTests
    {
        [Theory]
        [InlineData("A", 1)]
        [InlineData("B", 2)]
        [InlineData("Z", 26)]
        [InlineData("AA", 27)]
        [InlineData("AZ", 52)]
        [InlineData("BA", 53)]
        [InlineData("ZZ", 702)]
        [InlineData("AAA", 703)]
        public void ColumnNameToIndex_ShouldMapCorrectly(string colName, int expectedIndex)
        {
            int index = ExcelCellAddress.ColumnNameToIndex(colName);
            Assert.Equal(expectedIndex, index);
        }

        [Theory]
        [InlineData(1, "A")]
        [InlineData(2, "B")]
        [InlineData(26, "Z")]
        [InlineData(27, "AA")]
        [InlineData(52, "AZ")]
        [InlineData(53, "BA")]
        [InlineData(702, "ZZ")]
        [InlineData(703, "AAA")]
        public void IndexToColumnName_ShouldMapCorrectly(int index, string expectedName)
        {
            string name = ExcelCellAddress.IndexToColumnName(index);
            Assert.Equal(expectedName, name);
        }

        [Fact]
        public void ParseCellRange_ValidRange_ShouldExtractCoordinates()
        {
            ExcelCellAddress.ParseCellRange("B5:F20", out string startCol, out int startRow, out string endCol, out int endRow);

            Assert.Equal("B", startCol);
            Assert.Equal(5, startRow);
            Assert.Equal("F", endCol);
            Assert.Equal(20, endRow);
        }

        [Fact]
        public void ConvertHeaderRangeToDataRange_ShouldShiftDownAndExpand()
        {
            string dataRange = ExcelCellAddress.ConvertHeaderRangeToDataRange("D24:T24", maxRows: 5000);
            Assert.Equal("D25:T5024", dataRange);
        }

        [Fact]
        public void TryParseCellReference_ValidCells_ShouldSucceed()
        {
            Assert.True(ExcelCellAddress.TryParseCellReference("AA105", out string col, out int row));
            Assert.Equal("AA", col);
            Assert.Equal(105, row);
        }
    }
}
