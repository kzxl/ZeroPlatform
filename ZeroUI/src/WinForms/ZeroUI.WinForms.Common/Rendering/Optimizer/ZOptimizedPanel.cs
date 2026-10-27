using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Rendering.Optimizer;

namespace ZeroUI.WinForms.Rendering.Optimizer
{
    /// <summary>
    /// Industrial Smart Card Panel for Windows Forms, powered by ZeroUI's Automatic Render Optimizer.
    /// Eliminates GDI+ software blur lag by utilizing pre-computed 9-slice analytical shadow geometry from ZeroShadowAtlas.
    /// Preserves crisp ClearType typography for child controls while delivering modern elevated card aesthetics.
    /// </summary>
    [ToolboxItem(true)]
    public class ZOptimizedPanel : Panel
    {
        private float _elevation = 6f;
        private float _blurRadius = 12f;
        private float _glowIntensity = 0f;
        private Color _glowColor = Color.FromArgb(0, 229, 255);
        private float _cornerRadius = 8f;
        private Color _cardBackColor = Color.FromArgb(22, 27, 38);
        private Color _cardBorderColor = Color.FromArgb(42, 51, 71);
        private float _cardBorderWidth = 1f;

        [Category("ZeroUI Optimizer")]
        [DefaultValue(6f)]
        public float Elevation
        {
            get => _elevation;
            set { _elevation = Math.Max(0f, value); Invalidate(); }
        }

        [Category("ZeroUI Optimizer")]
        [DefaultValue(12f)]
        public float BlurRadius
        {
            get => _blurRadius;
            set { _blurRadius = Math.Max(0f, value); Invalidate(); }
        }

        [Category("ZeroUI Optimizer")]
        [DefaultValue(0f)]
        public float GlowIntensity
        {
            get => _glowIntensity;
            set { _glowIntensity = Math.Max(0f, value); Invalidate(); }
        }

        [Category("ZeroUI Optimizer")]
        public Color GlowColor
        {
            get => _glowColor;
            set { _glowColor = value; Invalidate(); }
        }

        [Category("ZeroUI Optimizer")]
        [DefaultValue(8f)]
        public float CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = Math.Max(0f, value); Invalidate(); }
        }

        [Category("ZeroUI Optimizer")]
        public Color CardBackColor
        {
            get => _cardBackColor;
            set { _cardBackColor = value; Invalidate(); }
        }

        [Category("ZeroUI Optimizer")]
        public Color CardBorderColor
        {
            get => _cardBorderColor;
            set { _cardBorderColor = value; Invalidate(); }
        }

        [Category("ZeroUI Optimizer")]
        [DefaultValue(1f)]
        public float CardBorderWidth
        {
            get => _cardBorderWidth;
            set { _cardBorderWidth = Math.Max(0f, value); Invalidate(); }
        }

        [Browsable(false)]
        public RenderDecision LastDecision { get; private set; }

        public ZOptimizedPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);

            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Padding = new Padding(16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = Width;
            int h = Height;
            if (w <= 0 || h <= 0) return;

            // 1. Analyze profile via ZeroRenderAnalyzer
            var profile = RenderOperationProfile.ForCard(
                w, h,
                elevation: _elevation,
                cornerRadius: _cornerRadius,
                blurRadius: _blurRadius,
                glowIntensity: _glowIntensity,
                hasText: HasChildren,
                isAnimated: false,
                batchCount: 1);

            LastDecision = ZeroRenderAnalyzer.Evaluate(profile);

            // 2. Draw Glow if enabled
            if (_glowIntensity > 0.01f)
            {
                DrawGlow(g, w, h);
            }

            // 3. Draw Shadow Pass using 9-slice cached atlas geometry
            if (_elevation > 0.01f)
            {
                DrawAtlasShadow(g, w, h);
            }

            // 4. Draw Card Body & Border
            using (var path = CreateRoundedRectanglePath(0, 0, w - 1, h - 1, _cornerRadius))
            {
                using (var brush = new SolidBrush(_cardBackColor))
                {
                    g.FillPath(brush, path);
                }

                if (_cardBorderWidth > 0 && _cardBorderColor != Color.Transparent)
                {
                    using (var pen = new Pen(_cardBorderColor, _cardBorderWidth))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            base.OnPaint(e);
        }

        private void DrawGlow(Graphics g, int w, int h)
        {
            float spread = Math.Max(3f, (_blurRadius + _elevation) * 0.6f);
            int steps = 3;
            for (int i = steps; i >= 1; i--)
            {
                float s = spread * ((float)i / steps);
                float intensity = ZeroShaderRegistry.EvaluateNeonGlowIntensity(s, spread, _glowIntensity);
                int alpha = Math.Min(255, Math.Max(0, (int)(intensity * 110)));
                if (alpha == 0) continue;

                using (var path = CreateRoundedRectanglePath(-s * 0.5f, -s * 0.5f, w + s, h + s, _cornerRadius + s * 0.5f))
                using (var pen = new Pen(Color.FromArgb(alpha, _glowColor), s * 1.5f))
                {
                    g.DrawPath(pen, path);
                }
            }
        }

        private void DrawAtlasShadow(Graphics g, int w, int h)
        {
            // Retrieves cached analytical 9-slice parameters
            var patch = ZeroShadowAtlas.GetOrCreatePatch(_cornerRadius, _elevation, _blurRadius);
            float offsetY = _elevation * 0.65f;

            int steps = 3;
            for (int i = steps; i >= 1; i--)
            {
                float spread = (steps - i + 1) * (_blurRadius / 4f);
                int alpha = 20 + (i * 15);

                using (var path = CreateRoundedRectanglePath(-spread * 0.5f, offsetY - spread * 0.25f, w + spread, h + spread * 0.5f, _cornerRadius + spread * 0.5f))
                using (var brush = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
                {
                    g.FillPath(brush, path);
                }
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(float x, float y, float width, float height, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2f;

            if (d <= 0.1f)
            {
                path.AddRectangle(new RectangleF(x, y, width, height));
                return path;
            }

            d = Math.Min(d, Math.Min(width, height));

            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + width - d, y, d, d, 270, 90);
            path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
            path.AddArc(x, y + height - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZOptimizedPanel"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("OptimizedPanel is deprecated and will be removed in 5 release cycles. Please migrate to ZOptimizedPanel instead.")]
    [ToolboxItem(false)]
    public class OptimizedPanel : ZOptimizedPanel
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZOptimizedPanel"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroOptimizedPanel is deprecated. Please use ZOptimizedPanel instead.")]
    [ToolboxItem(false)]
    public class ZeroOptimizedPanel : ZOptimizedPanel
    {
    }

    #endregion
}
