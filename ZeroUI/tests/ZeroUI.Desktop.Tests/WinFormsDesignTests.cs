using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing.Design;
using System.Linq;
using System.Windows.Forms;
using Xunit;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.Core.Media;
using ZeroUI.WinForms.Charts;
using ZeroUI.WinForms.Charts.Model;
using ZeroUI.WinForms.Containers;
using ZeroUI.WinForms.DataGrid;
using ZeroUI.WinForms.Design.Charts;
using ZeroUI.WinForms.Design.Data;
using ZeroUI.WinForms.Design.Editors;
using ZeroUI.WinForms.Design.Industrial;
using ZeroUI.WinForms.Design.Media;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Industrial;
using ZeroUI.WinForms.Layout;
using ZeroUI.WinForms.Media;
using ZeroUI.WinForms.Navigation;

namespace ZeroUI.Desktop.Tests
{
    public class WinFormsDesignTests
    {
        [Fact]
        public void ZGrid_InitialState_HasEmptyColumns()
        {
            StaTestRunner.Run(() =>
            {
                using var grid = new ZGrid();
                Assert.NotNull(grid.Columns);
                Assert.Empty(grid.Columns);
            });
        }

        [Fact]
        public void ZGrid_HasDesignerAttributeRegistered()
        {
            var attributes = TypeDescriptor.GetAttributes(typeof(ZGrid));
            var designerAttr = attributes.OfType<DesignerAttribute>().FirstOrDefault();
            Assert.NotNull(designerAttr);
            Assert.Contains("ZGridDesigner", designerAttr.DesignerTypeName);
        }

        [Fact]
        public void ZGridActionList_ReturnsExpectedActionItems()
        {
            StaTestRunner.Run(() =>
            {
                using var grid = new ZGrid();
                var actionList = new ZGridActionList(grid);
                var items = actionList.GetSortedActionItems();

                Assert.NotNull(items);
                Assert.NotEmpty(items);

                // Check properties
                actionList.ShowFooter = true;
                Assert.True(grid.ShowFooter);

                actionList.ShowAutoFilterRow = true;
                Assert.True(grid.ShowAutoFilterRow);

                actionList.ViewType = GridViewType.CardView;
                Assert.Equal(GridViewType.CardView, grid.ViewType);

                actionList.Dock = DockStyle.Fill;
                Assert.Equal(DockStyle.Fill, grid.Dock);
            });
        }

        [Fact]
        public void ZChart_HasDesignerAttributeRegistered()
        {
            var attributes = TypeDescriptor.GetAttributes(typeof(ZChart));
            var designerAttr = attributes.OfType<DesignerAttribute>().FirstOrDefault();
            Assert.NotNull(designerAttr);
            Assert.Contains("ZChartDesigner", designerAttr.DesignerTypeName);
        }

        [Fact]
        public void ZChartActionList_ManipulatesPropertiesCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                using var chart = new ZChart();
                var actionList = new ZChartActionList(chart);

                actionList.LegendPosition = ChartLegendPosition.Bottom;
                Assert.Equal(ChartLegendPosition.Bottom, chart.LegendPosition);

                actionList.ShowLegend = false;
                Assert.Equal(ChartLegendPosition.None, chart.LegendPosition);

                actionList.ShowTooltips = false;
                Assert.False(chart.ShowTooltips);

                actionList.ShowCrosshair = true;
                Assert.True(chart.ShowCrosshair);

                actionList.Dock = DockStyle.Bottom;
                Assert.Equal(DockStyle.Bottom, chart.Dock);

                var items = actionList.GetSortedActionItems();
                Assert.NotNull(items);
                Assert.True(items.Count >= 5);
            });
        }

        [Fact]
        public void ZRadialGauge_HasDesignerAndEditorAttributes()
        {
            var attributes = TypeDescriptor.GetAttributes(typeof(ZRadialGauge));
            var designerAttr = attributes.OfType<DesignerAttribute>().FirstOrDefault();
            Assert.NotNull(designerAttr);
            Assert.Contains("ZRadialGaugeDesigner", designerAttr.DesignerTypeName);

            var prop = TypeDescriptor.GetProperties(typeof(ZRadialGauge))["Thresholds"];
            Assert.NotNull(prop);
            var editorAttr = prop.Attributes.OfType<EditorAttribute>().FirstOrDefault();
            Assert.NotNull(editorAttr);
            Assert.Contains("GaugeThresholdEditor", editorAttr.EditorTypeName);
        }

        [Fact]
        public void ZRadialGaugeActionList_ManipulatesPropertiesCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                using var gauge = new ZRadialGauge();
                var actionList = new ZRadialGaugeActionList(gauge);

                actionList.Minimum = 10.0;
                actionList.Maximum = 200.0;
                actionList.Value = 55.0;
                actionList.SweepAngle = 270;
                actionList.Title = "Pressure";
                actionList.Unit = "bar";

                Assert.Equal(10.0, gauge.Minimum);
                Assert.Equal(200.0, gauge.Maximum);
                Assert.Equal(55.0, gauge.Value);
                Assert.Equal(270, gauge.SweepAngle);
                Assert.Equal("Pressure", gauge.Title);
                Assert.Equal("bar", gauge.Unit);
            });
        }

        [Fact]
        public void ZSevenSegment_HasDesignerAttributeRegistered()
        {
            var attributes = TypeDescriptor.GetAttributes(typeof(ZSevenSegment));
            var designerAttr = attributes.OfType<DesignerAttribute>().FirstOrDefault();
            Assert.NotNull(designerAttr);
            Assert.Contains("ZSevenSegmentDesigner", designerAttr.DesignerTypeName);
        }

        [Fact]
        public void ZSevenSegmentActionList_ManipulatesPropertiesCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                using var seg = new ZSevenSegment();
                var actionList = new ZSevenSegmentActionList(seg);

                actionList.Value = "88.8";
                actionList.DigitCount = 6;
                actionList.ColorPreset = SevenSegmentColorPreset.NeonCyan;
                actionList.SlantAngle = 12f;

                Assert.Equal("88.8", seg.Value);
                Assert.Equal(6, seg.DigitCount);
                Assert.Equal(SevenSegmentColorPreset.NeonCyan, seg.ColorPreset);
                Assert.Equal(12f, seg.SlantAngle);
            });
        }

        [Fact]
        public void ZImageViewer_HasDesignerAttributeRegistered()
        {
            var attributes = TypeDescriptor.GetAttributes(typeof(ZImageViewer));
            var designerAttr = attributes.OfType<DesignerAttribute>().FirstOrDefault();
            Assert.NotNull(designerAttr);
            Assert.Contains("ZImageViewerDesigner", designerAttr.DesignerTypeName);
        }

        [Fact]
        public void ZImageViewerActionList_ManipulatesPropertiesCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                using var viewer = new ZImageViewer();
                var actionList = new ZImageViewerActionList(viewer);

                actionList.ViewMode = ImageViewMode.OriginalSize;
                actionList.ShowToolbar = true;
                actionList.ShowMiniMap = true;
                actionList.ShowPixelGrid = true;

                Assert.Equal(ImageViewMode.OriginalSize, viewer.ViewMode);
                Assert.True(viewer.ShowToolbar);
                Assert.True(viewer.ShowMiniMap);
                Assert.True(viewer.ShowPixelGrid);
            });
        }

        [Fact]
        public void ZDateEdit_HasDesignerAttributeRegistered()
        {
            var attributes = TypeDescriptor.GetAttributes(typeof(ZDateEdit));
            var designerAttr = attributes.OfType<DesignerAttribute>().FirstOrDefault();
            Assert.NotNull(designerAttr);
            Assert.Contains("ZDateEditDesigner", designerAttr.DesignerTypeName);
        }

        [Fact]
        public void ZDateEditActionList_ManipulatesPropertiesCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                using var dateEdit = new ZDateEdit();
                var actionList = new ZDateEditActionList(dateEdit);

                actionList.DateFormat = "dd/MM/yyyy";
                actionList.ShowPresets = false;
                actionList.ReadOnly = true;

                Assert.Equal("dd/MM/yyyy", dateEdit.DateFormat);
                Assert.False(dateEdit.ShowPresets);
                Assert.True(dateEdit.ReadOnly);
            });
        }

        [Fact]
        public void ContainerDesigners_HaveDesignerAttributesRegistered()
        {
            Assert.Contains("ZPanelDesigner", TypeDescriptor.GetAttributes(typeof(ZPanel)).OfType<DesignerAttribute>().First().DesignerTypeName);
            Assert.Contains("ZCardDesigner", TypeDescriptor.GetAttributes(typeof(ZCard)).OfType<DesignerAttribute>().First().DesignerTypeName);
            Assert.Contains("ZGroupBoxDesigner", TypeDescriptor.GetAttributes(typeof(ZGroupBox)).OfType<DesignerAttribute>().First().DesignerTypeName);
            Assert.Contains("ZTabControlDesigner", TypeDescriptor.GetAttributes(typeof(ZTabControl)).OfType<DesignerAttribute>().First().DesignerTypeName);
            Assert.Contains("ZSplitContainerDesigner", TypeDescriptor.GetAttributes(typeof(ZSplitContainer)).OfType<DesignerAttribute>().First().DesignerTypeName);
            Assert.Contains("ZFlowLayoutDesigner", TypeDescriptor.GetAttributes(typeof(ZFlowLayout)).OfType<DesignerAttribute>().First().DesignerTypeName);
        }

        [Fact]
        public void GaugeThresholdEditor_ReturnsModalStyle()
        {
            var editor = new GaugeThresholdEditor();
            Assert.Equal(UITypeEditorEditStyle.Modal, editor.GetEditStyle(null));
        }

        [Fact]
        public void ColorPickPaletteEditor_ReturnsDropDownStyleAndPaintSupport()
        {
            var editor = new ColorPickPaletteEditor();
            Assert.Equal(UITypeEditorEditStyle.DropDown, editor.GetEditStyle(null));
            Assert.True(editor.GetPaintValueSupported(null));
        }

        [Fact]
        public void ZTreeList_InitialState_HasEmptyColumns()
        {
            StaTestRunner.Run(() =>
            {
                using var treeList = new ZeroUI.WinForms.Data.ZTreeList();
                Assert.NotNull(treeList.Columns);
                Assert.Empty(treeList.Columns);
            });
        }

        [Fact]
        public void ZTreeList_HasDesignerAttributeRegistered()
        {
            var attributes = TypeDescriptor.GetAttributes(typeof(ZeroUI.WinForms.Data.ZTreeList));
            var designerAttr = attributes.OfType<DesignerAttribute>().FirstOrDefault();
            Assert.NotNull(designerAttr);
            Assert.Contains("ZTreeListDesigner", designerAttr.DesignerTypeName);
        }

        [Fact]
        public void ZTreeListActionList_ManipulatesPropertiesCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                using var treeList = new ZeroUI.WinForms.Data.ZTreeList();
                var actionList = new ZTreeListActionList(treeList);

                actionList.KeyFieldName = "Id";
                actionList.ParentFieldName = "ParentId";
                actionList.ShowCheckBoxes = false;
                actionList.ShowLines = true;
                actionList.ShowColumnHeaders = true;

                Assert.Equal("Id", treeList.KeyFieldName);
                Assert.Equal("ParentId", treeList.ParentFieldName);
                Assert.False(treeList.ShowCheckBoxes);
                Assert.True(treeList.ShowLines);
                Assert.True(treeList.ShowColumnHeaders);
            });
        }

        [Fact]
        public void ZPivotGrid_HasDesignerAttributeRegistered()
        {
            var attributes = TypeDescriptor.GetAttributes(typeof(ZeroUI.WinForms.PivotGrid.ZPivotGrid));
            var designerAttr = attributes.OfType<DesignerAttribute>().FirstOrDefault();
            Assert.NotNull(designerAttr);
            Assert.Contains("ZPivotGridDesigner", designerAttr.DesignerTypeName);
        }

        [Fact]
        public void ZPivotGridActionList_ReturnsExpectedActionItems()
        {
            StaTestRunner.Run(() =>
            {
                using var pivot = new ZeroUI.WinForms.PivotGrid.ZPivotGrid();
                var actionList = new ZPivotGridActionList(pivot);
                var items = actionList.GetSortedActionItems();

                Assert.NotNull(items);
                Assert.NotEmpty(items);

                actionList.Dock = DockStyle.Fill;
                Assert.Equal(DockStyle.Fill, pivot.Dock);
            });
        }

        [Fact]
        public void CodeExportForm_GeneratesValidCSharpAndXamlSnippets()
        {
            StaTestRunner.Run(() =>
            {
                using var grid = new ZGrid();
                grid.Columns.Add(new ZeroColumn("Id", 60, CellAlignment.Right));
                grid.Columns.Add(new ZeroColumn("Name", 120, CellAlignment.Left));

                string cs = ZeroUI.WinForms.Design.Forms.CodeExportForm.GenerateCSharpCode("ZGrid", grid);
                string xaml = ZeroUI.WinForms.Design.Forms.CodeExportForm.GenerateXamlCode("ZGrid", grid);

                Assert.NotNull(cs);
                Assert.Contains("ZeroUI.WinForms.DataGrid.ZGrid", cs);
                Assert.Contains("Columns.Add", cs);

                Assert.NotNull(xaml);
                Assert.Contains("ZeroGridControl", xaml);
                Assert.Contains("ZeroColumn", xaml);
            });
        }

        [Fact]
        public void TreeListDesignerForm_GeneratesValidSetupCode()
        {
            StaTestRunner.Run(() =>
            {
                using var treeList = new ZeroUI.WinForms.Data.ZTreeList();
                treeList.Columns.Add(new ZeroUI.WinForms.Data.TreeListColumn("Task", "Task Name", 200));

                string cs = ZeroUI.WinForms.Design.Forms.TreeListDesignerForm.GenerateCSharpSetupCode(treeList);
                string xaml = ZeroUI.WinForms.Design.Forms.TreeListDesignerForm.GenerateXamlSetupCode(treeList);

                Assert.NotNull(cs);
                Assert.Contains("ZTreeList", cs);
                Assert.Contains("TreeListColumn", cs);

                Assert.NotNull(xaml);
                Assert.Contains("ZTreeList", xaml);
            });
        }

        [Fact]
        public void ChartWizardForm_GeneratesValidSetupCode()
        {
            StaTestRunner.Run(() =>
            {
                using var chart = new ZChart();
                chart.Title = "Telemetry Overview";

                string cs = ZeroUI.WinForms.Design.Forms.ChartWizardForm.GenerateCSharpSetupCode(chart);
                string xaml = ZeroUI.WinForms.Design.Forms.ChartWizardForm.GenerateXamlSetupCode(chart);

                Assert.NotNull(cs);
                Assert.Contains("ZChart", cs);
                Assert.Contains("Telemetry Overview", cs);

                Assert.NotNull(xaml);
                Assert.Contains("ZChart", xaml);
            });
        }

        [Fact]
        public void GaugeThresholdEditorDialog_AddAndRetrieveRanges()
        {
            StaTestRunner.Run(() =>
            {
                var initial = new System.Collections.Generic.List<ZeroUI.Core.Scada.GaugeThresholdRange>
                {
                    new ZeroUI.Core.Scada.GaugeThresholdRange(0, 80, ZeroUI.Core.Scada.GaugeSeverity.Normal, (uint)System.Drawing.Color.Green.ToArgb(), "Safe")
                };

                using var dlg = new GaugeThresholdEditor.GaugeThresholdEditorDialog(initial);
                var ranges = dlg.GetRanges();
                Assert.NotNull(ranges);
                Assert.Single(ranges);
                Assert.Equal(80, ranges[0].To);
            });
        }

        [Fact]
        public void ZeroColorPaletteDialog_InstantiatesCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                using var dlg = new ZeroColorPaletteDialog(System.Drawing.Color.FromArgb(37, 99, 235));
                Assert.NotNull(dlg);
                Assert.Equal(System.Drawing.Color.FromArgb(37, 99, 235), dlg.SelectedColor);
            });
        }
    }
}
