using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using ZeroUI.Core.Scene;

namespace ZeroUI.WinForms.Industrial.Scene
{
    /// <summary>
    /// Industrial liquid storage vessel / tank scene node.
    /// Renders dynamic liquid level percentage, min/max thresholds, gradient fluid shader, and digital readout.
    /// </summary>
    public class TankNode : SceneNode
    {
        public double LevelPercent
        {
            get => Math.Max(0.0, Math.Min(100.0, Value));
            set => Value = value;
        }

        public double LowAlarmLimit { get; set; } = 15.0;
        public double HighAlarmLimit { get; set; } = 85.0;
        public Color LiquidColor { get; set; } = Color.FromArgb(14, 165, 233); // Sky cyan
        public Color ShellColorDark { get; set; } = Color.FromArgb(51, 65, 85);
        public Color ShellColorLight { get; set; } = Color.FromArgb(203, 213, 225);

        public TankNode(string label = "TK-101", float x = 0f, float y = 0f, float width = 80f, float height = 120f)
        {
            Label = label;
            Transform.SetPosition(x, y);
            Width = width;
            Height = height;
            EngineeringUnit = "%";
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
            Color shellColor = isDark ? ShellColorDark : ShellColorLight;
            Color textColor = isDark ? Color.White : Color.FromArgb(15, 23, 42);

            // 1. Tank Vessel Shell
            using (var shellBrush = new SolidBrush(shellColor))
            using (var borderPen = new Pen(isDark ? Color.FromArgb(100, 116, 139) : Color.FromArgb(148, 163, 184), 2f))
            {
                // Rounded dome top and flat/rounded bottom
                var vesselRect = new RectangleF(x, y, w, h);
                g.FillRectangle(shellBrush, vesselRect);
                g.DrawRectangle(borderPen, vesselRect.X, vesselRect.Y, vesselRect.Width, vesselRect.Height);
            }

            // 2. Liquid Level Fill
            float fillPct = (float)(LevelPercent / 100.0);
            float liquidHeight = (h - 8f) * fillPct;
            if (liquidHeight > 2f)
            {
                float liquidY = y + h - 4f - liquidHeight;
                var liquidRect = new RectangleF(x + 4f, liquidY, w - 8f, liquidHeight);

                Color topColor = Color.FromArgb(200, LiquidColor.R, LiquidColor.G, LiquidColor.B);
                Color botColor = Color.FromArgb(255, (int)(LiquidColor.R * 0.7), (int)(LiquidColor.G * 0.7), (int)(LiquidColor.B * 0.7));

                using (var liquidBrush = new LinearGradientBrush(liquidRect, topColor, botColor, LinearGradientMode.Vertical))
                {
                    g.FillRectangle(liquidBrush, liquidRect);
                }

                // Surface meniscus line
                using (var surfacePen = new Pen(Color.White, 1.5f))
                {
                    g.DrawLine(surfacePen, liquidRect.Left, liquidRect.Top, liquidRect.Right, liquidRect.Top);
                }
            }

            // 3. Alarm Threshold Lines (Low / High)
            using (var alarmPen = new Pen(Color.FromArgb(239, 68, 68), 1.5f) { DashStyle = DashStyle.Dash })
            {
                float highY = y + h - 4f - (float)((h - 8f) * (HighAlarmLimit / 100.0));
                g.DrawLine(alarmPen, x + 2f, highY, x + w - 2f, highY);

                float lowY = y + h - 4f - (float)((h - 8f) * (LowAlarmLimit / 100.0));
                g.DrawLine(alarmPen, x + 2f, lowY, x + w - 2f, lowY);
            }

            // 4. Readout & Label
            using (var font = new Font("Segoe UI", Math.Max(7f, 9f * context.ZoomFactor), FontStyle.Bold))
            using (var textBrush = new SolidBrush(textColor))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                // Equipment Tag at Top
                g.DrawString(Label, font, textBrush, new RectangleF(x, y + 4f, w, 16f), sf);

                // Percentage Readout in Middle / Bottom
                string valStr = $"{LevelPercent:F1}%";
                g.DrawString(valStr, font, textBrush, new RectangleF(x, y + h - 22f, w, 16f), sf);
            }

            // 5. Selection / Hover Adornments
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
    }
}
