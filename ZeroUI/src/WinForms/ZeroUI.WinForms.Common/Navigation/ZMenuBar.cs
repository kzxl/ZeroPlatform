using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Navigation
{
    /// <summary>
    /// Modern theme-aware application top menu bar adhering to ZeroUI design tokens.
    /// Features auto-skinning, crisp ClearType typography, dark/light theme reactivity,
    /// and fluent menu-building extension methods.
    /// Canonical drop-in replacement for standard <see cref="System.Windows.Forms.MenuStrip"/>.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Navigation")]
    [Description("Modern theme-aware top menu bar with auto-skinning and fluent APIs")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroDefaultControl.bmp")]
    public class ZMenuBar : MenuStrip
    {
        public ZMenuBar()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);

            Renderer = new MenuRenderer();
            GripStyle = ToolStripGripStyle.Hidden;
            ShowItemToolTips = true;
            Font = ZeroFontCache.Get("Segoe UI", 9.25f, FontStyle.Regular);

            UpdateThemeColors();

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateThemeColors();
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            UpdateThemeColors();
            Invalidate();
        }

        private void UpdateThemeColors()
        {
            var palette = ZeroTheme.Colors;
            BackColor = palette.Surface;
            ForeColor = palette.TextPrimary;
        }

        #region Fluent API Helpers

        /// <summary>
        /// Fluent helper to quickly add a top-level menu item.
        /// </summary>
        public ToolStripMenuItem AddMenu(string text)
        {
            var item = new ToolStripMenuItem(text);
            Items.Add(item);
            return item;
        }

        /// <summary>
        /// Fluent helper to quickly add a submenu item with an optional click handler and shortcut keys.
        /// </summary>
        public ToolStripMenuItem AddMenuItem(ToolStripMenuItem parent, string text, EventHandler? onClick = null, Keys shortcutKeys = Keys.None)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));

            var item = new ToolStripMenuItem(text, null, onClick);
            if (shortcutKeys != Keys.None)
            {
                item.ShortcutKeys = shortcutKeys;
                item.ShowShortcutKeys = true;
            }
            parent.DropDownItems.Add(item);
            return item;
        }

        /// <summary>
        /// Fluent helper to append a separator line to a menu.
        /// </summary>
        public ToolStripSeparator AddSeparator(ToolStripMenuItem parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));

            var sep = new ToolStripSeparator();
            parent.DropDownItems.Add(sep);
            return sep;
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZMenuBar"/>.
    /// </summary>
    [Obsolete("MenuBarControl is deprecated and will be removed in 5 release cycles. Please migrate to ZMenuBar instead.")]
    public class MenuBarControl : ZMenuBar { }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZMenuBar"/>.
    /// </summary>
    [Obsolete("ZeroMenuBarControl is deprecated and will be removed in 5 release cycles. Please migrate to ZMenuBar instead.")]
    public class ZeroMenuBarControl : ZMenuBar { }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZMenuBar"/>.
    /// </summary>
    [Obsolete("ZeroMenuBar is deprecated and will be removed in 5 release cycles. Please migrate to ZMenuBar instead.")]
    public class ZeroMenuBar : ZMenuBar { }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZMenuBar"/>.
    /// </summary>
    [Obsolete("ZeroMenuStrip is deprecated and will be removed in 5 release cycles. Please migrate to ZMenuBar instead.")]
    public class ZeroMenuStrip : ZMenuBar { }

    /// <summary>
    /// Convenience alias for <see cref="ZMenuBar"/>.
    /// </summary>
    public class ZMenuStrip : ZMenuBar { }

    #endregion
}
