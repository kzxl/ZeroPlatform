using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.WinForms.Base;
using ZeroUI.Core.Theme;

namespace ZeroUI.WinForms.Overlays
{
    [ToolboxItem(true)]
    [Category("ZeroUI - Overlays")]
    public class ZPopover : ControlBase
    {
        public static void Show(object content, object placement) { }
        
        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }
    }

    [Obsolete("ZeroPopover is deprecated and will be removed in 5 release cycles. Please migrate to ZPopover instead.")]
    [ToolboxItem(false)]
    public class ZeroPopover : ZPopover
    {
    }
}
