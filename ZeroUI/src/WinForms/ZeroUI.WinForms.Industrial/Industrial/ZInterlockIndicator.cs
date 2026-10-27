using System;

using ZeroUI.WinForms.Icons;using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    /// <summary>
    /// Industrial safety interlock monitor displaying shield/lock status,
    /// dynamic trip conditions, and expandable diagnostics explaining why equipment cannot run.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Industrial safety interlock status indicator with diagnostic trip condition breakdown")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroInterlockIndicator.bmp")]
    public class ZInterlockIndicator : Control, IScadaBindable
    {
        private bool _isBlocked = false;
        private string _tagLabel = "INTERLOCK";
        private readonly List<string> _activeInterlocks = new List<string>();
        private bool _isHovered;
        private readonly ToolTip _toolTip = new ToolTip();

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("Interlock Status")]
        [DefaultValue(false)]
        public bool IsBlocked
        {
            get => _isBlocked;
            set
            {
                if (_isBlocked != value)
                {
                    _isBlocked = value;
                    UpdateTooltip();
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue("INTERLOCK")]
        public string TagLabel
        {
            get => _tagLabel;
            set { _tagLabel = value ?? ""; Invalidate(); }
        }

        public IReadOnlyList<string> ActiveInterlocks => _activeInterlocks.AsReadOnly();

        public ZInterlockIndicator()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(110, 48);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            UpdateTooltip();
        }

        public void SetInterlockCondition(string conditionName, bool isTripped)
        {
            if (isTripped)
            {
                if (!_activeInterlocks.Contains(conditionName))
                    _activeInterlocks.Add(conditionName);
            }
            else
            {
                _activeInterlocks.Remove(conditionName);
            }

            IsBlocked = _activeInterlocks.Count > 0;
            UpdateTooltip();
            Invalidate();
        }

        public void ClearAllInterlocks()
        {
            _activeInterlocks.Clear();
            IsBlocked = false;
            UpdateTooltip();
            Invalidate();
        }

        private void UpdateTooltip()
        {
            if (_isBlocked && _activeInterlocks.Count > 0)
            {
                string text = "BLOCKED BY:\n• " + string.Join("\n• ", _activeInterlocks);
                _toolTip.SetToolTip(this, text);
            }
            else
            {
                _toolTip.SetToolTip(this, "ALL INTERLOCKS CLEAR - SAFE TO OPERATE");
            }
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
            _toolTip.Dispose();
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag == null) return;
            if (tag.Value is bool b)
            {
                IsBlocked = b;
            }
            else if (int.TryParse(tag.Value?.ToString(), out var i))
            {
                IsBlocked = i != 0;
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool isDark = ZeroTheme.IsDark;
            Color iconColor = _isBlocked ? Color.FromArgb(239, 68, 68) : Color.FromArgb(34, 197, 94);
            Color boxBg = isDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(248, 250, 252);
            Color borderCol = _isHovered ? Color.FromArgb(59, 130, 246) : (isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(203, 213, 225));
            Color textCol = isDark ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);

            // 1. Box Container
            var rect = new RectangleF(1f, 1f, Width - 3f, Height - 3f);
            using (var bgBrush = new SolidBrush(boxBg))
            using (var borderPen = new Pen(borderCol, 1.2f))
            {
                g.FillRectangle(bgBrush, rect);
                g.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width, rect.Height);
            }

            // 2. Shield Icon
            float iconX = 14f;
            float iconY = Height * 0.5f;
            using (var shieldPath = new GraphicsPath())
            {
                shieldPath.AddLine(iconX - 8f, iconY - 9f, iconX + 8f, iconY - 9f);
                shieldPath.AddLine(iconX + 8f, iconY, iconX, iconY + 10f);
                shieldPath.AddLine(iconX, iconY + 10f, iconX - 8f, iconY);
                shieldPath.CloseFigure();

                using (var shieldBrush = new SolidBrush(iconColor))
                using (var shieldPen = new Pen(isDark ? Color.Black : Color.White, 1.5f))
                {
                    g.FillPath(shieldBrush, shieldPath);
                    g.DrawPath(shieldPen, shieldPath);
                }
            }

            // 3. Status Labels
            using (var fontTitle = new Font(Font.FontFamily, 7.5f, FontStyle.Bold))
            using (var fontState = new Font(Font.FontFamily, 8f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139)))
            using (var stateBrush = new SolidBrush(iconColor))
            {
                g.DrawString(_tagLabel, fontTitle, titleBrush, 28f, 7f);
                string stateText = _isBlocked ? "TRIPPED" : "CLEAR";
                g.DrawString(stateText, fontState, stateBrush, 28f, 22f);
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZInterlockIndicator"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroInterlockIndicator is deprecated and will be removed in 5 release cycles. Please migrate to ZInterlockIndicator instead.")]
    [ToolboxItem(false)]
    public class ZeroInterlockIndicator : ZInterlockIndicator
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZInterlockIndicator"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("InterlockIndicator is deprecated and will be removed in 5 release cycles. Please migrate to ZInterlockIndicator instead.")]
    [ToolboxItem(false)]
    public class InterlockIndicator : ZInterlockIndicator
    {
    }

    #endregion
}
