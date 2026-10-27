using System;
using System.Diagnostics;
using Xunit;
using ZeroUI.Core.Virtualization;

namespace ZeroUI.Core.Tests
{
    public class VirtualizationStressAndEdgeTests
    {
        [Fact]
        public void GroupedRowIndexMap_ZeroRows_ShouldBeEmptyAndNotThrow()
        {
            var map = new GroupedRowIndexMap();
            map.BuildGroups(0, new[] { 0 }, (r, c) => "Any");

            Assert.Equal(0, map.ActiveCount);
            Assert.False(map.HasGrouping);
            Assert.Empty(map.RootGroups);

            map.ExpandAll();
            map.CollapseAll();
            Assert.Equal(0, map.ActiveCount);
        }

        [Fact]
        public void GroupedRowIndexMap_StressTest_100000Rows_SingleGroup_ShouldPerformFast()
        {
            const int totalRows = 100000;
            var map = new GroupedRowIndexMap();

            var sw = Stopwatch.StartNew();
            map.BuildGroups(totalRows, new[] { 0 }, (r, c) => "UniformCategory");
            sw.Stop();

            Assert.True(map.HasGrouping);
            Assert.Single(map.RootGroups);
            Assert.Equal(totalRows + 1, map.ActiveCount); // 1 header + 100,000 items
            Assert.True(sw.ElapsedMilliseconds < 3000, $"Grouping 100k rows took {sw.ElapsedMilliseconds}ms, expected < 3000ms");

            // Collapse group -> should only have 1 active visual row (the header)
            map.CollapseAll();
            Assert.Equal(1, map.ActiveCount);
            Assert.True(map[0].IsGroup);
            Assert.False(map[0].IsExpanded);

            // Expand again
            map.ExpandAll();
            Assert.Equal(totalRows + 1, map.ActiveCount);
        }

        [Fact]
        public void GroupedRowIndexMap_MultiLevelGrouping_ShouldNestProperly()
        {
            // 8 rows, Level 0 (Dept): HR, IT, Level 1 (Team): Alpha, Beta
            var depts = new[] { "HR", "HR", "IT", "IT", "IT", "IT", "HR", "IT" };
            var teams = new[] { "Alpha", "Beta", "Alpha", "Beta", "Beta", "Alpha", "Alpha", "Beta" };

            var map = new GroupedRowIndexMap();
            map.BuildGroups(8, new[] { 0, 1 }, (r, c) => (c == 0) ? depts[r] : teams[r]);

            Assert.True(map.HasGrouping);
            Assert.Equal(2, map.RootGroups.Count); // HR and IT

            // Check sub-groups
            var hrGroup = map.RootGroups[0];
            Assert.Equal("HR", hrGroup.GroupKey);
            Assert.Equal(2, hrGroup.SubGroups.Count); // Alpha and Beta

            var itGroup = map.RootGroups[1];
            Assert.Equal("IT", itGroup.GroupKey);
            Assert.Equal(2, itGroup.SubGroups.Count); // Alpha and Beta
        }

        [Fact]
        public void RowIndexMap_EdgeCases_ZeroAndOneCapacity_ShouldResizeCleanly()
        {
            var map = new RowIndexMap(0);
            Assert.Equal(0, map.ActiveCount);

            map.ResetIdentity(1);
            Assert.Equal(1, map.ActiveCount);
            Assert.Equal(0, map[0]);

            map.ResetIdentity(5000);
            Assert.Equal(5000, map.ActiveCount);
            Assert.Equal(4999, map[4999]);

            // Filter out all odd numbers
            map.Filter(row => (row % 2 == 0), 5000);
            Assert.Equal(2500, map.ActiveCount);
            Assert.Equal(0, map[0]);
            Assert.Equal(2, map[1]);
            Assert.Equal(4998, map[2499]);
        }
    }
}
