using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Network;
using ZeroUI.Core.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Network
{
    /// <summary>
    /// Hardware Chassis Health HUD & SFP Optical DDM Monitoring Control.
    /// Visualizes dual redundant power supplies, fan tachometer gauges with rotating blades,
    /// and optical transceiver laser power metrics.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Network & Infrastructure")]
    [Description("Hardware Chassis Health HUD for redundant PSUs, fans, and optical DDM diagnostics")]
    public class ZDeviceFaceplate : Control
    {
        private readonly ChassisHealthProfile _profile = new ChassisHealthProfile();
        private IDisposable? _animSub;

        [Category("Hardware")]
        [Description("Access to the underlying hardware chassis health profile")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ChassisHealthProfile Profile => _profile;

        public ZDeviceFaceplate()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(540, 320);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 8.25f);

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!DesignMode)
            {
                _animSub = ZeroAnimationClock.Subscribe((delta, frame) =>
                {
                    if (IsHandleCreated && Visible)
                    {
                        Invalidate();
                    }
                });
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _animSub?.Dispose();
            _animSub = null;
            base.OnHandleDestroyed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
                _animSub?.Dispose();
                _animSub = null;
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var bounds = ClientRectangle;
            if (bounds.Width < 200 || bounds.Height < 150) return;

            // 1. Outer Chassis Container
            using (var bgBrush = new SolidBrush(Color.FromArgb(20, 24, 33)))
            using (var borderPen = new Pen(Color.FromArgb(45, 55, 72), 1.5f))
            {
                using var path = CreateRoundedRect(new Rectangle(0, 0, bounds.Width - 1, bounds.Height - 1), 6);
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
            }

            // 2. Chassis Title Header & Overall Status
            int headerH = 44;
            DrawHeader(g, new Rectangle(10, 10, bounds.Width - 20, headerH));

            // 3. Dual Redundant PSUs Section
            int psuTop = headerH + 18;
            int psuH = 80;
            DrawPsuSection(g, new Rectangle(10, psuTop, bounds.Width - 20, psuH));

            // 4. Fans Tachometers & Optical DDM split
            int lowerTop = psuTop + psuH + 10;
            int lowerH = bounds.Height - lowerTop - 10;
            int halfW = (bounds.Width - 28) / 2;

            DrawFansSection(g, new Rectangle(10, lowerTop, halfW, lowerH));
            DrawOpticalDdmSection(g, new Rectangle(18 + halfW, lowerTop, halfW, lowerH));
        }

        private void DrawHeader(Graphics g, Rectangle r)
        {
            using (var headerBrush = new SolidBrush(Color.FromArgb(28, 33, 46)))
            using (var headerPen = new Pen(Color.FromArgb(45, 55, 72), 1f))
            {
                using var path = CreateRoundedRect(r, 4);
                g.FillPath(headerBrush, path);
                g.DrawPath(headerPen, path);
            }

            using var titleFont = new Font(Font.FontFamily, 9f, FontStyle.Bold);
            using var subFont = new Font(Font.FontFamily, 7.5f, FontStyle.Regular);
            using var textBrush = new SolidBrush(Color.FromArgb(241, 245, 249));
            using var subBrush = new SolidBrush(Color.FromArgb(148, 163, 184));

            g.DrawString(_profile.DeviceName, titleFont, textBrush, r.Left + 10, r.Top + 6);
            g.DrawString($"{_profile.Model} | S/N: {_profile.SerialNumber} | Uptime: {_profile.Uptime.Days}d {_profile.Uptime.Hours}h", subFont, subBrush, r.Left + 10, r.Top + 24);

            // Health Status Badge
            var health = _profile.EvaluateOverallHealth();
            Color badgeBg = health switch
            {
                ChassisOverallHealth.Healthy => Color.FromArgb(20, 83, 45),
                ChassisOverallHealth.Degraded => Color.FromArgb(120, 53, 15),
                _ => Color.FromArgb(127, 29, 29)
            };
            Color badgeBorder = health switch
            {
                ChassisOverallHealth.Healthy => Color.FromArgb(34, 197, 94),
                ChassisOverallHealth.Degraded => Color.FromArgb(245, 158, 11),
                _ => Color.FromArgb(239, 68, 68)
            };

            var badgeRect = new Rectangle(r.Right - 110, r.Top + 8, 100, 26);
            using (var brush = new SolidBrush(badgeBg))
            using (var pen = new Pen(badgeBorder, 1.2f))
            {
                using var path = CreateRoundedRect(badgeRect, 4);
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            using var badgeFont = new Font(Font.FontFamily, 7.5f, FontStyle.Bold);
            using var badgeTextBrush = new SolidBrush(badgeBorder);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(health.ToString().ToUpperInvariant(), badgeFont, badgeTextBrush, badgeRect, sf);
        }

        private void DrawPsuSection(Graphics g, Rectangle r)
        {
            using (var sectionBrush = new SolidBrush(Color.FromArgb(24, 28, 38)))
            using (var sectionPen = new Pen(Color.FromArgb(40, 48, 64), 1f))
            {
                using var path = CreateRoundedRect(r, 4);
                g.FillPath(sectionBrush, path);
                g.DrawPath(sectionPen, path);
            }

            int halfW = (r.Width - 12) / 2;
            DrawSinglePsuCard(g, _profile.Psu1, new Rectangle(r.Left + 4, r.Top + 4, halfW, r.Height - 8));
            DrawSinglePsuCard(g, _profile.Psu2, new Rectangle(r.Left + halfW + 8, r.Top + 4, halfW, r.Height - 8));
        }

        private void DrawSinglePsuCard(Graphics g, PsuStatus psu, Rectangle r)
        {
            using var cardBrush = new SolidBrush(Color.FromArgb(30, 36, 50));
            using var cardPen = new Pen(Color.FromArgb(48, 58, 78), 1f);
            using var path = CreateRoundedRect(r, 4);
            g.FillPath(cardBrush, path);
            g.DrawPath(cardPen, path);

            // LED status
            Color ledColor = psu.Status switch
            {
                PsuHealthStatus.Normal => Color.FromArgb(34, 197, 94),
                PsuHealthStatus.Warning => Color.FromArgb(245, 158, 11),
                _ => Color.FromArgb(239, 68, 68)
            };
            using (var ledBrush = new SolidBrush(ledColor))
            {
                g.FillEllipse(ledBrush, r.Left + 10, r.Top + 10, 8, 8);
            }

            using var titleFont = new Font(Font.FontFamily, 8f, FontStyle.Bold);
            using var subFont = new Font(Font.FontFamily, 7.25f, FontStyle.Regular);
            using var textBrush = new SolidBrush(Color.FromArgb(226, 232, 240));
            using var subBrush = new SolidBrush(Color.FromArgb(148, 163, 184));

            string statusStr = psu.IsPowered ? "ONLINE (Active)" : "OFFLINE";
            g.DrawString($"Power Supply {psu.PsuIndex}: {statusStr}", titleFont, textBrush, r.Left + 24, r.Top + 7);
            g.DrawString($"Input: {psu.InputVoltageVolts:F0} V | Draw: {psu.PowerWatts:F0} W ({psu.OutputCurrentAmps:F1} A) | Temp: {psu.TemperatureCelsius:F0}°C", subFont, subBrush, r.Left + 10, r.Top + 28);

            // Power load bar
            int barW = r.Width - 20;
            var barRect = new Rectangle(r.Left + 10, r.Top + 48, barW, 8);
            using var barBg = new SolidBrush(Color.FromArgb(15, 18, 26));
            g.FillRectangle(barBg, barRect);

            int fillW = (int)(barW * Math.Min(1.0, psu.PowerWatts / 500.0));
            using var barFill = new SolidBrush(Color.FromArgb(56, 189, 248));
            g.FillRectangle(barFill, barRect.X, barRect.Y, fillW, barRect.Height);
        }

        private void DrawFansSection(Graphics g, Rectangle r)
        {
            using (var bgBrush = new SolidBrush(Color.FromArgb(24, 28, 38)))
            using (var bgPen = new Pen(Color.FromArgb(40, 48, 64), 1f))
            {
                using var path = CreateRoundedRect(r, 4);
                g.FillPath(bgBrush, path);
                g.DrawPath(bgPen, path);
            }

            using var titleFont = new Font(Font.FontFamily, 8f, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(226, 232, 240));
            g.DrawString("Chassis Fan Tachometers", titleFont, textBrush, r.Left + 10, r.Top + 8);

            float fanW = (r.Width - 16) / Math.Max(1, _profile.Fans.Count);
            float animTime = (float)ZeroAnimationClock.TotalElapsedTime;

            for (int i = 0; i < _profile.Fans.Count; i++)
            {
                var fan = _profile.Fans[i];
                float x = r.Left + 8 + (i * fanW);
                float y = r.Top + 28;

                // Rotating blade glyph
                float cx = x + (fanW / 2);
                float cy = y + 24;
                float angle = animTime * 360f * (float)fan.RpmRatio * 2f;

                using var bladePen = new Pen(fan.Status == FanHealthStatus.Normal ? Color.FromArgb(56, 189, 248) : Color.FromArgb(239, 68, 68), 2f);
                var mState = g.Save();
                g.TranslateTransform(cx, cy);
                g.RotateTransform(angle);
                g.DrawLine(bladePen, -12, 0, 12, 0);
                g.DrawLine(bladePen, 0, -12, 0, 12);
                g.Restore(mState);

                using (var hubBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
                {
                    g.FillEllipse(hubBrush, cx - 3, cy - 3, 6, 6);
                }

                // RPM readout
                using var font = new Font(Font.FontFamily, 7f, FontStyle.Bold);
                using var subFont = new Font(Font.FontFamily, 6.5f, FontStyle.Regular);
                using var valBrush = new SolidBrush(Color.FromArgb(241, 245, 249));
                using var subBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
                var sf = new StringFormat { Alignment = StringAlignment.Center };

                g.DrawString(fan.Name, subFont, subBrush, new RectangleF(x, y + 50, fanW, 14), sf);
                g.DrawString($"{fan.CurrentRpm} RPM", font, valBrush, new RectangleF(x, y + 64, fanW, 16), sf);
            }
        }

        private void DrawOpticalDdmSection(Graphics g, Rectangle r)
        {
            using (var bgBrush = new SolidBrush(Color.FromArgb(24, 28, 38)))
            using (var bgPen = new Pen(Color.FromArgb(40, 48, 64), 1f))
            {
                using var path = CreateRoundedRect(r, 4);
                g.FillPath(bgBrush, path);
                g.DrawPath(bgPen, path);
            }

            using var titleFont = new Font(Font.FontFamily, 8f, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(226, 232, 240));
            g.DrawString("SFP Digital Optical Monitoring (DDM)", titleFont, textBrush, r.Left + 10, r.Top + 8);

            float rowH = (r.Height - 34) / Math.Max(1, _profile.OpticalTransceivers.Count);
            using var font = new Font(Font.FontFamily, 7f, FontStyle.Regular);
            using var boldFont = new Font(Font.FontFamily, 7f, FontStyle.Bold);
            using var subBrush = new SolidBrush(Color.FromArgb(148, 163, 184));

            for (int i = 0; i < _profile.OpticalTransceivers.Count; i++)
            {
                var sfp = _profile.OpticalTransceivers[i];
                float y = r.Top + 30 + (i * rowH);

                Color statusColor = sfp.IsRxOpticalSignalAcceptable ? Color.FromArgb(34, 197, 94) : Color.FromArgb(239, 68, 68);
                using (var dot = new SolidBrush(statusColor))
                {
                    g.FillEllipse(dot, r.Left + 12, y + 4, 6, 6);
                }

                g.DrawString(sfp.PortName, boldFont, textBrush, r.Left + 24, y + 1);
                g.DrawString($"Tx: {sfp.TxPowerDbm:F1} dBm | Rx: {sfp.RxPowerDbm:F1} dBm | Temp: {sfp.TransceiverTempCelsius:F1}°C", font, subBrush, r.Left + 24, y + 16);

                // Mini optical budget indicator bar
                float barW = r.Width - 36;
                var barRect = new RectangleF(r.Left + 24, y + 32, barW, 5);
                using var barBg = new SolidBrush(Color.FromArgb(15, 18, 26));
                g.FillRectangle(barBg, barRect);

                // Normal optical range normalized
                float ratio = (float)Math.Max(0, Math.Min(1.0, (sfp.RxPowerDbm + 25.0) / 25.0));
                using var fillBrush = new SolidBrush(statusColor);
                g.FillRectangle(fillBrush, barRect.X, barRect.Y, barW * ratio, barRect.Height);
            }
        }

        private static GraphicsPath CreateRoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    
    private void OnThemeChanged(object? sender, EventArgs e) => Invalidate();

}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZDeviceFaceplate"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DeviceFaceplate is deprecated and will be removed in 5 release cycles. Please migrate to ZDeviceFaceplate instead.")]
    [ToolboxItem(false)]
    public class DeviceFaceplate : ZDeviceFaceplate
    {
    }

    #endregion
}
