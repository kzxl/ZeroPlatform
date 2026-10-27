using System;
using System.Collections.Generic;
using System.Globalization;

namespace ZeroUI.Wpf.Media
{
    /// <summary>
    /// Pure mathematical spline engine for tone curves and transfer functions.
    /// Implements Fritsch-Carlson monotone cubic Hermite spline interpolation (guarantees monotonicity without overshoot).
    /// Generates precomputed 256-level lookup tables for O(1) evaluation.
    /// Fully multi-target compatible across .NET Framework 4.6.2, .NET Standard 2.0, and modern .NET.
    /// </summary>
    public static class CurveMath
    {
        public const int LutSize = 256;

        /// <summary>
        /// Normalizes points list: clamps x, y to [0..1], sorts along x ascending, and ensures at least 2 points (0,0) and (1,1).
        /// </summary>
        public static List<(float x, float y)> Normalize(IReadOnlyList<(float x, float y)>? points)
        {
            var list = new List<(float x, float y)>();
            if (points != null)
            {
                foreach (var p in points)
                {
                    list.Add((Clamp(p.x, 0f, 1f), Clamp(p.y, 0f, 1f)));
                }
            }

            if (list.Count < 2)
            {
                return new List<(float x, float y)> { (0f, 0f), (1f, 1f) };
            }

            list.Sort((a, b) => a.x.CompareTo(b.x));
            return list;
        }

        /// <summary>
        /// Determines whether the given normalized curve is an exact linear identity diagonal (0,0) -> (1,1).
        /// </summary>
        public static bool IsIdentity(IReadOnlyList<(float x, float y)> normalized)
        {
            return normalized != null &&
                   normalized.Count == 2 &&
                   Near(normalized[0].x, 0f) && Near(normalized[0].y, 0f) &&
                   Near(normalized[1].x, 1f) && Near(normalized[1].y, 1f);
        }

        /// <summary>
        /// Builds a 256-bin lookup table using Fritsch-Carlson monotone cubic Hermite spline interpolation.
        /// </summary>
        public static float[] BuildLut(IReadOnlyList<(float x, float y)>? points)
        {
            var pts = Normalize(points);
            int n = pts.Count;
            var xs = new float[n];
            var ys = new float[n];
            for (int i = 0; i < n; i++)
            {
                xs[i] = pts[i].x;
                ys[i] = pts[i].y;
            }

            var d = new float[n - 1];
            var m = new float[n];
            for (int i = 0; i < n - 1; i++)
            {
                float dx = xs[i + 1] - xs[i];
                d[i] = dx > 1e-6f ? (ys[i + 1] - ys[i]) / dx : 0f;
            }

            m[0] = d[0];
            m[n - 1] = d[n - 2];
            for (int i = 1; i < n - 1; i++)
            {
                m[i] = (d[i - 1] * d[i] <= 0f) ? 0f : (d[i - 1] + d[i]) / 2f;
            }

            for (int i = 0; i < n - 1; i++)
            {
                if (Near(d[i], 0f))
                {
                    m[i] = 0f;
                    m[i + 1] = 0f;
                    continue;
                }

                float a = m[i] / d[i];
                float b = m[i + 1] / d[i];
                float hyp = a * a + b * b;
                if (hyp > 9f)
                {
                    float t = 3f / (float)Math.Sqrt(hyp);
                    m[i] = t * a * d[i];
                    m[i + 1] = t * b * d[i];
                }
            }

            var lut = new float[LutSize];
            int seg = 0;
            for (int k = 0; k < LutSize; k++)
            {
                float x = k / (float)(LutSize - 1);
                while (seg < n - 2 && x > xs[seg + 1])
                {
                    seg++;
                }

                float x0 = xs[seg];
                float x1 = xs[seg + 1];
                float h = x1 - x0;
                float y;
                if (h <= 1e-6f)
                {
                    y = ys[seg];
                }
                else
                {
                    float t = (x - x0) / h;
                    float t2 = t * t;
                    float t3 = t2 * t;
                    float h00 = 2f * t3 - 3f * t2 + 1f;
                    float h10 = t3 - 2f * t2 + t;
                    float h01 = -2f * t3 + 3f * t2;
                    float h11 = t3 - t2;
                    y = h00 * ys[seg] + h10 * h * m[seg] + h01 * ys[seg + 1] + h11 * h * m[seg + 1];
                }

                lut[k] = Clamp(y, 0f, 1f);
            }

            return lut;
        }

        /// <summary>
        /// Evaluates the curve at input position x [0..1] with linear interpolation between LUT entries.
        /// </summary>
        public static float Eval(float[] lut, float x)
        {
            if (lut == null || lut.Length == 0) return 0f;
            if (x <= 0f) return lut[0];
            if (x >= 1f) return lut[lut.Length - 1];

            float fx = x * (lut.Length - 1);
            int i = (int)fx;
            float frac = fx - i;
            if (i >= lut.Length - 1) return lut[lut.Length - 1];
            return lut[i] + (lut[i + 1] - lut[i]) * frac;
        }

        /// <summary>
        /// Serializes point list to culture-invariant formatted string: "x0,y0;x1,y1;...".
        /// </summary>
        public static string Serialize(IReadOnlyList<(float x, float y)> points)
        {
            var pts = Normalize(points);
            var parts = new string[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                parts[i] = string.Format(CultureInfo.InvariantCulture, "{0:R},{1:R}", pts[i].x, pts[i].y);
            }
            return string.Join(";", parts);
        }

        /// <summary>
        /// Parses a serialized point string ("x0,y0;x1,y1;..."). Returns null if invalid or less than 2 valid points.
        /// </summary>
        public static List<(float x, float y)>? Parse(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;

            var list = new List<(float x, float y)>();
            var pairs = s!.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var xy = pair.Split(',');
                if (xy.Length != 2) continue;

                if (float.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                    float.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                {
                    list.Add((x, y));
                }
            }

            return list.Count >= 2 ? list : null;
        }

        private static float Clamp(float val, float min, float max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }

        private static bool Near(float a, float b = 0f) => Math.Abs(a - b) < 1e-4f;
    }
}
