using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Virtualization;

namespace ZeroUI.Core.Tests
{
    public class GroupedRowIndexMapTests
    {
        [Fact]
        public void GroupedRowIndexMap_ResetIdentity_PreservesFlatMapping()
        {
            var map = new GroupedRowIndexMap();
            map.ResetIdentity(100);

            Assert.Equal(100, map.ActiveCount);
            Assert.False(map.HasGrouping);

            for (int i = 0; i < 100; i++)
            {
                var entry = map[i];
                Assert.True(entry.IsData);
                Assert.False(entry.IsGroup);
                Assert.Equal(i, entry.ModelRowIndex);
                Assert.Equal(0, entry.Level);
            }
        }

        [Fact]
        public void GroupedRowIndexMap_BuildGroups_SingleLevel_CorrectStructure()
        {
            // 6 rows, Categories: [0]=A, [1]=A, [2]=B, [3]=B, [4]=B, [5]=C
            string[] categories = new[] { "Electronics", "Electronics", "Mechanical", "Mechanical", "Mechanical", "Chemical" };
            var map = new GroupedRowIndexMap();

            map.BuildGroups(6, new[] { 0 }, (row, col) => categories[row]);

            Assert.True(map.HasGrouping);
            Assert.Equal(3, map.RootGroups.Count);

            // Group 0: Electronics (2 items) -> Visual: [Group Header], [Row 0], [Row 1] (3 entries)
            // Group 1: Mechanical (3 items)  -> Visual: [Group Header], [Row 2], [Row 3], [Row 4] (4 entries)
            // Group 2: Chemical (1 item)    -> Visual: [Group Header], [Row 5] (2 entries)
            // Total Active = 3 + 4 + 2 = 9 visual entries
            Assert.Equal(9, map.ActiveCount);

            // Entry 0 is Group 0
            Assert.True(map[0].IsGroup);
            Assert.Equal(0, map[0].GroupId);
            Assert.True(map[0].IsExpanded);

            // Entry 1 & 2 are Data rows 0 and 1
            Assert.True(map[1].IsData);
            Assert.Equal(0, map[1].ModelRowIndex);
            Assert.True(map[2].IsData);
            Assert.Equal(1, map[2].ModelRowIndex);

            // Entry 3 is Group 1
            Assert.True(map[3].IsGroup);
            Assert.Equal(1, map[3].GroupId);

            // Entry 4, 5, 6 are Data rows 2, 3, 4
            Assert.Equal(2, map[4].ModelRowIndex);
            Assert.Equal(3, map[5].ModelRowIndex);
            Assert.Equal(4, map[6].ModelRowIndex);

            // Entry 7 is Group 2
            Assert.True(map[7].IsGroup);
            Assert.Equal(2, map[7].GroupId);
            Assert.Equal(5, map[8].ModelRowIndex);
        }

        [Fact]
        public void GroupedRowIndexMap_ToggleGroup_HidesChildrenCorrectly()
        {
            string[] categories = new[] { "Electronics", "Electronics", "Mechanical", "Mechanical" };
            var map = new GroupedRowIndexMap();

            map.BuildGroups(4, new[] { 0 }, (row, col) => categories[row]);

            // Initial: Group 0 (3 items with header), Group 1 (3 items with header) = 6 total
            Assert.Equal(6, map.ActiveCount);

            // Collapse Group 0 (visual index 0)
            bool toggled = map.ToggleGroup(0);
            Assert.True(toggled);

            // Now Group 0 is collapsed: [Group 0], [Group 1], [Row 2], [Row 3] = 4 entries
            Assert.Equal(4, map.ActiveCount);
            Assert.True(map[0].IsGroup);
            Assert.False(map[0].IsExpanded);

            // Entry 1 is now Group 1
            Assert.True(map[1].IsGroup);
            Assert.Equal(1, map[1].GroupId);

            // Re-expand Group 0
            map.ToggleGroup(0);
            Assert.Equal(6, map.ActiveCount);
            Assert.True(map[0].IsExpanded);
        }

        [Fact]
        public void GroupedRowIndexMap_CollapseAllAndExpandAll()
        {
            string[] categories = new[] { "A", "A", "B", "B", "C" };
            var map = new GroupedRowIndexMap();
            map.BuildGroups(5, new[] { 0 }, (row, col) => categories[row]);

            map.CollapseAll();
            // 3 root groups, all collapsed = 3 visual entries
            Assert.Equal(3, map.ActiveCount);
            Assert.True(map[0].IsGroup && !map[0].IsExpanded);
            Assert.True(map[1].IsGroup && !map[1].IsExpanded);
            Assert.True(map[2].IsGroup && !map[2].IsExpanded);

            map.ExpandAll();
            // 3 headers + 5 data = 8 visual entries
            Assert.Equal(8, map.ActiveCount);
            Assert.True(map[0].IsExpanded);
        }

        [Fact]
        public void GroupedRowIndexMap_MultiLevelGrouping_CorrectNesting()
        {
            // Level 0: Dept ("R&D", "Prod")
            // Level 1: Role ("Dev", "QA")
            string[] depts = new[] { "R&D", "R&D", "Prod" };
            string[] roles = new[] { "Dev", "QA", "Operator" };

            var map = new GroupedRowIndexMap();
            map.BuildGroups(3, new[] { 0, 1 }, (row, col) => col == 0 ? depts[row] : roles[row]);

            Assert.True(map.HasGrouping);
            // Root groups: R&D, Prod
            Assert.Equal(2, map.RootGroups.Count);

            var rdGroup = map.RootGroups[0];
            Assert.Equal(0, rdGroup.Level);
            Assert.Equal(2, rdGroup.SubGroups.Count); // Dev, QA

            var prodGroup = map.RootGroups[1];
            Assert.Single(prodGroup.SubGroups); // Operator
        }
    }
}
