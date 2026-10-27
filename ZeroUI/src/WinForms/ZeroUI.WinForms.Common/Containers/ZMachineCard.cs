using System;

using ZeroUI.WinForms.Icons;using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Containers
{
    public enum MachineStatus
    {
        Running,
        Idle,
        Alarm,
        Maintenance
    }

    /// <summary>
    /// Industrial compact machine overview card.
    /// Summarizes operational status, control mode, OEE performance gauge, current speed,
    /// and active alarm count in a high-density, single-HWND layout.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Industrial machine faceplate card with OEE gauge and status telemetry")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroMachineCard.bmp")]
    public class ZMachineCard : Control, IScadaBindable
    {
        private string _machineId = "CNC-04";
        private string _machineName = "5-Axis Milling Center";
        private MachineStatus _status = MachineStatus.Running;
        private string _mode = "AUTO";
        private double _oeePercent = 88.5;
        private double _speedRpm = 12000.0;
        private int _alarmCount = 0;
        private int _partCount = 1420;
        private bool _isHovered;

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        public event EventHandler? CardClicked;

        [Category("Machine Profile")]
        [DefaultValue("CNC-04")]
        public string MachineId
        {
            get => _machineId;
            set { _machineId = value ?? ""; Invalidate(); }
        }

        [Category("Machine Profile")]
        [DefaultValue("5-Axis Milling Center")]
        public string MachineName
        {
            get => _machineName;
            set { _machineName = value ?? ""; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(MachineStatus.Running)]
        public MachineStatus Status
        {
            get => _status;
            set { _status = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue("AUTO")]
        public string Mode
        {
            get => _mode;
            set { _mode = value ?? "AUTO"; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(88.5)]
        public double OeePercent
        {
            get => _oeePercent;
            set { _oeePercent = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(12000.0)]
        public double SpeedRpm
        {
            get => _speedRpm;
            set { _speedRpm = Math.Max(0, value); Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(0)]
        public int AlarmCount
        {
            get => _alarmCount;
            set { _alarmCount = Math.Max(0, value); Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(1420)]
        public int PartCount
        {
            get => _partCount;
            set { _partCount = Math.Max(0, value); Invalidate(); }
        }

        public ZMachineCard()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(220, 135);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f);
            Cursor = Cursors.Hand;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!ZeroDesignHelper.IsInDesignMode(this))
            {
                ZeroTagEngine.RegisterBindable(this);
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            ZeroTagEngine.UnregisterBindable(this);
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag == null) return;
            if (tag.Value is MachineStatus st)
            {
                Status = st;
            }
            else if (double.TryParse(tag.Value?.ToString(), out var num))
            {
                OeePercent = num;
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            CardClicked?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool isDark = ZeroTheme.IsDark;
            Color cardBg = isDark ? Color.FromArgb(15, 23, 42) : Color.White;
            Color borderCol = _isHovered ? Color.FromArgb(59, 130, 246) : (isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240));
            Color textPrimary = isDark ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);
            Color textSecondary = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);

            Color statusCol = _status switch
            {
                MachineStatus.Running => Color.FromArgb(34, 197, 94),
                MachineStatus.Idle => Color.FromArgb(245, 158, 11),
                MachineStatus.Alarm => Color.FromArgb(239, 68, 68),
                _ => Color.FromArgb(148, 163, 184)
            };

            // 1. Card Container
            var rect = new RectangleF(1f, 1f, Width - 3f, Height - 3f);
            using (var path = CreateRoundedRectanglePath(rect, 6f))
            using (var bgBrush = new SolidBrush(cardBg))
            using (var borderPen = new Pen(borderCol, _isHovered ? 2f : 1.2f))
            {
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
            }

            // Status stripe header (Top 4px)
            using (var stripeBrush = new SolidBrush(statusCol))
            {
                g.FillRectangle(stripeBrush, 3f, 2f, Width - 6f, 3f);
            }

            // 2. Machine ID & Name
            using (var idFont = new Font(Font.FontFamily, 9.5f, FontStyle.Bold))
            using (var nameFont = new Font(Font.FontFamily, 7.5f, FontStyle.Regular))
            using (var pBrush = new SolidBrush(textPrimary))
            using (var sBrush = new SolidBrush(textSecondary))
            {
                g.DrawString(_machineId, idFont, pBrush, 10f, 10f);
                g.DrawString(_machineName, nameFont, sBrush, 10f, 28f);

                // Mode Badge
                var modeSize = g.MeasureString(_mode, nameFont);
                float badgeX = Width - modeSize.Width - 16f;
                var modeRect = new RectangleF(badgeX, 10f, modeSize.Width + 8f, 16f);
                using (var modeBg = new SolidBrush(isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(241, 245, 249)))
                using (var modeBorder = new Pen(borderCol, 1f))
                {
                    g.FillRectangle(modeBg, modeRect);
                    g.DrawRectangle(modeBorder, modeRect.X, modeRect.Y, modeRect.Width, modeRect.Height);
                    g.DrawString(_mode, nameFont, pBrush, badgeX + 4f, 11f);
                }
            }

            // 3. OEE Mini Donut Gauge (Left)
            float gaugeSize = 44f;
            float gx = 12f;
            float gy = 50f;
            var gaugeRect = new RectangleF(gx, gy, gaugeSize, gaugeSize);

            using (var trackPen = new Pen(isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(226, 232, 240), 5f))
            using (var oeePen = new Pen(Color.FromArgb(59, 130, 246), 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawArc(trackPen, gaugeRect, -90f, 360f);
                float sweepAngle = (float)(_oeePercent / 100.0 * 360.0);
                if (sweepAngle > 0)
                {
                    g.DrawArc(oeePen, gaugeRect, -90f, sweepAngle);
                }

                // OEE % Text inside donut
                using (var oeeFont = new Font(Font.FontFamily, 7.5f, FontStyle.Bold))
                using (var oeeBrush = new SolidBrush(textPrimary))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString($"{_oeePercent:0}%", oeeFont, oeeBrush, gaugeRect, sf);
                }
            }

            // 4. Telemetry Metrics (Right of Donut)
            float infoX = gx + gaugeSize + 14f;
            using (var labelFont = new Font(Font.FontFamily, 7.5f, FontStyle.Regular))
            using (var valFont = new Font(Font.FontFamily, 8f, FontStyle.Bold))
            using (var sBrush = new SolidBrush(textSecondary))
            using (var pBrush = new SolidBrush(textPrimary))
            {
                g.DrawString("OEE PERFORMANCE", labelFont, sBrush, infoX, 50f);
                g.DrawString($"SPEED: {_speedRpm:0} RPM", valFont, pBrush, infoX, 64f);
                g.DrawString($"PARTS: {_partCount:N0}", valFont, pBrush, infoX, 78f);
            }

            // 5. Card Footer: Status Text & Alarms
            using (var footFont = new Font(Font.FontFamily, 8f, FontStyle.Bold))
            using (var statusBrush = new SolidBrush(statusCol))
            {
                g.DrawString($"● {_status.ToString().ToUpperInvariant()}", footFont, statusBrush, 10f, Height - 22f);

                if (_alarmCount > 0)
                {
                    using (var almBrush = new SolidBrush(Color.FromArgb(239, 68, 68)))
                    {
                        string almText = $"⚠ {_alarmCount} ALARM{(_alarmCount > 1 ? "S" : "")}";
                        var almSize = g.MeasureString(almText, footFont);
                        g.DrawString(almText, footFont, almBrush, Width - almSize.Width - 10f, Height - 22f);
                    }
                }
                else
                {
                    using (var okBrush = new SolidBrush(textSecondary))
                    {
                        string okText = "NO ALARMS";
                        var okSize = g.MeasureString(okText, footFont);
                        g.DrawString(okText, footFont, okBrush, Width - okSize.Width - 10f, Height - 22f);
                    }
                }
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2f;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZMachineCard"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroMachineCard is deprecated and will be removed in 5 release cycles. Please migrate to ZMachineCard instead.")]
    [ToolboxItem(false)]
    public class ZeroMachineCard : ZMachineCard
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZMachineCard"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("MachineCard is deprecated and will be removed in 5 release cycles. Please migrate to ZMachineCard instead.")]
    [ToolboxItem(false)]
    public class MachineCard : ZMachineCard
    {
    }

    #endregion
}
