using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Rendering;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    public enum TankAlarmState
    {
        Normal,
        LowLevel,
        HighOverflow
    }

    /// <summary>
    /// Industrial 3D cylindrical fluid storage tank with animated surface waves,
    /// graduated level markings, and high/low sensor trips.
    /// Synchronized with ZeroAnimationClock and ZeroFontCache.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroTank3D.bmp")]
    [Category("ZeroUI - Industrial & SCADA")]
    [DefaultProperty("CurrentLevelLiters")]
    [Description("Industrial 3D cylindrical fluid storage tank with animated liquid waves")]
    public class ZTank3D : ControlBase
    {
        private float _capacityLiters = 10000f;
        private float _currentLevelLiters = 6850f;
        private float _highAlarmPct = 90f;
        private float _lowAlarmPct = 15f;
        private string _tankName = "Solvent Tank TK-01";
        private string _fluidName = "IPA Solution 99.7%";
        private Color _fluidColor = Color.FromArgb(6, 182, 212); // Cyan Blue

        private IDisposable? _clockToken;
        private float _wavePhase = 0f;
        private bool _blinkPhase = false;

        public ZTank3D()
        {
            Size = new Size(180, 240);
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
            _wavePhase = (_wavePhase + (float)(deltaSeconds * 2.5)) % (float)(Math.PI * 2);
            _blinkPhase = ZeroAnimationClock.BlinkSlow;
            if (IsHandleCreated && Visible)
            {
                Invalidate();
            }
        }

        [Category("Tank Parameters")]
        [DefaultValue(10000f)]
        public float CapacityLiters
        {
            get => _capacityLiters;
            set { _capacityLiters = Math.Max(10f, value); Invalidate(); }
        }

        [Category("Tank Parameters")]
        [DefaultValue(6850f)]
        public float CurrentLevelLiters
        {
            get => _currentLevelLiters;
            set
            {
                _currentLevelLiters = Math.Max(0f, Math.Min(_capacityLiters, value));
                Invalidate();
            }
        }

        [Category("Tank Parameters")]
        [DefaultValue(90f)]
        public float HighAlarmPct
        {
            get => _highAlarmPct;
            set { _highAlarmPct = value; Invalidate(); }
        }

        [Category("Tank Parameters")]
        [DefaultValue(15f)]
        public float LowAlarmPct
        {
            get => _lowAlarmPct;
            set { _lowAlarmPct = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("Solvent Tank TK-01")]
        public string TankName
        {
            get => _tankName;
            set { _tankName = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("IPA Solution 99.7%")]
        public string FluidName
        {
            get => _fluidName;
            set { _fluidName = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(typeof(Color), "6, 182, 212")]
        public Color FluidColor
        {
            get => _fluidColor;
            set { _fluidColor = value; Invalidate(); }
        }

        [Browsable(false)]
        public float Percentage => (_currentLevelLiters / _capacityLiters) * 100f;

        [Browsable(false)]
        public TankAlarmState AlarmState
        {
            get
            {
                if (Percentage >= _highAlarmPct) return TankAlarmState.HighOverflow;
                if (Percentage <= _lowAlarmPct) return TankAlarmState.LowLevel;
                return TankAlarmState.Normal;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = Width;
            int h = Height;

            // 1. Header: Tank Name
            var titleFont = ZeroFontCache.Get("Segoe UI", 8f * DpiScale, FontStyle.Bold);
            using (var titleBrush = new SolidBrush(ZeroTheme.Colors.TextPrimary))
            {
                var sz = g.MeasureString(_tankName, titleFont);
                g.DrawString(_tankName, titleFont, titleBrush, (w - sz.Width) / 2, 2);
            }

            // 2. Tank Geometry Dimensions
            int tankTop = 22;
            int tankBottom = h - 34;
            int tankH = tankBottom - tankTop;
            int tankW = w - 46;
            int tankX = 14;

            int capH = 14;

            // 3. Tank Steel Body (Back and Outline)
            var tankBodyRect = new Rectangle(tankX, tankTop + (capH / 2), tankW, tankH - capH);

            // Draw Metallic Cylinder Body
            using (var steelBrush = new LinearGradientBrush(
                new Point(tankX, 0),
                new Point(tankX + tankW, 0),
                Color.FromArgb(226, 232, 240),
                Color.FromArgb(203, 213, 225)))
            {
                g.FillRectangle(steelBrush, tankBodyRect);
            }

            // 4. Fluid Fill Calculation
            float fillRatio = Math.Max(0f, Math.Min(1f, _currentLevelLiters / _capacityLiters));
            int fluidH = (int)(tankBodyRect.Height * fillRatio);
            int fluidY = tankBodyRect.Bottom - fluidH;

            if (fluidH > 0)
            {
                // Clip fluid inside cylinder body
                var prevClip = g.Clip;
                g.SetClip(tankBodyRect);

                // Wave Path at top of fluid
                using (var wavePath = new GraphicsPath())
                {
                    int waveSteps = 20;
                    float stepX = (float)tankW / waveSteps;
                    PointF[] wavePts = new PointF[waveSteps + 1];

                    for (int i = 0; i <= waveSteps; i++)
                    {
                        float wx = tankX + (i * stepX);
                        float amp = 3.5f;
                        float wy = fluidY + (amp * (float)Math.Sin(_wavePhase + (i * 0.45f)));
                        wavePts[i] = new PointF(wx, wy);
                    }

                    wavePath.AddLine(tankX, tankBodyRect.Bottom, tankX, fluidY);
                    wavePath.AddCurve(wavePts);
                    wavePath.AddLine(tankX + tankW, fluidY, tankX + tankW, tankBodyRect.Bottom);
                    wavePath.CloseFigure();

                    using (var fluidBrush = new LinearGradientBrush(
                        new Point(tankX, fluidY),
                        new Point(tankX + tankW, tankBodyRect.Bottom),
                        _fluidColor,
                        Color.FromArgb(200, _fluidColor.R / 2, _fluidColor.G / 2, _fluidColor.B / 2)))
                    {
                        g.FillPath(fluidBrush, wavePath);
                    }
                }

                g.Clip = prevClip;
            }

            // 5. 3D Top and Bottom Elliptical Caps
            var topCapRect = new Rectangle(tankX, tankTop, tankW, capH);
            var bottomCapRect = new Rectangle(tankX, tankBottom - capH, tankW, capH);

            using (var capBrush = new LinearGradientBrush(
                new Point(tankX, 0),
                new Point(tankX + tankW, 0),
                Color.FromArgb(241, 245, 249),
                Color.FromArgb(148, 163, 184)))
            {
                g.FillEllipse(capBrush, bottomCapRect);
                g.FillEllipse(capBrush, topCapRect);
            }

            // Outline of cylinder
            using (var borderPen = new Pen(Color.FromArgb(100, 116, 139), 1.5f))
            {
                g.DrawLine(borderPen, tankX, tankTop + (capH / 2), tankX, tankBottom - (capH / 2));
                g.DrawLine(borderPen, tankX + tankW, tankTop + (capH / 2), tankX + tankW, tankBottom - (capH / 2));
                g.DrawEllipse(borderPen, topCapRect);
                g.DrawArc(borderPen, bottomCapRect, 0, 180);
            }

            // 6. Right Side Sight-Glass Level Tube with Graduation Marks
            int tubeX = tankX + tankW + 8;
            int tubeW = 8;
            var tubeRect = new Rectangle(tubeX, tankBodyRect.Y, tubeW, tankBodyRect.Height);

            using (var glassBrush = new SolidBrush(Color.FromArgb(100, 241, 245, 249)))
            {
                g.FillRectangle(glassBrush, tubeRect);
            }

            // Fluid level inside glass tube
            if (fluidH > 0)
            {
                using var tubeFluidBrush = new SolidBrush(_fluidColor);
                g.FillRectangle(tubeFluidBrush, tubeX, fluidY, tubeW, fluidH);
            }

            using (var tubePen = new Pen(Color.FromArgb(71, 85, 105), 1f))
            {
                g.DrawRectangle(tubePen, tubeRect);

                // Tick marks (0%, 25%, 50%, 75%, 100%)
                var tickFont = ZeroFontCache.Get("Segoe UI", 6f * DpiScale, FontStyle.Regular);
                using var tickBrush = new SolidBrush(ZeroTheme.Colors.TextSecondary);
                for (int pct = 0; pct <= 100; pct += 25)
                {
                    int ty = tankBodyRect.Bottom - (int)(tankBodyRect.Height * (pct / 100f));
                    g.DrawLine(tubePen, tubeX + tubeW, ty, tubeX + tubeW + 3, ty);
                    g.DrawString($"{pct}%", tickFont, tickBrush, tubeX + tubeW + 4, ty - 4);
                }
            }

            // 7. Alarm Trip Indicators (LEDs)
            Color alarmLedColor = Color.FromArgb(100, 116, 139);
            if (AlarmState == TankAlarmState.HighOverflow)
                alarmLedColor = _blinkPhase ? Color.FromArgb(239, 68, 68) : Color.FromArgb(185, 28, 28);
            else if (AlarmState == TankAlarmState.LowLevel)
                alarmLedColor = _blinkPhase ? Color.FromArgb(245, 158, 11) : Color.FromArgb(180, 83, 9);
            else
                alarmLedColor = Color.FromArgb(16, 185, 129);

            int ledX = tankX + (tankW / 2) - 4;
            int ledY = tankTop - 2;
            using (var ledBrush = new SolidBrush(alarmLedColor))
            {
                g.FillEllipse(ledBrush, ledX, ledY, 8, 8);
            }
            using (var ledPen = new Pen(Color.FromArgb(51, 65, 85), 1f))
            {
                g.DrawEllipse(ledPen, ledX, ledY, 8, 8);
            }

            // 8. Bottom Digital Value Readout
            string valText = $"{_currentLevelLiters:N0} L ({Percentage:F1}%)";
            var valFont = ZeroFontCache.Get("Segoe UI", 8.5f * DpiScale, FontStyle.Bold);
            using (var valBrush = new SolidBrush(ZeroTheme.Colors.TextPrimary))
            {
                var sz = g.MeasureString(valText, valFont);
                g.DrawString(valText, valFont, valBrush, (w - sz.Width) / 2, h - 28);
            }

            var subFont = ZeroFontCache.Get("Segoe UI", 7f * DpiScale, FontStyle.Regular);
            using (var subBrush = new SolidBrush(ZeroTheme.Colors.TextSecondary))
            {
                var sz = g.MeasureString(_fluidName, subFont);
                g.DrawString(_fluidName, subFont, subBrush, (w - sz.Width) / 2, h - 14);
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
    }

    /// <summary>
    /// Legacy alias for <see cref="ZTank3D"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroTank3D is deprecated and will be removed in 5 release cycles. Please migrate to ZTank3D instead.")]
    [ToolboxItem(false)]
    public class ZeroTank3D : ZTank3D
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZTank3D"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("Tank3D is deprecated and will be removed in 5 release cycles. Please migrate to ZTank3D instead.")]
    [ToolboxItem(false)]
    public class Tank3D : ZTank3D
    {
    }

    #endregion
}
