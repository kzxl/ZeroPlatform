using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using ZeroUI.WinForms.Charts.Model;

namespace ZeroUI.WinForms.Charts
{
    [ToolboxItem(true)]
    [Category("ZeroUI - Charts")]
    public class ZChoroplethMap : ZChart
    {
        public object Regions { get; set; } public object ColorScale { get; set; } public bool ShowLegend { get; set; }
    }

    [Obsolete("ZeroChoroplethMap is deprecated and will be removed in 5 release cycles. Please migrate to ZChoroplethMap instead.")]
    [ToolboxItem(false)]
    public class ZeroChoroplethMap : ZChoroplethMap
    {
    }
}

