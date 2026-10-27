using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using ZeroUI.Core.Scene;

namespace ZeroUI.WinForms.Industrial.Scene
{
    /// <summary>
    /// Industrial instrument sensor node (ISA-5.1 circular bubble symbol).
    /// Displays tag identifier (e.g. PT-101, TT-202) and dynamic digital engineering value readout.
    /// </summary>
    public class SensorNode : SceneNode
    {
        public string InstrumentType { get; set; } = "PT"; // PT, TT, FT, LT, AT
        public string Unit { get => EngineeringUnit; set => EngineeringUnit = value; }

        public SensorNode(string label = "PT-101", float x = 0f, float y = 0f, float diameter = 48f, string unit = "bar")
        {
            Label = label;
            Transform.SetPosition(x, y);
            Width = diameter;
            Height = diameter;
            EngineeringUnit = unit;
            Value = 0.0;
        }

        public override void Render(object graphicsContext, in RenderContext context)
        {
            if (!(graphicsContext is Graphics g) || !IsVisible) return;

            var bounds = WorldBounds;
            float x = bounds.X;
            float y = bounds.Y;
            float d = Math.Min(bounds.Width, bounds.Height);

            bool isDark = context.IsDarkTheme;
            Color dialBg = isDark ? Color.FromArgb(30, 41, 59) : Color.White;
            Color textColor = isDark ? Color.White : Color.FromArgb(15, 23, 42);
            Color borderColor = GetBorderColor(isDark);

            // 1. Instrument Circle Body
            using (var bgBrush = new SolidBrush(dialBg))
            using (var borderPen = new Pen(borderColor, 2f))
            {
                var circleRect = new RectangleF(x, y, d, d);
                g.FillEllipse(bgBrush, circleRect);
                g.DrawEllipse(borderPen, circleRect);

                // ISA-5.1 Horizontal Split Line
                g.DrawLine(borderPen, x + 2f, y + d * 0.45f, x + d - 2f, y + d * 0.45f);
            }

            // 2. Tag Label (Top Half)
            using (var fontTag = new Font("Segoe UI", Math.Max(6.5f, 7.5f * context.ZoomFactor), FontStyle.Bold))
            using (var textBrush = new SolidBrush(textColor))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                var tagRect = new RectangleF(x, y + 2f, d, d * 0.4f);
                g.DrawString(Label, fontTag, textBrush, tagRect, sf);

                // 3. Process Value + Unit (Bottom Half)
                using (var fontVal = new Font("Segoe UI", Math.Max(7f, 8f * context.ZoomFactor), FontStyle.Bold))
                using (var valBrush = new SolidBrush(Color.FromArgb(56, 189, 248))) // Cyan value
                {
                    var valRect = new RectangleF(x, y + d * 0.45f, d, d * 0.5f);
                    string valStr = string.IsNullOrWhiteSpace(Unit) ? $"{Value:F1}" : $"{Value:F1}\n{Unit}";
                    g.DrawString(valStr, fontVal, valBrush, valRect, sf);
                }
            }

            // 4. Selection Highlight
            if (IsSelected)
            {
                using (var selPen = new Pen(Color.FromArgb(245, 158, 11), 2f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawRectangle(selPen, x - 2, y - 2, d + 4, d + 4);
                }
            }
            else if (IsHovered)
            {
                using (var hovPen = new Pen(Color.FromArgb(59, 130, 246), 1.5f))
                {
                    g.DrawRectangle(hovPen, x - 1, y - 1, d + 2, d + 2);
                }
            }
        }

        private Color GetBorderColor(bool isDark)
        {
            switch (State)
            {
                case ScadaNodeState.Alarm:
                case ScadaNodeState.Fault: return Color.FromArgb(239, 68, 68);    // Red
                case ScadaNodeState.Warning: return Color.FromArgb(245, 158, 11); // Amber
                default: return isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(71, 85, 105);
            }
        }
    }
}
