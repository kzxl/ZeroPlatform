using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Input.Time;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Modern anti-aliased time picker control for ZeroUI.
    /// Supports segmented keyboard/wheel editing (HH:mm:ss), 12h/24h formats,
    /// flyweight preset popup, and headless TimeSegmentModel coordination.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultProperty("Value")]
    [DefaultEvent("ValueChanged")]
    [Description("Modern anti-aliased time picker control")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroTimePicker.bmp")]
    public class ZTimeEdit : ControlBase
    {
        private readonly TimeSegmentModel _model = new TimeSegmentModel();
        private bool _isHovered = false;
        private bool _isFocused = false;
        private bool _isDroppedDown = false;

        private Rectangle _hourRect;
        private Rectangle _minuteRect;
        private Rectangle _secondRect;
        private Rectangle _clockIconRect;

        private readonly DropDownHost _dropdown;
        private readonly TimePresetListControl _presetControl;

        public event EventHandler? ValueChanged;

        public ZTimeEdit()
        {
            Size = new Size(160, 36);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            BackColor = Color.Transparent;

            _model.TimeChanged += (s, e) =>
            {
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            };
            _model.SegmentChanged += (s, e) => Invalidate();

            _presetControl = new TimePresetListControl(this);
            _dropdown = new DropDownHost
            {
                Content = _presetControl
            };
            _dropdown.Opened += (s, e) => { _isDroppedDown = true; Invalidate(); };
            _dropdown.Closed += (s, e) => { _isDroppedDown = false; Invalidate(); };

            ZeroUIConfig.CornerStyleChanged += (s, e) => Invalidate();
            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = ZeroUIConfig.DefaultFont;
                Invalidate();
            };
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            _presetControl?.UpdateTheme();
            Invalidate();
        }

        /// <summary>
        /// Gets the underlying headless time segment model.
        /// </summary>
        [Browsable(false)]
        public TimeSegmentModel Model => _model;

        [Category("Behavior")]
        public TimeSpan Value
        {
            get => _model.Time;
            set => _model.Time = value;
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool ShowSeconds
        {
            get => _model.ShowSeconds;
            set => _model.ShowSeconds = value;
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool Is24Hour
        {
            get => _model.Is24Hour;
            set => _model.Is24Hour = value;
        }

        [Category("Behavior")]
        [DefaultValue(1)]
        public int StepMinutes
        {
            get => _model.StepMinutes;
            set => _model.StepMinutes = value;
        }

        [Browsable(false)]
        public TimeSegment FocusedSegment
        {
            get => _model.FocusedSegment;
            set => _model.FocusedSegment = value;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int delta = e.Delta > 0 ? 1 : -1;
            _model.AdjustCurrentSegment(delta);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Up)
            {
                _model.AdjustCurrentSegment(1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                _model.AdjustCurrentSegment(-1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                _model.MoveNextSegment();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Left)
            {
                _model.MovePreviousSegment();
                e.Handled = true;
            }
            else if (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9)
            {
                _model.TryApplyDigit(e.KeyCode - Keys.D0);
                e.Handled = true;
            }
            else if (e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9)
            {
                _model.TryApplyDigit(e.KeyCode - Keys.NumPad0);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                ShowDropDown();
                e.Handled = true;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (_clockIconRect.Contains(e.Location))
            {
                if (_isDroppedDown) _dropdown.Close();
                else ShowDropDown();
                return;
            }

            if (_hourRect.Contains(e.Location))
            {
                _model.FocusedSegment = TimeSegment.Hour;
            }
            else if (_minuteRect.Contains(e.Location))
            {
                _model.FocusedSegment = TimeSegment.Minute;
            }
            else if (_model.ShowSeconds && _secondRect.Contains(e.Location))
            {
                _model.FocusedSegment = TimeSegment.Second;
            }
        }

        public void ShowDropDown()
        {
            if (!Enabled) return;

            int popW = 200;
            int popH = 190;
            _dropdown.ShowDropDown(this, popW, popH);
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

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            _isFocused = true;
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            _isFocused = false;
            _model.ResetDigitInput();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateSegments();
        }

        private void UpdateSegments()
        {
            int startX = 34;
            int segW = 28;
            int segH = Height - 8;
            int segY = 4;

            _hourRect = new Rectangle(startX, segY, segW, segH);
            _minuteRect = new Rectangle(startX + segW + 10, segY, segW, segH);
            _secondRect = new Rectangle(startX + (segW * 2) + 20, segY, segW, segH);
            _clockIconRect = new Rectangle(Width - 28, (Height - 16) / 2, 16, 16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = CurrentPalette;
            bool isDark = EffectiveSkin.IsDark;

            int effRadius = ZeroUIConfig.GetEffectiveRadius(6);
            var borderRect = new Rectangle(1, 1, Width - 2, Height - 2);

            Color borderColor;
            Color bgColor;

            if (!Enabled)
            {
                borderColor = isDark ? Color.FromArgb(55, 60, 70) : Color.FromArgb(215, 220, 228);
                bgColor = isDark ? Color.FromArgb(28, 33, 43) : Color.FromArgb(243, 246, 249);
            }
            else if (_isFocused || _isDroppedDown)
            {
                borderColor = palette.Primary;
                bgColor = palette.Surface;
            }
            else if (_isHovered)
            {
                borderColor = palette.Primary;
                bgColor = palette.Surface;
            }
            else
            {
                borderColor = palette.Border;
                bgColor = palette.Surface;
            }

            // Fill & Border
            using (var path = ZeroUIConfig.CreateRoundedRectangle(borderRect, effRadius))
            {
                using (var bgBrush = new SolidBrush(bgColor))
                {
                    g.FillPath(bgBrush, path);
                }

                float penWidth = (_isFocused || _isDroppedDown) ? 1.5f : 1.0f;
                using (var pen = new Pen(borderColor, penWidth))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Left Vector Clock Icon
            int iconX = 12;
            int iconY = (Height - 14) / 2;
            using (var clockPen = new Pen(palette.TextSecondary, 1.3f))
            {
                g.DrawEllipse(clockPen, iconX, iconY, 14, 14);
                g.DrawLine(clockPen, iconX + 7, iconY + 3, iconX + 7, iconY + 7);
                g.DrawLine(clockPen, iconX + 7, iconY + 7, iconX + 10, iconY + 7);
            }

            // Draw Segments
            string hStr = _model.DisplayHour.ToString("D2");
            string mStr = _model.DisplayMinute.ToString("D2");
            string sStr = _model.DisplaySecond.ToString("D2");

            UpdateSegments();

            using var focusSegBrush = new SolidBrush(Color.FromArgb(40, palette.Primary));
            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;

            // Hour segment
            if (_isFocused && _model.FocusedSegment == TimeSegment.Hour)
            {
                using var segPath = ZeroUIConfig.CreateRoundedRectangle(_hourRect, 4);
                g.FillPath(focusSegBrush, segPath);
            }
            TextRenderer.DrawText(g, hStr, Font, _hourRect, (_isFocused && _model.FocusedSegment == TimeSegment.Hour) ? palette.Primary : palette.TextPrimary, flags);

            // Separator 1 (:)
            int sep1X = _hourRect.Right;
            var sep1Rect = new Rectangle(sep1X, 0, 10, Height);
            TextRenderer.DrawText(g, ":", Font, sep1Rect, palette.TextSecondary, flags);

            // Minute segment
            if (_isFocused && _model.FocusedSegment == TimeSegment.Minute)
            {
                using var segPath = ZeroUIConfig.CreateRoundedRectangle(_minuteRect, 4);
                g.FillPath(focusSegBrush, segPath);
            }
            TextRenderer.DrawText(g, mStr, Font, _minuteRect, (_isFocused && _model.FocusedSegment == TimeSegment.Minute) ? palette.Primary : palette.TextPrimary, flags);

            if (_model.ShowSeconds)
            {
                // Separator 2 (:)
                int sep2X = _minuteRect.Right;
                var sep2Rect = new Rectangle(sep2X, 0, 10, Height);
                TextRenderer.DrawText(g, ":", Font, sep2Rect, palette.TextSecondary, flags);

                // Second segment
                if (_isFocused && _model.FocusedSegment == TimeSegment.Second)
                {
                    using var segPath = ZeroUIConfig.CreateRoundedRectangle(_secondRect, 4);
                    g.FillPath(focusSegBrush, segPath);
                }
                TextRenderer.DrawText(g, sStr, Font, _secondRect, (_isFocused && _model.FocusedSegment == TimeSegment.Second) ? palette.Primary : palette.TextPrimary, flags);
            }

            // Right Chevron (▼)
            using var chevBrush = new SolidBrush(palette.TextSecondary);
            float cx = _clockIconRect.X + 8;
            float cy = _clockIconRect.Y + 8;
            PointF[] pts = new[]
            {
                new PointF(cx - 3.5f, cy - 2f),
                new PointF(cx + 3.5f, cy - 2f),
                new PointF(cx, cy + 2.5f)
            };
            g.FillPolygon(chevBrush, pts);
        }

        /// <summary>
        /// Flyweight preset list popup for common factory shifts and intervals.
        /// </summary>
        private class TimePresetListControl : Control
        {
            private readonly ZTimeEdit _owner;
            private int _hoveredIndex = -1;
            private readonly List<TimePreset> _presetItems;

            public TimePresetListControl(ZTimeEdit owner)
            {
                _owner = owner;
                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw, true);

                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);

                _presetItems = new List<TimePreset>
                {
                    new TimePreset("Current Time (Now)", DateTime.Now.TimeOfDay)
                };
                _presetItems.AddRange(TimeSegmentModel.DefaultShiftPresets);

                UpdateTheme();
            }

            public void UpdateTheme()
            {
                BackColor = _owner.CurrentPalette.CardBackground;
                Invalidate();
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int itemH = Height / _presetItems.Count;
                if (itemH <= 0) itemH = 22;
                int idx = e.Y / itemH;
                if (idx >= 0 && idx < _presetItems.Count && idx != _hoveredIndex)
                {
                    _hoveredIndex = idx;
                    Invalidate();
                }
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                int itemH = Height / _presetItems.Count;
                if (itemH <= 0) itemH = 22;
                int idx = e.Y / itemH;
                if (idx >= 0 && idx < _presetItems.Count)
                {
                    TimeSpan selected = idx == 0 ? DateTime.Now.TimeOfDay : _presetItems[idx].Time;
                    _owner.Value = selected;
                    _owner._dropdown.Close();
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var palette = _owner.CurrentPalette;
                int itemH = Height / _presetItems.Count;

                using var borderPen = new Pen(palette.Border, 1f);
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

                using var hoverBrush = new SolidBrush(Color.FromArgb(25, palette.Primary));

                for (int i = 0; i < _presetItems.Count; i++)
                {
                    int y = i * itemH;
                    var itemRect = new Rectangle(2, y, Width - 4, itemH);

                    if (i == _hoveredIndex)
                    {
                        g.FillRectangle(hoverBrush, itemRect);
                    }

                    var textRect = new Rectangle(10, y, Width - 20, itemH);
                    var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix;
                    Color txtColor = i == _hoveredIndex ? palette.Primary : palette.TextPrimary;
                    TextRenderer.DrawText(g, _presetItems[i].Label, Font, textRect, txtColor, flags);
                }
            }
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZTimeEdit"/>.
    /// </summary>
    [Obsolete("ZeroTimePicker is deprecated and will be removed in 5 release cycles. Please migrate to ZTimeEdit instead.")]
    [ToolboxItem(false)]
    public class ZeroTimePicker : ZTimeEdit
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZTimeEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("TimeEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZTimeEdit instead.")]
    [ToolboxItem(false)]
    public class TimeEdit : ZTimeEdit
    {
    }

    #endregion
}
