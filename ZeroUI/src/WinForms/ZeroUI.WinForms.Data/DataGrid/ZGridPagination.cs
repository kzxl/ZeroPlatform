using System;
using System.ComponentModel;
using ZeroUI.WinForms.Navigation;

namespace ZeroUI.WinForms.DataGrid
{
    /// <summary>
    /// High-performance, single-HWND pagination toolbar control designed for virtual grids and large datasets.
    /// Modernized from legacy 8-control panel to 1-HWND vector rendering with zero allocations.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - DataGrid")]
    [DefaultEvent("PageChanged")]
    [Description("High-performance single-HWND pagination toolbar control for virtual grids")]
    public class ZGridPagination : ZPagination
    {
        public new event EventHandler? PageChanged;

        public ZGridPagination()
        {
            Height = 46;
            PageSizes = new[] { 100, 500, 1000, 5000 };
            PageSize = 1000;
            base.PageChanged += (s, page) => PageChanged?.Invoke(this, EventArgs.Empty);
        }

        [Category("Data")]
        public int TotalRows
        {
            get => TotalCount;
            set => TotalCount = value;
        }

        public int PageStartRow => (CurrentPage - 1) * PageSize;
        public int PageEndRow => Math.Min(TotalCount, CurrentPage * PageSize);

        public void NavigateToPage(int page)
        {
            GoToPage(page);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZGridPagination"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("GridPagination is deprecated and will be removed in 5 release cycles. Please migrate to ZGridPagination instead.")]
    [ToolboxItem(false)]
    public class GridPagination : ZGridPagination
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZGridPagination"/>.
    /// </summary>
    [Obsolete("ZeroGridPagination is deprecated. Please use ZGridPagination instead.")]
    [ToolboxItem(false)]
    public class ZeroGridPagination : ZGridPagination
    {
    }

    #endregion
}
