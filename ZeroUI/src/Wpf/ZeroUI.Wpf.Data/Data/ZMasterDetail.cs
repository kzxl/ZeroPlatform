using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ZeroUI.Core;

namespace ZeroUI.Wpf.Data
{
    public class ZMasterDetail : Control
    {
        public System.Windows.Controls.Control MasterControl { get; set; } public System.Windows.Controls.Control DetailControl { get; set; } public System.Windows.Controls.Orientation Orientation { get; set; } public int SplitterPosition { get; set; } public event EventHandler MasterSelectionChanged;
    }
    
    [Obsolete("MasterDetail is deprecated and will be removed in 5 release cycles. Please migrate to ZMasterDetail instead.")]
    public class MasterDetail : ZMasterDetail { }
    
    [Obsolete("ZeroMasterDetail is deprecated and will be removed in 5 release cycles. Please migrate to ZMasterDetail instead.")]
    public class ZeroMasterDetail : ZMasterDetail { }
}
