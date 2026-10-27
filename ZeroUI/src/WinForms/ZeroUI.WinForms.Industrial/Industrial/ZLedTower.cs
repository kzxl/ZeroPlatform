using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Rendering;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    public enum LedState
    {
        Off,
        On,
        Blinking
    }

    /// <summary>
    /// Industrial Andon Signal Tower Light control (SCADA / MES) with Red, Amber, Green, and Blue segments.
    /// Supports solid illumination, high-visibility 1Hz/2Hz flashing, and vector anti-aliased 3D glass rendering.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroLedTower.bmp")]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Industrial Andon Signal Tower Light with Red, Amber, Green, and Blue segments")]
    public class ZLedTower : Control
    {
        private LedState _red = LedState.Off;
        private LedState _amber = LedState.Off;
        private LedState _green = LedState.On;
        private LedState _blue = LedState.Off;

        private IDisposable? _clockToken;
        private bool _lastBlink;

        public ZLedTower()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(54, 180);
            BackColor = Color.Transparent;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!ZeroDesignHelper.IsInDesignMode(this))
            {
                _clockToken = ZeroAnimationClock.Subscribe(OnAnimationFrameTick);
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            _clockToken?.Dispose();
            _clockToken = null;
        }

        private void OnAnimationFrameTick(double deltaSeconds, long frameCount)
        {
            if (_red == LedState.Blinking || _amber == LedState.Blinking ||
                _green == LedState.Blinking || _blue == LedState.Blinking)
            {
                bool currentBlink = ZeroAnimationClock.BlinkSlow;
                if (currentBlink != _lastBlink)
                {
                    _lastBlink = currentBlink;
                    if (IsHandleCreated && Visible)
                    {
                        Invalidate();
                    }
                }
            }
        }


        [Category("Andon Lights")]
        [DefaultValue(LedState.Off)]
        public LedState RedLight
        {
            get => _red;
            set { _red = value; Invalidate(); }
        }

        [Category("Andon Lights")]
        [DefaultValue(LedState.Off)]
        public LedState AmberLight
        {
            get => _amber;
            set { _amber = value; Invalidate(); }
        }

        [Category("Andon Lights")]
        [DefaultValue(LedState.On)]
        public LedState GreenLight
        {
            get => _green;
            set { _green = value; Invalidate(); }
        }

        [Category("Andon Lights")]
        [DefaultValue(LedState.Off)]
        public LedState BlueLight
        {
            get => _blue;
            set { _blue = value; Invalidate(); }
        }

        public void SetStatus(bool running, bool warning, bool alarm)
        {
            if (alarm)
            {
                RedLight = LedState.Blinking;
                AmberLight = LedState.Off;
                GreenLight = LedState.Off;
            }
            else if (warning)
            {
                RedLight = LedState.Off;
                AmberLight = LedState.On;
                GreenLight = LedState.Off;
            }
            else if (running)
            {
                RedLight = LedState.Off;
                AmberLight = LedState.Off;
                GreenLight = LedState.On;
            }
            else
            {
                RedLight = LedState.Off;
                AmberLight = LedState.Off;
                GreenLight = LedState.Off;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _clockToken?.Dispose();
                _clockToken = null;
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = Width;
            int h = Height;
            int segW = Math.Min(w - 12, 38);
            int segH = 26;
            int centerX = w / 2;
            int startX = centerX - (segW / 2);
            int startY = 12;

            // 1. Top Cap
            using (var capBrush = new LinearGradientBrush(
                new Point(startX, startY - 8),
                new Point(startX + segW, startY - 8),
                Color.FromArgb(100, 116, 139),
                Color.FromArgb(51, 65, 85)))
            {
                g.FillPie(capBrush, startX + 2, startY - 8, segW - 4, 14, 180, 180);
            }

            // 2. Render 4 Lamp Segments: Red, Amber, Green, Blue
            DrawSegment(g, startX, startY, segW, segH, _red, Color.FromArgb(239, 68, 68), Color.FromArgb(70, 20, 20));
            DrawSegment(g, startX, startY + segH + 2, segW, segH, _amber, Color.FromArgb(245, 158, 11), Color.FromArgb(70, 45, 15));
            DrawSegment(g, startX, startY + (segH + 2) * 2, segW, segH, _green, Color.FromArgb(16, 185, 129), Color.FromArgb(15, 60, 40));
            DrawSegment(g, startX, startY + (segH + 2) * 3, segW, segH, _blue, Color.FromArgb(59, 130, 246), Color.FromArgb(15, 35, 75));

            int baseY = startY + (segH + 2) * 4;

            // 3. Tower Base & Mounting Pole
            using (var baseBrush = new LinearGradientBrush(
                new Point(startX - 2, baseY),
                new Point(startX + segW + 2, baseY),
                Color.FromArgb(148, 163, 184),
                Color.FromArgb(71, 85, 105)))
            {
                g.FillRectangle(baseBrush, startX + 2, baseY, segW - 4, 12);

                // Aluminum Mounting Pole
                int poleW = 8;
                int poleH = Math.Max(10, h - baseY - 24);
                g.FillRectangle(baseBrush, centerX - (poleW / 2), baseY + 12, poleW, poleH);

                // Mounting Flange Foot
                g.FillEllipse(baseBrush, centerX - 18, baseY + 12 + poleH - 4, 36, 12);
            }
        }

        private void DrawSegment(Graphics g, int x, int y, int w, int h, LedState state, Color brightColor, Color darkColor)
        {
            bool isLit = (state == LedState.On) || (state == LedState.Blinking && ZeroAnimationClock.BlinkSlow);
            Rectangle rect = new Rectangle(x, y, w, h);

            Color cMain = isLit ? brightColor : darkColor;
            Color cLight = isLit ? Color.FromArgb(255, Math.Min(255, cMain.R + 60), Math.Min(255, cMain.G + 60), Math.Min(255, cMain.B + 60)) : Color.FromArgb(90, cMain);
            Color cDark = isLit ? Color.FromArgb(200, cMain.R / 2, cMain.G / 2, cMain.B / 2) : Color.FromArgb(20, 20, 20);

            // Halo glow when lit
            if (isLit)
            {
                using var glowBrush = new PathGradientBrush(new PointF[]
                {
                    new PointF(rect.Left - 6, rect.Top - 3),
                    new PointF(rect.Right + 6, rect.Top - 3),
                    new PointF(rect.Right + 6, rect.Bottom + 3),
                    new PointF(rect.Left - 6, rect.Bottom + 3)
                })
                {
                    CenterColor = Color.FromArgb(80, cMain),
                    SurroundColors = new Color[] { Color.Transparent }
                };
                g.FillRectangle(glowBrush, rect.Left - 6, rect.Top - 3, rect.Width + 12, rect.Height + 6);
            }

            // Segment Cylinder Fill
            using (var brush = new LinearGradientBrush(new Point(x, y), new Point(x + w, y), cLight, cDark))
            {
                brush.SetBlendTriangularShape(0.35f);
                g.FillRectangle(brush, rect);
            }

            // Glass Reflection highlight strip
            using (var reflBrush = new LinearGradientBrush(new Point(x + 4, y), new Point(x + 10, y), Color.FromArgb(isLit ? 140 : 40, Color.White), Color.Transparent))
            {
                g.FillRectangle(reflBrush, x + 4, y + 2, 6, h - 4);
            }

            // Dark border segment separator
            using var borderPen = new Pen(Color.FromArgb(40, 0, 0, 0), 1f);
            g.DrawRectangle(borderPen, rect);
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZLedTower"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroLedTower is deprecated and will be removed in 5 release cycles. Please migrate to ZLedTower instead.")]
    [ToolboxItem(false)]
    public class ZeroLedTower : ZLedTower
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZLedTower"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("LedTower is deprecated and will be removed in 5 release cycles. Please migrate to ZLedTower instead.")]
    [ToolboxItem(false)]
    public class LedTower : ZLedTower
    {
    }

    #endregion
}
