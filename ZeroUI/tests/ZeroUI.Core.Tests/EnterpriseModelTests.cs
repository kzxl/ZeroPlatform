using System;
using System.ComponentModel;
using Xunit;
using ZeroUI.Core.Data;

namespace ZeroUI.Core.Tests
{
    public class EnterpriseModelTests
    {
        [Fact]
        public void CellRange_ComputesBoundsAndHitTestCorrectly()
        {
            var range = new CellRange(5, 4, 2, 1); // Reversed start/end

            Assert.Equal(2, range.TopRow);
            Assert.Equal(5, range.BottomRow);
            Assert.Equal(1, range.LeftColumn);
            Assert.Equal(4, range.RightColumn);
            Assert.Equal(4, range.RowCount);
            Assert.Equal(4, range.ColumnCount);

            Assert.True(range.Contains(3, 2));
            Assert.True(range.Contains(2, 1));
            Assert.True(range.Contains(5, 4));
            Assert.False(range.Contains(1, 1));
            Assert.False(range.Contains(6, 2));
        }

        [Fact]
        public void TreeModel_FlatteningAndExpandCollapse_WorksCorrectly()
        {
            var model = new TreeModel();
            var root1 = model.AddRoot("Root 1", "Data 1");
            var child1_1 = root1.AddChild("Child 1.1", "Data 1.1");
            var child1_2 = root1.AddChild("Child 1.2", "Data 1.2");
            var subChild = child1_1.AddChild("SubChild 1.1.1", "Data 1.1.1");
            var root2 = model.AddRoot("Root 2", "Data 2");

            // All expanded by default: root1, child1_1, subChild, child1_2, root2 (5 total)
            Assert.Equal(5, model.VisibleNodeCount);
            Assert.Equal("Root 1", model.GetVisibleNode(0).GetValue(0));
            Assert.Equal("Child 1.1", model.GetVisibleNode(1).GetValue(0));
            Assert.Equal("SubChild 1.1.1", model.GetVisibleNode(2).GetValue(0));
            Assert.Equal("Child 1.2", model.GetVisibleNode(3).GetValue(0));
            Assert.Equal("Root 2", model.GetVisibleNode(4).GetValue(0));

            // Collapse child1_1: hides subChild -> 4 visible nodes
            model.ToggleExpand(child1_1);
            Assert.Equal(4, model.VisibleNodeCount);
            Assert.Equal("Child 1.2", model.GetVisibleNode(2).GetValue(0));

            // CollapseAll: only root1 and root2 visible -> 2 nodes
            model.CollapseAll();
            Assert.Equal(2, model.VisibleNodeCount);
            Assert.Equal("Root 1", model.GetVisibleNode(0).GetValue(0));
            Assert.Equal("Root 2", model.GetVisibleNode(1).GetValue(0));

            // ExpandAll: back to 5
            model.ExpandAll();
            Assert.Equal(5, model.VisibleNodeCount);
        }

        [Fact]
        [Obsolete]
        public void ZeroTreeModel_BackwardCompatibility_WorksCorrectly()
        {
            var model = new ZeroTreeModel();
            var root = model.AddRoot(new ZeroTreeNode("Legacy Root"));
            Assert.Equal(1, model.VisibleNodeCount);
            Assert.Equal("Legacy Root", model.GetVisibleNode(0).GetValue(0));
        }

        private class SampleTestDevice
        {
            [Category("Hardware")]
            [DisplayName("Device Name")]
            [Description("The unique network name of this equipment.")]
            public string DeviceName { get; set; } = "PLC-Station-01";

            [Category("Hardware")]
            [DisplayName("IP Address")]
            public string IpAddress { get; set; } = "192.168.1.100";

            [Category("Operational")]
            [DisplayName("Target RPM")]
            public double TargetRpm { get; set; } = 1750.0;

            [Category("Operational")]
            [DisplayName("Is Online")]
            public bool IsOnline { get; set; } = true;
        }

        [Fact]
        public void ZeroPropertyModel_ReflectionAndCategorization_WorksAccurately()
        {
            var device = new SampleTestDevice();
            var model = new ZeroPropertyModel();
            model.SetSelectedObject(device);

            // Expect 2 categories: Hardware and Operational
            Assert.Equal(2, model.Categories.Count);

            var hwCat = model.Categories[0].Name == "Hardware" ? model.Categories[0] : model.Categories[1];
            Assert.Equal(2, hwCat.Items.Count);

            // Verify property binding & reflection updates
            bool eventFired = false;
            model.PropertyValueChanged += (s, e) =>
            {
                eventFired = true;
                Assert.Equal(2400.0, e.NewValue);
            };

            var rpmItem = model.Items[2].Name == "TargetRpm" ? model.Items[2] : model.Items[3];
            rpmItem.Value = 2400.0;

            Assert.True(eventFired);
            Assert.Equal(2400.0, device.TargetRpm);

            // Test Search Filter
            model.SearchFilter = "IP";
            Assert.Single(model.Categories);
            Assert.Equal("Hardware", model.Categories[0].Name);
            Assert.Single(model.Categories[0].Items);
            Assert.Equal("IP Address", model.Categories[0].Items[0].DisplayName);
        }
    }
}
