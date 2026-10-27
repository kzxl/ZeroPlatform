using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;


namespace ZeroUI.WinForms.Media
{
    [ToolboxItem(true)]
    [Category("ZeroUI")]
    
    public class ZLiveCamera : Control
    {
        public string? StreamUrl { get; set; }
        public bool IsPlaying { get; set; }
        public bool ShowControls { get; set; }

        

        public ZLiveCamera()
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

        public void Play() { }
        public void Pause() { }
        public void TakeSnapshot() { }
    }

    [Obsolete("LiveCamera is deprecated and will be removed in 5 release cycles. Please migrate to ZLiveCamera instead.")]
    [ToolboxItem(false)]
    public class LiveCamera : ZLiveCamera { }

    [Obsolete("ZeroLiveCamera is deprecated. Please use ZLiveCamera instead.")]
    [ToolboxItem(false)]
    public class ZeroLiveCamera : ZLiveCamera { }
}
