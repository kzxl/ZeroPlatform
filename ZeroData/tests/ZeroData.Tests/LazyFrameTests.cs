using System;
using System.Linq;
using Xunit;
using ZeroData.Core;

namespace ZeroData.Tests
{
    public class LazyFrameTests
    {
        [Fact]
        public void LazyFrame_SimpleFilterAndCollect_ProducesCorrectResults()
        {
            var df = new DataFrame();
            df.AddColumn(new DataColumn<int>("Id", new[] { 1, 2, 3, 4, 5 }));
            df.AddColumn(new DataColumn<string>("Category", new[] { "CNC", "Laser", "CNC", "Robot", "Laser" }));
            df.AddColumn(new DataColumn<double>("Score", new[] { 90.5, 75.0, 88.0, 92.5, 60.0 }));

            var result = df.Lazy()
                           .Where<double>("Score", s => s >= 88.0)
                           .Collect();

            Assert.Equal(3, result.RowCount);
            Assert.Equal(new[] { 1, 3, 4 }, result.Column<int>("Id").AsReadOnlySpan().ToArray());
        }

        [Fact]
        public void LazyFrame_ProjectionPushdown_PrunesUnusedColumnsEarly()
        {
            var df = new DataFrame();
            df.AddColumn(new DataColumn<int>("Id", new[] { 10, 20, 30 }));
            df.AddColumn(new DataColumn<string>("ColA", new[] { "A1", "A2", "A3" }));
            df.AddColumn(new DataColumn<string>("ColB", new[] { "B1", "B2", "B3" }));
            df.AddColumn(new DataColumn<string>("ColC", new[] { "C1", "C2", "C3" }));

            var result = df.Lazy()
                           .Select("Id", "ColB")
                           .Collect();

            Assert.Equal(2, result.ColumnCount);
            Assert.True(result.HasColumn("Id"));
            Assert.True(result.HasColumn("ColB"));
            Assert.False(result.HasColumn("ColA"));
            Assert.False(result.HasColumn("ColC"));
            Assert.Equal(3, result.RowCount);
        }

        [Fact]
        public void LazyFrame_FilterWithCategoricalColumn_WorksSeamlessly()
        {
            var df = new DataFrame();
            df.AddColumn(new DataColumn<int>("Id", new[] { 1, 2, 3, 4, 5 }));
            df.AddColumn(new DataColumn<string>("Dept", new[] { "IT", "HR", "IT", "Finance", "IT" }));
            df.Categorize("Dept");

            var result = df.Lazy()
                           .Where<string>("Dept", d => d == "IT")
                           .Select("Id", "Dept")
                           .Collect();

            Assert.Equal(3, result.RowCount);
            var idCol = result.Column<int>("Id");
            Assert.Equal(1, idCol[0]);
            Assert.Equal(3, idCol[1]);
            Assert.Equal(5, idCol[2]);
        }

        [Fact]
        public void LazyFrame_SortingAndSlicing_EvaluatedInOrder()
        {
            var df = new DataFrame();
            df.AddColumn(new DataColumn<int>("Id", new[] { 1, 2, 3, 4, 5 }));
            df.AddColumn(new DataColumn<int>("Val", new[] { 50, 10, 40, 20, 30 }));

            var result = df.Lazy()
                           .OrderByDescending<int>("Val")
                           .Slice(1, 3) // Skip top 1 (50), take next 3: 40, 30, 20
                           .Collect();

            Assert.Equal(3, result.RowCount);
            var valCol = result.Column<int>("Val");
            Assert.Equal(40, valCol[0]);
            Assert.Equal(30, valCol[1]);
            Assert.Equal(20, valCol[2]);
        }

        [Fact]
        public void LazyFrame_ExplainPlan_OutputsHumanReadableDAG()
        {
            var df = new DataFrame();
            df.AddColumn(new DataColumn<int>("Id", new[] { 1, 2 }));
            df.AddColumn(new DataColumn<string>("Name", new[] { "Alpha", "Beta" }));

            var lazy = df.Lazy()
                         .Where<int>("Id", id => id > 1)
                         .Select("Name");

            var plan = lazy.ExplainPlan();

            Assert.Contains("Filter", plan);
            Assert.Contains("Project", plan);
            Assert.Contains("DataFrame", plan);
        }
    }
}
