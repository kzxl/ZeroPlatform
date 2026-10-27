using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;
using ZeroUI.Core.Compliance;

namespace ZeroUI.WinForms.Compliance
{
    [ToolboxItem(true)]
    [Category("ZeroUI")]
    
    public class ZAuditTrail : Control
    {
        public List<AuditEntry> Entries { get; set; }
        public string? Filter { get; set; }
        public bool ShowDiff { get; set; }
        public int MaxEntries { get; set; }

        

        public ZAuditTrail()
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

    [Obsolete("AuditTrail is deprecated and will be removed in 5 release cycles. Please migrate to ZAuditTrail instead.")]
    [ToolboxItem(false)]
    public class AuditTrail : ZAuditTrail { }

    [Obsolete("ZeroAuditTrail is deprecated. Please use ZAuditTrail instead.")]
    [ToolboxItem(false)]
    public class ZeroAuditTrail : ZAuditTrail { }
}
