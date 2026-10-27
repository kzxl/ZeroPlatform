using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Theme
{
    public enum ZeroCornerStyle
    {
        Rounded,    // Modern rounded corners (default)
        Sharp,      // Square 90-degree flat corners (Enterprise dense ERP style)
        Pill        // Highly rounded pill styling
    }

    /// <summary>
    /// Global application-wide configuration for ZeroUI.
    /// Provides centralized management for global font, corner rounding style, border radius, and theme tokens.
    /// </summary>
    public static class ZeroUIConfig
    {
        private static Font _defaultFont = new Font("Segoe UI", 9.25f, FontStyle.Regular);
        private static ZeroCornerStyle _cornerStyle = ZeroCornerStyle.Rounded;
        private static int _defaultBorderRadius = 6;
        private static bool _enableAntiAliasing = true;

        public static event EventHandler? ConfigChanged;
        public static event EventHandler? CornerStyleChanged;
        public static event EventHandler? FontChanged;

        private static System.Windows.Forms.Timer? _cornerAnimTimer;
        private static float _animCurrentRadius = 6f;
        private static float _animTargetRadius = 6f;

        /// <summary>
        /// Global switch for GDI+ anti-aliased rendering.
        /// </summary>
        public static bool EnableAntiAliasing
        {
            get => _enableAntiAliasing;
            set
            {
                if (_enableAntiAliasing != value)
                {
                    _enableAntiAliasing = value;
                    NotifyConfigChanged();
                }
            }
        }

        /// <summary>
        /// Global default font used across all ZeroUI controls.
        /// </summary>
        public static Font DefaultFont
        {
            get => _defaultFont;
            set
            {
                if (value != null && _defaultFont != value)
                {
                    _defaultFont = value;
                    FontChanged?.Invoke(null, EventArgs.Empty);
                    NotifyConfigChanged();
                }
            }
        }

        /// <summary>
        /// Global corner styling: Rounded (default), Sharp (flat 90° corners), or Pill.
        /// </summary>
        public static ZeroCornerStyle CornerStyle
        {
            get => _cornerStyle;
            set
            {
                if (_cornerStyle != value)
                {
                    _cornerStyle = value;
                    CornerStyleChanged?.Invoke(null, EventArgs.Empty);
                    NotifyConfigChanged();
                }
            }
        }

        /// <summary>
        /// Quick toggle whether rounded corners are enabled globally.
        /// When false, controls render with sharp 90-degree corners with zero corner clipping.
        /// </summary>
        public static bool RoundedCorners
        {
            get => _cornerStyle != ZeroCornerStyle.Sharp && _defaultBorderRadius > 0;
            set
            {
                var target = value ? ZeroCornerStyle.Rounded : ZeroCornerStyle.Sharp;
                if (_cornerStyle != target)
                {
                    _cornerStyle = target;
                    if (!value) _defaultBorderRadius = 0;
                    else if (_defaultBorderRadius == 0) _defaultBorderRadius = 6;
                    CornerStyleChanged?.Invoke(null, EventArgs.Empty);
                    NotifyConfigChanged();
                }
            }
        }

        /// <summary>
        /// Default border radius when CornerStyle is Rounded.
        /// </summary>
        public static int DefaultBorderRadius
        {
            get => _defaultBorderRadius;
            set
            {
                int val = Math.Max(0, value);
                if (_defaultBorderRadius != val)
                {
                    _defaultBorderRadius = val;
                    CornerStyleChanged?.Invoke(null, EventArgs.Empty);
                    NotifyConfigChanged();
                }
            }
        }

        /// <summary>
        /// Smoothly transitions the global corner radius between Rounded and Sharp (or a target radius)
        /// with a 60 FPS animation over ~130 milliseconds for a silky smooth user experience.
        /// </summary>
        public static void ToggleRoundedCornersAnimated(Form? parentForm = null, Action? onCompleted = null)
        {
            bool targetRounded = !RoundedCorners;
            float targetRadius = targetRounded ? 6f : 0f;
            AnimateCornerRadius(targetRadius, parentForm, () =>
            {
                _cornerStyle = targetRounded ? ZeroCornerStyle.Rounded : ZeroCornerStyle.Sharp;
                _defaultBorderRadius = (int)Math.Round(targetRadius);
                CornerStyleChanged?.Invoke(null, EventArgs.Empty);
                onCompleted?.Invoke();
            });
        }

        /// <summary>
        /// Animates the corner radius to a specific target radius with smooth ease-out quadratic interpolation.
        /// </summary>
        public static void AnimateCornerRadius(float targetRadius, Form? parentForm = null, Action? onCompleted = null)
        {
            Form? targetForm = parentForm ?? (Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null);
            _animTargetRadius = targetRadius;
            _animCurrentRadius = _cornerStyle == ZeroCornerStyle.Sharp ? 0f : _defaultBorderRadius;

            if (Math.Abs(_animCurrentRadius - _animTargetRadius) < 0.1f)
            {
                _defaultBorderRadius = (int)Math.Round(_animTargetRadius);
                _cornerStyle = _defaultBorderRadius == 0 ? ZeroCornerStyle.Sharp : ZeroCornerStyle.Rounded;
                CornerStyleChanged?.Invoke(null, EventArgs.Empty);
                targetForm?.Invalidate(true);
                onCompleted?.Invoke();
                return;
            }

            // Temporarily set to Rounded style during animation so GetEffectiveRadius scales
            _cornerStyle = ZeroCornerStyle.Rounded;

            if (_cornerAnimTimer != null)
            {
                _cornerAnimTimer.Stop();
                _cornerAnimTimer.Dispose();
                _cornerAnimTimer = null;
            }

            int stepCount = 0;
            const int maxSteps = 8; // 8 frames * 16ms = ~130ms snappy animation
            float startRadius = _animCurrentRadius;

            _cornerAnimTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _cornerAnimTimer.Tick += (s, e) =>
            {
                stepCount++;
                float progress = Math.Min(1.0f, (float)stepCount / maxSteps);

                // Ease-out quadratic: fast start, soft stop
                float ease = 1.0f - ((1.0f - progress) * (1.0f - progress));
                _animCurrentRadius = startRadius + ((_animTargetRadius - startRadius) * ease);
                _defaultBorderRadius = Math.Max(0, (int)Math.Round(_animCurrentRadius));

                CornerStyleChanged?.Invoke(null, EventArgs.Empty);
                targetForm?.Invalidate(true);

                if (stepCount >= maxSteps)
                {
                    _cornerAnimTimer.Stop();
                    _cornerAnimTimer.Dispose();
                    _cornerAnimTimer = null;

                    _defaultBorderRadius = (int)Math.Round(_animTargetRadius);
                    _cornerStyle = _defaultBorderRadius == 0 ? ZeroCornerStyle.Sharp : ZeroCornerStyle.Rounded;
                    CornerStyleChanged?.Invoke(null, EventArgs.Empty);
                    targetForm?.Invalidate(true);
                    onCompleted?.Invoke();
                }
            };

            _cornerAnimTimer.Start();
        }

        /// <summary>
        /// Calculates effective radius taking global CornerStyle into account.
        /// Scales proportionally relative to base 6px so badges, modals, and buttons maintain harmony.
        /// </summary>
        public static int GetEffectiveRadius(int localRadius)
        {
            if (_cornerStyle == ZeroCornerStyle.Sharp || localRadius <= 0) return 0;
            if (_defaultBorderRadius <= 0) return 0;
            if (_cornerStyle == ZeroCornerStyle.Pill) return Math.Max(localRadius, 16);

            // Proportional scaling relative to base 6px
            return (int)Math.Round(localRadius * (_defaultBorderRadius / 6.0));
        }

        /// <summary>
        /// Helper to retrieve the parent container's solid background color,
        /// completely eliminating black triangular corner clipping artifacts.
        /// </summary>
        public static Color GetParentBackground(Control control, Color fallback)
        {
            if (control == null) return fallback;
            Control? parent = control.Parent;
            while (parent != null)
            {
                if (parent.BackColor != Color.Transparent && parent.BackColor.A == 255)
                {
                    return parent.BackColor;
                }
                parent = parent.Parent;
            }
            return fallback;
        }

        public static void NotifyConfigChanged()
        {
            ConfigChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Creates a high-precision rounded rectangle GraphicsPath with perfectly balanced arcs on all 4 corners.
        /// </summary>
        public static GraphicsPath CreateRoundedRectangle(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0 || r.Width <= 0 || r.Height <= 0)
            {
                path.AddRectangle(r);
                return path;
            }

            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Creates a floating-point high-precision rounded rectangle GraphicsPath.
        /// </summary>
        public static GraphicsPath CreateRoundedRectangleF(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0 || r.Width <= 0 || r.Height <= 0)
            {
                path.AddRectangle(r);
                return path;
            }

            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Creates a high-precision rounded rectangle with only top corners rounded (e.g. for card tabs, dialog headers).
        /// Safely handles radius <= 0.
        /// </summary>
        public static GraphicsPath CreateTopRoundedRectangle(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0 || r.Width <= 0 || r.Height <= 0)
            {
                path.AddRectangle(r);
                return path;
            }

            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
            path.CloseFigure();
            return path;
        }
    }
}
