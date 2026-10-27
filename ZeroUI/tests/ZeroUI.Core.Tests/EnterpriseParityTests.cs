using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.Core.Layout;

namespace ZeroUI.Core.Tests
{
    public class EnterpriseParityTests
    {
        [Fact]
        public void FilterCriteria_Generates_Accurate_Sql_Where_Clause()
        {
            var root = new GroupFilterNode(FilterGroupOperator.And);
            root.AddCondition("Status", FilterComparisonOperator.Equals, "Active");
            root.AddCondition("Quantity", FilterComparisonOperator.GreaterThan, "100");

            var orGroup = root.AddGroup(FilterGroupOperator.Or);
            orGroup.AddCondition("Category", FilterComparisonOperator.Equals, "Electronics");
            orGroup.AddCondition("Category", FilterComparisonOperator.Equals, "Hardware");

            string sql = root.ToSqlWhere();
            Assert.Equal("([Status] = 'Active' AND [Quantity] > '100' AND ([Category] = 'Electronics' OR [Category] = 'Hardware'))", sql);

            string display = root.ToDisplayString();
            Assert.Contains("[Status] Equals 'Active'", display);
            Assert.Contains("[Category] Equals 'Electronics'", display);
        }

        [Fact]
        public void WorkspaceSerializer_Captures_And_Applies_Grid_Column_Layout()
        {
            var cols = new List<ZeroColumn>
            {
                new ZeroColumn { FieldName = "Id", HeaderText = "ID", Width = 80, IsVisible = true },
                new ZeroColumn { FieldName = "Name", HeaderText = "Full Name", Width = 150, IsVisible = true, SortOrder = SortDirection.Ascending },
                new ZeroColumn { FieldName = "Secret", HeaderText = "Secret Key", Width = 100, IsVisible = false }
            };

            var state = ZeroWorkspaceSerializer.CaptureGrid(cols);
            Assert.Equal(3, state.GridColumns.Count);
            Assert.Equal("Id", state.GridColumns[0].FieldName);
            Assert.Equal(80, state.GridColumns[0].Width);

            string json = ZeroWorkspaceSerializer.Serialize(state);
            Assert.Contains("\"FieldName\": \"Name\"", json);
            Assert.Contains("\"SortOrder\": 1", json);

            // Mutate original columns
            cols[0].Width = 200;
            cols[1].SortOrder = SortDirection.None;

            // Apply back
            ZeroWorkspaceSerializer.ApplyToGrid(cols, state);
            Assert.Equal(80, cols[0].Width);
            Assert.Equal(SortDirection.Ascending, cols[1].SortOrder);
        }
    }
}
