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
    public enum MachineControlMode
    {
        Auto,
        Manual,
        Remote,
        Local
    }

    /// <summary>
    /// Industrial 4-position segmented mode selector (Auto, Manual, Remote, Local)
    /// with lockout protection and direct SCADA tag engine telemetry binding.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Industrial segmented control mode selector with lockout protection")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroModeSelector.bmp")]
    public class ZModeSelector : Control, IScadaBindable
    {
        private MachineControlMode _selectedMode = MachineControlMode.Auto;
        private bool _isLocked = false;
        private string _tagLabel = "MODE";
        private int _hoveredSegment = -1;

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        public event EventHandler<MachineControlMode>? ModeChanged;

        [Category("Mode Selection")]
        [DefaultValue(MachineControlMode.Auto)]
        public MachineControlMode SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (_selectedMode != value && !_isLocked)
                {
                    _selectedMode = value;
                    Invalidate();
                    ModeChanged?.Invoke(this, _selectedMode);
                }
            }
        }

        [Category("Mode Selection")]
        [DefaultValue(false)]
        public bool IsLocked
        {
            get => _isLocked;
            set { _isLocked = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("MODE")]
        public string TagLabel
        {
            get => _tagLabel;
            set { _tagLabel = value ?? ""; Invalidate(); }
        }

        public ZModeSelector()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(240, 48);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
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
            if (tag.Value is MachineControlMode mode)
            {
                SelectedMode = mode;
            }
            else if (Enum.TryParse<MachineControlMode>(tag.Value?.ToString(), true, out var parsed))
            {
                SelectedMode = parsed;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int seg = GetSegmentAt(e.X);
            if (seg != _hoveredSegment)
            {
                _hoveredSegment = seg;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredSegment = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || _isLocked) return;

            int seg = GetSegmentAt(e.X);
            if (seg >= 0 && seg < 4)
            {
                SelectedMode = (MachineControlMode)seg;
                if (!string.IsNullOrEmpty(BoundTagPath))
                {
                    ZeroTagEngine.SetTagValue(BoundTagPath!, SelectedMode.ToString());
                }
            }
        }

        private int GetSegmentAt(int mouseX)
        {
            float segW = Width / 4f;
            return Math.Max(0, Math.Min(3, (int)(mouseX / segW)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool isDark = ZeroTheme.IsDark;
            Color trackBg = isDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(241, 245, 249);
            Color borderCol = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(203, 213, 225);
            Color textInactive = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);

            // 1. Segmented Track Outer Box
            var rect = new RectangleF(1f, 1f, Width - 3f, Height - 3f);
            using (var trackBrush = new SolidBrush(trackBg))
            using (var trackPen = new Pen(borderCol, 1f))
            {
                g.FillRectangle(trackBrush, rect);
                g.DrawRectangle(trackPen, rect.X, rect.Y, rect.Width, rect.Height);
            }

            // 2. Segments
            string[] modes = { "AUTO", "MANUAL", "REMOTE", "LOCAL" };
            float segW = rect.Width / 4f;

            for (int i = 0; i < 4; i++)
            {
                bool isSelected = (int)_selectedMode == i;
                bool isHovered = _hoveredSegment == i && !_isLocked;
                var segRect = new RectangleF(rect.X + i * segW + 2f, rect.Y + 2f, segW - 4f, rect.Height - 4f);

                if (isSelected)
                {
                    Color selBg = i == 0 ? Color.FromArgb(22, 163, 74) : (i == 1 ? Color.FromArgb(217, 119, 6) : Color.FromArgb(37, 99, 235));
                    using (var selBrush = new SolidBrush(selBg))
                    {
                        g.FillRectangle(selBrush, segRect);
                    }
                }
                else if (isHovered)
                {
                    using (var hovBrush = new SolidBrush(isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(226, 232, 240)))
                    {
                        g.FillRectangle(hovBrush, segRect);
                    }
                }

                // Text
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var textBrush = new SolidBrush(isSelected ? Color.White : textInactive))
                {
                    g.DrawString(modes[i], Font, textBrush, segRect, sf);
                }
            }

            // Lockout Indicator overlay
            if (_isLocked)
            {
                using (var lockBrush = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                {
                    g.FillRectangle(lockBrush, rect);
                }
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var fontLock = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.FromArgb(248, 113, 113)))
                {
                    g.DrawString("🔒 LOCKED", fontLock, textBrush, rect, sf);
                }
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZModeSelector"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroModeSelector is deprecated and will be removed in 5 release cycles. Please migrate to ZModeSelector instead.")]
    [ToolboxItem(false)]
    public class ZeroModeSelector : ZModeSelector
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZModeSelector"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ModeSelector is deprecated and will be removed in 5 release cycles. Please migrate to ZModeSelector instead.")]
    [ToolboxItem(false)]
    public class ModeSelector : ZModeSelector
    {
    }

    #endregion
}
