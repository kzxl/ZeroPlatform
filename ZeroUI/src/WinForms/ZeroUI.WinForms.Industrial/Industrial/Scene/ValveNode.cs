using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using ZeroUI.Core.Scene;

namespace ZeroUI.WinForms.Industrial.Scene
{
    /// <summary>
    /// Industrial control valve scene node (ISA / P&ID standard hourglass symbol with actuator).
    /// Indicates discrete Open/Closed states or continuous throttling percentage (0..100%).
    /// </summary>
    public class ValveNode : SceneNode
    {
        public double OpeningPercent
        {
            get => Math.Max(0.0, Math.Min(100.0, Value));
            set => Value = value;
        }

        public bool IsOpen => OpeningPercent > 0.0;
        public bool IsActuatorPneumatic { get; set; } = true;

        public ValveNode(string label = "XV-101", float x = 0f, float y = 0f, float width = 50f, float height = 45f)
        {
            Label = label;
            Transform.SetPosition(x, y);
            Width = width;
            Height = height;
            OpeningPercent = 100.0;
            State = ScadaNodeState.Running;
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
            Color statusColor = GetValveColor();

            float valveBodyTop = y + h * 0.4f;
            float valveBodyHeight = h * 0.6f;
            float midX = x + w * 0.5f;
            float midY = valveBodyTop + valveBodyHeight * 0.5f;

            // 1. Actuator (Pneumatic Dome or Solenoid Box)
            using (var actBrush = new SolidBrush(isDark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(148, 163, 184)))
            using (var actPen = new Pen(isDark ? Color.White : Color.Black, 1.5f))
            {
                // Stem connecting actuator to valve body
                g.DrawLine(actPen, midX, y + h * 0.3f, midX, valveBodyTop);

                if (IsActuatorPneumatic)
                {
                    // Diaphragm dome
                    var domeRect = new RectangleF(midX - w * 0.25f, y, w * 0.5f, h * 0.35f);
                    g.FillPie(actBrush, domeRect.X, domeRect.Y, domeRect.Width, domeRect.Height, 180, 180);
                    g.DrawPie(actPen, domeRect.X, domeRect.Y, domeRect.Width, domeRect.Height, 180, 180);
                }
                else
                {
                    // Solenoid square
                    var solRect = new RectangleF(midX - w * 0.2f, y, w * 0.4f, h * 0.3f);
                    g.FillRectangle(actBrush, solRect);
                    g.DrawRectangle(actPen, solRect.X, solRect.Y, solRect.Width, solRect.Height);
                }
            }

            // 2. Hourglass Valve Body (Left & Right triangles)
            using (var bodyBrush = new SolidBrush(statusColor))
            using (var borderPen = new Pen(isDark ? Color.White : Color.Black, 1.5f))
            {
                // Left Triangle
                PointF[] leftTriangle =
                {
                    new PointF(x, valveBodyTop),
                    new PointF(midX, midY),
                    new PointF(x, valveBodyTop + valveBodyHeight)
                };
                g.FillPolygon(bodyBrush, leftTriangle);
                g.DrawPolygon(borderPen, leftTriangle);

                // Right Triangle
                PointF[] rightTriangle =
                {
                    new PointF(x + w, valveBodyTop),
                    new PointF(midX, midY),
                    new PointF(x + w, valveBodyTop + valveBodyHeight)
                };
                g.FillPolygon(bodyBrush, rightTriangle);
                g.DrawPolygon(borderPen, rightTriangle);
            }

            // 3. Label & Opening %
            using (var font = new Font("Segoe UI", Math.Max(6.5f, 8f * context.ZoomFactor), FontStyle.Bold))
            using (var textBrush = new SolidBrush(isDark ? Color.White : Color.FromArgb(15, 23, 42)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
            {
                string statusText = OpeningPercent >= 99.5 ? "OPEN" : (OpeningPercent <= 0.5 ? "SHUT" : $"{OpeningPercent:F0}%");
                g.DrawString($"{Label}\n{statusText}", font, textBrush, new RectangleF(x - 15f, valveBodyTop + valveBodyHeight + 2f, w + 30f, 26f), sf);
            }

            // 4. Selection Highlight
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

        private Color GetValveColor()
        {
            if (State == ScadaNodeState.Fault || State == ScadaNodeState.Alarm)
                return Color.FromArgb(239, 68, 68); // Red

            if (OpeningPercent >= 99.0)
                return Color.FromArgb(34, 197, 94); // Fully open green

            if (OpeningPercent <= 1.0)
                return Color.FromArgb(100, 116, 139); // Closed slate grey

            return Color.FromArgb(245, 158, 11); // Throttling amber
        }
    }
}
