using System;

using ZeroUI.WinForms.Icons;using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Feedback
{
    public enum SkeletonShape
    {
        Rectangle,
        RoundedRectangle,
        Circle,
        Text
    }

    /// <summary>
    /// Modern anti-aliased Skeleton/Shimmer loading placeholder for ZeroUI WinForms.
    /// Provides smooth animated gradient shimmer waves imitating UI cards, text, and avatars
    /// while background data operations resolve.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Feedback")]
    [Description("Animated shimmer placeholder placeholder for loading states")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroSkeleton.bmp")]
    public class ZSkeleton : Control
    {
        private SkeletonShape _shape = SkeletonShape.RoundedRectangle;
        private int _cornerRadius = 6;
        private float _shimmerProgress = 0f;
        private readonly Timer _animationTimer;

        [Category("Appearance")]
        [DefaultValue(SkeletonShape.RoundedRectangle)]
        public SkeletonShape Shape
        {
            get => _shape;
            set { _shape = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(6)]
        public int CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = Math.Max(0, value); Invalidate(); }
        }

        public ZSkeleton()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Size = new Size(160, 24);

            _animationTimer = new Timer { Interval = 25 };
            _animationTimer.Tick += (s, e) =>
            {
                _shimmerProgress += 0.04f;
                if (_shimmerProgress > 1.5f) _shimmerProgress = -0.5f;
                Invalidate();
            };

            if (!DesignMode)
            {
                _animationTimer.Start();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer?.Stop();
                _animationTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var colors = ZeroTheme.Colors;
            Color baseColor = colors.HeaderBackground;
            Color shimmerColor = colors.Surface;

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            GraphicsPath? path = null;
            if (_shape == SkeletonShape.Circle)
            {
                int d = Math.Min(bounds.Width, bounds.Height);
                path = new GraphicsPath();
                path.AddEllipse((bounds.Width - d) / 2, (bounds.Height - d) / 2, d, d);
            }
            else if (_shape == SkeletonShape.Text)
            {
                int textH = Math.Min(14, bounds.Height);
                int y = (bounds.Height - textH) / 2;
                path = CreateRoundedRectanglePath(new Rectangle(0, y, bounds.Width, textH), 3);
            }
            else if (_shape == SkeletonShape.RoundedRectangle)
            {
                path = CreateRoundedRectanglePath(bounds, _cornerRadius);
            }

            // Draw base shape
            using (var baseBrush = new SolidBrush(baseColor))
            {
                if (path != null) g.FillPath(baseBrush, path);
                else g.FillRectangle(baseBrush, bounds);
            }

            // Draw animated Shimmer Wave
            float waveWidth = Math.Max(60, Width * 0.4f);
            float waveX = Width * _shimmerProgress;
            var waveRect = new RectangleF(waveX - waveWidth / 2f, 0, waveWidth, Height);

            if (waveRect.Right > 0 && waveRect.Left < Width)
            {
                using (var lgb = new LinearGradientBrush(
                    new PointF(waveRect.Left, 0),
                    new PointF(waveRect.Right, 0),
                    Color.FromArgb(0, shimmerColor),
                    Color.FromArgb(140, shimmerColor)))
                {
                    var cb = new ColorBlend(3)
                    {
                        Colors = new[] { Color.FromArgb(0, shimmerColor), Color.FromArgb(140, shimmerColor), Color.FromArgb(0, shimmerColor) },
                        Positions = new[] { 0f, 0.5f, 1f }
                    };
                    lgb.InterpolationColors = cb;

                    if (path != null)
                    {
                        var oldClip = g.Clip;
                        g.SetClip(path);
                        g.FillRectangle(lgb, waveRect);
                        g.Clip = oldClip;
                    }
                    else
                    {
                        g.FillRectangle(lgb, waveRect);
                    }
                }
            }

            path?.Dispose();
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    [Obsolete("Use SkeletonControl instead.")]
    [ToolboxItem(false)]
    public class ZeroSkeleton : SkeletonControl
    {
    }

    [Obsolete("Use SkeletonControl instead.")]
    [ToolboxItem(false)]
    public class ZeroSkeletonControl : ZSkeleton
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSkeleton"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SkeletonControl is deprecated and will be removed in 5 release cycles. Please migrate to ZSkeleton instead.")]
    [ToolboxItem(false)]
    public class SkeletonControl : ZSkeleton
    {
    }

    #endregion
}
