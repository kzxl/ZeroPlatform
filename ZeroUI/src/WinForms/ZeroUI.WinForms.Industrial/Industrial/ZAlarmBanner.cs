using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    /// <summary>
    /// ISA-18.2 compliant industrial top/bottom alarm banner ticker control.
    /// Provides real-time visibility of the highest-priority active alarm, severity counts,
    /// and quick operator actions (Acknowledge, Ack All, Silence Horn).
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("ISA-18.2 compliant industrial alarm banner and ticker")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroAlarmGrid.bmp")]
    public class ZAlarmBanner : ControlBase, IAnimationFrameListener
    {
        private string _operatorName = "Operator";
        private bool _isSilenced;
        private bool _blinkState;
        private float _blinkTimer;
        private IDisposable? _clockToken;

        private Rectangle _btnAckRect;
        private Rectangle _btnAckAllRect;
        private Rectangle _btnSilenceRect;
        private Rectangle _contentRect;

        public event EventHandler<ScadaAlarmRecord>? AlarmDetailsRequested;
        public event EventHandler<bool>? SilenceStateChanged;

        [Category("Operator Context")]
        [DefaultValue("Operator")]
        public string OperatorName
        {
            get => _operatorName;
            set => _operatorName = value ?? "Operator";
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool IsSilenced
        {
            get => _isSilenced;
            set
            {
                if (_isSilenced != value)
                {
                    _isSilenced = value;
                    SilenceStateChanged?.Invoke(this, value);
                    Invalidate();
                }
            }
        }

        public ZAlarmBanner()
        {
            Dock = DockStyle.Top;
            Height = 36;
            BackColor = Color.FromArgb(15, 23, 42);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!DesignMode)
            {
                _clockToken = ZeroAnimationClock.Subscribe(OnAnimationFrameTick);
                ScadaAlarmEngine.AlarmStateChanged += OnAlarmStateChanged;
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            _clockToken?.Dispose();
            _clockToken = null;
            ScadaAlarmEngine.AlarmStateChanged -= OnAlarmStateChanged;
        }

        public void OnAnimationFrame(double deltaSeconds, long frameCount)
        {
            OnAnimationFrameTick(deltaSeconds, frameCount);
        }

        private void OnAnimationFrameTick(double deltaSeconds, long frameCount)
        {
            _blinkTimer += (float)deltaSeconds;
            if (_blinkTimer >= 0.5f)
            {
                _blinkTimer = 0f;
                _blinkState = !_blinkState;
                if (IsHandleCreated && Visible)
                {
                    Invalidate();
                }
            }
        }

        private void OnAlarmStateChanged(ScadaAlarmRecord record)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(Invalidate)); } catch { }
            }
            else
            {
                Invalidate();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalculateLayout();
        }

        private void RecalculateLayout()
        {
            int w = Width;
            int h = Height;
            int btnWidth = 72;
            int btnHeight = 24;
            int btnY = (h - btnHeight) / 2;

            _btnSilenceRect = new Rectangle(w - btnWidth - 8, btnY, btnWidth, btnHeight);
            _btnAckAllRect = new Rectangle(_btnSilenceRect.Left - btnWidth - 6, btnY, btnWidth, btnHeight);
            _btnAckRect = new Rectangle(_btnAckAllRect.Left - btnWidth - 6, btnY, btnWidth, btnHeight);

            int contentLeft = 240;
            int contentWidth = Math.Max(50, _btnAckRect.Left - contentLeft - 10);
            _contentRect = new Rectangle(contentLeft, 0, contentWidth, h);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            var highest = ScadaAlarmEngine.GetActiveAlarms().FirstOrDefault();

            if (_btnAckRect.Contains(e.Location))
            {
                if (highest != null && highest.NeedsAck)
                {
                    ScadaAlarmEngine.Acknowledge(highest.Id, _operatorName);
                    Invalidate();
                }
            }
            else if (_btnAckAllRect.Contains(e.Location))
            {
                ScadaAlarmEngine.AcknowledgeAll(_operatorName);
                Invalidate();
            }
            else if (_btnSilenceRect.Contains(e.Location))
            {
                IsSilenced = !IsSilenced;
            }
            else if (_contentRect.Contains(e.Location))
            {
                if (highest != null)
                {
                    AlarmDetailsRequested?.Invoke(this, highest);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int w = Width;
            int h = Height;
            bool isDark = ZeroTheme.IsDark;

            var highest = ScadaAlarmEngine.GetActiveAlarms().FirstOrDefault();
            var counts = ScadaAlarmEngine.GetAlarmSummary();

            // Background color depends on highest active unacknowledged alarm
            Color bgColor = Color.FromArgb(15, 23, 42);
            Color accentColor = Color.FromArgb(71, 85, 105);

            if (highest != null && highest.NeedsAck && _blinkState)
            {
                switch (highest.Severity)
                {
                    case ScadaAlarmSeverity.Critical:
                        bgColor = Color.FromArgb(127, 29, 29); // Pulsing dark red
                        accentColor = Color.FromArgb(239, 68, 68);
                        break;
                    case ScadaAlarmSeverity.High:
                        bgColor = Color.FromArgb(124, 45, 18); // Dark orange
                        accentColor = Color.FromArgb(249, 115, 22);
                        break;
                    case ScadaAlarmSeverity.Medium:
                        bgColor = Color.FromArgb(113, 63, 18); // Dark amber
                        accentColor = Color.FromArgb(245, 158, 11);
                        break;
                    default:
                        bgColor = Color.FromArgb(30, 41, 59);
                        accentColor = Color.FromArgb(59, 130, 246);
                        break;
                }
            }
            else if (highest != null)
            {
                bgColor = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(241, 245, 249);
            }

            using (var bgBrush = new SolidBrush(bgColor))
            {
                g.FillRectangle(bgBrush, 0, 0, w, h);
            }

            // Bottom border
            using (var borderPen = new Pen(accentColor, 1.5f))
            {
                g.DrawLine(borderPen, 0, h - 1, w, h - 1);
            }

            var fontBold = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Bold);
            var fontReg = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Regular);

            // 1. Severity Counters Group (Left)
            int badgeX = 8;
            int badgeY = (h - 20) / 2;

            DrawBadge(g, ref badgeX, badgeY, "CRIT", counts.Critical, Color.FromArgb(239, 68, 68), fontBold);
            DrawBadge(g, ref badgeX, badgeY, "HIGH", counts.High, Color.FromArgb(249, 115, 22), fontBold);
            DrawBadge(g, ref badgeX, badgeY, "MED", counts.Medium, Color.FromArgb(245, 158, 11), fontBold);
            DrawBadge(g, ref badgeX, badgeY, "LOW", counts.Low, Color.FromArgb(59, 130, 246), fontBold);

            // 2. Highest Priority Alarm Message (Center Ticker)
            Color textColor = (highest != null && highest.NeedsAck && _blinkState) ? Color.White : (isDark ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42));

            using (var textBrush = new SolidBrush(textColor))
            {
                if (highest != null)
                {
                    string timeStr = highest.ActiveTimestamp.ToLocalTime().ToString("HH:mm:ss");
                    string alarmLine = $"[{timeStr}] {highest.TagPath} - {highest.Description} (Val: {highest.TriggerValue ?? "N/A"}) [{highest.State}]";
                    g.DrawString(alarmLine, fontBold, textBrush, _contentRect, new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter });
                }
                else
                {
                    using (var okBrush = new SolidBrush(Color.FromArgb(34, 197, 94)))
                    {
                        g.DrawString("✓ ALL SYSTEMS NORMAL - NO ACTIVE ALARMS", fontBold, okBrush, _contentRect, new StringFormat { LineAlignment = StringAlignment.Center });
                    }
                }
            }

            // 3. Action Buttons (Right)
            DrawButton(g, _btnAckRect, "ACK", highest != null && highest.NeedsAck, Color.FromArgb(239, 68, 68), fontBold);
            DrawButton(g, _btnAckAllRect, "ACK ALL", counts.TotalActive > 0, Color.FromArgb(59, 130, 246), fontBold);
            DrawButton(g, _btnSilenceRect, _isSilenced ? "UNSILENCE" : "SILENCE", true, _isSilenced ? Color.FromArgb(245, 158, 11) : Color.FromArgb(100, 116, 139), fontBold);
        }

        private void DrawBadge(Graphics g, ref int x, int y, string label, int count, Color color, Font font)
        {
            string text = $"{label}: {count}";
            var sz = g.MeasureString(text, font);
            int badgeW = (int)sz.Width + 10;
            int badgeH = 20;

            var r = new Rectangle(x, y, badgeW, badgeH);
            Color fill = count > 0 ? color : Color.FromArgb(40, color);
            Color textCol = count > 0 ? Color.White : Color.FromArgb(148, 163, 184);

            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(color, 1f))
            using (var textBrush = new SolidBrush(textCol))
            {
                g.FillRectangle(brush, r);
                g.DrawRectangle(pen, r);
                g.DrawString(text, font, textBrush, x + 5, y + 3);
            }

            x += badgeW + 6;
        }

        private void DrawButton(Graphics g, Rectangle r, string text, bool enabled, Color accentColor, Font font)
        {
            Color btnBg = enabled ? Color.FromArgb(45, 55, 72) : Color.FromArgb(30, 41, 59);
            Color textColor = enabled ? Color.White : Color.FromArgb(100, 116, 139);

            using (var brush = new SolidBrush(btnBg))
            using (var borderPen = new Pen(enabled ? accentColor : Color.FromArgb(51, 65, 85), 1.2f))
            using (var textBrush = new SolidBrush(textColor))
            {
                g.FillRectangle(brush, r);
                g.DrawRectangle(borderPen, r);
                var sz = g.MeasureString(text, font);
                g.DrawString(text, font, textBrush, r.X + (r.Width - sz.Width) / 2, r.Y + (r.Height - sz.Height) / 2);
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZAlarmBanner"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("AlarmBannerControl is deprecated and will be removed in 5 release cycles. Please migrate to ZAlarmBanner instead.")]
    [ToolboxItem(false)]
    public class AlarmBannerControl : ZAlarmBanner
    {
    }

    #endregion
}
