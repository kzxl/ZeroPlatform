using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Data;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Navigation
{
    /// <summary>
    /// Interactive hierarchical path navigator supporting segmented crumb buttons,
    /// chevrons, direct path string editing, back/forward history, and theme reactivity.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Layout")]
    [DefaultProperty("Path")]
    [DefaultEvent("PathChanged")]
    [Description("Hierarchical domain and asset path breadcrumb navigator with inline path editing")]
    [ToolboxBitmap(typeof(ZeroIcons), "BreadcrumbControl.bmp")]
    public class ZBreadcrumb : Control
    {
        private readonly ObservableCollection<BreadcrumbItem> _items = new ObservableCollection<BreadcrumbItem>();
        private readonly List<Rectangle> _crumbRects = new List<Rectangle>();
        private readonly List<Rectangle> _chevronRects = new List<Rectangle>();
        private readonly Stack<string> _backHistory = new Stack<string>();
        private readonly Stack<string> _forwardHistory = new Stack<string>();

        private string _separator = "›";
        private bool _allowPathEdit = true;
        private bool _isEditingPath = false;
        private readonly TextBox _pathEditor;

        private int _hoveredCrumbIndex = -1;
        private int _hoveredChevronIndex = -1;

        public event EventHandler<BreadcrumbItem>? ItemClicked;
        public event EventHandler<string>? PathChanged;
        public event EventHandler<int>? ChevronClicked;

        public ZBreadcrumb()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(360, 32);
            Font = new Font("Segoe UI", 9f);
            Cursor = Cursors.Default;
            BackColor = Color.Transparent;

            _items.CollectionChanged += OnItemsChanged;
            ZeroTheme.ThemeChanged += OnThemeChanged;

            // Inline path editor for Explorer-style editing
            _pathEditor = new TextBox
            {
                Visible = false,
                BorderStyle = BorderStyle.None,
                Font = Font
            };
            _pathEditor.KeyDown += OnPathEditorKeyDown;
            _pathEditor.LostFocus += (s, e) => CancelPathEdit();
            Controls.Add(_pathEditor);
        }

        #region Properties

        [Category("ZeroUI")]
        [Description("Collection of breadcrumb items in the active path.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public ObservableCollection<BreadcrumbItem> Items => _items;

        [Category("ZeroUI")]
        [Description("Delimiter string separating adjacent crumbs.")]
        [DefaultValue("›")]
        public string Separator
        {
            get => _separator;
            set
            {
                _separator = value ?? "›";
                Invalidate();
            }
        }

        [Category("ZeroUI")]
        [Description("Formatted hierarchical path string.")]
        public string Path
        {
            get
            {
                var list = new List<string>();
                for (int i = 0; i < _items.Count; i++) list.Add(_items[i].DisplayText);
                return string.Join(" / ", list);
            }
            set
            {
                if (Path != value)
                {
                    if (!string.IsNullOrEmpty(Path))
                    {
                        _backHistory.Push(Path);
                        _forwardHistory.Clear();
                    }

                    _items.Clear();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        var parts = value.Split(new[] { '/', '\\', '>' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < parts.Length; i++)
                        {
                            string part = parts[i].Trim();
                            _items.Add(new BreadcrumbItem(part, part));
                        }
                    }
                    Invalidate();
                    PathChanged?.Invoke(this, Path);
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Allows 1-click transition to an editable path text box.")]
        [DefaultValue(true)]
        public bool AllowPathEdit
        {
            get => _allowPathEdit;
            set => _allowPathEdit = value;
        }

        [Browsable(false)]
        public bool CanGoBack => _backHistory.Count > 0;

        [Browsable(false)]
        public bool CanGoForward => _forwardHistory.Count > 0;

        #endregion

        #region Navigation History API

        public bool GoBack()
        {
            if (_backHistory.Count == 0) return false;
            _forwardHistory.Push(Path);
            string prev = _backHistory.Pop();
            SetPathInternal(prev);
            return true;
        }

        public bool GoForward()
        {
            if (_forwardHistory.Count == 0) return false;
            _backHistory.Push(Path);
            string next = _forwardHistory.Pop();
            SetPathInternal(next);
            return true;
        }

        private void SetPathInternal(string pathString)
        {
            _items.Clear();
            if (!string.IsNullOrWhiteSpace(pathString))
            {
                var parts = pathString.Split(new[] { '/', '\\', '>' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i].Trim();
                    _items.Add(new BreadcrumbItem(part, part));
                }
            }
            Invalidate();
            PathChanged?.Invoke(this, Path);
        }

        #endregion

        #region Path Editing

        public void StartPathEdit()
        {
            if (!_allowPathEdit) return;
            _isEditingPath = true;
            _pathEditor.Text = Path;
            _pathEditor.Bounds = new Rectangle(Padding.Left + 4, (Height - _pathEditor.PreferredHeight) / 2, Width - Padding.Horizontal - 8, _pathEditor.PreferredHeight);
            _pathEditor.BackColor = ZeroTheme.IsDark ? Color.FromArgb(30, 41, 59) : Color.White;
            _pathEditor.ForeColor = ZeroTheme.IsDark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42);
            _pathEditor.Visible = true;
            _pathEditor.Focus();
            _pathEditor.SelectAll();
            Invalidate();
        }

        private void CommitPathEdit()
        {
            if (!_isEditingPath) return;
            string newPath = _pathEditor.Text;
            _isEditingPath = false;
            _pathEditor.Visible = false;
            Path = newPath;
        }

        private void CancelPathEdit()
        {
            if (!_isEditingPath) return;
            _isEditingPath = false;
            _pathEditor.Visible = false;
            Invalidate();
        }

        private void OnPathEditorKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                CommitPathEdit();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                CancelPathEdit();
            }
        }

        #endregion

        #region Events & Hit-Testing

        private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isEditingPath) return;

            int newCrumbHover = -1;
            for (int i = 0; i < _crumbRects.Count; i++)
            {
                if (_crumbRects[i].Contains(e.Location))
                {
                    newCrumbHover = i;
                    break;
                }
            }

            int newChevronHover = -1;
            for (int i = 0; i < _chevronRects.Count; i++)
            {
                if (_chevronRects[i].Contains(e.Location))
                {
                    newChevronHover = i;
                    break;
                }
            }

            if (_hoveredCrumbIndex != newCrumbHover || _hoveredChevronIndex != newChevronHover)
            {
                _hoveredCrumbIndex = newCrumbHover;
                _hoveredChevronIndex = newChevronHover;
                Cursor = (_hoveredCrumbIndex >= 0 || _hoveredChevronIndex >= 0) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredCrumbIndex != -1 || _hoveredChevronIndex != -1)
            {
                _hoveredCrumbIndex = -1;
                _hoveredChevronIndex = -1;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left || _isEditingPath) return;

            // 1. Crumb Click
            for (int i = 0; i < _crumbRects.Count; i++)
            {
                if (_crumbRects[i].Contains(e.Location) && i < _items.Count)
                {
                    ItemClicked?.Invoke(this, _items[i]);
                    // Truncate down to clicked crumb
                    if (i < _items.Count - 1)
                    {
                        _backHistory.Push(Path);
                        _forwardHistory.Clear();
                        while (_items.Count > i + 1)
                        {
                            _items.RemoveAt(_items.Count - 1);
                        }
                        PathChanged?.Invoke(this, Path);
                    }
                    return;
                }
            }

            // 2. Chevron Click
            for (int i = 0; i < _chevronRects.Count; i++)
            {
                if (_chevronRects[i].Contains(e.Location))
                {
                    ChevronClicked?.Invoke(this, i);
                    return;
                }
            }

            // 3. Clicked empty blank area on the right -> start editing
            if (_allowPathEdit)
            {
                StartPathEdit();
            }
        }

        #endregion

        #region Painting

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Outer Container Border / Background
            Color bg = ZeroTheme.IsDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(248, 250, 252);
            Color border = ZeroTheme.IsDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240);
            Color textPrimary = ZeroTheme.IsDark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42);
            Color textMuted = ZeroTheme.IsDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
            Color hoverPillBg = ZeroTheme.IsDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(241, 245, 249);
            Color activeAccent = Color.FromArgb(14, 165, 233); // Sky Blue

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = CreateRoundedRectanglePath(bounds, 4))
            using (var bgBrush = new SolidBrush(bg))
            using (var borderPen = new Pen(_isEditingPath ? activeAccent : border, 1))
            {
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
            }

            if (_isEditingPath) return;

            _crumbRects.Clear();
            _chevronRects.Clear();

            int curX = Padding.Left + 8;
            int centerY = Height / 2;

            for (int i = 0; i < _items.Count; i++)
            {
                string crumbText = _items[i].DisplayText;
                var textSize = g.MeasureString(crumbText, Font);
                int crumbW = (int)Math.Ceiling(textSize.Width) + 8;
                int crumbH = Math.Min(Height - 6, (int)Math.Ceiling(textSize.Height) + 4);
                int crumbY = centerY - (crumbH / 2);

                var crumbRect = new Rectangle(curX, crumbY, crumbW, crumbH);
                _crumbRects.Add(crumbRect);

                // Hover Pill Background
                if (i == _hoveredCrumbIndex)
                {
                    using var pillPath = CreateRoundedRectanglePath(crumbRect, 3);
                    using var pillBrush = new SolidBrush(hoverPillBg);
                    g.FillPath(pillBrush, pillPath);
                }

                // Crumb Text
                bool isLast = (i == _items.Count - 1);
                Color textColor = isLast ? textPrimary : textMuted;
                if (i == _hoveredCrumbIndex) textColor = activeAccent;

                TextRenderer.DrawText(g, crumbText, isLast ? ZeroFontCache.Get(Font.FontFamily.Name, Font.Size, FontStyle.Bold) : Font,
                    crumbRect, textColor, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);

                curX += crumbW + 2;

                // Chevron Separator
                if (i < _items.Count - 1)
                {
                    var sepSize = g.MeasureString(_separator, Font);
                    int sepW = (int)Math.Ceiling(sepSize.Width) + 6;
                    var sepRect = new Rectangle(curX, crumbY, sepW, crumbH);
                    _chevronRects.Add(sepRect);

                    Color sepColor = (i == _hoveredChevronIndex) ? activeAccent : textMuted;
                    TextRenderer.DrawText(g, _separator, Font, sepRect, sepColor, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
                    curX += sepW + 2;
                }
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
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

    /// <summary>
    /// Alias for BreadcrumbControl providing hierarchical path navigation.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Layout")]
    [Description("Hierarchical breadcrumb navigation bar")]
    public class BreadcrumbNavigation : ZBreadcrumb
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZBreadcrumb"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("BreadcrumbControl is deprecated and will be removed in 5 release cycles. Please migrate to ZBreadcrumb instead.")]
    [ToolboxItem(false)]
    public class BreadcrumbControl : ZBreadcrumb
    {
    }

    #endregion
}
