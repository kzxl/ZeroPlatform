using System.Linq;
using System.Windows.Forms;
using Xunit;
using ZeroUI.WinForms.Editors;

namespace ZeroUI.Desktop.Tests
{
    public class ComboBoxDropdownTests
    {
        [Fact]
        public void FirstOpen_WithFewerItemsThanMax_ShouldNotShowScrollBar()
        {
            StaTestRunner.Run(() =>
            {
                var form = new Form { Width = 400, Height = 300 };
                var combo = new ComboBoxEdit
                {
                    Width = 200,
                    Height = 36,
                    MaxDropDownItems = 8
                };
                combo.SetItems(new[] { "Item 1", "Item 2", "Item 3", "Item 4", "Item 5" });
                form.Controls.Add(combo);
                form.Show();

                // First open
                combo.ShowDropDown();

                // Check internal list control scrollbar
                var field = typeof(ZComboBox).GetField("_listControl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.NotNull(field);
                var listControl = (Control)field.GetValue(combo)!;
                var vScrollBar = listControl.Controls.OfType<VScrollBar>().FirstOrDefault();

                Assert.NotNull(vScrollBar);
                Assert.False(vScrollBar.Visible, "ScrollBar should NOT be visible on first open when items count (5) <= MaxDropDownItems (8)");

                combo.CloseDropDown();

                // Now test when items exceed MaxDropDownItems
                combo.SetItems(Enumerable.Range(1, 15).Select(i => $"Item {i}"));
                combo.ShowDropDown();

                Assert.True(vScrollBar.Visible, "ScrollBar should be visible when items count (15) > MaxDropDownItems (8)");
                Assert.Equal(15 - 8, vScrollBar.Maximum);

                combo.CloseDropDown();
                form.Close();
                form.Dispose();
            });
        }
    }
}
