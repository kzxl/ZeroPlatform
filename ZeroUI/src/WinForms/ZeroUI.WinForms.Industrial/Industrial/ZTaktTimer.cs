using System;

using ZeroUI.WinForms.Icons;using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Native;

namespace ZeroUI.WinForms.Industrial
{
    public enum TaktStatus
    {
        OnTrack,    // Green (>20% time remaining)
        Warning,    // Amber (<=20% time remaining)
        Overdue     // Red (Exceeded planned takt)
    }

    /// <summary>
    /// Industrial Takt Time & Cycle Timer for assembly lines and Lean Manufacturing cells.
    /// Visualizes planned vs. actual cycle time with an animated countdown ring, automatic color alerts,
    /// and takt completion telemetry.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [DefaultProperty("TargetTaktSeconds")]
    [DefaultEvent("TaktCompleted")]
    [Description("Industrial Takt Time countdown ring and cycle timer for manufacturing lines")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroTaktTimer.bmp")]
    public class ZTaktTimer : Control
    {
        private float _targetTaktSeconds = 30f;
        private float _elapsedSeconds = 0f;
        private float _averageCycleTime = 28.4f;
        private int _completedUnits = 142;

        private readonly Timer _tickTimer;
        private bool _isRunning = false;
        private bool _blinkPhase = false;
        private bool _hasFiredOverdue = false;

        public event EventHandler? TaktCompleted;
        public event EventHandler? TaktOverdue;

        public ZTaktTimer()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(160, 160);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f);

            _tickTimer = new Timer { Interval = 100 }; // 10Hz high resolution
            _tickTimer.Tick += (s, e) =>
            {
                if (_isRunning)
                {
                    _elapsedSeconds += 0.1f;
                    if (Status == TaktStatus.Overdue)
                    {
                        _blinkPhase = !_blinkPhase;
                        if (!_hasFiredOverdue)
                        {
                            _hasFiredOverdue = true;
                            TaktOverdue?.Invoke(this, EventArgs.Empty);
                        }
                    }
                    Invalidate();
                }
            };


            if (!ZeroDesignHelper.IsInDesignMode(this))
            {
                _tickTimer.Start();
                _isRunning = true;
            }
        }

        [Category("Takt Parameters")]
        [DefaultValue(30f)]
        public float TargetTaktSeconds
        {
            get => _targetTaktSeconds;
            set { _targetTaktSeconds = Math.Max(1f, value); Invalidate(); }
        }

        [Category("Takt Parameters")]
        [DefaultValue(0f)]
        public float ElapsedSeconds
        {
            get => _elapsedSeconds;
            set { _elapsedSeconds = Math.Max(0f, value); Invalidate(); }
        }

        [Category("Takt Parameters")]
        [DefaultValue(28.4f)]
        public float AverageCycleTime
        {
            get => _averageCycleTime;
            set { _averageCycleTime = value; Invalidate(); }
        }

        [Category("Takt Parameters")]
        [DefaultValue(142)]
        public int CompletedUnits
        {
            get => _completedUnits;
            set { _completedUnits = value; Invalidate(); }
        }

        [Browsable(false)]
        public bool IsRunning => _isRunning;

        [Browsable(false)]
        public float RemainingSeconds => Math.Max(0f, _targetTaktSeconds - _elapsedSeconds);

        [Browsable(false)]
        public TaktStatus Status
        {
            get
            {
                if (_elapsedSeconds > _targetTaktSeconds) return TaktStatus.Overdue;
                if (RemainingSeconds <= _targetTaktSeconds * 0.2f) return TaktStatus.Warning;
                return TaktStatus.OnTrack;
            }
        }

        public void Start()
        {
            _isRunning = true;
            if (!ZeroDesignHelper.IsInDesignMode(this)) _tickTimer.Start();
        }

        public void Stop()
        {
            _isRunning = false;
        }

        public void Reset()
        {
            _elapsedSeconds = 0f;
            _hasFiredOverdue = false;
            Invalidate();
        }


        public void CompleteUnit()
        {
            _completedUnits++;
            // Update rolling average cycle time
            _averageCycleTime = (_averageCycleTime * 0.8f) + (_elapsedSeconds * 0.2f);
            TaktCompleted?.Invoke(this, EventArgs.Empty);
            Reset();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = Width;
            int h = Height;
            int size = Math.Min(w, h);
            int margin = 8;
            int arcSize = size - (margin * 2);
            int thickness = 10;

            int centerX = w / 2;
            int centerY = h / 2;

            // 1. Arc Bounding Rectangle
            var arcRect = new Rectangle((w - arcSize) / 2, (h - arcSize) / 2, arcSize, arcSize);

            // 2. Base Track Arc (270 degrees from 135 to 405)
            using (var trackPen = new Pen(Color.FromArgb(226, 232, 240), thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawArc(trackPen, arcRect, 135, 270);
            }

            // 3. Progress Angle Calculation
            float ratio = Math.Min(1f, _elapsedSeconds / _targetTaktSeconds);
            float sweepAngle = 270f * ratio;

            Color statusColor;
            switch (Status)
            {
                case TaktStatus.Overdue:
                    statusColor = _blinkPhase ? Color.FromArgb(239, 68, 68) : Color.FromArgb(185, 28, 28);
                    break;
                case TaktStatus.Warning:
                    statusColor = Color.FromArgb(245, 158, 11);
                    break;
                default:
                    statusColor = Color.FromArgb(16, 185, 129);
                    break;
            }

            // Draw active progress arc
            if (sweepAngle > 0)
            {
                using var progressPen = new Pen(statusColor, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(progressPen, arcRect, 135, sweepAngle);
            }

            // 4. Center Display: Time Readout
            int remain = (int)Math.Ceiling(RemainingSeconds);
            string timeText = (_elapsedSeconds > _targetTaktSeconds)
                ? $"+{_elapsedSeconds - _targetTaktSeconds:F1}s"
                : $"{remain}s";

            using (var timeFont = new Font("Segoe UI", 16f, FontStyle.Bold))
            using (var timeBrush = new SolidBrush(statusColor))
            {
                var sz = g.MeasureString(timeText, timeFont);
                g.DrawString(timeText, timeFont, timeBrush, centerX - (sz.Width / 2), centerY - sz.Height + 4);
            }

            // 5. Center Subtitle
            string subText = (_elapsedSeconds > _targetTaktSeconds) ? "OVERDUE" : "REMAINING";
            using (var subFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var subBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
            {
                var sz = g.MeasureString(subText, subFont);
                g.DrawString(subText, subFont, subBrush, centerX - (sz.Width / 2), centerY + 4);
            }

            // 6. Bottom Legend: Target vs Actual Avg
            string statText = $"Target: {_targetTaktSeconds:F0}s | Avg: {_averageCycleTime:F1}s";
            using (var statFont = new Font("Segoe UI", 7.5f))
            using (var statBrush = new SolidBrush(Color.FromArgb(71, 85, 105)))
            {
                var sz = g.MeasureString(statText, statFont);
                g.DrawString(statText, statFont, statBrush, centerX - (sz.Width / 2), h - sz.Height - 2);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tickTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZTaktTimer"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroTaktTimer is deprecated and will be removed in 5 release cycles. Please migrate to ZTaktTimer instead.")]
    [ToolboxItem(false)]
    public class ZeroTaktTimer : ZTaktTimer
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZTaktTimer"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("TaktTimer is deprecated and will be removed in 5 release cycles. Please migrate to ZTaktTimer instead.")]
    [ToolboxItem(false)]
    public class TaktTimer : ZTaktTimer
    {
    }

    #endregion
}
