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
    
    public class Z3DModelViewer : Control
    {
        public byte[]? ModelSource { get; set; }
        public double RotationX { get; set; }
        public double RotationY { get; set; }
        public double RotationZ { get; set; }
        public double Zoom { get; set; }
        public bool ShowWireframe { get; set; }
        public Color BackgroundColor { get; set; }

        

        public Z3DModelViewer()
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

        
    }

    [Obsolete("ThreeDModelViewer is deprecated and will be removed in 5 release cycles. Please migrate to Z3DModelViewer instead.")]
    [ToolboxItem(false)]
    public class ThreeDModelViewer : Z3DModelViewer { }

    [Obsolete("Zero3DModelViewer is deprecated. Please use Z3DModelViewer instead.")]
    [ToolboxItem(false)]
    public class Zero3DModelViewer : Z3DModelViewer { }
}
