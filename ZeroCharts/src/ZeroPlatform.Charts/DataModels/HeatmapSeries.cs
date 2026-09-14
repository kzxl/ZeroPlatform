using System;
using System.Drawing;

namespace ZeroPlatform.Charts.DataModels
{
    /// <summary>
    /// 2D scalar field heatmap series for thermal imaging and sensor grids.
    /// </summary>
    public class HeatmapSeries
    {
        private double[,] _data;

        public string Title { get; set; }
        public int Width { get; }
        public int Height { get; }
        public double MinValue { get; private set; }
        public double MaxValue { get; private set; }
        public bool IsVisible { get; set; } = true;

        public double this[int x, int y] => _data[x, y];

        public HeatmapSeries(int width, int height, string title = "Thermal Heatmap")
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
            Title = title;
            _data = new double[width, height];
            MinValue = double.MaxValue;
            MaxValue = double.MinValue;
        }

        public void SetData(double[,] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.GetLength(0) != Width || data.GetLength(1) != Height)
                throw new ArgumentException($"Data dimensions {data.GetLength(0)}x{data.GetLength(1)} do not match heatmap size {Width}x{Height}.");

            _data = (double[,])data.Clone();
            RecalculateBounds();
        }

        public void SetValue(int x, int y, double val)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return;
            _data[x, y] = val;
            if (val < MinValue) MinValue = val;
            if (val > MaxValue) MaxValue = val;
        }

        private void RecalculateBounds()
        {
            MinValue = double.MaxValue;
            MaxValue = double.MinValue;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    double v = _data[x, y];
                    if (v < MinValue) MinValue = v;
                    if (v > MaxValue) MaxValue = v;
                }
            }
        }

        /// <summary>
        /// Maps a scalar value to a standard industrial thermal gradient color (Blue -> Cyan -> Green -> Yellow -> Red).
        /// </summary>
        public Color GetThermalColor(double value)
        {
            if (MaxValue <= MinValue) return Color.FromArgb(0, 0, 255);

            double norm = (value - MinValue) / (MaxValue - MinValue);
            norm = Math.Max(0.0, Math.Min(1.0, norm));

            byte r = 0, g = 0, b = 0;
            if (norm < 0.25)
            {
                // Blue to Cyan
                double t = norm / 0.25;
                b = 255;
                g = (byte)(t * 255);
            }
            else if (norm < 0.5)
            {
                // Cyan to Green
                double t = (norm - 0.25) / 0.25;
                g = 255;
                b = (byte)((1.0 - t) * 255);
            }
            else if (norm < 0.75)
            {
                // Green to Yellow
                double t = (norm - 0.5) / 0.25;
                g = 255;
                r = (byte)(t * 255);
            }
            else
            {
                // Yellow to Red
                double t = (norm - 0.75) / 0.25;
                r = 255;
                g = (byte)((1.0 - t) * 255);
            }

            return Color.FromArgb(r, g, b);
        }
    }
}
