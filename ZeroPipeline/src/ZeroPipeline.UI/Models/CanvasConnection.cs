using System;
using System.Drawing;

namespace ZeroPipeline.UI.Models
{
    /// <summary>
    /// Visual connection wire (cubic Bezier noodle) between a source output pin and target input pin.
    /// Supports selection, hover detection, and distance-to-curve hit testing.
    /// </summary>
    public sealed class CanvasConnection
    {
        public CanvasPortPin SourcePin { get; }
        public CanvasPortPin TargetPin { get; }
        public bool IsSelected { get; set; }
        public bool IsHovered { get; set; }

        public CanvasConnection(CanvasPortPin sourcePin, CanvasPortPin targetPin)
        {
            SourcePin = sourcePin ?? throw new ArgumentNullException(nameof(sourcePin));
            TargetPin = targetPin ?? throw new ArgumentNullException(nameof(targetPin));
        }

        public void GetControlPoints(out PointF p0, out PointF p1, out PointF p2, out PointF p3)
        {
            p0 = SourcePin.GetWorldCenter();
            p3 = TargetPin.GetWorldCenter();
            ComputeBezierPoints(p0, p3, out p1, out p2);
        }

        public static void ComputeBezierPoints(PointF p0, PointF p3, out PointF p1, out PointF p2)
        {
            float dx = Math.Max(40.0f, Math.Abs(p3.X - p0.X) * 0.5f);
            p1 = new PointF(p0.X + dx, p0.Y);
            p2 = new PointF(p3.X - dx, p3.Y);
        }

        public static PointF EvaluateBezier(PointF p0, PointF p1, PointF p2, PointF p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            float x = uuu * p0.X + 3f * uu * t * p1.X + 3f * u * tt * p2.X + ttt * p3.X;
            float y = uuu * p0.Y + 3f * uu * t * p1.Y + 3f * u * tt * p2.Y + ttt * p3.Y;
            return new PointF(x, y);
        }

        public float DistanceToPoint(PointF pt, int samples = 24)
        {
            GetControlPoints(out var p0, out var p1, out var p2, out var p3);
            float minDistanceSq = float.MaxValue;
            PointF prev = p0;

            for (int i = 1; i <= samples; i++)
            {
                float t = i / (float)samples;
                PointF curr = EvaluateBezier(p0, p1, p2, p3, t);

                float distSq = DistanceToSegmentSquared(pt, prev, curr);
                if (distSq < minDistanceSq)
                {
                    minDistanceSq = distSq;
                }
                prev = curr;
            }

            return (float)Math.Sqrt(minDistanceSq);
        }

        public bool Hits(PointF pt, float tolerance = 6.0f)
        {
            return DistanceToPoint(pt) <= tolerance;
        }

        private static float DistanceToSegmentSquared(PointF p, PointF v, PointF w)
        {
            float l2 = (w.X - v.X) * (w.X - v.X) + (w.Y - v.Y) * (w.Y - v.Y);
            if (l2 < 0.0001f)
            {
                float dx0 = p.X - v.X;
                float dy0 = p.Y - v.Y;
                return dx0 * dx0 + dy0 * dy0;
            }

            float t = Math.Max(0f, Math.Min(1f, ((p.X - v.X) * (w.X - v.X) + (p.Y - v.Y) * (w.Y - v.Y)) / l2));
            float projX = v.X + t * (w.X - v.X);
            float projY = v.Y + t * (w.Y - v.Y);

            float dx = p.X - projX;
            float dy = p.Y - projY;
            return dx * dx + dy * dy;
        }

        public override string ToString() =>
            $"{SourcePin.OwnerNode.Name}.{SourcePin.Name} -> {TargetPin.OwnerNode.Name}.{TargetPin.Name}";
    }
}
