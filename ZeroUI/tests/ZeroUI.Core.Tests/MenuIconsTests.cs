using Xunit;
using ZeroUI.Core.Icons;

namespace ZeroUI.Core.Tests
{
    public class MenuIconsTests
    {
        [Fact]
        public void MenuIcons_Constants_AreNotEmptyAndValid()
        {
            Assert.False(string.IsNullOrWhiteSpace(MenuIcons.Add));
            Assert.False(string.IsNullOrWhiteSpace(MenuIcons.Edit));
            Assert.False(string.IsNullOrWhiteSpace(MenuIcons.Rename));
            Assert.False(string.IsNullOrWhiteSpace(MenuIcons.Delete));
            Assert.False(string.IsNullOrWhiteSpace(MenuIcons.Save));
            Assert.False(string.IsNullOrWhiteSpace(MenuIcons.Refresh));
            Assert.False(string.IsNullOrWhiteSpace(MenuIcons.Copy));
            Assert.False(string.IsNullOrWhiteSpace(MenuIcons.Paste));
            Assert.False(string.IsNullOrWhiteSpace(MenuIcons.Frame));
            Assert.False(string.IsNullOrWhiteSpace(MenuIcons.FitToContent));
            Assert.False(string.IsNullOrWhiteSpace(MenuIcons.AutoLayout));
            Assert.False(string.IsNullOrWhiteSpace(MenuIcons.Connect));
            Assert.False(string.IsNullOrWhiteSpace(MenuIcons.ArrowRight));
        }

        [Theory]
        [InlineData(MenuIcons.Add, "New Step", "➕ New Step")]
        [InlineData(MenuIcons.Edit, "Rename Item", "✏ Rename Item")]
        [InlineData(MenuIcons.Delete, "Remove Frame", "🗑 Remove Frame")]
        [InlineData("", "Plain Text", "Plain Text")]
        [InlineData(null, "Plain Text", "Plain Text")]
        public void MenuIcons_Format_ReturnsExpectedString(string? icon, string text, string expected)
        {
            string formatted = MenuIcons.Format(icon, text);
            Assert.Equal(expected, formatted);
        }
    }
}
