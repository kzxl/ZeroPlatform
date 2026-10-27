using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Feedback
{
    /// <summary>
    /// Specifies the visual arrangement of the spinner and text labels inside <see cref="ZProgressPanel"/>.
    /// </summary>
    public enum ProgressOrientation
    {
        Horizontal,
        Vertical
    }

    /// <summary>
    /// Modern, lightweight in-place progress indicator panel for WinForms.
    /// Displays a smooth animated ring spinner accompanied by primary caption and secondary description.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Feedback")]
    [Description("Modern animated progress indicator panel with caption and description")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroProgressPanel.bmp")]
    public class ZProgressPanel : Control
    {
        private string _caption = "Please wait";
        private string _description = "Loading data...";
        private int? _progress;
        private ProgressOrientation _orientation = ProgressOrientation.Horizontal;
        private readonly Timer _animTimer;
        private float _spinnerAngle = 0f;
        private float _dpiScale = 1.0f;
        private bool _showDescription = true;

        [Category("Appearance")]
        [DefaultValue("Please wait")]
        public string Caption
        {
            get => _caption;
            set
            {
                if (_caption != value)
                {
                    _caption = value ?? string.Empty;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue("Loading data...")]
        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value ?? string.Empty;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowDescription
        {
            get => _showDescription;
            set
            {
                if (_showDescription != value)
                {
                    _showDescription = value;
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(null)]
        public int? ProgressPercentage
        {
            get => _progress;
            set
            {
                _progress = value.HasValue ? Math.Max(0, Math.Min(100, value.Value)) : null;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(ProgressOrientation.Horizontal)]
        public ProgressOrientation Orientation
        {
            get => _orientation;
            set
            {
                if (_orientation != value)
                {
                    _orientation = value;
                    Invalidate();
                }
            }
        }

        public ZProgressPanel()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Size = new Size(240, 56);

            using (var g = CreateGraphics())
            {
                _dpiScale = Math.Max(1.0f, g.DpiX / 96.0f);
            }

            _animTimer = new Timer { Interval = 25 }; // ~40 FPS smooth spinner
            _animTimer.Tick += (s, e) =>
            {
                _spinnerAngle = (_spinnerAngle + 12f) % 360f;
                Invalidate();
            };
            _animTimer.Start();

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Palette;

            int spinnerSize = (int)Math.Round(32 * _dpiScale);

            if (_orientation == ProgressOrientation.Horizontal)
            {
                // Horizontal Layout
                int spinnerX = 8;
                int spinnerY = (Height - spinnerSize) / 2;
                Rectangle spinnerRect = new Rectangle(spinnerX, spinnerY, spinnerSize, spinnerSize);

                DrawSpinner(g, spinnerRect, palette);

                int textLeft = spinnerRect.Right + (int)Math.Round(12 * _dpiScale);
                int textW = Width - textLeft - 8;

                if (_showDescription && !string.IsNullOrEmpty(_description))
                {
                    using var capFont = new Font("Segoe UI", 9.5f * _dpiScale, FontStyle.Bold);
                    Rectangle capRect = new Rectangle(textLeft, (Height / 2) - (int)Math.Round(18 * _dpiScale), textW, (int)Math.Round(18 * _dpiScale));
                    TextRenderer.DrawText(g, _caption, capFont, capRect, palette.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    using var descFont = new Font("Segoe UI", 8.25f * _dpiScale, FontStyle.Regular);
                    string desc = _progress.HasValue ? $"{_description} ({_progress.Value}%)" : _description;
                    Rectangle descRect = new Rectangle(textLeft, (Height / 2) + 1, textW, (int)Math.Round(16 * _dpiScale));
                    TextRenderer.DrawText(g, desc, descFont, descRect, palette.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
                else
                {
                    using var capFont = new Font("Segoe UI", 10f * _dpiScale, FontStyle.Bold);
                    Rectangle capRect = new Rectangle(textLeft, (Height - (int)Math.Round(24 * _dpiScale)) / 2, textW, (int)Math.Round(24 * _dpiScale));
                    TextRenderer.DrawText(g, _caption, capFont, capRect, palette.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
            else
            {
                // Vertical Layout
                int spinnerX = (Width - spinnerSize) / 2;
                int spinnerY = 8;
                Rectangle spinnerRect = new Rectangle(spinnerX, spinnerY, spinnerSize, spinnerSize);

                DrawSpinner(g, spinnerRect, palette);

                int textTop = spinnerRect.Bottom + (int)Math.Round(8 * _dpiScale);
                using var capFont = new Font("Segoe UI", 9.5f * _dpiScale, FontStyle.Bold);
                Rectangle capRect = new Rectangle(4, textTop, Width - 8, (int)Math.Round(18 * _dpiScale));
                TextRenderer.DrawText(g, _caption, capFont, capRect, palette.TextPrimary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                if (_showDescription && !string.IsNullOrEmpty(_description))
                {
                    using var descFont = new Font("Segoe UI", 8.25f * _dpiScale, FontStyle.Regular);
                    string desc = _progress.HasValue ? $"{_description} ({_progress.Value}%)" : _description;
                    Rectangle descRect = new Rectangle(4, capRect.Bottom + 2, Width - 8, (int)Math.Round(16 * _dpiScale));
                    TextRenderer.DrawText(g, desc, descFont, descRect, palette.TextSecondary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
        }

        private void DrawSpinner(Graphics g, Rectangle rect, ZeroThemePalette palette)
        {
            using (var trackPen = new Pen(palette.Border, (int)Math.Round(3f * _dpiScale)))
            {
                g.DrawEllipse(trackPen, rect);
            }

            using (var spinPen = new Pen(palette.Primary, (int)Math.Round(3f * _dpiScale)))
            {
                spinPen.StartCap = LineCap.Round;
                spinPen.EndCap = LineCap.Round;
                float sweep = _progress.HasValue ? (_progress.Value / 100f * 360f) : 110f;
                g.DrawArc(spinPen, rect, _spinnerAngle, sweep);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
                _animTimer.Stop();
                _animTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    
    private void OnThemeChanged(object sender, EventArgs e) => Invalidate();

}

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZProgressPanel"/>.
    /// </summary>
    [Obsolete("Use ProgressPanel instead.")]
    public class ProgressPanelControl : ProgressPanel { }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZProgressPanel"/>.
    /// </summary>
    [Obsolete("Use ProgressPanel instead.")]
    public class ZeroProgressPanel : ZProgressPanel { }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZProgressPanel"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ProgressPanel is deprecated and will be removed in 5 release cycles. Please migrate to ZProgressPanel instead.")]
    [ToolboxItem(false)]
    public class ProgressPanel : ZProgressPanel
    {
    }

    #endregion
}
