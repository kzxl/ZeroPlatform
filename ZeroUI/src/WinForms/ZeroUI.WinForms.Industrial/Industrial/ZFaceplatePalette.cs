using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    public class FaceplateTemplate { public string Name { get; set; } = string.Empty; public string Glyph { get; set; } = string.Empty; public string Type { get; set; } = string.Empty; }
    /// <summary>
    /// ZFaceplatePalette WinForms control.
    /// </summary>
    public class ZFaceplatePalette : Control
    {        public System.Collections.Generic.List<FaceplateTemplate> Templates { get; set; } = new System.Collections.Generic.List<FaceplateTemplate>();
        public event EventHandler TemplateSelected;
        public event EventHandler TemplateDragStarted;
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroFaceplatePalette is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroFaceplatePalette : ZFaceplatePalette { }

    [Obsolete("FaceplatePalette is deprecated.")]
    [ToolboxItem(false)]
    public class FaceplatePalette : ZFaceplatePalette { }
}
