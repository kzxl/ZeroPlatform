using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Scada.Safety;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    /// <summary>
    /// Industrial multi-state pilot light indicator compliant with ISA-101 and IEC 60073.
    /// Features optical glass Fresnel sheen, realistic outer bezel, configurable blink/pulse rates,
    /// customizable geometry shapes (Circle, Square, RoundedRect), and zero-alloc animation.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroLed.bmp")]
    [Category("ZeroUI - SCADA")]
    public class ZMultiStateLed : ControlBase, IScadaBindable
    {
        private MultiStateLedState _state = MultiStateLedState.Normal;
        private MultiStateLedBlink _blinkRate = MultiStateLedBlink.None;
        private MultiStateLedShape _shape = MultiStateLedShape.Circle;
        private LedLabelPosition _labelPosition = LedLabelPosition.Bottom;
        private string _label = "RUN";
        private Color _customColor = Color.Empty;
        private double _haloIntensity = 0.6;
        private bool _showReflection = true;

        private IDisposable? _clockToken;
        private float _animationPhase = 0f;
        private bool _blinkLit = true;

        public ZMultiStateLed()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            Size = new Size(64, 76);
            StartOrStopAnimation();
        }

        #region Properties

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("LED State")]
        [DefaultValue(MultiStateLedState.Normal)]
        public MultiStateLedState State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    Invalidate();
                }
            }
        }

        [Category("LED State")]
        [DefaultValue(MultiStateLedBlink.None)]
        public MultiStateLedBlink BlinkRate
        {
            get => _blinkRate;
            set
            {
                if (_blinkRate != value)
                {
                    _blinkRate = value;
                    StartOrStopAnimation();
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(MultiStateLedShape.Circle)]
        public MultiStateLedShape Shape
        {
            get => _shape;
            set { _shape = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("RUN")]
        public string Label
        {
            get => _label;
            set { _label = value ?? string.Empty; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(LedLabelPosition.Bottom)]
        public LedLabelPosition LabelPosition
        {
            get => _labelPosition;
            set { _labelPosition = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color CustomColor
        {
            get => _customColor;
            set { _customColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(0.6)]
        public double HaloIntensity
        {
            get => _haloIntensity;
            set { _haloIntensity = Math.Max(0.0, Math.Min(1.0, value)); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowReflection
        {
            get => _showReflection;
            set { _showReflection = value; Invalidate(); }
        }

        #endregion

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag?.Value == null) return;
            string valStr = tag.Value?.ToString() ?? string.Empty;
            if (Enum.TryParse<MultiStateLedState>(valStr, true, out var st))
            {
                State = st;
            }
            else if (int.TryParse(valStr, out var i) && Enum.IsDefined(typeof(MultiStateLedState), i))
            {
                State = (MultiStateLedState)i;
            }
            else if (bool.TryParse(valStr, out var b))
            {
                State = b ? MultiStateLedState.Normal : MultiStateLedState.Off;
            }
        }

        private void StartOrStopAnimation()
        {
            if (_blinkRate == MultiStateLedBlink.None)
            {
                _clockToken?.Dispose();
                _clockToken = null;
                _blinkLit = true;
                _animationPhase = 0f;
            }
            else if (_clockToken == null)
            {
                _clockToken = ZeroAnimationClock.Subscribe(OnAnimationFrame);
            }
        }

        private void OnAnimationFrame(double deltaSeconds, long frameCount)
        {
            float dt = (float)deltaSeconds;

            if (_blinkRate == MultiStateLedBlink.Slow)
            {
                _animationPhase += dt * 2.0f; // 1 Hz toggle (period 1.0s)
                _blinkLit = ((int)_animationPhase % 2) == 0;
            }
            else if (_blinkRate == MultiStateLedBlink.Fast)
            {
                _animationPhase += dt * 4.0f; // 2 Hz toggle (period 0.5s)
                _blinkLit = ((int)_animationPhase % 2) == 0;
            }
            else if (_blinkRate == MultiStateLedBlink.Pulse)
            {
                _animationPhase += dt * 3.5f;
                if (_animationPhase > 6.28318f) _animationPhase -= 6.28318f;
                _blinkLit = true;
            }

            Invalidate();
        }

        public Color GetEffectiveColor()
        {
            if (!_customColor.IsEmpty) return _customColor;

            switch (_state)
            {
                case MultiStateLedState.Off: return Color.FromArgb(60, 65, 70);
                case MultiStateLedState.Normal: return Color.FromArgb(40, 205, 65);
                case MultiStateLedState.Warning: return Color.FromArgb(245, 175, 20);
                case MultiStateLedState.Alarm: return Color.FromArgb(235, 40, 45);
                case MultiStateLedState.Maintenance: return Color.FromArgb(30, 140, 245);
                case MultiStateLedState.Standby: return Color.FromArgb(220, 230, 245);
                case MultiStateLedState.Unknown:
                default:
                    return Color.FromArgb(120, 125, 130);
            }
        }

        #region Painting

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            CalculateLayout(out RectangleF ledBounds, out RectangleF labelBounds);

            // Determine active visual state
            Color baseColor = GetEffectiveColor();
            bool isDark = _state == MultiStateLedState.Off || (!_blinkLit && _blinkRate != MultiStateLedBlink.Pulse);

            float pulseFactor = 1.0f;
            if (_blinkRate == MultiStateLedBlink.Pulse && _state != MultiStateLedState.Off)
            {
                pulseFactor = 0.35f + 0.65f * (float)(Math.Sin(_animationPhase) * 0.5 + 0.5);
            }

            // 1. Halo Glow
            if (!isDark && _haloIntensity > 0.05 && _state != MultiStateLedState.Off)
            {
                DrawHalo(g, ledBounds, baseColor, (float)_haloIntensity * pulseFactor);
            }

            // 2. Metallic Bezel / Frame
            DrawBezel(g, ledBounds);

            // 3. Core Lens Illumination
            float lensInset = Math.Max(3f, Math.Min(ledBounds.Width, ledBounds.Height) * 0.08f);
            var lensBounds = new RectangleF(ledBounds.X + lensInset, ledBounds.Y + lensInset, ledBounds.Width - lensInset * 2f, ledBounds.Height - lensInset * 2f);

            DrawLens(g, lensBounds, baseColor, isDark, pulseFactor);

            // 4. Glass Reflection Sheen
            if (_showReflection && !isDark)
            {
                DrawReflection(g, lensBounds);
            }

            // 5. Label
            if (_labelPosition != LedLabelPosition.None && !string.IsNullOrEmpty(_label) && labelBounds.Width > 5 && labelBounds.Height > 5)
            {
                DrawLabel(g, labelBounds);
            }
        }

        private void CalculateLayout(out RectangleF ledBounds, out RectangleF labelBounds)
        {
            float w = Width;
            float h = Height;

            if (_labelPosition == LedLabelPosition.None || string.IsNullOrEmpty(_label))
            {
                float side = Math.Min(w, h) - 6f;
                ledBounds = new RectangleF((w - side) / 2f, (h - side) / 2f, side, side);
                labelBounds = RectangleF.Empty;
                return;
            }

            float labelHeight = Math.Max(16f, h * 0.22f);
            float labelWidth = Math.Max(24f, w * 0.40f);

            switch (_labelPosition)
            {
                case LedLabelPosition.Bottom:
                    float sideB = Math.Min(w - 6f, h - labelHeight - 8f);
                    ledBounds = new RectangleF((w - sideB) / 2f, 4f, sideB, sideB);
                    labelBounds = new RectangleF(0, h - labelHeight - 2f, w, labelHeight);
                    break;
                case LedLabelPosition.Top:
                    float sideT = Math.Min(w - 6f, h - labelHeight - 8f);
                    ledBounds = new RectangleF((w - sideT) / 2f, labelHeight + 4f, sideT, sideT);
                    labelBounds = new RectangleF(0, 2f, w, labelHeight);
                    break;
                case LedLabelPosition.Right:
                    float sideR = Math.Min(w - labelWidth - 8f, h - 6f);
                    ledBounds = new RectangleF(4f, (h - sideR) / 2f, sideR, sideR);
                    labelBounds = new RectangleF(w - labelWidth - 2f, 0, labelWidth, h);
                    break;
                case LedLabelPosition.Left:
                    float sideL = Math.Min(w - labelWidth - 8f, h - 6f);
                    ledBounds = new RectangleF(w - sideL - 4f, (h - sideL) / 2f, sideL, sideL);
                    labelBounds = new RectangleF(2f, 0, labelWidth, h);
                    break;
                default:
                    float sideDef = Math.Min(w, h) - 6f;
                    ledBounds = new RectangleF((w - sideDef) / 2f, (h - sideDef) / 2f, sideDef, sideDef);
                    labelBounds = RectangleF.Empty;
                    break;
            }
        }

        private void DrawHalo(Graphics g, RectangleF bounds, Color color, float intensity)
        {
            float haloExpansion = Math.Min(bounds.Width, bounds.Height) * 0.35f * intensity;
            var haloRect = new RectangleF(bounds.X - haloExpansion, bounds.Y - haloExpansion, bounds.Width + haloExpansion * 2f, bounds.Height + haloExpansion * 2f);

            using (var path = CreateShapePath(haloRect))
            using (var pgb = new PathGradientBrush(path))
            {
                int alpha = (int)(90 * intensity);
                pgb.CenterColor = Color.FromArgb(alpha, color.R, color.G, color.B);
                pgb.SurroundColors = new[] { Color.FromArgb(0, color.R, color.G, color.B) };
                g.FillPath(pgb, path);
            }
        }

        private void DrawBezel(Graphics g, RectangleF bounds)
        {
            using (var path = CreateShapePath(bounds))
            {
                using (var brush = new LinearGradientBrush(bounds, Color.FromArgb(130, 135, 142), Color.FromArgb(40, 42, 45), 45f))
                {
                    g.FillPath(brush, path);
                }
                using (var pen = new Pen(Color.FromArgb(30, 32, 35), 1.5f))
                {
                    g.DrawPath(pen, path);
                }
            }
        }

        private void DrawLens(Graphics g, RectangleF bounds, Color color, bool isDark, float pulseFactor)
        {
            using (var path = CreateShapePath(bounds))
            {
                if (isDark)
                {
                    using (var brush = new LinearGradientBrush(bounds, Color.FromArgb(60, 65, 70), Color.FromArgb(30, 33, 36), 90f))
                    {
                        g.FillPath(brush, path);
                    }
                    using (var pen = new Pen(Color.FromArgb(20, 22, 24), 1f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
                else
                {
                    int r = Math.Min(255, (int)(color.R * pulseFactor));
                    int gr = Math.Min(255, (int)(color.G * pulseFactor));
                    int b = Math.Min(255, (int)(color.B * pulseFactor));

                    Color centerCol = Color.FromArgb(255, Math.Min(255, r + 40), Math.Min(255, gr + 40), Math.Min(255, b + 40));
                    Color edgeCol = Color.FromArgb(255, (int)(r * 0.65f), (int)(gr * 0.65f), (int)(b * 0.65f));

                    using (var pgb = new PathGradientBrush(path))
                    {
                        pgb.CenterColor = centerCol;
                        pgb.SurroundColors = new[] { edgeCol };
                        g.FillPath(pgb, path);
                    }

                    using (var pen = new Pen(Color.FromArgb(180, edgeCol.R, edgeCol.G, edgeCol.B), 1.5f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }
        }

        private void DrawReflection(Graphics g, RectangleF bounds)
        {
            float rw = bounds.Width * 0.65f;
            float rh = bounds.Height * 0.35f;
            var sheenRect = new RectangleF(bounds.X + (bounds.Width - rw) / 2f, bounds.Y + bounds.Height * 0.08f, rw, rh);

            using (var path = new GraphicsPath())
            {
                path.AddEllipse(sheenRect);
                using (var brush = new LinearGradientBrush(sheenRect, Color.FromArgb(140, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                {
                    g.FillPath(brush, path);
                }
            }
        }

        private void DrawLabel(Graphics g, RectangleF bounds)
        {
            var font = ZeroFontCache.Get("Segoe UI", Math.Max(7.5f, Math.Min(12f, bounds.Height * 0.55f)), FontStyle.Bold);
            using (var textBrush = new SolidBrush(Color.FromArgb(215, 220, 225)))
            using (var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            {
                g.DrawString(_label, font, textBrush, bounds, sf);
            }
        }

        private GraphicsPath CreateShapePath(RectangleF rect)
        {
            var path = new GraphicsPath();
            switch (_shape)
            {
                case MultiStateLedShape.Circle:
                    path.AddEllipse(rect);
                    break;
                case MultiStateLedShape.Square:
                    path.AddRectangle(rect);
                    break;
                case MultiStateLedShape.RoundedRectangle:
                    float r = Math.Min(rect.Width, rect.Height) * 0.22f;
                    path.AddArc(rect.X, rect.Y, r * 2f, r * 2f, 180, 90);
                    path.AddArc(rect.Right - r * 2f, rect.Y, r * 2f, r * 2f, 270, 90);
                    path.AddArc(rect.Right - r * 2f, rect.Bottom - r * 2f, r * 2f, r * 2f, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - r * 2f, r * 2f, r * 2f, 90, 90);
                    path.CloseFigure();
                    break;
            }
            return path;
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _clockToken?.Dispose();
                _clockToken = null;
            }
            base.Dispose(disposing);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZMultiStateLed"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("MultiStateLed is deprecated and will be removed in 5 release cycles. Please migrate to ZMultiStateLed instead.")]
    [ToolboxItem(false)]
    public class MultiStateLed : ZMultiStateLed
    {
    }

    #endregion
}
