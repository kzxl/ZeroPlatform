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
    [DefaultEvent("ImageChanged")]
    public class ZImageGallery : Control
    {
        public List<GalleryImage> Images { get; set; }
        public int SelectedIndex { get; set; }
        public bool ShowThumbnailStrip { get; set; }
        public int SlideShowInterval { get; set; }

        public event EventHandler? ImageChanged;

        public ZImageGallery()
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

    [Obsolete("ImageGallery is deprecated and will be removed in 5 release cycles. Please migrate to ZImageGallery instead.")]
    [ToolboxItem(false)]
    public class ImageGallery : ZImageGallery { }

    [Obsolete("ZeroImageGallery is deprecated. Please use ZImageGallery instead.")]
    [ToolboxItem(false)]
    public class ZeroImageGallery : ZImageGallery { }
}
