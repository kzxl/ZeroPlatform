using System;
using System.Collections.Generic;
using System.Drawing;
using Xunit;

namespace ZeroUI.Desktop.Tests
{
    public class P3ProductionControlsTests
    {
        #region ZMarkdownViewer Tests

        [Fact]
        public void ZMarkdownViewer_WinForms_RenderingAndRawToggle()
        {
            using var viewer = new ZeroUI.WinForms.Documents.ZMarkdownViewer();
            bool eventFired = false;
            viewer.MarkdownChanged += (s, e) => eventFired = true;

            viewer.MarkdownText = "# Title\n\n```csharp\nvar x = 1;\n```\n\n- Item 1\n- Item 2\n\n> Quote";
            Assert.True(eventFired);
            Assert.Equal(5, viewer.Blocks.Count);

            viewer.IsRawView = true;
            Assert.True(viewer.IsRawView);

            viewer.IsRawView = false;
            Assert.False(viewer.IsRawView);
        }

        [Fact]
        public void ZMarkdownViewer_Wpf_RenderingAndRawToggleOnSta()
        {
            StaTestRunner.Run(() =>
            {
                var viewer = new ZeroUI.Wpf.Documents.ZMarkdownViewer();
                bool eventFired = false;
                viewer.MarkdownChanged += (s, e) => eventFired = true;

                viewer.MarkdownText = "## Subtitle\n\nSome text content\n\n---\n\n* Bullet item";
                Assert.True(eventFired);
                Assert.Equal(4, viewer.Blocks.Count);

                viewer.IsRawView = true;
                Assert.True(viewer.IsRawView);
            });
        }

        #endregion

        #region ZCodeEditor Tests

        [Fact]
        public void ZCodeEditor_WinForms_TextAndLineNumbering()
        {
            using var editor = new ZeroUI.WinForms.Documents.ZCodeEditor();
            editor.Language = "csharp";
            editor.TabSize = 4;
            editor.ShowLineNumbers = true;

            bool textChanged = false;
            editor.TextChanged += (s, e) => textChanged = true;

            editor.Text = "line 1\nline 2\nline 3";
            Assert.True(textChanged);
            Assert.Equal("csharp", editor.Language);
            Assert.Equal(4, editor.TabSize);
            Assert.True(editor.ShowLineNumbers);
        }

        [Fact]
        public void ZCodeEditor_Wpf_TextAndGutterOnSta()
        {
            StaTestRunner.Run(() =>
            {
                var editor = new ZeroUI.Wpf.Documents.ZCodeEditor();
                editor.Language = "json";
                editor.ReadOnly = true;

                bool textChanged = false;
                editor.TextChanged += (s, e) => textChanged = true;

                editor.Text = "{\n  \"key\": \"value\"\n}";
                Assert.True(textChanged);
                Assert.True(editor.ReadOnly);
                Assert.Equal("json", editor.Language);
            });
        }

        #endregion

        #region ZJsonEditor Tests

        [Fact]
        public void ZJsonEditor_WinForms_FormattingMinificationAndValidation()
        {
            using var editor = new ZeroUI.WinForms.Editors.ZJsonEditor();
            bool jsonChanged = false;
            editor.JsonChanged += (s, e) => jsonChanged = true;

            // 1. Valid JSON formatting
            editor.JsonText = "{\"name\":\"ZeroUI\",\"version\":1}";
            Assert.True(jsonChanged);
            Assert.True(editor.IsValid);
            Assert.Null(editor.ErrorMessage);

            editor.FormatJson();
            Assert.Contains(Environment.NewLine, editor.JsonText);

            // 2. Minification
            editor.MinifyJson();
            Assert.DoesNotContain(Environment.NewLine, editor.JsonText);

            // 3. Validation failure
            bool validationFired = false;
            editor.ValidationChanged += (s, e) => validationFired = true;

            editor.JsonText = "{\"unclosed\": \"bracket\"";
            Assert.False(editor.IsValid);
            Assert.NotNull(editor.ErrorMessage);
            Assert.True(validationFired);
        }

        [Fact]
        public void ZJsonEditor_Wpf_FormattingMinificationAndValidationOnSta()
        {
            StaTestRunner.Run(() =>
            {
                var editor = new ZeroUI.Wpf.Editors.ZJsonEditor();
                bool jsonChanged = false;
                editor.JsonChanged += (s, e) => jsonChanged = true;

                editor.JsonText = "{\"status\":\"ok\",\"code\":200}";
                Assert.True(jsonChanged);
                Assert.True(editor.IsValid);

                editor.FormatJson();
                Assert.Contains(Environment.NewLine, editor.JsonText);

                editor.MinifyJson();
                Assert.DoesNotContain(Environment.NewLine, editor.JsonText);

                editor.JsonText = "{ invalid: json }";
                Assert.False(editor.IsValid);
            });
        }

        #endregion

        #region ZTreeView Tests

        [Fact]
        public void ZTreeView_WinForms_HierarchyAndExpandCollapse()
        {
            using var tree = new ZeroUI.WinForms.Navigation.ZTreeView();
            tree.ShowCheckboxes = true;
            tree.ShowLines = true;

            var root = tree.AddNode("Root Node");
            var child1 = root.Add("Child 1");
            var child2 = root.Add("Child 2");
            var grandChild = child1.Add("GrandChild 1.1");

            Assert.Single(tree.Nodes);
            Assert.Equal(2, root.Nodes.Count);
            Assert.Equal(0, root.Level);
            Assert.Equal(1, child1.Level);
            Assert.Equal(2, grandChild.Level);

            bool selectedFired = false;
            tree.NodeSelected += (s, e) => selectedFired = true;

            tree.SelectedNode = child1;
            Assert.True(selectedFired);
            Assert.True(child1.IsSelected);
            Assert.Equal(child1, tree.SelectedNode);

            // Expansion
            tree.ExpandAll();
            Assert.True(root.IsExpanded);
            Assert.True(child1.IsExpanded);

            tree.CollapseAll();
            Assert.False(root.IsExpanded);
            Assert.False(child1.IsExpanded);
        }

        [Fact]
        public void ZTreeView_Wpf_HierarchyAndSelectionOnSta()
        {
            StaTestRunner.Run(() =>
            {
                var tree = new ZeroUI.Wpf.Navigation.ZTreeView();
                tree.ShowCheckboxes = true;

                var root = tree.AddNode("Industrial System");
                var plc = root.Add("Siemens S7-1500");
                var tag1 = plc.Add("DB1.DBD0");

                Assert.Single(tree.Nodes);
                Assert.Equal(0, root.Level);
                Assert.Equal(1, plc.Level);
                Assert.Equal(2, tag1.Level);

                bool selectedFired = false;
                tree.NodeSelected += (s, e) => selectedFired = true;

                tree.SelectedNode = plc;
                Assert.True(selectedFired);
                Assert.True(plc.IsSelected);

                tree.ExpandAll();
                Assert.True(root.IsExpanded);
                Assert.True(plc.IsExpanded);

                tree.CollapseAll();
                Assert.False(root.IsExpanded);
            });
        }

        #endregion

        #region ZTimeline Tests

        [Fact]
        public void ZTimeline_WinForms_ItemLifecycleAndStatus()
        {
            using var timeline = new ZeroUI.WinForms.Workflow.ZTimeline();
            timeline.ItemSpacing = 48;

            timeline.Add("Raw Material Intake", "08:00 AM", "Batch LOT-2026-A validated", ZeroUI.WinForms.Workflow.TimelineStatus.Completed);
            timeline.Add("Thermal Treatment", "09:30 AM", "Temperature reached 145C", ZeroUI.WinForms.Workflow.TimelineStatus.InProgress);
            timeline.Add("Quality Inspection", "11:00 AM", "Pending spectrometer results", ZeroUI.WinForms.Workflow.TimelineStatus.Pending);

            Assert.Equal(3, timeline.Items.Count);
            Assert.Equal(48, timeline.ItemSpacing);
            Assert.Equal("Raw Material Intake", timeline.Items[0].Title);
            Assert.Equal(ZeroUI.WinForms.Workflow.TimelineStatus.InProgress, timeline.Items[1].Status);

            timeline.Clear();
            Assert.Empty(timeline.Items);
        }

        [Fact]
        public void ZTimeline_Wpf_ItemLifecycleAndStatusOnSta()
        {
            StaTestRunner.Run(() =>
            {
                var timeline = new ZeroUI.Wpf.Workflow.ZTimeline();
                timeline.ItemSpacing = 56;

                timeline.Add("Fermentation Start", "07:00", "Inoculation successful", ZeroUI.Wpf.Workflow.TimelineStatus.Completed);
                timeline.Add("pH Regulation", "08:15", "Buffer solution injected", ZeroUI.Wpf.Workflow.TimelineStatus.Completed);
                timeline.Add("Centrifugal Separation", "09:45", "High RPM stage running", ZeroUI.Wpf.Workflow.TimelineStatus.InProgress);

                Assert.Equal(3, timeline.Items.Count);
                Assert.Equal(56, timeline.ItemSpacing);
                Assert.Equal("Fermentation Start", timeline.Items[0].Title);
                Assert.Equal(ZeroUI.Wpf.Workflow.TimelineStatus.InProgress, timeline.Items[2].Status);

                timeline.Clear();
                Assert.Empty(timeline.Items);
            });
        }

        #endregion
    }
}
