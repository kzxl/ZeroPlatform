using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using ZeroUI.Core.Scene;

namespace ZeroUI.WinForms.Industrial.Scene
{
    /// <summary>
    /// Centrifugal pump scene node with volute casing, tangential discharge, and dynamic animated impeller.
    /// State-dependent color coding (Green=Running, Grey=Stopped, Amber=Warning, Red=Fault).
    /// </summary>
    public class PumpNode : SceneNode
    {
        private float _impellerAngle = 0f;
        public float SpeedRpm { get; set; } = 1450f;

        public PumpNode(string label = "P-101A", float x = 0f, float y = 0f, float size = 60f)
        {
            Label = label;
            Transform.SetPosition(x, y);
            Width = size;
            Height = size;
            State = ScadaNodeState.Stopped;
        }

        public override void UpdateAnimation(long elapsedMs)
        {
            base.UpdateAnimation(elapsedMs);

            if (State == ScadaNodeState.Running)
            {
                // Rotate impeller based on speed
                _impellerAngle = (_impellerAngle + (SpeedRpm / 60f) * 360f * (elapsedMs / 1000f)) % 360f;
                NotifyDirty();
            }
        }

        public override void Render(object graphicsContext, in RenderContext context)
        {
            if (!(graphicsContext is Graphics g) || !IsVisible) return;

            var bounds = WorldBounds;
            float x = bounds.X;
            float y = bounds.Y;
            float w = bounds.Width;
            float h = bounds.Height;

            bool isDark = context.IsDarkTheme;
            Color stateColor = GetStateColor();

            // 1. Pump Base / Footing
            using (var baseBrush = new SolidBrush(isDark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(148, 163, 184)))
            {
                g.FillRectangle(baseBrush, x + w * 0.15f, y + h * 0.85f, w * 0.7f, h * 0.15f);
            }

            // 2. Volute Discharge Nozzle (top right)
            using (var bodyBrush = new SolidBrush(stateColor))
            using (var borderPen = new Pen(isDark ? Color.White : Color.Black, 1.5f))
            {
                var nozzleRect = new RectangleF(x + w * 0.55f, y, w * 0.25f, h * 0.45f);
                g.FillRectangle(bodyBrush, nozzleRect);
                g.DrawRectangle(borderPen, nozzleRect.X, nozzleRect.Y, nozzleRect.Width, nozzleRect.Height);

                // Volute Circular Casing
                var casingRect = new RectangleF(x + w * 0.05f, y + h * 0.15f, w * 0.75f, h * 0.75f);
                g.FillEllipse(bodyBrush, casingRect);
                g.DrawEllipse(borderPen, casingRect);
            }

            // 3. Rotating Impeller Blades
            float centerX = x + w * 0.425f;
            float centerY = y + h * 0.525f;
            float radius = w * 0.25f;

            using (var bladePen = new Pen(isDark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42), 2f))
            {
                float baseAngle = State == ScadaNodeState.Running ? _impellerAngle : 0f;
                for (int i = 0; i < 4; i++)
                {
                    float angleRad = (float)((baseAngle + i * 90) * Math.PI / 180.0);
                    float tipX = centerX + radius * (float)Math.Cos(angleRad);
                    float tipY = centerY + radius * (float)Math.Sin(angleRad);
                    g.DrawLine(bladePen, centerX, centerY, tipX, tipY);
                }

                // Center hub
                using (var hubBrush = new SolidBrush(isDark ? Color.White : Color.Black))
                {
                    g.FillEllipse(hubBrush, centerX - 3f, centerY - 3f, 6f, 6f);
                }
            }

            // 4. Equipment Label
            using (var font = new Font("Segoe UI", Math.Max(7f, 8.5f * context.ZoomFactor), FontStyle.Bold))
            using (var textBrush = new SolidBrush(isDark ? Color.White : Color.FromArgb(15, 23, 42)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
            {
                g.DrawString(Label, font, textBrush, new RectangleF(x - 10f, y + h + 2f, w + 20f, 16f), sf);
            }

            // 5. Selection / Hover
            if (IsSelected)
            {
                using (var selPen = new Pen(Color.FromArgb(245, 158, 11), 2f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawRectangle(selPen, x - 2, y - 2, w + 4, h + 4);
                }
            }
            else if (IsHovered)
            {
                using (var hovPen = new Pen(Color.FromArgb(59, 130, 246), 1.5f))
                {
                    g.DrawRectangle(hovPen, x - 1, y - 1, w + 2, h + 2);
                }
            }
        }

        private Color GetStateColor()
        {
            switch (State)
            {
                case ScadaNodeState.Running: return Color.FromArgb(34, 197, 94);   // Green
                case ScadaNodeState.Warning: return Color.FromArgb(245, 158, 11);  // Amber
                case ScadaNodeState.Alarm:
                case ScadaNodeState.Fault: return Color.FromArgb(239, 68, 68);     // Red
                default: return Color.FromArgb(100, 116, 139);                     // Slate grey
            }
        }
    }
}
