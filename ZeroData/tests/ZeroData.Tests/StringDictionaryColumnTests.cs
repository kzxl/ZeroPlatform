using System;
using System.Linq;
using Xunit;
using ZeroData.Core;

namespace ZeroData.Tests
{
    public class StringDictionaryColumnTests
    {
        [Fact]
        public void StringDictionaryColumn_BasicCreation_WorksCorrectly()
        {
            var rawData = new[] { "CNC", "Laser", "CNC", "Robot", "Laser", "CNC" };
            var col = new StringDictionaryColumn("MachineType", rawData);

            Assert.Equal("MachineType", col.Name);
            Assert.Equal(6, col.Length);
            Assert.Equal(typeof(string), col.DataType);
            Assert.Equal(3, col.Cardinality); // "CNC", "Laser", "Robot"

            Assert.Equal("CNC", col[0]);
            Assert.Equal("Laser", col[1]);
            Assert.Equal("CNC", col[2]);
            Assert.Equal("Robot", col[3]);
            Assert.Equal("Laser", col[4]);
            Assert.Equal("CNC", col[5]);
        }

        [Fact]
        public void StringDictionaryColumn_FindMatchingRowIndices_UsesFastIntegerCodes()
        {
            var rawData = new[] { "Apple", "Banana", "Orange", "Apple", "Banana", "Apple" };
            var col = new StringDictionaryColumn("Fruit", rawData);

            var appleIndices = col.FindMatchingRowIndices("Apple");
            Assert.Equal(new[] { 0, 3, 5 }, appleIndices);

            var bananaIndices = col.FindMatchingRowIndices("Banana");
            Assert.Equal(new[] { 1, 4 }, bananaIndices);

            var nonExistent = col.FindMatchingRowIndices("Grape");
            Assert.Empty(nonExistent);
        }

        [Fact]
        public void StringDictionaryColumn_SliceAndClone_WorkCorrectly()
        {
            var rawData = new[] { "A", "B", "C", "D", "E" };
            var col = new StringDictionaryColumn("Letters", rawData);

            var sliced = (StringDictionaryColumn)col.Slice(1, 3);
            Assert.Equal(3, sliced.Length);
            Assert.Equal("B", sliced[0]);
            Assert.Equal("C", sliced[1]);
            Assert.Equal("D", sliced[2]);

            var cloned = (StringDictionaryColumn)col.Clone();
            Assert.Equal(col.Length, cloned.Length);
            Assert.Equal(col[0], cloned[0]);
            Assert.Equal(col[4], cloned[4]);
        }

        [Fact]
        public void StringDictionaryColumn_FilterWithIndices_WorksCorrectly()
        {
            var rawData = new[] { "Alpha", "Beta", "Gamma", "Delta" };
            var col = new StringDictionaryColumn("Codes", rawData);

            var filtered = (StringDictionaryColumn)col.Filter(new[] { 0, 2 });
            Assert.Equal(2, filtered.Length);
            Assert.Equal("Alpha", filtered[0]);
            Assert.Equal("Gamma", filtered[1]);
        }

        [Fact]
        public void DataFrame_Categorize_ConvertsDataColumnToStringDictionaryColumn()
        {
            var df = new DataFrame();
            df.AddColumn(new DataColumn<int>("Id", new[] { 1, 2, 3, 4 }));
            df.AddColumn(new DataColumn<string>("Status", new[] { "Active", "Pending", "Active", "Active" }));

            Assert.IsType<DataColumn<string>>(df["Status"]);

            df.Categorize("Status");

            var catCol = df["Status"] as StringDictionaryColumn;
            Assert.NotNull(catCol);
            Assert.Equal(2, catCol.Cardinality);
            Assert.Equal("Active", catCol[0]);
            Assert.Equal("Pending", catCol[1]);
            Assert.Equal("Active", catCol[2]);
            Assert.Equal("Active", catCol[3]);
        }

        [Fact]
        public void DataFrame_CategorizeAllStrings_OnlyConvertsHighRepetitionColumns()
        {
            var df = new DataFrame();
            // High repetition column (cardinality 2 / 10 = 0.2 < default threshold 0.5)
            df.AddColumn(new DataColumn<string>("Category", new[] { "A", "B", "A", "B", "A", "B", "A", "B", "A", "B" }));
            // Low repetition unique column (cardinality 10 / 10 = 1.0 > 0.5)
            df.AddColumn(new DataColumn<string>("UniqueCode", Enumerable.Range(1, 10).Select(i => "CODE_" + i).ToArray()));

            int convertedCount = df.CategorizeAllStrings(maxUniqueRatio: 0.5);

            Assert.Equal(1, convertedCount);
            Assert.IsType<StringDictionaryColumn>(df["Category"]);
            Assert.IsType<DataColumn<string>>(df["UniqueCode"]);
        }
    }
}
