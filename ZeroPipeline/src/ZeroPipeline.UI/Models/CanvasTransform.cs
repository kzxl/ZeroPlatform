using System;
using System.Drawing;

namespace ZeroPipeline.UI.Models
{
    /// <summary>
    /// Manages 2D viewport coordinates, zoom scaling (0.2x to 3.0x), and camera panning for the infinite node canvas.
    /// Provides invertible transformations between World space (virtual coordinates) and Screen space (pixels).
    /// </summary>
    public sealed class CanvasTransform
    {
        public float PanX { get; set; }
        public float PanY { get; set; }
        public float Zoom { get; set; } = 1.0f;

        public const float MinZoom = 0.2f;
        public const float MaxZoom = 3.0f;

        public CanvasTransform(float panX = 40f, float panY = 40f, float zoom = 1.0f)
        {
            PanX = panX;
            PanY = panY;
            Zoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
        }

        public PointF WorldToScreen(PointF worldPoint) =>
            new PointF(worldPoint.X * Zoom + PanX, worldPoint.Y * Zoom + PanY);

        public PointF ScreenToWorld(PointF screenPoint) =>
            new PointF((screenPoint.X - PanX) / Zoom, (screenPoint.Y - PanY) / Zoom);

        public RectangleF WorldToScreen(RectangleF worldRect) =>
            new RectangleF(
                worldRect.X * Zoom + PanX,
                worldRect.Y * Zoom + PanY,
                worldRect.Width * Zoom,
                worldRect.Height * Zoom);

        public RectangleF ScreenToWorld(RectangleF screenRect) =>
            new RectangleF(
                (screenRect.X - PanX) / Zoom,
                (screenRect.Y - PanY) / Zoom,
                screenRect.Width / Zoom,
                screenRect.Height / Zoom);

        public void ZoomAt(PointF screenPivot, float zoomDelta)
        {
            float oldZoom = Zoom;
            float newZoom = Math.Max(MinZoom, Math.Min(MaxZoom, oldZoom * zoomDelta));

            if (Math.Abs(newZoom - oldZoom) < 0.001f) return;

            // Maintain world position under the mouse pivot
            float worldPivotX = (screenPivot.X - PanX) / oldZoom;
            float worldPivotY = (screenPivot.Y - PanY) / oldZoom;

            Zoom = newZoom;
            PanX = screenPivot.X - worldPivotX * newZoom;
            PanY = screenPivot.Y - worldPivotY * newZoom;
        }

        public void Pan(float dx, float dy)
        {
            PanX += dx;
            PanY += dy;
        }

        public void Reset(float panX = 40f, float panY = 40f, float zoom = 1.0f)
        {
            PanX = panX;
            PanY = panY;
            Zoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
        }
    }
}
