using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Scene;

namespace ZeroUI.WinForms.Industrial.Scene
{
    /// <summary>
    /// ISA-18.2 compliant industrial alarm beacon scene node.
    /// Features dynamic flashing for unacknowledged alarms, steady glow for acknowledged alarms, and severity color coding.
    /// </summary>
    public class AlarmNode : SceneNode
    {
        private bool _flashState = true;
        private long _lastFlashTime = 0;

        public ScadaAlarmSeverity Severity { get; set; } = ScadaAlarmSeverity.High;
        public bool IsActive { get; set; } = true;
        public bool IsAcknowledged { get; set; } = false;
        public string AlarmMessage { get; set; } = "High Pressure Alarm";

        public AlarmNode(string label = "ALM-01", float x = 0f, float y = 0f, float size = 40f)
        {
            Label = label;
            Transform.SetPosition(x, y);
            Width = size;
            Height = size;
            State = ScadaNodeState.Alarm;
        }

        public override void UpdateAnimation(long elapsedMs)
        {
            base.UpdateAnimation(elapsedMs);

            if (IsActive && !IsAcknowledged)
            {
                _lastFlashTime += elapsedMs;
                if (_lastFlashTime >= 500) // Flash at 2 Hz
                {
                    _flashState = !_flashState;
                    _lastFlashTime = 0;
                    NotifyDirty();
                }
            }
            else
            {
                _flashState = true;
            }
        }

        public override void Render(object graphicsContext, in RenderContext context)
        {
            if (!(graphicsContext is Graphics g) || !IsVisible) return;

            var bounds = WorldBounds;
            float x = bounds.X;
            float y = bounds.Y;
            float s = Math.Min(bounds.Width, bounds.Height);

            bool isDark = context.IsDarkTheme;
            Color sevColor = GetSeverityColor();

            // Flash visibility
            Color fillColor = (IsActive && (!IsAcknowledged ? _flashState : true))
                ? sevColor
                : (isDark ? Color.FromArgb(40, 50, 70) : Color.FromArgb(200, 210, 225));

            // 1. Diamond Alarm Beacon Shape
            PointF[] diamond =
            {
                new PointF(x + s * 0.5f, y),
                new PointF(x + s, y + s * 0.5f),
                new PointF(x + s * 0.5f, y + s),
                new PointF(x, y + s * 0.5f)
            };

            using (var fillBrush = new SolidBrush(fillColor))
            using (var borderPen = new Pen(isDark ? Color.White : Color.Black, 2f))
            {
                g.FillPolygon(fillBrush, diamond);
                g.DrawPolygon(borderPen, diamond);
            }

            // 2. Exclamation Glyph (!)
            using (var font = new Font("Segoe UI", Math.Max(8f, 10f * context.ZoomFactor), FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.White))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("!", font, textBrush, new RectangleF(x, y, s, s), sf);

                // Label below beacon
                using (var lblFont = new Font("Segoe UI", Math.Max(6.5f, 7.5f * context.ZoomFactor), FontStyle.Bold))
                using (var lblBrush = new SolidBrush(isDark ? Color.White : Color.FromArgb(15, 23, 42)))
                {
                    g.DrawString(Label, lblFont, lblBrush, new RectangleF(x - 20f, y + s + 2f, s + 40f, 16f), sf);
                }
            }

            // 3. Selection Highlight
            if (IsSelected)
            {
                using (var selPen = new Pen(Color.FromArgb(245, 158, 11), 2f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawRectangle(selPen, x - 2, y - 2, s + 4, s + 4);
                }
            }
        }

        private Color GetSeverityColor()
        {
            switch (Severity)
            {
                case ScadaAlarmSeverity.Critical: return Color.FromArgb(220, 38, 38); // Crimson
                case ScadaAlarmSeverity.High: return Color.FromArgb(239, 68, 68);     // Red
                case ScadaAlarmSeverity.Medium: return Color.FromArgb(245, 158, 11);  // Amber
                case ScadaAlarmSeverity.Low: return Color.FromArgb(59, 130, 246);     // Blue
                default: return Color.FromArgb(100, 116, 139);
            }
        }
    }
}
