using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using ZeroUI.Core;

namespace ZeroUI.WinForms.Data
{
    public class ZMasterDetail : Control
    {
        public Control MasterControl { get; set; } public Control DetailControl { get; set; } public Orientation Orientation { get; set; } public int SplitterPosition { get; set; } public event EventHandler MasterSelectionChanged;
    }
    
    [Obsolete("MasterDetail is deprecated and will be removed in 5 release cycles. Please migrate to ZMasterDetail instead.")]
    [ToolboxItem(false)]
    public class MasterDetail : ZMasterDetail { }
    
    [Obsolete("ZeroMasterDetail is deprecated and will be removed in 5 release cycles. Please migrate to ZMasterDetail instead.")]
    [ToolboxItem(false)]
    public class ZeroMasterDetail : ZMasterDetail { }
}
