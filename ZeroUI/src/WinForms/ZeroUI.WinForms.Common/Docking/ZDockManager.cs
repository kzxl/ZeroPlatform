using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Layout;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Navigation;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Docking
{
    public enum DockPosition
    {
        Left,
        Right,
        Top,
        Bottom,
        Document,
        Float
    }

    /// <summary>
    /// Represents an individual docking panel container hosting child controls with a modern themed header,
    /// pin toggle, float button, and close actions.
    /// </summary>
    [ToolboxItem(false)]
    public class DockPanelControl : Panel
    {
        private string _title = "Panel";
        private DockPosition _dockPosition = DockPosition.Document;
        private bool _isPinned = true;
        private bool _autoHide = false;
        private bool _closable = true;
        private bool _floatable = true;
        private int _headerHeight = 28;

        private Rectangle _pinButtonRect;
        private Rectangle _floatButtonRect;
        private Rectangle _closeButtonRect;
        private bool _isPinHovered = false;
        private bool _isFloatHovered = false;
        private bool _isCloseHovered = false;

        private bool _isDraggingHeader = false;
        private Point _dragStartPoint;

        public event EventHandler? DockPositionChanged;
        public event EventHandler? PinStateChanged;
        public event EventHandler? CloseRequested;
        public event EventHandler? FloatRequested;
        public event EventHandler<Point>? HeaderDragged;
        public event EventHandler<Point>? HeaderDragEnded;

        private string _panelKey = string.Empty;

        [Category("Design")]
        [Description("Unique key identifier for layout persistence.")]
        [DefaultValue("")]
        public string PanelKey
        {
            get => string.IsNullOrEmpty(_panelKey) ? _title : _panelKey;
            set => _panelKey = value ?? string.Empty;
        }

        [Category("Appearance")]
        [DefaultValue("Panel")]
        public string Title
        {
            get => _title;
            set { _title = value; Invalidate(); }
        }

        [Category("Layout")]
        [DefaultValue(DockPosition.Document)]
        public DockPosition DockPosition
        {
            get => _dockPosition;
            set
            {
                if (_dockPosition != value)
                {
                    _dockPosition = value;
                    DockPositionChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (_isPinned != value)
                {
                    _isPinned = value;
                    PinStateChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool AutoHide
        {
            get => _autoHide;
            set { _autoHide = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool Closable
        {
            get => _closable;
            set { _closable = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool Floatable
        {
            get => _floatable;
            set { _floatable = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(28)]
        public int HeaderHeight
        {
            get => _headerHeight;
            set { _headerHeight = Math.Max(20, value); Invalidate(); }
        }

        public Control? HostedContent { get; set; }

        public DockPanelControl()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            Padding = new Padding(0, _headerHeight, 0, 0);
        }

        public DockPanelControl(string title, DockPosition position = DockPosition.Document) : this()
        {
            Title = title;
            DockPosition = position;
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (e.Control != null && HostedContent == null)
            {
                HostedContent = e.Control;
                e.Control.Dock = DockStyle.Fill;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var colors = ZeroTheme.Colors;
            var headerRect = new Rectangle(0, 0, Width, _headerHeight);

            // Draw header background
            using (var brush = new SolidBrush(colors.HeaderBackground))
            {
                g.FillRectangle(brush, headerRect);
            }

            // Draw header bottom border
            using (var pen = new Pen(colors.Border))
            {
                g.DrawLine(pen, 0, _headerHeight - 1, Width, _headerHeight - 1);
            }

            // Draw panel title
            int textX = 10;
            int buttonSize = 16;
            int rightX = Width - 8;

            // Close button rect
            if (_closable)
            {
                rightX -= buttonSize;
                _closeButtonRect = new Rectangle(rightX, (_headerHeight - buttonSize) / 2, buttonSize, buttonSize);
                rightX -= 4;
            }
            else
            {
                _closeButtonRect = Rectangle.Empty;
            }

            // Float button rect
            if (_floatable)
            {
                rightX -= buttonSize;
                _floatButtonRect = new Rectangle(rightX, (_headerHeight - buttonSize) / 2, buttonSize, buttonSize);
                rightX -= 4;
            }
            else
            {
                _floatButtonRect = Rectangle.Empty;
            }

            // Pin button rect
            rightX -= buttonSize;
            _pinButtonRect = new Rectangle(rightX, (_headerHeight - buttonSize) / 2, buttonSize, buttonSize);

            // Draw Title Text
            using (var titleFont = new Font(Font.FontFamily, 9f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(colors.TextPrimary))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                var titleBounds = new Rectangle(textX, 0, Math.Max(20, rightX - textX - 8), _headerHeight);
                g.DrawString(_title, titleFont, textBrush, titleBounds, sf);
            }

            // Render Header Buttons
            DrawHeaderButtons(g, colors);
        }

        private void DrawHeaderButtons(Graphics g, ZeroThemePalette colors)
        {
            // Draw Pin Button
            if (!_pinButtonRect.IsEmpty)
            {
                Color pinColor = _isPinHovered ? colors.Primary : (_isPinned ? colors.TextPrimary : colors.TextSecondary);
                using (var pen = new Pen(pinColor, 1.5f))
                {
                    int cx = _pinButtonRect.X + _pinButtonRect.Width / 2;
                    int cy = _pinButtonRect.Y + _pinButtonRect.Height / 2;
                    if (_isPinned)
                    {
                        // Vertical pin
                        g.DrawLine(pen, cx, cy - 4, cx, cy + 4);
                        g.DrawLine(pen, cx - 3, cy - 2, cx + 3, cy - 2);
                    }
                    else
                    {
                        // Horizontal pin
                        g.DrawLine(pen, cx - 4, cy, cx + 4, cy);
                        g.DrawLine(pen, cx - 2, cy - 3, cx - 2, cy + 3);
                    }
                }
            }

            // Draw Float Button
            if (_floatable && !_floatButtonRect.IsEmpty)
            {
                Color floatColor = _isFloatHovered ? colors.Primary : colors.TextSecondary;
                using (var pen = new Pen(floatColor, 1.3f))
                {
                    int x = _floatButtonRect.X + 2;
                    int y = _floatButtonRect.Y + 2;
                    g.DrawRectangle(pen, x, y + 2, 8, 8);
                    g.DrawRectangle(pen, x + 3, y, 7, 7);
                }
            }

            // Draw Close Button
            if (_closable && !_closeButtonRect.IsEmpty)
            {
                Color closeColor = _isCloseHovered ? colors.Danger : colors.TextSecondary;
                using (var pen = new Pen(closeColor, 1.5f))
                {
                    int x = _closeButtonRect.X + 3;
                    int y = _closeButtonRect.Y + 3;
                    int size = 10;
                    g.DrawLine(pen, x, y, x + size, y + size);
                    g.DrawLine(pen, x + size, y, x, y + size);
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && e.Y <= _headerHeight)
            {
                if (_closeButtonRect.Contains(e.Location) && _closable)
                {
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    return;
                }

                if (_floatButtonRect.Contains(e.Location) && _floatable)
                {
                    FloatRequested?.Invoke(this, EventArgs.Empty);
                    return;
                }

                if (_pinButtonRect.Contains(e.Location))
                {
                    IsPinned = !IsPinned;
                    return;
                }

                // Start dragging header for floating or docking
                _isDraggingHeader = true;
                _dragStartPoint = e.Location;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool prevPin = _isPinHovered;
            bool prevFloat = _isFloatHovered;
            bool prevClose = _isCloseHovered;

            _isPinHovered = _pinButtonRect.Contains(e.Location);
            _isFloatHovered = _floatButtonRect.Contains(e.Location);
            _isCloseHovered = _closeButtonRect.Contains(e.Location);

            if (prevPin != _isPinHovered || prevFloat != _isFloatHovered || prevClose != _isCloseHovered)
            {
                Invalidate(new Rectangle(Width - 80, 0, 80, _headerHeight));
            }

            if (_isDraggingHeader && e.Button == MouseButtons.Left)
            {
                int dx = Math.Abs(e.X - _dragStartPoint.X);
                int dy = Math.Abs(e.Y - _dragStartPoint.Y);
                if (dx > 4 || dy > 4)
                {
                    HeaderDragged?.Invoke(this, PointToScreen(e.Location));
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDraggingHeader)
            {
                _isDraggingHeader = false;
                HeaderDragEnded?.Invoke(this, PointToScreen(e.Location));
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isPinHovered || _isFloatHovered || _isCloseHovered)
            {
                _isPinHovered = false;
                _isFloatHovered = false;
                _isCloseHovered = false;
                Invalidate(new Rectangle(Width - 80, 0, 80, _headerHeight));
            }
        }
    }

    /// <summary>
    /// Independent floating tool window hosting a detached DockPanelControl across multiple monitors.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "FloatingWindow.bmp")]
    public class FloatingWindow : Form
    {
        private readonly DockPanelControl _panel;
        private readonly ZDockManager _dockManager;

        public DockPanelControl DockPanel => _panel;

        public FloatingWindow(ZDockManager dockManager, DockPanelControl panel, Rectangle? initialBounds = null)
        {
            _dockManager = dockManager ?? throw new ArgumentNullException(nameof(dockManager));
            _panel = panel ?? throw new ArgumentNullException(nameof(panel));

            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            Text = panel.Title;
            if (initialBounds.HasValue && initialBounds.Value.Width > 80 && initialBounds.Value.Height > 80)
            {
                Bounds = initialBounds.Value;
            }
            else
            {
                Size = new Size(320, 420);
            }
            BackColor = ZeroTheme.Colors.Surface;

            Controls.Add(panel);
            panel.Dock = DockStyle.Fill;

            panel.CloseRequested += (s, e) => Close();
            panel.FloatRequested += (s, e) => RedockToManager(DockPosition.Document);
        }

        public FloatingWindow(ZDockManager dockManager, DockPanelControl panel)
            : this(dockManager, panel, null)
        {
        }

        public void RedockToManager(DockPosition targetPosition)
        {
            Controls.Remove(_panel);
            _dockManager.AddPanel(_panel, targetPosition);
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (Controls.Contains(_panel))
            {
                Controls.Remove(_panel);
            }
        }
    }

    /// <summary>
    /// Enterprise multi-region Dock Manager for WinForms.
    /// Manages Left, Right, Top, Bottom, and Document docking zones with resizable splitters,
    /// tabbed document groups, auto-hide sidebars, and multi-monitor floating windows.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Layout & Windowing")]
    [Description("Enterprise Visual Studio-style multi-region dock manager with splitters, tabs, and multi-monitor floating panels.")]
    [ToolboxBitmap(typeof(ZeroIcons), "DockManager.bmp")]
    public class ZDockManager : Control
    {
        private readonly List<DockPanelControl> _panels = new List<DockPanelControl>();
        private readonly List<FloatingWindow> _floatingWindows = new List<FloatingWindow>();

        // Layout Containers
        private readonly Panel _leftContainer = new Panel { Width = 260, Dock = DockStyle.Left, Visible = false };
        private readonly Splitter _leftSplitter = new Splitter { Dock = DockStyle.Left, Width = 5, Visible = false };

        private readonly Panel _rightContainer = new Panel { Width = 280, Dock = DockStyle.Right, Visible = false };
        private readonly Splitter _rightSplitter = new Splitter { Dock = DockStyle.Right, Width = 5, Visible = false };

        private readonly Panel _bottomContainer = new Panel { Height = 180, Dock = DockStyle.Bottom, Visible = false };
        private readonly Splitter _bottomSplitter = new Splitter { Dock = DockStyle.Bottom, Height = 5, Visible = false };

        private readonly Panel _topContainer = new Panel { Height = 140, Dock = DockStyle.Top, Visible = false };
        private readonly Splitter _topSplitter = new Splitter { Dock = DockStyle.Top, Height = 5, Visible = false };

        // Center Document Area using TabControlEx
        private readonly TabControlEx _documentTabControl = new TabControlEx
        {
            Dock = DockStyle.Fill,
            TabStyle = TabStyle.Card
        };

        // Auto-Hide Sidebars & Drawer Overlay
        private readonly Panel _leftAutoHideBar = new Panel { Width = 28, Dock = DockStyle.Left, Visible = false };
        private readonly Panel _rightAutoHideBar = new Panel { Width = 28, Dock = DockStyle.Right, Visible = false };
        private readonly Panel _drawerOverlay;
        private DockPanelControl? _activeDrawerPanel;

        // Visual Dock Guides Diamond HUD
        private readonly DockGuideHUD _guideHUD;

        [Browsable(false)]
        public IReadOnlyList<DockPanelControl> Panels => _panels;

        [Browsable(false)]
        public TabControlEx DocumentTabs => _documentTabControl;

        public ZDockManager()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = ZeroTheme.Colors.Background;

            // Assemble layout containers in correct WinForms docking hierarchy
            Controls.Add(_documentTabControl);

            Controls.Add(_bottomSplitter);
            Controls.Add(_bottomContainer);

            Controls.Add(_topSplitter);
            Controls.Add(_topContainer);

            Controls.Add(_rightSplitter);
            Controls.Add(_rightContainer);

            Controls.Add(_leftSplitter);
            Controls.Add(_leftContainer);

            Controls.Add(_leftAutoHideBar);
            Controls.Add(_rightAutoHideBar);

            _drawerOverlay = new Panel
            {
                Width = 260,
                Visible = false,
                BackColor = ZeroTheme.Colors.Surface
            };
            Controls.Add(_drawerOverlay);

            _guideHUD = new DockGuideHUD(this);
            Controls.Add(_guideHUD);

            _leftSplitter.BackColor = ZeroTheme.Colors.Border;
            _rightSplitter.BackColor = ZeroTheme.Colors.Border;
            _topSplitter.BackColor = ZeroTheme.Colors.Border;
            _bottomSplitter.BackColor = ZeroTheme.Colors.Border;

            _leftAutoHideBar.Paint += OnLeftAutoHideBarPaint;
            _leftAutoHideBar.MouseDown += OnLeftAutoHideBarMouseDown;
            _rightAutoHideBar.Paint += OnRightAutoHideBarPaint;
            _rightAutoHideBar.MouseDown += OnRightAutoHideBarMouseDown;

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        public void AddPanel(DockPanelControl panel, DockPosition position)
        {
            if (panel == null) throw new ArgumentNullException(nameof(panel));
            panel.DockPosition = position;
            AddPanel(panel);
        }

        public void AddPanel(DockPanelControl panel)
        {
            if (panel == null || _panels.Contains(panel)) return;
            _panels.Add(panel);

            panel.CloseRequested += Panel_CloseRequested;
            panel.FloatRequested += Panel_FloatRequested;
            panel.PinStateChanged += Panel_PinStateChanged;
            panel.HeaderDragged += Panel_HeaderDragged;
            panel.HeaderDragEnded += Panel_HeaderDragEnded;

            ArrangePanel(panel);
        }

        public void RemovePanel(DockPanelControl panel)
        {
            if (panel == null || !_panels.Remove(panel)) return;

            panel.CloseRequested -= Panel_CloseRequested;
            panel.FloatRequested -= Panel_FloatRequested;
            panel.PinStateChanged -= Panel_PinStateChanged;
            panel.HeaderDragged -= Panel_HeaderDragged;
            panel.HeaderDragEnded -= Panel_HeaderDragEnded;

            if (panel.Parent != null)
            {
                panel.Parent.Controls.Remove(panel);
            }

            if (_activeDrawerPanel == panel)
            {
                _drawerOverlay.Controls.Clear();
                _drawerOverlay.Visible = false;
                _activeDrawerPanel = null;
            }

            RebuildLayout();
        }

        private void Panel_HeaderDragged(object? sender, Point screenPt)
        {
            if (sender is DockPanelControl panel)
            {
                _guideHUD.Bounds = ClientRectangle;
                _guideHUD.BringToFront();
                _guideHUD.Visible = true;
                _guideHUD.UpdateMouse(screenPt);
            }
        }

        private void Panel_HeaderDragEnded(object? sender, Point screenPt)
        {
            if (sender is DockPanelControl panel)
            {
                var targetPos = _guideHUD.HoveredPosition;
                _guideHUD.Visible = false;
                _guideHUD.Reset();

                if (targetPos.HasValue)
                {
                    RedockPanel(panel, targetPos.Value);
                }
                else
                {
                    Point clientPt = PointToClient(screenPt);
                    if (!ClientRectangle.Contains(clientPt))
                    {
                        FloatPanel(panel);
                    }
                }
            }
        }

        public void RedockPanel(DockPanelControl panel, DockPosition position)
        {
            if (panel.Parent != null)
            {
                panel.Parent.Controls.Remove(panel);
            }
            panel.DockPosition = position;
            panel.IsPinned = true;
            ArrangePanel(panel);
            RebuildLayout();
        }

        private void ArrangePanel(DockPanelControl panel)
        {
            switch (panel.DockPosition)
            {
                case DockPosition.Left:
                    panel.Dock = DockStyle.Fill;
                    _leftContainer.Controls.Add(panel);
                    _leftContainer.Visible = true;
                    _leftSplitter.Visible = true;
                    break;

                case DockPosition.Right:
                    panel.Dock = DockStyle.Fill;
                    _rightContainer.Controls.Add(panel);
                    _rightContainer.Visible = true;
                    _rightSplitter.Visible = true;
                    break;

                case DockPosition.Bottom:
                    panel.Dock = DockStyle.Fill;
                    _bottomContainer.Controls.Add(panel);
                    _bottomContainer.Visible = true;
                    _bottomSplitter.Visible = true;
                    break;

                case DockPosition.Top:
                    panel.Dock = DockStyle.Fill;
                    _topContainer.Controls.Add(panel);
                    _topContainer.Visible = true;
                    _topSplitter.Visible = true;
                    break;

                case DockPosition.Document:
                    var page = new TabPageEx(panel.Title) { Closable = panel.Closable };
                    panel.Dock = DockStyle.Fill;
                    page.Controls.Add(panel);
                    _documentTabControl.TabPages.Add(page);
                    break;

                case DockPosition.Float:
                    FloatPanel(panel);
                    break;
            }
        }

        public void FloatPanel(DockPanelControl panel) => FloatPanel(panel, null);

        public void FloatPanel(DockPanelControl panel, Rectangle? initialBounds)
        {
            if (panel == null) return;
            if (panel.Parent != null)
            {
                panel.Parent.Controls.Remove(panel);
            }

            panel.DockPosition = DockPosition.Float;
            var floatWin = new FloatingWindow(this, panel, initialBounds);
            _floatingWindows.Add(floatWin);
            floatWin.FormClosed += (s, e) => _floatingWindows.Remove(floatWin);
            floatWin.Show(this);
            RebuildLayout();
        }

        private void Panel_FloatRequested(object? sender, EventArgs e)
        {
            if (sender is DockPanelControl panel)
            {
                FloatPanel(panel);
            }
        }

        private void Panel_CloseRequested(object? sender, EventArgs e)
        {
            if (sender is DockPanelControl panel)
            {
                RemovePanel(panel);
            }
        }

        private void Panel_PinStateChanged(object? sender, EventArgs e)
        {
            if (sender is DockPanelControl panel)
            {
                if (!panel.IsPinned)
                {
                    if (panel.Parent != null)
                    {
                        panel.Parent.Controls.Remove(panel);
                    }
                    if (_activeDrawerPanel == panel)
                    {
                        _drawerOverlay.Controls.Clear();
                        _drawerOverlay.Visible = false;
                        _activeDrawerPanel = null;
                    }
                }
                else
                {
                    if (_activeDrawerPanel == panel)
                    {
                        _drawerOverlay.Controls.Clear();
                        _drawerOverlay.Visible = false;
                        _activeDrawerPanel = null;
                    }
                    ArrangePanel(panel);
                }
                RebuildLayout();
            }
        }

        private void OnLeftAutoHideBarPaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var palette = ZeroTheme.Colors;

            using var bgBrush = new SolidBrush(palette.HeaderBackground);
            g.FillRectangle(bgBrush, _leftAutoHideBar.ClientRectangle);
            using var borderPen = new Pen(palette.Border);
            g.DrawLine(borderPen, _leftAutoHideBar.Width - 1, 0, _leftAutoHideBar.Width - 1, _leftAutoHideBar.Height);

            int curY = 6;
            using var font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold);

            foreach (var p in _panels)
            {
                if (!p.IsPinned && p.DockPosition == DockPosition.Left)
                {
                    var textSz = g.MeasureString(p.Title, font);
                    int tabH = (int)textSz.Width + 24;
                    var tabRect = new Rectangle(2, curY, _leftAutoHideBar.Width - 4, tabH);

                    bool isActive = _activeDrawerPanel == p;
                    if (isActive)
                    {
                        using var activeBrush = new SolidBrush(palette.Primary);
                        g.FillRectangle(activeBrush, tabRect);
                    }

                    using var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center,
                        FormatFlags = StringFormatFlags.DirectionVertical
                    };
                    using var tb = new SolidBrush(isActive ? Color.White : palette.TextPrimary);
                    g.DrawString(p.Title, font, tb, tabRect, sf);

                    curY += tabH + 6;
                }
            }
        }

        private void OnLeftAutoHideBarMouseDown(object? sender, MouseEventArgs e)
        {
            int curY = 6;
            using var g = _leftAutoHideBar.CreateGraphics();
            using var font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold);

            foreach (var p in _panels)
            {
                if (!p.IsPinned && p.DockPosition == DockPosition.Left)
                {
                    var textSz = g.MeasureString(p.Title, font);
                    int tabH = (int)textSz.Width + 24;
                    var tabRect = new Rectangle(2, curY, _leftAutoHideBar.Width - 4, tabH);

                    if (tabRect.Contains(e.Location))
                    {
                        ToggleDrawer(p, isLeft: true);
                        return;
                    }
                    curY += tabH + 6;
                }
            }
        }

        private void OnRightAutoHideBarPaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var palette = ZeroTheme.Colors;

            using var bgBrush = new SolidBrush(palette.HeaderBackground);
            g.FillRectangle(bgBrush, _rightAutoHideBar.ClientRectangle);
            using var borderPen = new Pen(palette.Border);
            g.DrawLine(borderPen, 0, 0, 0, _rightAutoHideBar.Height);

            int curY = 6;
            using var font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold);

            foreach (var p in _panels)
            {
                if (!p.IsPinned && p.DockPosition == DockPosition.Right)
                {
                    var textSz = g.MeasureString(p.Title, font);
                    int tabH = (int)textSz.Width + 24;
                    var tabRect = new Rectangle(2, curY, _rightAutoHideBar.Width - 4, tabH);

                    bool isActive = _activeDrawerPanel == p;
                    if (isActive)
                    {
                        using var activeBrush = new SolidBrush(palette.Primary);
                        g.FillRectangle(activeBrush, tabRect);
                    }

                    using var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center,
                        FormatFlags = StringFormatFlags.DirectionVertical
                    };
                    using var tb = new SolidBrush(isActive ? Color.White : palette.TextPrimary);
                    g.DrawString(p.Title, font, tb, tabRect, sf);

                    curY += tabH + 6;
                }
            }
        }

        private void OnRightAutoHideBarMouseDown(object? sender, MouseEventArgs e)
        {
            int curY = 6;
            using var g = _rightAutoHideBar.CreateGraphics();
            using var font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold);

            foreach (var p in _panels)
            {
                if (!p.IsPinned && p.DockPosition == DockPosition.Right)
                {
                    var textSz = g.MeasureString(p.Title, font);
                    int tabH = (int)textSz.Width + 24;
                    var tabRect = new Rectangle(2, curY, _rightAutoHideBar.Width - 4, tabH);

                    if (tabRect.Contains(e.Location))
                    {
                        ToggleDrawer(p, isLeft: false);
                        return;
                    }
                    curY += tabH + 6;
                }
            }
        }

        private void ToggleDrawer(DockPanelControl panel, bool isLeft)
        {
            if (_activeDrawerPanel == panel)
            {
                _drawerOverlay.Controls.Clear();
                _drawerOverlay.Visible = false;
                _activeDrawerPanel = null;
            }
            else
            {
                _drawerOverlay.Controls.Clear();
                panel.Dock = DockStyle.Fill;
                _drawerOverlay.Controls.Add(panel);
                int drawerW = Math.Min(300, Width / 2);
                int drawerX = isLeft ? _leftAutoHideBar.Right : (Width - _rightAutoHideBar.Width - drawerW);
                _drawerOverlay.SetBounds(drawerX, 0, drawerW, Height);
                _drawerOverlay.BringToFront();
                _drawerOverlay.Visible = true;
                _activeDrawerPanel = panel;
            }
            _leftAutoHideBar.Invalidate();
            _rightAutoHideBar.Invalidate();
        }

        public void RebuildLayout()
        {
            _leftContainer.Visible = _leftContainer.Controls.Count > 0;
            _leftSplitter.Visible = _leftContainer.Visible;

            _rightContainer.Visible = _rightContainer.Controls.Count > 0;
            _rightSplitter.Visible = _rightContainer.Visible;

            _bottomContainer.Visible = _bottomContainer.Controls.Count > 0;
            _bottomSplitter.Visible = _bottomContainer.Visible;

            _topContainer.Visible = _topContainer.Controls.Count > 0;
            _topSplitter.Visible = _topContainer.Visible;

            bool hasUnpinnedLeft = false;
            bool hasUnpinnedRight = false;
            foreach (var p in _panels)
            {
                if (!p.IsPinned)
                {
                    if (p.DockPosition == DockPosition.Left) hasUnpinnedLeft = true;
                    if (p.DockPosition == DockPosition.Right) hasUnpinnedRight = true;
                }
            }
            _leftAutoHideBar.Visible = hasUnpinnedLeft;
            _rightAutoHideBar.Visible = hasUnpinnedRight;

            _leftAutoHideBar.Invalidate();
            _rightAutoHideBar.Invalidate();
            Invalidate(true);
        }

        #region Dock Layout Serialization & Persistence

        /// <summary>
        /// Captures the complete current docking layout state (containers, split ratios, panel positions, auto-hide tabs, floating coordinates).
        /// </summary>
        public WorkspaceLayoutState SaveLayout()
        {
            var state = new WorkspaceLayoutState
            {
                Version = "1.1",
                SavedAt = DateTime.UtcNow,
                Containers = new DockContainerLayoutState
                {
                    LeftWidth = _leftContainer.Width,
                    RightWidth = _rightContainer.Width,
                    TopHeight = _topContainer.Height,
                    BottomHeight = _bottomContainer.Height,
                    ActiveDocumentTitle = _documentTabControl.SelectedTab?.Text ?? string.Empty
                }
            };

            for (int i = 0; i < _panels.Count; i++)
            {
                var p = _panels[i];
                var pState = new DockPanelLayoutState
                {
                    Name = p.PanelKey,
                    Title = p.Title,
                    DockPosition = p.DockPosition.ToString(),
                    IsPinned = p.IsPinned,
                    AutoHide = p.AutoHide,
                    Closable = p.Closable,
                    Floatable = p.Floatable,
                    Width = p.Width,
                    Height = p.Height,
                    OrderIndex = i
                };

                if (p.DockPosition == DockPosition.Float)
                {
                    var win = _floatingWindows.Find(f => f.DockPanel == p);
                    if (win != null && !win.IsDisposed)
                    {
                        pState.FloatX = win.Location.X;
                        pState.FloatY = win.Location.Y;
                        pState.FloatWidth = win.Width;
                        pState.FloatHeight = win.Height;
                    }
                }

                state.DockPanels.Add(pState);
            }

            return state;
        }

        /// <summary>
        /// Restores docking layout from a saved WorkspaceLayoutState.
        /// Rebuilds split containers, document tabs, auto-hide sidebars, and floating windows safely.
        /// </summary>
        public void RestoreLayout(WorkspaceLayoutState state)
        {
            if (state == null) return;

            SuspendLayout();
            try
            {
                // 1. Restore container dimensions
                if (state.Containers != null)
                {
                    if (state.Containers.LeftWidth > 0) _leftContainer.Width = state.Containers.LeftWidth;
                    if (state.Containers.RightWidth > 0) _rightContainer.Width = state.Containers.RightWidth;
                    if (state.Containers.TopHeight > 0) _topContainer.Height = state.Containers.TopHeight;
                    if (state.Containers.BottomHeight > 0) _bottomContainer.Height = state.Containers.BottomHeight;
                }

                // 2. Build lookup map of registered panels by key and title
                var panelLookup = new Dictionary<string, DockPanelControl>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in _panels)
                {
                    string key = p.PanelKey;
                    panelLookup[key] = p;
                    if (!panelLookup.ContainsKey(p.Title))
                    {
                        panelLookup[p.Title] = p;
                    }
                }

                // 3. Clear existing attachments from containers and tabs
                _leftContainer.Controls.Clear();
                _rightContainer.Controls.Clear();
                _topContainer.Controls.Clear();
                _bottomContainer.Controls.Clear();
                _documentTabControl.TabPages.Clear();
                _drawerOverlay.Controls.Clear();
                _drawerOverlay.Visible = false;
                _activeDrawerPanel = null;

                var oldFloats = new List<FloatingWindow>(_floatingWindows);
                foreach (var fw in oldFloats)
                {
                    try { fw.Close(); } catch { }
                }
                _floatingWindows.Clear();

                // 4. Re-dock panels according to saved layout
                var appliedPanels = new HashSet<DockPanelControl>();

                foreach (var pState in state.DockPanels)
                {
                    string key = !string.IsNullOrEmpty(pState.Name) ? pState.Name : pState.Title;
                    if (panelLookup.TryGetValue(key, out var panel) && appliedPanels.Add(panel))
                    {
                        if (Enum.TryParse<DockPosition>(pState.DockPosition, true, out var pos))
                        {
                            panel.DockPosition = pos;
                        }
                        else
                        {
                            panel.DockPosition = DockPosition.Document;
                        }

                        panel.IsPinned = pState.IsPinned;
                        panel.AutoHide = pState.AutoHide;
                        panel.Closable = pState.Closable;
                        panel.Floatable = pState.Floatable;
                        if (pState.Width > 0) panel.Width = pState.Width;
                        if (pState.Height > 0) panel.Height = pState.Height;

                        if (panel.DockPosition == DockPosition.Float)
                        {
                            FloatPanel(panel, new Rectangle(pState.FloatX, pState.FloatY, pState.FloatWidth, pState.FloatHeight));
                        }
                        else if (!panel.IsPinned && (panel.DockPosition == DockPosition.Left || panel.DockPosition == DockPosition.Right))
                        {
                            // Unpinned auto-hide panel lives in sidebar until clicked
                        }
                        else
                        {
                            ArrangePanel(panel);
                        }
                    }
                }

                // 5. Arrange any panels not explicitly saved in layout
                foreach (var p in _panels)
                {
                    if (!appliedPanels.Contains(p))
                    {
                        ArrangePanel(p);
                    }
                }

                // 6. Restore active document tab
                if (state.Containers != null && !string.IsNullOrEmpty(state.Containers.ActiveDocumentTitle))
                {
                    for (int i = 0; i < _documentTabControl.TabPages.Count; i++)
                    {
                        if (string.Equals(_documentTabControl.TabPages[i].Text, state.Containers.ActiveDocumentTitle, StringComparison.OrdinalIgnoreCase))
                        {
                            _documentTabControl.SelectedIndex = i;
                            break;
                        }
                    }
                }

                RebuildLayout();
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        /// <summary>
        /// Serializes the current docking layout state directly to a formatted JSON string.
        /// </summary>
        public string SaveLayoutToJson()
        {
            var state = SaveLayout();
            return ZeroWorkspaceSerializer.Serialize(state);
        }

        /// <summary>
        /// Restores docking layout directly from a JSON string.
        /// </summary>
        public void RestoreLayoutFromJson(string json)
        {
            var state = ZeroWorkspaceSerializer.Deserialize(json);
            RestoreLayout(state);
        }

        /// <summary>
        /// Saves the current layout to a JSON file on disk.
        /// </summary>
        public void SaveLayout(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            string json = SaveLayoutToJson();
            System.IO.File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Restores the docking layout from a JSON file on disk if it exists.
        /// </summary>
        public void RestoreLayout(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (System.IO.File.Exists(filePath))
            {
                string json = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                RestoreLayoutFromJson(json);
            }
        }

        /// <summary>
        /// Writes the serialized layout to a stream.
        /// </summary>
        public void SaveLayoutToStream(System.IO.Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            string json = SaveLayoutToJson();
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Restores the layout by reading from an input stream.
        /// </summary>
        public void RestoreLayoutFromStream(System.IO.Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            using (var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8, true, 1024, true))
            {
                string json = reader.ReadToEnd();
                RestoreLayoutFromJson(json);
            }
        }

        #endregion
    
    private void OnThemeChanged(object sender, EventArgs e)
    {
                BackColor = ZeroTheme.Colors.Background;
                _drawerOverlay.BackColor = ZeroTheme.Colors.Surface;
                _leftSplitter.BackColor = ZeroTheme.Colors.Border;
                _rightSplitter.BackColor = ZeroTheme.Colors.Border;
                _topSplitter.BackColor = ZeroTheme.Colors.Border;
                _bottomSplitter.BackColor = ZeroTheme.Colors.Border;
                Invalidate(true);
            }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }

}

    [Obsolete("Use DockPosition instead.")]
    public enum ZeroDockPosition
    {
        Left = DockPosition.Left,
        Right = DockPosition.Right,
        Top = DockPosition.Top,
        Bottom = DockPosition.Bottom,
        Document = DockPosition.Document,
        Float = DockPosition.Float
    }

    [Obsolete("Use DockPanelControl instead.")]
    [ToolboxItem(false)]
    public class ZeroDockPanel : DockPanelControl
    {
        public new ZeroDockPosition DockPosition
        {
            get => (ZeroDockPosition)base.DockPosition;
            set => base.DockPosition = (DockPosition)value;
        }
    }

    [Obsolete("Use FloatingWindow instead.")]
    [ToolboxItem(false)]
    public class ZeroFloatingWindow : FloatingWindow
    {
        public ZeroFloatingWindow(DockManager dockManager, DockPanelControl panel, Rectangle? initialBounds = null)
            : base(dockManager, panel, initialBounds)
        {
        }

        public ZeroFloatingWindow(DockManager dockManager, DockPanelControl panel)
            : base(dockManager, panel)
        {
        }

        public void RedockToManager(ZeroDockPosition targetPosition)
            => base.RedockToManager((DockPosition)targetPosition);
    }

    [Obsolete("Use DockManager instead.")]
    public class ZeroDockManager : DockManager
    {
        public void AddPanel(DockPanelControl panel, ZeroDockPosition position)
            => base.AddPanel(panel, (DockPosition)position);
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZDockManager"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DockManager is deprecated and will be removed in 5 release cycles. Please migrate to ZDockManager instead.")]
    [ToolboxItem(false)]
    public class DockManager : ZDockManager
    {
    }

    #endregion
}
