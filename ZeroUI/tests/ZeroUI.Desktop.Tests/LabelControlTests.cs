using System.Drawing;
using Xunit;
using WfLabel = ZeroUI.WinForms.Editors.LabelControl;
using WpfLabel = ZeroUI.Wpf.Editors.LabelControl;

namespace ZeroUI.Desktop.Tests
{
    public class LabelControlTests
    {
        [Fact]
        public void WinForms_LabelControl_DefaultsToAutoEllipsisTrue()
        {
            using var label = new WfLabel();
            Assert.True(label.AutoEllipsis, "AutoEllipsis should default to true on WinForms LabelControl.");
            Assert.True(label.ShowTooltipWhenTruncated, "ShowTooltipWhenTruncated should default to true.");
            Assert.Equal(ContentAlignment.MiddleLeft, label.TextAlign);
        }

        [Fact]
        public void WinForms_LabelControl_DetectsTruncation()
        {
            using var label = new WfLabel
            {
                Size = new Size(50, 20),
                Text = "This is a very long text that must be trimmed by AutoEllipsis."
            };

            // Force paint to measure and update truncation
            using var bmp = new Bitmap(50, 20);
            using var g = Graphics.FromImage(bmp);
            label.InvokePaint(g, label.ClientRectangle);

            Assert.True(label.IsTextTruncated, "LabelControl should detect text truncation when text exceeds width.");
        }

        [Fact]
        public void Wpf_LabelControl_DefaultsToAutoEllipsisTrue()
        {
            StaTestRunner.Run(() =>
            {
                var label = new WpfLabel();
                Assert.True(label.AutoEllipsis, "AutoEllipsis should default to true on WPF LabelControl.");
                Assert.Equal(System.Windows.TextTrimming.CharacterEllipsis, label.TextTrimming);
                Assert.True(label.ShowTooltipWhenTruncated, "ShowTooltipWhenTruncated should default to true.");
            });
        }

        [Fact]
        public void Wpf_LabelControl_TextPropertyBindsCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                var label = new WpfLabel
                {
                    Text = "ZeroUI Modern Label"
                };

                Assert.Equal("ZeroUI Modern Label", label.Text);
            });
        }
    }
}
