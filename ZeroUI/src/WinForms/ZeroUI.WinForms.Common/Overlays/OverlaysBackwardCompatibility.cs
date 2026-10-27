using System;
using System.ComponentModel;

namespace ZeroUI.WinForms.Overlays
{
    // =========================================================================================
    // BACKWARD COMPATIBILITY SHIMS
    // These type declarations ensure that existing code referencing ZeroUI.WinForms.Overlays
    // continues to compile without breaking changes.
    // Recommended: migrate using directives to ZeroUI.WinForms.Navigation or Data.
    // =========================================================================================

    #region Navigation Shims

    [Obsolete("Use ZeroUI.WinForms.Navigation.TabControlEx instead.")]
    public class TabControlEx : ZeroUI.WinForms.Navigation.TabControlEx { }

    [Obsolete("Use ZeroUI.WinForms.Navigation.TabPageEx instead.")]
    public class TabPageEx : ZeroUI.WinForms.Navigation.TabPageEx
    {
        public TabPageEx() { }
        public TabPageEx(string title, string icon = "") : base(title, icon) { }
    }

    [Obsolete("Use ZeroUI.WinForms.Navigation.ZeroTabControl instead.")]
    [ToolboxItem(false)]
    public class ZeroTabControl : ZeroUI.WinForms.Navigation.ZeroTabControl { }

    [Obsolete("Use ZeroUI.WinForms.Navigation.ZeroTabPage instead.")]
    public class ZeroTabPage : ZeroUI.WinForms.Navigation.ZeroTabPage
    {
        public ZeroTabPage() : base() { }
        public ZeroTabPage(string title, string icon = "") : base(title, icon) { }
    }

    [Obsolete("Use ZeroUI.WinForms.Navigation.SideNavControl instead.")]
    public class SideNavControl : ZeroUI.WinForms.Navigation.SideNavControl { }

    [Obsolete("Use ZeroUI.WinForms.Navigation.ZeroSideNav instead.")]
    [ToolboxItem(false)]
    public class ZeroSideNav : ZeroUI.WinForms.Navigation.ZeroSideNav { }

    [Obsolete("Use ZeroUI.WinForms.Navigation.ToolbarControl instead.")]
    public class ToolbarControl : ZeroUI.WinForms.Navigation.ToolbarControl { }

    [Obsolete("Use ZeroUI.WinForms.Navigation.ZeroToolbar instead.")]
    [ToolboxItem(false)]
    public class ZeroToolbar : ZeroUI.WinForms.Navigation.ZeroToolbar { }

    #endregion
}
