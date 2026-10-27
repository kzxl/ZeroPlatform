using System;

using ZeroUI.WinForms.Icons;using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    /// <summary>
    /// Industrial shift status and crew assignment card displaying active shift name,
    /// scheduled hours, shift supervisor, operator on duty, and accumulated machine downtime.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Industrial shift overview card with crew assignment and downtime tracker")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroShiftStatus.bmp")]
    public class ZShiftStatus : Control, IScadaBindable
    {
        private string _shiftName = "SHIFT A (DAY)";
        private string _shiftHours = "06:00 - 14:00";
        private string _operatorName = "David Nguyen";
        private string _supervisorName = "Alex Thorne";
        private TimeSpan _downtimeToday = TimeSpan.FromMinutes(24);
        private bool _isHovered;

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("Shift Info")]
        [DefaultValue("SHIFT A (DAY)")]
        public string ShiftName
        {
            get => _shiftName;
            set { _shiftName = value ?? ""; Invalidate(); }
        }

        [Category("Shift Info")]
        [DefaultValue("06:00 - 14:00")]
        public string ShiftHours
        {
            get => _shiftHours;
            set { _shiftHours = value ?? ""; Invalidate(); }
        }

        [Category("Shift Crew")]
        [DefaultValue("David Nguyen")]
        public string OperatorName
        {
            get => _operatorName;
            set { _operatorName = value ?? ""; Invalidate(); }
        }

        [Category("Shift Crew")]
        [DefaultValue("Alex Thorne")]
        public string SupervisorName
        {
            get => _supervisorName;
            set { _supervisorName = value ?? ""; Invalidate(); }
        }

        [Category("Performance")]
        public TimeSpan DowntimeToday
        {
            get => _downtimeToday;
            set { _downtimeToday = value; Invalidate(); }
        }

        public ZShiftStatus()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(240, 110);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f);
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
            if (double.TryParse(tag.Value?.ToString(), out var dtMinutes))
            {
                DowntimeToday = TimeSpan.FromMinutes(dtMinutes);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool isDark = ZeroTheme.IsDark;
            Color panelBg = isDark ? Color.FromArgb(15, 23, 42) : Color.White;
            Color borderCol = _isHovered ? Color.FromArgb(59, 130, 246) : (isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240));
            Color textPrimary = isDark ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);
            Color textSecondary = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);

            // 1. Container
            var rect = new RectangleF(1f, 1f, Width - 3f, Height - 3f);
            using (var bgBrush = new SolidBrush(panelBg))
            using (var borderPen = new Pen(borderCol, 1.2f))
            {
                g.FillRectangle(bgBrush, rect);
                g.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width, rect.Height);
            }

            // Top accent stripe (Purple/Indigo)
            using (var stripeBrush = new SolidBrush(Color.FromArgb(99, 102, 241)))
            {
                g.FillRectangle(stripeBrush, 2f, 2f, Width - 4f, 3f);
            }

            // 2. Shift Header
            using (var hFont = new Font(Font.FontFamily, 8.5f, FontStyle.Bold))
            using (var subFont = new Font(Font.FontFamily, 7.5f, FontStyle.Regular))
            using (var pBrush = new SolidBrush(textPrimary))
            using (var sBrush = new SolidBrush(textSecondary))
            {
                g.DrawString(_shiftName, hFont, pBrush, 10f, 10f);
                g.DrawString(_shiftHours, subFont, sBrush, 10f, 26f);

                // 3. Crew assignments
                g.DrawString($"OPERATOR: {_operatorName}", subFont, pBrush, 10f, 48f);
                g.DrawString($"SUPERVISOR: {_supervisorName}", subFont, sBrush, 10f, 64f);

                // 4. Downtime indicator
                string dtStr = $"DOWNTIME: {_downtimeToday.Hours:00}h {_downtimeToday.Minutes:00}m";
                Color dtCol = _downtimeToday.TotalMinutes > 30 ? Color.FromArgb(239, 68, 68) : Color.FromArgb(245, 158, 11);
                using (var dtBrush = new SolidBrush(dtCol))
                {
                    g.DrawString(dtStr, hFont, dtBrush, 10f, Height - 22f);
                }
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZShiftStatus"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroShiftStatus is deprecated and will be removed in 5 release cycles. Please migrate to ZShiftStatus instead.")]
    [ToolboxItem(false)]
    public class ZeroShiftStatus : ZShiftStatus
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZShiftStatus"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ShiftStatus is deprecated and will be removed in 5 release cycles. Please migrate to ZShiftStatus instead.")]
    [ToolboxItem(false)]
    public class ShiftStatus : ZShiftStatus
    {
    }

    #endregion
}
