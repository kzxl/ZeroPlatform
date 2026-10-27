using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using ZeroUI.WinForms.Charts.Model;

namespace ZeroUI.WinForms.Charts
{
    [ToolboxItem(true)]
    [Category("ZeroUI - Charts")]
    public class ZStackedAreaChart : ZChart
    {
        public object Series { get; set; } public bool Stacked { get; set; } public bool ShowLegend { get; set; }
    }

    [Obsolete("ZeroStackedAreaChart is deprecated and will be removed in 5 release cycles. Please migrate to ZStackedAreaChart instead.")]
    [ToolboxItem(false)]
    public class ZeroStackedAreaChart : ZStackedAreaChart
    {
    }
}

