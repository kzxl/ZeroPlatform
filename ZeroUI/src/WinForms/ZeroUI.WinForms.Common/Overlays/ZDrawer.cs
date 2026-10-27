using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Overlays
{
    public enum DrawerMode
    {
        /// <summary>
        /// Slides as a floating overlay above sibling controls. Zero layout recalculations on parent window, achieving 60-120 FPS.
        /// </summary>
        FloatingOverlay,

        /// <summary>
        /// Docks to the right edge and pushes sibling controls.
        /// </summary>
        DockRight
    }

    /// <summary>
    /// High-performance modern slide-out drawer panel for Master-Detail inspection and side forms.
    /// Features fluid time-based cubic easing animation, zero parent relayout storms via Floating Overlay,
    /// and automatic ZeroUI Skin Framework synchronization.
    /// <summary>
    /// High-performance modern slide-out drawer panel for Master-Detail inspection and side forms.
    /// Features fluid time-based cubic/quartic easing animation, zero parent relayout storms via Floating Overlay,
    /// 120 FPS high-refresh boost, and automatic ZeroUI Skin Framework synchronization.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Overlays")]
    [DefaultProperty("Title")]
    [Description("High-performance slide-out drawer panel with non-blocking floating overlay")]
    public class ZDrawer : ControlBase
    {
        private static readonly Font TitleFont = new Font("Segoe UI", 11f, FontStyle.Bold);
        private static readonly Font SubFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        private static readonly Font CloseFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        private string _title = "Detail Inspection";
        private string? _subtitle;
        private int _drawerWidth = 420;
        private bool _isOpen = false;
        private readonly DoubleBufferedPanel _contentPanel;
        private Rectangle _closeRect;
        private bool _isCloseHovered = false;

        private IDisposable? _animSub;
        private IDisposable? _fpsBoost;
        private double _progress = 0.0; // 0.0 (Closed) -> 1.0 (Fully Open)
        private const double AnimationDuration = 0.22; // 220 ms fluid animation standard
        private DrawerMode _mode = DrawerMode.FloatingOverlay;
        private Control? _trackedParent;

        public event EventHandler? Opened;
        public event EventHandler? Closed;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED: Windows DWM top-down double-buffering
                return cp;
            }
        }

        public ZDrawer()
        {
            Dock = DockStyle.None;
            Width = 0;
            Visible = false;

            _contentPanel = new DoubleBufferedPanel
            {
                Padding = new Padding(16),
                Location = new Point(0, 56)
            };

            Controls.Add(_contentPanel);
        }

        #region Properties

        [Category("Behavior")]
        [DefaultValue(DrawerMode.FloatingOverlay)]
        [Description("Display mode: FloatingOverlay (60-120 FPS zero parent relayout) or DockRight.")]
        public DrawerMode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    if (_mode == DrawerMode.DockRight)
                    {
                        Dock = DockStyle.Right;
                    }
                    else
                    {
                        Dock = DockStyle.None;
                    }
                    UpdateBoundsAndLayout();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue("Detail Inspection")]
        public string Title
        {
            get => _title;
            set { _title = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        public string? Subtitle
        {
            get => _subtitle;
            set { _subtitle = value; Invalidate(); }
        }

        [Category("Layout")]
        [DefaultValue(420)]
        public int DrawerWidth
        {
            get => _drawerWidth;
            set
            {
                _drawerWidth = Math.Max(200, value);
                UpdateBoundsAndLayout();
            }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Dismisses the drawer when the user clicks outside the drawer on the parent container.")]
        public bool CloseOnOutsideClick { get; set; } = true;

        [Browsable(false)]
        public bool IsOpen => _isOpen;

        [Browsable(false)]
        public Panel ContentPanel => _contentPanel;

        #endregion

        #region Public Methods

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            _progress = 0.0;
            UpdateBoundsAndLayout();
            Visible = true;
            BringToFront();
            Focus();
            StartAnimation();
            Opened?.Invoke(this, EventArgs.Empty);
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            StartAnimation();
        }

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        #endregion

        #region Animation & Layout Isolation

        private void StartAnimation()
        {
            _fpsBoost ??= ZeroAnimationClock.BoostTargetFps(120);
            _animSub ??= ZeroAnimationClock.Subscribe(OnAnimationStep);
        }

        private void StopAnimation()
        {
            _animSub?.Dispose();
            _animSub = null;
            _fpsBoost?.Dispose();
            _fpsBoost = null;
        }

        private static double EaseOutQuart(double t)
        {
            double f = 1.0 - t;
            return 1.0 - f * f * f * f;
        }

        private void OnAnimationStep(double deltaSeconds, long frameCount)
        {
            double delta = Math.Max(0.001, deltaSeconds) / AnimationDuration;

            if (_isOpen)
            {
                _progress = Math.Min(1.0, _progress + delta);
                if (_progress >= 1.0)
                {
                    _progress = 1.0;
                    StopAnimation();
                }
            }
            else
            {
                _progress = Math.Max(0.0, _progress - delta);
                if (_progress <= 0.0)
                {
                    _progress = 0.0;
                    StopAnimation();
                    Visible = false;
                    Closed?.Invoke(this, EventArgs.Empty);
                }
            }

            UpdateBoundsAndLayout();
            Invalidate();
            Update(); // Force immediate paint to avoid WM_PAINT starvation in message queue
        }

        private void UpdateBoundsAndLayout()
        {
            if (Parent == null) return;

            double eased = EaseOutQuart(_progress);

            if (_mode == DrawerMode.FloatingOverlay)
            {
                int top = 0;
                int height = Parent.ClientSize.Height;
                int hiddenLeft = Parent.ClientSize.Width;
                int currentLeft = hiddenLeft - (int)Math.Round(_drawerWidth * eased);

                // Fixed-size window sliding: Keep Width = _drawerWidth and Height = Parent.Height constant!
                // This completely eliminates WM_SIZE cascades and child layout storms during sliding.
                SetBounds(currentLeft, top, _drawerWidth, height, BoundsSpecified.All);
            }
            else
            {
                int currentWidth = (int)Math.Round(_drawerWidth * eased);
                Width = currentWidth;
            }

            // Keep child content panel fixed at full target dimensions
            _contentPanel.Width = _drawerWidth;
            _contentPanel.Height = Math.Max(0, Height - 56);
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);

            if (_trackedParent != null)
            {
                _trackedParent.Resize -= OnParentResize;
                _trackedParent.MouseDown -= OnParentMouseDown;
            }

            _trackedParent = Parent;
            if (_trackedParent != null)
            {
                _trackedParent.Resize += OnParentResize;
                _trackedParent.MouseDown += OnParentMouseDown;
                UpdateBoundsAndLayout();
            }
        }

        private void OnParentMouseDown(object? sender, MouseEventArgs e)
        {
            if (CloseOnOutsideClick && _isOpen && _mode == DrawerMode.FloatingOverlay)
            {
                if (!Bounds.Contains(e.Location))
                {
                    Close();
                }
            }
        }

        private void OnParentResize(object? sender, EventArgs e)
        {
            UpdateBoundsAndLayout();
        }

        #endregion

        #region Rendering & Interaction

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            BackColor = ColorTranslator.FromHtml(skin.Tokens.BgCard);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None; // Fast crisp rectangle and border rendering
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var skin = EffectiveSkin;
            Color bgCard = ColorTranslator.FromHtml(skin.Tokens.BgCard);
            Color borderDefault = ColorTranslator.FromHtml(skin.Tokens.BorderDefault);
            Color textPrimary = ColorTranslator.FromHtml(skin.Tokens.TextPrimary);
            Color textSecondary = ColorTranslator.FromHtml(skin.Tokens.TextSecondary);
            Color hoverBg = ColorTranslator.FromHtml(skin.Tokens.BgHover);

            // 1. Draw Surface & Structural Left Border
            using (var bgBrush = new SolidBrush(bgCard))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            using (var borderPen = new Pen(borderDefault, 1f))
            {
                g.DrawLine(borderPen, 0, 0, 0, Height);
                g.DrawLine(borderPen, 0, 56, Width, 56); // Header divider
            }

            // 2. Draw Title & Subtitle (using cached static fonts)
            int textLeft = 16;
            Rectangle titleRect = new Rectangle(textLeft, string.IsNullOrEmpty(_subtitle) ? 16 : 8, Math.Max(0, Width - textLeft - 44), 22);
            TextRenderer.DrawText(g, _title, TitleFont, titleRect, textPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (!string.IsNullOrEmpty(_subtitle))
            {
                Rectangle subRect = new Rectangle(textLeft, titleRect.Bottom + 1, Math.Max(0, Width - textLeft - 44), 18);
                TextRenderer.DrawText(g, _subtitle, SubFont, subRect, textSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            // 3. Draw Close Button (✕)
            _closeRect = new Rectangle(Width - 36, 16, 24, 24);
            if (_isCloseHovered)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var bPath = PaintHelper.CreateRoundedRectangle(_closeRect, 4);
                using var bBrush = new SolidBrush(hoverBg);
                g.FillPath(bBrush, bPath);
                g.SmoothingMode = SmoothingMode.None;
            }

            TextRenderer.DrawText(
                g,
                "✕",
                CloseFont,
                _closeRect,
                _isCloseHovered ? textPrimary : textSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_closeRect.IsEmpty && _closeRect.Contains(e.Location))
            {
                if (!_isCloseHovered)
                {
                    _isCloseHovered = true;
                    Cursor = Cursors.Hand;
                    Invalidate(_closeRect);
                }
            }
            else if (_isCloseHovered)
            {
                _isCloseHovered = false;
                Cursor = Cursors.Default;
                Invalidate(_closeRect);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isCloseHovered)
            {
                _isCloseHovered = false;
                Cursor = Cursors.Default;
                Invalidate(_closeRect);
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left && !_closeRect.IsEmpty && _closeRect.Contains(e.Location))
            {
                Close();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && _isOpen)
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopAnimation();
                if (_trackedParent != null)
                {
                    _trackedParent.Resize -= OnParentResize;
                    _trackedParent.MouseDown -= OnParentMouseDown;
                    _trackedParent = null;
                }
            }
            base.Dispose(disposing);
        }

        #endregion
    }

    /// <summary>
    /// Legacy alias for <see cref="ZDrawer"/>.
    /// </summary>
    [Obsolete("Use DrawerControl instead.")]
    [ToolboxItem(false)]
    public class ZeroDrawer : DrawerControl
    {
    }

    /// <summary>
    /// Standard alias for <see cref="ZDrawer"/>.
    /// </summary>
    public class Drawer : ZDrawer
    {
    }

    /// <summary>
    /// High-performance container panel with enabled double-buffering and zero-flicker flags.
    /// </summary>
    internal sealed class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZDrawer"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DrawerControl is deprecated and will be removed in 5 release cycles. Please migrate to ZDrawer instead.")]
    [ToolboxItem(false)]
    public class DrawerControl : ZDrawer
    {
    }

    #endregion
}
