using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Docking
{
    /// <summary>
    /// Visual Docking Diamond HUD overlay control for DockManager.
    /// Renders 5-zone docking targets (Center/Document, Left, Top, Right, Bottom)
    /// and dynamic translucent preview zones during panel drag operations.
    /// </summary>
    internal class DockGuideHUD : Control
    {
        private readonly ZDockManager _manager;
        private DockPosition? _hoveredPosition;

        private Rectangle _centerRect;
        private Rectangle _leftRect;
        private Rectangle _rightRect;
        private Rectangle _topRect;
        private Rectangle _bottomRect;

        public DockPosition? HoveredPosition => _hoveredPosition;

        public DockGuideHUD(ZDockManager manager)
        {
            _manager = manager;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Visible = false;
        }

        public void UpdateMouse(Point screenPoint)
        {
            Point clientPt = PointToClient(screenPoint);
            DockPosition? newPos = null;

            if (_centerRect.Contains(clientPt)) newPos = DockPosition.Document;
            else if (_leftRect.Contains(clientPt)) newPos = DockPosition.Left;
            else if (_rightRect.Contains(clientPt)) newPos = DockPosition.Right;
            else if (_topRect.Contains(clientPt)) newPos = DockPosition.Top;
            else if (_bottomRect.Contains(clientPt)) newPos = DockPosition.Bottom;

            if (_hoveredPosition != newPos)
            {
                _hoveredPosition = newPos;
                Invalidate();
            }
        }

        public void Reset()
        {
            _hoveredPosition = null;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ComputeGuideRectangles();
        }

        private void ComputeGuideRectangles()
        {
            int cx = Width / 2;
            int cy = Height / 2;
            int size = 36;
            int spacing = 44;

            _centerRect = new Rectangle(cx - size / 2, cy - size / 2, size, size);
            _leftRect = new Rectangle(cx - size / 2 - spacing, cy - size / 2, size, size);
            _rightRect = new Rectangle(cx - size / 2 + spacing, cy - size / 2, size, size);
            _topRect = new Rectangle(cx - size / 2, cy - size / 2 - spacing, size, size);
            _bottomRect = new Rectangle(cx - size / 2, cy - size / 2 + spacing, size, size);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var palette = ZeroTheme.Colors;

            // 1. Draw Translucent Dock Preview Zone
            if (_hoveredPosition.HasValue)
            {
                Rectangle previewRect = _hoveredPosition.Value switch
                {
                    DockPosition.Left => new Rectangle(0, 0, Math.Min(280, Width / 2), Height),
                    DockPosition.Right => new Rectangle(Width - Math.Min(280, Width / 2), 0, Math.Min(280, Width / 2), Height),
                    DockPosition.Top => new Rectangle(0, 0, Width, Math.Min(200, Height / 2)),
                    DockPosition.Bottom => new Rectangle(0, Height - Math.Min(200, Height / 2), Width, Math.Min(200, Height / 2)),
                    _ => new Rectangle(40, 40, Math.Max(100, Width - 80), Math.Max(100, Height - 80))
                };

                using var fillBrush = new SolidBrush(Color.FromArgb(55, palette.Primary));
                g.FillRectangle(fillBrush, previewRect);
                using var borderPen = new Pen(palette.Primary, 2f);
                g.DrawRectangle(borderPen, previewRect);
            }

            // 2. Draw Diamond HUD Buttons
            ComputeGuideRectangles();
            DrawGuideButton(g, _centerRect, DockPosition.Document, palette);
            DrawGuideButton(g, _leftRect, DockPosition.Left, palette);
            DrawGuideButton(g, _rightRect, DockPosition.Right, palette);
            DrawGuideButton(g, _topRect, DockPosition.Top, palette);
            DrawGuideButton(g, _bottomRect, DockPosition.Bottom, palette);
        }

        private void DrawGuideButton(Graphics g, Rectangle r, DockPosition pos, ZeroThemePalette palette)
        {
            bool isHovered = _hoveredPosition == pos;
            Color bgColor = isHovered ? palette.Primary : Color.FromArgb(240, palette.Surface);
            Color borderColor = isHovered ? Color.White : palette.Border;
            Color iconColor = isHovered ? Color.White : palette.TextPrimary;

            // Button Card Shadow
            using (var shadowBrush = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
            {
                g.FillRectangle(shadowBrush, r.X + 2, r.Y + 2, r.Width, r.Height);
            }

            using (var path = ZeroUIConfig.CreateRoundedRectangle(r, 6))
            {
                using var brush = new SolidBrush(bgColor);
                g.FillPath(brush, path);
                using var pen = new Pen(borderColor, 1.5f);
                g.DrawPath(pen, path);
            }

            // Vector Glyphs
            using var iconPen = new Pen(iconColor, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            int cx = r.X + r.Width / 2;
            int cy = r.Y + r.Height / 2;

            switch (pos)
            {
                case DockPosition.Document:
                    g.DrawRectangle(iconPen, cx - 7, cy - 7, 14, 14);
                    g.DrawLine(iconPen, cx - 7, cy - 3, cx + 7, cy - 3);
                    break;

                case DockPosition.Left:
                    g.DrawRectangle(iconPen, cx - 7, cy - 7, 14, 14);
                    using (var b = new SolidBrush(iconColor))
                        g.FillRectangle(b, cx - 7, cy - 7, 5, 14);
                    break;

                case DockPosition.Right:
                    g.DrawRectangle(iconPen, cx - 7, cy - 7, 14, 14);
                    using (var b = new SolidBrush(iconColor))
                        g.FillRectangle(b, cx + 2, cy - 7, 5, 14);
                    break;

                case DockPosition.Top:
                    g.DrawRectangle(iconPen, cx - 7, cy - 7, 14, 14);
                    using (var b = new SolidBrush(iconColor))
                        g.FillRectangle(b, cx - 7, cy - 7, 14, 5);
                    break;

                case DockPosition.Bottom:
                    g.DrawRectangle(iconPen, cx - 7, cy - 7, 14, 14);
                    using (var b = new SolidBrush(iconColor))
                        g.FillRectangle(b, cx - 7, cy + 2, 14, 5);
                    break;
            }
        }
    }

    [Obsolete("Use DockGuideHUD instead.")]
    internal class ZeroDockGuideHUD : DockGuideHUD
    {
        public ZeroDockGuideHUD(DockManager manager) : base(manager) { }
    }
}
