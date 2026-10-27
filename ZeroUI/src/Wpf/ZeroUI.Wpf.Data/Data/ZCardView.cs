using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ZeroUI.Core;

namespace ZeroUI.Wpf.Data
{
    public class ZCardView : Control
    {
        public object ItemsSource { get; set; } public Action CardTemplate { get; set; } public int ColumnCount { get; set; } public int CardSpacing { get; set; } public event EventHandler CardClicked;
    }
    
    [Obsolete("CardView is deprecated and will be removed in 5 release cycles. Please migrate to ZCardView instead.")]
    public class CardView : ZCardView { }
    
    [Obsolete("ZeroCardView is deprecated and will be removed in 5 release cycles. Please migrate to ZCardView instead.")]
    public class ZeroCardView : ZCardView { }
}
