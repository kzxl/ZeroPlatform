using System;
using System.ComponentModel;

namespace ZeroUI.WinForms.Data
{
    [Obsolete("Use ZeroUI.WinForms.Data.TreeList instead.")]
    public class TreeListLegacy : ZeroUI.WinForms.Data.TreeList { }

    [Obsolete("Use ZeroUI.WinForms.Data.ZeroTreeNode instead.")]
    public class ZeroTreeNodeLegacy : ZeroUI.WinForms.Data.ZeroTreeNode
    {
        public ZeroTreeNodeLegacy() { }
        public ZeroTreeNodeLegacy(string text, string icon = "", string subText = "") : base(text, icon, subText) { }
    }

    [Obsolete("Use ZeroUI.WinForms.Data.PropertyGridControl instead.")]
    public class PropertyGridControlLegacy : ZeroUI.WinForms.Data.PropertyGridControl { }

    [Obsolete("Use ZeroUI.WinForms.Data.ListViewControl instead.")]
    public class ListViewControlLegacy : ZeroUI.WinForms.Data.ListViewControl { }

    [Obsolete("Use ZeroUI.WinForms.Data.ZeroListView instead.")]
    [ToolboxItem(false)]
    public class ZeroListViewLegacy : ZeroUI.WinForms.Data.ZeroListView { }
}
