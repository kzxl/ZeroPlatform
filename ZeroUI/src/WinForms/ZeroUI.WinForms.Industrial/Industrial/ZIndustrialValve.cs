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
    public enum ZeroValveType
    {
        TwoWaySolenoid,
        ControlValve,
        BallValve,
        CheckValve,
        ThreeWayDiverter,
        ThreeWayMixing
    }

    public enum ZeroValveState
    {
        Closed,
        Open,
        InTransit,
        Fault
    }

    /// <summary>
    /// Industrial standard P&ID vector valve component with actuator indicators,
    /// manual override interaction, and real-time SCADA telemetry binding.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroIndustrialValve.bmp")]
    [Category("ZeroUI - SCADA")]
    public class ZIndustrialValve : ControlBase, IScadaBindable
    {
        private ZeroValveType _valveType = ZeroValveType.TwoWaySolenoid;
        private ZeroValveState _state = ZeroValveState.Open;
        private DeviceStatusFlags _statusFlags = DeviceStatusFlags.None;
        private double _positionPercent = 100.0; // 0 = closed, 100 = fully open
        private string _tagLabel = "XV-101";
        private bool _isHovered;
        private IDisposable? _clockToken;
        private bool _lastBlinkState;

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

        [Category("Appearance")]
        [DefaultValue(ZeroValveType.TwoWaySolenoid)]
        public ZeroValveType ValveType
        {
            get => _valveType;
            set { _valveType = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(ZeroValveState.Open)]
        public ZeroValveState State
        {
            get => _state;
            set { _state = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(100.0)]
        public double PositionPercent
        {
            get => _positionPercent;
            set
            {
                _positionPercent = Math.Max(0, Math.Min(100, value));
                _state = _positionPercent switch
                {
                    0.0 => ZeroValveState.Closed,
                    100.0 => ZeroValveState.Open,
                    _ => ZeroValveState.InTransit
                };
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue("XV-101")]
        public string TagLabel
        {
            get => _tagLabel;
            set { _tagLabel = value ?? ""; Invalidate(); }
        }

        public event EventHandler? ValveStateChanged;

        public ZIndustrialValve()
        {
            BackColor = Color.Transparent;
            Size = new Size(54, 62);
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
            if (_state == ZeroValveState.InTransit || _state == ZeroValveState.Fault)
            {
                bool currentBlink = ZeroAnimationClock.BlinkSlow;
                if (currentBlink != _lastBlinkState)
                {
                    _lastBlinkState = currentBlink;
                    if (IsHandleCreated && Visible)
                    {
                        Invalidate();
                    }
                }
            }
        }

        private void OnThemeChanged(object? sender, EventArgs e) => Invalidate();

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnTagValueChanged(tag)));
                return;
            }

            if (tag.Value is bool b)
            {
                PositionPercent = b ? 100.0 : 0.0;
            }
            else if (tag.Value is double d)
            {
                PositionPercent = d;
            }
            else if (tag.Value is int i)
            {
                PositionPercent = i;
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
            // Toggle state on click
            if (_state == ZeroValveState.Open)
            {
                PositionPercent = 0.0;
            }
            else
            {
                PositionPercent = 100.0;
            }

            // Sync with tag engine if bound
            if (!string.IsNullOrEmpty(BoundTagPath))
            {
                ZeroTagEngine.SetTagValue(BoundTagPath!, _state == ZeroValveState.Open);
            }

            ValveStateChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var palette = ZeroTheme.Colors;
            bool isDark = ZeroTheme.IsDark;

            int cx = Width / 2;
            int bodyY = Height - 24;
            int bodyW = 36;
            int bodyH = 20;

            Color stateColor = _state switch
            {
                ZeroValveState.Open => palette.Success,
                ZeroValveState.Closed => palette.TextSecondary,
                ZeroValveState.InTransit => (ZeroAnimationClock.BlinkSlow ? palette.Warning : Color.FromArgb(70, palette.Warning)),
                ZeroValveState.Fault => (ZeroAnimationClock.BlinkSlow ? palette.Danger : Color.FromArgb(70, palette.Danger)),
                _ => palette.Border
            };

            // 1. Actuator Stem & Symbol (Top)
            using (var penStem = new Pen(palette.Border, 2f))
            {
                g.DrawLine(penStem, cx, 14, cx, bodyY);
            }

            // Actuator Top Head
            if (_valveType == ZeroValveType.TwoWaySolenoid)
            {
                // Solenoid Box (Square with 'S')
                Rectangle actRect = new Rectangle(cx - 8, 2, 16, 14);
                using (var brushAct = new SolidBrush(stateColor))
                {
                    g.FillRectangle(brushAct, actRect);
                }
                using (var penAct = new Pen(palette.Border, 1f))
                {
                    g.DrawRectangle(penAct, actRect);
                }
                using (var fontS = new Font("Segoe UI", 7f, FontStyle.Bold))
                using (var brushText = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("S", fontS, brushText, actRect, sf);
                }
            }
            else
            {
                // Diaphragm/Control Actuator Dome
                Rectangle domeRect = new Rectangle(cx - 10, 2, 20, 12);
                using (var brushDome = new SolidBrush(stateColor))
                {
                    g.FillPie(brushDome, domeRect, 180, 180);
                }
                using (var penDome = new Pen(palette.Border, 1.2f))
                {
                    g.DrawPie(penDome, domeRect, 180, 180);
                }
            }

            // 2. Standard P&ID Bowtie Valve Body
            Point leftTop = new Point(cx - bodyW / 2, bodyY);
            Point leftBottom = new Point(cx - bodyW / 2, bodyY + bodyH);
            Point center = new Point(cx, bodyY + bodyH / 2);
            Point rightTop = new Point(cx + bodyW / 2, bodyY);
            Point rightBottom = new Point(cx + bodyW / 2, bodyY + bodyH);

            using var path = new GraphicsPath();
            path.AddPolygon(new[] { leftTop, center, leftBottom });
            path.AddPolygon(new[] { rightTop, center, rightBottom });

            if (_valveType == ZeroValveType.ThreeWayDiverter || _valveType == ZeroValveType.ThreeWayMixing)
            {
                Point bottomPortLeft = new Point(cx - bodyH / 2, bodyY + bodyH + 10);
                Point bottomPortRight = new Point(cx + bodyH / 2, bodyY + bodyH + 10);
                path.AddPolygon(new[] { bottomPortLeft, center, bottomPortRight });
            }

            using (var brushBody = new SolidBrush(stateColor))
            {
                g.FillPath(brushBody, path);
            }

            Color outlineCol = _isHovered ? palette.Primary : palette.Border;
            using (var penOutline = new Pen(outlineCol, _isHovered ? 1.8f : 1.2f))
            {
                g.DrawPath(penOutline, path);
            }

            // 3. Tag Label Text below valve
            if (!string.IsNullOrEmpty(_tagLabel))
            {
                var fontTag = ZeroFontCache.Get("Segoe UI", 7f * DpiScale, FontStyle.Bold);
                using var brushLabel = new SolidBrush(palette.TextPrimary);
                var sfTag = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString(_tagLabel, fontTag, brushLabel, cx, bodyY + bodyH + 2, sfTag);
            }

            // 4. Safety & Interlock Badges (LOTO, Tagout, Interlock, Fault)
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
    /// Legacy alias for <see cref="ZIndustrialValve"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroIndustrialValve is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialValve instead.")]
    [ToolboxItem(false)]
    public class ZeroIndustrialValve : ZIndustrialValve
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZIndustrialValve"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("IndustrialValve is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialValve instead.")]
    [ToolboxItem(false)]
    public class IndustrialValve : ZIndustrialValve
    {
    }

    #endregion
}
