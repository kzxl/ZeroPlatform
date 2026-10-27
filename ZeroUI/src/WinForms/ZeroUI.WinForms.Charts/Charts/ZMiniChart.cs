using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using ZeroUI.WinForms.Charts.Model;

namespace ZeroUI.WinForms.Charts
{
    [ToolboxItem(true)]
    [Category("ZeroUI - Charts")]
    public class ZMiniChart : ZChart
    {
        public double[] Values { get; set; } public string ChartType { get; set; } public bool HighlightMin { get; set; } public bool HighlightMax { get; set; }
    }

    [Obsolete("ZeroMiniChart is deprecated and will be removed in 5 release cycles. Please migrate to ZMiniChart instead.")]
    [ToolboxItem(false)]
    public class ZeroMiniChart : ZMiniChart
    {
    }
}

