using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Theme;
using ZeroUI.WinForms.Base;


namespace ZeroUI.WinForms.Industrial.Compliance
{
    /// <summary>
    /// ZComplianceChecklist control for Compliance.
    /// </summary>
    public class ZComplianceChecklist : ControlBase
    {
        public System.Collections.Generic.List<ZeroUI.Core.Compliance.ChecklistItem> Items { get; set; } public double CompletionPercent { get { return 0; } } public bool ShowProgress { get; set; } public event EventHandler ItemChecked; public event EventHandler ChecklistCompleted;

        public ZComplianceChecklist()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(ZeroTheme.Colors.Background);
            using (var brush = new SolidBrush(ZeroTheme.Colors.TextPrimary))
            {
                e.Graphics.DrawString("ZComplianceChecklist rendering", System.Drawing.SystemFonts.DefaultFont, brush, 10, 10);
            }
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }
    }

    [Obsolete("Use ZComplianceChecklist instead.")]
    public class ZeroComplianceChecklist : ZComplianceChecklist { }
}
