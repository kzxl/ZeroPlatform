using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using Xunit;
using ZeroUI.Wpf.Charts;
using ZeroUI.Wpf.DataGrid;
using ZeroUI.Wpf.Design.Adorners;
using ZeroUI.Wpf.Design.Metadata;
using ZeroUI.Wpf.Design.Mock;
using ZeroUI.Wpf.Editors;
using ZeroUI.Wpf.Industrial;
using ZeroUI.Wpf.Layout;
using ZeroUI.Wpf.Media;

namespace ZeroUI.Desktop.Tests
{
    public class WpfDesignTests
    {
        [Fact]
        public void WpfZGrid_InitialState_HasEmptyColumns()
        {
            StaTestRunner.Run(() =>
            {
                var grid = new ZGrid();
                Assert.NotNull(grid.Columns);
                Assert.Empty(grid.Columns);
            });
        }

        [Fact]
        public void ZeroWpfMetadataProvider_RegistersCategoriesCorrectly()
        {
            ZeroWpfMetadataProvider.RegisterMetadata();

            // ZGrid
            var gridCat = TypeDescriptor.GetAttributes(typeof(ZGrid)).OfType<CategoryAttribute>().FirstOrDefault();
            Assert.NotNull(gridCat);
            Assert.Equal("ZeroUI - DataGrid", gridCat.Category);

            // ZChart
            var chartCat = TypeDescriptor.GetAttributes(typeof(ZChart)).OfType<CategoryAttribute>().FirstOrDefault();
            Assert.NotNull(chartCat);
            Assert.Equal("ZeroUI - Charts & Analytics", chartCat.Category);

            // ZRadialGauge
            var gaugeCat = TypeDescriptor.GetAttributes(typeof(ZRadialGauge)).OfType<CategoryAttribute>().FirstOrDefault();
            Assert.NotNull(gaugeCat);
            Assert.Equal("ZeroUI - Industrial & SCADA", gaugeCat.Category);

            // ZSevenSegment
            var segCat = TypeDescriptor.GetAttributes(typeof(ZSevenSegment)).OfType<CategoryAttribute>().FirstOrDefault();
            Assert.NotNull(segCat);
            Assert.Equal("ZeroUI - Industrial & SCADA", segCat.Category);

            // ZImageViewer
            var imgCat = TypeDescriptor.GetAttributes(typeof(ZImageViewer)).OfType<CategoryAttribute>().FirstOrDefault();
            Assert.NotNull(imgCat);
            Assert.Equal("ZeroUI - Media", imgCat.Category);

            // ZDateEdit
            var dateCat = TypeDescriptor.GetAttributes(typeof(ZDateEdit)).OfType<CategoryAttribute>().FirstOrDefault();
            Assert.NotNull(dateCat);
            Assert.Equal("ZeroUI - Editors", dateCat.Category);

            // ZFlowLayout
            var flowCat = TypeDescriptor.GetAttributes(typeof(ZFlowLayout)).OfType<CategoryAttribute>().FirstOrDefault();
            Assert.NotNull(flowCat);
            Assert.Equal("ZeroUI - Layout", flowCat.Category);
        }

        [Fact]
        public void DesignDataFactory_GeneratesValidSampleTelemetry()
        {
            var data = DesignDataFactory.CreateSampleTelemetryData(15);
            Assert.NotNull(data);
            Assert.Equal(15, data.Count);

            foreach (var item in data)
            {
                Assert.False(string.IsNullOrEmpty(item.TagName));
                Assert.False(string.IsNullOrEmpty(item.Unit));
                Assert.False(string.IsNullOrEmpty(item.Status));
                Assert.True(item.Value >= 0);
            }
        }

        [Fact]
        public void DesignDataFactory_GeneratesValidVirtualSource()
        {
            var source = DesignDataFactory.CreateSampleVirtualSource(12);
            Assert.NotNull(source);
            Assert.Equal(12, source.TotalRowCount);
            Assert.Equal(6, source.TotalColumnCount);

            var buffer = new ZeroUI.Core.Data.CellValueBuffer();
            source.GetCellValue(0, 1, ref buffer);
            Assert.False(buffer.Text.IsEmpty);
        }

        [Fact]
        public void DesignOverlayAdorner_InstantiatesWithoutExceptions()
        {
            StaTestRunner.Run(() =>
            {
                var button = new Button();
                var adorner = new DesignOverlayAdorner(button, "ZGrid (Design Mode)");
                Assert.NotNull(adorner);
                Assert.False(adorner.IsHitTestVisible);
            });
        }
    }
}
