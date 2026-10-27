using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;
using ZeroUI.Core.Media;

namespace ZeroUI.WinForms.Media
{
    [ToolboxItem(true)]
    [Category("ZeroUI")]
    [DefaultEvent("CaptureCompleted")]
    public class ZScreenCapture : Control
    {
        public ScreenCaptureMode CaptureMode { get; set; }
        public Image? LastCapture { get; set; }

        public event EventHandler? CaptureCompleted;

        public ZScreenCapture()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(object sender, EventArgs e) => Invalidate();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ZeroTheme.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(ZeroTheme.Colors.Surface);
        }

        public void StartCapture() { }
    }

    [Obsolete("ScreenCapture is deprecated and will be removed in 5 release cycles. Please migrate to ZScreenCapture instead.")]
    [ToolboxItem(false)]
    public class ScreenCapture : ZScreenCapture { }

    [Obsolete("ZeroScreenCapture is deprecated. Please use ZScreenCapture instead.")]
    [ToolboxItem(false)]
    public class ZeroScreenCapture : ZScreenCapture { }
}
