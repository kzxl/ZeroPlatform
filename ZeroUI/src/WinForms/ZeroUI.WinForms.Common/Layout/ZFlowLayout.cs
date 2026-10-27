using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Layout;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Layout
{
    /// <summary>
    /// Responsive card and widget layout container that automatically wraps child items
    /// into dynamic rows based on viewport width. Features animated drag-and-drop tile reordering
    /// and JSON layout state serialization for customizable plant dashboards.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Layout")]
    [DefaultProperty("HGap")]
    [DefaultEvent("OrderChanged")]
    [Description("Responsive card container with auto-wrapping and interactive drag reordering")]
    [ToolboxBitmap(typeof(ZeroIcons), "FlowLayoutControl.bmp")]
    [Designer("ZeroUI.WinForms.Design.Common.ZFlowLayoutDesigner, ZeroUI.WinForms.Design")]
    public class ZFlowLayout : Panel
    {
        private int _hGap = 8;
        private int _vGap = 8;
        private bool _allowDragReordering = true;
        private int _dragThreshold = 6;
        private bool _isPerformingLayout = false;

        private Control? _draggedControl = null;
        private Point _dragStartPoint;
        private bool _isDragging = false;
        private readonly List<LayoutRect> _lastSlots = new List<LayoutRect>();

        public event EventHandler? OrderChanged;

        #region Properties

        [Category("ZeroUI - Layout")]
        [Description("Horizontal spacing in pixels between adjacent items.")]
        [DefaultValue(8)]
        public int HGap
        {
            get => _hGap;
            set
            {
                _hGap = Math.Max(0, value);
                PerformLayout();
            }
        }

        [Category("ZeroUI - Layout")]
        [Description("Vertical spacing in pixels between adjacent rows.")]
        [DefaultValue(8)]
        public int VGap
        {
            get => _vGap;
            set
            {
                _vGap = Math.Max(0, value);
                PerformLayout();
            }
        }

        [Category("ZeroUI - Behavior")]
        [Description("Enables interactive drag-and-drop tile reordering across slots.")]
        [DefaultValue(true)]
        public bool AllowDragReordering
        {
            get => _allowDragReordering;
            set => _allowDragReordering = value;
        }

        [Category("ZeroUI - Behavior")]
        [Description("Minimum drag distance in pixels before reordering begins.")]
        [DefaultValue(6)]
        public int DragThreshold
        {
            get => _dragThreshold;
            set => _dragThreshold = Math.Max(1, value);
        }

        #endregion

        public ZFlowLayout()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.ResizeRedraw, true);

            DoubleBuffered = true;
            AutoScroll = true;
            Padding = new Padding(8);
            BackColor = Color.Transparent;

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (e.Control != null)
            {
                WireChildDrag(e.Control);
            }
            PerformLayout();
        }

        protected override void OnControlRemoved(ControlEventArgs e)
        {
            base.OnControlRemoved(e);
            if (e.Control != null)
            {
                UnwireChildDrag(e.Control);
            }
            PerformLayout();
        }

        private void WireChildDrag(Control ctrl)
        {
            ctrl.MouseDown += Child_MouseDown;
            ctrl.MouseMove += Child_MouseMove;
            ctrl.MouseUp += Child_MouseUp;
        }

        private void UnwireChildDrag(Control ctrl)
        {
            ctrl.MouseDown -= Child_MouseDown;
            ctrl.MouseMove -= Child_MouseMove;
            ctrl.MouseUp -= Child_MouseUp;
        }

        private void Child_MouseDown(object? sender, MouseEventArgs e)
        {
            if (!_allowDragReordering || e.Button != MouseButtons.Left) return;
            if (sender is Control ctrl)
            {
                _draggedControl = ctrl;
                _dragStartPoint = ctrl.PointToScreen(e.Location);
                _isDragging = false;
            }
        }

        private void Child_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_allowDragReordering || _draggedControl == null || e.Button != MouseButtons.Left) return;

            Point currentScreenPt = (sender as Control)?.PointToScreen(e.Location) ?? PointToScreen(e.Location);
            int dx = Math.Abs(currentScreenPt.X - _dragStartPoint.X);
            int dy = Math.Abs(currentScreenPt.Y - _dragStartPoint.Y);

            if (!_isDragging && (dx >= _dragThreshold || dy >= _dragThreshold))
            {
                _isDragging = true;
                _draggedControl.Cursor = Cursors.SizeAll;
            }

            if (_isDragging && _lastSlots.Count > 0)
            {
                Point containerPt = PointToClient(currentScreenPt);
                int targetSlot = FlowLayoutEngine.HitTestSlot(containerPt.X - AutoScrollPosition.X, containerPt.Y - AutoScrollPosition.Y, _lastSlots);

                int currentIdx = Controls.IndexOf(_draggedControl);
                if (targetSlot >= 0 && targetSlot < Controls.Count && targetSlot != currentIdx)
                {
                    Controls.SetChildIndex(_draggedControl, targetSlot);
                    PerformLayout();
                }
            }
        }

        private void Child_MouseUp(object? sender, MouseEventArgs e)
        {
            if (_draggedControl != null)
            {
                _draggedControl.Cursor = Cursors.Default;
                if (_isDragging)
                {
                    _isDragging = false;
                    OrderChanged?.Invoke(this, EventArgs.Empty);
                }
                _draggedControl = null;
            }
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            if (_isPerformingLayout) return;
            _isPerformingLayout = true;

            try
            {
                var visibleControls = new List<Control>();
                var itemSizes = new List<(int Width, int Height)>();

                for (int i = 0; i < Controls.Count; i++)
                {
                    var c = Controls[i];
                    if (c.Visible)
                    {
                        visibleControls.Add(c);
                        itemSizes.Add((c.Width, c.Height));
                    }
                }

                int availW = ClientSize.Width - Padding.Horizontal;
                int startX = Padding.Left + AutoScrollPosition.X;
                int startY = Padding.Top + AutoScrollPosition.Y;

                var slots = FlowLayoutEngine.CalculateLayout(
                    itemSizes,
                    availW,
                    _hGap,
                    _vGap,
                    Padding.Left,
                    Padding.Top,
                    out int totalH);

                _lastSlots.Clear();
                _lastSlots.AddRange(slots);

                for (int i = 0; i < visibleControls.Count && i < slots.Count; i++)
                {
                    var c = visibleControls[i];
                    var rect = slots[i];
                    int cx = rect.X + AutoScrollPosition.X;
                    int cy = rect.Y + AutoScrollPosition.Y;

                    if (c.Location.X != cx || c.Location.Y != cy)
                    {
                        c.SetBounds(cx, cy, rect.Width, rect.Height, BoundsSpecified.Location);
                    }
                }

                AutoScrollMinSize = new Size(0, totalH + Padding.Bottom);
            }
            finally
            {
                _isPerformingLayout = false;
            }

            base.OnLayout(levent);
        }

        #region Serialization

        /// <summary>
        /// Exports the current order of child controls by name into a lightweight JSON array.
        /// Example: ["cardOee", "cardRpm", "cardThermal"].
        /// </summary>
        public string ExportLayoutJson()
        {
            var names = new List<string>(Controls.Count);
            for (int i = 0; i < Controls.Count; i++)
            {
                string name = Controls[i].Name;
                if (!string.IsNullOrEmpty(name))
                {
                    names.Add($"\"{name}\"");
                }
            }
            return "[" + string.Join(",", names) + "]";
        }

        /// <summary>
        /// Re-orders child controls matching the provided JSON name array.
        /// </summary>
        public void ImportLayoutJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            // Simple parser extracting names without external dependencies
            string trimmed = json.Trim().TrimStart('[').TrimEnd(']');
            if (string.IsNullOrWhiteSpace(trimmed)) return;

            string[] tokens = trimmed.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            int targetIdx = 0;

            for (int i = 0; i < tokens.Length; i++)
            {
                string name = tokens[i].Trim().Trim('\"', '\'');
                if (string.IsNullOrEmpty(name)) continue;

                Control[] matches = Controls.Find(name, false);
                if (matches.Length > 0 && targetIdx < Controls.Count)
                {
                    Controls.SetChildIndex(matches[0], targetIdx++);
                }
            }

            PerformLayout();
            OrderChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    
    private void OnThemeChanged(object sender, EventArgs e) => Invalidate();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }

}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZFlowLayout"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("FlowLayoutControl is deprecated and will be removed in 5 release cycles. Please migrate to ZFlowLayout instead.")]
    [ToolboxItem(false)]
    public class FlowLayoutControl : ZFlowLayout
    {
    }

    #endregion
}
