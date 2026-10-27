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
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    public enum ZeroPumpState
    {
        Stopped,
        Running,
        Trip
    }

    /// <summary>
    /// Industrial standard P&ID centrifugal pump and motor drive component.
    /// Features smooth vector impeller rotation animation, telemetry readouts,
    /// and IScadaBindable tag engine integration.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroIndustrialPump.bmp")]
    [Category("ZeroUI - SCADA")]
    public class ZIndustrialPump : ControlBase, IScadaBindable
    {
        private ZeroPumpState _state = ZeroPumpState.Running;
        private DeviceStatusFlags _statusFlags = DeviceStatusFlags.None;
        private double _speedRpm = 2950.0;
        private double _powerKw = 18.5;
        private string _tagLabel = "P-101A";
        private float _impellerAngle = 0f;
        private IDisposable? _clockToken;
        private bool _isHovered;

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("Safety & Interlocks")]
        [DefaultValue(DeviceStatusFlags.None)]
        [Description("Active OSHA LOTO, safety interlock, and maintenance status flags")]
        public DeviceStatusFlags StatusFlags
        {
            get => _statusFlags;
            set { _statusFlags = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(ZeroPumpState.Running)]
        public ZeroPumpState State
        {
            get => _state;
            set { _state = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(2950.0)]
        public double SpeedRpm
        {
            get => _speedRpm;
            set { _speedRpm = Math.Max(0, value); Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(18.5)]
        public double PowerKw
        {
            get => _powerKw;
            set { _powerKw = Math.Max(0, value); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("P-101A")]
        public string TagLabel
        {
            get => _tagLabel;
            set { _tagLabel = value ?? ""; Invalidate(); }
        }

        public event EventHandler? PumpStateChanged;

        public ZIndustrialPump()
        {
            BackColor = Color.Transparent;
            Size = new Size(80, 84);
            Cursor = Cursors.Hand;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!ZeroDesignHelper.IsInDesignMode(this))
            {
                _clockToken = ZeroAnimationClock.Subscribe(OnAnimationFrameTick);
                ZeroTagEngine.RegisterBindable(this);
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            _clockToken?.Dispose();
            _clockToken = null;
            ZeroTagEngine.UnregisterBindable(this);
        }

        private void OnAnimationFrameTick(double deltaSeconds, long frameCount)
        {
            if (_state == ZeroPumpState.Running && _speedRpm > 0)
            {
                float delta = (float)(_speedRpm / 60.0 * 360.0 * deltaSeconds);
                _impellerAngle = (_impellerAngle + delta) % 360f;
                if (IsHandleCreated && Visible)
                {
                    Invalidate();
                }
            }
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnTagValueChanged(tag)));
                return;
            }

            if (tag.Value is bool b)
            {
                State = b ? ZeroPumpState.Running : ZeroPumpState.Stopped;
            }
            else if (tag.Value is int i)
            {
                SpeedRpm = i;
                State = i > 0 ? ZeroPumpState.Running : ZeroPumpState.Stopped;
            }
            else if (tag.Value is double d)
            {
                SpeedRpm = d;
                State = d > 0 ? ZeroPumpState.Running : ZeroPumpState.Stopped;
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            // Toggle Run / Stop state
            if (_state == ZeroPumpState.Running)
            {
                State = ZeroPumpState.Stopped;
            }
            else
            {
                State = ZeroPumpState.Running;
                if (_speedRpm <= 0) _speedRpm = 2950.0;
            }

            if (!string.IsNullOrEmpty(BoundTagPath))
            {
                ZeroTagEngine.SetTagValue(BoundTagPath!, _state == ZeroPumpState.Running);
            }

            PumpStateChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var palette = ZeroTheme.Colors;
            bool isDark = ZeroTheme.IsDark;

            int cx = Width / 2;
            int cy = Height / 2 - 6;
            int radius = 24;

            Color statusColor = _state switch
            {
                ZeroPumpState.Running => palette.Success,
                ZeroPumpState.Stopped => palette.TextSecondary,
                ZeroPumpState.Trip => palette.Danger,
                _ => palette.Border
            };

            // 1. Motor Mount Base (Pedestal)
            var baseRect = new Rectangle(cx - 20, cy + radius - 2, 40, 6);
            using (var brushBase = new SolidBrush(isDark ? Color.FromArgb(40, 48, 70) : Color.FromArgb(148, 163, 184)))
            {
                g.FillRectangle(brushBase, baseRect);
            }

            // 2. Discharge Tangent Port (Top Right)
            Point[] dischargeNozzle = new[]
            {
                new Point(cx + radius - 6, cy - radius + 4),
                new Point(cx + radius + 10, cy - radius - 8),
                new Point(cx + radius + 14, cy - radius - 4),
                new Point(cx + radius - 2, cy - radius + 8)
            };
            using (var brushNozzle = new SolidBrush(isDark ? Color.FromArgb(30, 36, 56) : Color.FromArgb(203, 213, 225)))
            {
                g.FillPolygon(brushNozzle, dischargeNozzle);
            }

            // 3. Volute Circular Pump Body
            var bodyRect = new Rectangle(cx - radius, cy - radius, radius * 2, radius * 2);
            using (var brushVolute = new SolidBrush(palette.Surface))
            {
                g.FillEllipse(brushVolute, bodyRect);
            }

            using (var penVolute = new Pen(_isHovered ? palette.Primary : statusColor, 2.5f))
            {
                g.DrawEllipse(penVolute, bodyRect);
            }

            // 4. Rotating Impeller Blades (Center)
            var stateContainer = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(_impellerAngle);

            using (var penBlade = new Pen(statusColor, 2.2f))
            {
                // 3 Curved Impeller Blades
                for (int i = 0; i < 3; i++)
                {
                    g.DrawArc(penBlade, -14, -14, 28, 28, i * 120, 80);
                }
            }

            // Central Impeller Hub
            using (var brushHub = new SolidBrush(palette.TextPrimary))
            {
                g.FillEllipse(brushHub, -4, -4, 8, 8);
            }
            g.Restore(stateContainer);

            // 5. Telemetry Readout below pump
            var fontTag = ZeroFontCache.Get("Segoe UI", 7.5f * DpiScale, FontStyle.Bold);
            var fontRpm = ZeroFontCache.Get("Segoe UI", 6.5f * DpiScale, FontStyle.Regular);
            using var brushText = new SolidBrush(palette.TextPrimary);
            using var brushMuted = new SolidBrush(palette.TextSecondary);
            using var brushDanger = new SolidBrush(palette.Danger);

            var sf = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString(_tagLabel, fontTag, brushText, cx, cy + radius + 6, sf);

            string infoStr = _state == ZeroPumpState.Running ? $"{_speedRpm:0} RPM" : (_state == ZeroPumpState.Trip ? "TRIP!" : "STOPPED");
            g.DrawString(infoStr, fontRpm, (_state == ZeroPumpState.Trip) ? brushDanger : brushMuted, cx, cy + radius + 17, sf);

            // 6. Safety & Interlock Badges
            if (_statusFlags != DeviceStatusFlags.None)
            {
                ZDeviceBadgeRenderer.DrawBadges(g, ClientRectangle, _statusFlags, isDark);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _clockToken?.Dispose();
                _clockToken = null;
                ZeroTagEngine.UnregisterBindable(this);
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZIndustrialPump"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroIndustrialPump is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialPump instead.")]
    [ToolboxItem(false)]
    public class ZeroIndustrialPump : ZIndustrialPump
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZIndustrialPump"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("IndustrialPump is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialPump instead.")]
    [ToolboxItem(false)]
    public class IndustrialPump : ZIndustrialPump
    {
    }

    #endregion
}
