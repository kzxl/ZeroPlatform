using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using ZeroUI.Core.Scene;

namespace ZeroUI.WinForms.Industrial.Scene
{
    /// <summary>
    /// Industrial pipeline scene node.
    /// Supports fluid color coding (water, steam, gas, chemical) and dynamic flow animation.
    /// </summary>
    public class PipeNode : SceneNode
    {
        private float _flowOffset = 0f;

        public float EndX { get; set; }
        public float EndY { get; set; }
        public float PipeDiameter { get; set; } = 12f;
        public Color FluidColor { get; set; } = Color.FromArgb(56, 189, 248); // Light blue
        public bool IsFlowing { get; set; } = true;
        public float FlowSpeed { get; set; } = 25f; // px/sec

        public PipeNode(float startX, float startY, float endX, float endY, float diameter = 12f, string label = "")
        {
            Transform.SetPosition(startX, startY);
            EndX = endX;
            EndY = endY;
            PipeDiameter = diameter;
            Label = label;
            ComputePipeBounds();
        }

        public void SetEndpoints(float startX, float startY, float endX, float endY)
        {
            Transform.SetPosition(startX, startY);
            EndX = endX;
            EndY = endY;
            ComputePipeBounds();
        }

        private void ComputePipeBounds()
        {
            float minX = Math.Min(X, EndX) - PipeDiameter;
            float maxX = Math.Max(X, EndX) + PipeDiameter;
            float minY = Math.Min(Y, EndY) - PipeDiameter;
            float maxY = Math.Max(Y, EndY) + PipeDiameter;

            Width = Math.Max(1f, maxX - minX);
            Height = Math.Max(1f, maxY - minY);
        }

        public override void UpdateAnimation(long elapsedMs)
        {
            base.UpdateAnimation(elapsedMs);

            if (IsFlowing)
            {
                _flowOffset = (_flowOffset + FlowSpeed * (elapsedMs / 1000f)) % 24f;
                NotifyDirty();
            }
        }

        public override void Render(object graphicsContext, in RenderContext context)
        {
            if (!(graphicsContext is Graphics g) || !IsVisible) return;

            float x1 = X;
            float y1 = Y;
            float x2 = EndX;
            float y2 = EndY;

            bool isDark = context.IsDarkTheme;
            Color pipeWallColor = isDark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(148, 163, 184);

            // 1. Pipe Outer Wall
            using (var wallPen = new Pen(pipeWallColor, PipeDiameter + 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(wallPen, x1, y1, x2, y2);
            }

            // 2. Fluid Core
            using (var fluidPen = new Pen(FluidColor, PipeDiameter) { StartCap = LineCap.Flat, EndCap = LineCap.Flat })
            {
                g.DrawLine(fluidPen, x1, y1, x2, y2);
            }

            // 3. Flow Dash Animation
            if (IsFlowing)
            {
                using (var flowPen = new Pen(Color.FromArgb(220, Color.White), PipeDiameter * 0.35f))
                {
                    flowPen.DashStyle = DashStyle.Custom;
                    flowPen.DashPattern = new float[] { 4f, 4f };
                    flowPen.DashOffset = _flowOffset;
                    g.DrawLine(flowPen, x1, y1, x2, y2);
                }
            }

            // 4. Selection Highlight
            if (IsSelected)
            {
                using (var selPen = new Pen(Color.FromArgb(245, 158, 11), 2f) { DashStyle = DashStyle.Dash })
                {
                    var b = WorldBounds;
                    g.DrawRectangle(selPen, b.X - 2, b.Y - 2, b.Width + 4, b.Height + 4);
                }
            }
        }
    }
}
