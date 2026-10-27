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
    [DefaultEvent("AnnotationAdded")]
    public class ZPdfAnnotator : Control
    {
        public string? DocumentPath { get; set; }
        public List<PdfAnnotation> Annotations { get; set; }
        public PdfAnnotationMode AnnotationMode { get; set; }

        public event EventHandler? AnnotationAdded;

        public ZPdfAnnotator()
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

    [Obsolete("PdfAnnotator is deprecated and will be removed in 5 release cycles. Please migrate to ZPdfAnnotator instead.")]
    [ToolboxItem(false)]
    public class PdfAnnotator : ZPdfAnnotator { }

    [Obsolete("ZeroPdfAnnotator is deprecated. Please use ZPdfAnnotator instead.")]
    [ToolboxItem(false)]
    public class ZeroPdfAnnotator : ZPdfAnnotator { }
}
