using System;

namespace ZeroGraphics.Vision.Matching
{
    /// <summary>
    /// Represents the result of a template matching operation.
    /// Supports sub-pixel precision coordinates and normalized correlation score.
    /// </summary>
    public readonly struct TemplateMatchResult
    {
        /// <summary>
        /// X-coordinate of the top-left corner (sub-pixel precision).
        /// </summary>
        public double X { get; }

        /// <summary>
        /// Y-coordinate of the top-left corner (sub-pixel precision).
        /// </summary>
        public double Y { get; }

        /// <summary>
        /// Width of the matched template.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Height of the matched template.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Normalized Cross-Correlation score in range [-1.0, 1.0].
        /// 1.0 represents a perfect identical match.
        /// </summary>
        public double Score { get; }

        /// <summary>
        /// X-coordinate of the matched pattern's center.
        /// </summary>
        public double CenterX => X + Width * 0.5;

        /// <summary>
        /// Y-coordinate of the matched pattern's center.
        /// </summary>
        public double CenterY => Y + Height * 0.5;

        /// <summary>
        /// Whether a valid match was found.
        /// </summary>
        public bool IsFound => Score > -1.0;

        public static TemplateMatchResult NotFound => new TemplateMatchResult(0, 0, 0, 0, -1.0);

        public TemplateMatchResult(double x, double y, int width, int height, double score)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Score = score;
        }

        public override string ToString()
            => $"[Match] Center=({CenterX:F2}, {CenterY:F2}), Size={Width}x{Height}, Score={Score:F4}";
    }
}
