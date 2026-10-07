using System;

namespace ZeroScan3D.Core.Camera
{
    /// <summary>
    /// Represents an RGB-D synchronized frame containing depth and optional RGB color channels.
    /// </summary>
    public sealed class RgbdFrame
    {
        private readonly float[] _depth;
        private readonly byte[]? _rgb; // 3 bytes per pixel (R, G, B)

        public int Width { get; }
        public int Height { get; }
        public long FrameId { get; }
        public double TimestampSeconds { get; }
        public bool HasColor => _rgb != null;

        public ReadOnlySpan<float> DepthSpan => _depth;

        public RgbdFrame(
            int width,
            int height,
            float[] depth,
            byte[]? rgb = null,
            long frameId = 0,
            double timestampSeconds = 0.0)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (depth == null) throw new ArgumentNullException(nameof(depth));
            if (depth.Length != width * height)
                throw new ArgumentException($"Depth buffer length ({depth.Length}) must equal width*height ({width * height}).");

            if (rgb != null && rgb.Length != width * height * 3)
                throw new ArgumentException($"RGB buffer length ({rgb.Length}) must equal width*height*3 ({width * height * 3}).");

            Width = width;
            Height = height;
            _depth = depth;
            _rgb = rgb;
            FrameId = frameId;
            TimestampSeconds = timestampSeconds;
        }

        /// <summary>
        /// Creates an RgbdFrame from 16-bit unsigned depth data (e.g. millimeters) converting directly to raw depth float units.
        /// </summary>
        public static RgbdFrame FromUshort(
            int width,
            int height,
            ushort[] rawDepth,
            byte[]? rgb = null,
            long frameId = 0,
            double timestampSeconds = 0.0)
        {
            if (rawDepth == null) throw new ArgumentNullException(nameof(rawDepth));
            if (rawDepth.Length != width * height)
                throw new ArgumentException("rawDepth length does not match width*height.");

            float[] floatDepth = new float[rawDepth.Length];
            for (int i = 0; i < rawDepth.Length; i++)
            {
                floatDepth[i] = rawDepth[i];
            }

            return new RgbdFrame(width, height, floatDepth, rgb, frameId, timestampSeconds);
        }

        public float GetDepth(int u, int v)
        {
            if (u < 0 || u >= Width || v < 0 || v >= Height) return 0f;
            return _depth[v * Width + u];
        }

        public RgbColor GetColor(int u, int v)
        {
            if (_rgb == null || u < 0 || u >= Width || v < 0 || v >= Height)
                return RgbColor.White;

            int idx = (v * Width + u) * 3;
            return new RgbColor(_rgb[idx], _rgb[idx + 1], _rgb[idx + 2]);
        }
    }
}
