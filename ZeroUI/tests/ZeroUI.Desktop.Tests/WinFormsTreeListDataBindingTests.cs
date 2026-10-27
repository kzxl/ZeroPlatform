using System;
using System.Collections.Generic;
using System.Data;
using Xunit;
using ZeroUI.WinForms.Data;

namespace ZeroUI.Desktop.Tests
{
    public class WinFormsTreeListDataBindingTests
    {
        public class TaskItem
        {
            public int TaskId { get; set; }
            public int? ParentId { get; set; }
            public string TaskName { get; set; } = string.Empty;
            public decimal EstimatedHours { get; set; }
            public decimal Cost { get; set; }
        }

        private static List<TaskItem> CreateSampleTasks()
        {
            return new List<TaskItem>
            {
                new TaskItem { TaskId = 1, ParentId = null, TaskName = "Project Alpha", EstimatedHours = 100, Cost = 5000 },
                new TaskItem { TaskId = 2, ParentId = 1, TaskName = "Phase 1 - Design", EstimatedHours = 30, Cost = 1500 },
                new TaskItem { TaskId = 3, ParentId = 1, TaskName = "Phase 2 - Execution", EstimatedHours = 70, Cost = 3500 },
                new TaskItem { TaskId = 4, ParentId = 2, TaskName = "UI/UX Wireframing", EstimatedHours = 12, Cost = 600 },
                new TaskItem { TaskId = 5, ParentId = 2, TaskName = "Database Modeling", EstimatedHours = 18, Cost = 900 },
                new TaskItem { TaskId = 6, ParentId = 3, TaskName = "Backend Services", EstimatedHours = 40, Cost = 2000 },
                new TaskItem { TaskId = 7, ParentId = 3, TaskName = "Frontend Controls", EstimatedHours = 30, Cost = 1500 }
            };
        }

        private static DataTable CreateSampleDataTable()
        {
            var dt = new DataTable("BOM");
            dt.Columns.Add("PartId", typeof(string));
            dt.Columns.Add("ParentPartId", typeof(string));
            dt.Columns.Add("Description", typeof(string));
            dt.Columns.Add("Quantity", typeof(int));
            dt.Columns.Add("UnitPrice", typeof(decimal));

            dt.Rows.Add("ASSY-01", "", "Complete Machine Assembly", 1, 10000m);
            dt.Rows.Add("SUB-10", "ASSY-01", "Motor Drive Sub-assembly", 1, 4000m);
            dt.Rows.Add("SUB-20", "ASSY-01", "Chassis & Frame Sub-assembly", 1, 3000m);
            dt.Rows.Add("PART-101", "SUB-10", "Servo Motor 750W", 2, 1200m);
            dt.Rows.Add("PART-102", "SUB-10", "Planetary Gearbox", 2, 800m);
            dt.Rows.Add("PART-201", "SUB-20", "Aluminum Extrusion 40x40", 8, 150m);
            dt.Rows.Add("PART-202", "SUB-20", "Corner Brackets & Screws", 32, 10m);

            return dt;
        }

        [Fact]
        public void WinForms_TreeList_EnumerableDataBinding_BuildsHierarchy()
        {
            StaTestRunner.Run(() =>
            {
                using var tree = new TreeList();
                var tasks = CreateSampleTasks();

                tree.SetDataSource(tasks, nameof(TaskItem.TaskId), nameof(TaskItem.ParentId));

                // Verify root nodes
                Assert.Single(tree.Nodes);
                var root = tree.Nodes[0];
                Assert.Equal("1", root.Id);
                Assert.Equal("Project Alpha", root[nameof(TaskItem.TaskName)]?.ToString());
                Assert.Equal(2, root.Children.Count);

                // Verify children of Phase 1
                var phase1 = root.Children[0];
                Assert.Equal("2", phase1.Id);
                Assert.Equal("Phase 1 - Design", phase1[nameof(TaskItem.TaskName)]?.ToString());
                Assert.Equal(2, phase1.Children.Count);

                var wireframe = phase1.Children[0];
                Assert.Equal("4", wireframe.Id);
                Assert.Equal("UI/UX Wireframing", wireframe[nameof(TaskItem.TaskName)]?.ToString());
                Assert.Empty(wireframe.Children);
                Assert.Equal(2, wireframe.Level);
            });
        }

        [Fact]
        public void WinForms_TreeList_DataTableDataBinding_BuildsHierarchyAndAutoGeneratesColumns()
        {
            StaTestRunner.Run(() =>
            {
                using var tree = new TreeList();
                var dt = CreateSampleDataTable();

                tree.SetDataSource(dt, "PartId", "ParentPartId");

                // Columns should be auto-generated
                Assert.Equal(5, tree.Columns.Count);
                Assert.Equal("PartId", tree.Columns[0].Name);
                Assert.Equal("Description", tree.Columns[2].Name);

                // Verify Tree hierarchy
                Assert.Single(tree.Nodes);
                var root = tree.Nodes[0];
                Assert.Equal("ASSY-01", root.Id);
                Assert.Equal("Complete Machine Assembly", root["Description"]?.ToString());
                Assert.Equal(2, root.Children.Count);

                var sub10 = root.Children[0];
                Assert.Equal("SUB-10", sub10.Id);
                Assert.Equal(2, sub10.Children.Count);
                Assert.Equal("PART-101", sub10.Children[0].Id);
            });
        }

        [Fact]
        public void WinForms_TreeList_MultiSelection_OperationsWorkAccurately()
        {
            StaTestRunner.Run(() =>
            {
                using var tree = new TreeList();
                var node1 = new ZeroTreeNode("Node 1");
                var node2 = new ZeroTreeNode("Node 2");
                var node3 = new ZeroTreeNode("Node 3");

                tree.AddNode(node1);
                tree.AddNode(node2);
                tree.AddNode(node3);

                tree.SelectionMode = TreeListSelectionMode.MultiSelect;

                // Select first node
                tree.SelectNode(node1);
                Assert.Single(tree.SelectedNodes);
                Assert.True(tree.IsNodeSelected(node1));
                Assert.False(tree.IsNodeSelected(node2));

                // Add second node to selection (Ctrl+Click simulation)
                tree.SelectNode(node2, addToSelection: true);
                Assert.Equal(2, tree.SelectedNodes.Count);
                Assert.True(tree.IsNodeSelected(node1));
                Assert.True(tree.IsNodeSelected(node2));

                // Deselect first node
                tree.DeselectNode(node1);
                Assert.Single(tree.SelectedNodes);
                Assert.False(tree.IsNodeSelected(node1));
                Assert.True(tree.IsNodeSelected(node2));

                // Clear selection
                tree.ClearSelection();
                Assert.Empty(tree.SelectedNodes);
                Assert.Null(tree.SelectedNode);
            });
        }

        [Fact]
        public void WinForms_TreeList_DynamicLazyLoading_TriggersBeforeExpand()
        {
            StaTestRunner.Run(() =>
            {
                using var tree = new TreeList();
                var rootNode = new ZeroTreeNode("Lazy Root")
                {
                    IsLazyLoadable = true,
                    HasLoadedChildren = false
                };
                tree.AddNode(rootNode);

                Assert.True(rootNode.HasChildren); // HasChildren true even before children loaded
                Assert.Empty(rootNode.Children);

                bool eventFired = false;
                tree.BeforeExpand += (s, e) =>
                {
                    eventFired = true;
                    e.Node.AddChild(new ZeroTreeNode("Loaded Child 1"));
                    e.Node.AddChild(new ZeroTreeNode("Loaded Child 2"));
                };

                // Trigger lazy load
                tree.TriggerLazyLoad(rootNode);

                Assert.True(eventFired);
                Assert.True(rootNode.HasLoadedChildren);
                Assert.Equal(2, rootNode.Children.Count);
                Assert.Equal("Loaded Child 1", rootNode.Children[0].Text);
            });
        }
    }
}
