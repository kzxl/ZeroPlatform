using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Containers
{
    /// <summary>
    /// Style of the border outline for <see cref="ZPanel"/>.
    /// </summary>
    public enum PanelBorderStyle
    {
        None = 0,
        Solid = 1,
        Dotted = 2,
        Dashed = 3
    }

    /// <summary>
    /// Semantic theme background mode for <see cref="ZPanel"/>.
    /// </summary>
    public enum PanelType
    {
        Surface = 0,
        Card = 1,
        Header = 2,
        Transparent = 3,
        Custom = 4
    }

    #region Backward Compatibility Enums (5-Release Deprecation)

    [Obsolete("ZeroPanelBorderStyle is deprecated and will be removed in 5 release cycles. Use PanelBorderStyle instead.")]
    public enum ZeroPanelBorderStyle
    {
        None = 0,
        Solid = 1,
        Dotted = 2,
        Dashed = 3
    }

    [Obsolete("ZeroPanelType is deprecated and will be removed in 5 release cycles. Use PanelType instead.")]
    public enum ZeroPanelType
    {
        Surface = 0,
        Card = 1,
        Header = 2,
        Transparent = 3,
        Custom = 4
    }

    #endregion

    /// <summary>
    /// Modern theme-aware container panel with customizable border styles, rounded corners,
    /// High-DPI Per-Monitor V2 scaling, and automatic Dark/Light theme reactivity.
    /// Canonical drop-in replacement for standard <see cref="System.Windows.Forms.Panel"/>.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Containers")]
    [Description("Modern theme-aware container panel with border, radius, and DPI scaling")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroDefaultControl.bmp")]
    [Designer("ZeroUI.WinForms.Design.Common.ZPanelDesigner, ZeroUI.WinForms.Design")]
    public class ZPanel : Panel, IZeroDpiScalable
    {
        private PanelBorderStyle _borderStyle = PanelBorderStyle.Solid;
        private PanelType _panelType = PanelType.Surface;
        private int _baseBorderRadius = 0;
        private int _borderRadius = 0;
        private float _borderThickness = 1f;
        private Color _customBorderColor = Color.Empty;
        private Color _customBackColor = Color.Empty;
        private float _currentDpiScale = 1.0f;

        [Category("Appearance")]
        [DefaultValue(PanelBorderStyle.Solid)]
        [Description("Visual style of the panel border.")]
        public new PanelBorderStyle BorderStyle
        {
            get => _borderStyle;
            set
            {
                if (_borderStyle != value)
                {
                    _borderStyle = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(PanelType.Surface)]
        [Description("Semantic background surface type according to the active ZeroTheme.")]
        public PanelType PanelType
        {
            get => _panelType;
            set
            {
                if (_panelType != value)
                {
                    _panelType = value;
                    UpdateThemeColors();
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(0)]
        [Description("Radius of rounded panel corners in pixels (0 = square corners).")]
        public int BorderRadius
        {
            get => _baseBorderRadius;
            set
            {
                int val = Math.Max(0, value);
                if (_baseBorderRadius != val)
                {
                    _baseBorderRadius = val;
                    _borderRadius = (int)Math.Round(_baseBorderRadius * _currentDpiScale);
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(1f)]
        [Description("Border thickness in pixels.")]
        public float BorderThickness
        {
            get => _borderThickness;
            set
            {
                float val = Math.Max(0f, value);
                if (Math.Abs(_borderThickness - val) > 0.001f)
                {
                    _borderThickness = val;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(typeof(Color), "")]
        [Description("Custom border color override. Leave empty to follow active theme border.")]
        public Color BorderColor
        {
            get => _customBorderColor;
            set
            {
                if (_customBorderColor != value)
                {
                    _customBorderColor = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(typeof(Color), "")]
        [Description("Custom background color override. Leave empty to follow PanelType theme tokens.")]
        public Color CustomBackColor
        {
            get => _customBackColor;
            set
            {
                if (_customBackColor != value)
                {
                    _customBackColor = value;
                    Invalidate();
                }
            }
        }

        public ZPanel()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            base.BorderStyle = System.Windows.Forms.BorderStyle.None;
            BackColor = Color.Transparent;

            UpdateThemeColors();

            ZeroTheme.ThemeChanged += OnThemeChanged;
            ZeroUIConfig.CornerStyleChanged += OnCornerStyleChanged;
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            if (IsDisposed || Disposing) return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    UpdateThemeColors();
                    Invalidate();
                }));
            }
            else
            {
                UpdateThemeColors();
                Invalidate();
            }
        }

        private void OnCornerStyleChanged(object? sender, EventArgs e)
        {
            if (IsDisposed || Disposing) return;
            Invalidate();
        }

        private void UpdateThemeColors()
        {
            if (_customBackColor != Color.Empty)
            {
                return;
            }

            switch (_panelType)
            {
                case PanelType.Surface:
                    BackColor = ZeroTheme.Colors.Surface;
                    break;
                case PanelType.Card:
                    BackColor = ZeroTheme.Colors.CardBackground;
                    break;
                case PanelType.Header:
                    BackColor = ZeroTheme.Colors.HeaderBackground;
                    break;
                case PanelType.Transparent:
                    BackColor = Color.Transparent;
                    break;
                case PanelType.Custom:
                    break;
            }
        }

        public override Rectangle DisplayRectangle
        {
            get
            {
                int borderW = (_borderStyle != PanelBorderStyle.None && _borderThickness > 0) ? (int)Math.Ceiling(_borderThickness) : 0;
                int padLeft = Padding.Left + borderW;
                int padRight = Padding.Right + borderW;
                int padTop = Padding.Top + borderW;
                int padBottom = Padding.Bottom + borderW;
                return new Rectangle(padLeft, padTop, Math.Max(0, Width - padLeft - padRight), Math.Max(0, Height - padTop - padBottom));
            }
        }

        [Browsable(false)]
        public float DpiScale => _currentDpiScale;

        public void ApplyDpiScaling(float scaleFactor)
        {
            if (scaleFactor <= 0f) scaleFactor = 1.0f;
            _currentDpiScale = scaleFactor;
            _borderRadius = (int)Math.Round(_baseBorderRadius * scaleFactor);
            _borderThickness = Math.Max(1f, _borderThickness * scaleFactor);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (rect.Width <= 0 || rect.Height <= 0) return;

            // Determine effective radius
            int effRadius = _borderRadius;
            if (effRadius == 0 && ZeroUIConfig.CornerStyle == ZeroCornerStyle.Rounded && _panelType != PanelType.Transparent)
            {
                effRadius = (int)Math.Round(6 * _currentDpiScale);
            }

            Color effectiveBgColor = _customBackColor != Color.Empty ? _customBackColor :
                _panelType switch
                {
                    PanelType.Card => ZeroTheme.Colors.CardBackground,
                    PanelType.Header => ZeroTheme.Colors.HeaderBackground,
                    PanelType.Transparent => Color.Transparent,
                    _ => ZeroTheme.Colors.Surface
                };

            Color effectiveBorderColor = _customBorderColor != Color.Empty ? _customBorderColor : ZeroTheme.Colors.Border;

            // 1. Draw Background
            if (effectiveBgColor != Color.Transparent)
            {
                using var bgBrush = new SolidBrush(effectiveBgColor);
                if (effRadius > 0)
                {
                    using var path = ZeroUIConfig.CreateRoundedRectangle(rect, effRadius);
                    g.FillPath(bgBrush, path);
                }
                else
                {
                    g.FillRectangle(bgBrush, new Rectangle(0, 0, Width, Height));
                }
            }

            // 2. Draw Border
            if (_borderStyle != PanelBorderStyle.None && _borderThickness > 0)
            {
                using var pen = new Pen(effectiveBorderColor, _borderThickness)
                {
                    Alignment = PenAlignment.Inset,
                    DashStyle = _borderStyle switch
                    {
                        PanelBorderStyle.Dotted => DashStyle.Dot,
                        PanelBorderStyle.Dashed => DashStyle.Dash,
                        _ => DashStyle.Solid
                    }
                };

                if (effRadius > 0)
                {
                    using var path = ZeroUIConfig.CreateRoundedRectangle(rect, effRadius);
                    g.DrawPath(pen, path);
                }
                else
                {
                    g.DrawRectangle(pen, rect);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= OnThemeChanged;
                ZeroUIConfig.CornerStyleChanged -= OnCornerStyleChanged;
            }
            base.Dispose(disposing);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZPanel"/>.
    /// </summary>
    [Obsolete("PanelControl is deprecated and will be removed in 5 release cycles. Please migrate to ZPanel instead.")]
    public class PanelControl : ZPanel { }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZPanel"/>.
    /// </summary>
    [Obsolete("ZeroPanel is deprecated and will be removed in 5 release cycles. Please migrate to ZPanel instead.")]
    public class ZeroPanel : ZPanel { }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZPanel"/>.
    /// </summary>
    [Obsolete("ZeroPanelControl is deprecated and will be removed in 5 release cycles. Please migrate to ZPanel instead.")]
    public class ZeroPanelControl : ZPanel { }

    /// <summary>
    /// Convenience alias for <see cref="ZPanel"/>.
    /// </summary>
    public class ZPanelControl : ZPanel { }

    #endregion
}
