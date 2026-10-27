using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Theme
{
    /// <summary>
    /// Modern anti-aliased menu renderer for ZeroUI context menus with Dark/Light design token support.
    /// </summary>
    public class MenuRenderer : ToolStripProfessionalRenderer
    {
        public MenuRenderer() : base(new ZeroMenuColorTable())
        {
            RoundedEdges = true;
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var theme = ZeroTheme.Palette;
            if (e.Item.Selected && e.Item.Enabled)
            {
                var rect = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
                using var brush = new SolidBrush(theme.Hover);
                e.Graphics.FillRectangle(brush, rect);
                using var pen = new Pen(theme.Border);
                e.Graphics.DrawRectangle(pen, rect);
            }
            else
            {
                base.OnRenderMenuItemBackground(e);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var theme = ZeroTheme.Palette;
            e.TextColor = e.Item.Enabled ? theme.TextPrimary : theme.TextSecondary;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            var theme = ZeroTheme.Palette;
            using var pen = new Pen(theme.Border, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="MenuRenderer"/>.
    /// </summary>
    public class ZeroMenuRenderer : MenuRenderer
    {
    }

    internal class ZeroMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => ZeroTheme.Palette.Surface;
        public override Color MenuBorder => ZeroTheme.Palette.Border;
        public override Color MenuItemBorder => ZeroTheme.Palette.Border;
        public override Color MenuItemSelected => ZeroTheme.Palette.Hover;
        public override Color MenuItemSelectedGradientBegin => ZeroTheme.Palette.Hover;
        public override Color MenuItemSelectedGradientEnd => ZeroTheme.Palette.Hover;
        public override Color MenuStripGradientBegin => ZeroTheme.Palette.Surface;
        public override Color MenuStripGradientEnd => ZeroTheme.Palette.Surface;
        public override Color ImageMarginGradientBegin => ZeroTheme.Palette.Surface;
        public override Color ImageMarginGradientMiddle => ZeroTheme.Palette.Surface;
        public override Color ImageMarginGradientEnd => ZeroTheme.Palette.Surface;
        public override Color SeparatorDark => ZeroTheme.Palette.Border;
        public override Color SeparatorLight => Color.Transparent;
    }
}
