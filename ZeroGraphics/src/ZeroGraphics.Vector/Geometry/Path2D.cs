using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ZeroGraphics.Vector.Geometry
{
    /// <summary>
    /// Sovereign 2D geometric path container holding subpaths of MoveTo, LineTo, QuadTo, CubicTo, and Close verbs.
    /// Fully cross-platform with 0 native dependencies, supporting SVG path serialization and bounds evaluation.
    /// </summary>
    public sealed class Path2D
    {
        private const float CircleKappa = 0.5522847498307935f;

        private readonly List<PathVerb> _verbs;
        private readonly List<VectorPoint> _points;

        public IReadOnlyList<PathVerb> Verbs => _verbs;
        public IReadOnlyList<VectorPoint> Points => _points;

        public int VerbCount => _verbs.Count;
        public int PointCount => _points.Count;

        public Path2D()
        {
            _verbs = new List<PathVerb>();
            _points = new List<VectorPoint>();
        }

        public Path2D(int initialCapacity)
        {
            _verbs = new List<PathVerb>(initialCapacity);
            _points = new List<VectorPoint>(initialCapacity * 2);
        }

        public Path2D MoveTo(float x, float y)
        {
            _verbs.Add(PathVerb.MoveTo);
            _points.Add(new VectorPoint(x, y));
            return this;
        }

        public Path2D MoveTo(VectorPoint p) => MoveTo(p.X, p.Y);

        public Path2D LineTo(float x, float y)
        {
            if (_verbs.Count == 0)
            {
                MoveTo(0.0f, 0.0f);
            }
            _verbs.Add(PathVerb.LineTo);
            _points.Add(new VectorPoint(x, y));
            return this;
        }

        public Path2D LineTo(VectorPoint p) => LineTo(p.X, p.Y);

        public Path2D QuadTo(float cx, float cy, float x, float y)
        {
            if (_verbs.Count == 0)
            {
                MoveTo(0.0f, 0.0f);
            }
            _verbs.Add(PathVerb.QuadTo);
            _points.Add(new VectorPoint(cx, cy));
            _points.Add(new VectorPoint(x, y));
            return this;
        }

        public Path2D CubicTo(float c1x, float c1y, float c2x, float c2y, float x, float y)
        {
            if (_verbs.Count == 0)
            {
                MoveTo(0.0f, 0.0f);
            }
            _verbs.Add(PathVerb.CubicTo);
            _points.Add(new VectorPoint(c1x, c1y));
            _points.Add(new VectorPoint(c2x, c2y));
            _points.Add(new VectorPoint(x, y));
            return this;
        }

        public Path2D Close()
        {
            if (_verbs.Count > 0 && _verbs[_verbs.Count - 1] != PathVerb.Close)
            {
                _verbs.Add(PathVerb.Close);
            }
            return this;
        }

        public Path2D AddRect(float x, float y, float width, float height)
        {
            MoveTo(x, y);
            LineTo(x + width, y);
            LineTo(x + width, y + height);
            LineTo(x, y + height);
            Close();
            return this;
        }

        public Path2D AddCircle(float cx, float cy, float radius)
        {
            float offset = radius * CircleKappa;

            MoveTo(cx, cy - radius);
            CubicTo(cx + offset, cy - radius, cx + radius, cy - offset, cx + radius, cy);
            CubicTo(cx + radius, cy + offset, cx + offset, cy + radius, cx, cy + radius);
            CubicTo(cx - offset, cy + radius, cx - radius, cy + offset, cx - radius, cy);
            CubicTo(cx - radius, cy - offset, cx - offset, cy - radius, cx, cy - radius);
            Close();
            return this;
        }

        public Path2D AddPath(Path2D other)
        {
            if (other == null || other.VerbCount == 0) return this;

            _verbs.AddRange(other.Verbs);
            _points.AddRange(other.Points);
            return this;
        }

        public void Reset()
        {
            _verbs.Clear();
            _points.Clear();
        }

        /// <summary>
        /// Computes the axis-aligned bounding box enclosing all control points of the path.
        /// </summary>
        public void GetBounds(out float minX, out float minY, out float maxX, out float maxY)
        {
            if (_points.Count == 0)
            {
                minX = minY = maxX = maxY = 0.0f;
                return;
            }

            minX = float.MaxValue;
            minY = float.MaxValue;
            maxX = float.MinValue;
            maxY = float.MinValue;

            for (int i = 0; i < _points.Count; i++)
            {
                var p = _points[i];
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
        }

        /// <summary>
        /// Serializes the path into standard SVG path 'd' attribute syntax.
        /// </summary>
        public string ToSvgPathData(int decimals = 2)
        {
            if (_verbs.Count == 0) return string.Empty;

            var sb = new StringBuilder(_verbs.Count * 32);
            string fmt = "0." + new string('#', decimals);
            int ptIdx = 0;

            for (int i = 0; i < _verbs.Count; i++)
            {
                var verb = _verbs[i];
                if (i > 0) sb.Append(' ');

                switch (verb)
                {
                    case PathVerb.MoveTo:
                        var pMove = _points[ptIdx++];
                        sb.AppendFormat(CultureInfo.InvariantCulture, "M {0} {1}", pMove.X.ToString(fmt, CultureInfo.InvariantCulture), pMove.Y.ToString(fmt, CultureInfo.InvariantCulture));
                        break;

                    case PathVerb.LineTo:
                        var pLine = _points[ptIdx++];
                        sb.AppendFormat(CultureInfo.InvariantCulture, "L {0} {1}", pLine.X.ToString(fmt, CultureInfo.InvariantCulture), pLine.Y.ToString(fmt, CultureInfo.InvariantCulture));
                        break;

                    case PathVerb.QuadTo:
                        var qCtrl = _points[ptIdx++];
                        var qEnd = _points[ptIdx++];
                        sb.AppendFormat(CultureInfo.InvariantCulture, "Q {0} {1}, {2} {3}",
                            qCtrl.X.ToString(fmt, CultureInfo.InvariantCulture), qCtrl.Y.ToString(fmt, CultureInfo.InvariantCulture),
                            qEnd.X.ToString(fmt, CultureInfo.InvariantCulture), qEnd.Y.ToString(fmt, CultureInfo.InvariantCulture));
                        break;

                    case PathVerb.CubicTo:
                        var c1 = _points[ptIdx++];
                        var c2 = _points[ptIdx++];
                        var cEnd = _points[ptIdx++];
                        sb.AppendFormat(CultureInfo.InvariantCulture, "C {0} {1}, {2} {3}, {4} {5}",
                            c1.X.ToString(fmt, CultureInfo.InvariantCulture), c1.Y.ToString(fmt, CultureInfo.InvariantCulture),
                            c2.X.ToString(fmt, CultureInfo.InvariantCulture), c2.Y.ToString(fmt, CultureInfo.InvariantCulture),
                            cEnd.X.ToString(fmt, CultureInfo.InvariantCulture), cEnd.Y.ToString(fmt, CultureInfo.InvariantCulture));
                        break;

                    case PathVerb.Close:
                        sb.Append('Z');
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
