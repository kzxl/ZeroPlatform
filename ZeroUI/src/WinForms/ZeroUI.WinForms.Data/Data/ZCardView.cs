using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using ZeroUI.Core;

namespace ZeroUI.WinForms.Data
{
    public class ZCardView : Control
    {
        public object ItemsSource { get; set; } public Action CardTemplate { get; set; } public int ColumnCount { get; set; } public int CardSpacing { get; set; } public event EventHandler CardClicked;
    }
    
    [Obsolete("CardView is deprecated and will be removed in 5 release cycles. Please migrate to ZCardView instead.")]
    [ToolboxItem(false)]
    public class CardView : ZCardView { }
    
    [Obsolete("ZeroCardView is deprecated and will be removed in 5 release cycles. Please migrate to ZCardView instead.")]
    [ToolboxItem(false)]
    public class ZeroCardView : ZCardView { }
}
