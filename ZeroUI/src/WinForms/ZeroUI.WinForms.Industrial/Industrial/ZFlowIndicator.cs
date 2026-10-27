using System;

using ZeroUI.WinForms.Icons;using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    public enum FlowIndicatorDirection
    {
        LeftToRight,
        RightToLeft,
        TopToBottom,
        BottomToTop
    }

    /// <summary>
    /// Directional animated fluid flow indicator displaying moving chevron vector patterns,
    /// dynamic velocity scaling, and real-time SCADA telemetry synchronization.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Directional animated fluid flow indicator with moving chevron vectors")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroFlowIndicator.bmp")]
    public class ZFlowIndicator : Control, IScadaBindable, IAnimationFrameListener
    {
        private FlowIndicatorDirection _direction = FlowIndicatorDirection.LeftToRight;
        private double _velocity = 1.5; // m/s
        private bool _isFlowing = true;
        private Color _flowColor = Color.FromArgb(6, 182, 212); // Cyan
        private float _animPhase = 0f;
        private IDisposable? _clockToken;

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("Appearance")]
        [DefaultValue(FlowIndicatorDirection.LeftToRight)]
        public FlowIndicatorDirection Direction
        {
            get => _direction;
            set { _direction = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(1.5)]
        public double Velocity
        {
            get => _velocity;
            set { _velocity = Math.Max(0, value); Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(true)]
        public bool IsFlowing
        {
            get => _isFlowing;
            set { _isFlowing = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color FlowColor
        {
            get => _flowColor;
            set { _flowColor = value; Invalidate(); }
        }

        public ZFlowIndicator()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(120, 36);
            BackColor = Color.Transparent;
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

        public void OnAnimationFrame(double deltaSeconds, long frameCount)
        {
            OnAnimationFrameTick(deltaSeconds, frameCount);
        }

        private void OnAnimationFrameTick(double deltaSeconds, long frameCount)
        {
            if (!_isFlowing || _velocity <= 0) return;

            float step = (float)(_velocity * 40.0 * deltaSeconds);
            _animPhase = (_animPhase + step) % 24f;

            if (IsHandleCreated && Visible)
            {
                Invalidate();
            }
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag == null) return;
            if (double.TryParse(tag.Value?.ToString(), out var vel))
            {
                Velocity = vel;
                IsFlowing = vel > 0.05;
            }
            else if (tag.Value is bool b)
            {
                IsFlowing = b;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool isDark = ZeroTheme.IsDark;
            Color trackColor = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(226, 232, 240);
            Color arrowColor = _isFlowing ? _flowColor : (isDark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(148, 163, 184));

            // Background duct channel
            using (var trackBrush = new SolidBrush(trackColor))
            using (var borderPen = new Pen(isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(203, 213, 225), 1f))
            {
                g.FillRectangle(trackBrush, 0, 0, Width - 1, Height - 1);
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }

            // Draw animated directional chevrons
            using (var arrowPen = new Pen(arrowColor, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                float spacing = 24f;
                bool isHorizontal = _direction == FlowIndicatorDirection.LeftToRight || _direction == FlowIndicatorDirection.RightToLeft;

                if (isHorizontal)
                {
                    float cy = Height * 0.5f;
                    float arm = 8f;
                    float offset = _direction == FlowIndicatorDirection.LeftToRight ? _animPhase : -_animPhase;

                    float x = -spacing + (offset % spacing);
                    while (x < Width + spacing)
                    {
                        if (_direction == FlowIndicatorDirection.LeftToRight)
                        {
                            g.DrawLine(arrowPen, x - arm, cy - arm, x, cy);
                            g.DrawLine(arrowPen, x, cy, x - arm, cy + arm);
                        }
                        else
                        {
                            g.DrawLine(arrowPen, x + arm, cy - arm, x, cy);
                            g.DrawLine(arrowPen, x, cy, x + arm, cy + arm);
                        }
                        x += spacing;
                    }
                }
                else
                {
                    float cx = Width * 0.5f;
                    float arm = 8f;
                    float offset = _direction == FlowIndicatorDirection.TopToBottom ? _animPhase : -_animPhase;

                    float y = -spacing + (offset % spacing);
                    while (y < Height + spacing)
                    {
                        if (_direction == FlowIndicatorDirection.TopToBottom)
                        {
                            g.DrawLine(arrowPen, cx - arm, y - arm, cx, y);
                            g.DrawLine(arrowPen, cx, y, cx + arm, y - arm);
                        }
                        else
                        {
                            g.DrawLine(arrowPen, cx - arm, y + arm, cx, y);
                            g.DrawLine(arrowPen, cx, y, cx + arm, y + arm);
                        }
                        y += spacing;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZFlowIndicator"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroFlowIndicator is deprecated and will be removed in 5 release cycles. Please migrate to ZFlowIndicator instead.")]
    [ToolboxItem(false)]
    public class ZeroFlowIndicator : ZFlowIndicator
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZFlowIndicator"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("FlowIndicator is deprecated and will be removed in 5 release cycles. Please migrate to ZFlowIndicator instead.")]
    [ToolboxItem(false)]
    public class FlowIndicator : ZFlowIndicator
    {
    }

    #endregion
}
